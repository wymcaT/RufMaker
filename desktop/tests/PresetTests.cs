using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class PresetTests
{
    private static void Assert(bool value,string message){if(!value)throw new Exception(message);}
    private static void Near(double a,double b,string label){Assert(Math.Abs(a-b)<.00001,label+": "+a+" / "+b);}
    private static Track CameraTrack(){return new Track{id=Guid.NewGuid().ToString("N"),kind="camera",name="拉镜",start=10,end=210,x=400,y=304,cameraZoom=1.2,angle=3};}
    private static void Sprite(string project,string name,Color color)
    {
        string folder=Path.Combine(project,"sprites",name);Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"sprite.txt"),"frames=1\norigin_x=1\norigin_y=1\ncollision_shape=0\nalpha_tolerance=0\nper_frame_colliders=0\nbbox_type=0\nbbox_left=0\nbbox_top=0\nbbox_right=1\nbbox_bottom=1\n");
        using(var image=new Bitmap(2,2)){using(var g=Graphics.FromImage(image))g.Clear(color);image.Save(Path.Combine(folder,"0.png"),System.Drawing.Imaging.ImageFormat.Png);}
    }
    private static string NewProject(string scratch,string name)
    {string result=Path.Combine(scratch,name);Directory.CreateDirectory(Path.Combine(result,"sprites"));File.WriteAllText(Path.Combine(result,"sprites","index.yyd"),"");File.WriteAllText(Path.Combine(result,"sprites","tree.yyd"),"");return result;}
    private static void Browser(string folder)
    {
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var library=new PresetLibrary(Path.Combine(folder,"user-presets"));library.CreateFolder("","分类");library.Save("","测试预设",TrackPreset.Capture("测试预设",new[]{CameraTrack()},50),folder);
        using(var editor=new MainForm(folder)){
            var state=new EditorState();typeof(MainForm).GetField("state",flags).SetValue(editor,state);
            typeof(MainForm).GetField("document",flags).SetValue(editor,new LoadData{state=state,project=folder,inventory=new Inventory{objects=new string[0],sprites=new string[0],scripts=new[]{"alpha","beta"}}});
            var search=(TextBox)typeof(MainForm).GetField("search",flags).GetValue(editor);var list=(ListView)typeof(MainForm).GetField("resources",flags).GetValue(editor);
            string[] kinds={"presets","effect-presets","camera-presets","custom-presets"};int[] counts={MotionPresets.Names.Length,EffectPresets.Names.Length,CameraPresets.Names.Length,2};
            for(int i=0;i<kinds.Length;i++){
                typeof(MainForm).GetField("resourceKind",flags).SetValue(editor,kinds[i]);search.Text="   ";typeof(MainForm).GetMethod("FillResources",flags).Invoke(editor,null);
                Assert(list.Items.Count==counts[i],"Whitespace hid "+kinds[i]);
                typeof(MainForm).GetField("restoredDocumentView",flags).SetValue(editor,false);typeof(MainForm).GetField("restoredView",flags).SetValue(editor,new WorkspaceView{resourceKind=kinds[i],search="   "});
                typeof(MainForm).GetMethod("RestoreDocumentView",flags).Invoke(editor,null);
                Assert((string)typeof(MainForm).GetField("resourceKind",flags).GetValue(editor)==kinds[i]&&search.Text==""&&list.Items.Count==counts[i],"Restored preset category or blank filter was lost");
            }
            typeof(MainForm).GetField("resourceKind",flags).SetValue(editor,"camera-presets");search.Text=" 摇摆 ";Assert(list.Items.Count==1&&list.Items[0].Text=="柔和摇摆","Preset search must trim surrounding spaces and still filter");
            typeof(MainForm).GetField("resourceKind",flags).SetValue(editor,"scripts");search.Text="   ";Assert(list.Items.Count==2,"Whitespace hid normal resources");search.Text=" beta ";Assert(list.Items.Count==1&&list.Items[0].Text=="beta","Resource filtering must still work");
        }
        Console.WriteLine("PASS: blank searches show all action/effect/camera presets and custom presets; category restore and normal resource filtering work (no application window).");
    }
    [STAThread] private static int Main(string[] args)
    {
        try{
            if(args.Length==2&&args[0]=="--browser"){Browser(args[1]);return 0;}
            if(args.Length==1&&args[0]=="--icons"){
                for(int rebuild=0;rebuild<24;rebuild++)using(var icons=PresetIcons.Create()){
                    Assert(icons.Handle!=IntPtr.Zero&&icons.Images.Count==3,"Preset image list handle missing");
                    for(int i=0;i<icons.Images.Count;i++)using(var image=icons.Images[i])Assert(image.Width==24&&image.Height==24,"Preset icon was disposed before list creation");
                }
                Console.WriteLine("Preset image handle and bitmap lifetime checks passed (24 rebuilds, no application window).");return 0;
            }
            var generated=new List<Track>();
            foreach(string name in MotionPresets.Names){var t=CameraTrack();Motion.SetKey(t,"cameraZoom",10,1.2);Motion.SetKey(t,"cameraZoom",210,1.4);MotionPresets.Apply(t,name,10);Assert(t.start==10&&t.end==210,"Action changed track boundaries");Assert(t.keyframes.Count(k=>k.property=="cameraZoom")==2,"Action erased camera zoom keys");
                if(t.motion=="parabola")Assert(Motion.Fields(t).Contains("pathProgress"),"Camera path controls missing");if(t.motion=="orbit")Assert(Motion.Fields(t).Contains("orbitPhase"),"Camera orbit controls missing");
                for(int f=10;f<=210;f++){var p=Motion.At(t,f);Assert(!Double.IsNaN(p.x)&&!Double.IsInfinity(p.y),"Invalid action pose: "+name);}generated.Add(t);
            }
            foreach(string name in CameraPresets.Names){var t=CameraTrack();CameraPresets.Apply(t,name,10,50);Assert(t.keyframes.Count>0,"Camera has no animation");Assert(t.keyframes.All(k=>k.frame>=10&&k.frame<=210),"Camera key outside clip");for(int f=10;f<=210;f++){var p=Camera.At(t,f,new RoomData());Assert(p.zoom>0&&p.zoom<=20&&!Double.IsNaN(p.angle),"Invalid camera preset: "+name);}
                if(new[]{"柔和摇摆","呼吸拉镜","旋转缩放过渡","横向甩镜过渡","冲击回弹","轻微手持"}.Contains(name)){var end=Camera.At(t,210,new RoomData());Near(end.x,400,"Camera return X");Near(end.y,304,"Camera return Y");Near(end.zoom,1.2,"Camera return zoom");Near(end.angle,3,"Camera return angle");}generated.Add(t);
            }
            foreach(string name in EffectPresets.Names){var t=new Track{id=Guid.NewGuid().ToString("N"),kind="effect",start=10,end=110};EffectPresets.Apply(t,name);Assert(Effects.Values.Contains(t.effect),"Unknown preset effect");Assert(t.keyframes.All(k=>k.frame>=10&&k.frame<=110),"Effect key outside clip");generated.Add(t);}
            string scratch=args[0],source=NewProject(scratch,"source"),target=NewProject(scratch,"target");Sprite(source,"sprDemo",Color.Red);Sprite(source,Rainbow.Sprite,Color.Lime);Sprite(target,"sprDemo",Color.Blue);Sprite(target,Rainbow.Sprite,Color.Yellow);
            byte[] original=File.ReadAllBytes(Path.Combine(target,"sprites","sprDemo","0.png"));
            var bullet=new Track{id="source-bullet",kind="emitter",name="自定义弹幕",start=100,end=200,sprite="sprDemo",motion="velocity",velocityX=2,accelerationY=.2,angularSpeed=1,trailMode="shrink-fade",trail=true};Motion.SetKey(bullet,"x",150,456);Motion.SetKey(bullet,"speed",180,4);bullet.keyframes[0].easing="expsin";
            var rainbow=bullet.Snapshot();rainbow.id="rainbow";rainbow.rainbow=true;rainbow.sprite=Rainbow.Sprite;rainbow.rainbowSourceSprite="sprDemo";rainbow.rainbowSourceColor="#123456";
            var camera=CameraTrack();camera.start=140;camera.end=280;CameraPresets.Apply(camera,"柔和摇摆",140,50);
            var effect=new Track{id="fx",kind="effect",start=90,end=200};EffectPresets.Apply(effect,"淡入");
            var script=new Track{id="script",kind="script",start=120,end=130,code="phase=irandom(100);\n"};
            var selected=new[]{bullet,rainbow,camera,effect,script};var preset=TrackPreset.Capture("组合",selected,50);Assert(preset.tracks[0].start==10,"Preset did not normalize group origin");Assert(bullet.start==100&&bullet.keyframes[0].frame==150,"Capture modified source tracks");
            var library=new PresetLibrary(Path.Combine(scratch,"library"));library.CreateFolder("","运动");library.CreateFolder("运动","组合");string saved=library.Save("运动/组合","一组弹幕与拉镜",preset,source);
            Assert(new PresetLibrary(library.Root).Folders().Contains(Path.Combine("运动","组合")),"Folder persistence failed");
            var pasted=library.Instantiate(saved,target,500,50);Assert(pasted[0].start==510&&pasted[2].start==550&&pasted[3].start==500,"Group time alignment failed");Assert(pasted[0].keyframes[0].frame==560&&pasted[0].keyframes[0].easing=="expsin","Keyframe lost");Assert(pasted.Select(t=>t.id).Distinct().Count()==5&&pasted[0].id!=bullet.id,"Preset IDs not independent");Assert(pasted[4].code==script.code,"GML changed");
            Assert(pasted[0].sprite!="sprDemo"&&pasted[1].sprite!=Rainbow.Sprite,"Conflicting assets were overwritten");Assert(File.ReadAllBytes(Path.Combine(target,"sprites","sprDemo","0.png")).SequenceEqual(original),"Target sprite changed");Assert(File.ReadAllBytes(Path.Combine(target,"sprites",pasted[0].sprite,"0.png")).SequenceEqual(File.ReadAllBytes(Path.Combine(source,"sprites","sprDemo","0.png"))),"Carried sprite pixels differ");
            Assert(pasted[1].rainbowSprite==pasted[1].sprite,"Rainbow remap missing");var normalized=new EditorState{tracks=pasted};Motion.Normalize(normalized);Assert(pasted[1].sprite==pasted[1].rainbowSprite,"Rainbow normalization lost carried sprite");
            string index=File.ReadAllText(Path.Combine(target,"sprites","index.yyd"));var again=library.Instantiate(saved,target,0,50);Assert(again[0].sprite==pasted[0].sprite&&File.ReadAllText(Path.Combine(target,"sprites","index.yyd"))==index,"Repeated application duplicated sprites");
            var faster=preset.Instantiate(0,100);Assert(faster[0].start==20&&faster[0].end==220&&faster[0].keyframes[0].frame==120,"FPS did not scale key times");Near(faster[0].velocityX,1,"FPS velocity");Near(faster[0].accelerationY,.05,"FPS acceleration");Near(faster[0].keyframes.First(k=>k.property=="speed").value,2,"FPS keyed speed");
            saved=library.Move(saved,"运动","重命名组合");Assert(File.Exists(library.Resolve(saved)),"Rename/move preset failed");library.Move("运动","","动作分类");saved=Path.Combine("动作分类","重命名组合.rufpreset");
            string single=Path.Combine(scratch,"分享预设.rufpreset");library.Export(saved,single);var second=new PresetLibrary(Path.Combine(scratch,"library2"));Assert(second.Import(single,"")==1&&second.Import(single,"")==1,"Single import failed");Assert(Directory.GetFiles(second.Root,"*.rufpreset").Length==2,"Duplicate import overwrote preset");
            string bundle=Path.Combine(scratch,"动作分类.rufpresets");library.Export("动作分类",bundle);second.Import(bundle,"");Assert(File.Exists(second.Resolve(Path.Combine("动作分类","重命名组合.rufpreset"))),"Folder export lost classification");Assert(Directory.Exists(second.Resolve(Path.Combine("动作分类","组合"))),"Empty category lost on export");
            bool rejected=false;try{library.Resolve("../outside");}catch(InvalidDataException){rejected=true;}Assert(rejected,"Library accepted outside path");
            var payload=new EditorState{duration=500,fps=50,tracks=generated};File.WriteAllText(args[1],Json.Write(payload));File.WriteAllText(args[2],Json.Write(pasted));
            Console.WriteLine("Preset checks passed: all built-in actions/cameras/effects; grouped keys, FPS, sprite conflicts, rainbow, rename, folders and import/export.");return 0;
        }catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
