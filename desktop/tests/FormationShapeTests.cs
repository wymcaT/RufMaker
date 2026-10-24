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
internal sealed class ShapeSample {public string id;public int frame;public ShapePoint[] points;}
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
            foreach(var sample in fixture.samples){var t=fixture.state.tracks.First(item=>item.id==sample.id);var actual=Motion.RodPoints(t,sample.frame);Check(actual.Length==sample.points.Length,"Count "+t.shape);for(int i=0;i<actual.Length;i++){Near(actual[i].X,sample.points[i].x,.0001,"Track X "+t.shape);Near(actual[i].Y,sample.points[i].y,.0001,"Track Y "+t.shape);}}
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
            Console.WriteLine("PASS: formation geometry C#/Node parity; keyed dimensions, scrolling, parent rotation/scale, inspector choices, copying and undo selection.");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
