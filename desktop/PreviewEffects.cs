using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;

internal sealed class PreviewCache : IDisposable
{
    public Bitmap roomLayer;public string roomKey;
    public BulletWorld bulletWorld;public EditorState expandedState;
    private NativeFilmReader film;private string filmKey;
    public NativeFilmInfo FilmInfo {get{return film==null?null:film.Info;}}
    public HashSet<string> FilmNativeIds {get{return film==null?null:film.NativeIds;}}
    public Bitmap FilmFrame(string path,int frame,int fps)
    {
        if(String.IsNullOrEmpty(path))return null;var file=new System.IO.FileInfo(path);if(!file.Exists)return null;
        string key=path+":"+file.LastWriteTimeUtc.Ticks+":"+file.Length;
        if(key!=filmKey){if(film!=null)film.Dispose();film=null;filmKey=null;film=new NativeFilmReader(path);filmKey=key;}
        return film.Frame((int)Math.Round(frame*film.Info.fps/(double)Math.Max(1,fps)));
    }
    public readonly Dictionary<string,History> history=new Dictionary<string,History>();
    internal sealed class History : IDisposable {public Bitmap image;public int frame;public void Dispose(){if(image!=null)image.Dispose();image=null;}}
    public void ClearHistory(){foreach(var entry in history.Values)entry.Dispose();history.Clear();bulletWorld=null;expandedState=null;}
    public void Clear(){if(roomLayer!=null)roomLayer.Dispose();roomLayer=null;roomKey=null;if(film!=null)film.Dispose();film=null;filmKey=null;ClearHistory();}
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
    private static Bitmap Rgb(Bitmap source,int dx,int dy,double opacity)
    {
        int width=source.Width,height=source.Height;var bounds=new Rectangle(0,0,width,height);var data=source.LockBits(bounds,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);int stride=data.Stride;
        var original=new byte[stride*height];try{Marshal.Copy(data.Scan0,original,0,original.Length);}finally{source.UnlockBits(data);}
        var pixels=new byte[original.Length];
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {int i=y*stride+x*4;int rx=x-dx,ry=y-dy,bx=x+dx,by=y+dy;
            int red=rx>=0&&rx<width&&ry>=0&&ry<height?original[ry*stride+rx*4+2]:0,blue=bx>=0&&bx<width&&by>=0&&by<height?original[by*stride+bx*4]:0;
            pixels[i]=(byte)(original[i]*(1-opacity)+blue*opacity);pixels[i+1]=original[i+1];pixels[i+2]=(byte)(original[i+2]*(1-opacity)+red*opacity);pixels[i+3]=255;}
        var result=new Bitmap(width,height,PixelFormat.Format32bppArgb);data=result.LockBits(bounds,ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);try{Marshal.Copy(pixels,0,data.Scan0,pixels.Length);}finally{result.UnlockBits(data);}return result;
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
        foreach(var t in state.tracks.Where(t=>t.enabled&&t.kind=="effect"&&frame>=t.start&&frame<=t.end))
        {
            double u=(frame-t.start)/(double)Math.Max(1,t.end-t.start);u=Motion.Ease(t.easing,u,t.easePower,t.easePeriod);
            double strength=Math.Max(0,Motion.Value(t,"strength",frame)),opacity=Unit(Motion.Value(t,"fxOpacity",frame)),angle=Motion.Value(t,"fxAngle",frame);Bitmap next=null;
            if(t.effect=="rgb-impact"||t.effect=="glitch")next=Style(image,t,state,frame,cache,strength,opacity,angle);
            else if(t.effect=="shake")
            {double age=frame-t.start;next=Shift(image,(int)Math.Round(Math.Sin(age*t.fxFrequency*2.17)*strength*opacity),(int)Math.Round(Math.Cos(age*t.fxFrequency*1.61)*strength*opacity));}
            else if(t.effect=="rgb")next=Rgb(image,(int)Math.Round(Math.Cos(angle*Math.PI/180)*strength),(int)Math.Round(-Math.Sin(angle*Math.PI/180)*strength),opacity);
            else if(t.effect=="blur"||t.effect=="motion-blur")
            {
                next=Blur(image,strength,angle,t.fxSamples,opacity);
                if(t.effect=="motion-blur")
                {
                    PreviewCache.History history;if(!cache.history.TryGetValue(t.id,out history)){history=new PreviewCache.History();cache.history[t.id]=history;}
                    if(history.image!=null&&frame>history.frame&&frame-history.frame<=Math.Max(1,state.fps/5)&&history.image.Size==next.Size)using(var g=Graphics.FromImage(next))DrawAlpha(g,history.image,0,0,Unit(Motion.Value(t,"fxRetention",frame))*.95*opacity);
                    if(history.frame!=frame||history.image==null){history.Dispose();history.image=(Bitmap)next.Clone();history.frame=frame;}
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
        return image;
    }
}
