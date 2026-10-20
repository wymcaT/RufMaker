using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

internal sealed class CameraPose
{
    public double x,y,zoom=1,angle;
    public Matrix WorldToView(int width,int height)
    {
        double a=angle*Math.PI/180,c=Math.Cos(a)*zoom,s=Math.Sin(a)*zoom;
        return new Matrix((float)c,(float)s,(float)-s,(float)c,(float)(width/2.0-c*x+s*y),(float)(height/2.0-s*x-c*y));
    }
    public PointF[] Corners(int width,int height)
    {var p=new[]{new PointF(0,0),new PointF(width,0),new PointF(width,height),new PointF(0,height)};using(var m=WorldToView(width,height)){m.Invert();m.TransformPoints(p);}return p;}
}
internal static class Camera
{
    // Last active camera in timeline order wins. A gap returns to the room view.
    public static Track Active(EditorState state,int frame)
    {return state.tracks.LastOrDefault(t=>t.enabled&&t.kind=="camera"&&frame>=t.start&&frame<=t.end);}
    public static CameraPose At(Track t,int frame,RoomData room)
    {if(t==null)return new CameraPose{x=room.width/2.0,y=room.height/2.0};var p=Motion.At(t,frame);return new CameraPose{x=p.x,y=p.y,angle=p.angle,zoom=Math.Max(.05,Math.Min(20,Motion.Value(t,"cameraZoom",frame)))};}
}
internal sealed class NativeViewPose
{
    public bool enabled;public double x,y,w,h,angle,portX,portY,portW,portH,outputW,outputH;
    public Matrix WorldToRoom(int width,int height)
    {
        if(!enabled||w<=0||h<=0||outputW<=0||outputH<=0||portW<=0||portH<=0)return new Matrix();
        double a=angle*Math.PI/180,c=Math.Cos(a),s=Math.Sin(a),sx=portW/outputW*width/w,sy=portH/outputH*height/h,cx=x+w/2,cy=y+h/2;
        return new Matrix((float)(c*sx),(float)(s*sy),(float)(-s*sx),(float)(c*sy),(float)((portX+portW/2)/outputW*width-c*sx*cx+s*sx*cy),(float)((portY+portH/2)/outputH*height-s*sy*cx-c*sy*cy));
    }
    public RectangleF Viewport(int width,int height){return !enabled||outputW<=0||outputH<=0?new RectangleF(0,0,width,height):new RectangleF((float)(portX/outputW*width),(float)(portY/outputH*height),(float)(portW/outputW*width),(float)(portH/outputH*height));}
}
