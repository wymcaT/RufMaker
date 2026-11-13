// --pure constructs no controls and requires no display. Native interaction tests
// remain explicitly opt-in; compilation alone is not interaction validation.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Text.RegularExpressions;
using System.Windows.Forms;
internal static class SimulatedLifetimeUiTests
{
    private static int checks;
    private static void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
    private static T Field<T>(object target,string name){return (T)target.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(target);}
    private static void Call(object target,string name){target.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(target,null);}
    private static void Pump(Func<bool> done){var until=DateTime.UtcNow.AddSeconds(10);while(!done()&&DateTime.UtcNow<until){Application.DoEvents();Thread.Sleep(10);}Check(done(),"UI operation completes within test timeout");}
    private static LifetimeProgress Progress(int last,int end=100,double elapsed=1,int active=-1,string phase="simulate")
    {return new LifetimeProgress(last,end,elapsed,15,active,phase);}
    private static void Pure(string output)
    {
        double now=0;var mailbox=new LifetimeProgressMailbox(100,15,()=>now);var value=mailbox.Read();
        Check(value.CompletedFrames==0&&value.RequestedFrames==101,"inclusive window has101 frames and no completed frame initially");
        Check(value.Progress.LastCompletedFrame==-1&&value.Progress.ActiveFrame==-1&&value.Progress.Phase=="prepare","preparation cannot imply frame0 completed");
        Check(!value.Cancelling&&value.ElapsedSeconds==0&&value.Progress.MaxSeconds==15,"default progress budget and elapsed explicit");
        now=.5;Check(mailbox.Read().ElapsedSeconds==.5,"elapsed display advances without new callbacks");
        var report=Progress(0,100,.25,1);mailbox.Report(report);value=mailbox.Read();
        Check(ReferenceEquals(value.Progress,report)&&value.CompletedFrames==1,"mailbox retains one immutable latest report");
        Check(value.Progress.ActiveFrame==1&&value.CompletedFrames<value.Progress.ActiveFrame+1,"in-flight frame never increments committed total");
        now=2.5;value=mailbox.Read();Check(value.SinceCheckpoint==2&&value.ElapsedSeconds==2.25,"checkpoint age and elapsed remain distinct");
        mailbox.Report(Progress(0,100,2.25,1,"collect"));value=mailbox.Read();
        Check(value.SinceCheckpoint==0&&value.CompletedFrames==1,"same-frame heartbeat resets checkpoint age without progress inflation");
        mailbox.Report(Progress(-1,100,3,0));Check(mailbox.Read().CompletedFrames==1,"older frame callback cannot rewind completed total");
        mailbox.Report(Progress(40,90,3,41));Check(mailbox.Read().RequestedFrames==101&&mailbox.Read().CompletedFrames==1,"mismatched window report ignored");
        mailbox.RequestCancel();mailbox.Report(Progress(4,100,3,5));value=mailbox.Read();
        Check(value.Cancelling&&value.CompletedFrames==5,"later heartbeat preserves cancellation acknowledgement");
        mailbox.RequestCancel();Check(mailbox.Read().Cancelling,"repeat cancel is idempotent");
        mailbox.Close();mailbox.Report(Progress(100,100,4,100,"complete"));mailbox.Report(null);
        Check(mailbox.Read().CompletedFrames==5&&mailbox.Read().Cancelling,"closed request ignores late callbacks including null");
        var replacement=new LifetimeProgressMailbox(100,15,()=>now);replacement.Report(Progress(2));mailbox.Report(Progress(99));
        Check(replacement.Read().CompletedFrames==3&&!replacement.Read().Cancelling,"new request cannot receive old progress or cancellation");
        var zero=new LifetimeProgressMailbox(0,15,()=>now);Check(zero.Read().RequestedFrames==1&&zero.Read().CompletedFrames==0,"frame0-only window has one requested frame");
        zero.Report(Progress(0,0,1,0,"complete"));Check(zero.Read().CompletedFrames==1,"frame0 counts after commit");
        var burst=new LifetimeProgressMailbox(20000,15,()=>0);Exception writerError=null;
        var writer=new Thread(()=>{try{for(int i=0;i<=20000;i++)burst.Report(Progress(i,20000,1,i+1));}catch(Exception e){writerError=e;}});
        writer.IsBackground=true;writer.Start();int seen=0;bool monotonic=true;
        while(writer.IsAlive){int count=burst.Read().CompletedFrames;if(count<seen)monotonic=false;seen=count;Thread.Yield();}
        Check(writer.Join(5000)&&writerError==null&&monotonic,"worker-thread burst and UI reads are synchronized");
        Check(burst.Read().CompletedFrames==20001,"burst coalesces to latest complete count without UI messages");
        var gate=new LifetimeRequestGate();int old=gate.Begin();int fresh=gate.Begin();
        Check(!gate.Accept(old,true)&&gate.Accept(fresh,true),"old generation cannot overwrite replacement result");
        gate.Invalidate();Check(!gate.Accept(fresh,true),"scene invalidation rejects progress and completion");
        fresh=gate.Begin();gate.Close();Check(!gate.Accept(fresh,true),"close rejects pending completion");
        bool closedBegin=false;try{gate.Begin();}catch(ObjectDisposedException){closedBegin=true;}Check(closedBegin,"closed gate cannot restart");
        foreach(string language in new[]{"zh-CN","en"}){
            // Initialize reads isolated settings and never enumerates open forms.
            string root=Path.Combine(output,language);Directory.CreateDirectory(Path.Combine(root,"user-data"));
            File.WriteAllText(Path.Combine(root,"user-data","settings.json"),"{\"language\":\""+language+"\"}");Language.Initialize(root);
            string running=LifetimeProgressText.Running(new LifetimeProgressState(Progress(4,100,3,5),false,3,0));
            Check(running.Contains("5 / 101")&&running.Contains("0–100")&&running.Contains("15"),"running text includes committed/requested count horizon and budget "+language);
            Check(running.Contains(String.Format(Language.Text("正在处理第 {0} 帧（尚未完成）…"),5)),"active frame explicitly uncompleted "+language);
            string pending=LifetimeProgressText.Running(new LifetimeProgressState(Progress(-1,100,0,-1,"prepare"),false,0,0));
            Check(pending.Contains("0 / 101")&&pending.Contains(Language.Text("正在准备完整 C# 场景…")),"preparation has honest zero count "+language);
            string finishing=LifetimeProgressText.Running(new LifetimeProgressState(Progress(100,100,5,100,"complete"),false,7,2));
            Check(finishing.StartsWith(Language.Text("正在整理观察结果…"))&&!finishing.Contains(String.Format(Language.Text("最近检查点距今 {0:F1} 秒；当前工作尚未返回检查点。"),2.0)),"terminal heartbeat does not imply stalled simulation "+language);
            string cancelled=LifetimeProgressText.Running(new LifetimeProgressState(Progress(5,100,4,6),true,4,2));
            Check(cancelled.StartsWith(Language.Text("正在取消；等待分析检查点…"))&&cancelled.Contains("6 / 101"),"cancel takes precedence over later progress while counts remain "+language);
            Check(cancelled.Contains(String.Format(Language.Text("最近检查点距今 {0:F1} 秒；当前工作尚未返回检查点。"),2.0)),"slow checkpoint text makes no completion estimate "+language);
            foreach(string stop in new[]{"cancelled","time-limit","work-limit","sample-limit","entity-limit","error"}){
                var partial=new LifetimeResult{RequestedEnd=100,LastFrame=4,Stop=stop,Seconds=2};string final=LifetimeProgressText.Finished(partial,15);
                Check(final.Contains("5 / 101")&&final.Contains(Language.Text("部分观察，后续未知")),"partial state keeps requested total and unknown future "+stop+" "+language);
                bool retry=stop=="time-limit"||stop=="work-limit"||stop=="sample-limit";
                Check(final.Contains(Language.Text("可提高软时限后重新分析（从第 0 帧开始）；仍受资源上限限制。"))==retry,"retry suggestion only applies to scalable budgets "+stop+" "+language);
            }
            var noFrames=new LifetimeResult{RequestedEnd=100,LastFrame=-1,Stop="cancelled",Seconds=.1};string empty=LifetimeProgressText.Finished(noFrames,15);
            Check(empty.Contains("0 / 101")&&empty.StartsWith(Language.Text("尚无完整帧；")),"zero-frame cancellation retains full request context "+language);
            var complete=new LifetimeResult{RequestedEnd=4,LastFrame=4,Stop="complete",Seconds=1};string finished=LifetimeProgressText.Finished(complete,15);
            Check(finished.Contains("5 / 5")&&finished.Contains(Language.Text("仅本次 C# 观察窗口"))&&!finished.Contains(Language.Text("部分观察，后续未知")),"complete is scoped to C# window only "+language);
            var limit=new LifetimeResult{RequestedEnd=100,LastFrame=4,Stop="time-limit",Seconds=180};
            Check(!LifetimeProgressText.Finished(limit,180).Contains(Language.Text("可提高软时限后重新分析（从第 0 帧开始）；仍受资源上限限制。")),"maximum budget does not offer an unavailable increase "+language);
            Check(Language.Text(SimulatedLifetimeView.ScopeNote).Contains(language=="en"?"native GML is not executed":"不执行原生 GML"),"native GML exclusion remains visible "+language);
            if(language=="en")Check(!Regex.IsMatch(running+pending+finishing+cancelled+empty+finished+LifetimeProgressText.Finished(limit,180),"[\\u4e00-\\u9fff]"),"English progress and terminal labels fully translated");
        }
        Console.WriteLine("PASS "+checks+" pure simulated lifetime progress checks");
    }
    private static void Native(string output)
    {
        Control.CheckForIllegalCrossThreadCalls=true;
        var scene=new EditorState{duration=20};scene.tracks.Add(new Track{id="source",name="用户名称 untouched",kind="emitter",count=1,start=0,end=10,life=2,interval=5,speed=0});Motion.Normalize(scene);string before=Json.Write(scene);bool current=true;
        foreach(string language in new[]{"zh-CN","en"}){Language.Select(output,language);using(var view=new SimulatedLifetimeView(new LifetimeSnapshot(scene,new RoomData()),()=>current)){
            view.Show();Pump(()=>!Field<bool>(view,"running"));var list=Field<ListView>(view,"rows");Check(list.Items.Count==1,"result list shows source");Check(list.Items[0].Text.Contains("用户名称 untouched"),"user name preserved in both languages");
            Check(Field<NumericUpDown>(view,"budget").Value==15,"default soft time budget remains explicit");
            Check(Field<Label>(view,"status").Text.Contains("21 / 21")&&Field<ProgressBar>(view,"progressBar").Value==21,"inclusive completed total displayed in native controls");
            var oldMailbox=Field<LifetimeProgressMailbox>(view,"progressMailbox");string previous=Field<Label>(view,"status").Text;
            oldMailbox.Report(Progress(0,20,1,1));Call(view,"RefreshProgress");Check(Field<Label>(view,"status").Text==previous,"late progress cannot replace final status");
            Field<NumericUpDown>(view,"budget").Value=60;var start=Field<Button>(view,"start");start.PerformClick();start.PerformClick();Pump(()=>!Field<bool>(view,"running"));Check(list.Items.Count==1,"repeat run replaces result without duplicate rows");
            Check(!ReferenceEquals(oldMailbox,Field<LifetimeProgressMailbox>(view,"progressMailbox")),"rerun uses isolated progress mailbox");
            view.Size=new System.Drawing.Size(740,520);Application.DoEvents();Check(Field<TextBox>(view,"detail").Height>0&&list.Height>0,"narrow window keeps result and detail reachable");
            using(var bitmap=new System.Drawing.Bitmap(view.Width,view.Height)){view.DrawToBitmap(bitmap,view.ClientRectangle);bitmap.Save(Path.Combine(output,"lifetime-"+language+".png"));}
            current=false;Pump(()=>Field<bool>(view,"invalidated"));Check(list.Items.Count==0&&!start.Enabled,"scene change clears old result and blocks rerun");view.Close();Application.DoEvents();current=true;
        }}
        // Deterministic worker seam: keep a frame in flight so cancel/close tests
        // do not race a tiny scene completing before the button is pressed.
        using(var release=new ManualResetEvent(false)){
            LifetimeAnalysisRunner worker=(input,options,token,report)=>{
                report(new LifetimeProgress(0,options.EndFrame,.1,options.MaxSeconds,1,"simulate"));
                release.WaitOne();
                report(new LifetimeProgress(1,options.EndFrame,.2,options.MaxSeconds,2,"collect"));
                return new LifetimeResult{RequestedEnd=options.EndFrame,LastFrame=1,Stop=token.IsCancellationRequested?"cancelled":"time-limit",Seconds=.2};
            };
            using(var view=new SimulatedLifetimeView(new LifetimeSnapshot(scene,new RoomData()),()=>true,worker)){
                try{
                    view.Show();Pump(()=>Field<LifetimeProgressMailbox>(view,"progressMailbox").Read().CompletedFrames==1);
                    Field<Button>(view,"cancel").PerformClick();Check(!Field<Button>(view,"cancel").Enabled&&Field<bool>(view,"running"),"cancel acknowledged while worker still pending");
                    var mailbox=Field<LifetimeProgressMailbox>(view,"progressMailbox");mailbox.Report(Progress(1,20,.15,2));Call(view,"RefreshProgress");
                    Check(Field<Label>(view,"status").Text.StartsWith(Language.Text("正在取消；等待分析检查点…")),"later in-flight report cannot restore running label");
                    Check(Field<Label>(view,"status").Text.Contains("2 / 21"),"cancel acknowledgement keeps latest complete count");
                    release.Set();Pump(()=>!Field<bool>(view,"running"));Check(Field<LifetimeResult>(view,"result").Stop=="cancelled","cancelled result published after worker returns");
                    string terminal=Field<Label>(view,"status").Text;mailbox.Report(Progress(20,20,1,20,"complete"));Call(view,"RefreshProgress");
                    Check(Field<Label>(view,"status").Text==terminal,"late callback cannot replace cancelled result");
                    release.Reset();Field<Button>(view,"start").PerformClick();
                    var nextMailbox=Field<LifetimeProgressMailbox>(view,"progressMailbox");mailbox.Report(Progress(20,20,1,20,"complete"));Call(view,"RefreshProgress");
                    Check(!ReferenceEquals(mailbox,nextMailbox)&&!nextMailbox.Read().Cancelling,"cancelled request cannot leak into rerun");
                    Check(!Field<Label>(view,"status").Text.StartsWith(Language.Text("正在取消；等待分析检查点…")),"rerun does not restore old cancelling status");
                    release.Set();Pump(()=>!Field<bool>(view,"running"));
                }finally{release.Set();view.Close();}
            }
        }
        using(var release=new ManualResetEvent(false)){
            bool valid=true;
            LifetimeAnalysisRunner worker=(input,options,token,report)=>{
                release.WaitOne();report(new LifetimeProgress(0,options.EndFrame,.1,options.MaxSeconds,1,"simulate"));
                return new LifetimeResult{RequestedEnd=options.EndFrame,LastFrame=0,Stop="cancelled",Seconds=.1};
            };
            using(var view=new SimulatedLifetimeView(new LifetimeSnapshot(scene,new RoomData()),()=>valid,worker)){
                try{
                    view.Show();valid=false;Call(view,"InvalidateResult");release.Set();Pump(()=>!Field<bool>(view,"running"));
                    Check(Field<LifetimeResult>(view,"result")==null&&!Field<Button>(view,"start").Enabled,"source invalidation rejects in-flight completion and rerun");
                    Check(Field<Label>(view,"status").Text==Language.Text("场景已变化；请关闭并重新打开分析。"),"late completion cannot erase invalidation message");
                }finally{release.Set();view.Close();}
            }
        }
        using(var release=new ManualResetEvent(false))using(var finished=new ManualResetEvent(false)){
            LifetimeAnalysisRunner worker=(input,options,token,report)=>{
                release.WaitOne();report(new LifetimeProgress(0,options.EndFrame,.1,options.MaxSeconds,1,"simulate"));finished.Set();
                return new LifetimeResult{RequestedEnd=options.EndFrame,LastFrame=0,Stop="cancelled",Seconds=.1};
            };
            using(var view=new SimulatedLifetimeView(new LifetimeSnapshot(scene,new RoomData()),()=>true,worker)){
                try{view.Show();view.Close();Check(view.IsDisposed,"close during pending analysis disposes controls");release.Set();Pump(()=>finished.WaitOne(0));Pump(()=>!Field<bool>(view,"running"));Check(Field<LifetimeResult>(view,"result")==null,"closed view rejects pending completion");}
                finally{release.Set();}
            }
        }
        Check(Json.Write(scene)==before,"all dialog flows preserve project data");Console.WriteLine("PASS "+checks+" native simulated lifetime UI checks");
    }
    [STAThread] private static void Main(string[] args)
    {
        bool pure=args.Length>0&&args[0]=="--pure";
        string output=pure?(args.Length>1?args[1]:Directory.GetCurrentDirectory()):(args.Length>0?args[0]:Directory.GetCurrentDirectory());Directory.CreateDirectory(output);
        if(pure){Pure(output);return;}
        Native(output);
    }
}
