using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class UsabilityRegressionTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static object Get(object o,string name){return o.GetType().GetField(name,Private).GetValue(o);}
    private static void Set(object o,string name,object value){o.GetType().GetField(name,Private).SetValue(o,value);}
    private static object Call(object o,string name,params object[] values){return o.GetType().GetMethods(Private).Single(m=>m.Name==name&&m.GetParameters().Length==values.Length).Invoke(o,values);}
    private static void Mouse(Control c,string name,int x,int y){Call(c,name,new MouseEventArgs(MouseButtons.Left,1,x,y,0));}
    private static void PumpUntil(Func<bool> finished){var end=DateTime.UtcNow.AddSeconds(8);while(!finished()){if(DateTime.UtcNow>end)throw new Exception("Async save timed out");Application.DoEvents();Thread.Sleep(5);}}
    [STAThread] private static void Main(string[] args)
    {try{Run(args);}catch(Exception error){Console.WriteLine(error.GetType().FullName+": "+error.Message+"\n"+error.StackTrace);Environment.ExitCode=1;}}
    private static void Run(string[] args)
    {
        Application.EnableVisualStyles();string root=args[0];
        var track=new Track{id="test",name="测试物件",kind="object",start=100,end=400};
        Motion.EditValue(track,"x",200,620);Check(track.x==620&&track.keyframes.Count==0,"Editing an unkeyed property must stay global");
        Motion.SetKey(track,"x",100,100);Motion.SetKey(track,"x",200,200);Motion.SetKey(track,"y",150,150);Motion.SetKey(track,"y",250,250);
        Motion.EditValue(track,"x",175,175);Check(track.keyframes.Count==5&&track.x==620,"Editing keyed X creates only X at the playhead");track.keyframes.RemoveAll(k=>k.frame==175);
        var state=new EditorState{duration=1000,fps=50,tracks={track}};
        int undoCount=0,changedCount=0;Console.WriteLine("Global/keyed model passed");
        using(var timeline=new TimelineCanvas{State=state,Width=1100,Height=300,PixelsPerSecond=100})
        {
            timeline.CreateControl();timeline.BeginEdit=()=>undoCount++;timeline.Changed=()=>changedCount++;timeline.Expand(track,true);
            Check(((IList)Get(timeline,"lanes")).Count==3,"Only keyed X/Y lanes should be expanded");
            Console.WriteLine("Keyed lanes passed");
            Mouse(timeline,"OnMouseDown",370,108);Mouse(timeline,"OnMouseMove",710,165);Mouse(timeline,"OnMouseUp",710,165);
            Check(timeline.SelectedKeys.Count==4,"Left drag must box-select keys across two properties");
            Console.WriteLine("Box selection passed");
            Mouse(timeline,"OnMouseDown",390,121);Mouse(timeline,"OnMouseMove",490,121);Mouse(timeline,"OnMouseUp",490,121);
            Check(track.keyframes.Select(k=>k.frame).SequenceEqual(new[]{150,250,200,300}),"Group move must preserve relative spacing");Check(undoCount==1&&changedCount==1,"A group drag should create one undo transaction");
            Console.WriteLine("Group move passed");
            int[] keyFrames=track.keyframes.Select(k=>k.frame).ToArray();
            Mouse(timeline,"OnMouseDown",390,91);Mouse(timeline,"OnMouseMove",430,91);Mouse(timeline,"OnMouseUp",430,91);
            Check(track.start==120&&track.keyframes.Select(k=>k.frame).SequenceEqual(keyFrames),"Expanded bar edge must resize the track without moving keys");
            Mouse(timeline,"OnMouseMove",990,91);Check(timeline.Cursor==Cursors.SizeWE,"Edge must expose a horizontal resize cursor");
            var view=timeline.CaptureView();using(var other=new TimelineCanvas{State=state,Width=1100,Height=300}){other.CreateControl();other.RestoreView(view);Check(other.PixelsPerSecond==100&&((IList)Get(other,"lanes")).Count==3,"Zoom and expansion restore");}
            using(var bitmap=new Bitmap(1100,300)){timeline.DrawToBitmap(bitmap,new Rectangle(0,0,1100,300));bitmap.Save(Path.Combine(root,"timeline-selection.png"));}
        }
        int begin=0,endScrub=0;double value=500;
        Console.WriteLine("Bar resize/view restoration passed");
        using(var number=new ScrubNumber("X",typeof(double),()=>value,v=>value=(double)v){Width=120,Height=28})
        {number.CreateControl();number.BeginScrub=()=>begin++;number.EndScrub=()=>endScrub++;Mouse(number,"OnMouseDown",20,18);Mouse(number,"OnMouseMove",20,8);Mouse(number,"OnMouseUp",20,8);Check(value==520&&begin==1&&endScrub==1,"Numeric drag and single transaction");}
        Check(ScrubNumber.StepFor(.5,false,"透明度")<ScrubNumber.StepFor(500,false,"X"),"Numeric drag adapts to magnitude");
        Console.WriteLine("Numeric scrubbing passed");
        var savedView=new WorkspaceView{x=20,y=30,width=1300,height=820,positioned=true,leftWidth=240,rightWidth=330,timelineHeight=320,frame=275,timeline=new TimelineView{pixelsPerSecond=100,expanded=new[]{"test"}}};savedView.Save(root);var loadedView=WorkspaceView.Load(root);Check(loadedView.leftWidth==240&&loadedView.timeline.expanded[0]=="test"&&loadedView.frame==275,"View file round trip");
        using(var bridge=new NativeBridge(root))
        using(var form=new MainForm(root))
        {
            form.CreateControl();var read=bridge.Call<LoadData>("load",new{});PumpUntil(()=>read.IsCompleted);var document=read.Result;
            document.state=state;Set(form,"bridge",bridge);Set(form,"document",document);Set(form,"state",state);
            var canvas=(StageCanvas)Get(form,"stage");canvas.State=state;canvas.Room=document.room;canvas.Sprites=new SpriteStore{Project=document.project,Metadata=document.spriteMeta};
            var timeline=(TimelineCanvas)Get(form,"timeline");timeline.State=state;timeline.RefreshLayout();Call(form,"RestorePanelLayout");Call(form,"SelectTrack",track,null);
            var inspector=(ParameterInspector)Get(form,"properties");var bag=inspector.SelectedObject;
            var descriptors=TypeDescriptor.GetProperties(bag);descriptors["透明度"].SetValue(bag,.4);Check(track.alpha==.4&&!Motion.HasKeys(track,"alpha"),"Native inspector keeps alpha global");
            descriptors["X"].SetValue(bag,777.0);Check(Motion.HasKeys(track,"x")&&track.keyframes.Any(k=>k.property=="x"&&k.frame==0&&k.value==777),"Native inspector edits keyed X only");
            timeline.SetSelection(track.keyframes.Take(2),track,track.keyframes[0]);Call(form,"CopyKey");Call(form,"SetFrame",500);Call(form,"PasteKey");Check(timeline.SelectedKeys.Count==2,"Batch clipboard paste");Call(form,"RemoveSelection");Check(timeline.SelectedKeys.Count==0,"Batch delete clears selection");
            Set(form,"dirty",false);Call(form,"SetFrame",275);IntPtr handle=form.Handle;Call(form,"OnClosing",form,new FormClosingEventArgs(CloseReason.UserClosing,false));PumpUntil(()=>form.IsDisposed);
            var persisted=Json.Read<EditorState>(File.ReadAllText(Path.Combine(root,"projects","rufmaker-demo.gm82","rufmaker.json")));
            Check(persisted.tracks[0].alpha==.4,"Closing saves even when dirty flag was clear");var view=WorkspaceView.Load(root);Check(view.frame==275&&view.leftWidth==240&&view.rightWidth==330&&view.timelineHeight==320,"Closing saves actual panel layout and playhead");
        }
        File.WriteAllText(Path.Combine(root,"usability-report.json"),Json.Write(new{result="passed",features=new[]{"close saves project and workspace","restore split sizes and timeline view","box-select across property lanes","group move/copy/paste/delete","expanded bars resize without key interception","only keyed property lanes","global versus keyed inspector values","native numeric drag with adaptive sensitivity"}}));
        Console.WriteLine("PASS: layout persistence, close autosave, marquee/group editing, edge resizing, keyed-only lanes, global parameters and numeric scrubbing.");
    }
}
