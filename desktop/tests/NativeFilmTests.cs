using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

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
        File.WriteAllText(Path.Combine(folder,"count.txt"),"4");var ended=NativeFilmBuilder.Generate(job,CancellationToken.None);Check(ended.frameCount==4,"A normal native game ending finalizes the captured frames");
    }
    public static int Main(string[] args)
    {
        try{if(args[0]=="--inspect"){
            Directory.CreateDirectory(args[2]);using(var reader=new NativeFilmReader(args[1])){foreach(int frame in new[]{0,Math.Min(1000,reader.Info.frameCount-1),reader.Info.frameCount/2,reader.Info.frameCount-1}.Distinct())reader.Frame(frame).Save(Path.Combine(args[2],frame+".png"),ImageFormat.Png);Console.WriteLine(NativeFilmReader.Json(reader.Info));}return 0;
        }Test(args[0]);return 0;}catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
