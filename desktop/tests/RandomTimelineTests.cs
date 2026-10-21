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
        }
        Console.WriteLine("PASS: native inspector random switch, expression editing and per-property animation on/off.");
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
    [STAThread] public static int Main(string[] args)
    {try{Expressions(args[0]);Inspector(args[1]);Timeline(args[2]);return 0;}catch(Exception error){Console.Error.WriteLine(error);return 1;}}
}
