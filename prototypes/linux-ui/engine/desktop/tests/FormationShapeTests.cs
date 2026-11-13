using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
#pragma warning disable 0649
internal sealed class GeometryCase {public string shape;public int sides;public double u,radius,orientation,aspect,inner,x,y,perimeter;}
internal sealed class ShapePoint {public double x,y;}
internal sealed class ShapeSample {public string id;public int frame;public ShapePoint[] points;public int[] images;}
internal sealed class ShapeFixture {public GeometryCase[] geometry;public EditorState state;public ShapeSample[] samples;}
#pragma warning restore 0649
internal static class FormationShapeTests
{
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private static void Near(double a,double b,double tolerance,string label){Check(Math.Abs(a-b)<=tolerance,label+": "+a+" / "+b);}
    private const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
    private static void Set(object value,string name,object data){value.GetType().GetField(name,Flags).SetValue(value,data);}
    private static object Get(object value,string name){return value.GetType().GetField(name,Flags).GetValue(value);}
    private static object Call(object value,string name,params object[] args){return value.GetType().GetMethod(name,Flags).Invoke(value,args);}
    [STAThread] public static int Main(string[] args)
    {
        try
        {
            var fixture=Json.Read<ShapeFixture>(File.ReadAllText(Path.Combine(args[0],"fixtures.json")));
            foreach(var c in fixture.geometry){var p=FormationShapes.Point(c.shape,c.u,c.radius,c.orientation,c.sides,c.aspect,c.inner);Near(p.X,c.x,1e-9,"Unit X "+c.shape);Near(p.Y,c.y,1e-9,"Unit Y "+c.shape);Near(FormationShapes.Perimeter(c.shape,c.radius,c.sides,c.aspect,c.inner),c.perimeter,1e-8,"Perimeter "+c.shape);}
            Motion.Normalize(fixture.state);
            var colorTrack=new Track{kind="rod",shape="rod",rainbow=true,rainbowMode="angle"};var origin=new Keyframe{x=400,y=304};
            foreach(var pair in new[]{Tuple.Create(new PointF(420,304),0),Tuple.Create(new PointF(400,284),8),Tuple.Create(new PointF(380,304),16),Tuple.Create(new PointF(400,324),24)})Check(Rainbow.Index(colorTrack,Rainbow.RodSequence(colorTrack,0,1,0,pair.Item1,origin),0)==pair.Item2,"Rainbow angles are not centered on object X/Y");
            Near(Rainbow.RodSequence(colorTrack,0,1,0,new PointF(420,304),origin,90),8,0,"Template rotation was omitted from rainbow angle");colorTrack.rainbowMode="distance";Near(Rainbow.RodSequence(colorTrack,0,1,0,new PointF(403,308),origin),5,0,"Rainbow distance is not geometric");colorTrack.rainbowMode="sequence";Near(Rainbow.RodSequence(colorTrack,0,5,0),2,0,"Legacy center rod colors changed");
            foreach(var sample in fixture.samples){var t=fixture.state.tracks.First(item=>item.id==sample.id);var actual=Motion.RodPoints(t,sample.frame);Check(actual.Length==sample.points.Length,"Count "+sample.id);for(int i=0;i<actual.Length;i++){Near(actual[i].X,sample.points[i].x,.0001,"Track X "+sample.id+" / "+sample.frame+" / "+i);Near(actual[i].Y,sample.points[i].y,.0001,"Track Y "+sample.id+" / "+sample.frame+" / "+i);}}
            foreach(var sample in fixture.samples){var t=fixture.state.tracks.First(item=>item.id==sample.id);if(!t.rainbow)continue;var points=Motion.RodPoints(t,sample.frame);var center=Motion.At(t,sample.frame);for(int i=0;i<points.Length;i++)Check(Rainbow.Index(t,Rainbow.RodSequence(t,i,points.Length,sample.frame,points[i],center),sample.frame)==sample.images[i],"Rainbow mismatch "+sample.id+" / "+sample.frame+" / "+i);}
            foreach(string shape in FormationShapes.Values.Where(s=>s!="rod"&&s!="ring"&&s!="polygon"))
            {
                using(var form=new MainForm(args[0]))
                {
                    var t=new Track{id="shape",kind="rod",name=FormationShapes.Name(shape),shape=shape,count=60,start=0,end=200};var state=new EditorState{duration=200,tracks=new List<Track>{t}};Motion.Normalize(state);
                    Set(form,"state",state);Set(form,"document",new LoadData{state=state,project=args[0],projectName="test",room=new RoomData(),waveform=new WaveData(),inventory=new Inventory{objects=new string[0],sprites=new[]{"sprCherry"},scripts=new string[0]}});
                    var timeline=(TimelineCanvas)Get(form,"timeline");timeline.State=state;Call(form,"SelectTrack",t,null);var inspector=(ParameterInspector)Get(form,"properties");var properties=TypeDescriptor.GetProperties(inspector.SelectedObject);
                    var values=properties["种类"].Converter.GetStandardValues();Check(values.Count==FormationShapes.Values.Length,"Missing shape choices");
                    foreach(string p in FormationShapes.Fields(shape))Check(properties[Motion.Name(p)]!=null,"Missing parameter "+shape+"/"+p);
                    properties["半径"].SetValue(inspector.SelectedObject,180.0);Call(form,"Undo",false);var restored=((EditorState)Get(form,"state")).tracks[0];Near(restored.radius,120,0,"Shape undo value");Check(Get(form,"selectedTrack")==restored&&timeline.SelectedTracks.Contains(restored),"Shape undo lost selection");
                    var copy=restored.Snapshot();Motion.SetKey(copy,"shapeAspect",50,1.6);Check(restored.keyframes.Count==0&&copy.shape==shape,"Shape copy shares keys or lost kind");
                    Rainbow.Enable(restored,true);Call(form,"SelectTrack",restored,null);var rainbowChoice=TypeDescriptor.GetProperties(inspector.SelectedObject)["彩虹分配方式"];Check(rainbowChoice.Converter.GetStandardValues().Count==3,"Rainbow modes missing from inspector");rainbowChoice.SetValue(inspector.SelectedObject,"距中心点距离");Check(restored.rainbowMode=="distance","Rainbow choice did not save stable mode");rainbowChoice.SetValue(inspector.SelectedObject,"中心点角度");Check(restored.Snapshot().rainbowMode=="angle"&&Json.Read<Track>(Json.Write(restored)).rainbowMode=="angle","Rainbow mode lost in copy/save");
                    if(shape=="ellipse")
                    {
                        Call(form,"SelectTrack",restored,null);var aspect=(ValueProperty)TypeDescriptor.GetProperties(inspector.SelectedObject)[Motion.Name("shapeAspect")];
                        aspect.SetValue(inspector.SelectedObject,-.65);Near(restored.shapeAspect,-.65,0,"Inspector rejected a negative base ratio");aspect.SetValue(inspector.SelectedObject,0.0);Near(restored.shapeAspect,0,0,"Inspector rejected zero height");
                        Call(form,"SetFrame",100);aspect.Actions.ToggleKey();Call(form,"ShowProperties");aspect=(ValueProperty)TypeDescriptor.GetProperties(inspector.SelectedObject)[Motion.Name("shapeAspect")];aspect.SetValue(inspector.SelectedObject,-.8);Near(restored.keyframes.Single(k=>k.property=="shapeAspect"&&k.frame==100).value,-.8,0,"Inspector rejected a negative key value");
                        aspect.Actions.SetExpression("random_range(-.75,-.75)");Near(Motion.Value(restored,"shapeAspect",100),-.75,0,"Expression rejected a negative ratio");var loaded=Json.Read<Track>(Json.Write(restored.Snapshot()));Near(loaded.keyframes.Single(k=>k.property=="shapeAspect").value,-.75,0,"Snapshot or serialization lost the negative ratio");
                    }
                }
            }
            using(var image=new Bitmap(1200,660))using(var g=Graphics.FromImage(image))
            {
                g.Clear(Color.FromArgb(24,28,38));int index=0;
                foreach(string shape in FormationShapes.Values.Where(s=>s!="rod"))
                {
                    int cx=150+(index%4)*300,cy=170+(index/4)*310;var t=new Track{shape=shape,kind="rod",count=60,x=cx,y=cy,radius=100,orientation=new[]{"star","star2","polygon"}.Contains(shape)?90:0};
                    g.DrawString(FormationShapes.Name(shape),Theme.Bold,Brushes.White,cx-80,cy-135);
                    foreach(var p in Motion.RodPoints(t,0))g.FillEllipse(Brushes.HotPink,p.X-3,p.Y-3,6,6);index++;
                }
                image.Save(Path.Combine(args[0],"formations.png"),System.Drawing.Imaging.ImageFormat.Png);
            }
            Console.WriteLine("PASS: formation geometry C#/Node parity; signed/zero height, animated flips, expressions, scrolling, parent rotation/scale, inspector editing, copying and undo selection.");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
