using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

internal static class Motion
{
    public static readonly string[] Basic={"x","y","angle","scale","alpha"};
    public static readonly string[] EaseValues={"linear","in","out","smooth","sin-in","sin-out","sin-inout","circle-in","circle-out","circle-inout","power-in","power-out","power-inout","expo-in","expo-out","expo-inout","bounce","bounce-in","bounce-inout","expsin","back","step"};
    public static readonly string[] EaseNames={"线性","二次缓入","二次缓出","平滑","Sin 缓入","Sin 缓出","Sin 缓入缓出","Circle 缓入","Circle 缓出","Circle 缓入缓出","Power 缓入","Power 缓出","Power 缓入缓出","Expo 缓入","Expo 缓出","Expo 缓入缓出","弹跳缓出","弹跳缓入","弹跳缓入缓出","ExpSin 弹性","Back 回退","保持 / 跳变"};
    public static string Name(string property)
    {
        switch(property){case "radius":return "半径";case "orientation":return "多边形朝向";case "sides":return "边数";case "cameraZoom":return "镜头缩放";case "rainbowDensity":return "颜色密度";case "rainbowOffset":return "起始色相 / 帧";case "rainbowSpeed":return "色彩流动速度 / 帧每帧";case "spriteAngle":return "贴图角度";case "x":return "X";case "y":return "Y";case "angle":return "角度";case "scale":return "缩放";case "alpha":return "透明度";case "x1":return "端点1 X";case "y1":return "端点1 Y";case "x2":return "端点2 X";case "y2":return "端点2 Y";case "length":return "棒长";case "rollSpeed":return "滚动速度";case "speed":return "弹速";case "spread":return "角度间隔";case "count":return "弹幕数量";case "interval":return "发射间隔";case "life":return "寿命";case "strength":return "效果强度";case "fxOpacity":return "效果透明度";case "fxAngle":return "效果方向";case "fxRetention":return "帧混合比例";default:return property;}
    }
    public static string[] Fields(Track t)
    {
        var fields=t.kind=="camera"?new[]{"x","y","cameraZoom","angle"}:t.kind=="effect"?new[]{"strength","fxOpacity","fxAngle","fxRetention"}:t.kind=="rod"?Basic.Concat(t.shape=="rod"?new[]{"spriteAngle","x1","y1","x2","y2","length","rollSpeed","count"}:t.shape=="polygon"?new[]{"spriteAngle","radius","orientation","sides","rollSpeed","count"}:new[]{"spriteAngle","radius","rollSpeed","count"}).ToArray():t.kind=="emitter"?Basic.Concat(new[]{"spriteAngle","speed","spread","count","life"}).ToArray():t.kind=="object"?Basic:new string[0];return t.rainbow?fields.Concat(Rainbow.Parameters).ToArray():fields;
    }
    public static double Base(Track t,string property)
    {var field=typeof(Track).GetField(property);return field==null?0:Convert.ToDouble(field.GetValue(t));}
    public static bool HasKeys(Track t,string property){return t.keyframes.Any(k=>k.property==property);}
    public static void SetBase(Track t,string property,double value)
    {var field=typeof(Track).GetField(property);if(field==null)throw new ArgumentException("未知属性："+property);field.SetValue(t,field.FieldType==typeof(int)?(object)(int)Math.Round(value):(object)value);}
    public static void EditValue(Track t,string property,int frame,double value)
    {if(HasKeys(t,property))SetKey(t,property,frame,value);else SetBase(t,property,value);}
    public static void Normalize(EditorState state)
    {
        foreach(var t in state.tracks)
        {
            if(t.rainbow){t.sprite=Rainbow.Sprite;t.color="#FFFFFF";}
            var converted=new List<Keyframe>();
            foreach(var key in t.keyframes)
            {
                if(!String.IsNullOrEmpty(key.property)){converted.Add(key);continue;}
                foreach(string field in Basic)converted.Add(new Keyframe{frame=key.frame,property=field,value=Convert.ToDouble(typeof(Keyframe).GetField(field).GetValue(key))});
            }
            t.keyframes=converted.OrderBy(k=>k.frame).ToList();
            if(t.trailMode=="legacy")Trails.SetMode(t,t.trail?"fade":"off");
            if(t.kind=="effect"&&t.effect!="flash"&&t.name=="闪屏")t.name=Effects.Name(t.effect);
        }
        state.version=2;
    }
    public static double Value(Track t,string property,double frame)
    {
        Keyframe before=null,after=null;
        foreach(var key in t.keyframes)
        {
            if(key.property!=property)continue;
            if(key.frame<=frame&&(before==null||key.frame>=before.frame))before=key;
            if(key.frame>frame&&(after==null||key.frame<after.frame))after=key;
        }
        if(before==null)return after==null?Base(t,property):after.value;
        if(after==null)return before.value;
        double u=(frame-before.frame)/Math.Max(1,after.frame-before.frame);
        return before.value+(after.value-before.value)*Ease(String.IsNullOrEmpty(before.easing)?t.easing:before.easing,u,t.easePower,t.easePeriod);
    }
    public static Keyframe At(Track t,double frame)
    {
        var p=new Keyframe{x=Value(t,"x",frame),y=Value(t,"y",frame),angle=Value(t,"angle",frame),scale=Value(t,"scale",frame),alpha=Value(t,"alpha",frame)};
        double dt=Math.Max(0,frame-t.start);
        if(t.motion=="velocity"){p.x+=t.velocityX*dt+t.accelerationX*dt*dt/2;p.y+=t.velocityY*dt+t.accelerationY*dt*dt/2;}
        if(t.motion=="orbit"){double a=(t.orbitPhase+t.orbitSpeed*dt)*Math.PI/180;p.x+=Math.Cos(a)*t.orbitRadius;p.y-=Math.Sin(a)*t.orbitRadius;}
        p.angle+=t.angularSpeed*dt;return p;
    }
    public static void SetKey(Track t,string property,int frame,double value)
    {var key=t.keyframes.FirstOrDefault(k=>k.property==property&&k.frame==frame);if(key==null){key=new Keyframe{frame=frame,property=property};t.keyframes.Add(key);}key.value=value;t.start=Math.Min(t.start,frame);t.end=Math.Max(t.end,frame);}
    public static PointF[] Endpoints(Track t,double frame)
    {
        var p=At(t,frame);double dx=Math.Cos(p.angle*Math.PI/180)*Value(t,"length",frame),dy=-Math.Sin(p.angle*Math.PI/180)*Value(t,"length",frame);
        if(t.rodMode=="center")return new[]{new PointF((float)(p.x-dx/2),(float)(p.y-dy/2)),new PointF((float)(p.x+dx/2),(float)(p.y+dy/2))};
        var b=new PointF((float)Value(t,"x2",frame),(float)Value(t,"y2",frame));
        return new[]{t.rodMode=="anchor"?new PointF((float)p.x,(float)p.y):new PointF((float)Value(t,"x1",frame),(float)Value(t,"y1",frame)),b};
    }
    public static PointF[] RodPoints(Track t,double frame)
    {
        if(t.shape!="rod")
        {
            var p=At(t,frame);double radius=Math.Max(0,Value(t,"radius",frame));int n=Math.Max(1,Math.Min(300,(int)Math.Round(Value(t,"count",frame)))),sides=Math.Max(3,Math.Min(64,(int)Math.Round(Value(t,"sides",frame))));var result=new PointF[n];
            double perimeter=t.shape=="polygon"?2*sides*radius*Math.Sin(Math.PI/sides):2*Math.PI*radius,phase=p.angle/360;
            if(t.rolling&&perimeter>.001)phase+=(frame-t.start)*Value(t,"rollSpeed",frame)/perimeter;
            for(int i=0;i<n;i++)
            {
                double u=i/(double)n+phase;u-=Math.Floor(u);double x,y;
                if(t.shape=="polygon"){double v=u*sides,seg=Math.Floor(v),f=v-seg,a=(Value(t,"orientation",frame)+seg*360/sides)*Math.PI/180,b=a+2*Math.PI/sides;x=radius*(Math.Cos(a)*(1-f)+Math.Cos(b)*f);y=-radius*(Math.Sin(a)*(1-f)+Math.Sin(b)*f);}
                else{x=radius*Math.Cos(u*2*Math.PI);y=-radius*Math.Sin(u*2*Math.PI);}
                result[i]=new PointF((float)(p.x+x),(float)(p.y+y));
            }
            return result;
        }
        var endpoints=Endpoints(t,frame);double dx=endpoints[1].X-endpoints[0].X,dy=endpoints[1].Y-endpoints[0].Y,len=Math.Sqrt(dx*dx+dy*dy);
        int count=Math.Max(1,Math.Min(300,(int)Math.Round(Value(t,"count",frame))));var points=new PointF[count];
        // Rolling uses a half-open line to keep a constant number of evenly spaced bullets at wraparound.
        double offset=t.rolling&&len>.001?(frame-t.start)*Value(t,"rollSpeed",frame)/len:0;
        for(int i=0;i<count;i++){double u=t.rolling?(i/(double)count+offset):count==1?.5:i/(double)(count-1);if(t.rolling)u-=Math.Floor(u);points[i]=new PointF((float)(endpoints[0].X+dx*u),(float)(endpoints[0].Y+dy*u));}return points;
    }
    private static double Bounce(double u)
    {if(u<1/2.75)return 7.5625*u*u;if(u<2/2.75){u-=1.5/2.75;return 7.5625*u*u+.75;}if(u<2.5/2.75){u-=2.25/2.75;return 7.5625*u*u+.9375;}u-=2.625/2.75;return 7.5625*u*u+.984375;}
    public static double Ease(string easing,double u,double power=3,double period=.3)
    {
        u=Math.Max(0,Math.Min(1,u));if(u==0||u==1)return u;power=Math.Max(.1,power);period=Math.Max(.01,period);
        switch(easing){case "in":return u*u;case "out":return 1-(1-u)*(1-u);case "smooth":return u*u*(3-2*u);
        case "sin-in":return 1-Math.Cos(u*Math.PI/2);case "sin-out":return Math.Sin(u*Math.PI/2);case "sin-inout":return (1-Math.Cos(u*Math.PI))/2;
        case "circle-in":return 1-Math.Sqrt(1-u*u);case "circle-out":return Math.Sqrt(1-(u-1)*(u-1));case "circle-inout":return u<.5?(1-Math.Sqrt(1-4*u*u))/2:(Math.Sqrt(1-4*(u-1)*(u-1))+1)/2;
        case "power-in":return Math.Pow(u,power);case "power-out":return 1-Math.Pow(1-u,power);case "power-inout":return u<.5?Math.Pow(2*u,power)/2:1-Math.Pow(2*(1-u),power)/2;
        case "expo-in":return Math.Pow(2,10*(u-1));case "expo-out":return 1-Math.Pow(2,-10*u);case "expo-inout":return u<.5?Math.Pow(2,20*u-10)/2:1-Math.Pow(2,-20*u+10)/2;
        case "bounce":return Bounce(u);case "bounce-in":return 1-Bounce(1-u);case "bounce-inout":return u<.5?(1-Bounce(1-2*u))/2:(1+Bounce(2*u-1))/2;
        case "expsin":return Math.Pow(2,-10*u)*Math.Sin((u-period/4)*2*Math.PI/period)+1;case "back":return 1+2.70158*Math.Pow(u-1,3)+1.70158*Math.Pow(u-1,2);case "step":return 0;default:return u;}
    }
    public static Keyframe Copy(Keyframe key){return key.Snapshot();}
    public static Color ColorOf(string color){try{return ColorTranslator.FromHtml(color);}catch{return Color.White;}}
}
