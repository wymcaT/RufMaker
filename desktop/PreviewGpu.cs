using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

// Owned exclusively by one preview worker. Only isolated sprite layers and full-frame
// image operations enter this device. Game simulation and depth ordering stay on the CPU.
internal sealed class PreviewGpu : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Quad
    {
        public int tile;
        public float x,y,sx,sy,angle,ox,oy;
        public uint rgba;
    }
    private struct Entry
    {
        public Bitmap image;
        public double x,y,sx,sy,angle,alpha;
        public int ox,oy;
        public Color tint;
    }
    internal const int MinimumBatch=80;
    private const string Library=@"assets\native\x86\RufPreviewGpu.dll";
    private static class Native
    {
        [DefaultDllImportSearchPaths(DllImportSearchPath.ApplicationDirectory|DllImportSearchPath.System32)]
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Unicode,ExactSpelling=true)]
        internal static extern IntPtr rp_create([Out]StringBuilder name,int length);
        [DefaultDllImportSearchPaths(DllImportSearchPath.ApplicationDirectory|DllImportSearchPath.System32)]
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern int rp_base(IntPtr device,int w,int h,IntPtr data,int stride);
        [DefaultDllImportSearchPaths(DllImportSearchPath.ApplicationDirectory|DllImportSearchPath.System32)]
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern int rp_frame(IntPtr device,[In] Quad[] q,int count,[In]float[] matrix,int w,int h,[In]int[] clip,[In]int[] modes,[In]float[] amounts,[In]int[] offsets,int effects,int rw,int rh,[In]float[] region,IntPtr dest,int stride);
        [DefaultDllImportSearchPaths(DllImportSearchPath.ApplicationDirectory|DllImportSearchPath.System32)]
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern void rp_release(IntPtr device);
        [DefaultDllImportSearchPaths(DllImportSearchPath.ApplicationDirectory|DllImportSearchPath.System32)]
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern int rp_upload(IntPtr device,int w,int h,IntPtr data,int stride);
        [DefaultDllImportSearchPaths(DllImportSearchPath.ApplicationDirectory|DllImportSearchPath.System32)]
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern int rp_draw(IntPtr device,[In] Quad[] quads,int count,[In]float[] m,int w,int h,IntPtr data,int stride);
        [DefaultDllImportSearchPaths(DllImportSearchPath.ApplicationDirectory|DllImportSearchPath.System32)]
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern int rp_effect(IntPtr device,int w,int h,IntPtr src,int pitch,IntPtr dest,int stride,int mode,float opacity,[In]int[] offsets);
    }
    private IntPtr device;
    private bool failed;
    private string adapter;
    private readonly Dictionary<Bitmap,int> textures=new Dictionary<Bitmap,int>();
    private readonly List<Entry> entries=new List<Entry>();
    private Quad[] quads=new Quad[128];
    private readonly int[] offsets=new int[32];
    private Bitmap layer,commandSurface;
    private Graphics commandGraphics;
    private Bitmap baseSource;
    private Size baseSize;
    private RectangleF? baseClip;
    private float[] baseMatrix;
    private bool recording,recordFailed;
    private int recorded;
    private readonly int[] clipPixels=new int[4],modes=new int[16],postOffsets=new int[16*32];
    private readonly float[] amounts=new float[16],region=new float[4];
    private static readonly float[] Identity={1,0,0,1,0,0};
    internal void InvalidateBase(){baseSource=null;}
    internal void RequireGdi(){if(recording)recordFailed=true;}

    private Graphics collecting;
    private SpriteStore owner;
    private PreviewTiming timing;
    private int width,height;
    // Stream very large emitters in painter order without retaining an entire frame of commands.
    private const int MaxCommands=8192;
    private bool Ready()
    {
        if(device!=IntPtr.Zero)return true;
        if(failed)return false;
        var name=new StringBuilder(128);
        try{device=Native.rp_create(name,name.Capacity);}
        catch(DllNotFoundException){}catch(EntryPointNotFoundException){}catch(BadImageFormatException){}
        adapter=name.ToString();failed=device==IntPtr.Zero;return !failed;
    }
    internal bool Add(Graphics g,Bitmap image,int ox,int oy,double x,double y,double sx,double sy,double angle,double alpha,Color tint)
    {
        if(g!=collecting||failed)return false;
        if(recording){
            if(recordFailed)return true;
            int tile=Texture(image);
            if(tile<0||recorded>=131072){recordFailed=true;return true;}
            if(recorded==quads.Length)Array.Resize(ref quads,recorded*2);
            quads[recorded++]=MakeQuad(tile,ox,oy,x,y,sx,sy,angle,alpha,tint);
            return true;
        }
        if(entries.Count==MaxCommands){Flush();if(failed)return false;}
        entries.Add(new Entry{image=image,x=x,y=y,sx=sx,sy=sy,angle=angle,ox=ox,oy=oy,alpha=alpha,tint=tint});
        return true;
    }
    internal void Batch(Graphics g,SpriteStore sprites,int w,int h,PreviewTiming timing,Action draw)
    {
        if(recording||failed||w>8192||h>8192){draw();return;}
        entries.Clear();collecting=g;sprites.Batch=this;
        owner=sprites;this.timing=timing;width=w;height=h;
        try{long tick=PreviewTiming.Now();draw();timing.Gather+=PreviewTiming.Since(tick);Flush();}
        finally{collecting=null;sprites.Batch=null;owner=null;this.timing=null;entries.Clear();}
    }
    private void Flush()
    {
        long tick=PreviewTiming.Now();
        if(entries.Count>=MinimumBatch&&Ready()&&DrawLayer(collecting,width,height)){
            timing.GpuSprites+=entries.Count;timing.Adapter=adapter;timing.Readbacks++;
        }else foreach(var e in entries)owner.DrawCpu(collecting,e.image,e.ox,e.oy,e.x,e.y,e.sx,e.sy,e.angle,e.alpha,e.tint);
        timing.Submit+=PreviewTiming.Since(tick);entries.Clear();
    }
    private int Texture(Bitmap image)
    {
        int tile;if(textures.TryGetValue(image,out tile))return tile;
        var rect=new Rectangle(0,0,image.Width,image.Height);
        if(rect.Width>2048||rect.Height>2048)return -1;
        var data=image.LockBits(rect,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        try{tile=Native.rp_upload(device,rect.Width,rect.Height,data.Scan0,data.Stride);}
        finally{image.UnlockBits(data);}
        textures[image]=tile;return tile;
    }
    private bool DrawLayer(Graphics g,int w,int h)
    {
        if(entries.Count>quads.Length)Array.Resize(ref quads,Math.Max(entries.Count,quads.Length*2));
        for(int i=0;i<entries.Count;i++){
            var e=entries[i];int tile=Texture(e.image);if(tile<0)return false;
            quads[i]=MakeQuad(tile,e.ox,e.oy,e.x,e.y,e.sx,e.sy,e.angle,e.alpha,e.tint);
        }
        if(layer==null||layer.Width!=w||layer.Height!=h){
            if(layer!=null)layer.Dispose();layer=new Bitmap(w,h,PixelFormat.Format32bppPArgb);
        }
        var data=layer.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
        int ok;
        try{using(var matrix=g.Transform)ok=Native.rp_draw(device,quads,entries.Count,matrix.Elements,w,h,data.Scan0,data.Stride);}
        finally{layer.UnlockBits(data);}
        if(ok==0){Disable();return false;}
        var saved=g.Save();try{g.ResetTransform();g.DrawImageUnscaled(layer,0,0);}finally{g.Restore(saved);}
        return true;
    }
    private static Quad MakeQuad(int tile,int ox,int oy,double x,double y,double sx,double sy,double angle,double alpha,Color tint)
    {
        uint opacity=(uint)Math.Round(Math.Max(0,Math.Min(1,alpha))*255);
        return new Quad{tile=tile,x=(float)x,y=(float)y,sx=(float)sx,sy=(float)sy,ox=ox,oy=oy,angle=(float)angle,
            rgba=(uint)(tint.R|(tint.G<<8)|(tint.B<<16))|(opacity<<24)};
    }
    private bool PrepareBase(Bitmap source,Size size,float[] matrix,RectangleF? clip,Action<Graphics> paint)
    {
        if(baseSource==source&&baseSize==size&&baseClip==clip&&baseMatrix.SequenceEqual(matrix))return true;
        using(var background=new Bitmap(size.Width,size.Height,PixelFormat.Format32bppArgb)){
            using(var g=Graphics.FromImage(background))paint(g);
            var data=background.LockBits(new Rectangle(Point.Empty,size),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            try{if(Native.rp_base(device,size.Width,size.Height,data.Scan0,data.Stride)==0)return false;}
            finally{background.UnlockBits(data);}
        }
        baseSource=source;baseSize=size;baseClip=clip;baseMatrix=matrix;
        return true;
    }

    // Negative means that an effect needs the existing history or a GDI drawing operation.
    private int PlanEffects(EditorState state,int frame)
    {
        int count=0;
        foreach(var t in state.tracks){
            if(!t.enabled||t.kind!="effect"||frame<t.start||frame>t.end)continue;
            if(count==modes.Length||t.effect!="rgb"&&t.effect!="blur")return -1;
            double amount=Effects.Unit(Motion.Value(t,"fxOpacity",frame));
            double strength=Math.Max(0,Motion.Value(t,"strength",frame)),angle=Motion.Value(t,"fxAngle",frame);
            int index=count*32,mode;
            if(t.effect=="rgb"){
                mode=1;postOffsets[index]=(int)Math.Round(Math.Cos(angle*Math.PI/180)*strength);
                postOffsets[index+1]=(int)Math.Round(-Math.Sin(angle*Math.PI/180)*strength);
                if(amount<=0||postOffsets[index]==0&&postOffsets[index+1]==0)continue;
            }else{
                mode=Math.Max(2,Math.Min(16,t.fxSamples));BlurOffsets(postOffsets,index,mode,strength,angle);
            }
            modes[count]=mode;amounts[count++]=(float)amount;
        }
        return count;
    }
    internal Bitmap Compose(SpriteStore sprites,PreviewCache cache,EditorState state,int frame,Bitmap source,Size size,
        Matrix transform,RectangleF? clip,Size roomSize,RectangleF? effectsFrame,bool allowEffects,Action<Graphics> paintBase,Action<Graphics> draw,out bool finishedEffects)
    {
        finishedEffects=false;
        if(size.Width>8192||size.Height>8192||!Ready())return null;
        float[] matrix=transform==null?Identity:transform.Elements;
        RectangleF rect=effectsFrame??new RectangleF(0,0,size.Width,size.Height);
        Size postSize=effectsFrame.HasValue?roomSize:size;
        // At extreme zoom GDI+ uses a different minification filter. Keep that path exact.
        bool supportedRegion=postSize.Width<=8192&&postSize.Height<=8192&&rect.Width>=postSize.Width*.5f&&rect.Height>=postSize.Height*.5f&&
            rect.Left>=0&&rect.Top>=0&&rect.Right<=size.Width&&rect.Bottom<=size.Height;
        int effects=allowEffects&&supportedRegion?PlanEffects(state,frame):-1;
        region[0]=rect.X;region[1]=rect.Y;region[2]=rect.Width;region[3]=rect.Height;
        long tick=PreviewTiming.Now();
        try{if(!PrepareBase(source,size,matrix,clip,paintBase)){Disable();return null;}}
        catch(EntryPointNotFoundException){Disable();return null;}
        cache.Timing.Backdrop+=PreviewTiming.Since(tick);
        if(commandSurface==null){commandSurface=new Bitmap(1,1);commandGraphics=Graphics.FromImage(commandSurface);}
        recording=true;recorded=0;recordFailed=false;collecting=commandGraphics;sprites.Batch=this;
        int before=cache.Timing.Sprites;double trails=cache.Timing.Trails,bodies=cache.Timing.Bodies;
        tick=PreviewTiming.Now();
        try{draw(commandGraphics);}
        finally{collecting=null;recording=false;sprites.Batch=null;}
        cache.Timing.Gather+=PreviewTiming.Since(tick);
        if(!recordFailed){
            cache.ThrowIfCancelled();tick=PreviewTiming.Now();
            RectangleF bounds=clip??new RectangleF(0,0,size.Width,size.Height);
            clipPixels[0]=Math.Max(0,(int)Math.Ceiling(bounds.Left-.5));clipPixels[1]=Math.Max(0,(int)Math.Ceiling(bounds.Top-.5));
            clipPixels[2]=Math.Max(clipPixels[0],Math.Min(size.Width,(int)Math.Ceiling(bounds.Right-.5)));
            clipPixels[3]=Math.Max(clipPixels[1],Math.Min(size.Height,(int)Math.Ceiling(bounds.Bottom-.5)));
            var image=cache.TakeFrame(size);
            bool ok=false;
            try{
                var data=image.LockBits(new Rectangle(Point.Empty,size),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
                try{ok=Native.rp_frame(device,quads,recorded,matrix,size.Width,size.Height,clipPixels,modes,amounts,postOffsets,
                    Math.Max(0,effects),postSize.Width,postSize.Height,region,data.Scan0,data.Stride)!=0;}
                finally{image.UnlockBits(data);}
            }finally{if(!ok)image.Dispose();}
            if(ok){
                double elapsed=PreviewTiming.Since(tick);cache.Timing.Submit+=elapsed;cache.Timing.Fused+=elapsed;cache.Timing.Readbacks++;
                cache.Timing.GpuSprites+=recorded;cache.Timing.GpuEffects+=Math.Max(0,effects);cache.Timing.Adapter=adapter;
                finishedEffects=effects>=0;
                if(finishedEffects)cache.TrimEffects(frame);
                return image;
            }
            Disable();
        }
        cache.Timing.Sprites=before;cache.Timing.Trails=trails;cache.Timing.Bodies=bodies;
        return null;
    }

    internal Bitmap Rgb(Bitmap source,int dx,int dy,double opacity,PreviewTiming timing)
    {
        offsets[0]=dx;offsets[1]=dy;return Effect(source,1,opacity,timing);
    }
    internal Bitmap Blur(Bitmap source,double distance,double angle,int samples,double opacity,PreviewTiming timing)
    {
        int count=Math.Max(2,Math.Min(16,samples));BlurOffsets(offsets,0,count,distance,angle);
        return Effect(source,count,opacity,timing);
    }
    private static void BlurOffsets(int[] values,int start,int count,double distance,double angle)
    {
        double rad=angle*Math.PI/180;
        for(int i=0;i<count;i++){
            double at=(i/(double)(count-1)-.5)*distance;
            values[start+i*2]=(int)Math.Round((float)(Math.Cos(rad)*at));values[start+i*2+1]=(int)Math.Round((float)(-Math.Sin(rad)*at));
        }
    }
    private Bitmap Effect(Bitmap source,int mode,double opacity,PreviewTiming timing)
    {
        int w=source.Width,h=source.Height;
        if(w>8192||h>8192||(long)w*h<65536||!Ready())return null;
        var rect=new Rectangle(0,0,w,h);var result=new Bitmap(w,h,PixelFormat.Format32bppArgb);
        try{
            var input=source.LockBits(rect,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            try{
                var output=result.LockBits(rect,ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
                try{
                    if(input.Stride>0&&output.Stride>0&&Native.rp_effect(device,w,h,input.Scan0,input.Stride,output.Scan0,output.Stride,mode,(float)opacity,offsets)!=0){
                        timing.Readbacks++;timing.GpuEffects++;timing.Adapter=adapter;return result;
                    }
                }finally{result.UnlockBits(output);}
            }finally{source.UnlockBits(input);}
        }catch{result.Dispose();throw;}
        result.Dispose();Disable();return null;
    }
    private void Disable(){Dispose();failed=true;}
    public void Dispose()
    {
        if(device!=IntPtr.Zero){Native.rp_release(device);device=IntPtr.Zero;}
        if(commandGraphics!=null){commandGraphics.Dispose();commandGraphics=null;commandSurface.Dispose();commandSurface=null;}
        baseSource=null;if(layer!=null){layer.Dispose();layer=null;}textures.Clear();
    }
}
