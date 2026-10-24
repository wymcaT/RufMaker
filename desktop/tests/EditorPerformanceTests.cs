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
    private static Dictionary<string,object> KeyProfile(string folder,bool verify)
    {
        using(var form=Editor(folder,40,160)){
            var inspector=(ParameterInspector)Get(form,"properties");var rows=(TableLayoutPanel)Get(inspector,"rows");var common=All(rows).ToArray();var add=new List<double>();var undo=new List<double>();bool retained=true;
            for(int i=0;i<7;i++){var clock=Stopwatch.StartNew();Call(form,"AddPropertyKey","alpha");add.Add(clock.Elapsed.TotalMilliseconds);var current=new HashSet<Control>(All(rows));retained&=common.All(c=>!c.IsDisposed&&current.Contains(c));Check(Current(form).keyframes.Any(k=>k.property=="alpha")&&Get(form,"selectedKey")!=null,"K did not create/select the key");clock.Restart();Call(form,"Undo",false);undo.Add(clock.Elapsed.TotalMilliseconds);current=new HashSet<Control>(All(rows));retained&=common.All(c=>!c.IsDisposed&&current.Contains(c));Check(!Current(form).keyframes.Any(k=>k.property=="alpha"),"Key undo failed");}
            if(verify)Check(retained,"Adding/removing key details recreated unrelated parameter rows");var data=new Dictionary<string,object>{{"keyAddMedianMs",add.OrderBy(t=>t).ElementAt(3)},{"keyUndoMedianMs",undo.OrderBy(t=>t).ElementAt(3)},{"commonControlsRetained",retained},{"baseControls",common.Length}};Console.WriteLine(Json.Write(data));return data;
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
            var plainControls=All(rows).ToArray();liveX.Actions.ToggleKey();Call(form,"ShowProperties");Check(Current(form).keyframes.Any(k=>k.property=="x")&&plainControls.SequenceEqual(All(rows)),"Animation must mark the existing row without moving or rebuilding it");var keyedControls=All(rows).ToArray();Call(form,"Undo",false);Check(!Current(form).keyframes.Any(k=>k.property=="x")&&keyedControls.SequenceEqual(All(rows)),"Undo animation toggle rebuilt parameter rows");
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
    private static void IncrementalRows(string folder)
    {
        using(var form=Editor(folder,1,0)){
            var inspector=(ParameterInspector)Get(form,"properties");var rows=(TableLayoutPanel)Get(inspector,"rows");inspector.Size=new Size(420,640);inspector.PerformLayout();
            Func<string,Control> editor=name=>{var descriptors=TypeDescriptor.GetProperties(inspector.SelectedObject);var map=(System.Collections.IDictionary)Get(inspector,"boundRows");return (Control)map[descriptors[name]].GetType().GetField("editor").GetValue(map[descriptors[name]]);};
            var alpha=editor("透明度");int originalRow=rows.GetRow(alpha);var controls=All(rows).ToArray();
            Call(form,"AddPropertyKey","alpha");Check(controls.All(c=>!c.IsDisposed&&All(rows).Contains(c))&&ReferenceEquals(editor("透明度"),alpha)&&rows.GetRow(alpha)==originalRow,"K moved or rebuilt original parameter rows");
            var toggle=All(alpha).OfType<CheckBox>().Single(c=>c.Name=="key:透明度");Check(toggle.Checked&&toggle.Text=="◆"&&TypeDescriptor.GetProperties(inspector.SelectedObject)["透明度"].Category=="参数","Keyed status not shown in the original category");
            var valueControl=editor("数值");Call(form,"SetFrame",30);Call(form,"AddPropertyKey","alpha");Check(ReferenceEquals(valueControl,editor("数值")),"Selecting a new key recreated its detail editor");Write(form,"数值",.42);Check(Current(form).keyframes.Single(k=>k.property=="alpha"&&k.frame==30).value==.42,"Reused detail editor wrote to the previous key");
            foreach(string property in new[]{"x","scale"}){Call(form,"AddPropertyKey",property);var selected=(ValueProperty)TypeDescriptor.GetProperties(inspector.SelectedObject)["数值"];Check(ReferenceEquals(valueControl,editor("数值"))&&selected.Actions.Property==property&&ScrubNumber.StepFor(1,false,"数值",selected.Actions.Property)==(property=="scale"?.01:1),"Reused key editor did not inherit the selected parameter's drag unit");}Call(form,"SelectTrack",Current(form),Current(form).keyframes.Single(k=>k.property=="alpha"&&k.frame==30));
            Call(form,"RemoveSelection");Check(!Current(form).keyframes.Any(k=>k.property=="alpha"&&k.frame==30)&&controls.All(c=>!c.IsDisposed&&All(rows).Contains(c)),"Deleting a key recreated base rows");
            Call(form,"SelectTrack",Current(form),null);var baseControls=All(rows).ToArray();var p=(ValueProperty)TypeDescriptor.GetProperties(inspector.SelectedObject)["透明度"];p.Actions.ToggleKey();Call(form,"ShowProperties");Check(baseControls.SequenceEqual(All(rows))&&!toggle.Checked&&toggle.Text=="◇","Disabling animation moved/recreated rows");
            Write(form,"拖影样式","自定义");Call(form,"ShowProperties");Check(ReferenceEquals(alpha,editor("透明度")),"Adding trail controls rebuilt base fields");var trailSpeed=editor("移动速度 / 像素每帧");Current(form).trailMode="fade";Call(form,"ShowProperties");Check(!trailSpeed.IsDisposed&&ReferenceEquals(trailSpeed,editor("移动速度 / 像素每帧")),"Changing trail mode recreated common trail fields");
            Current(form).motion="parabola";Call(form,"ShowProperties");Check(ReferenceEquals(alpha,editor("透明度")),"Adding motion fields recreated unrelated fields");
            var narrow=All(rows).ToArray();inspector.Size=new Size(800,700);inspector.PerformLayout();Check(narrow.All(c=>!c.IsDisposed&&All(rows).Contains(c)),"Changing sidebar width recreated controls");inspector.Size=new Size(420,640);inspector.PerformLayout();Check(narrow.All(c=>!c.IsDisposed&&All(rows).Contains(c)),"Stacked layout switch recreated controls");
            var cells=new HashSet<string>();foreach(Control item in rows.Controls)for(int column=rows.GetColumn(item);column<rows.GetColumn(item)+rows.GetColumnSpan(item);column++)Check(cells.Add(rows.GetRow(item)+":"+column),"Incremental layout placed two controls in the same cell");
            using(var surface=new Panel{Size=new Size(420,720),BackColor=Theme.Panel}){var parent=inspector.Parent;parent.Controls.Remove(inspector);surface.Controls.Add(inspector);inspector.Dock=DockStyle.Fill;surface.PerformLayout();using(var bitmap=new Bitmap(surface.Width,surface.Height)){surface.DrawToBitmap(bitmap,surface.ClientRectangle);bitmap.Save(Path.Combine(folder,"incremental-parameters.png"));}surface.Controls.Remove(inspector);parent.Controls.Add(inspector);}
        }
        Console.WriteLine("PASS: K/add/select/delete/toggle/undo retain parameter rows in place; detail bindings target current key; trail/motion changes and wide/narrow layouts update incrementally");
    }
    private static void DragUnits()
    {
        foreach(double value in new[]{0.0,.5,1,10,1000}){Check(ScrubNumber.StepFor(value,false,"缩放","scale")==.01,"Scale drag unit changed with its value");Check(ScrubNumber.StepFor(value,false,"X","x")==1&&ScrubNumber.StepFor(value,false,"Y","y")==1,"Coordinate drag unit changed with magnitude");}
        Check(ScrubNumber.StepFor(1,false,"镜头缩放","cameraZoom")==.01&&ScrubNumber.StepFor(.5,false,"透明度","alpha")==.01,"Normalized parameter drag unit");Check(ScrubNumber.StepFor(1000,true,"寿命")==1&&ScrubNumber.StepFor(3,false,"X 速度","velocityX")==.1&&ScrubNumber.StepFor(.4,false,"X 加速度","accelerationX")==.01,"Count/speed/acceleration drag units");Check(ScrubNumber.StepFor(1,false,"统一缩放")==.01&&ScrubNumber.StepFor(400,false,"参考玩家 X")==1,"Unanimated/native field drag units");Console.WriteLine("PASS: stable semantic drag units for scale/opacity/positions/frames/speed/acceleration and selected-key bindings");
    }
    [STAThread] public static int Main(string[] args){try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Language.Initialize(args[0]);if(args[2]=="key-profile"){File.WriteAllText(Path.Combine(args[0],args[1]+".json"),Json.Write(KeyProfile(args[0],false)));return 0;}var report=Profile(args[0],args[2]!="profile");foreach(var p in KeyProfile(args[0],args[2]!="profile"))report[p.Key]=p.Value;File.WriteAllText(Path.Combine(args[0],args[1]+".json"),Json.Write(report));if(args[2]!="profile"){Verify(args[0]);Snapshots();IncrementalRows(args[0]);DragUnits();}return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
