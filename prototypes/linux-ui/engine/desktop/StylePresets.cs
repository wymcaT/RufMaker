using System;
using System.Linq;

internal static class StylePresets
{
    public static readonly string[] Cameras={"变速推拉","甩镜过渡","旋转回弹","自然手持漂移"};
    public static double Seconds(string name){return name=="甩镜过渡"?.6:name=="变速推拉"||name=="旋转回弹"?1.6:4;}
    public static void Camera(Track t,string name,int fps,double x,double y,double z,double angle)
    {
        t.cameraStyle=name;t.cameraIntensity=1;t.cameraOriginX=x;t.cameraOriginY=y;t.cameraOriginZoom=z;t.cameraOriginAngle=angle;t.cameraMirror=true;t.cameraBlur=0;t.cameraBlurAngle=0;
        int length=Math.Max(1,t.end-t.start);
        for(int i=0;i<=Math.Min(240,length);i++){
            double u=i/(double)Math.Min(240,length),dx=0,dy=0,zoom=z,a=angle,blur=0,blurAngle=0;
            if(name=="变速推拉"){
                if(u<.1)zoom=z*(1-.035*Motion.Ease("smooth",u/.1));
                else if(u<.21)zoom=z*(.965+.43*Motion.Ease("expo-out",(u-.1)/.11));
                else if(u<.8)zoom=z*(1+.395*(1-Motion.Ease("smooth",(u-.21)/.59)));
                blur=18*Math.Max(0,1-Math.Abs(u-.16)/.08);
            }else if(name=="甩镜过渡"){
                if(u<.5){double v=Motion.Ease("expo-in",u*2);dx=800*v;a=angle+2*v;}
                else{double v=1-Motion.Ease("expo-out",(u-.5)*2);dx=-800*v;a=angle-2*v;}
                zoom=z*(1+.08*Math.Sin(Math.PI*u));blur=90*Math.Pow(Math.Sin(Math.PI*u),4);
            }else if(name=="旋转回弹"){
                double sec=u*1.6;a+=15*Math.Exp(-4.8*sec)*Math.Sin(18*sec);zoom=z*(1+.22*Math.Exp(-4*sec)*Math.Sin(10*sec));blur=8*Math.Exp(-10*sec);blurAngle=45;
            }else{
                double sec=u*length/Math.Max(1,fps),envelope=Math.Min(1,Math.Min(u/.08,(1-u)/.08));envelope=Motion.Ease("sin-inout",envelope);
                dx=envelope*(9*Math.Sin(sec*1.6)+3*Math.Sin(sec*4.1+.6)+1.5*Math.Sin(sec*8.7));
                dy=envelope*(5*Math.Sin(sec*1.1+1.4)+2*Math.Sin(sec*3.7)+.6*Math.Sin(sec*10.2));
                a+=envelope*(.8*Math.Sin(sec*1.3)+.18*Math.Sin(sec*4.6+1.3));zoom=z*(1+.065*envelope+.006*Math.Sin(sec*1.2)*envelope);
            }
            if(i==0||i==Math.Min(240,length)){dx=dy=blur=0;zoom=z;a=angle;}
            MotionPresets.Key(t,"x",u,x+dx);MotionPresets.Key(t,"y",u,y+dy);MotionPresets.Key(t,"angle",u,a);MotionPresets.Key(t,"cameraZoom",u,zoom);MotionPresets.Key(t,"cameraBlur",u,blur);MotionPresets.Key(t,"cameraBlurAngle",u,blurAngle);
        }
    }
    public static bool Effect(Track t,string name)
    {
        if(name!="RGB 冲击"&&name!="故障切换")return false;
        t.effect=name=="RGB 冲击"?"rgb-impact":"glitch";t.name=name;t.color="#FFFFFF";t.strength=name=="RGB 冲击"?11:55;t.fxOpacity=1;t.fxAngle=0;t.fxRetention=.24;t.fxZoom=1.13;t.fxFlash=.18;t.fxSlices=11;t.fxStutter=2;t.easing="linear";
        if(name=="RGB 冲击"){MotionPresets.Key(t,"fxOpacity",0,0,"expo-out");MotionPresets.Key(t,"fxOpacity",.08,1,"expo-out");MotionPresets.Key(t,"fxOpacity",1,0);}
        else{MotionPresets.Key(t,"fxOpacity",0,0,"sin-inout");MotionPresets.Key(t,"fxOpacity",.25,1,"linear");MotionPresets.Key(t,"fxOpacity",.6,.85,"sin-inout");MotionPresets.Key(t,"fxOpacity",1,0);}
        return true;
    }
}
