using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

internal static class TimelineRegressionTests
{
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    [STAThread] private static void Main(string[] args)
    {
        string root=args.Length>0?args[0]:"D:\\rufmaker";Directory.SetCurrentDirectory("D:\\rufmaker");
        var track=new Track{kind="rod",count=5,start=0,end=100,rodMode="endpoints",x1=100,y1=100,x2=500,y2=100};
        Motion.SetKey(track,"x1",0,100);Motion.SetKey(track,"x1",100,200);Motion.SetKey(track,"y1",0,100);Motion.SetKey(track,"y1",50,200);
        Check(Math.Abs(Motion.Value(track,"x1",50)-150)<.001,"Independent X1 interpolation");Check(Math.Abs(Motion.Value(track,"y1",50)-200)<.001,"Independent Y1 timing");
        var points=Motion.RodPoints(track,0);Check(points.Length==5&&points[0].X==100&&points[4].X==500&&points[1].X==200,"Five endpoint-inclusive bullets");
        track.rolling=true;track.rollSpeed=2;points=Motion.RodPoints(track,10);Check(points.Length==5&&points.All(p=>p.X>=110&&p.X<500),"Wrapped rolling geometry");
        foreach(string easing in Motion.EaseValues){Check(Motion.Ease(easing,0)==0&&Motion.Ease(easing,1)==1,"Easing endpoints: "+easing);for(int i=0;i<=100;i++)Check(!Double.IsNaN(Motion.Ease(easing,i/100.0)),"Easing finite: "+easing);}
        track.rodMode="center";track.rolling=false;track.x=400;track.y=304;track.angle=90;track.length=200;track.keyframes.Clear();var ends=Motion.Endpoints(track,0);Check(Math.Abs(ends[0].Y-404)<.001&&Math.Abs(ends[1].Y-204)<.001,"Center angle/length geometry");
        track.rodMode="anchor";ends=Motion.Endpoints(track,0);Check(ends[0].X==400&&ends[0].Y==304&&ends[1].X==500,"Anchored geometry");
        double audioStart,audioEnd;
        using(var audio=new NativeAudio())
        {
            audio.Load(Path.Combine(root,"projects","rufmaker-demo.gm82","Data","Music","BGM.ogg"));Check(audio.Duration>300,"OGG load");audio.Play(120);audioStart=audio.Position;Thread.Sleep(250);audio.Update();audioEnd=audio.Position;Check(audioEnd>audioStart+.1&&audioStart>=119.9,"Seek and audible-channel clock");audio.Pause();double paused=audio.Position;Thread.Sleep(100);audio.Update();Check(Math.Abs(audio.Position-paused)<.05,"Pause holds position");
        }
        using(var bridge=new NativeBridge(root))
        {
            var loaded=bridge.Call<LoadData>("load",new{}).GetAwaiter().GetResult();Motion.Normalize(loaded.state);
            using(var store=new SpriteStore{Project=loaded.project,Metadata=loaded.spriteMeta})
            {
                var timer=Stopwatch.StartNew();for(int i=0;i<15;i++)using(var bitmap=PreviewRenderer.Render(loaded.state,loaded.room,loaded.inventory.objectSprites,store,250+i,false,false)){}
                double mean=timer.Elapsed.TotalMilliseconds/15;Check(mean<100,"Preview throughput budget: "+mean);Console.WriteLine("Mean scene render: "+mean.ToString("0.0")+" ms");
            }
            using(var timeline=new TimelineCanvas{State=loaded.state,Width=1100,Height=300}){timeline.CreateControl();timeline.RefreshLayout();timeline.FitAll();timeline.Frame=12000;timeline.EnsureFrameVisible();timeline.SetZoom(400);timeline.Expand(loaded.state.tracks[0],true);using(var bmp=new Bitmap(1100,300))timeline.DrawToBitmap(bmp,new Rectangle(0,0,1100,300));}
        }
        Console.WriteLine("PASS independent keys, 22 easing modes, rod geometry, rolling, native OGG seek/pause, timeline >30 seconds. Audio "+audioStart.ToString("0.000")+" -> "+audioEnd.ToString("0.000"));
    }
}
