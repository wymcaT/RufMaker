using System;
using System.Linq;
using System.Windows.Forms;

internal static class MotionPresets
{
    public static readonly string[] Names={"抛物线运动","圆周运动","水平往返","垂直往返","水平滑入","水平滑出","向上漂浮","下坠弹跳","斜向穿越","S 形穿行","螺旋扩散","螺旋收束","八字运动","停顿后冲刺"};
    public static readonly string[] PathFields={"pathStartX","pathStartY","pathPeakX","pathPeakY","pathEndX","pathEndY","pathProgress"};
    public static void Apply(Track t,string name,int frame)
    {
        if(!Names.Contains(name))throw new ArgumentException("未知动作预设");
        var p=Motion.At(t,frame);foreach(string field in new[]{"x","y","orbitPhase","orbitRadius","velocityX","velocityY","accelerationX","accelerationY","orbitSpeed"}.Concat(PathFields))t.expressions.Remove(field);t.keyframes.RemoveAll(k=>k.property=="x"||k.property=="y"||k.property=="orbitPhase"||k.property=="orbitRadius"||PathFields.Contains(k.property));
        int middle=(t.start+t.end)/2;t.motion="keyframes";t.x=p.x;t.y=p.y;
        t.velocityX=t.velocityY=t.accelerationX=t.accelerationY=0;
        if(t.kind=="rod"&&t.shape=="rod")t.rodMode="center";
        if(name==Names[0]){
            t.motion="parabola";t.x=0;t.y=0;t.pathStartX=p.x;t.pathStartY=p.y;t.pathEndX=p.x+240;t.pathEndY=p.y;t.pathPeakX=p.x+120;t.pathPeakY=p.y-150;
            Motion.SetKey(t,"pathProgress",t.start,0);Motion.SetKey(t,"pathProgress",middle,.5);Motion.SetKey(t,"pathProgress",t.end,1);
        }else if(name==Names[1]){
            t.motion="orbit";t.orbitRadius=100;t.orbitSpeed=0;t.orbitPhase=0;t.x=p.x-100;t.y=p.y;
            Motion.SetKey(t,"orbitPhase",t.start,0);Motion.SetKey(t,"orbitPhase",middle,180);Motion.SetKey(t,"orbitPhase",t.end,360);
        }else if(name==Names[2]||name==Names[3]){
            string axis=name==Names[2]?"x":"y";double initial=axis=="x"?p.x:p.y;
            Motion.SetKey(t,axis,t.start,initial);Motion.SetKey(t,axis,middle,initial+160);Motion.SetKey(t,axis,t.end,initial);
        }else if(name=="螺旋扩散"||name=="螺旋收束"){
            bool inward=name=="螺旋收束";t.motion="orbit";t.orbitSpeed=0;t.x=p.x-(inward?160:0);
            Key(t,"orbitRadius",0,inward?160:0);Key(t,"orbitRadius",1,inward?0:160);Key(t,"orbitPhase",0,0);Key(t,"orbitPhase",1,720);
        }else if(name=="水平滑入"){Key(t,"x",0,p.x-240,"expo-out");Key(t,"x",1,p.x);}
        else if(name=="水平滑出"){Key(t,"x",0,p.x,"expo-in");Key(t,"x",1,p.x+240);}
        else if(name=="下坠弹跳"){Key(t,"y",0,p.y,"bounce");Key(t,"y",1,p.y+200);}
        else if(name=="斜向穿越"){Key(t,"x",0,p.x);Key(t,"x",1,p.x+320);Key(t,"y",0,p.y);Key(t,"y",1,p.y-180);}
        else if(name=="停顿后冲刺"){Key(t,"x",0,p.x);Key(t,"x",.6,p.x,"expo-in");Key(t,"x",1,p.x+320);}
        else{
            for(int i=0;i<=32;i++){double u=i/32.0,a=2*Math.PI*u,x=p.x,y=p.y;
                if(name=="向上漂浮"){x+=12*Math.Sin(a);y-=180*u;}
                if(name=="S 形穿行"){x+=320*u;y+=80*Math.Sin(a);}
                if(name=="八字运动"){x+=140*Math.Sin(a);y+=65*Math.Sin(2*a);}
                Key(t,"x",u,x,"sin-inout");Key(t,"y",u,y,"sin-inout");
            }
        }
        if(Array.IndexOf(Names,name)<4)foreach(var key in t.keyframes.Where(k=>k.property=="pathProgress"||k.property=="orbitPhase"||k.property=="x"||k.property=="y"))key.easing=name==Names[2]||name==Names[3]?"sin-inout":"linear";
    }
    public static void Key(Track t,string field,double u,double value,string easing="linear")
    {int at=t.start+(int)Math.Round((t.end-t.start)*u);Motion.SetKey(t,field,at,value);t.keyframes.First(k=>k.property==field&&k.frame==at).easing=easing;}
}
internal sealed partial class MainForm
{
    private int SnapCursor(int value,double tolerance)
    {
        if(state==null||(ModifierKeys&Keys.Alt)!=0)return value;
        return CursorSnapping.Snap(state,selectedTrack,value,tolerance,keySnap.Checked&&timeline.SelectedTracks.Count==1);
    }
    private void ApplyMotionPreset(string name,Track target)
    {
        if(target==null||!Theme.Spatial(target.kind)||!MotionPresets.Names.Contains(name)){SetStatus("请将动作预设拖到拉镜、弹幕、整体对象或对象轨道上。");return;}
        if(target.end-target.start<2){SetStatus("物件时段至少需要 2 帧，才能导入运动预设。");return;}
        StopPlaying();properties.CommitPending();PushUndo();MotionPresets.Apply(target,name,Math.Max(target.start,Math.Min(target.end,frame)));SelectTrack(target,null);timeline.Expand(target,true);MarkChanged();SetStatus("已应用 "+name+"；可编辑各属性关键帧和路径参数，Ctrl+Z 撤销。");
    }
    private void MotionControls(NativeProperties bag,Track track)
    {
        string[] names={"关键帧","速度 / 加速度","圆周运动","抛物线（起点 / 终点 / 峰）"},values={"keyframes","velocity","orbit","parabola"};
        Field(bag,"运动逻辑","运动",()=>names[Math.Max(0,Array.IndexOf(values,track.motion))],value=>{track.motion=values[Array.IndexOf(names,value)];QueueInspectorUpdate();},names,"动作预设支持拉镜及物件。运动叠加在关键帧位置上，时间单位为帧。");
        Field(bag,track.kind=="camera"?"镜头旋转速度 °/帧":"自转速度 °/帧","运动",()=>track.angularSpeed,value=>track.angularSpeed=value);
        if(track.motion=="velocity"){Field(bag,"X 速度","运动",()=>track.velocityX,value=>track.velocityX=value);Field(bag,"Y 速度","运动",()=>track.velocityY,value=>track.velocityY=value);Field(bag,"X 加速度","运动",()=>track.accelerationX,value=>track.accelerationX=value);Field(bag,"Y 加速度","运动",()=>track.accelerationY,value=>track.accelerationY=value);}
        if(track.motion=="orbit")Field(bag,"公转速度 °/帧","运动",()=>track.orbitSpeed,value=>track.orbitSpeed=value);
        if(track.motion=="parabola")bag.Add("路径说明","运动",typeof(string),()=>"设置起点、终点和峰（通过点）；路径进度 0→1 沿连续曲线运动。",null);
    }
}
