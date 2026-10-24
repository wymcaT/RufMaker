using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

#pragma warning disable 0649
internal sealed class ExpressionCase {public string expression;public double value;}
#pragma warning restore 0649
internal static class PlayerExpressionTests
{
    private const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static object Get(object obj,string field){return obj.GetType().GetField(field,flags).GetValue(obj);}
    private static void Set(object obj,string field,object value){obj.GetType().GetField(field,flags).SetValue(obj,value);}
    private static object Call(object obj,string method,params object[] args){return obj.GetType().GetMethod(method,flags).Invoke(obj,args);}
    private static IEnumerable<Control> All(Control control){yield return control;foreach(Control child in control.Controls)foreach(var item in All(child))yield return item;}
    [STAThread] public static int Main(string[] args)
    {
        try{
            var context=new ExpressionContext{playerX=527,playerY=301,x=100,y=200,x1=10,y1=20,x2=30,y2=40,frame=157,age=37,direction=359,speed=3,fps=50};foreach(var item in Json.Read<ExpressionCase[]>(File.ReadAllText(args[0])))Check(Math.Abs(RandomExpression.Evaluate(item.expression,991,"fixture",context)-item.value)<1e-8,"C#/Node mismatch: "+item.expression);
            var t=new Track{id="test",name="Homing",kind="emitter",start=0,end=100,x=100,y=200,motion="velocity",velocityX=2};t.expressions["x"]="x+10";t.expressions["angle"]="point_direction(x,y,player.x,player.y)";var state=new EditorState{duration=200,tracks=new List<Track>{t}};Motion.Normalize(state);double angle=Motion.At(t,10).angle;Check(Math.Abs(angle-ExpressionContext.Direction(130,200,400,304))<1e-8,"Aiming ignores animated position");for(int i=0;i<5;i++)RandomParameters.Resolve(state);Check(t.x==100&&Motion.At(t,10).angle==angle,"Saving drifts live expressions");state.nativeTest.x=50;RandomParameters.Attach(state);Check(Motion.At(t,10).angle!=angle,"Reference player change ignored");
            var rod=new Track{id="rod",kind="rod",start=0,end=100};rod.expressions["x1"]="player.x-100";rod.expressions["y1"]="player.y";rod.expressions["x2"]="player.x+100";rod.expressions["y2"]="player.y+100";rod.expressions["spriteAngle"]="point_direction(x1,y1,x2,y2)";Motion.Normalize(new EditorState{tracks=new List<Track>{rod}});Check(Math.Abs(Motion.Value(rod,"spriteAngle",10)-333.434948822922)<1e-9,"Animated endpoints unavailable to formula");
            state.nativeTest=new NativeTestPosition{x=0,y=-100};RandomParameters.Attach(state);t.flightMode="turn";t.turnRate=3;t.homingDelay=0;var path=BulletFlight.Path(t,0,10,0,0,0,3,0);Check(path.Length==11&&Math.Abs(path[10].angle-30)<1e-8&&path[10].y<0,"Homing does not turn by three degrees per frame");t.homingDelay=5;path=BulletFlight.Path(t,0,10,0,0,0,3,0);Check(path[5].angle==0&&Math.Abs(path[10].angle-15)<1e-8,"Homing delay ignored");t.flightMode="expression";t.flightExpression="turn_towards(direction,point_direction(x,y,player.x,player.y),3)";t.homingDelay=0;path=BulletFlight.Path(t,0,10,0,0,0,3,0);Check(Math.Abs(path[10].angle-30)<1e-8,"Custom flight formula uses emitter position or direction");
            using(var form=new MainForm(args[1])){
                Set(form,"state",state);Set(form,"document",new LoadData{state=state,project=args[1],projectName="fixture",room=new RoomData(),waveform=new WaveData(),inventory=new Inventory{objects=new string[0],sprites=new[]{"sprCherry"},scripts=new string[0]}});var timeline=(TimelineCanvas)Get(form,"timeline");timeline.State=state;Call(form,"SelectTrack",t,null);var inspector=(ParameterInspector)Get(form,"properties");Check(All(inspector).OfType<Button>().Any(button=>button.Name=="expression:X"),"Formula picker missing");
                Motion.SetKey(t,"alpha",0,.3);Call(form,"FillExpressionKeys",new[]{t},"angle","point_direction(x,y,player.x,player.y)",10,20);Check(t.keyframes.Count(k=>k.property=="angle"&&k.frame>=10&&k.frame<=20)==11,"Fill must include both range ends");Check(t.keyframes.Count(k=>k.property=="alpha")==1,"Fill alters another property");Check(t.keyframes.Where(k=>k.property=="angle"&&k.frame>=10&&k.frame<=20).All(k=>k.easing=="step"&&k.expression.Contains("player")),"Filled keys lost live expression or frame sampling");double a=Motion.Value(t,"angle",15);state.nativeTest.y=0;RandomParameters.Attach(state);Check(Motion.Value(t,"angle",15)!=a,"Filled key does not read changed player");
                Call(form,"Undo",false);state=(EditorState)Get(form,"state");Check(state.tracks[0].keyframes.All(k=>k.property!="angle"),"Fill is not a single undo operation");
            }
            using(var dialog=new ExpressionEditor("angle","point_direction(x,y,player.x,player.y)",20,0,100,200,true,(text,frame)=>RandomExpression.Evaluate(text,991,"fixture",context))){Language.Attach(dialog);var content=dialog.Controls[0];dialog.Controls.Remove(content);using(var surface=new Panel{Size=dialog.ClientSize,Font=dialog.Font,BackColor=dialog.BackColor,ForeColor=dialog.ForeColor}){surface.Controls.Add(content);surface.PerformLayout();using(var bitmap=new Bitmap(surface.Width,surface.Height)){surface.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(args[2],"expression-editor.png"));}surface.Controls.Remove(content);}dialog.Controls.Add(content);}
            Console.WriteLine("PASS: C#/Node formula parity, stable saves, animated aiming, reference player, homing/delay/custom flight, native picker, inclusive fill, independent properties and undo. No editor session or game launched.");return 0;
        }catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
