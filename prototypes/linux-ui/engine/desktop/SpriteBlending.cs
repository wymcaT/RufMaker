using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

// Blend each sprite separately: flattening a whole rod first loses overlapping light.
internal static class SpriteBlending
{
    [ThreadStatic] private static Dictionary<Graphics,Bitmap> targets;
    public static IDisposable Target(Graphics graphics,Bitmap bitmap)
    {
        if(targets==null)targets=new Dictionary<Graphics,Bitmap>();
        targets.Add(graphics,bitmap);return new Binding(graphics);
    }
    private sealed class Binding : IDisposable
    {
        private Graphics graphics;
        public Binding(Graphics value){graphics=value;}
        public void Dispose(){if(graphics!=null){targets.Remove(graphics);graphics=null;}}
    }
    public static void Add(Graphics graphics,PointF[] corners,Action<Graphics> draw)
    {
        Bitmap target;
        if(targets==null||!targets.TryGetValue(graphics,out target))throw new InvalidOperationException("Additive sprite drawing requires a bitmap render target.");
        using(var matrix=graphics.Transform)matrix.TransformPoints(corners);
        float left=corners[0].X,right=left,top=corners[0].Y,bottom=top;
        foreach(var p in corners){left=Math.Min(left,p.X);right=Math.Max(right,p.X);top=Math.Min(top,p.Y);bottom=Math.Max(bottom,p.Y);}
        if(float.IsNaN(left)||float.IsNaN(top)||float.IsInfinity(left)||float.IsInfinity(top))return;
        // Bound the work by this sprite, not by the entire viewport for every rod member.
        var bounds=Rectangle.Intersect(new Rectangle(0,0,target.Width,target.Height),Rectangle.FromLTRB(
            (int)Math.Min(target.Width,Math.Max(0,Math.Floor(left)-2)),(int)Math.Min(target.Height,Math.Max(0,Math.Floor(top)-2)),
            (int)Math.Max(0,Math.Min(target.Width,Math.Ceiling(right)+2)),(int)Math.Max(0,Math.Min(target.Height,Math.Ceiling(bottom)+2))));
        if(bounds.Width<=0||bounds.Height<=0)return;
        // Rasterize with the exact device transform and world clip before compositing.
        using(var layer=new Bitmap(bounds.Width,bounds.Height,PixelFormat.Format32bppArgb))
        {
            using(var g=Graphics.FromImage(layer))using(var transform=graphics.Transform)using(var clip=graphics.Clip)
            {
                g.PageUnit=graphics.PageUnit;g.PageScale=graphics.PageScale;transform.Translate(-bounds.X,-bounds.Y,MatrixOrder.Append);g.Transform=transform;g.SetClip(clip,CombineMode.Replace);
                g.InterpolationMode=graphics.InterpolationMode;g.PixelOffsetMode=graphics.PixelOffsetMode;g.SmoothingMode=graphics.SmoothingMode;
                draw(g);
            }
            graphics.Flush(FlushIntention.Sync);
            Composite(target,layer,bounds);
        }
    }
    internal static void Composite(Bitmap target,Bitmap source)
    {
        if(target.Size!=source.Size)throw new ArgumentException("Blend target size mismatch.");
        Composite(target,source,new Rectangle(Point.Empty,target.Size));
    }
    private static void Composite(Bitmap target,Bitmap source,Rectangle bounds)
    {
        // Lock from the bitmap origin and address the sprite region explicitly.
        // Some libgdiplus versions corrupt the bitmap when unlocking a nonzero
        // rectangle, even without writes. Full-image locking also follows GDI+
        // semantics on Windows; only bounds.Width pixels per affected row are copied.
        var dst=target.LockBits(new Rectangle(Point.Empty,target.Size),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
        try
        {
            var src=source.LockBits(new Rectangle(Point.Empty,source.Size),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            try
            {
                int bytes=source.Width*4;var d=new byte[bytes];var s=new byte[bytes];
                for(int y=0;y<source.Height;y++)
                {
                    IntPtr dp=IntPtr.Add(dst.Scan0,(bounds.Y+y)*dst.Stride+bounds.X*4),sp=IntPtr.Add(src.Scan0,y*src.Stride);
                    Marshal.Copy(dp,d,0,bytes);Marshal.Copy(sp,s,0,bytes);
                    for(int x=0;x<bytes;x+=4)
                    {
                        int a=s[x+3];if(a==0)continue;
                        // GM bm_add: source-alpha / one, including alpha channel.
                        for(int c=0;c<4;c++)d[x+c]=(byte)Math.Min(255,d[x+c]+(s[x+c]*a+127)/255);
                    }
                    Marshal.Copy(d,0,dp,bytes);
                }
            }
            finally{source.UnlockBits(src);}
        }
        finally{target.UnlockBits(dst);}
    }
}
