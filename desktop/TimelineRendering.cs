using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

internal sealed partial class TimelineCanvas
{
    private Bitmap timelineLayer;private bool layerDirty=true,cursorInvalidation;private double? scrubPosition;private int? cursorSnapGuide;
    internal int LayerBuilds {get;private set;}internal int CursorPaints {get;private set;}internal int PresentRequests {get;private set;}
    internal float PlayheadX {get{return X(dragMode=="seek"&&scrubPosition.HasValue?scrubPosition.Value:Frame);}}
    public void ShowSnapGuide(int? frame)
    {if(frame==cursorSnapGuide)return;int? before=cursorSnapGuide;cursorSnapGuide=frame;if(before.HasValue)InvalidateCursor(X(before.Value),X(before.Value));if(frame.HasValue)InvalidateCursor(X(frame.Value),X(frame.Value));}
    private void InvalidateCursor(float oldX,float newX)
    {
        int left=Math.Max(Names,(int)Math.Floor(Math.Min(oldX,newX))-9),right=Math.Min(Width-vertical.Width,(int)Math.Ceiling(Math.Max(oldX,newX))+10);
        if(right<=left)return;cursorInvalidation=true;try{Invalidate(new Rectangle(left,0,right-left,Math.Max(1,Height-horizontal.Height)));}finally{cursorInvalidation=false;}
    }
    public void PresentPlayhead(){if(IsHandleCreated&&!IsDisposed){PresentRequests++;Update();}}
    private void MoveScrub(int x,bool begin)
    {
        float old=PlayheadX;scrubPosition=Math.Max(0,Math.Min(State.duration,(x-Names+horizontal.Value)/(double)PixelsPerSecond*State.fps));
        if(begin)scrub.Begin(RawFrame(x),(ReadModifiers()&Keys.Alt)!=0);else scrub.Move(RawFrame(x),(ReadModifiers()&Keys.Alt)!=0);
        InvalidateCursor(old,PlayheadX);PresentPlayhead();
    }
    protected override void OnInvalidated(InvalidateEventArgs e){if(!cursorInvalidation)layerDirty=true;base.OnInvalidated(e);}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);int width=Width-vertical.Width,height=Height-horizontal.Height;
        if(timelineLayer==null||timelineLayer.Width!=Math.Max(1,Width)||timelineLayer.Height!=Math.Max(1,Height)){ReleaseTimelineLayer();timelineLayer=new Bitmap(Math.Max(1,Width),Math.Max(1,Height),PixelFormat.Format32bppPArgb);layerDirty=true;}
        if(layerDirty){using(var g=Graphics.FromImage(timelineLayer))DrawTimelineLayer(g,width,height);layerDirty=false;LayerBuilds++;}
        e.Graphics.DrawImageUnscaled(timelineLayer,0,0);if(State==null||width<=Names||height<=0)return;
        var saved=e.Graphics.Save();e.Graphics.SetClip(new Rectangle(Names,0,width-Names,height),CombineMode.Intersect);
        if(clipSnapGuide.HasValue)DrawGuide(e.Graphics,X(clipSnapGuide.Value),Header,height);
        if(cursorSnapGuide.HasValue)DrawGuide(e.Graphics,X(cursorSnapGuide.Value),0,height);
        float playhead=PlayheadX;if(playhead>=Names&&playhead<width){using(var pen=new Pen(Theme.Accent,1.5f))e.Graphics.DrawLine(pen,playhead,8,playhead,height);using(var brush=new SolidBrush(Theme.Accent))e.Graphics.FillPolygon(brush,new[]{new PointF(playhead-6,0),new PointF(playhead+6,0),new PointF(playhead+6,9),new PointF(playhead,15),new PointF(playhead-6,9)});}
        e.Graphics.Restore(saved);CursorPaints++;
    }
    private static void DrawGuide(Graphics g,float x,int top,int bottom)
    {using(var pen=new Pen(Color.FromArgb(245,203,104),1)){pen.DashStyle=DashStyle.Dash;g.DrawLine(pen,x,top,x,bottom);}}
    private void ReleaseTimelineLayer(){if(timelineLayer!=null)timelineLayer.Dispose();timelineLayer=null;}
}
