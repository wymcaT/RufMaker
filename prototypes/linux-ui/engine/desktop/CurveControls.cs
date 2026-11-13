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
        PaintGenerationRegions(g);
        var t=SelectedTrack;if(t==null||t.kind!="emitter"||t.emissionShape!="bezier")return;var points=CurveHandles(t);using(var pen=new Pen(Color.FromArgb(150,Theme.Accent),1)){pen.DashStyle=System.Drawing.Drawing2D.DashStyle.Dash;g.DrawLines(pen,points);}using(var pen=new Pen(Color.Cyan,1.5f))g.DrawBezier(pen,points[0],points[1],points[2],points[3]);
        for(int i=0;i<points.Length;i++){var p=points[i];g.FillRectangle(Brushes.White,p.X-5,p.Y-5,10,10);using(var pen=new Pen(Theme.Accent,1))g.DrawRectangle(pen,p.X-6,p.Y-6,12,12);using(var brush=new SolidBrush(Theme.Text))g.DrawString("P"+i,Theme.Small,brush,p.X+9,p.Y-13);}
    }
    private void PaintGenerationRegions(Graphics g)
    {
        if(State==null||SelectedTrack==null)return;
        var source=SelectedTrack;var pose=Motion.At(source,Frame);
        foreach(var action in source.spawnActions.Concat(source.bulletActions).Where(a=>a.enabled&&a.generationPreset==GenerationPresets.Region)){
            var t=State.tracks.FirstOrDefault(row=>row.id==action.templateId);if(t==null)continue;
            var context=ExpressionContext.For(source,Frame);double x=ScalarExpressions.ActionValue(source,action,"spawnOffsetX",context),y=ScalarExpressions.ActionValue(source,action,"spawnOffsetY",context);
            double angle=(action.inheritAngle?pose.angle:0)+GenerationPresets.Phase(action,source,Math.Max(0,Frame-TimelineLayout.ClockStart(source)),0,Frame),r=angle*Math.PI/180;
            double cx=(action.spawnAtWorld?0:pose.x)+x*Math.Cos(r)+y*Math.Sin(r),cy=(action.spawnAtWorld?0:pose.y)-x*Math.Sin(r)+y*Math.Cos(r),w=Math.Abs(Motion.Value(t,"emitWidth",t.start)),h=Math.Abs(Motion.Value(t,"emitHeight",t.start));
            double rotation=r+Motion.Value(t,"emitRotation",t.start)*Math.PI/180;var points=new[]{new PointF((float)-w/2,(float)-h/2),new PointF((float)w/2,(float)-h/2),new PointF((float)w/2,(float)h/2),new PointF((float)-w/2,(float)h/2)}.Select(p=>new PointF((float)(cx+p.X*Math.Cos(rotation)+p.Y*Math.Sin(rotation)),(float)(cy-p.X*Math.Sin(rotation)+p.Y*Math.Cos(rotation)))).ToArray();
            using(var pen=new Pen(Color.Cyan,1.5f)){pen.DashStyle=System.Drawing.Drawing2D.DashStyle.Dash;g.DrawPolygon(pen,points);}using(var brush=new SolidBrush(Color.Cyan))g.DrawString(Language.Text("生成区域"),Theme.Small,brush,points[0]);
        }
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
