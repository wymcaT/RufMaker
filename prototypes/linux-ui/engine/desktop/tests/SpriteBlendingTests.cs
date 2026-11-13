using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
internal static class SpriteBlendingTests
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Near(int a,int b){Check(Math.Abs(a-b)<=2,"Expected "+b+", got "+a);}
    static void CompositeAlpha()
    {
        foreach(int alpha in new[]{0,1,64,128,254,255})
        foreach(int destinationAlpha in new[]{0,64,255})
        using(var target=new Bitmap(1,1,PixelFormat.Format32bppArgb))
        using(var source=new Bitmap(1,1,PixelFormat.Format32bppArgb))
        {
            var d=Color.FromArgb(destinationAlpha,90,180,240);var v=Color.FromArgb(alpha,200,100,50);
            target.SetPixel(0,0,d);source.SetPixel(0,0,v);SpriteBlending.Composite(target,source);
            var actual=target.GetPixel(0,0);
            Check(actual.ToArgb()==Color.FromArgb(Add(d.A,v.A,alpha),Add(d.R,v.R,alpha),Add(d.G,v.G,alpha),Add(d.B,v.B,alpha)).ToArgb(),"Straight-alpha composite mismatch: "+alpha+"/"+destinationAlpha+" "+actual);
        }
    }
    static int Add(int destination,int source,int alpha){return Math.Min(255,destination+(source*alpha+127)/255);}
    static void OffsetRasterRegions()
    {
        // Compare the cropped raster path against an independently rasterized full
        // layer, including untouched pixels outside the region. Nonzero origins
        // previously exposed libgdiplus partial-LockBits corruption.
        for(int variant=0;variant<7;variant++)
        using(var target=new Bitmap(19,17,PixelFormat.Format32bppArgb))
        using(var reference=new Bitmap(19,17,PixelFormat.Format32bppArgb))
        {
            for(int y=0;y<target.Height;y++)for(int x=0;x<target.Width;x++)target.SetPixel(x,y,Color.FromArgb(255,x*10,y*12,70));
            Action<Graphics> draw=g=>{using(var brush=new SolidBrush(Color.FromArgb(128,200,100,50)))g.FillRectangle(brush,0,0,4,3);};
            using(var g=Graphics.FromImage(target))using(var full=Graphics.FromImage(reference))using(SpriteBlending.Target(g,target))
            {
                foreach(var canvas in new[]{g,full})
                {
                    canvas.TranslateTransform(variant==4?18:variant==5?-2:8,variant==4?16:variant==5?-1:7);
                    if(variant==1)canvas.RotateTransform(90);
                    if(variant==6)canvas.RotateTransform(37);
                    if(variant==2)canvas.ScaleTransform(-1,2);
                    if(variant==3)canvas.SetClip(new Rectangle(1,0,2,2));
                }
                draw(full);
                SpriteBlending.Add(g,new[]{new PointF(0,0),new PointF(4,0),new PointF(0,3),new PointF(4,3)},draw);
                // Verify the Graphics remains usable after direct pixel writes.
                g.ResetTransform();g.ResetClip();g.FillRectangle(Brushes.Blue,0,0,1,1);
            }
            for(int y=0;y<target.Height;y++)for(int x=0;x<target.Width;x++)
            {
                var v=reference.GetPixel(x,y);var expected=x==0&&y==0?Color.Blue:Color.FromArgb(255,Add(x*10,v.R,v.A),Add(y*12,v.G,v.A),Add(70,v.B,v.A));
                var actual=target.GetPixel(x,y);Check(actual.ToArgb()==expected.ToArgb(),"Offset/transform raster mismatch variant "+variant+" at "+x+","+y+": "+actual+" vs "+expected);
            }
        }
    }
    static void SpriteGeometry(SpriteStore store)
    {
        foreach(var format in new[]{PixelFormat.Format32bppArgb,PixelFormat.Format32bppPArgb})
        using(var target=new Bitmap(23,19,format))
        using(var reference=new Bitmap(23,19,PixelFormat.Format32bppArgb))
        {
            // Two separated transformed sprite draws must not move or overwrite
            // earlier pixels. Test negative scale, rotation, and an offscreen edge.
            using(var g=Graphics.FromImage(target))using(var full=Graphics.FromImage(reference))using(SpriteBlending.Target(g,target))
            {
                g.Clear(Color.FromArgb(12,23,34));
                foreach(var canvas in new[]{g,full}){canvas.InterpolationMode=InterpolationMode.NearestNeighbor;canvas.PixelOffsetMode=PixelOffsetMode.Half;}
                store.DrawExt(full,"light",8,7,-2,3,90,.5,Color.FromArgb(100,80,60));
                store.DrawExt(g,"light",8,7,-2,3,90,.5,Color.FromArgb(100,80,60),0,"add");
                store.DrawExt(full,"light",22,18,2,2,0,1,Color.White);
                store.DrawExt(g,"light",22,18,2,2,0,1,Color.White,0,"add");
            }
            for(int y=0;y<target.Height;y++)for(int x=0;x<target.Width;x++)
            {
                var v=reference.GetPixel(x,y);var actual=target.GetPixel(x,y);
                Near(actual.R,Add(12,v.R,v.A));Near(actual.G,Add(23,v.G,v.A));Near(actual.B,Add(34,v.B,v.A));Check(actual.A==255,"Sprite alpha changed");
            }
        }
    }
    static void Main(string[] args)
    {
        var root=args[0];Directory.CreateDirectory(Path.Combine(root,"sprites","light"));
        using(var image=new Bitmap(2,2)){using(var g=Graphics.FromImage(image))g.Clear(Color.White);image.Save(Path.Combine(root,"sprites","light","0.png"),ImageFormat.Png);}
        using(var store=new SpriteStore{Project=root})using(var bitmap=new Bitmap(8,8))
        {
            using(var g=Graphics.FromImage(bitmap))using(SpriteBlending.Target(g,bitmap))
            {
                g.Clear(Color.FromArgb(20,30,40));g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
                g.TranslateTransform(2,2);g.SetClip(new Rectangle(0,0,1,2));
                store.Draw(g,"light",0,0,1,0,.5,Color.FromArgb(100,80,60),0,"add");
                store.Draw(g,"light",0,0,1,0,.5,Color.FromArgb(100,80,60),0,"add");
                g.ResetTransform();g.ResetClip();g.FillRectangle(Brushes.Blue,0,0,1,1);
            }
            var p=bitmap.GetPixel(2,2);Near(p.R,120);Near(p.G,110);Near(p.B,100);
            Check(bitmap.GetPixel(3,2).ToArgb()==Color.FromArgb(20,30,40).ToArgb(),"Clip leaked");
            Check(bitmap.GetPixel(0,0).ToArgb()==Color.Blue.ToArgb(),"Subsequent normal drawing corrupted");
            using(var g=Graphics.FromImage(bitmap))using(SpriteBlending.Target(g,bitmap))
            {
                store.Draw(g,"light",4,4,1,0,1,Color.White,0,"add");
                store.Draw(g,"light",4,4,1,0,0,Color.White,0,"add");
            }
            Console.WriteLine("Saturation pixel="+bitmap.GetPixel(4,4));
            Check(bitmap.GetPixel(4,4).ToArgb()==Color.White.ToArgb(),"Additive saturation failed");
        }
        CompositeAlpha();OffsetRasterRegions();
        using(var store=new SpriteStore{Project=root})SpriteGeometry(store);
        var track=new Track{blendMode="add"};Check(track.Snapshot().blendMode=="add","Snapshot lost blend mode");
        Console.WriteLine("Sprite additive pixel, tint, opacity, overlap, clip, restore, saturation snapshot, 18 straight-alpha cases and 7 whole-bitmap offset/transform checks passed.");
    }
}
