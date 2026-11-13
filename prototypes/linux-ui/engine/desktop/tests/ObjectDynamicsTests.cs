using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class ObjectDynamicsTests
{
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static void Near(double a,double b){Check(Math.Abs(a-b)<.00001,a+" != "+b);}
    private static readonly BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Put(object o,string name,object value){o.GetType().GetField(name,flags).SetValue(o,value);}
    [STAThread] public static int Main(string[] args)
    {
        if(args.Length>0&&args[0].StartsWith("--"))return TemplateSystemTests.Main(args);
        try{
            var shot=new Track{id="gravity",name="gravity",kind="emitter",start=0,end=0,count=1,speed=0,x=100,y=100,bulletGravity=1,imageIndex=2,imageSpeed=.5,life=30};
            var scene=new EditorState{duration=60,tracks=new System.Collections.Generic.List<Track>{shot}};Motion.Normalize(scene);
            var world=new BulletWorld(scene);world.Seek(0);var b=world.bullets.Single();Near(b.y,100);world.Seek(1);Near(b.y,101);world.Seek(2);Near(b.y,103);Check(ObjectDynamics.Image(b,2)==3,"Animation did not advance by age");
            world.Seek(0);world.Seek(2);Near(world.bullets.Single().y,103);
            var copy=Json.Read<EditorState>(Json.Write(scene.Snapshot()));Check(copy.tracks[0].bulletGravity==1&&copy.tracks[0].imageSpeed==.5,"Dynamics lost in JSON/snapshot");
            ObjectProperties.Hide(shot,5);Check(!ObjectProperties.Shows(shot,5)&&shot.bulletGravity==1,"Hiding changed physics");ObjectProperties.Attach(shot,5,0);Check(shot.bulletGravity==1,"Attaching reset existing settings");
            Near(ScrubNumber.StepFor(1,false,"", "bulletGravity"),.01);
            var scaled=new Track{imageDelay=1,imageSpeed=.5,bulletGravity=.4,keyframes=new System.Collections.Generic.List<Keyframe>{new Keyframe{property="bulletGravity",value=.4,frame=0}}};
            ObjectDynamics.Scale(scaled,.5);Check(scaled.imageDelay==1,"Animation delay uses inconsistent half-frame rounding");Near(scaled.imageSpeed,1);Near(scaled.bulletGravity,1.6);Near(scaled.keyframes[0].value,1.6);
            var converted=Json.Read<GmConversionResult>("{\"supported\":true,\"track\":{\"kind\":\"emitter\",\"imageDelay\":1,\"bulletGravity\":0.4}}");Check(converted.supported&&converted.track.imageDelay==1&&converted.track.bulletGravity==.4,"Conversion protocol did not deserialize");
            Language.Select(args[0],"en");foreach(var text in new[]{"弹幕物理","贴图动画","转换为可编辑物件…","动画延迟 / 帧"})Check(Language.Text(text)!=text,"Missing translation: "+text);Language.Select(args[0],"zh-CN");
            Application.EnableVisualStyles();
            using(var form=new MainForm(args[0])){
                var basic=new Track{id="ui",name="ui",kind="emitter",start=0,end=20};var state=new EditorState{tracks=new System.Collections.Generic.List<Track>{basic}};Motion.Normalize(state);
                Put(form,"state",state);Put(form,"document",new LoadData{state=state,room=new RoomData(),project=args[0],inventory=new Inventory{sprites=new[]{"sprBullet"}}});
                Action select=()=>typeof(MainForm).GetMethod("SelectTrack",flags).Invoke(form,new object[]{basic,null});select();
                var inspector=(ParameterInspector)typeof(MainForm).GetField("properties",flags).GetValue(form);
                Check(TypeDescriptor.GetProperties(inspector.SelectedObject)["重力 / 每帧"]==null,"Physics clutters a fresh object");
                ObjectProperties.Attach(basic,5,0);ObjectProperties.Attach(basic,6,0);select();var fields=TypeDescriptor.GetProperties(inspector.SelectedObject);
                Check(fields["重力 / 每帧"]!=null&&fields["动画速度 / 帧每帧"]!=null,"Attached modules are unreachable");
                fields["飞行阻力 / 每帧"].SetValue(inspector.SelectedObject,-3.0);Check(basic.bulletFriction==0,"Numeric friction can bypass its minimum");
                fields["动画延迟 / 帧"].SetValue(inspector.SelectedObject,.5);Check(basic.imageDelay==1,"Numeric delay is not rounded consistently");
                var parent=inspector.Parent;parent.Controls.Remove(inspector);
                using(var host=new Panel{Size=new Size(500,760),BackColor=Theme.Panel}){
                    host.Controls.Add(inspector);inspector.Dock=DockStyle.Fill;host.CreateControl();host.PerformLayout();inspector.PerformLayout();inspector.OpenSection("弹幕物理");
                    using(var image=new Bitmap(host.Width,host.Height)){host.DrawToBitmap(image,host.ClientRectangle);image.Save(Path.Combine(args[0],"properties.png"));}
                    host.Controls.Remove(inspector);
                }
                parent.Controls.Add(inspector);
                ((Timer)typeof(MainForm).GetField("saveTimer",flags).GetValue(form)).Stop();
            }
            Console.WriteLine("PASS: gravity at birth/next frames, animation, reverse seek, snapshot/JSON, optional property UI and numeric step.");return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
