using System;
using System.Linq;

internal static class CameraPresets
{
    public static readonly string[] Names={"变速推拉","甩镜过渡","旋转回弹","自然手持漂移","柔和摇摆","呼吸拉镜","缓慢推进","缓慢拉远","旋转缩放过渡","横向甩镜过渡","冲击回弹","轻微手持"};
    public static void Apply(Track t,string name,int frame,int fps)
    {
        if(t.kind!="camera"||!Names.Contains(name))throw new ArgumentException("请选择拉镜预设和拉镜轨道。");
        var p=Motion.At(t,frame);double z=Motion.Value(t,"cameraZoom",frame);
        foreach(string field in new[]{"x","y","angle","cameraZoom","cameraBlur","cameraBlurAngle","angularSpeed","orbitPhase","orbitRadius"}.Concat(MotionPresets.PathFields))t.expressions.Remove(field);
        t.keyframes.RemoveAll(k=>new[]{"x","y","angle","cameraZoom","cameraBlur","cameraBlurAngle","orbitPhase","orbitRadius"}.Contains(k.property)||MotionPresets.PathFields.Contains(k.property));
        t.motion="keyframes";t.x=p.x;t.y=p.y;t.angle=p.angle;t.cameraZoom=z;t.angularSpeed=0;t.cameraStyle="";t.cameraIntensity=1;t.cameraMirror=false;t.cameraBlur=t.cameraBlurAngle=0;
        if(StylePresets.Cameras.Contains(name)){StylePresets.Camera(t,name,fps,p.x,p.y,z,p.angle);return;}
        Action<string,double,double,string> key=(field,u,value,ease)=>MotionPresets.Key(t,field,u,value,ease);
        if(name=="缓慢推进"||name=="缓慢拉远"){
            bool forward=name=="缓慢推进";key("cameraZoom",0,z*(forward?1:1.25),"sin-inout");key("cameraZoom",1,z*(forward?1.25:1),"linear");
        }else if(name=="旋转缩放过渡"){
            foreach(double u in new[]{0,.4,.6,1}){key("cameraZoom",u,z*(u==0||u==1?1:1.4),"sin-inout");key("angle",u,p.angle+(u==.4?-8:u==.6?8:0),"sin-inout");}
        }else if(name=="横向甩镜过渡"){
            key("x",0,p.x,"expo-in");key("x",.45,p.x+240,"step");key("x",.55,p.x-240,"expo-out");key("x",1,p.x,"linear");
            key("cameraZoom",0,z,"sin-inout");key("cameraZoom",.5,z*1.15,"sin-inout");key("cameraZoom",1,z,"linear");
        }else if(name=="冲击回弹"){
            key("cameraZoom",0,z,"expo-out");key("cameraZoom",.2,z*1.2,"expsin");key("cameraZoom",1,z,"linear");
        }else{
            int samples=Math.Max(8,Math.Min(160,(t.end-t.start)/Math.Max(1,fps/10)));double seconds=(t.end-t.start)/(double)Math.Max(1,fps);
            for(int i=0;i<=samples;i++){double u=i/(double)samples,a=u*seconds*2*Math.PI/3,envelope=Math.Sin(Math.PI*u);
                if(name=="呼吸拉镜")key("cameraZoom",u,z*(1+.06*(1-Math.Cos(4*Math.PI*u))/2),"linear");
                else{
                    bool handheld=name=="轻微手持";
                    key("x",u,p.x+envelope*(handheld?5*Math.Sin(1.7*a)+3*Math.Sin(4.3*a):14*Math.Sin(a)),"sin-inout");
                    key("y",u,p.y+envelope*(handheld?4*Math.Cos(2.3*a)+2*Math.Sin(5.1*a):8*Math.Sin(1.3*a)),"sin-inout");
                    key("angle",u,p.angle+envelope*(handheld?.6*Math.Sin(2.1*a):1.2*Math.Sin(.8*a)),"sin-inout");
                    key("cameraZoom",u,z*(1+.06*envelope),"sin-inout");
                }
            }
        }
    }
}

internal static class EffectPresets
{
    public static readonly string[] Names={"淡入","淡出","白色闪屏","黑场","轻柔震屏","RGB 冲击","动态模糊","方向模糊","暗角","反色","故障切换"};
    private static readonly string[] Values={"fade-in","fade-out","flash","color","shake","rgb","motion-blur","blur","vignette","invert","glitch"};
    public static void Apply(Track t,string name)
    {
        int index=Array.IndexOf(Names,name);if(t.kind!="effect"||index<0)throw new ArgumentException("请选择特效预设和特效轨道。");
        foreach(string field in new[]{"strength","fxOpacity","fxAngle","fxRetention","fxFrequency","fxSamples","fxZoom","fxFlash","fxSlices","fxStutter"})t.expressions.Remove(field);
        t.effect=Values[index];t.name=name;t.color=index==2?"#FFFFFF":"#000000";t.strength=index==8?65:index==5?5:8;t.fxOpacity=1;t.fxAngle=0;t.fxRetention=.55;t.fxFrequency=.3;t.fxSamples=6;t.easing="sin-inout";
        t.keyframes.RemoveAll(k=>new[]{"strength","fxOpacity","fxAngle","fxRetention","fxZoom","fxFlash"}.Contains(k.property));
        if(StylePresets.Effect(t,name))return;
        if(index==4||index==5||index==6||index==7){MotionPresets.Key(t,"fxOpacity",0,0,"sin-inout");MotionPresets.Key(t,"fxOpacity",.25,1,"sin-inout");MotionPresets.Key(t,"fxOpacity",1,0);}
    }
    public static double Seconds(string name){return name=="白色闪屏"?.2:name=="故障切换"?.5:name=="RGB 冲击"||name=="轻柔震屏"?.6:1.5;}
}
