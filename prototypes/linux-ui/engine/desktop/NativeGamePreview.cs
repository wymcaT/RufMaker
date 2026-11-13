using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed class NativeFilmStatus
{public string sourceSignature="",output="";}

internal static class NativeProgressLayout
{
    public static void Build(Form form,Label label,ProgressBar progress,Button cancel)
    {
        form.AutoScaleDimensions=new SizeF(96,96);form.AutoScaleMode=AutoScaleMode.Dpi;form.Font=Theme.Font;form.Icon=AppWindow.LoadIcon();form.ShowInTaskbar=false;
        form.ClientSize=new Size(540,160);form.MinimumSize=new Size(560,0);form.AutoSize=true;form.AutoSizeMode=AutoSizeMode.GrowAndShrink;
        var layout=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Padding=new Padding(16),ColumnCount=1,RowCount=3};
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));for(int i=0;i<3;i++)layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        label.AutoSize=true;label.Dock=DockStyle.Fill;label.Padding=Padding.Empty;label.Margin=new Padding(0,0,0,14);label.MaximumSize=new Size(508,0);label.UseMnemonic=false;
        progress.Dock=DockStyle.Fill;progress.Height=24;progress.Margin=new Padding(0,0,0,14);
        cancel.AutoSize=true;cancel.Dock=DockStyle.Fill;cancel.MinimumSize=new Size(0,36);cancel.Margin=Padding.Empty;
        layout.Controls.Add(label,0,0);layout.Controls.Add(progress,0,1);layout.Controls.Add(cancel,0,2);form.Controls.Add(layout);
    }
}

internal sealed class NativeReplayProgressForm:LocalizedForm
{
    private readonly NativeBridge bridge;private readonly System.Windows.Forms.Timer poll=new System.Windows.Forms.Timer{Interval=500};private readonly Label label;private readonly ProgressBar bar;private readonly Button cancel;
    private bool finished,polling;public bool Cancelled;public string ErrorText;public BuildResult Result;
    public NativeReplayProgressForm(NativeBridge bridge,object payload,int target,bool godMode=false)
    {
        this.bridge=bridge;Text=godMode?"准备调试试玩（无敌模式）":"准备原生试玩";FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;BackColor=Theme.Panel;ForeColor=Theme.Text;
        label=new Label{Text=godMode?"正在同步并编译调试试玩副本（无敌模式）…":"正在同步并编译原生试玩副本…"};
        bar=new ProgressBar{Dock=DockStyle.Top,Height=25,Minimum=0,Maximum=Math.Max(1,target),Style=ProgressBarStyle.Marquee};
        cancel=new Button{Text="取消"};cancel.Click+=(s,e)=>CancelReplay();NativeProgressLayout.Build(this,label,bar,cancel);
        FormClosing+=(s,e)=>{if(!finished){e.Cancel=true;CancelReplay();}};poll.Tick+=async(s,e)=>await ReadProgress();
        Shown+=async(s,e)=>{
            poll.Start();try{Result=await bridge.Call<BuildResult>("run",payload);}catch(Exception error){if(!Cancelled)ErrorText=error.Message;}finally{finished=true;poll.Stop();Close();}
        };
    }
    private async Task ReadProgress()
    {
        if(polling||finished||Cancelled)return;polling=true;
        try{var progress=await bridge.Call<NativeRunProgress>("run-progress",new{});if(finished||IsDisposed||Cancelled||progress==null)return;
            if(progress.phase=="launch"){bar.Style=ProgressBarStyle.Marquee;label.Text=Language.Text("正在启动游戏运行器…");}
            else if(progress.phase=="replay"){bar.Style=ProgressBarStyle.Continuous;bar.Maximum=Math.Max(1,progress.target);bar.Value=Math.Max(0,Math.Min(bar.Maximum,progress.frame));label.Text="正在重放至播放头 · "+progress.frame+" / "+progress.target+" 帧\r\n画面复杂时可能较慢；有帧进度就会继续等待。";}
        }catch{ /* The run request reports bridge failures. */ }finally{polling=false;}
    }
    private async void CancelReplay()
    {
        if(Cancelled||finished)return;Cancelled=true;cancel.Enabled=false;label.Text="正在取消原生试玩重放…";
        try{await bridge.Call<object>("run-cancel",new{});}catch{ /* The run request reports bridge failures. */ }
    }
    protected override void Dispose(bool disposing){if(disposing)poll.Dispose();base.Dispose(disposing);}
}

internal sealed class NativeFilmProgressForm:LocalizedForm
{
    private readonly CancellationTokenSource cancellation=new CancellationTokenSource();private bool finished;
    public NativeFilmInfo Result;public string ErrorText;
    public NativeFilmProgressForm(NativeFilmJob job)
    {
        Text=job.nativeTrackIds.Length>0?"生成原生对象动态预览":"生成原生底片";FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;BackColor=Theme.Panel;ForeColor=Theme.Text;
        var label=new Label{Text="正在完整运行原生耐久，player 固定在 (400,304)…"};
        var bar=new ProgressBar{Dock=DockStyle.Top,Height=25,Minimum=0,Maximum=job.frameCount};
        var cancel=new Button{Text="取消"};cancel.Click+=(s,e)=>{cancellation.Cancel();cancel.Enabled=false;label.Text="正在停止生成，已有底片会保留…";};NativeProgressLayout.Build(this,label,bar,cancel);
        FormClosing+=(s,e)=>{if(!finished){e.Cancel=true;cancellation.Cancel();cancel.Enabled=false;}};
        Shown+=async(s,e)=>{
            try{var progress=new Progress<NativeFilmProgress>(value=>{if(IsDisposed||finished)return;bar.Value=Math.Min(bar.Maximum,value.frame);label.Text="正在生成原生底片 · "+value.frame+" / "+value.total+" 帧（50 FPS）";});Result=await Task.Run(()=>NativeFilmBuilder.Generate(job,cancellation.Token,progress));DialogResult=DialogResult.OK;}
            catch(OperationCanceledException){DialogResult=DialogResult.Cancel;}catch(Exception error){ErrorText=error.Message;DialogResult=DialogResult.Abort;}finally{finished=true;Close();}
        };
    }
    protected override void Dispose(bool disposing){if(disposing)cancellation.Dispose();base.Dispose(disposing);}
}
internal sealed partial class MainForm
{
    private readonly CheckBox nativeRender=new CheckBox{Text="原生底片",AutoSize=true,Enabled=false,ForeColor=Theme.Muted,Margin=new Padding(8,9,5,0)};
    private readonly Button regenerateNativeFilm=new Button{Text="重新生成原生底片",AutoSize=true,Enabled=false,FlatStyle=FlatStyle.Flat,BackColor=Theme.Raised,ForeColor=Theme.Text,UseVisualStyleBackColor=false,TabStop=false,Padding=new Padding(8,3,8,3),Margin=new Padding(8,3,5,0)};
    private NativeEvent selectedNativeEvent;private NativeFilmInfo nativeFilmInfo,baseNativeFilmInfo;private bool nativeFilmGenerating,nativeObjectFilmStale;
    private string nativePreviewKey="",nativeCheckedSignature;
    private EditorState coverageDocument;private int coverageRevision=-1;private NativePreviewRequirements coverageRequirements;
    private NativePreviewCoverageReport CurrentNativePreviewCoverage()
    {
        if(coverageRequirements==null||coverageDocument!=state||coverageRevision!=revision){coverageRequirements=NativePreviewCoverage.Collect(state);coverageDocument=state;coverageRevision=revision;}
        bool displayed=!String.IsNullOrEmpty(stage.NativeFilmPath);
        var info=displayed?stage.NativeFilmInfo:nativeFilmInfo;
        string signature=displayed&&stage.NativeFilmPath!=NativeFilmPath?null:nativeCheckedSignature;
        return NativePreviewCoverage.Evaluate(coverageRequirements,state,info,displayed,signature,nativeObjectFilmStale);
    }
    private async Task VerifyNativeFilmCoverage()
    {
        if(state==null||bridge==null||dirty&&nativeObjectFilmStale||nativeFilmGenerating)return;
        var requestedDocument=document;var requestedBridge=bridge;string key=NativeClip.PreviewKey(state);
        NativeFilmInfo checkedFilm=null;if(File.Exists(NativeFilmPath))using(var film=new NativeFilmReader(NativeFilmPath))checkedFilm=film.Info;
        var status=await requestedBridge.Call<NativeFilmStatus>("native-film-status",new{});
        if(document!=requestedDocument||bridge!=requestedBridge||key!=NativeClip.PreviewKey(state))return;
        nativeCheckedSignature=status.sourceSignature;nativeFilmInfo=checkedFilm;if(!HasNativeObjectClips)baseNativeFilmInfo=checkedFilm;
        nativeObjectFilmStale=nativeFilmInfo!=null&&nativeFilmInfo.sourceSignature!=status.sourceSignature;
        ToggleNativeFilm();RefreshShell();
    }
    private bool HasNativeObjectClips {get{return state!=null&&NativeClip.HasClips(state.tracks);}}
    private string BaseNativeFilmPath {get{return document==null?null:Path.Combine(document.project,"cache","native-film","base.rufvideo");}}
    private string NativeFilmPath {get{return document==null?null:Path.Combine(document.project,"cache","native-film",HasNativeObjectClips?"objects.rufvideo":"base.rufvideo");}}
    private void ToggleNativeFilm()
    {
        string path=NativeFilmPath;var info=nativeFilmInfo;
        if(HasNativeObjectClips&&(nativeObjectFilmStale||!File.Exists(path))){path=BaseNativeFilmPath;info=baseNativeFilmInfo;}
        stage.NativeFilmPath=nativeRender.Checked&&path!=null&&File.Exists(path)?path:null;
        stage.NativeFilmInfo=info;
        stage.NativeFilmIds=new System.Collections.Generic.HashSet<string>(info==null?new string[0]:info.nativeIds??new string[0]);stage.SceneChanged();
    }
    private void NativeObjectPreviewChanged(bool resources=false)
    {
        string key=NativeClip.PreviewKey(state);
        if(!resources&&key==nativePreviewKey)return;
        nativePreviewKey=key;nativeObjectFilmStale=true;nativeCheckedSignature=null;
        if(HasNativeObjectClips)nativeRender.Enabled=true;
        ToggleNativeFilm();RefreshShell();
    }
    private async Task EnsureNativeFilm()
    {
        // Enabled includes the parent's temporary disabled state during SwitchProject.
        // Availability comes from project data, never from a control's effective state.
        bool available=state!=null&&(state.nativeProject!=null||HasNativeObjectClips||File.Exists(NativeFilmPath));nativeRender.Enabled=available;regenerateNativeFilm.Enabled=available&&!nativeFilmGenerating;nativeFilmInfo=null;baseNativeFilmInfo=null;nativeObjectFilmStale=false;nativeCheckedSignature=null;coverageRequirements=null;nativePreviewKey=NativeClip.PreviewKey(state);
        if(!available){stage.NativeFilmPath=null;return;}
        if(HasNativeObjectClips&&File.Exists(BaseNativeFilmPath)){
            try{using(var film=new NativeFilmReader(BaseNativeFilmPath))baseNativeFilmInfo=film.Info;}
            catch(IOException){SetStatus("原生底片缓存无法读取，可重新生成。");}
        }
        if(File.Exists(NativeFilmPath)){
            try{
                using(var film=new NativeFilmReader(NativeFilmPath))nativeFilmInfo=film.Info;
                if(!HasNativeObjectClips)baseNativeFilmInfo=nativeFilmInfo;
                if(bridge!=null){var status=await bridge.Call<NativeFilmStatus>("native-film-status",new{});nativeCheckedSignature=status.sourceSignature;nativeObjectFilmStale=nativeFilmInfo.sourceSignature!=status.sourceSignature;}
                nativeRender.Checked=true;ToggleNativeFilm();RefreshShell();SetStatus(nativeObjectFilmStale?"原生底片已过期，请在原生底片管理中重新生成。":nativeFilmInfo.views==null?"旧底片未记录原生视野；请点击“重新生成原生底片”一次，以同步物件和拉镜坐标。":"原生底片已载入 · 物件跟随原生视野，RufMaker 拉镜与特效实时叠加。");}
            catch(Exception error){nativeRender.Checked=false;Error(error);}return;
        }
        if(HasNativeObjectClips){nativeFilmInfo=baseNativeFilmInfo;nativeObjectFilmStale=true;nativeRender.Checked=baseNativeFilmInfo!=null;ToggleNativeFilm();SetStatus("原生对象动态预览尚未生成，按空格生成，或在原生预览管理中手动生成。");return;}
        if(!EngineSetup.Find(root).Ready){nativeRender.Checked=false;SetStatus("尚未配置 GM8.2；可继续编辑，使用顶栏“引擎设置”完成配置后再试玩或导出。");return;}
        await GenerateNativeFilm();
    }
    private async Task GenerateNativeFilm()
    {
        if(state==null||nativeFilmGenerating)return;if(!await EnsureEngine())return;bool wasBusy=busy;nativeFilmGenerating=true;StopPlaying();SetBusy(true);regenerateNativeFilm.Enabled=false;
        try{
            CommitInspectorEdits();if(!await SaveNow(false))return;string requestedKey=NativeClip.PreviewKey(state);SetStatus("正在编译原生底片录制副本…");var job=await bridge.Call<NativeFilmJob>("native-film-prepare",new{});
            using(var dialog=new NativeFilmProgressForm(job)){
                var result=Language.ShowDialog(dialog,this);if(result==DialogResult.OK){nativeFilmInfo=dialog.Result;nativeCheckedSignature=job.sourceSignature;if(!HasNativeObjectClips)baseNativeFilmInfo=nativeFilmInfo;nativePreviewKey=NativeClip.PreviewKey(state);nativeObjectFilmStale=requestedKey!=nativePreviewKey;if(!nativeObjectFilmStale){var current=await bridge.Call<NativeFilmStatus>("native-film-status",new{});nativeCheckedSignature=current.sourceSignature;nativeObjectFilmStale=nativeFilmInfo.sourceSignature!=current.sourceSignature;}nativeRender.Enabled=true;nativeRender.Checked=true;ToggleNativeFilm();ShowProperties();SetStatus(nativeObjectFilmStale?"原生底片已过期，请在原生底片管理中重新生成。":"原生底片已保存 · "+nativeFilmInfo.frameCount+" 帧 · 后续打开直接使用缓存。");}
                else if(!String.IsNullOrEmpty(dialog.ErrorText))throw new IOException(dialog.ErrorText);else SetStatus("底片生成已取消，可稍后点击重新生成。");
            }
        }catch(Exception error){Error(error);}finally{nativeFilmGenerating=false;regenerateNativeFilm.Enabled=state!=null;SetBusy(wasBusy);}
    }
    private async Task<bool> PrepareNativeObjectPlayback()
    {
        if(!HasNativeObjectClips){if(state!=null&&nativeRender.Checked&&nativeFilmInfo!=null)await VerifyNativeFilmCoverage();return true;}
        CommitInspectorEdits();if(dirty&&!await SaveNow(false))return false;
        var status=await bridge.Call<NativeFilmStatus>("native-film-status",new{});nativeCheckedSignature=status.sourceSignature;
        if(File.Exists(status.output)){
            try{using(var film=new NativeFilmReader(status.output)){
                if(NativePreviewCoverage.CacheState(film.Info,true,status.sourceSignature,false,state,state.duration)=="matching"){nativeFilmInfo=film.Info;nativeObjectFilmStale=false;nativeRender.Enabled=true;nativeRender.Checked=true;ToggleNativeFilm();return true;}
            }}catch(InvalidDataException){}catch(IOException){}
        }
        nativeObjectFilmStale=true;
        await GenerateNativeFilm();
        if(nativeObjectFilmStale||nativeFilmInfo==null)return false;
        var current=await bridge.Call<NativeFilmStatus>("native-film-status",new{});
        nativeCheckedSignature=current.sourceSignature;nativeObjectFilmStale=NativePreviewCoverage.CacheState(nativeFilmInfo,true,current.sourceSignature,false,state,state.duration)!="matching";
        if(nativeObjectFilmStale){ToggleNativeFilm();SetStatus("原生对象已修改，按空格更新预览。");}
        return !nativeObjectFilmStale;
    }
    private void NativeProperties(NativeProperties bag)
    {
        var native=state.nativeProject;selectionTitle.Text=Language.Text("GM 工程 · 原生");codeButton.Enabled=true;
        NativeTestFields(bag);
        bag.Add("耐久房间","原生工程",typeof(string),()=>state.room,null);bag.Add("控制对象","原生工程",typeof(string),()=>native.controller,null);bag.Add("计时方式","原生工程",typeof(string),()=>String.IsNullOrEmpty(native.timeline)?native.clock:native.timeline+" × "+native.timelineSpeed,null);
        bag.Add("底片状态","原生底片",typeof(string),()=>nativeFilmInfo==null?"尚未生成":nativeFilmInfo.frameCount+" 帧 / "+nativeFilmInfo.fps+" FPS · "+(nativeFilmInfo.views==null?"缺少原生视野，请重新生成一次":"已记录原生视野"),null);
        bag.Add("已定位事件","识别结果",typeof(int),()=>native.events.Length,null);bag.Add("未换算表达式","识别结果",typeof(int),()=>native.unresolved.Length,null);
        if(selectedNativeEvent!=null){var item=selectedNativeEvent;bag.Add("原表达式","所选事件",typeof(string),()=>item.expression,null);bag.Add("帧","所选事件",typeof(int),()=>item.frame,null);bag.Add("原始代码","所选事件",typeof(string),()=>item.file+":"+item.line,null);bag.Add("内容","所选事件",typeof(string),()=>item.snippet,null);}
        bag.Add("兼容提示与建议","识别结果",typeof(string),()=>String.Join("\r\n",native.warnings),null);bag.Add("编辑方式","原生工程",typeof(string),()=>"双击事件标记编辑该帧代码块。改动原生代码后点击“重新生成原生底片”；RufMaker 物件和特效实时叠加。F5 进入真实试玩。",null);
    }
    private async Task EditNativeEvent()
    {
        if(state.nativeProject==null||selectedNativeEvent==null)return;var item=selectedNativeEvent;
        if(item.kind=="timeline"){
            string section=item.expression.Substring(item.expression.LastIndexOf(' ')+1);
            await OpenGmAsset("timelines",Path.GetFileNameWithoutExtension(item.file),section);return;
        }
        try{StopPlaying();CommitInspectorEdits();if(!await SaveNow(false))return;bool saved=false;var source=await bridge.Call<SourceData>("native-event-read",new{ @event=item });using(var editor=new CodeForm("第 "+item.frame+" 帧 · "+item.expression+" · "+item.file,source.content,async text=>{var result=await bridge.Call<SourceData>("native-event-save",new{ @event=item,content=text,hash=source.hash,start=source.start,end=source.end });source.hash=result.hash;source.start=result.start;source.end=result.end;saved=true;}))Language.ShowDialog(editor,this);if(saved){await LoadProject();SetStatus("原生代码已保存，点击“重新生成原生底片”更新画面。");}}catch(Exception error){Error(error);}
    }
    private async Task EditNativeSource()
    {
        if(state.nativeProject==null)return;string file=selectedNativeEvent==null?"objects/"+state.nativeProject.controller+".gml":selectedNativeEvent.file;if(String.IsNullOrEmpty(state.nativeProject.controller)&&selectedNativeEvent==null){SetStatus("未识别控制对象，请从左栏资源打开对应 GML。");return;}
        try{bool saved=false;var source=await bridge.Call<SourceData>("native-source-read",new{path=file});using(var editor=new CodeForm(file,source.content,async text=>{var result=await bridge.Call<SourceData>("native-source-save",new{path=file,content=text,hash=source.hash});source.hash=result.hash;if(!await SaveNow(false))throw new IOException(saveFailure??Language.Text("保存失败"));saved=true;},selectedNativeEvent==null?1:selectedNativeEvent.line))Language.ShowDialog(editor,this);if(saved){await LoadProject();SetStatus("原生代码已保存，点击“重新生成原生底片”更新画面。");}}catch(Exception error){Error(error);}
    }
}
