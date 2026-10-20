using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.Windows.Forms;

internal static class NativeFilmTests
{
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static string Hash(string file){using(var hash=SHA256.Create())return Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(file)));}
    private static void Test(string folder)
    {
        var job=new NativeFilmJob{exe=Path.Combine(folder,"producer.exe"),dir=folder,statusFile=Path.Combine(folder,"status.txt"),frameFile=Path.Combine(folder,"frame.bmp"),ackDirectory=Path.Combine(folder,"ack"),output=Path.Combine(folder,"base.rufvideo"),fps=50,width=64,height=48,frameCount=8,room="room",nativeIds=new[]{"baked"}};
        File.WriteAllText(Path.Combine(folder,"count.txt"),"8");File.WriteAllText(Path.Combine(folder,"color.txt"),"blue");NativeFilmBuilder.Generate(job,CancellationToken.None);string original=Hash(job.output);
        using(var old=new NativeFilmReader(job.output)){
            Check(old.Info.frameCount==8&&old.Info.playerX==400&&old.Info.playerY==304,"Manifest and fixed player metadata");
            Check(old.Frame(7).GetPixel(30,30).B>old.Frame(0).GetPixel(30,30).B+40,"Frame random access");Check(old.Frame(100).GetPixel(30,30)==old.Frame(7).GetPixel(30,30),"Clamp last frame");
            var cancelled=new CancellationTokenSource();cancelled.Cancel();try{NativeFilmBuilder.Generate(job,cancelled.Token);throw new Exception("Cancellation failed");}catch(OperationCanceledException){}Check(Hash(job.output)==original,"Cancelled generation must keep saved movie");
            File.WriteAllText(Path.Combine(folder,"color.txt"),"green");NativeFilmBuilder.Generate(job,CancellationToken.None);Check(old.Frame(2).GetPixel(30,30).B>100,"Existing reader keeps its previous snapshot during replacement");
        }
        using(var ready=new NativeFilmReader(job.output))Check(ready.Frame(2).GetPixel(30,30).G>100,"New reader loads regenerated movie");
        string spriteFolder=Path.Combine(folder,"sprites","overlay");Directory.CreateDirectory(spriteFolder);using(var sprite=new Bitmap(4,4))using(var g=Graphics.FromImage(sprite)){g.Clear(Color.White);sprite.Save(Path.Combine(spriteFolder,"0.png"),ImageFormat.Png);}
        var state=new EditorState{fps=50,duration=7};state.tracks.Add(new Track{id="live",kind="object",sprite="overlay",start=0,end=7,x=10,y=10,color="#ffffff"});
        var room=new RoomData{width=64,height=48};using(var sprites=new SpriteStore{Project=folder})using(var cache=new PreviewCache()){
            using(var image=PreviewRenderer.Render(state,room,new Dictionary<string,string>(),sprites,2,false,false,cache,nativeFilmPath:job.output))Check(image.GetPixel(11,11).R>240&&image.GetPixel(30,30).G>100,"Real-time object must composite over cached native frame");
            state.tracks.Add(new Track{id="effect",kind="effect",effect="color",color="#ff0000",fxOpacity=1,start=0,end=7});
            using(var image=PreviewRenderer.Render(state,room,new Dictionary<string,string>(),sprites,2,false,false,cache,nativeFilmPath:job.output))Check(image.GetPixel(30,30).R>240&&image.GetPixel(30,30).G<10,"Real-time full-screen effects apply to native film too");
        }
        Check(!Directory.GetFiles(folder,"*.partial-*").Any(),"No incomplete movie remains");Console.WriteLine("PASS: persisted frames, random seeking, cache reuse, atomic regeneration, cancellation and real-time layer/effect composition");
        string cacheFolder=Path.Combine(folder,"cache","native-film");Directory.CreateDirectory(cacheFolder);File.Copy(job.output,Path.Combine(cacheFolder,"base.rufvideo"));
        // SwitchProject temporarily disables the owner while LoadProject loads its cache.
        // Effective Control.Enabled must not decide whether a native project exists.
        using(var editor=new MainForm(folder)){
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;var nativeState=new EditorState{nativeProject=new NativeProject{imported=true}};
            typeof(MainForm).GetField("state",flags).SetValue(editor,nativeState);typeof(MainForm).GetField("document",flags).SetValue(editor,new LoadData{project=folder,state=nativeState});editor.Enabled=false;
            ((Task)typeof(MainForm).GetMethod("EnsureNativeFilm",flags).Invoke(editor,null)).GetAwaiter().GetResult();editor.Enabled=true;
            var nativeToggle=(CheckBox)typeof(MainForm).GetField("nativeRender",flags).GetValue(editor);var regenerate=(Button)typeof(MainForm).GetField("regenerateNativeFilm",flags).GetValue(editor);
            Check(nativeToggle.Checked&&regenerate.Enabled,"Native movie must load and regeneration remain enabled after switching a disabled owner");
        }
        Console.WriteLine("PASS: native film loads during project switching and regeneration remains available");
        File.WriteAllText(Path.Combine(folder,"count.txt"),"4");var ended=NativeFilmBuilder.Generate(job,CancellationToken.None);Check(ended.frameCount==4,"A normal native game ending finalizes the captured frames");
        File.WriteAllText(Path.Combine(folder,"view.txt"),"1");var withView=NativeFilmBuilder.Generate(job,CancellationToken.None);Check(withView.views.Length==4&&withView.views[2].x==4,"Per-frame native view persists in the movie");
        state.tracks.RemoveAll(t=>t.kind=="effect");using(var sprites=new SpriteStore{Project=folder})using(var cache=new PreviewCache())
        using(var image=PreviewRenderer.Render(state,room,new Dictionary<string,string>(),sprites,2,false,false,cache,nativeFilmPath:job.output)){Check(image.GetPixel(13,17).R>240&&image.GetPixel(10,10).R<10,"Native view transforms UI objects without transforming the baked movie twice");}
        state.tracks[0].x=40;using(var sprites=new SpriteStore{Project=folder})using(var cache=new PreviewCache())using(var transform=new CameraPose{x=32,y=24,zoom=.5}.WorldToView(64,48))
        using(var image=PreviewRenderer.Render(state,room,new Dictionary<string,string>(),sprites,2,false,false,cache,transform,64,48,new RectangleF(0,0,64,48),nativeFilmPath:job.output))Check(image.GetPixel(53,20).R<100,"A post camera cannot reveal UI objects clipped by the native viewport");
        var native=new NativeViewPose{enabled=true,x=100,y=48,w=400,h=304,portW=800,portH=608,outputW=800,outputH=608};var points=new[]{new PointF(300,200),new PointF(400,200)};
        using(var matrix=native.WorldToRoom(800,608))matrix.TransformPoints(points);using(var matrix=new CameraPose{x=400,y=304,zoom=.5,angle=30}.WorldToView(800,608))matrix.TransformPoints(points);
        Check(Math.Abs(points[0].X-400)<.01&&Math.Abs(points[0].Y-304)<.01&&Math.Abs(points[1].X-486.6025)<.01&&Math.Abs(points[1].Y-354)<.01,"Native view composes before RufMaker camera; physics coordinates remain native");
        Console.WriteLine("PASS: per-frame view capture, preview composition, native world coordinates and post camera transform");
    }
    [STAThread] public static int Main(string[] args)
    {
        try{if(args[0]=="--inspect"){
            Directory.CreateDirectory(args[2]);using(var reader=new NativeFilmReader(args[1])){foreach(int frame in new[]{0,Math.Min(1000,reader.Info.frameCount-1),reader.Info.frameCount/2,reader.Info.frameCount-1}.Distinct())reader.Frame(frame).Save(Path.Combine(args[2],frame+".png"),ImageFormat.Png);Console.WriteLine(NativeFilmReader.Json(reader.Info));}return 0;
        }Test(args[0]);return 0;}catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
