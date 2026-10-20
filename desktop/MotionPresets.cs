using System;
using System.Linq;
using System.Windows.Forms;

internal static class MotionPresets
{
    public static readonly string[] Names={"抛物线运动","圆周运动","水平往返","垂直往返"};
    public static readonly string[] PathFields={"pathStartX","pathStartY","pathPeakX","pathPeakY","pathEndX","pathEndY","pathProgress"};
    public static void Apply(Track t,string name,int frame)
    {
        var p=Motion.At(t,frame);t.keyframes.RemoveAll(k=>k.property=="x"||k.property=="y"||k.property=="orbitPhase"||k.property=="orbitRadius"||PathFields.Contains(k.property));
        int middle=(t.start+t.end)/2;t.motion="keyframes";t.x=p.x;t.y=p.y;
        if(t.kind=="rod"&&t.shape=="rod")t.rodMode="center";
        if(name==Names[0]){
            t.motion="parabola";t.x=0;t.y=0;t.pathStartX=p.x;t.pathStartY=p.y;t.pathEndX=p.x+240;t.pathEndY=p.y;t.pathPeakX=p.x+120;t.pathPeakY=p.y-150;
            Motion.SetKey(t,"pathProgress",t.start,0);Motion.SetKey(t,"pathProgress",middle,.5);Motion.SetKey(t,"pathProgress",t.end,1);
        }else if(name==Names[1]){
            t.motion="orbit";t.orbitRadius=100;t.orbitSpeed=0;t.orbitPhase=0;t.x=p.x-100;t.y=p.y;
            Motion.SetKey(t,"orbitPhase",t.start,0);Motion.SetKey(t,"orbitPhase",middle,180);Motion.SetKey(t,"orbitPhase",t.end,360);
        }else{
            string axis=name==Names[2]?"x":"y";double initial=axis=="x"?p.x:p.y;
            Motion.SetKey(t,axis,t.start,initial);Motion.SetKey(t,axis,middle,initial+160);Motion.SetKey(t,axis,t.end,initial);
        }
        foreach(var key in t.keyframes.Where(k=>k.property=="pathProgress"||k.property=="orbitPhase"||k.property=="x"||k.property=="y"))key.easing=name==Names[2]||name==Names[3]?"sin-inout":"linear";
    }
}
internal sealed partial class MainForm
{
    private int SnapCursor(int value,double tolerance)
    {
        if(state==null||(ModifierKeys&Keys.Alt)!=0)return value;
        if(keySnap.Checked&&selectedTrack!=null&&timeline.SelectedTracks.Count==1&&selectedTrack.keyframes.Count>0){int closest=selectedTrack.keyframes.Select(k=>k.frame).OrderBy(f=>Math.Abs(f-value)).First();if(Math.Abs(closest-value)<=tolerance)return closest;}
        return BeatGrid.Snap(state,value);
    }
    private void ApplyMotionPreset(string name,Track target)
    {
        if(target==null||!Theme.Visual(target.kind)||!MotionPresets.Names.Contains(name)){SetStatus("请将预设拖到弹幕、整体对象或对象轨道上。");return;}
        if(target.end-target.start<2){SetStatus("物件时段至少需要 2 帧，才能导入运动预设。");return;}
        StopPlaying();PushUndo();MotionPresets.Apply(target,name,Math.Max(target.start,Math.Min(target.end,frame)));SelectTrack(target,null);timeline.Expand(target,true);MarkChanged();SetStatus("已应用 "+name+"；可在右栏调整路径参数，Ctrl+Z 撤销。");
    }
}
