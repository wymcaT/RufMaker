using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

internal static class Motion
{
    public static readonly string[] Basic={"x","y","angle","scale","alpha"};
    public static readonly string[] EaseValues={"linear","in","out","smooth","sin-in","sin-out","sin-inout","circle-in","circle-out","circle-inout","power-in","power-out","power-inout","expo-in","expo-out","expo-inout","bounce","bounce-in","bounce-inout","expsin","back","step","edit-smooth","edit-snap","edit-back","edit-elastic"};
    public static readonly string[] EaseNames={"线性","二次缓入","二次缓出","平滑","Sin 缓入","Sin 缓出","Sin 缓入缓出","Circle 缓入","Circle 缓出","Circle 缓入缓出","Power 缓入","Power 缓出","Power 缓入缓出","Expo 缓入","Expo 缓出","Expo 缓入缓出","弹跳缓出","弹跳缓入","弹跳缓入缓出","ExpSin 弹性","Back 回退","保持 / 跳变","剪辑 · 柔和进出","剪辑 · 快进慢停","剪辑 · 过冲回弹","剪辑 · 弹性落位"};
    public static string Name(string property)
    {
        switch(property){case "controlValue":return "事件值";case "controlFrom":return "事件方向";case "eventAcceleration":return "事件加速度";case "eventTurn":return "事件转向";case "from":return "起始值";case "to":return "结束值";case "childSpeed":return "子弹速度";case "spawnAngle":return "生成方向";case "spawnAngleStep":return "每颗生成方向递增 / 度";case "spawnWaveAngleStep":return "每轮递增角度 / 度";case "copyFacingAngle":return "副本朝向偏移 / 度";}
        switch(property){case "cameraIntensity":return "预设运动强度";case "cameraBlur":return "拉镜模糊强度";case "cameraBlurAngle":return "拉镜模糊方向";case "fxZoom":return "冲击缩放";case "fxFlash":return "闪光强度";case "pathStartX":return "起点 X";case "pathStartY":return "起点 Y";case "pathEndX":return "终点 X";case "pathEndY":return "终点 Y";case "pathPeakX":return "峰 X";case "pathPeakY":return "峰 Y";case "pathProgress":return "路径进度 0–1";case "orbitRadius":return "公转半径";case "orbitPhase":return "公转初相角";}
        switch(property){case "emitWidth":return "阵列宽度";case "emitHeight":return "阵列高度";case "emitArc":return "圆弧角度";case "emitPhase":return "阵列相位 / 度";case "emitRotation":return "阵列旋转 / 度";case "emitWaves":return "波形周期数";case "radius":return "半径";case "shapeAspect":return "轮廓高度比例";case "shapeInner":return "内凹比例";case "orientation":return "轮廓朝向";case "sides":return "边数 / 尖角数";case "cameraZoom":return "镜头缩放";case "rainbowDensity":return "颜色密度";case "rainbowOffset":return "起始色相 / 帧";case "rainbowSpeed":return "色彩流动速度 / 帧每帧";case "spriteAngle":return "贴图角度";case "x":return "X";case "y":return "Y";case "angle":return "角度";case "scale":return "缩放";case "alpha":return "透明度";case "x1":return "端点1 X";case "y1":return "端点1 Y";case "x2":return "端点2 X";case "y2":return "端点2 Y";case "length":return "棒长";case "rollSpeed":return "滚动速度";case "speed":return "弹速";case "spread":return "角度间隔";case "count":return "弹幕数量";case "interval":return "发射间隔";case "life":return "寿命";case "strength":return "效果强度";case "fxOpacity":return "效果透明度";case "fxAngle":return "效果方向";case "fxRetention":return "帧混合比例";default:return property;}
    }
    public static string[] Fields(Track t)
    {
        if(t.kind=="group")return Basic;
        var fields=t.kind=="camera"?new[]{"x","y","cameraZoom","angle","cameraBlur","cameraBlurAngle"}.Concat(String.IsNullOrEmpty(t.cameraStyle)?new string[0]:new[]{"cameraIntensity"}).ToArray():t.kind=="effect"?new[]{"strength","fxOpacity","fxAngle","fxRetention"}.Concat(t.effect=="rgb-impact"?new[]{"fxZoom","fxFlash"}:new string[0]).ToArray():t.kind=="rod"?Basic.Concat(FormationShapes.Fields(t.shape)).ToArray():t.kind=="emitter"?Basic.Concat(new[]{"spriteAngle","speed","spread","count","life"}).Concat(t.emissionShape!="point"?new[]{"emitWidth","emitHeight","emitArc","emitPhase","emitRotation","emitWaves"}:new string[0]).ToArray():t.kind=="object"?Basic:new string[0];
        if(Theme.Spatial(t.kind)){if(t.motion=="parabola")fields=fields.Concat(MotionPresets.PathFields).ToArray();if(t.motion=="orbit")fields=fields.Concat(new[]{"orbitRadius","orbitPhase"}).ToArray();}
        return t.rainbow?fields.Concat(Rainbow.Parameters).ToArray():fields;
    }
    public static double Base(Track t,string property)
    {var field=typeof(Track).GetField(property);return field==null?0:Convert.ToDouble(field.GetValue(t));}
    public static bool HasKeys(Track t,string property){return t.keyframes.Any(k=>k.property==property);}
    public static void SetBase(Track t,string property,double value)
    {var field=typeof(Track).GetField(property);if(field==null)throw new ArgumentException("未知属性："+property);field.SetValue(t,field.FieldType==typeof(int)?(object)(int)Math.Round(value):(object)value);}
    public static void EditValue(Track t,string property,int frame,double value)
    {if(HasKeys(t,property))SetKey(t,property,frame,value);else {t.expressions.Remove(property);SetBase(t,property,value);}}
    public static void Normalize(EditorState state)
    {
        if(state.nativeTest==null)state.nativeTest=new NativeTestPosition();
        TimelineGroups.Normalize(state);
        TimelineLayout.Normalize(state);
        TimelineLayout.Order(state);
        foreach(var t in state.tracks)
        {
            if(EventPresets.IsEvent(t))t.controlAction="event";
            t.rainbowMode=Rainbow.Mode(t);t.fadeInFrames=Math.Max(0,t.fadeInFrames);
            if(t.rainbow){t.sprite=String.IsNullOrEmpty(t.rainbowSprite)?Rainbow.Sprite:t.rainbowSprite;t.color="#FFFFFF";}
            var converted=new List<Keyframe>();
            foreach(var key in t.keyframes)
            {
                if(!String.IsNullOrEmpty(key.property)){converted.Add(key);continue;}
                foreach(string field in Basic)converted.Add(new Keyframe{frame=key.frame,property=field,value=Convert.ToDouble(typeof(Keyframe).GetField(field).GetValue(key))});
            }
            t.keyframes=converted.OrderBy(k=>k.frame).ToList();
            NormalizePresets(t,state.fps);
            if(t.trailMode=="legacy")Trails.SetMode(t,t.trail?"fade":"off");
            if(t.kind=="effect"&&t.effect!="flash"&&t.name=="闪屏")t.name=Effects.Name(t.effect);
        }
        state.version=2;
        RandomParameters.Resolve(state);
        BulletSystem.Attach(state);
    }
    private static void NormalizePresets(Track t,int fps)
    {if(MotionPresets.Bound(t)){if(MotionPresets.LegacyKeysMatch(t))MotionPresets.Bake(t);else {t.motionPreset=null;t.keyIndex=null;}}CameraPresets.MigrateLegacy(t,fps);if(CameraPresets.Bound(t))CameraPresets.Refresh(t);foreach(var child in t.children)NormalizePresets(child,fps);}
    private static ExpressionContext GeometryContext(Track t,double frame)
    {var raw=ExpressionContext.For(t,frame);var context=ExpressionContext.For(t,frame);context.x1=ValueCore(t,"x1",frame,raw);context.y1=ValueCore(t,"y1",frame,raw);context.x2=ValueCore(t,"x2",frame,raw);context.y2=ValueCore(t,"y2",frame,raw);return context;}
    public static double Value(Track t,string property,double frame)
    {
        if(!t.liveExpressions)return ValueCore(t,property,frame,null);
        var context=new[]{"x1","y1","x2","y2"}.Contains(property)?ExpressionContext.For(t,frame):GeometryContext(t,frame);
        if(!new[]{"x","y","x1","y1","x2","y2"}.Contains(property)){var position=Position(t,frame,context);context.x=position.x;context.y=position.y;}
        return ValueCore(t,property,frame,context);
    }
    private static double ExpressionValue(Track t,string property,double frame,string expression,string slot,double fallback,ExpressionContext context)
    {if(String.IsNullOrWhiteSpace(expression)||!RandomExpression.IsDynamic(expression)&&(context==null||!context.sampleRandom))return fallback;return RandomParameters.Clamp(property,RandomExpression.Evaluate(expression,t.expressionSeed,t.randomId+"|"+property+"|"+slot,context));}
    public static double SampleValue(Track t,string property,double frame,ExpressionContext context){return ValueCore(t,property,frame,context);}
    private static double ValueCore(Track t,string property,double frame,ExpressionContext context)
    {
        if(MotionPresets.Bound(t)&&(property=="x"||property=="y")){var point=MotionPresets.Point(t.motionPreset,frame);return property=="x"?point.X:point.Y;}
        if(CameraPresets.Generated(t,property))return CameraPresets.Value(t,property,frame);
        Keyframe before=null,after=null;
        if(MotionPresets.Bound(t)||t.keyframeCycle!=null){
            if(t.keyIndex==null)t.keyIndex=t.keyframes.GroupBy(k=>k.property).ToDictionary(g=>g.Key,g=>g.OrderBy(k=>k.frame).ToArray());
            Keyframe[] keys;if(t.keyIndex.TryGetValue(property,out keys)){int lo=0,hi=keys.Length;while(lo<hi){int mid=(lo+hi)/2;if(keys[mid].frame<=frame)lo=mid+1;else hi=mid;}if(lo>0)before=keys[lo-1];if(lo<keys.Length)after=keys[lo];}
        }else foreach(var key in t.keyframes)
        {
            if(key.property!=property)continue;
            if(key.frame<=frame&&(before==null||key.frame>=before.frame))before=key;
            if(key.frame>frame&&(after==null||key.frame<after.frame))after=key;
        }
        string expression;t.expressions.TryGetValue(property,out expression);
        Func<Keyframe,double> value=k=>ExpressionValue(t,property,frame,k.expression,k.randomId,k.value,context);
        if(before==null)return after==null?ExpressionValue(t,property,frame,expression,"base",Base(t,property),context):value(after);
        if(after==null)return value(before);
        double u=(frame-before.frame)/Math.Max(1,after.frame-before.frame);
        return value(before)+(value(after)-value(before))*Ease(String.IsNullOrEmpty(before.easing)?t.easing:before.easing,u,t.easePower,t.easePeriod);
    }
    private static Keyframe Position(Track t,double frame,ExpressionContext context)
    {
        var p=new Keyframe{x=ValueCore(t,"x",frame,context),y=ValueCore(t,"y",frame,context)};
        double dt=Math.Max(0,frame-TimelineLayout.ClockStart(t));
        if(t.motion=="velocity"){p.x+=ValueCore(t,"velocityX",frame,context)*dt+ValueCore(t,"accelerationX",frame,context)*dt*dt/2;p.y+=ValueCore(t,"velocityY",frame,context)*dt+ValueCore(t,"accelerationY",frame,context)*dt*dt/2;}
        if(t.motion=="orbit"){double a=(ValueCore(t,"orbitPhase",frame,context)+ValueCore(t,"orbitSpeed",frame,context)*dt)*Math.PI/180;p.x+=Math.Cos(a)*ValueCore(t,"orbitRadius",frame,context);p.y-=Math.Sin(a)*ValueCore(t,"orbitRadius",frame,context);}
        if(t.motion=="parabola"){
            double u=Math.Max(0,Math.Min(1,ValueCore(t,"pathProgress",frame,context))),v=1-u;
            p.x+=v*v*ValueCore(t,"pathStartX",frame,context)+2*v*u*(2*ValueCore(t,"pathPeakX",frame,context)-(ValueCore(t,"pathStartX",frame,context)+ValueCore(t,"pathEndX",frame,context))/2)+u*u*ValueCore(t,"pathEndX",frame,context);
            p.y+=v*v*ValueCore(t,"pathStartY",frame,context)+2*v*u*(2*ValueCore(t,"pathPeakY",frame,context)-(ValueCore(t,"pathStartY",frame,context)+ValueCore(t,"pathEndY",frame,context))/2)+u*u*ValueCore(t,"pathEndY",frame,context);
        }
        return p;
    }
    public static Keyframe At(Track t,double frame)
    {var context=GeometryContext(t,frame);var p=Position(t,frame,context);context.x=p.x;context.y=p.y;p.angle=ValueCore(t,"angle",frame,context)+ValueCore(t,"angularSpeed",frame,context)*Math.Max(0,frame-TimelineLayout.ClockStart(t));p.scale=ValueCore(t,"scale",frame,context);p.alpha=ValueCore(t,"alpha",frame,context);return BulletSystem.Transform(t.parent,frame,p);}
    public static Keyframe SampleAt(Track t,double frame,ExpressionContext context)
    {var p=Position(t,frame,context);context.x=p.x;context.y=p.y;p.angle=ValueCore(t,"angle",frame,context)+ValueCore(t,"angularSpeed",frame,context)*Math.Max(0,frame-TimelineLayout.ClockStart(t));p.scale=ValueCore(t,"scale",frame,context);p.alpha=ValueCore(t,"alpha",frame,context);return BulletSystem.Transform(t.parent,frame,p);}
    public static void SetKey(Track t,string property,int frame,double value)
    {t.keyIndex=null;var key=t.keyframes.FirstOrDefault(k=>k.property==property&&k.frame==frame);if(key==null){key=new Keyframe{frame=frame,property=property};t.keyframes.Add(key);}key.expression="";key.value=value;t.start=Math.Min(t.start,frame);t.end=Math.Max(t.end,frame);}
    public static PointF[] Endpoints(Track t,double frame)
    {
        if(t.parent!=null){var local=t.Snapshot();local.parent=null;return BulletSystem.TransformPoints(t.parent,frame,Endpoints(local,frame));}
        var p=At(t,frame);double dx=Math.Cos(p.angle*Math.PI/180)*Value(t,"length",frame),dy=-Math.Sin(p.angle*Math.PI/180)*Value(t,"length",frame);
        if(t.rodMode=="center")return new[]{new PointF((float)(p.x-dx/2),(float)(p.y-dy/2)),new PointF((float)(p.x+dx/2),(float)(p.y+dy/2))};
        var b=new PointF((float)Value(t,"x2",frame),(float)Value(t,"y2",frame));
        return new[]{t.rodMode=="anchor"?new PointF((float)p.x,(float)p.y):new PointF((float)Value(t,"x1",frame),(float)Value(t,"y1",frame)),b};
    }
    public static PointF[] RodPoints(Track t,double frame)
    {
        if(t.parent!=null){var local=t.Snapshot();local.parent=null;return BulletSystem.TransformPoints(t.parent,frame,RodPoints(local,frame));}
        if(t.shape!="rod")
        {
            var p=At(t,frame);double radius=Math.Max(0,Value(t,"radius",frame));int n=Math.Max(1,Math.Min(300,(int)BulletSystem.Round(Value(t,"count",frame)))),sides=Math.Max(3,Math.Min(64,(int)BulletSystem.Round(Value(t,"sides",frame))));var result=new PointF[n];
            double aspect=Value(t,"shapeAspect",frame),inner=Value(t,"shapeInner",frame),orientation=t.shape=="ring"?0:Value(t,"orientation",frame),phase=p.angle/360;
            if(t.rolling){double perimeter=FormationShapes.Perimeter(t.shape,radius,sides,aspect,inner);if(perimeter>.001)phase+=(frame-TimelineLayout.ClockStart(t))*Value(t,"rollSpeed",frame)/perimeter;}
            for(int i=0;i<n;i++)
            {
                // heart.gml numbers its instances from 1, star2.gml from 0.
                double u=(i+(t.shape=="heart"?1:0))/(double)n+phase;
                var point=FormationShapes.Point(t.shape,u,radius,orientation,sides,t.shape=="ring"?1:aspect,inner);
                result[i]=new PointF((float)(p.x+point.X),(float)(p.y+point.Y));
            }
            return result;
        }
        var endpoints=Endpoints(t,frame);double dx=endpoints[1].X-endpoints[0].X,dy=endpoints[1].Y-endpoints[0].Y,len=Math.Sqrt(dx*dx+dy*dy);
        int count=Math.Max(1,Math.Min(300,(int)Math.Round(Value(t,"count",frame))));var points=new PointF[count];
        // Rolling uses a half-open line to keep a constant number of evenly spaced bullets at wraparound.
        double offset=t.rolling&&len>.001?(frame-TimelineLayout.ClockStart(t))*Value(t,"rollSpeed",frame)/len:0;
        for(int i=0;i<count;i++){double u=t.rolling?(i/(double)count+offset):count==1?.5:i/(double)(count-1);if(t.rolling)u-=Math.Floor(u);points[i]=new PointF((float)(endpoints[0].X+dx*u),(float)(endpoints[0].Y+dy*u));}return points;
    }
    private static double Bounce(double u)
    {if(u<1/2.75)return 7.5625*u*u;if(u<2/2.75){u-=1.5/2.75;return 7.5625*u*u+.75;}if(u<2.5/2.75){u-=2.25/2.75;return 7.5625*u*u+.9375;}u-=2.625/2.75;return 7.5625*u*u+.984375;}
    public static double Ease(string easing,double u,double power=3,double period=.3)
    {
        u=Math.Max(0,Math.Min(1,u));if(u==0||u==1)return u;power=Math.Max(.1,power);period=Math.Max(.01,period);
        switch(easing){case "edit-smooth":return u*u*(3-2*u);case "edit-snap":return 1-Math.Pow(2,-10*u);case "edit-back":return 1+2.70158*Math.Pow(u-1,3)+1.70158*Math.Pow(u-1,2);case "edit-elastic":return Math.Pow(2,-8*u)*Math.Sin((u-.075)*2*Math.PI/.3)+1;case "in":return u*u;case "out":return 1-(1-u)*(1-u);case "smooth":return u*u*(3-2*u);
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
