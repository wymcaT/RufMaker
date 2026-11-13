using System;
using System.Linq;

internal static class ObjectDynamics
{
    public static readonly string[] Fields={"bulletAcceleration","bulletFriction","bulletGravity","bulletGravityDirection"};
    public static readonly string[] Rates={"bulletAcceleration","bulletFriction","bulletGravity"};
    public static bool Enabled(Track t){return Fields.Any(p=>Motion.Base(t,p)!=(p=="bulletGravityDirection"?270:0)||t.expressions.ContainsKey(p)||t.keyframes.Any(k=>k.property==p));}
    public static void Normalize(Track t){foreach(string p in Fields){double v=Motion.Base(t,p);if(Double.IsNaN(v)||Double.IsInfinity(v)||Math.Abs(v)>1000000||p=="bulletFriction"&&v<0)throw new FormatException("弹幕动力参数无效。");}}
    public static void ClearGravity(SimBullet b){b.gravity=0;b.gravityDirection=270;}
    public static void ScaleKey(Keyframe k,double ratio){if(!Rates.Contains(k.property)&&k.property!="imageSpeed"&&k.property!="imageDelay")return;double factor=k.property=="imageDelay"?ratio:1/(k.property=="imageSpeed"?ratio:ratio*ratio);k.value*=factor;if(!String.IsNullOrWhiteSpace(k.expression)&&ratio!=1)k.expression="("+k.expression+")*"+factor.ToString("R",System.Globalization.CultureInfo.InvariantCulture);}
    public static void ScaleTrack(Track t,double ratio){foreach(string p in new[]{"bulletAcceleration","bulletFriction","bulletGravity","imageSpeed","imageDelay"}){double factor=p=="imageDelay"?ratio:1/(p=="imageSpeed"?ratio:ratio*ratio);Motion.SetBase(t,p,p=="imageDelay"?BulletSystem.Round(Motion.Base(t,p)*factor):Motion.Base(t,p)*factor);ScalarExpressions.ScaleBinding(t.expressions,p,factor);}if(t.controlAction=="gravity"){t.controlFrom/=ratio*ratio;t.controlValue/=ratio*ratio;ScalarExpressions.ScaleBinding(t.expressions,"controlFrom",1/(ratio*ratio));ScalarExpressions.ScaleBinding(t.expressions,"controlValue",1/(ratio*ratio));if(!String.IsNullOrWhiteSpace(t.controlExpression)&&ratio!=1)t.controlExpression="("+t.controlExpression+")*"+(1/(ratio*ratio)).ToString("R",System.Globalization.CultureInfo.InvariantCulture);}}
    public static readonly string[] Physics={"bulletAcceleration","bulletFriction","bulletGravity","bulletGravityDirection"};
    public static readonly string[] Animation={"imageIndex","imageSpeed","imageDelay"};
    public static bool HasPhysics(Track t)
    {return t.bulletAcceleration!=0||t.bulletFriction!=0||t.bulletGravity!=0||t.keyframes.Any(k=>k.property=="bulletAcceleration"||k.property=="bulletFriction"||k.property=="bulletGravity")||Physics.Take(3).Any(t.expressions.ContainsKey);}
    public static bool HasAnimation(Track t)
    {return t.imageIndex!=0||t.imageSpeed!=0||t.imageDelay!=0||t.keyframes.Any(k=>k.property=="imageIndex"||k.property=="imageSpeed"||k.property=="imageDelay")||Animation.Any(t.expressions.ContainsKey);}
    public static int TrackImage(Track t,double time,double sampleTime)
    {return (int)Math.Floor(Motion.Value(t,"imageIndex",sampleTime)+Motion.Value(t,"imageSpeed",sampleTime)*Math.Max(0,time-(t.kind=="emitter"?sampleTime:TimelineLayout.ClockStart(t))-Motion.Value(t,"imageDelay",sampleTime)));}
    public static bool IsFrozen(SimBullet b){return b.imageFrozen||b.frozenImage>=0;}
    public static void Freeze(SimBullet b,int image){b.frozenImage=image;b.imageFrozen=true;}
    public static int Image(SimBullet b,int frame)
    {
        if(IsFrozen(b))return b.frozenImage;
        // Flying explicit frames were already sampled at birth, including negative fallback.
        if(b.kinematic)return Rainbow.Index(b.source,b.sequence,frame-b.clockShift,frame-b.clockShift);
        if(b.source.rainbow)return Rainbow.FlyingIndex(b,frame);
        return (int)Math.Floor(b.animationIndex+Math.Max(0,frame-b.animationBirth-b.animationDelay)*b.animationSpeed);
    }
    public static void Initialize(SimBullet b,Track t,int birth,int sampleTime,ExpressionContext context)
    {
        double explicitFrame=Motion.SampleValue(t,"spriteFrame",sampleTime,context);if(explicitFrame>=0)Freeze(b,(int)Math.Floor(explicitFrame));
        if(t.rainbow){b.rainbowDensity=Motion.SampleValue(t,"rainbowDensity",sampleTime,context);b.rainbowOffset=Motion.SampleValue(t,"rainbowOffset",sampleTime,context);b.rainbowSpeed=Motion.SampleValue(t,"rainbowSpeed",sampleTime,context);b.rainbowSampled=true;}
        b.acceleration=Motion.SampleValue(t,"bulletAcceleration",sampleTime,context);
        b.friction=Math.Max(0,Motion.SampleValue(t,"bulletFriction",sampleTime,context));
        b.gravity=Motion.SampleValue(t,"bulletGravity",sampleTime,context);
        b.gravityDirection=Motion.SampleValue(t,"bulletGravityDirection",sampleTime,context);
        b.animationBirth=birth;b.animationSpeed=Motion.SampleValue(t,"imageSpeed",sampleTime,context);
        int delay=Math.Max(0,BulletSystem.Round(Motion.SampleValue(t,"imageDelay",sampleTime,context))),elapsed=t.kind=="rod"?Math.Max(0,sampleTime-TimelineLayout.ClockStart(t)):0;
        b.animationDelay=Math.Max(0,delay-elapsed);b.animationIndex=Motion.SampleValue(t,"imageIndex",sampleTime,context)+Math.Max(0,elapsed-delay)*b.animationSpeed;
    }
    public static void Gravity(SimBullet b)
    {
        if(b.gravity==0)return;
        double a=b.direction*Math.PI/180,g=b.gravityDirection*Math.PI/180;
        double vx=Math.Cos(a)*b.speed+Math.Cos(g)*b.gravity,vy=-Math.Sin(a)*b.speed-Math.Sin(g)*b.gravity;
        b.speed=Math.Sqrt(vx*vx+vy*vy);if(b.speed>.000000001)b.direction=ExpressionContext.Direction(0,0,vx,vy);
    }
    public static void Scale(Track t,double ratio)
    {
        if(t.controlAction=="gravity"){t.controlFrom/=ratio*ratio;t.controlValue/=ratio*ratio;ScalarExpressions.ScaleBinding(t.expressions,"controlFrom",1/(ratio*ratio));ScalarExpressions.ScaleBinding(t.expressions,"controlValue",1/(ratio*ratio));if(!String.IsNullOrWhiteSpace(t.controlExpression)&&ratio!=1)t.controlExpression="("+t.controlExpression+")*"+(1/(ratio*ratio)).ToString("R",System.Globalization.CultureInfo.InvariantCulture);}
        foreach(string p in new[]{"bulletAcceleration","bulletFriction","bulletGravity","imageSpeed","imageDelay"}){
            double factor=p=="imageDelay"?ratio:1/(p=="imageSpeed"?ratio:ratio*ratio);
            double value=Motion.Base(t,p)*factor;Motion.SetBase(t,p,p=="imageDelay"?BulletSystem.Round(value):value);ScalarExpressions.ScaleBinding(t.expressions,p,factor);
            foreach(var k in t.keyframes.Where(k=>k.property==p)){k.value*=factor;if(!String.IsNullOrWhiteSpace(k.expression))k.expression="("+k.expression+")*"+factor.ToString("0.################",System.Globalization.CultureInfo.InvariantCulture);}
            if(t.keyframeCycle!=null)foreach(var k in t.keyframeCycle.keys.Where(k=>k.property==p)){k.value*=factor;if(!String.IsNullOrWhiteSpace(k.expression))k.expression="("+k.expression+")*"+factor.ToString("0.################",System.Globalization.CultureInfo.InvariantCulture);}
        }
    }
}

internal sealed partial class MainForm
{
    private void DynamicsFields(NativeProperties bag,Track track)
    {
        if(ObjectProperties.Supports(track,"physics")&&ObjectProperties.Shows(track,5))
            bag.Add("作用方式","弹幕物理",typeof(string),()=>Language.Text("出生时取样；加速度、阻力、重力依次生效。整体对象在散开后生效。"),null);
        if(ObjectProperties.Supports(track,"animation")&&ObjectProperties.Shows(track,6)&&track.rainbow)
            bag.Add("动画控制","贴图动画",typeof(string),()=>Language.Text("当前由彩虹配色控制贴图帧；关闭彩虹后使用动画参数。"),null);
    }
}
