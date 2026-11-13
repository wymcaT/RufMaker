using System;
using System.Drawing;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

// One mailbox per analysis request. Worker callbacks replace one immutable value;
// only the UI timer reads it and updates controls. Closing drops late callbacks.
internal sealed class LifetimeProgressMailbox
{
    private readonly object sync = new object();
    private readonly Func<double> seconds;
    private LifetimeProgress latest;
    private double lastReportSeconds;
    private bool cancelling, closed;

    internal LifetimeProgressMailbox(int requestedEnd, double maxSeconds, Func<double> elapsed = null)
    {
        var clock = Stopwatch.StartNew();
        seconds = elapsed ?? (() => clock.Elapsed.TotalSeconds);
        latest = new LifetimeProgress(-1, requestedEnd, 0, maxSeconds, -1, "prepare");
        lastReportSeconds = seconds();
    }
    internal void Report(LifetimeProgress value)
    {
        if (value == null) return;
        lock (sync)
        {
            if (closed || value.RequestedEnd != latest.RequestedEnd || value.LastCompletedFrame < latest.LastCompletedFrame) return;
            latest = value;
            lastReportSeconds = seconds();
        }
    }
    internal void RequestCancel() { lock (sync) { if (!closed) cancelling = true; } }
    internal void Close() { lock (sync) { closed = true; } }
    internal LifetimeProgressState Read()
    {
        lock (sync)
        {
            double since = Math.Max(0, seconds() - lastReportSeconds);
            return new LifetimeProgressState(latest, cancelling, latest.ElapsedSeconds + since, since);
        }
    }
}
internal sealed class LifetimeProgressState
{
    internal readonly LifetimeProgress Progress;
    internal readonly bool Cancelling;
    internal readonly double ElapsedSeconds, SinceCheckpoint;
    internal LifetimeProgressState(LifetimeProgress progress, bool cancelling, double elapsedSeconds, double sinceCheckpoint)
    { Progress = progress; Cancelling = cancelling; ElapsedSeconds = elapsedSeconds; SinceCheckpoint = sinceCheckpoint; }
    internal int CompletedFrames { get { return Math.Max(0, Math.Min(Progress.RequestedEnd + 1, Progress.LastCompletedFrame + 1)); } }
    internal int RequestedFrames { get { return Progress.RequestedEnd + 1; } }
}
internal static class LifetimeProgressText
{
    internal static string Summary(int lastFrame, int requestedEnd, double elapsedSeconds, double maxSeconds)
    {
        return String.Format(Language.Text("已完成 {0} / {1} 帧（请求 0–{2}）；已用 {3:F1} 秒 / 软时限 {4:F0} 秒。"),
            Math.Max(0, lastFrame + 1), requestedEnd + 1, requestedEnd, elapsedSeconds, maxSeconds);
    }
    internal static string Running(LifetimeProgressState value)
    {
        var progress = value.Progress;
        string stage = value.Cancelling ? Language.Text("正在取消；等待分析检查点…") :
            progress.Phase == "prepare" ? Language.Text("正在准备完整 C# 场景…") :
            progress.Phase != "simulate" && progress.Phase != "collect" ? Language.Text("正在整理观察结果…") :
            progress.ActiveFrame > progress.LastCompletedFrame ? String.Format(Language.Text("正在处理第 {0} 帧（尚未完成）…"), progress.ActiveFrame) :
            Language.Text("正在分析完整 C# 场景…");
        string text = stage + "\r\n" + Summary(progress.LastCompletedFrame, progress.RequestedEnd, value.ElapsedSeconds, progress.MaxSeconds);
        if (value.SinceCheckpoint >= 1 && (progress.Phase == "prepare" || progress.Phase == "simulate" || progress.Phase == "collect"))
            text += "\r\n" + String.Format(Language.Text("最近检查点距今 {0:F1} 秒；当前工作尚未返回检查点。"), value.SinceCheckpoint);
        return text;
    }
    internal static string Finished(LifetimeResult value, double maxSeconds)
    {
        string text = (value.LastFrame < 0 ? Language.Text("尚无完整帧；") : "") + Language.Text(SimulatedLifetime.StopText(value.Stop)) + " · " +
            Language.Text(value.Complete ? "仅本次 C# 观察窗口" : "部分观察，后续未知") + "\r\n" +
            Summary(value.LastFrame, value.RequestedEnd, value.Seconds, maxSeconds);
        if (!value.Complete && maxSeconds < 180 && (value.Stop == "time-limit" || value.Stop == "work-limit" || value.Stop == "sample-limit"))
            text += "\r\n" + Language.Text("可提高软时限后重新分析（从第 0 帧开始）；仍受资源上限限制。");
        return text;
    }
}

internal delegate LifetimeResult LifetimeAnalysisRunner(LifetimeSnapshot input, LifetimeOptions options, CancellationToken cancellation, Action<LifetimeProgress> progress);

internal sealed class SimulatedLifetimeView : Form
{
    private readonly LifetimeSnapshot snapshot;private readonly Func<bool> current;
    private readonly LifetimeAnalysisRunner analyze;
    private readonly LifetimeRequestGate gate=new LifetimeRequestGate();
    private CancellationTokenSource cancellation;private bool running,invalidated,closing;
    private LifetimeProgressMailbox progressMailbox;private int activeRequest;private double requestedBudget;
    private readonly System.Windows.Forms.Timer validity=new System.Windows.Forms.Timer{Interval=200};
    private readonly NumericUpDown budget=new WheelSafeNumericUpDown{Minimum=5,Maximum=180,Increment=15,Value=15,Width=75};
    private readonly NumericUpDown end=new WheelSafeNumericUpDown{Minimum=0,Maximum=20000,Width=100};
    private readonly Button start=new Button{AutoSize=true},cancel=new Button{AutoSize=true};
    private readonly Label status=new Label{Dock=DockStyle.Fill,AutoSize=true,ForeColor=Theme.Accent};
    private readonly ProgressBar progressBar=new ProgressBar{Dock=DockStyle.Fill,Minimum=0,Maximum=1,Value=0,Style=ProgressBarStyle.Continuous};
    private readonly ListView rows=new ListView{Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,MultiSelect=false,HideSelection=false,BackColor=Theme.Back,ForeColor=Theme.Text};
    private readonly TextBox detail=new TextBox{Dock=DockStyle.Fill,ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Both,WordWrap=false,BackColor=Theme.Panel,ForeColor=Theme.Text};
    private LifetimeResult result;
    internal const string ScopeNote="逐帧观察完整 C# 场景，不执行原生 GML。区间表示同一来源至少一项存在，非单颗寿命；排程、模板容器、直接绘制物件分别列出。+ 表示观察在该帧结束，死亡时间未知。透明或正在退场仍可能存活；不推断实际像素、碰撞命中或完整生成祖先。";
    internal SimulatedLifetimeView(LifetimeSnapshot value,Func<bool> isCurrent,LifetimeAnalysisRunner runner=null)
    {
        analyze=runner??((input,options,token,progress)=>SimulatedLifetime.Analyze(input,options,token,null,progress));
        snapshot=value;current=isCurrent??(()=>true);Text=Language.Text("模拟存活区间");Size=new Size(1120,760);MinimumSize=new Size(720,500);StartPosition=FormStartPosition.CenterParent;BackColor=Theme.Back;ForeColor=Theme.Text;Font=Theme.Font;
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Padding=new Padding(8)};
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,116));layout.RowStyles.Add(new RowStyle(SizeType.Percent,60));layout.RowStyles.Add(new RowStyle(SizeType.Percent,40));
        var bar=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,WrapContents=true};
        bar.Controls.Add(new Label{AutoSize=true,Text=Language.Text("分析终点帧（从 0 开始）"),Padding=new Padding(0,6,0,0)});end.Value=Math.Max(0,Math.Min(20000,value.Scene.duration));bar.Controls.Add(end);bar.Controls.Add(new Label{AutoSize=true,Text=Language.Text("软时限 / 秒"),Padding=new Padding(0,6,0,0)});bar.Controls.Add(budget);
        start.Text=Language.Text("开始分析");cancel.Text=Language.Text("取消分析");cancel.Enabled=false;bar.Controls.Add(start);bar.Controls.Add(cancel);
        var close=new Button{Text=Language.Text("关闭"),AutoSize=true};close.Click+=(s,e)=>Close();bar.Controls.Add(close);
        var progressPanel=new TableLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,ColumnCount=1,RowCount=2,Margin=new Padding(0,4,0,4)};
        progressPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        status.MaximumSize=new Size(Math.Max(200,ClientSize.Width-48),0);
        SizeChanged+=(s,e)=>status.MaximumSize=new Size(Math.Max(200,ClientSize.Width-48),0);
        progressPanel.RowStyles.Add(new RowStyle(SizeType.Absolute,18));progressPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        progressPanel.Controls.Add(progressBar,0,0);progressPanel.Controls.Add(status,0,1);
        var note=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BackColor=Theme.Panel,ForeColor=Theme.Muted,Text=Language.Text(ScopeNote)+"\r\n"+Language.Text("分析上限：20,001 帧；默认 15 秒，可手动选择 5–180 秒软时限。采样/工作预算随时限增加，实体/区间/克隆上限不变，仍可能提前停止。取消在检查点生效；仅保留完整帧，显示全部来源。")+"\r\n"+String.Format(Language.Text("快照工程终点：{0} 帧；可另选分析终点检查尾部，不代表无限期结论。"),value.Scene.duration)};
        rows.Columns.Add(Language.Text("物件名称与稳定 ID"),290);rows.Columns.Add(Language.Text("观察类别"),165);rows.Columns.Add(Language.Text("观察区间 / 帧（含两端）"),270);rows.Columns.Add(Language.Text("峰值数量"),85);rows.Columns.Add(Language.Text("覆盖状态"),190);
        layout.Controls.Add(bar,0,0);layout.Controls.Add(progressPanel,0,1);layout.Controls.Add(note,0,2);layout.Controls.Add(rows,0,3);layout.Controls.Add(detail,0,4);Controls.Add(layout);Language.PreserveText(detail);
        rows.SelectedIndexChanged+=(s,e)=>ShowDetail();start.Click+=(s,e)=>BeginAnalysis();cancel.Click+=(s,e)=>CancelAnalysis();
        validity.Tick+=(s,e)=>{if(closing||IsDisposed)return;if(!current())InvalidateResult();else RefreshProgress();};validity.Start();Shown+=(s,e)=>BeginAnalysis();
    }
    private void CancelAnalysis()
    {
        if(!running||closing||invalidated||cancellation==null)return;
        if(progressMailbox!=null)progressMailbox.RequestCancel();
        cancel.Enabled=false;cancellation.Cancel();RefreshProgress();
    }
    private void RefreshProgress()
    {
        if(!running||closing||invalidated||IsDisposed||progressMailbox==null||!gate.Accept(activeRequest,true))return;
        var value=progressMailbox.Read();
        progressBar.Maximum=value.RequestedFrames;progressBar.Value=value.CompletedFrames;
        status.Text=LifetimeProgressText.Running(value);
    }
    private void InvalidateResult()
    {
        if(invalidated||closing||IsDisposed)return;invalidated=true;gate.Invalidate();
        if(progressMailbox!=null)progressMailbox.Close();if(cancellation!=null)cancellation.Cancel();
        result=null;rows.Items.Clear();detail.Clear();progressBar.Value=0;
        start.Enabled=cancel.Enabled=end.Enabled=budget.Enabled=false;status.Text=Language.Text("场景已变化；请关闭并重新打开分析。");
    }
    private async void BeginAnalysis()
    {
        if(running||invalidated||closing||IsDisposed)return;if(!current()){InvalidateResult();return;}
        int request=gate.Begin();activeRequest=request;running=true;result=null;rows.Items.Clear();detail.Clear();
        start.Enabled=end.Enabled=budget.Enabled=false;cancel.Enabled=true;
        var tokenSource=new CancellationTokenSource();cancellation=tokenSource;
        var options=LifetimeOptions.ForWindow((int)end.Value,(double)budget.Value);requestedBudget=options.MaxSeconds;
        var mailbox=new LifetimeProgressMailbox(options.EndFrame,options.MaxSeconds);progressMailbox=mailbox;RefreshProgress();
        try{
            // No control access, UI synchronization context, or posted event per
            // callback: the timer consumes the newest value at most five times/s.
            var completed=await Task.Run(()=>analyze(snapshot,options,tokenSource.Token,mailbox.Report));
            mailbox.Close();
            if(closing||IsDisposed||!gate.Accept(request,true))return;
            if(!current()){InvalidateResult();return;}
            result=completed;gate.Invalidate();Populate();
        }catch(Exception error){
            mailbox.Close();
            if(!closing&&!IsDisposed&&gate.Accept(request,true)){
                if(!current())InvalidateResult();
                else {gate.Invalidate();status.Text=Language.Text("分析失败")+": "+error.Message;}
            }
        }
        finally{
            mailbox.Close();tokenSource.Dispose();
            // An older continuation must not clear a replacement request or
            // re-enable controls belonging to a later run.
            if(ReferenceEquals(cancellation,tokenSource)&&ReferenceEquals(progressMailbox,mailbox)&&activeRequest==request){
                cancellation=null;running=false;
                if(!closing&&!IsDisposed&&!invalidated){start.Enabled=end.Enabled=budget.Enabled=true;cancel.Enabled=false;}
            }
        }
    }
    private void Populate()
    {
        status.Text=LifetimeProgressText.Finished(result,requestedBudget);
        progressBar.Maximum=result.RequestedEnd+1;progressBar.Value=Math.Max(0,Math.Min(progressBar.Maximum,result.LastFrame+1));
        rows.BeginUpdate();try{foreach(var row in result.Rows){var item=new ListViewItem((row.Name??"")+" ["+row.Id+"]");item.SubItems.Add(Language.Text(row.Category));item.SubItems.Add(row.Unknown?Language.Text("未知（未执行）"):row.Category.Contains("声明")?Language.Text("不作为存活区间"):SimulatedLifetime.Intervals(row.Alive,6));item.SubItems.Add(row.Unknown||row.Category.Contains("声明")?"—":row.Alive.Peak.ToString());item.SubItems.Add(Language.Text(row.Unknown?"原生 / GML 未执行":result.Complete?"仅本次 C# 观察窗口":"部分观察，后续未知"));item.Tag=row;rows.Items.Add(item);}}finally{rows.EndUpdate();}
        if(rows.Items.Count>0)rows.Items[0].Selected=true;else ShowDetail();
    }
    private void ShowDetail()
    {
        if(result==null)return;var row=rows.SelectedItems.Count==1?rows.SelectedItems[0].Tag as LifetimeRow:null;
        string text=Language.Text("所有结果只适用于本次快照。飞行子弹可晚于来源声明结束；模板容器结束不代表其子弹死亡。区间之间没有观察到该类别，不能拼接为连续寿命。")+"\r\n"+String.Join("\r\n",result.Warnings.Select(Language.Text));
        if(!String.IsNullOrEmpty(result.Error))text+="\r\n"+Language.Text("分析错误：")+Language.Text(result.Error);
        if(row!=null){text="["+row.Id+"] "+(row.Name??"")+"\r\n"+Language.Text(row.Category)+"\r\n"+row.Declaration+"\r\nclipSourceId: "+(row.ClipSourceId??"")+"\r\n"+Language.Text("观察区间：")+SimulatedLifetime.Intervals(row.Alive,2000)+"\r\n"+Language.Text("其中退场 / 淡出仍存活：")+SimulatedLifetime.Intervals(row.Fading,2000)+"\r\n"+Language.Text("其中有效不透明度 ≤ 0：")+SimulatedLifetime.Intervals(row.Transparent,2000)+"\r\n"+String.Format(Language.Text("峰值 {0}；累计数量×帧 {1}；最高观察代数 {2}。"),row.Alive.Peak,row.Alive.Samples,row.MaxGeneration)+"\r\n\r\n"+text;}
        if(row!=null&&(row.Unknown||row.Category.Contains("声明")))text="["+row.Id+"] "+(row.Name??"")+"\r\n"+Language.Text(row.Unknown?"未知（未执行）":"不作为存活区间")+"\r\n"+row.Declaration+"\r\n\r\n"+String.Join("\r\n",result.Warnings.Select(Language.Text));
        detail.Text=text;
    }
    protected override bool ProcessCmdKey(ref Message msg,Keys keys){if(keys==Keys.Escape){Close();return true;}if(keys==Keys.F5||keys==Keys.F6)return true;return base.ProcessCmdKey(ref msg,keys);}
    private void CloseAnalysis()
    {
        if(closing)return;closing=true;gate.Close();
        if(progressMailbox!=null)progressMailbox.Close();if(cancellation!=null)cancellation.Cancel();
    }
    protected override void OnFormClosing(FormClosingEventArgs e){base.OnFormClosing(e);if(!e.Cancel)CloseAnalysis();}
    protected override void Dispose(bool disposing){if(disposing){CloseAnalysis();validity.Stop();validity.Dispose();}base.Dispose(disposing);}
}
internal sealed partial class MainForm
{
    private void ShowSimulatedLifetimes()
    {
        if(state==null||stage.Room==null||sprites==null||busy||saving||writingRoomInstance)return;
        CommitInspectorEdits();StopPlaying();bool resumeSave=saveTimer.Enabled;saveTimer.Stop();
        var scene=state;var room=stage.Room;var metadata=sprites.Metadata;var film=stage.NativeFilmInfo;int expectedRevision=revision,expectedSceneVersion=stage.SceneVersion;
        try{
            var snapshot=new LifetimeSnapshot(scene,room,metadata,film);
            using(var viewer=new SimulatedLifetimeView(snapshot,()=>!IsDisposed&&ReferenceEquals(state,scene)&&revision==expectedRevision&&stage.SceneVersion==expectedSceneVersion&&ReferenceEquals(stage.Room,room)&&ReferenceEquals(sprites.Metadata,metadata)&&ReferenceEquals(stage.NativeFilmInfo,film)))Language.ShowDialog(viewer,this);
        }catch(Exception error){Error(error);}finally{if(resumeSave&&!IsDisposed)saveTimer.Start();}
    }
}
