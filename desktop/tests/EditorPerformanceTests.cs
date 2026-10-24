using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class EditorPerformanceTests
{
    private static readonly BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
    private static object Get(object x,string name){return x.GetType().GetField(name,flags).GetValue(x);}
    private static void Set(object x,string name,object value){x.GetType().GetField(name,flags).SetValue(x,value);}
    private static object Call(object x,string name,params object[] args){return x.GetType().GetMethod(name,flags).Invoke(x,args);}
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private static IEnumerable<Control> All(Control root){yield return root;foreach(Control child in root.Controls)foreach(var x in All(child))yield return x;}
    private static Track Current(MainForm form){return ((EditorState)Get(form,"state")).tracks[0];}
    private static void Write(MainForm form,string property,object value){var inspector=(ParameterInspector)Get(form,"properties");TypeDescriptor.GetProperties(inspector.SelectedObject)[property].SetValue(inspector.SelectedObject,value);}
    private static MainForm Editor(string folder,int tracks,int keys)
    {
        var form=new MainForm(folder);var state=new EditorState{duration=3000,music=""};for(int i=0;i<tracks;i++){var track=new Track{id="t"+i,name="发射器 "+i,kind="emitter",lane=i,start=0,end=3000};for(int j=0;j<keys;j++)track.keyframes.Add(new Keyframe{property=j%2==0?"x":"y",frame=j*10,value=j%2==0?400:304});state.tracks.Add(track);}Motion.Normalize(state);
        Set(form,"state",state);Set(form,"document",new LoadData{state=state,project=folder,projectName="isolated performance check",room=new RoomData(),inventory=new Inventory{objects=new string[0],sprites=new[]{"sprCherry"},scripts=new string[0]}});((TimelineCanvas)Get(form,"timeline")).State=state;Call(form,"SelectTrack",state.tracks[0],null);return form;
    }
    private static Dictionary<string,object> Profile(string folder,bool verify)
    {
        using(var form=Editor(folder,40,160)){
            var inspector=(ParameterInspector)Get(form,"properties");var rows=(TableLayoutPanel)Get(inspector,"rows");int disposed=0;foreach(var c in All(rows).ToArray())c.Disposed+=(s,e)=>disposed++;
            Write(form,"透明度",.7);Call(form,"Undo",false);var pushTimes=new List<double>();var undoTimes=new List<double>();
            var originalControls=All(rows).ToArray();int disposedBefore=disposed;
            for(int i=0;i<7;i++){var clock=Stopwatch.StartNew();Write(form,"透明度",.2+i*.05);pushTimes.Add(clock.Elapsed.TotalMilliseconds);clock.Restart();Call(form,"Undo",false);undoTimes.Add(clock.Elapsed.TotalMilliseconds);Check(Current(form).alpha==1,"Undo value");}
            bool retained=originalControls.SequenceEqual(All(rows));
            var result=new Dictionary<string,object>{{"tracks",40},{"keyframes",6400},{"editMedianMs",pushTimes.OrderBy(v=>v).ElementAt(3)},{"undoMedianMs",undoTimes.OrderBy(v=>v).ElementAt(3)},{"retainedControls",retained},{"initialControlsDisposed",disposed},{"controlsPerPage",originalControls.Length}};
            if(verify)Check(retained&&disposed==disposedBefore,"Scalar undo recreated property controls");Console.WriteLine(Json.Write(result));return result;
        }
    }
    private static void Verify(string folder)
    {
        using(var form=Editor(folder,1,0)){
            var inspector=(ParameterInspector)Get(form,"properties");var rows=(TableLayoutPanel)Get(inspector,"rows");var bag=inspector.SelectedObject;var alpha=TypeDescriptor.GetProperties(bag)["透明度"];var control=All(rows).OfType<ScrubNumber>().First();var scroll=(Panel)Get(inspector,"scroll");
            inspector.Size=new Size(420,640);inspector.PerformLayout();scroll.AutoScrollMinSize=new Size(0,3000);scroll.AutoScrollPosition=new Point(0,170);var position=scroll.AutoScrollPosition;
            Write(form,"透明度",.35);Call(form,"Undo",false);Check(ReferenceEquals(bag,inspector.SelectedObject)&&!control.IsDisposed&&Current(form).alpha==1&&scroll.AutoScrollPosition==position,"Scalar undo must reuse the inspector binding and scroll position");
            alpha.SetValue(bag,.45);Check(Current(form).alpha==.45,"Reused setter still writes into the discarded track");Call(form,"Undo",false);Call(form,"Undo",true);Check(Current(form).alpha==.45&&!control.IsDisposed,"Redo binding or controls");
            var x=(ValueProperty)TypeDescriptor.GetProperties(bag)["X"];x.Actions.ToggleRandom();Call(form,"ShowProperties");x=(ValueProperty)TypeDescriptor.GetProperties(inspector.SelectedObject)["X"];x.Actions.SetExpression("random_range(120,180)");Call(form,"Undo",false);var liveX=(ValueProperty)TypeDescriptor.GetProperties(inspector.SelectedObject)["X"];liveX.Actions.SetExpression("random_range(230,240)");Check(Motion.Value(Current(form),"x",0)>=230,"Rebound expression action writes into discarded state");
            liveX.Actions.ToggleKey();Call(form,"ShowProperties");Check(Current(form).keyframes.Any(k=>k.property=="x"),"Key toggle");var keyedControls=All(rows).ToArray();Call(form,"Undo",false);Check(!Current(form).keyframes.Any(k=>k.property=="x")&&!keyedControls.SequenceEqual(All(rows)),"A structural key/category change must update the inspector layout");
            Call(form,"SelectTrack",Current(form),null);Call(form,"RemoveSelection");Call(form,"Undo",false);Check(Get(form,"selectedTrack")==Current(form),"Undo deletion selection");
            var oldRoom=((LoadData)Get(form,"document")).room;var stage=(StageCanvas)Get(form,"stage");stage.Room=oldRoom;Call(form,"ReplaceRoom",oldRoom.Snapshot());Check(ReferenceEquals(oldRoom,((LoadData)Get(form,"document")).room),"Identical save response replaced the room");
            var changed=oldRoom.Snapshot();changed.width+=32;Call(form,"ReplaceRoom",changed);Check(ReferenceEquals(changed,stage.Room),"Changed room was ignored");
            var undo=(System.Collections.ICollection)Get(form,"undo");int before=undo.Count;Call(form,"PushUndo");int once=undo.Count;Call(form,"PushUndo");Check(undo.Count==once,"Duplicate captures added undo steps");Current(form).alpha=.987;Call(form,"PushUndo");Check(undo.Count==once+1,"Unmarked room/parameter edits must still create distinct history entries");
        }
        // Type-compatible controls must still be rebuilt when choice lists or action layout differ.
        using(var inspector=new ParameterInspector()){var a=new NativeProperties();a.Add("kind","group",typeof(string),()=>"a",v=>{},new[]{"a","b"});inspector.SelectedObject=a;var combo=All(inspector).OfType<ComboBox>().Single();var b=new NativeProperties();b.Add("kind","group",typeof(string),()=>"c",v=>{},new[]{"a","c"});inspector.SelectedObject=b;Check(combo.IsDisposed,"Changed choice list kept stale items");}
        double numberValue=1;string formula="";var actions=new ParameterActions{Random=()=>formula!="",Expression=()=>formula,SetExpression=s=>{formula=s;numberValue=27;}};
        using(var number=new ScrubNumber("test",typeof(double),()=>numberValue,v=>numberValue=(double)v,actions)){number.RefreshValue();Call(number,"EditText");var input=(TextBox)Get(number,"input");input.Text="random(30)";number.CommitText();Check(!input.Visible&&(double)Get(number,"displayedValue")==27&&(string)Get(number,"displayedExpression")=="random(30)","Committing a formula did not update the cached display");}
        Console.WriteLine("PASS: scalar undo/redo retains controls, refreshed delegates target restored objects, expression/key actions remain live, structural edits rebuild, unchanged room saves preserve preview state");
    }
    private static void Snapshots()
    {
        var child=new Track{id="child",kind="rod",shape="heart",count=6};child.keyframes.Add(new Keyframe{frame=20,property="radius",value=70,expression="random_range(20,80)"});child.bulletActions.Add(new BulletAction{type="spawn",templateId="other"});child.spawnActions.Add(new BulletAction{type="spawn",spawnRepeat=15});child.curvePoints.Add(new CurvePoint{x=3,y=8});child.expressions["x"]="random(600)";
        var state=new EditorState{player=new PlayerSettings(),plainRooms=new[]{"room"},nativeProject=new NativeProject{events=new[]{new NativeEvent{id="e",frame=20,snippet="x=1;"}},unresolved=new[]{new NativeEvent{id="u",reason="test"}},warnings=new[]{"warning"}},terrain=new Dictionary<string,object>{{"platforms",new object[]{new Dictionary<string,object>{{"x",12},{"y",80}}}}},tracks=new List<Track>{new Track{id="pattern",kind="pattern",children=new List<Track>{child}}}};state.playerTrace.Add(new PlayerTracePoint{frame=10,x=30,y=40});
        string expected=Json.Write(state);var selection=new UndoSnapshot{state=state,room=new RoomData{instances=new List<RoomInstance>{new RoomInstance{id="room-instance",x=12}}},selectedTrack="pattern",selectedTracks=new[]{"pattern"},selectedKeys=new[]{new UndoKey{trackId="child",property="radius",frame=20}}};var history=new UndoEntry(selection);var copy=history.Restore();Check(expected==Json.Write(copy.state),"Typed snapshot changed serialized project data");
        copy.state.player.gravity=10;copy.state.nativeTest.x=0;copy.state.plainRooms[0]="changed";copy.state.playerTrace[0].x=0;copy.state.nativeProject.events[0].frame=0;copy.state.nativeProject.unresolved[0].reason="changed";copy.state.nativeProject.warnings[0]="changed";copy.room.instances[0].x=99;copy.selectedTracks[0]="changed";copy.selectedKeys[0].frame=0;
        var c=copy.state.tracks[0].children[0];c.keyframes[0].value=1;c.expressions["x"]="1";c.bulletActions[0].templateId="changed";c.spawnActions[0].spawnRepeat=1;c.curvePoints[0].x=99;((Dictionary<string,object>)copy.state.terrain)["platforms"]="changed";
        var restored=history.Restore();Check(expected==Json.Write(restored.state)&&expected==Json.Write(state)&&restored.room.instances[0].x==12&&restored.selectedTracks[0]=="pattern"&&restored.selectedKeys[0].frame==20,"Mutable data leaked between history/source/restored copies");
        Check(SceneComparison.Same(restored.state,state),"Persisted scene equality");state.tracks[0].owner=state;Check(SceneComparison.Same(restored.state,state),"Preview owner references must not affect equality or recurse");state.nativeProject.events[0].snippet="x=2;";Check(!SceneComparison.Same(restored.state,state),"Native event changes ignored by history equality");Console.WriteLine("PASS: independent snapshots of nested templates, keys, actions, expressions, terrain, native metadata, player settings/trace and selection; persisted JSON unchanged");
    }
    [STAThread] public static int Main(string[] args){try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Language.Initialize(args[0]);var report=Profile(args[0],args[2]!="profile");File.WriteAllText(Path.Combine(args[0],args[1]+".json"),Json.Write(report));if(args[2]!="profile"){Verify(args[0]);Snapshots();}return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
