using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

#pragma warning disable 0649
internal sealed class RandomCase {public string expression,slot;public int seed;public double value;}
#pragma warning restore 0649
internal static class RandomTimelineTests
{
    private static readonly BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static object Get(object obj,string name){return obj.GetType().GetField(name,flags).GetValue(obj);}
    private static void Set(object obj,string name,object value){obj.GetType().GetField(name,flags).SetValue(obj,value);}
    private static object Call(object obj,string name,params object[] args){return obj.GetType().GetMethod(name,flags).Invoke(obj,args);}
    private static IEnumerable<Control> All(Control c){yield return c;foreach(Control child in c.Controls)foreach(var item in All(child))yield return item;}
    private static void Mouse(TimelineCanvas timeline,string action,int x,int y){Call(timeline,action,new MouseEventArgs(MouseButtons.Left,1,x,y,0));}
    private static void Expressions(string fixtures)
    {
        var data=Json.Read<RandomCase[]>(File.ReadAllText(fixtures));foreach(var c in data)Check(Math.Abs(RandomExpression.Evaluate(c.expression,c.seed,c.slot)-c.value)<1e-9,"C# and backend disagree: "+c.expression);
        foreach(string invalid in new[]{"choose()","random_range(2)","1/0","random(2);game_end()"}){bool rejected=false;try{RandomExpression.Evaluate(invalid,4,"x");}catch(FormatException){rejected=true;}Check(rejected,"Invalid expression accepted: "+invalid);}
        var t=new Track{id="item",kind="object",start=0,end=500};t.expressions["x"]="random_range(0,800)";var state=new EditorState{randomSeed=781,tracks=new List<Track>{t}};Motion.Normalize(state);double x=t.x;
        for(int f=0;f<500;f++)Check(Motion.Value(t,"x",f)==x,"Scrubbing rerolled a parameter");RandomParameters.Resolve(state);Check(x==t.x,"Saving changed a random value");state.randomSeed++;RandomParameters.Resolve(state);Check(x!=t.x,"Reroll did not change a random value");
        var copy=t.Snapshot();copy.expressions["x"]="random(1)";Check(t.expressions["x"]!="random(1)","Copy shares mutable expressions");
        Console.WriteLine("PASS: C#/Node sample parity, nested expressions, stable scrubbing, seed rerolls and independent copy data.");
    }
    private static void Inspector(string root)
    {
        using(var form=new MainForm(root)){
            var t=new Track{id="parameter",kind="object",start=0,end=500,name="test"};var state=new EditorState{duration=500,tracks=new List<Track>{t}};Motion.Normalize(state);
            Set(form,"state",state);Set(form,"document",new LoadData{state=state,project=root,projectName="test",room=new RoomData(),waveform=new WaveData(),inventory=new Inventory{objects=new string[0],sprites=new[]{"sprCherry"},scripts=new string[0]}});var timeline=(TimelineCanvas)Get(form,"timeline");timeline.State=state;Call(form,"SelectTrack",t,null);
            var inspector=(ParameterInspector)Get(form,"properties");var bag=inspector.SelectedObject;var parameter=(ValueProperty)TypeDescriptor.GetProperties(bag)["X"];var a=parameter.Actions;Check(a!=null&&!a.Keyed()&&!a.Random(),"New numeric parameter is not global by default");Check(All(inspector).OfType<CheckBox>().Any(c=>c.Name=="key:X"),"Per-property animation button missing");Check(All(inspector).OfType<CheckBox>().Any(c=>c.Name=="random:X"),"Random switch missing");
            a.ToggleRandom();a.SetExpression("random_range(100,200)");double sampled=t.x;Check(sampled>=100&&sampled<=200,"Random expression did not update the preview value");
            a.ToggleKey();Check(a.Keyed()&&t.keyframes.Count==1&&t.keyframes[0].property=="x"&&Math.Abs(sampled-t.keyframes[0].value)<1e-9,"Animation button did not preserve the current random value");
            a.ToggleKey();Check(!a.Keyed()&&Math.Abs(Motion.Value(t,"x",0)-sampled)<1e-9,"Disabling animation did not freeze the current value");
            Call(form,"RerollRandom");Check(state.randomSeed!=12345,"Preview reroll button did not change the seed");
            a.ToggleRandom();a.SetExpression("random_range(100,200)");var y=(ValueProperty)TypeDescriptor.GetProperties(inspector.SelectedObject)["Y"];y.Actions.ToggleKey();Call(form,"ShowProperties");
            var parent=inspector.Parent;parent.Controls.Remove(inspector);using(var surface=new Panel{Size=new Size(420,720),BackColor=Theme.Panel,Font=Theme.Font}){surface.Controls.Add(inspector);surface.PerformLayout();((Panel)Get(inspector,"scroll")).ScrollControlIntoView(All(inspector).OfType<CheckBox>().First(item=>item.Name=="random:X"));using(var bitmap=new Bitmap(surface.Width,surface.Height)){surface.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(Path.GetDirectoryName(root),"random-inspector.png"),System.Drawing.Imaging.ImageFormat.Png);}surface.Controls.Remove(inspector);}parent.Controls.Add(inspector);
            var stage=(StageCanvas)Get(form,"stage");
            Func<Track> currentTrack=()=>((EditorState)Get(form,"state")).tracks.First(item=>item.id=="parameter");
            TypeDescriptor.GetProperties(inspector.SelectedObject)["透明度"].SetValue(inspector.SelectedObject,0.35);
            Check(Math.Abs(currentTrack().alpha-0.35)<1e-9,"Inspector did not update alpha");
            var msg=new Message();Call(form,"ProcessCmdKey",msg,Keys.Control|Keys.Z);
            var afterUndo=currentTrack();
            Check(Math.Abs(afterUndo.alpha-1.0)<1e-9,"Ctrl+Z did not revert single property change");
            Check(Get(form,"selectedTrack")==afterUndo&&timeline.SelectedTrack==afterUndo&&timeline.SelectedTracks.Contains(afterUndo)&&stage.SelectedTrack==afterUndo,"Ctrl+Z lost selection of the edited track");
            Check(TypeDescriptor.GetProperties(inspector.SelectedObject)["透明度"]!=null&&!inspector.EditingText,"Inspector lost track properties or entered unintended text edit mode after Ctrl+Z");
            Call(form,"ProcessCmdKey",msg,Keys.Control|Keys.Y);
            var afterRedo=currentTrack();
            Check(Math.Abs(afterRedo.alpha-0.35)<1e-9&&Get(form,"selectedTrack")==afterRedo&&timeline.SelectedTracks.Contains(afterRedo),"Ctrl+Y did not preserve track selection");
            TypeDescriptor.GetProperties(inspector.SelectedObject)["启用"].SetValue(inspector.SelectedObject,false);
            TypeDescriptor.GetProperties(inspector.SelectedObject)["默认缓动"].SetValue(inspector.SelectedObject,"平滑");
            TypeDescriptor.GetProperties(inspector.SelectedObject)["名称"].SetValue(inspector.SelectedObject,"改名物件");
            Call(form,"ProcessCmdKey",msg,Keys.Control|Keys.Z);Check(currentTrack().name=="test"&&Get(form,"selectedTrack")==currentTrack(),"First sequential Ctrl+Z lost track selection");
            Call(form,"ProcessCmdKey",msg,Keys.Control|Keys.Z);Check(currentTrack().easing=="linear"&&Get(form,"selectedTrack")==currentTrack(),"Second sequential Ctrl+Z lost track selection");
            Call(form,"ProcessCmdKey",msg,Keys.Control|Keys.Z);Check(currentTrack().enabled&&Get(form,"selectedTrack")==currentTrack(),"Third sequential Ctrl+Z lost track selection");
            var keyToSelect=currentTrack().keyframes.First(k=>k.property=="y");
            Call(form,"SelectTrack",currentTrack(),keyToSelect);
            TypeDescriptor.GetProperties(inspector.SelectedObject)["数值"].SetValue(inspector.SelectedObject,456.0);
            Call(form,"ProcessCmdKey",msg,Keys.Control|Keys.Z);
            Check(Get(form,"selectedTrack")==currentTrack()&&Get(form,"selectedKey")!=null&&Math.Abs(((Keyframe)Get(form,"selectedKey")).value-304.0)<1e-9,"Ctrl+Z after editing a keyframe value lost keyframe or track selection");
            var second=new Track{id="second",kind="object",start=0,end=500,name="second",scale=1};((EditorState)Get(form,"state")).tracks.Add(second);
            timeline.SetTrackSelection(new[]{currentTrack(),second},currentTrack());
            TypeDescriptor.GetProperties(inspector.SelectedObject)["统一缩放"].SetValue(inspector.SelectedObject,2.5);
            Call(form,"ProcessCmdKey",msg,Keys.Control|Keys.Z);
            Check(timeline.SelectedTracks.Count==2&&Math.Abs(currentTrack().scale-1.0)<1e-9,"Ctrl+Z after group property edit lost multi-track selection");
            Call(form,"SelectTrack",currentTrack(),null);Call(form,"RemoveSelection");
            Check(((EditorState)Get(form,"state")).tracks.All(item=>item.id!="parameter"),"Track deletion did not occur");
            Call(form,"ProcessCmdKey",msg,Keys.Control|Keys.Z);
            Check(Get(form,"selectedTrack")==currentTrack()&&timeline.SelectedTracks.Contains(currentTrack()),"Undo deletion did not reselect the restored track");
            var room=((LoadData)Get(form,"document")).room;var instance=new RoomInstance{id="room-item",name="block",x=20,y=30};room.instances.Add(instance);Call(form,"SelectInstance",instance);Call(form,"PushUndo");instance.x=160;
            Call(form,"ProcessCmdKey",msg,Keys.Control|Keys.Z);
            var restoredInstance=((LoadData)Get(form,"document")).room.instances.First(item=>item.id==instance.id);
            Check(restoredInstance.x==20&&Get(form,"selectedInstance")==restoredInstance&&stage.SelectedInstance==restoredInstance,"Undo room edit lost instance selection");
        }
        Console.WriteLine("PASS: native inspector random switch, expression editing and per-property animation on/off.");
        Console.WriteLine("PASS: undo/redo retains object, keyframe and multi-selection, inspector context and restored room instances.");
    }
    private static void Timeline(string image)
    {
        var a=new Track{id="a",name="Emitter A",kind="emitter",lane=0,start=10,end=100};Motion.SetKey(a,"x",10,100);Motion.SetKey(a,"x",100,500);
        var b=new Track{id="b",name="Fade B",kind="effect",lane=0,start=120,end=200};Motion.SetKey(b,"fxOpacity",120,0);Motion.SetKey(b,"fxOpacity",200,1);
        var c=new Track{id="c",name="Camera C",kind="camera",lane=0,start=60,end=160};var d=new Track{id="d",name="Object D",kind="object",lane=1,start=200,end=300};
        var state=new EditorState{duration=600,laneCount=3,tracks=new List<Track>{a,b,c,d}};
        using(var timeline=new TimelineCanvas{Size=new Size(1250,600),State=state,PixelsPerSecond=100}){
            timeline.SelectTrack=(t,k)=>{timeline.SelectedTrack=t;timeline.SelectedKey=k;};int changed=0;timeline.Changed=()=>changed++;timeline.RefreshLayout();
            Check(timeline.ClipBounds(a).Y==timeline.ClipBounds(b).Y,"Non-overlapping clips do not share a lane row");Check(timeline.ClipBounds(c).Y!=timeline.ClipBounds(a).Y,"Overlapping clips hide each other");
            timeline.Expand(a,true);timeline.Expand(b,true);var rows=((IEnumerable)Get(timeline,"lanes")).Cast<object>().ToArray();Check(rows.Count(row=>(string)row.GetType().GetField("property").GetValue(row)=="x")==1,"Expanded clip does not have its own keyed row");timeline.Expand(a,false);rows=((IEnumerable)Get(timeline,"lanes")).Cast<object>().ToArray();Check(rows.Any(row=>(string)row.GetType().GetField("property").GetValue(row)=="fxOpacity"),"Collapsing one clip collapsed a sibling");
            timeline.Expand(b,false);var bar=timeline.ClipBounds(a);Mouse(timeline,"OnMouseDown",(int)(bar.X+bar.Width/2),(int)bar.Y+10);Mouse(timeline,"OnMouseMove",(int)(bar.X+bar.Width/2)+40,(int)timeline.ClipBounds(d).Y+10);Mouse(timeline,"OnMouseUp",(int)(bar.X+bar.Width/2)+40,(int)timeline.ClipBounds(d).Y+10);
            Check(a.lane==1&&a.start==30&&a.keyframes[0].frame==30,"Dragging did not move clip and keys to a new lane: "+a.lane+"/"+a.start+"/"+a.keyframes[0].frame);Check(b.lane==0&&b.start==120,"Dragging one clip changed its sibling");
            timeline.SetTrackSelection(new[]{a,d},a);timeline.RefreshLayout();bar=timeline.ClipBounds(a);int originalKey=a.keyframes[0].frame;Mouse(timeline,"OnMouseDown",(int)bar.X+1,(int)bar.Y+10);Mouse(timeline,"OnMouseMove",(int)bar.X+11,(int)bar.Y+10);Mouse(timeline,"OnMouseUp",(int)bar.X+11,(int)bar.Y+10);Check(a.start==35&&d.start==205&&a.keyframes[0].frame==originalKey,"Multi-clip edge trim moved keys or missed an object");
            Check(changed==2,"Timeline gesture did not commit once per edit");
            timeline.Expand(a,true);timeline.Expand(b,true);using(var bitmap=new Bitmap(timeline.Width,timeline.Height)){timeline.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(image,System.Drawing.Imaging.ImageFormat.Png);}
        }
        Console.WriteLine("PASS: mixed clips per lane, overlap stacking, independent expansion, drag between lanes, grouped trim and key offsets.");
    }
    private static Point KeyPoint(TimelineCanvas timeline,Track track,Keyframe key,bool expanded)
    {
        int y;
        if(expanded){var lane=((IEnumerable)Get(timeline,"lanes")).Cast<object>().First(row=>ReferenceEquals(row.GetType().GetField("track").GetValue(row),track)&&(string)row.GetType().GetField("property").GetValue(row)==key.property);y=timeline.AudioBottom+(int)lane.GetType().GetField("top").GetValue(lane)+(int)lane.GetType().GetField("height").GetValue(lane)/2;}
        else {var bar=timeline.ClipBounds(track);y=(int)bar.Bottom-3;}
        return new Point((int)(float)Call(timeline,"X",key.frame),y);
    }
    private static void KeyBounds()
    {
        var a=new Track{id="bounds-a",kind="object",lane=0,start=40,end=140};var x=new Keyframe{property="x",frame=60,value=10};var y=new Keyframe{property="y",frame=90,value=20};a.keyframes.AddRange(new[]{x,y});
        var b=new Track{id="bounds-b",kind="effect",lane=1,start=100,end=130};var alpha=new Keyframe{property="fxOpacity",frame=120,value=.5};b.keyframes.Add(alpha);
        var state=new EditorState{duration=500,fps=50,laneCount=2,tracks=new List<Track>{a,b}};
        using(var timeline=new TimelineCanvas{Size=new Size(1250,650),State=state,PixelsPerSecond=100})
        {
            timeline.SelectTrack=(t,k)=>{timeline.SelectedTrack=t;timeline.SelectedKey=k;};int begins=0,changes=0,seek=-1;timeline.BeginEdit=()=>begins++;timeline.Changed=()=>changes++;timeline.Seek=f=>seek=f;timeline.RefreshLayout();
            var p=KeyPoint(timeline,a,x,false);Mouse(timeline,"OnMouseDown",p.X,p.Y);Check((string)Get(timeline,"dragMode")=="key","Collapsed key was not hit");
            Mouse(timeline,"OnMouseMove",p.X-800,p.Y);Check(x.frame==40&&seek==40,"Single key crossed the left clip boundary");
            Mouse(timeline,"OnMouseMove",p.X+1000,p.Y);Check(x.frame==140&&seek==140,"Single key crossed the right clip boundary");
            Mouse(timeline,"OnMouseMove",p.X+16,p.Y);Mouse(timeline,"OnMouseUp",p.X+16,p.Y);Check(x.frame==68&&a.start==40&&a.end==140,"Key clamping changed clip length or lost drag origin");
            Check(begins==1&&changes==1,"Bounded drag committed more than one undo step");
            x.frame=60;timeline.Expand(a,true);timeline.Expand(b,true);timeline.SetSelection(new[]{x,y,alpha},a,x);p=KeyPoint(timeline,a,x,true);
            Mouse(timeline,"OnMouseDown",p.X,p.Y);Mouse(timeline,"OnMouseMove",p.X+1000,p.Y);Check(x.frame==70&&y.frame==100&&alpha.frame==130,"Cross-object selection did not stop at the tightest right boundary");
            Mouse(timeline,"OnMouseMove",p.X-800,p.Y);Check(x.frame==40&&y.frame==70&&alpha.frame==100,"Cross-property keys lost spacing or crossed a left boundary");Mouse(timeline,"OnMouseUp",p.X-800,p.Y);
            Check(a.start==40&&a.end==140&&b.start==100&&b.end==130&&timeline.SelectedKeys.Count==3,"Dragging keys changed clips or selection");
            timeline.MoveSelectedTo(500);Check(x.frame==70&&y.frame==100&&alpha.frame==130,"Move-to-playhead bypassed clip bounds");timeline.MoveSelectedTo(-100);Check(x.frame==40&&y.frame==70&&alpha.frame==100,"Move-to-playhead crossed a left boundary");
            timeline.SetSelection(new[]{x},a,x);var collision=new Keyframe{property="x",frame=140,value=80};a.keyframes.Add(collision);timeline.MoveSelectedTo(500);Check(x.frame==140&&!a.keyframes.Contains(collision)&&a.end==140,"Boundary collision did not retain the moved key");
            b.start=b.end=120;alpha.frame=120;timeline.SetSelection(new[]{alpha},b,alpha);timeline.MoveSelectedTo(0);Check(alpha.frame==120&&b.start==120&&b.end==120,"Zero-duration clip allowed key movement");
        }
        Console.WriteLine("PASS: collapsed/expanded key drags and move-to-playhead stay within each clip; multi-selection preserves offsets, boundaries, collisions and one undo step per gesture.");
    }
    private static void PasteAtPlayhead(string folder)
    {
        var a=new Track{id="paste-a",name="source",kind="rod",shape="ellipse",start=100,end=160,releaseFrame=140,x=120,y=210,rainbowMode="angle"};Motion.SetKey(a,"x",100,120);Motion.SetKey(a,"x",160,220);a.bulletActions.Add(new BulletAction{type="speed",start=5,to=4});
        var b=new Track{id="paste-b",name="effect",kind="effect",start=140,end=180};Motion.SetKey(b,"fxOpacity",140,0);Motion.SetKey(b,"fxOpacity",180,1);
        var group=new Track{id="paste-group",name="group",kind="group",start=50,end=160};var child=new Track{id="paste-child",name="child",parentId=group.id,kind="object",start=60,end=100};Motion.SetKey(child,"alpha",80,.5);
        var pattern=new Track{id="paste-pattern",name="pattern",kind="pattern",start=20,end=100,children=new List<Track>{new Track{id="local",kind="object",start=10,end=30}}};Motion.SetKey(pattern.children[0],"x",15,40);
        var state=new EditorState{duration=500,tracks=new List<Track>{a,b,group,child,pattern}};Motion.Normalize(state);
        using(var form=new MainForm(folder))
        {
            Set(form,"state",state);Set(form,"document",new LoadData{state=state,project=folder,projectName="paste test",room=new RoomData(),waveform=new WaveData(),inventory=new Inventory{objects=new string[0],sprites=new[]{"sprCherry"},scripts=new string[0]}});
            var timeline=(TimelineCanvas)Get(form,"timeline");timeline.State=state;Func<EditorState> scene=()=>((EditorState)Get(form,"state"));Func<string,Track> original=id=>scene().tracks.Single(t=>t.id==id);
            Action paste=()=>((System.Threading.Tasks.Task)Call(form,"PasteSelection")).GetAwaiter().GetResult();
            Call(form,"SelectTrack",a,null);Call(form,"CopySelection");Call(form,"SetFrame",300);paste();var copy=timeline.SelectedTracks.Single();
            Check(copy.id!=a.id&&copy.start==300&&copy.end==360&&copy.releaseFrame==340,"Single clip or release did not align to playhead");Check(copy.keyframes.Select(k=>k.frame).SequenceEqual(new[]{300,360})&&copy.x==120&&copy.y==210&&copy.rainbowMode=="angle"&&copy.bulletActions[0].start==5,"Paste changed positions, keys, appearance or bullet-age timing");
            Call(form,"SetFrame",380);paste();var repeated=timeline.SelectedTracks.Single();Check(repeated.start==380&&repeated.end==440&&original(a.id).start==100&&original(a.id).keyframes[0].frame==100,"Repeated paste moved source or clipboard origin");
            int count=scene().tracks.Count;string repeatedId=repeated.id;Call(form,"Undo",false);Check(scene().tracks.Count==count-1&&!scene().tracks.Any(t=>t.id==repeatedId),"Undo paste did not remove one copy");Call(form,"Undo",true);var redone=scene().tracks.Single(t=>t.id==repeatedId);Check(redone.start==380&&redone.end==440&&redone.releaseFrame==420&&redone.keyframes[1].frame==440&&timeline.SelectedTracks.All(t=>scene().tracks.Contains(t)),"Redo paste lost timing or retained stale selection references");
            Call(form,"SetFrame",490);paste();Check(timeline.SelectedTracks.Single().start==490&&timeline.SelectedTracks.Single().end==550&&scene().duration==550,"Paste near project end was clipped or shifted away from playhead");
            timeline.SetTrackSelection(new[]{original(a.id),original(b.id)},original(a.id));Call(form,"CopySelection");Call(form,"SetFrame",0);paste();var mixed=timeline.SelectedTracks.ToArray();var rod=mixed.Single(t=>t.kind=="rod");var effect=mixed.Single(t=>t.kind=="effect");Check(rod.start==0&&rod.end==60&&rod.releaseFrame==40&&effect.start==40&&effect.end==80&&effect.keyframes[1].frame==80,"Multi-kind paste lost relative times at frame zero");
            Call(form,"SelectTrack",original(group.id),null);Call(form,"CopySelection");Call(form,"SetFrame",200);paste();var children=timeline.SelectedTracks.ToArray();var pastedGroup=children.Single(t=>t.kind=="group");var pastedChild=children.Single(t=>t.kind=="object");Check(pastedGroup.start==200&&pastedChild.start==210&&pastedChild.keyframes.Single().frame==230&&pastedChild.parentId==pastedGroup.id,"Group paste lost child timing or remapped parent");
            Call(form,"SelectTrack",original(pattern.id),null);Call(form,"CopySelection");Call(form,"SetFrame",90);paste();var pastedPattern=timeline.SelectedTracks.Single();Check(pastedPattern.start==90&&pastedPattern.end==170&&pastedPattern.children[0].start==10&&pastedPattern.children[0].keyframes[0].frame==15,"Paste shifted pattern-local clocks");
        }
        Console.WriteLine("PASS: paste aligns clips to current playhead, retains keys/releases and multi/group offsets, extends duration, preserves local clocks and supports repeated paste/undo/redo.");
    }
    [STAThread] public static int Main(string[] args)
    {try{Expressions(args[0]);Inspector(args[1]);Timeline(args[2]);KeyBounds();PasteAtPlayhead(args[1]);return 0;}catch(Exception error){Console.Error.WriteLine(error);return 1;}}
}
