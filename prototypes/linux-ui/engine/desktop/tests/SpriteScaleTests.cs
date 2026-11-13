using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
internal static class SpriteScaleTests
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Near(double a,double b){Check(Math.Abs(a-b)<.001,"Expected "+b+", got "+a);}
    static void Main(string[] args)
    {
        var t=new Track{kind="object",scale=2,scaleX=-1,scaleY=1.5};Check(new Track().scaleX==1&&new Track().scaleY==1,"Legacy default");
        Check(Motion.Fields(t).Contains("scaleX")&&Motion.Fields(t).Contains("scaleY"),"Timeline fields");
        t.keyframes.Add(new Keyframe{property="scaleX",frame=0,value=-2,easing="linear"});t.keyframes.Add(new Keyframe{property="scaleX",frame=10,value=2});Near(Motion.Value(t,"scaleX",5),0);Near(Motion.Value(t,"scaleX",0),-2);
        Near(t.Snapshot().scaleY,1.5);Near(Json.Read<Track>(Json.Write(t)).scaleX,-1);
        var root=args[0];Directory.CreateDirectory(Path.Combine(root,"sprites","marker"));using(var image=new Bitmap(4,2)){using(var g=Graphics.FromImage(image))g.Clear(Color.White);image.Save(Path.Combine(root,"sprites","marker","0.png"),ImageFormat.Png);}
        using(var store=new SpriteStore{Project=root}){
            store.Metadata["marker"]=new SpriteMeta{originX=1,originY=1,frames=1};
            var bounds=store.Bounds("marker",10,10,-2,3,90);Near(bounds.X,7);Near(bounds.Y,8);Near(bounds.Width,6);Near(bounds.Height,8);
            foreach(var blend in new[]{"normal","add"})using(var output=new Bitmap(24,24)){
                using(var g=Graphics.FromImage(output))using(SpriteBlending.Target(g,output)){g.Clear(Color.Black);g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;store.DrawExt(g,"marker",10,10,-2,3,90,1,Color.White,0,blend);}
                Check(output.GetPixel(9,11).R>240,"Signed rotated interior missing: "+blend);Check(output.GetPixel(5,11).R==0,"Origin/axis bounds leaked: "+blend);
            }
        }
        Console.WriteLine("PASS: signed axes, zero crossing, legacy defaults, serialization, rotated origin bounds and normal/add pixels");
    }
}
