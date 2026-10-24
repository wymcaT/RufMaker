using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

internal sealed partial class StageCanvas
{
    private int curveHandle=-1;
    private List<CurvePoint> CurvePoints(Track t){return t.curvePoints.Count==4?t.curvePoints:new List<CurvePoint>{new CurvePoint{x=-120},new CurvePoint{x=-60,y=-120},new CurvePoint{x=60,y=120},new CurvePoint{x=120}};}
    private PointF[] CurveHandles(Track t)
    {
        var origin=Motion.At(t,Frame);double rotation=Motion.Value(t,"emitRotation",Frame),scale=1;if(t.parent!=null){var p=Motion.At(t.parent,Frame);rotation+=p.angle;scale=p.scale;}double r=rotation*Math.PI/180;
        return CurvePoints(t).Select(p=>new PointF((float)(origin.x+scale*(p.x*Math.Cos(r)+p.y*Math.Sin(r))),(float)(origin.y+scale*(-p.x*Math.Sin(r)+p.y*Math.Cos(r))))).ToArray();
    }
    private void PaintCurve(Graphics g)
    {
        var t=SelectedTrack;if(t==null||t.kind!="emitter"||t.emissionShape!="bezier")return;var points=CurveHandles(t);using(var pen=new Pen(Color.FromArgb(150,Theme.Accent),1)){pen.DashStyle=System.Drawing.Drawing2D.DashStyle.Dash;g.DrawLines(pen,points);}using(var pen=new Pen(Color.Cyan,1.5f))g.DrawBezier(pen,points[0],points[1],points[2],points[3]);
        for(int i=0;i<points.Length;i++){var p=points[i];g.FillRectangle(Brushes.White,p.X-5,p.Y-5,10,10);using(var pen=new Pen(Theme.Accent,1))g.DrawRectangle(pen,p.X-6,p.Y-6,12,12);using(var brush=new SolidBrush(Theme.Text))g.DrawString("P"+i,Theme.Small,brush,p.X+9,p.Y-13);}
    }
    private bool BeginCurve(PointF point)
    {
        var t=SelectedTrack;if(t==null||t.kind!="emitter"||t.emissionShape!="bezier"||SelectedTracks.Count>1)return false;var points=CurveHandles(t);for(int i=0;i<points.Length;i++)if(Near(points[i],point,11)){if(BeginEdit!=null)BeginEdit();if(t.curvePoints.Count!=4)t.curvePoints=CurvePoints(t);curveHandle=i;Capture=true;return true;}return false;
    }
    private void DragCurve(PointF point)
    {
        var t=SelectedTrack;if(t==null||curveHandle<0)return;var origin=Motion.At(t,Frame);double x=point.X-origin.x,y=point.Y-origin.y,rotation=Motion.Value(t,"emitRotation",Frame),scale=1;if(t.parent!=null){var parent=Motion.At(t.parent,Frame);rotation+=parent.angle;scale=parent.scale;}double r=rotation*Math.PI/180;scale=Math.Abs(scale)<.0001?.0001:scale;double px=(x*Math.Cos(r)-y*Math.Sin(r))/scale,py=(x*Math.Sin(r)+y*Math.Cos(r))/scale;if((ModifierKeys&Keys.Shift)!=0){px=Math.Round(px/16)*16;py=Math.Round(py/16)*16;}t.curvePoints[curveHandle].x=px;t.curvePoints[curveHandle].y=py;SceneChanged();
    }
    private bool HasSelectedParent(Track t){var parent=t.parent;while(parent!=null){if(SelectedTracks.Contains(parent))return true;parent=parent.parent;}return false;}
    private PointF ParentLocalPoint(Track t,PointF point)
    {if(t.parent==null)return point;var p=Motion.At(t.parent,Frame);double x=point.X-p.x,y=point.Y-p.y,r=p.angle*Math.PI/180,s=Math.Abs(p.scale)<.0001?.0001:p.scale;return new PointF((float)((x*Math.Cos(r)-y*Math.Sin(r))/s),(float)((x*Math.Sin(r)+y*Math.Cos(r))/s));}
}
