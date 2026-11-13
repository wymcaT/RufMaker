using System;
using System.Collections.Generic;
using System.Drawing;
internal static class TrailDepthTests
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static Track T(string kind,int depth,string mode="off"){return new Track{kind=kind,depth=depth,enabled=true,trailMode=mode};}
    static void Main()
    {
        var background=T("object",500);var emitter=T("emitter",-100,"fade");
        var tracks=new List<Track>{background,emitter};double depth=DrawingDepth.TrailLayer(tracks);
        Check(depth==-99,"Opaque background excluded");
        using(var image=new Bitmap(1,1))using(var g=Graphics.FromImage(image)){
            var layers=new DepthLayers();layers.Add(500,()=>g.Clear(Color.White));layers.Add(depth,()=>g.Clear(Color.Red));layers.Draw();
            Check(image.GetPixel(0,0).ToArgb()==Color.Red.ToArgb(),"White background must not cover trail pixels");
        }
        Check(DrawingDepth.TrailBase(new List<Track>())==-6999999,"Legacy floor");
        Check(DrawingDepth.TrailBase(new[]{T("rod",-50),T("emitter",125,"fade"),T("object",-250,"custom")})==126,"No-background behavior retained");
        Check(DrawingDepth.TrailBase(new[]{background,T("rod",400)})==401,"Custom rod anchors with trail off");
        Check(DrawingDepth.TrailBase(new[]{T("object",500,"fade"),emitter})==501,"Trailing object participates");
        var legacy=T("object",500,"legacy");legacy.trail=true;Check(DrawingDepth.TrailBase(new[]{legacy})==501,"Legacy trail flag");
        var bullet=new SimBullet{source=emitter,drawDepth=700};
        Check(DrawingDepth.TrailLayer(tracks,new[]{bullet})==701,"Live custom bullet depth");
        bullet.dead=true;Check(DrawingDepth.TrailLayer(tracks,new[]{bullet})==-99,"Dead bullet excluded");
        emitter.enabled=false;Check(DrawingDepth.TrailBase(tracks)==-6999999,"Disabled source excluded");
        Check(DrawingDepth.TrailBase(new[]{T("rod",Int32.MaxValue)})==2147483648.0,"Depth increment does not overflow");
        Console.WriteLine("PASS: shared trail depth/background pixel ordering, legacy, custom, live and trailing object cases");
    }
}
