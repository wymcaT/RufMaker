using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

internal static class CursorSnappingTests
{
    static readonly BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static object Get(object o,string name){return o.GetType().GetField(name,flags).GetValue(o);}
    static void Set(object o,string name,object value){o.GetType().GetField(name,flags).SetValue(o,value);}
    static object Call(object o,string name,params object[] args){return o.GetType().GetMethods(flags).Single(m=>m.Name==name&&m.GetParameters().Length==args.Length).Invoke(o,args);}
    static void Mouse(Control c,string method,int x,int y){typeof(Control).GetMethod(method,flags).Invoke(c,new object[]{new MouseEventArgs(MouseButtons.Left,1,x,y,0)});}
    static Track Item(string id,int start,int end,int lane=0){return new Track{id=id,name=id,kind="object",start=start,end=end,lane=lane};}
    static EditorState Scene(params Track[] tracks){var s=new EditorState{duration=1000,fps=50,tracks=tracks.ToList()};Motion.Normalize(s);return s;}
    static void Pump(int milliseconds){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<milliseconds){Application.DoEvents();Thread.Sleep(3);}}
    static void Targets(){
        var a=Item("a",100,139);var b=Item("b",200,239,1);Motion.SetKey(b,"x",225,300);var hidden=Item("hidden",0,900);hidden.templateOnly=true;Motion.SetKey(hidden,"x",224,0);var hiddenChild=Item("child",0,900);hiddenChild.parentId=hidden.id;hidden.kind="group";Motion.SetKey(hiddenChild,"x",223,0);var s=Scene(a,b,hidden,hiddenChild);s.nativeProject=new NativeProject{events=new[]{new NativeEvent{frame=203}}};
        Check(CursorSnapping.Snap(s,a,199,2,true)==200,"Unselected object start");Check(CursorSnapping.Snap(s,null,238,2,true)==239,"Unselected object last frame");Check(CursorSnapping.Snap(s,null,240,0,true)==240,"Visible right boundary");Check(CursorSnapping.Snap(s,a,224,3,true)==225,"Unselected/hidden property keys");Check(CursorSnapping.Snap(s,null,222,4,true)==225,"Hidden local templates leaked into song targets");Check(CursorSnapping.Snap(s,a,202,2,true)==203,"Native key without native selection");
        s.beatSnap=true;s.bpm=120;s.beatDivision=4;Check(CursorSnapping.Snap(s,a,202,3,true)==203,"Targets take precedence over beat grid");Check(CursorSnapping.Snap(s,a,202,3,false)==200,"Independent beat snap remains available");
        foreach(bool keys in new[]{true,false}){var lookup=CursorSnapping.Capture(s,a,3,keys);for(int value=0;value<=s.duration;value++)Check((lookup(value)??value)==CursorSnapping.Snap(s,a,value,3,keys),"Cached candidate search differs from direct snapping");}
        s.beatSnap=false;var captured=CursorSnapping.Capture(s,a,3,true);b.keyframes.Clear();Check(captured(224)==225&&CursorSnapping.Capture(s,a,3,true)(224)==null,"Gesture candidate index did not keep a stable snapshot");
    }
    static void Scrub(){
        var frames=new List<int>();var guides=new List<int?>();int builds=0;using(var scrub=new CursorScrub{Seek=frames.Add,GuideChanged=guides.Add,PrepareTargets=()=>{builds++;return value=>(int?)100;}}){
            scrub.Begin(101,false);for(int i=102;i<=109;i++)scrub.Move(i,false);Check(frames.SequenceEqual(Enumerable.Range(101,9))&&builds==1,"Moving cursor did not stay raw or rebuilt the index");Check(scrub.Guide==100&&guides.Count==1,"Candidate hint was not stable");Pump(220);scrub.Settle(false);Check(frames.Last()==109&&scrub.Guide==100,"Idle moved the playhead instead of just showing a guide");scrub.Move(108,false);scrub.End(false);Check(frames.Last()==100&&scrub.Guide==null,"Release did not commit the displayed guide");scrub.Settle(false);Check(frames.Last()==100,"Inactive gesture delivered a stale target");
            scrub.Begin(207,true);scrub.Move(208,true);Check(scrub.Guide==null,"Alt still showed a candidate");scrub.End(false);Pump(200);Check(frames.Last()==208,"Alt sample snapped after Alt was released");scrub.Begin(209,false);scrub.Settle(true);scrub.End(true);Pump(190);Check(frames.Last()==209,"Alt at release still snapped");
            scrub.Begin(205,false);Check(frames.Last()==205&&scrub.Guide==100,"Candidate fixture");scrub.BypassNow();Check(frames.Last()==205&&scrub.Guide==null,"Alt did not clear the hint");scrub.End(false);scrub.Begin(210,false);scrub.Cancel();Pump(200);Check(frames.Last()==210&&scrub.Guide==null,"Cancel retained a hint or delivered a stale snap");
            scrub.Begin(211,false);Pump(210);Check(frames.Last()==211,"A delayed timer snapped a held cursor");scrub.End(false);
        }
    }
    static void TimelineScrub(){
        var b=Item("b",200,239);Motion.SetKey(b,"x",225,0);var s=Scene(b);s.nativeProject=new NativeProject{events=new[]{new NativeEvent{frame=203}}};using(var t=new TimelineCanvas{State=s,Size=new Size(1400,440),PixelsPerSecond=100}){
            Keys modifiers=Keys.None;t.ReadModifiers=()=>modifiers;t.PrepareCursorTargets=()=>CursorSnapping.Capture(s,null,5,true);int value=-1;t.Seek=f=>{value=f;t.Frame=f;};t.CreateControl();
            Mouse(t,"OnMouseDown",600,5);Check(value==205,"Timeline mouse-down snapped immediately");Mouse(t,"OnMouseMove",604,5);Check(value==207,"Timeline mouse-move snapped immediately");((CursorScrub)Get(t,"scrub")).Settle(false);Check(value==207&&((CursorScrub)Get(t,"scrub")).Guide==203,"Timeline idle moved the cursor or missed the native hint");Mouse(t,"OnMouseMove",642,5);Check(value==226,"Timeline did not resume raw seek");Mouse(t,"OnMouseUp",642,5);Check(value==225,"Timeline release missed key");
            modifiers=Keys.Alt;Mouse(t,"OnMouseDown",646,5);Mouse(t,"OnMouseMove",648,5);Check(value==229,"Alt raw seek");modifiers=Keys.None;Mouse(t,"OnMouseUp",648,5);Pump(200);Check(value==229,"Timeline Alt release snapped later");
            Mouse(t,"OnMouseDown",600,5);((CursorScrub)Get(t,"scrub")).Settle(false);Check(value==205,"Idle moved playhead before Alt");modifiers=Keys.Alt;Call(t,"OnKeyDown",new KeyEventArgs(Keys.Menu|Keys.Alt));Check(value==205&&((CursorScrub)Get(t,"scrub")).Guide==null,"Timeline Alt did not hide candidate");Mouse(t,"OnMouseUp",600,5);modifiers=Keys.None;
            Mouse(t,"OnMouseDown",634,5);t.Capture=false;Check(value==225,"Capture loss did not settle safely");Mouse(t,"OnMouseDown",600,5);t.State=Scene(Item("new",500,550));Pump(190);Check(value==205,"Project switch delivered stale seek");Mouse(t,"OnMouseUp",600,5);
        }
    }
    static void Clip(string mode,bool alt,bool enabled=true,bool multiple=false,bool other=false){
        Track a=Item("moving",100,149),b=Item("other",other?350:200,other?399:249,1);var s=Scene(a,b);using(var t=new TimelineCanvas{State=s,Size=new Size(1500,440),PixelsPerSecond=100,Frame=mode=="left"?90:mode=="right"?175:other?900:300,ClipSnap=enabled}){
            t.ReadModifiers=()=>alt?Keys.Alt:Keys.None;t.CreateControl();t.SetTrackSelection(multiple?new[]{a,b}:new[]{a},a);t.RefreshLayout();var r=t.ClipBounds(a);int x=mode=="left"?(int)r.Left:mode=="right"?(int)r.Right:(int)r.Left+40,y=(int)r.Top+12,delta=mode=="left"?-24:mode=="right"?48:other?596:multiple?196:396;
            Mouse(t,"OnMouseDown",x,y);Mouse(t,"OnMouseMove",x+delta,y);bool snap=!alt&&enabled;
            if(mode=="left")Check(a.start==(snap?90:88),"Left edge cursor snap / Alt");else if(mode=="right")Check(a.end==(snap?174:173),"Right visible edge cursor snap / Alt");else if(other)Check(a.start==(snap?400:398),"Cross-lane clip adjacency");else if(multiple){Check(a.start==(snap?200:198)&&b.start==(snap?300:298),"Multiselection cursor snap changed relative offsets");}else Check(a.start==(snap?300:298),"Move clip cursor snap / Alt");
            Mouse(t,"OnMouseUp",x+delta,y);
        }
    }
    static void Slider(){
        using(var control=new SelectionTimeControl{Size=new Size(600,150)}){bool alt=false;control.ReadModifiers=()=>alt?Keys.Alt:Keys.None;control.SetView(50,1000,200,Item("a",100,500));control.CreateControl();var progress=(TrackBar)Get(control,"progress");int value=0;control.Seek=f=>{value=f;control.SetFrame(f);};control.SnapFrame=f=>225;
            Mouse(progress,"OnMouseDown",30,15);progress.Value=226;Check(value==226,"Inspector slider snapped while moving");Mouse(progress,"OnMouseUp",30,15);Check(value==225,"Inspector release did not snap");alt=true;Mouse(progress,"OnMouseDown",30,15);progress.Value=229;Mouse(progress,"OnMouseUp",30,15);alt=false;Pump(200);Check(value==229,"Inspector Alt snapped after release");
            Mouse(progress,"OnMouseDown",30,15);progress.Value=227;((CursorScrub)Get(control,"scrub")).Settle(false);Check(value==227&&((CursorScrub)Get(control,"scrub")).Guide==225,"Inspector idle should only show a hint");alt=true;typeof(Control).GetMethod("OnKeyDown",flags).Invoke(progress,new object[]{new KeyEventArgs(Keys.Alt|Keys.Menu)});Check(value==227&&((CursorScrub)Get(control,"scrub")).Guide==null,"Inspector Alt did not hide candidate");Mouse(progress,"OnMouseUp",30,15);
        }
    }
    static void Preference(string folder){
        var old=Json.Read<WorkspaceView>("{\"keySnap\":false}");Check(old.cursorSnap==null,"Old preference migration");old.Save(folder);using(var form=new MainForm(folder)){Check(((CheckBox)Get(form,"keySnap")).Checked,"New all-object snap should default on");var state=Scene(Item("a",100,150));Set(form,"state",state);Set(form,"document",new LoadData{state=state,room=new RoomData(),inventory=new Inventory{sprites=new string[0],objects=new string[0],scripts=new string[0]},waveform=new WaveData()});((TimelineCanvas)Get(form,"timeline")).State=state;Call(form,"SelectTrack",null,null);var box=(CheckBox)Get(form,"keySnap");Check(box.Enabled,"Global cursor snap still requires selection");box.Checked=false;Call(form,"SaveWorkspaceView");Check(WorkspaceView.Load(folder).cursorSnap==false,"Cursor snap choice was not saved");}
        using(var form=new MainForm(folder))Check(!((CheckBox)Get(form,"keySnap")).Checked,"Cursor snap choice was not restored");
    }
    [STAThread] static int Main(string[] args){try{Application.EnableVisualStyles();Targets();Scrub();TimelineScrub();foreach(string mode in new[]{"bar","left","right"}){Clip(mode,false);Clip(mode,true);Clip(mode,false,false);}Clip("bar",false,true,true);Clip("bar",false,true,false,true);Slider();Preference(args[0]);TimelineCursorRenderingTests.Run(args[0]);Console.WriteLine("PASS: all-object/native candidate index, raw pixel cursor and dashed guide, release-only snap, Alt/capture/project lifecycle, move/trims/multiselect, inspector sharing and static-layer paint reuse.");return 0;}catch(Exception error){Console.Error.WriteLine(error);return 1;}}
}
