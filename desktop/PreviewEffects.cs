using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;

internal sealed class PreviewCache : IDisposable
{
    // Adjacent frames reuse the exact same historical formation samples for their trails.
    private sealed class SampleRing {public FormationSample[] slots;}
    internal sealed class FormationSample {public int frame;public Keyframe pose;public PointF[] points;public int[] images;public double angle;}
    private readonly Dictionary<Track,SampleRing> formations=new Dictionary<Track,SampleRing>();
    private readonly List<Track> inactiveFormations=new List<Track>();
    internal void TrimFormations(int frame)
    {
        foreach(var t in formations.Keys)if(!BulletSystem.Active(t,frame))inactiveFormations.Add(t);
        foreach(var t in inactiveFormations)formations.Remove(t);
        inactiveFormations.Clear();
    }
    internal FormationSample Formation(Track t,int frame)
    {
        SampleRing ring;if(!formations.TryGetValue(t,out ring)){
            ring=new SampleRing{slots=new FormationSample[Trails.Mode(t)=="off"?1:Trails.Life(t)+1]};formations.Add(t,ring);
        }
        int index=(frame%ring.slots.Length+ring.slots.Length)%ring.slots.Length;
        var sample=ring.slots[index];if(sample!=null&&sample.frame==frame)return sample;
        var pose=Motion.At(t,frame);var points=Motion.RodPoints(t,frame);
        var images=sample!=null&&sample.images.Length==points.Length?sample.images:new int[points.Length];
        Rainbow.FormationImages(t,frame,points,pose,images);
        ring.slots[index]=sample??new FormationSample();
        var result=ring.slots[index];result.frame=frame;result.pose=pose;result.points=points;result.images=images;
        result.angle=Motion.Value(t,"spriteAngle",frame)+(t.rotateSprite?pose.angle:0);
        return result;
    }
    // The UI returns the previously displayed frame only after the worker has completed
    // and the front buffer has been swapped. The worker always overwrites every pixel.
    private Bitmap spareFrame;
    internal Bitmap TakeFrame(Size size)
    {
        var frame=spareFrame;spareFrame=null;
        if(frame!=null&&frame.Size==size)return frame;
        if(frame!=null)frame.Dispose();return new Bitmap(size.Width,size.Height,PixelFormat.Format32bppPArgb);
    }
    internal void RecycleFrame(Bitmap frame)
    {
        if(spareFrame!=null)spareFrame.Dispose();spareFrame=null;
        if(frame!=null&&UseGpu&&frame.PixelFormat==PixelFormat.Format32bppPArgb)spareFrame=frame;
        else if(frame!=null)frame.Dispose();
    }
    public bool UseGpu;
    private PreviewGpu gpu;
    public PreviewGpu Gpu { get {return UseGpu?(gpu??(gpu=new PreviewGpu())):null;} }
    public PreviewTiming Timing=new PreviewTiming();
    private RoomData artRoom;
    private RoomPresentation art;
    public RoomPresentation RoomArt(RoomData room)
    {if(artRoom!=room){artRoom=room;art=new RoomPresentation(room);}return art;}
    public Bitmap roomLayer;public string roomKey;
    public BulletWorld bulletWorld;public EditorState expandedState;
    private EditorState inputScene;private RoomData inputRoom;
    public Func<bool> Cancelled;
    public void ThrowIfCancelled(){if(Cancelled!=null&&Cancelled())throw new OperationCanceledException();}
    public void ClearEffects(){foreach(var entry in history.Values)entry.Dispose();history.Clear();}
    public void UseScene(EditorState scene,RoomData room)
    {
        if(ReferenceEquals(room,inputRoom)&&(ReferenceEquals(scene,inputScene)||ReferenceEquals(scene,expandedState)))return;
        if(gpu!=null)gpu.InvalidateBase();formations.Clear();
        var expanded=BulletSystem.Expand(scene);int first=PreviewRevision.SimulationFrom(expandedState,expanded,inputRoom,room);
        if(bulletWorld!=null){if(first==0)bulletWorld=null;else if(first>0)bulletWorld.Revise(expanded,first);}
        ClearEffects();expandedState=expanded;inputScene=scene;inputRoom=room;
    }
    private NativeFilmReader film;private string filmKey;private bool filmActive;
    public NativeFilmInfo FilmInfo {get{return !filmActive||film==null?null:film.Info;}}
    public HashSet<string> FilmNativeIds {get{return !filmActive||film==null?null:film.NativeIds;}}
    public Bitmap FilmFrame(string path,int frame,int fps)
    {
        if(String.IsNullOrEmpty(path)||!System.IO.File.Exists(path)){
            if(filmActive){filmActive=false;bulletWorld=null;ClearEffects();}
            return null;
        }
        var file=new System.IO.FileInfo(path);
        string key=path+":"+file.LastWriteTimeUtc.Ticks+":"+file.Length;
        if(key!=filmKey){if(film!=null)film.Dispose();film=null;filmKey=null;film=new NativeFilmReader(path);filmKey=key;bulletWorld=null;ClearEffects();}
        if(!filmActive){filmActive=true;bulletWorld=null;ClearEffects();}
        return film.Frame((int)Math.Round(frame*film.Info.fps/(double)Math.Max(1,fps)));
    }
    public readonly Dictionary<string,History> history=new Dictionary<string,History>();
    private readonly List<string> expired=new List<string>();
    public History EffectHistory(Track clip)
    {
        History entry;
        if(!history.TryGetValue(clip.id,out entry)){
            entry=new History{start=clip.start,end=clip.end};
            history.Add(clip.id,entry);
        }
        return entry;
    }
    public void TrimEffects(int frame)
    {
        foreach(var pair in history)
            if(frame<pair.Value.start||frame>pair.Value.end)expired.Add(pair.Key);
        foreach(string id in expired){history[id].Dispose();history.Remove(id);}
        expired.Clear();
    }
    internal sealed class History : IDisposable
    {
        public Bitmap image,previous;
        public int start,end,frame=-1;
        public void Dispose()
        {
            if(image!=null)image.Dispose();
            if(previous!=null)previous.Dispose();
            image=null;previous=null;frame=-1;
        }
    }
    public void ClearHistory(){formations.Clear();ClearEffects();bulletWorld=null;expandedState=null;inputScene=null;inputRoom=null;}
    public void Clear(){if(spareFrame!=null){spareFrame.Dispose();spareFrame=null;}if(gpu!=null){gpu.Dispose();gpu=null;}if(roomLayer!=null)roomLayer.Dispose();roomLayer=null;roomKey=null;if(film!=null)film.Dispose();film=null;filmKey=null;filmActive=false;ClearHistory();}
    public void Dispose(){Clear();}
}

internal static partial class Effects
{
    public static readonly string[] Values={"flash","shake","color","fade-in","fade-out","rgb","motion-blur","blur","invert","vignette","rgb-impact","glitch"};
    public static readonly string[] Names={"闪屏","震屏","黑场 / 纯色遮罩","淡入（从遮罩到画面）","淡出（从画面到遮罩）","全画面 RGB 分离","动态模糊 / 帧混合","方向模糊","反色","暗角","RGB 冲击（组合）","故障切换"};
    public static string Name(string value){int i=Array.IndexOf(Values,value);return i<0?value:Names[i];}
    public static double Unit(double value){return Math.Max(0,Math.Min(1,value));}
    private static void DrawAlpha(Graphics g,Image source,float x,float y,double alpha)
    {
        using(var attrs=new ImageAttributes()){var matrix=new ColorMatrix();matrix.Matrix33=(float)Unit(alpha);attrs.SetColorMatrix(matrix);g.DrawImage(source,new Rectangle((int)Math.Round(x),(int)Math.Round(y),source.Width,source.Height),0,0,source.Width,source.Height,GraphicsUnit.Pixel,attrs);}
    }
    private static Bitmap Transform(Bitmap source,ColorMatrix matrix)
    {var result=new Bitmap(source.Width,source.Height,PixelFormat.Format32bppArgb);using(var g=Graphics.FromImage(result))using(var a=new ImageAttributes()){a.SetColorMatrix(matrix);g.DrawImage(source,new Rectangle(0,0,result.Width,result.Height),0,0,source.Width,source.Height,GraphicsUnit.Pixel,a);}return result;}
    private static Bitmap Shift(Bitmap source,int dx,int dy)
    {var result=new Bitmap(source.Width,source.Height);using(var g=Graphics.FromImage(result)){g.Clear(Color.Black);g.DrawImageUnscaled(source,dx,dy);}return result;}
    [ThreadStatic]
    private static byte[] s_rgbOriginal;
    [ThreadStatic]
    private static byte[] s_rgbPixels;
    private static Bitmap Rgb(Bitmap source,int dx,int dy,double opacity,PreviewCache cache=null)
    {
        if(opacity<=0||(dx==0&&dy==0))return (Bitmap)source.Clone();
        if(cache!=null&&cache.Gpu!=null){var accelerated=cache.Gpu.Rgb(source,dx,dy,opacity,cache.Timing);if(accelerated!=null)return accelerated;}
        int width=source.Width,height=source.Height;var bounds=new Rectangle(0,0,width,height);var data=source.LockBits(bounds,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);int stride=data.Stride;
        int needed=stride*height;
        if(s_rgbOriginal==null||s_rgbOriginal.Length<needed){s_rgbOriginal=new byte[needed];s_rgbPixels=new byte[needed];}
        var original=s_rgbOriginal;var pixels=s_rgbPixels;
        try{Marshal.Copy(data.Scan0,original,0,needed);}finally{source.UnlockBits(data);}
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {int i=y*stride+x*4;int rx=x-dx,ry=y-dy,bx=x+dx,by=y+dy;
            int red=rx>=0&&rx<width&&ry>=0&&ry<height?original[ry*stride+rx*4+2]:0,blue=bx>=0&&bx<width&&by>=0&&by<height?original[by*stride+bx*4]:0;
            pixels[i]=(byte)(original[i]*(1-opacity)+blue*opacity);pixels[i+1]=original[i+1];pixels[i+2]=(byte)(original[i+2]*(1-opacity)+red*opacity);pixels[i+3]=255;}
        var result=new Bitmap(width,height,PixelFormat.Format32bppArgb);data=result.LockBits(bounds,ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);try{Marshal.Copy(pixels,0,data.Scan0,needed);}finally{result.UnlockBits(data);}return result;
    }
    internal static Bitmap Blur(Bitmap source,double distance,double angle,int samples,double opacity,bool mirror=false)
    {
        var result=new Bitmap(source.Width,source.Height);double rad=angle*Math.PI/180;int count=Math.Max(2,Math.Min(16,samples));
        using(var g=Graphics.FromImage(result))
        {g.Clear(Color.Black);for(int i=0;i<count;i++){double offset=(i/(double)(count-1)-.5)*distance;if(mirror)DrawMirrorAlpha(g,source,(float)(Math.Cos(rad)*offset),(float)(-Math.Sin(rad)*offset),1.0/(i+1));else DrawAlpha(g,source,(float)(Math.Cos(rad)*offset),(float)(-Math.Sin(rad)*offset),1.0/(i+1));}
            if(opacity<1)DrawAlpha(g,source,0,0,1-opacity);}
        return result;
    }
    // Owns and returns the current bitmap. Geometry is reconstructed independently of edit/playback state.
    public static Bitmap Apply(Bitmap image,EditorState state,int frame,PreviewCache cache)
    {
        long phase=PreviewTiming.Now();cache.TrimEffects(frame);
        foreach(var t in state.tracks.Where(t=>t.enabled&&t.kind=="effect"&&frame>=t.start&&frame<=t.end))
        {
            double u=(frame-TimelineLayout.ClockStart(t))/(double)Math.Max(1,TimelineLayout.ClockEnd(t)-TimelineLayout.ClockStart(t));u=Motion.Ease(t.easing,u,t.easePower,t.easePeriod);
            double strength=Math.Max(0,Motion.Value(t,"strength",frame)),opacity=Unit(Motion.Value(t,"fxOpacity",frame)),angle=Motion.Value(t,"fxAngle",frame);Bitmap next=null;
            if(t.effect=="rgb-impact"||t.effect=="glitch")next=Style(image,t,state,frame,cache,strength,opacity,angle);
            else if(t.effect=="shake")
            {double age=frame-TimelineLayout.ClockStart(t);next=Shift(image,(int)Math.Round(Math.Sin(age*t.fxFrequency*2.17)*strength*opacity),(int)Math.Round(Math.Cos(age*t.fxFrequency*1.61)*strength*opacity));}
            else if(t.effect=="rgb")next=Rgb(image,(int)Math.Round(Math.Cos(angle*Math.PI/180)*strength),(int)Math.Round(-Math.Sin(angle*Math.PI/180)*strength),opacity,cache);
            else if(t.effect=="blur"||t.effect=="motion-blur")
            {
                next=cache.Gpu==null?null:cache.Gpu.Blur(image,strength,angle,t.fxSamples,opacity,cache.Timing);
                if(next==null)next=Blur(image,strength,angle,t.fxSamples,opacity);
                if(t.effect=="motion-blur")
                {
                    var history=cache.EffectHistory(t);
                    if(history.image!=null){
                        if(history.image.Size!=next.Size||frame<history.frame||frame-history.frame>Math.Max(1,state.fps/5))history.Dispose();
                        else if(frame>history.frame){
                            var spare=history.previous;history.previous=history.image;history.image=spare;
                        }
                    }
                    if(history.previous!=null)using(var g=Graphics.FromImage(next))
                        DrawAlpha(g,history.previous,0,0,Unit(Motion.Value(t,"fxRetention",frame))*.95*opacity);
                    if(history.image==null)history.image=(Bitmap)next.Clone();
                    else using(var g=Graphics.FromImage(history.image)){
                        g.CompositingMode=System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                        g.DrawImageUnscaled(next,0,0);
                    }
                    history.frame=frame;
                }
            }
            else if(t.effect=="invert")
            {
                var m=new ColorMatrix();float a=(float)opacity;
                m.Matrix00=m.Matrix11=m.Matrix22=1-2*a;m.Matrix40=m.Matrix41=m.Matrix42=a;
                next=Transform(image,m);
            }
            else if(t.effect=="vignette")
            {using(var g=Graphics.FromImage(image)){int bands=24;float extent=Math.Min(image.Width,image.Height)*.4f;for(int i=bands-1;i>=0;i--){float inset=extent*i/bands;int a=(int)(255*opacity*Unit(strength/100)*Math.Pow(1-i/(double)bands,2));using(var pen=new Pen(Color.FromArgb(a,Color.Black),extent/bands+1))g.DrawRectangle(pen,inset,inset,image.Width-2*inset,image.Height-2*inset);}}}
            else if(t.effect=="color"||t.effect=="fade-in"||t.effect=="fade-out"||t.effect=="flash")
            {
                double coverage=t.effect=="fade-in"?1-u:t.effect=="fade-out"?u:t.effect=="flash"?1-u:1;
                using(var g=Graphics.FromImage(image))using(var brush=new SolidBrush(Color.FromArgb((int)(255*Unit(coverage*opacity)),Motion.ColorOf(t.color))))g.FillRectangle(brush,0,0,image.Width,image.Height);
            }
            if(next!=null){image.Dispose();image=next;}
        }
        cache.Timing.Effects+=PreviewTiming.Since(phase);return image;
    }
}
