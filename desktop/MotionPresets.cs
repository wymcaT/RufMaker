using System;
using System.Linq;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Drawing;

#pragma warning disable 0649 // Preset parameters are also edited through reflection/JSON.
internal sealed class MotionPresetData
{
    public string name="",easing="linear";
    public int start,end;
    public double startX,startY,endX,endY,peakX,peakY,centerX,centerY,amplitudeX,amplitudeY,radiusStart,radiusEnd,turns=1,phase,hold=.6;
    public MotionPresetData Snapshot(){return (MotionPresetData)MemberwiseClone();}
}

internal static class MotionPresets
{
    public static readonly string[] Names={"抛物线运动","圆周运动","水平往返","垂直往返","水平滑入","水平滑出","向上漂浮","下坠弹跳","斜向穿越","S 形穿行","螺旋扩散","螺旋收束","八字运动","停顿后冲刺"};
    public static readonly string[] PathFields={"pathStartX","pathStartY","pathPeakX","pathPeakY","pathEndX","pathEndY","pathProgress"};
    public static bool Bound(Track t){return t!=null&&t.motionPreset!=null&&Names.Contains(t.motionPreset.name);}
    public static bool Generated(Track t,string field){return Bound(t)&&(field=="x"||field=="y");}
    public static PointF Point(MotionPresetData d,double frame)
    {
        double u=Math.Max(0,Math.Min(1,(frame-d.start)/Math.Max(1.0,d.end-d.start))),v=1-u,a=2*Math.PI*d.turns*u+d.phase*Math.PI/180,x=d.startX,y=d.startY;
        string name=d.name;
        if(name=="抛物线运动"){x=v*v*d.startX+2*v*u*(2*d.peakX-(d.startX+d.endX)/2)+u*u*d.endX;y=v*v*d.startY+2*v*u*(2*d.peakY-(d.startY+d.endY)/2)+u*u*d.endY;}
        else if(name=="圆周运动"||name=="螺旋扩散"||name=="螺旋收束"){double r=d.radiusStart+(d.radiusEnd-d.radiusStart)*u;x=d.centerX+r*Math.Cos(a);y=d.centerY-r*Math.Sin(a);}
        else if(name=="水平往返"||name=="垂直往返"){double w=(1-Math.Cos(a))/2;x+=d.amplitudeX*w;y+=d.amplitudeY*w;}
        else if(name=="八字运动"){x=d.centerX+d.amplitudeX*Math.Sin(a);y=d.centerY+d.amplitudeY*Math.Sin(2*a);}
        else {
            double progress=name=="停顿后冲刺"?Math.Max(0,(u-d.hold)/Math.Max(.001,1-d.hold)):u;
            double w=Motion.Ease(d.easing,progress);x+=(d.endX-d.startX)*w;y+=(d.endY-d.startY)*w;
            if(name=="向上漂浮")x+=d.amplitudeX*Math.Sin(a);
            if(name=="S 形穿行")y+=d.amplitudeY*Math.Sin(a);
        }
        return new PointF((float)x,(float)y);
    }
    public static void Bake(Track t)
    {
        if(!Bound(t))return;t.motion="keyframes";t.velocityX=t.velocityY=t.accelerationX=t.accelerationY=0;
        t.expressions.Remove("x");t.expressions.Remove("y");t.keyframes.RemoveAll(k=>k.property=="x"||k.property=="y");
        for(int f=t.start;f<=t.end;f++){var p=Point(t.motionPreset,f);t.keyframes.Add(new Keyframe{property="x",frame=f,value=p.X,easing="linear"});t.keyframes.Add(new Keyframe{property="y",frame=f,value=p.Y,easing="linear"});}
        var first=Point(t.motionPreset,t.start);t.x=first.X;t.y=first.Y;t.keyframes=t.keyframes.OrderBy(k=>k.frame).ToList();t.keyIndex=null;
    }
    public static void Shift(Track t,int delta){if(Bound(t)){t.motionPreset.start+=delta;t.motionPreset.end+=delta;}}
    public static void Translate(Track t,double dx,double dy)
    {if(!Bound(t))return;var d=t.motionPreset;d.startX+=dx;d.endX+=dx;d.peakX+=dx;d.centerX+=dx;d.startY+=dy;d.endY+=dy;d.peakY+=dy;d.centerY+=dy;Bake(t);}
    public static void Manual(Track t){t.motionPreset=null;t.keyIndex=null;}
    public static void FitRange(Track t){if(Bound(t)){t.motionPreset.start=t.start;t.motionPreset.end=t.end;Bake(t);}}
    public static void Apply(Track t,string name,int frame)
    {
        if(!Names.Contains(name))throw new ArgumentException("未知动作预设");
        var p=Motion.At(t,frame);if(t.parent!=null){var parent=Motion.At(t.parent,frame);double dx=p.x-parent.x,dy=p.y-parent.y,a=parent.angle*Math.PI/180,scale=Math.Abs(parent.scale)<.000001?1:parent.scale;p.x=(dx*Math.Cos(a)-dy*Math.Sin(a))/scale;p.y=(dx*Math.Sin(a)+dy*Math.Cos(a))/scale;}
        foreach(string field in new[]{"x","y","orbitPhase","orbitRadius","velocityX","velocityY","accelerationX","accelerationY","orbitSpeed"}.Concat(PathFields))t.expressions.Remove(field);
        t.keyframes.RemoveAll(k=>k.property=="orbitPhase"||k.property=="orbitRadius"||PathFields.Contains(k.property));
        var d=new MotionPresetData{name=name,start=t.start,end=t.end,startX=p.x,startY=p.y,endX=p.x,endY=p.y,centerX=p.x,centerY=p.y};
        if(name=="抛物线运动"){d.endX+=240;d.peakX=p.x+120;d.peakY=p.y-150;}
        else if(name=="圆周运动"){d.centerX-=100;d.radiusStart=d.radiusEnd=100;}
        else if(name=="水平往返")d.amplitudeX=160;
        else if(name=="垂直往返")d.amplitudeY=160;
        else if(name=="水平滑入"){d.startX-=240;d.easing="expo-out";}
        else if(name=="水平滑出"){d.endX+=240;d.easing="expo-in";}
        else if(name=="向上漂浮"){d.endY-=180;d.amplitudeX=12;}
        else if(name=="下坠弹跳"){d.endY+=200;d.easing="bounce";}
        else if(name=="斜向穿越"){d.endX+=320;d.endY-=180;}
        else if(name=="S 形穿行"){d.endX+=320;d.amplitudeY=80;}
        else if(name=="螺旋扩散"){d.radiusEnd=160;d.turns=2;}
        else if(name=="螺旋收束"){d.centerX-=160;d.radiusStart=160;d.turns=2;}
        else if(name=="八字运动"){d.amplitudeX=140;d.amplitudeY=65;}
        else if(name=="停顿后冲刺"){d.endX+=320;d.easing="expo-in";}
        if(t.kind=="rod"&&t.shape=="rod")t.rodMode="center";t.motionPreset=d;Bake(t);
    }
    public static void Key(Track t,string field,double u,double value,string easing="linear")
    {int at=t.start+(int)Math.Round((t.end-t.start)*u);Motion.SetKey(t,field,at,value);t.keyframes.First(k=>k.property==field&&k.frame==at).easing=easing;}
}
internal sealed partial class MainForm
{
    private int SnapCursor(int value,double tolerance)
    {
        if(state==null||(ModifierKeys&Keys.Alt)!=0)return value;
        return CursorSnapping.Snap(state,selectedTrack,value,tolerance,keySnap.Checked);
    }
    private Func<int,int?> CaptureCursorTargets(double tolerance)
    {return state==null?(Func<int,int?>)(value=>null):CursorSnapping.Capture(state,selectedTrack,tolerance,keySnap.Checked);}
    private void ApplyMotionPreset(string name,Track target)
    {
        if(target==null||!Theme.Spatial(target.kind)||!MotionPresets.Names.Contains(name)){SetStatus("请将动作预设拖到拉镜、弹幕、整体对象或对象轨道上。");return;}
        if(target.end-target.start<2){SetStatus("物件时段至少需要 2 帧，才能导入运动预设。");return;}
        StopPlaying();CommitInspectorEdits();PushUndo();MotionPresets.Apply(target,name,Math.Max(target.start,Math.Min(target.end,frame)));SelectTrack(target,null);timeline.Expand(target,true);MarkChanged();SetStatus("已应用 "+name+"；在动作预设参数中修改路径，每帧自动生成关键帧。");
    }
    private void MotionControls(NativeProperties bag,Track track)
    {
        if(MotionPresets.Bound(track)){
            var d=track.motionPreset;string[] choices=new[]{"手动关键帧"}.Concat(MotionPresets.Names).ToArray();
            Field(bag,"动作预设","动作预设参数",()=>track.motionPreset.name,value=>{if(value=="手动关键帧")MotionPresets.Manual(track);else MotionPresets.Apply(track,value,Math.Max(track.start,Math.Min(track.end,frame)));QueueInspectorUpdate();},choices);
            bag.Add("生成方式","动作预设参数",typeof(string),()=>"每帧采样，参数修改后重建。",null);
            Action<string,string> number=(label,field)=>bag.Add(label,"动作预设参数",typeof(double),()=>typeof(MotionPresetData).GetField(field).GetValue(track.motionPreset),value=>{PushUndo();typeof(MotionPresetData).GetField(field).SetValue(track.motionPreset,field=="hold"?Math.Max(0,Math.Min(.99,(double)value)):(double)value);MotionPresets.Bake(track);if(selectedKey!=null&&MotionPresets.Generated(track,selectedKey.property)){selectedKey=null;timeline.ClearKeySelection();}MarkChanged();});
            bool circle=d.name=="圆周运动"||d.name=="螺旋扩散"||d.name=="螺旋收束",eight=d.name=="八字运动",back=d.name=="水平往返"||d.name=="垂直往返";
            if(circle||eight){number("运动中心 X","centerX");number("运动中心 Y","centerY");}
            else {number("预设起点 X","startX");number("预设起点 Y","startY");if(!back){number("预设终点 X","endX");number("预设终点 Y","endY");}}
            if(d.name=="抛物线运动"){number("预设峰 X","peakX");number("预设峰 Y","peakY");}
            if(d.name=="圆周运动")bag.Add("运动半径","动作预设参数",typeof(double),()=>track.motionPreset.radiusStart,value=>{PushUndo();track.motionPreset.radiusStart=track.motionPreset.radiusEnd=(double)value;MotionPresets.Bake(track);MarkChanged();});
            else if(circle){number("起始半径","radiusStart");number("结束半径","radiusEnd");}
            if(back||eight||d.name=="向上漂浮")number("水平振幅","amplitudeX");
            if(back||eight||d.name=="S 形穿行")number("垂直振幅","amplitudeY");
            if(circle||eight||back||d.name=="向上漂浮"||d.name=="S 形穿行"){number("运动圈数","turns");number("运动初相角","phase");}
            if(d.name=="停顿后冲刺")number("停顿占比 0–1","hold");
            if(!circle&&!eight&&!back&&d.name!="抛物线运动")Field(bag,"路径缓动","动作预设参数",()=>Motion.EaseNames[Math.Max(0,Array.IndexOf(Motion.EaseValues,d.easing))],value=>{d.easing=Motion.EaseValues[Array.IndexOf(Motion.EaseNames,value)];MotionPresets.Bake(track);},Motion.EaseNames);
            Field(bag,track.kind=="camera"?"镜头旋转速度 °/帧":"自转速度 °/帧","运动",()=>track.angularSpeed,value=>track.angularSpeed=value);return;
        }
        string[] names={"关键帧","速度 / 加速度","圆周运动","抛物线（起点 / 终点 / 峰）"},values={"keyframes","velocity","orbit","parabola"};
        Field(bag,"运动逻辑","运动",()=>names[Math.Max(0,Array.IndexOf(values,track.motion))],value=>{track.motion=values[Array.IndexOf(names,value)];QueueInspectorUpdate();},names,"动作预设支持拉镜及物件。运动叠加在关键帧位置上，时间单位为帧。");
        Field(bag,track.kind=="camera"?"镜头旋转速度 °/帧":"自转速度 °/帧","运动",()=>track.angularSpeed,value=>track.angularSpeed=value);
        if(track.motion=="velocity"){Field(bag,"X 速度","运动",()=>track.velocityX,value=>track.velocityX=value);Field(bag,"Y 速度","运动",()=>track.velocityY,value=>track.velocityY=value);Field(bag,"X 加速度","运动",()=>track.accelerationX,value=>track.accelerationX=value);Field(bag,"Y 加速度","运动",()=>track.accelerationY,value=>track.accelerationY=value);}
        if(track.motion=="orbit")Field(bag,"公转速度 °/帧","运动",()=>track.orbitSpeed,value=>track.orbitSpeed=value);
        if(track.motion=="parabola")bag.Add("路径说明","运动",typeof(string),()=>"设置起点、终点和峰（通过点）；路径进度 0→1 沿连续曲线运动。",null);
    }
}
