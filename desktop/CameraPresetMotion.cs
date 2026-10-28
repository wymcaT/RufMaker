using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;

// A preset is a compact description, not a row of generated timeline keys.
internal sealed class CameraPresetData
{
    public string name="";public int start,end;
    public double centerX,centerY,baseZoom=1,baseAngle,amplitudeX,amplitudeY,angleAmplitude,zoomAmplitude,blurAmplitude,speed=1;
    public bool legacySampling;public int legacyStart,legacyLength,legacyFps;public List<CameraTimeTransform> legacyTime=new List<CameraTimeTransform>();
    [ScriptIgnore] public Dictionary<string,Keyframe[]> samples;[ScriptIgnore] public int sampleFps;
    public CameraPresetData Snapshot(){var copy=(CameraPresetData)MemberwiseClone();copy.legacyTime=(legacyTime??new List<CameraTimeTransform>()).Select(s=>s.Snapshot()).ToList();return copy;}
}
internal sealed class CameraTimeTransform
{public int origin,subtract;public double scale=1;public CameraTimeTransform Snapshot(){return (CameraTimeTransform)MemberwiseClone();}}

internal static partial class CameraPresets
{
    public static readonly string[] ControlledFields={"x","y","angle","cameraZoom","cameraBlur","cameraBlurAngle"};
    public static bool Bound(Track t){return t!=null&&t.kind=="camera"&&t.cameraPreset!=null&&Names.Contains(t.cameraPreset.name);}
    public static bool Generated(Track t,string field){return Bound(t)&&ControlledFields.Contains(field);}
    private static void Transform(CameraPresetData d,int origin,int subtract,double scale)
    {if(!d.legacySampling)return;if(d.legacyTime==null)d.legacyTime=new List<CameraTimeTransform>();if(scale==1&&subtract==0&&d.legacyTime.Count>0&&d.legacyTime[d.legacyTime.Count-1].scale==1&&d.legacyTime[d.legacyTime.Count-1].subtract==0)d.legacyTime[d.legacyTime.Count-1].origin+=origin;else d.legacyTime.Add(new CameraTimeTransform{origin=origin,subtract=subtract,scale=scale});}
    public static void Shift(Track t,int delta){if(Bound(t)&&delta!=0){var d=t.cameraPreset;d.start+=delta;d.end+=delta;Transform(d,delta,0,1);d.samples=null;}}
    public static void Scale(Track t,int origin,double ratio){if(!Bound(t))return;var d=t.cameraPreset;Transform(d,origin,0,ratio);d.start=origin+BulletSystem.Round(d.start*ratio);d.end=origin+BulletSystem.Round(d.end*ratio);d.samples=null;}
    public static void FitRange(Track t){if(!Bound(t))return;var d=t.cameraPreset;int oldStart=d.start,oldLength=Math.Max(1,d.end-d.start),newLength=Math.Max(1,t.end-t.start);if(oldStart!=t.start||d.end!=t.end)Transform(d,t.start,oldStart,newLength/(double)oldLength);d.start=t.start;d.end=t.end;d.samples=null;}
    public static void SetDuration(Track t,int length){var d=t.cameraPreset;int oldLength=Math.Max(1,d.end-d.start);length=Math.Max(1,Math.Min(1000000,length));if(length!=oldLength)Transform(d,d.start,d.start,length/(double)oldLength);d.end=d.start+length;d.samples=null;}
    public static void Refresh(Track t)
    {if(!Bound(t))return;var d=t.cameraPreset;d.samples=null;t.cameraStyle=d.name;t.x=t.cameraOriginX=d.centerX;t.y=t.cameraOriginY=d.centerY;t.angle=t.cameraOriginAngle=d.baseAngle;t.cameraZoom=t.cameraOriginZoom=d.baseZoom;t.keyframes.RemoveAll(k=>ControlledFields.Contains(k.property));foreach(string field in ControlledFields)t.expressions.Remove(field);t.keyIndex=null;TimelineEditing.RemoveControlledCycleKeys(t);}
    public static void Translate(Track t,double dx,double dy){if(!Bound(t))return;t.cameraPreset.centerX+=dx;t.cameraPreset.centerY+=dy;Refresh(t);}
    public static void Manual(Track t,int frame)
    {
        if(!Bound(t))return;var p=Camera.At(t,frame,new RoomData());t.cameraPreset=null;t.cameraStyle="";t.x=p.x;t.y=p.y;t.angle=p.angle;t.cameraZoom=p.zoom;t.cameraBlur=p.blur;t.cameraBlurAngle=p.blurAngle;t.keyIndex=null;
    }
    private static CameraPresetData Defaults(string name,int start,int end,double x,double y,double zoom,double angle)
    {
        var d=new CameraPresetData{name=name,start=start,end=end,centerX=x,centerY=y,baseZoom=zoom,baseAngle=angle};
        if(name=="变速推拉"){d.zoomAmplitude=.43;d.blurAmplitude=18;}
        else if(name=="甩镜过渡"){d.amplitudeX=800;d.angleAmplitude=2;d.zoomAmplitude=.08;d.blurAmplitude=90;}
        else if(name=="旋转回弹"){d.angleAmplitude=15;d.zoomAmplitude=.22;d.blurAmplitude=8;}
        else if(name=="自然手持漂移"){d.amplitudeX=9;d.amplitudeY=5;d.angleAmplitude=.8;d.zoomAmplitude=.065;}
        else if(name=="柔和摇摆"){d.amplitudeX=14;d.amplitudeY=8;d.angleAmplitude=1.2;d.zoomAmplitude=.06;}
        else if(name=="轻微手持"){d.amplitudeX=5;d.amplitudeY=4;d.angleAmplitude=.6;d.zoomAmplitude=.06;}
        else if(name=="呼吸拉镜")d.zoomAmplitude=.06;
        else if(name=="缓慢推进"||name=="缓慢拉远")d.zoomAmplitude=.25;
        else if(name=="旋转缩放过渡"){d.zoomAmplitude=.4;d.angleAmplitude=8;}
        else if(name=="横向甩镜过渡"){d.amplitudeX=240;d.zoomAmplitude=.15;}
        else if(name=="冲击回弹")d.zoomAmplitude=.2;
        return d;
    }
    public static void Apply(Track t,string name,int frame,int fps)
    {
        if(t.kind!="camera"||!Names.Contains(name))throw new ArgumentException("请选择拉镜预设和拉镜轨道。");
        var p=Motion.At(t,frame);double zoom=Motion.Value(t,"cameraZoom",frame);MotionPresets.Manual(t,frame);
        t.motion="keyframes";t.angularSpeed=0;t.cameraPreset=Defaults(name,t.start,t.end,p.x,p.y,zoom,p.angle);t.cameraStyle=name;t.cameraIntensity=1;t.cameraMirror=StylePresets.Cameras.Contains(name);t.cameraBlur=t.cameraBlurAngle=0;Refresh(t);
    }
    public static bool MigrateLegacy(Track t,int fps)
    {
        if(t.kind!="camera"||t.cameraPreset!=null||MotionPresets.Bound(t)||t.keyframeCycle!=null)return false;
        string name=StylePresets.Cameras.Contains(t.cameraStyle)?t.cameraStyle:t.name;if(!Names.Contains(name)||ControlledFields.Any(p=>t.expressions.ContainsKey(p)))return false;
        if(StylePresets.Cameras.Contains(t.cameraStyle)&&(Math.Abs(t.cameraOriginX-t.x)>.00001||Math.Abs(t.cameraOriginY-t.y)>.00001||Math.Abs(t.cameraOriginZoom-t.cameraZoom)>.00001||Math.Abs(t.cameraOriginAngle-t.angle)>.00001))return false;
        var original=t.keyframes.Where(k=>ControlledFields.Contains(k.property)).OrderBy(k=>k.property).ThenBy(k=>k.frame).ToArray();if(original.Length==0||original.Any(k=>!String.IsNullOrEmpty(k.expression)))return false;
        var probe=t.Snapshot();probe.parent=null;probe.motion="keyframes";probe.angularSpeed=0;probe.cameraStyle="";probe.keyframes.RemoveAll(k=>ControlledFields.Contains(k.property));ApplyLegacy(probe,name,t.start,fps);
        var expected=probe.keyframes.Where(k=>ControlledFields.Contains(k.property)).OrderBy(k=>k.property).ThenBy(k=>k.frame).ToArray();
        if(original.Length!=expected.Length)return false;
        for(int i=0;i<original.Length;i++)if(original[i].property!=expected[i].property||original[i].frame!=expected[i].frame||original[i].easing!=expected[i].easing||Math.Abs(original[i].value-expected[i].value)>.00001)return false;
        t.cameraPreset=Defaults(name,t.start,t.end,t.x,t.y,t.cameraZoom,t.angle);t.cameraPreset.legacySampling=true;t.cameraPreset.legacyStart=t.start;t.cameraPreset.legacyLength=Math.Max(1,t.end-t.start);t.cameraPreset.legacyFps=Math.Max(1,fps);t.cameraStyle=name;Refresh(t);return true;
    }
    private static CameraPose Direct(CameraPresetData d,double frame,int fps,double? progress=null)
    {
        double u=progress??Math.Max(0,Math.Min(1,(frame-d.start)*d.speed/Math.Max(1.0,d.end-d.start))),sec=u*Math.Max(1,d.end-d.start)/Math.Max(1.0,fps),z=d.baseZoom,x=d.centerX,y=d.centerY,a=d.baseAngle,blur=0,blurAngle=0;
        if(d.name=="变速推拉"){
            double k=0;if(u<.1)k=-.035*Motion.Ease("smooth",u/.1);else if(u<.21)k=-.035+.43*Motion.Ease("expo-out",(u-.1)/.11);else if(u<.8)k=.395*(1-Motion.Ease("smooth",(u-.21)/.59));z*=1+d.zoomAmplitude*k/.43;blur=d.blurAmplitude*Math.Max(0,1-Math.Abs(u-.16)/.08);
        }else if(d.name=="甩镜过渡"){
            double v=u<.5?Motion.Ease("expo-in",u*2):-(1-Motion.Ease("expo-out",(u-.5)*2));x+=d.amplitudeX*v;a+=d.angleAmplitude*v;z*=1+d.zoomAmplitude*Math.Sin(Math.PI*u);blur=d.blurAmplitude*Math.Pow(Math.Sin(Math.PI*u),4);
        }else if(d.name=="旋转回弹"){
            double s=u*1.6;a+=d.angleAmplitude*Math.Exp(-4.8*s)*Math.Sin(18*s);z*=1+d.zoomAmplitude*Math.Exp(-4*s)*Math.Sin(10*s);blur=d.blurAmplitude*Math.Exp(-10*s);blurAngle=45;
        }else if(d.name=="自然手持漂移"){
            double env=Motion.Ease("sin-inout",Math.Min(1,Math.Min(u/.08,(1-u)/.08)));x+=env*d.amplitudeX*(Math.Sin(sec*1.6)+Math.Sin(sec*4.1+.6)/3+Math.Sin(sec*8.7)/6);y+=env*d.amplitudeY*(Math.Sin(sec*1.1+1.4)+.4*Math.Sin(sec*3.7)+.12*Math.Sin(sec*10.2));a+=env*d.angleAmplitude*(Math.Sin(sec*1.3)+.225*Math.Sin(sec*4.6+1.3));z*=1+env*d.zoomAmplitude*(1+(.006/.065)*Math.Sin(sec*1.2));
        }else if(d.name=="柔和摇摆"||d.name=="轻微手持"){
            double env=Math.Sin(Math.PI*u),phase=sec*2*Math.PI/3;bool hand=d.name=="轻微手持";x+=env*d.amplitudeX*(hand?Math.Sin(1.7*phase)+.6*Math.Sin(4.3*phase):Math.Sin(phase));y+=env*d.amplitudeY*(hand?Math.Cos(2.3*phase)+.5*Math.Sin(5.1*phase):Math.Sin(1.3*phase));a+=env*d.angleAmplitude*Math.Sin((hand?2.1:.8)*phase);z*=1+d.zoomAmplitude*env;
        }else if(d.name=="呼吸拉镜")z*=1+d.zoomAmplitude*(1-Math.Cos(4*Math.PI*u))/2;
        else if(d.name=="缓慢推进"||d.name=="缓慢拉远"){double w=Motion.Ease("sin-inout",u);z*=1+d.zoomAmplitude*(d.name=="缓慢推进"?w:1-w);}
        else if(d.name=="旋转缩放过渡"){
            if(u<.4){double w=Motion.Ease("sin-inout",u/.4);z*=1+d.zoomAmplitude*w;a-=d.angleAmplitude*w;}
            else if(u<.6){double w=Motion.Ease("sin-inout",(u-.4)/.2);z*=1+d.zoomAmplitude;a+=d.angleAmplitude*(2*w-1);}
            else{double w=Motion.Ease("sin-inout",(u-.6)/.4);z*=1+d.zoomAmplitude*(1-w);a+=d.angleAmplitude*(1-w);}
        }else if(d.name=="横向甩镜过渡"){
            if(u<.45)x+=d.amplitudeX*Motion.Ease("expo-in",u/.45);else if(u<.55)x+=d.amplitudeX;else x-=d.amplitudeX*(1-Motion.Ease("expo-out",(u-.55)/.45));z*=1+d.zoomAmplitude*(u<.5?Motion.Ease("sin-inout",u*2):1-Motion.Ease("sin-inout",(u-.5)*2));
        }else if(d.name=="冲击回弹")z*=1+d.zoomAmplitude*(u<.2?Motion.Ease("expo-out",u/.2):1-Motion.Ease("expsin",(u-.2)/.8));
        if(u>=1&&d.name=="横向甩镜过渡")x=d.centerX;
        if(u>=1&&(d.name=="冲击回弹"||d.name=="旋转缩放过渡")){z=d.baseZoom;if(d.name=="旋转缩放过渡")a=d.baseAngle;}
        if(StylePresets.Cameras.Contains(d.name)&&(u<=0||u>=1)){x=d.centerX;y=d.centerY;a=d.baseAngle;z=d.baseZoom;blur=0;}
        return new CameraPose{x=x,y=y,angle=a,zoom=z,blur=blur,blurAngle=blurAngle};
    }
    private static Dictionary<string,Keyframe[]> LegacyCurves(CameraPresetData d,int fps)
    {
        var data=new Dictionary<string,Keyframe>();var copy=d.Snapshot();copy.legacySampling=false;copy.speed=1;int length=d.legacyLength>0?d.legacyLength:Math.Max(1,d.end-d.start),sourceFps=d.legacyFps>0?d.legacyFps:fps,sourceStart=d.legacyLength>0?d.legacyStart:d.start;copy.start=sourceStart;copy.end=sourceStart+length;
        Action<string,double,string> add=(field,u,easing)=>{var p=Direct(copy,0,sourceFps,u);double v=field=="x"?p.x:field=="y"?p.y:field=="angle"?p.angle:field=="cameraZoom"?p.zoom:field=="cameraBlur"?p.blur:p.blurAngle;int frame=sourceStart+(int)Math.Round(length*u);foreach(var step in d.legacyTime??new List<CameraTimeTransform>())frame=step.origin+BulletSystem.Round((frame-step.subtract)*step.scale);data[field+":"+frame]=new Keyframe{property=field,frame=frame,value=v,easing=easing};};
        if(StylePresets.Cameras.Contains(d.name)){int count=Math.Min(240,length);for(int i=0;i<=count;i++)foreach(string field in ControlledFields)add(field,i/(double)count,"linear");}
        else if(d.name=="缓慢推进"||d.name=="缓慢拉远"){add("cameraZoom",0,"sin-inout");add("cameraZoom",1,"linear");}
        else if(d.name=="旋转缩放过渡"){foreach(double u in new[]{0,.4,.6,1}){add("cameraZoom",u,"sin-inout");add("angle",u,"sin-inout");}}
        else if(d.name=="横向甩镜过渡"){add("x",0,"expo-in");add("x",.45,"step");add("x",.55,"expo-out");add("x",1,"linear");add("cameraZoom",0,"sin-inout");add("cameraZoom",.5,"sin-inout");add("cameraZoom",1,"linear");}
        else if(d.name=="冲击回弹"){add("cameraZoom",0,"expo-out");add("cameraZoom",.2,"expsin");add("cameraZoom",1,"linear");}
        else {int count=Math.Max(8,Math.Min(160,length/Math.Max(1,sourceFps/10)));for(int i=0;i<=count;i++){double u=i/(double)count;if(d.name=="呼吸拉镜")add("cameraZoom",u,"linear");else foreach(string field in new[]{"x","y","angle","cameraZoom"})add(field,u,"sin-inout");}}
        return data.Values.GroupBy(k=>k.property).ToDictionary(g=>g.Key,g=>g.OrderBy(k=>k.frame).ToArray());
    }
    public static CameraPose Sample(CameraPresetData d,double frame,int fps)
    {
        if(!d.legacySampling)return Direct(d,frame,fps);if(d.samples==null||d.sampleFps!=fps){d.samples=LegacyCurves(d,fps);d.sampleFps=fps;}
        double at=d.start+(frame-d.start)*d.speed;
        Func<string,double,double> value=(field,fallback)=>{Keyframe[] keys;if(!d.samples.TryGetValue(field,out keys)||keys.Length==0)return fallback;int lo=0,hi=keys.Length;while(lo<hi){int mid=(lo+hi)/2;if(keys[mid].frame<=at)lo=mid+1;else hi=mid;}if(lo==0)return keys[0].value;if(lo>=keys.Length)return keys[keys.Length-1].value;var a=keys[lo-1];var b=keys[lo];double u=Motion.Ease(a.easing,(at-a.frame)/Math.Max(1,b.frame-a.frame));return a.value+(b.value-a.value)*u;};
        return new CameraPose{x=value("x",d.centerX),y=value("y",d.centerY),angle=value("angle",d.baseAngle),zoom=value("cameraZoom",d.baseZoom),blur=value("cameraBlur",0),blurAngle=value("cameraBlurAngle",0)};
    }
    public static double Value(Track t,string field,double frame)
    {var p=Sample(t.cameraPreset,frame,t.expressionFps);switch(field){case "x":return p.x;case "y":return p.y;case "angle":return p.angle;case "cameraZoom":return p.zoom;case "cameraBlur":return p.blur;case "cameraBlurAngle":return p.blurAngle;default:return 0;}}
}

internal sealed partial class MainForm
{
    private void CameraPresetControls(NativeProperties bag,Track t)
    {
        if(!CameraPresets.Bound(t)){if((StylePresets.Cameras.Contains(t.cameraStyle)||CameraPresets.Names.Contains(t.name))&&t.keyframes.Any(k=>CameraPresets.ControlledFields.Contains(k.property)))bag.Add("旧拉镜曲线","拉镜预设参数",typeof(string),()=>Language.Text("旧曲线未自动转换；重新应用预设可使用参数控制。"),null,null,"旧曲线保留原有关键帧。重新应用预设会替换当前曲线。");return;}var d=t.cameraPreset;
        Field(bag,"拉镜预设","拉镜预设参数",()=>t.cameraPreset.name,value=>{if(value=="手动关键帧")CameraPresets.Manual(t,frame);else CameraPresets.Apply(t,value,frame,state.fps);QueueInspectorUpdate();},new[]{"手动关键帧"}.Concat(CameraPresets.Names).ToArray());
        bag.Add("拉镜预设控制","拉镜预设参数",typeof(string),()=>Language.Text("由预设控制；切换手动时保留当前画面。"),null);
        bag.Add("拉镜预设持续时间 / 帧","拉镜预设参数",typeof(int),()=>Math.Max(1,t.cameraPreset.end-t.cameraPreset.start),value=>{PushUndo();CameraPresets.SetDuration(t,(int)value);MarkChanged();});
        Action<string,string> number=(label,field)=>bag.Add(label,"拉镜预设参数",typeof(double),()=>typeof(CameraPresetData).GetField(field).GetValue(t.cameraPreset),value=>{PushUndo();double v=(double)value;if(field=="speed")v=Math.Max(.01,Math.Min(100,v));else if(field=="baseZoom")v=Math.Max(.05,Math.Min(20,v));typeof(CameraPresetData).GetField(field).SetValue(t.cameraPreset,v);CameraPresets.Refresh(t);MarkChanged();});
        number("预设中心 X","centerX");number("预设中心 Y","centerY");number("基础缩放","baseZoom");number("基础角度","baseAngle");number("预设速度倍率","speed");
        if(new[]{"甩镜过渡","自然手持漂移","柔和摇摆","轻微手持","横向甩镜过渡"}.Contains(d.name))number("拉镜水平振幅","amplitudeX");
        if(new[]{"自然手持漂移","柔和摇摆","轻微手持"}.Contains(d.name))number("拉镜垂直振幅","amplitudeY");
        if(new[]{"甩镜过渡","旋转回弹","自然手持漂移","柔和摇摆","轻微手持","旋转缩放过渡"}.Contains(d.name))number("旋转幅度","angleAmplitude");
        number("缩放幅度","zoomAmplitude");if(new[]{"变速推拉","甩镜过渡","旋转回弹"}.Contains(d.name))number("模糊幅度","blurAmplitude");
    }
}
