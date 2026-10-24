using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

internal static partial class Effects
{
    private static void DrawMirrorAlpha(Graphics g,Bitmap source,float x,float y,double alpha)
    {using(var attributes=new ImageAttributes()){var matrix=new ColorMatrix();matrix.Matrix33=(float)Unit(alpha);attributes.SetColorMatrix(matrix);using(var brush=new TextureBrush(source,new Rectangle(0,0,source.Width,source.Height),attributes)){brush.WrapMode=WrapMode.TileFlipXY;brush.TranslateTransform(x,y);g.FillRectangle(brush,0,0,source.Width,source.Height);}}}
    internal static Bitmap CameraFrame(Bitmap source,CameraPose pose)
    {
        int w=source.Width,h=source.Height;var result=new Bitmap(w,h);
        using(var g=Graphics.FromImage(result))using(var attributes=new ImageAttributes())using(var matrix=pose.WorldToView(w,h)){
            g.Clear(Color.Black);g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;g.Transform=matrix;attributes.SetWrapMode(WrapMode.TileFlipXY);
            var corners=pose.Corners(w,h);double left=Double.MaxValue,right=Double.MinValue,top=Double.MaxValue,bottom=Double.MinValue;foreach(var p in corners){left=Math.Min(left,p.X);right=Math.Max(right,p.X);top=Math.Min(top,p.Y);bottom=Math.Max(bottom,p.Y);}
            int x0=pose.mirror?(int)Math.Floor(left/w):0,xn=pose.mirror?(int)Math.Floor(right/w):0,y0=pose.mirror?(int)Math.Floor(top/h):0,yn=pose.mirror?(int)Math.Floor(bottom/h):0;
            for(int y=y0;y<=yn;y++)for(int x=x0;x<=xn;x++){
                var saved=g.Save();bool flipX=(x&1)!=0,flipY=(y&1)!=0;g.TranslateTransform((x+(flipX?1:0))*w,(y+(flipY?1:0))*h);g.ScaleTransform(flipX?-1:1,flipY?-1:1);g.DrawImage(source,new Rectangle(0,0,w,h),0,0,w,h,GraphicsUnit.Pixel,attributes);g.Restore(saved);
            }
        }
        if(pose.blur>0){var blurred=Blur(result,pose.blur,pose.blurAngle,9,1,true);result.Dispose();result=blurred;}return result;
    }
    private static double Noise(double seed){double n=Math.Sin((seed+1)*12.9898)*43758.5453;return n-Math.Floor(n);}
    private static Bitmap Style(Bitmap source,Track t,EditorState state,int frame,PreviewCache cache,double strength,double opacity,double angle)
    {
        if(opacity<=0)return (Bitmap)source.Clone();int w=source.Width,h=source.Height;
        if(t.effect=="rgb-impact"){
            double zoom=1+(Math.Max(1,Motion.Value(t,"fxZoom",frame))-1)*opacity;
            var pose=new CameraPose{x=w/2.0,y=h/2.0,zoom=zoom,mirror=true};var image=CameraFrame(source,pose);
            double retention=Unit(Motion.Value(t,"fxRetention",frame))*opacity;
            if(retention>0){pose.zoom=1+(zoom-1)*1.75;using(var echo=CameraFrame(source,pose))using(var g=Graphics.FromImage(image))DrawAlpha(g,echo,(float)(strength*.55*opacity),0,retention);}
            var split=Rgb(image,(int)Math.Round(Math.Cos(angle*Math.PI/180)*strength*opacity),(int)Math.Round(-Math.Sin(angle*Math.PI/180)*strength*opacity),1);image.Dispose();
            double flash=Unit(Motion.Value(t,"fxFlash",frame))*opacity;if(flash>0)using(var g=Graphics.FromImage(split))using(var brush=new SolidBrush(Color.FromArgb((int)(flash*255),Motion.ColorOf(t.color))))g.FillRectangle(brush,0,0,w,h);
            return split;
        }
        PreviewCache.History history;if(!cache.history.TryGetValue(t.id,out history)){history=new PreviewCache.History();cache.history[t.id]=history;}
        int hold=Math.Max(1,Math.Min(12,t.fxStutter)),bucket=(frame-t.start)/hold;
        if(history.image==null||history.image.Size!=source.Size||frame<history.frame||frame-history.frame>hold||(history.frame-t.start)/hold!=bucket){history.Dispose();history.image=(Bitmap)source.Clone();history.frame=frame;}
        var result=Rgb(history.image,(int)Math.Round(Math.Cos(angle*Math.PI/180)*8*opacity),(int)Math.Round(-Math.Sin(angle*Math.PI/180)*8*opacity),1);
        using(var bands=(Bitmap)result.Clone())using(var g=Graphics.FromImage(result)){
            g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
            int count=Math.Max(1,Math.Min(32,t.fxSlices));for(int i=0;i<count;i++){
                double seed=bucket*131+i*37+t.start;int y=(int)(Noise(seed)*Math.Max(1,h-24)),height=Math.Min(h-y,4+(int)(Noise(seed+9)*20)),dx=(int)Math.Round((Noise(seed+17)*2-1)*strength*opacity);
                foreach(int x in new[]{dx,dx-w,dx+w})g.DrawImage(bands,new Rectangle(x,y,w,height),0,y,w,height,GraphicsUnit.Pixel);
            }
            using(var pen=new Pen(Color.FromArgb((int)(.18*opacity*255),Color.Black)))for(int y=0;y<h;y+=4)g.DrawLine(pen,0,y,w,y);
        }return result;
    }
}
