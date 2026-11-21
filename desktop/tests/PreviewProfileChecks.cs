using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

// Descriptive pixel distances allow for GDI+ and hardware rounding at sprite edges.
internal static class PreviewProfileChecks
{
    private static byte[] Bytes(Bitmap image)
    {
        var rect=new Rectangle(0,0,image.Width,image.Height);
        var bits=image.LockBits(rect,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        try{
            var bytes=new byte[image.Width*image.Height*4];
            for(int y=0;y<image.Height;y++)Marshal.Copy(IntPtr.Add(bits.Scan0,y*bits.Stride),bytes,y*image.Width*4,image.Width*4);
            return bytes;
        }finally{image.UnlockBits(bits);}
    }
    private static void Compare(Bitmap a,Bitmap b,string name,TextWriter report)
    {
        if(a.Size!=b.Size)throw new InvalidDataException("Mismatched viewport");
        var left=Bytes(a);var right=Bytes(b);long sum=0,changed=0;
        for(int i=0;i<left.Length;i+=4){
            int worst=0;for(int k=0;k<4;k++){
                int diff=Math.Abs(left[i+k]-right[i+k]);worst=Math.Max(worst,diff);if(k<3)sum+=diff;
            }
            if(worst>8)changed++;
        }
        double count=a.Width*a.Height,mean=sum/(3*count),percent=100*changed/count;
        report.WriteLine(String.Format(CultureInfo.InvariantCulture,"{0},{1:F5},{2:F5}",name,mean,percent));
        if(mean>1.5||percent>1)throw new InvalidDataException("Pixel mismatch: "+name+" "+mean+" / "+percent+"%");
    }
    internal static void WithoutLibrary(string data,int frame)
    {
        var d=Json.Read<LoadData>(File.ReadAllText(data));Motion.Normalize(d.state);
        using(var cache=new PreviewCache{UseGpu=true})
        using(var sprites=new SpriteStore{Project=d.project,Metadata=d.spriteMeta})
        using(var matrix=new Matrix())
        using(var frameImage=PreviewRenderer.Render(d.state,d.room,d.inventory.objectSprites,sprites,frame,true,false,cache,
            matrix,d.room.width,d.room.height,null,new RectangleF(0,0,d.room.width,d.room.height)))
        using(var cpu=new Bitmap(Path.Combine(Path.GetDirectoryName(data),"original-cpu-"+frame+".png"))){
            if(cache.Timing.GpuSprites+cache.Timing.GpuEffects!=0)throw new Exception("Expected CPU fallback");
            Compare(cpu,frameImage,"missing-library",Console.Out);
        }
    }
    internal static void Verify(string folder,int start,int count,string data)
    {
        using(var report=new StreamWriter(Path.Combine(folder,"pixel-differences.csv"))){
            report.WriteLine("comparison,mean_RGB_error_0_to_255,percent_pixels_any_channel_error_over_8");
            foreach(string fixture in new[]{"original","stress"})foreach(int frame in new[]{start,start+count/2,start+count-1}){
                using(var cpu=new Bitmap(Path.Combine(folder,fixture+"-cpu-"+frame+".png")))
                using(var gpu=new Bitmap(Path.Combine(folder,fixture+"-gpu-"+frame+".png")))Compare(cpu,gpu,fixture+"-"+frame,report);
            }
            var d=Json.Read<LoadData>(File.ReadAllText(data));Motion.Normalize(d.state);
            using(var sprites=new SpriteStore{Project=d.project,Metadata=d.spriteMeta})
            using(var matrix=new Matrix())
            using(var cpu=new PreviewCache())
            using(var gpu=new PreviewCache{UseGpu=true}){
                const int w=728,h=546;matrix.Translate(327,210);matrix.Rotate(-22);matrix.Scale(.83f,.83f);matrix.Translate(-355,-240);
                var clip=new RectangleF(41,27,641,468);
                using(var reference=PreviewRenderer.Render(d.state,d.room,d.inventory.objectSprites,sprites,start,true,false,cpu,matrix,w,h,clip))
                using(var accelerated=PreviewRenderer.Render(d.state,d.room,d.inventory.objectSprites,sprites,start,true,false,gpu,matrix,w,h,clip)){
                    if(gpu.Timing.GpuSprites==0)throw new InvalidDataException("Rotated scene used CPU fallback");
                    Compare(reference,accelerated,"rotated-clipped",report);
                }
                foreach(string effect in new[]{"rgb","blur"})d.state.tracks.Add(new Track{id=effect,kind="effect",effect=effect,
                    start=0,end=d.state.duration,enabled=true,strength=9,fxSamples=9,fxOpacity=.8,fxAngle=32});
                var changed=d.state.Snapshot();
                foreach(float scale in new[]{.81f,.66f,1.13f}){
                    int cw=scale>1?992:w,ch=scale>1?740:h;
                    matrix.Reset();matrix.Translate(32.35f,20.7f);matrix.Scale(scale,scale);
                    var area=new RectangleF(32.35f,20.7f,d.room.width*scale,d.room.height*scale);
                    using(var reference=PreviewRenderer.Render(changed,d.room,d.inventory.objectSprites,sprites,start,true,false,cpu,matrix,cw,ch,null,area))
                    using(var accelerated=PreviewRenderer.Render(changed,d.room,d.inventory.objectSprites,sprites,start,true,false,gpu,matrix,cw,ch,null,area)){
                        Compare(reference,accelerated,"scaled-effects-"+scale,report);
                        if(gpu.Timing.Readbacks!=1)throw new Exception("Expected a single scaled scene readback");
                    }
                }
                // Seeking backwards must rebuild exactly the same historical formation poses.
                using(var future=PreviewRenderer.Render(changed,d.room,d.inventory.objectSprites,sprites,start+45,true,false,gpu)){}
                using(var reference=PreviewRenderer.Render(changed,d.room,d.inventory.objectSprites,sprites,start,true,false,cpu))
                using(var rewound=PreviewRenderer.Render(changed,d.room,d.inventory.objectSprites,sprites,start,true,false,gpu))
                    Compare(reference,rewound,"rewound-formations",report);
            }
        }
    }
}
