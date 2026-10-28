using System;
using System.Linq;

internal static class EventPresets
{
    public static readonly string[] Keys={"clear-bullets","clear-danmaku","stop-bullets","reset-motion","group-copy","group-clear","group-set"};
    public static readonly string[] Names={"清除全屏子弹","清除全屏所有弹幕","所有子弹速度归零","重设所有子弹运动属性","复制组内所有弹幕一遍","清除组内所有弹幕","设置组内所有弹幕属性"};
    public static readonly string[] MotionActions={"speed","direction","turn","acceleration","aim","home"};
    public static bool IsEvent(Track t){return t!=null&&t.kind=="control"&&Keys.Contains(t.eventPreset);}
    public static bool Active(Track t,int frame){return IsEvent(t)&&BulletSystem.Active(t,frame);}
    public static void ApplyPreset(Track t,string key){if(!Keys.Contains(key))throw new ArgumentException("未知事件预设");t.kind="control";t.controlAction="event";t.eventPreset=key;t.name=Names[Array.IndexOf(Keys,key)];t.controlValue=key=="group-copy"?0:3;t.controlFrom=0;t.eventAcceleration=0;t.eventTurn=0;t.eventRestoreBirth=true;t.eventDisableMotion=true;t.end=t.start;}
    public static bool GroupEvent(Track t){return t!=null&&(t.eventPreset??"").StartsWith("group-",StringComparison.Ordinal);}
    public static string GroupId(Track e){return String.IsNullOrEmpty(e.eventGroup)?TimelineGroups.Id(e):e.eventGroup;}
    public static bool Matches(Track e,SimBullet b){return !GroupEvent(e)||!String.IsNullOrEmpty(GroupId(e))&&GroupId(e)==b.scopeGroup;}
    public static double Value(Track e,string property,SimBullet b,BulletWorld world,int frame)
    {string text;double value=Motion.Base(e,property);if(!e.expressions.TryGetValue(property,out text)||String.IsNullOrWhiteSpace(text))return value;var c=world.Context(b,frame);c.frame=frame;c.randomMix=true;c.randomOffset=b.birth*104729.0+b.index*9176.0+b.volley*1009.0+b.generation*13007.0;return RandomExpression.Evaluate(text,b.source.expressionSeed,(b.source.randomId??b.source.id)+"|event|"+e.id+"|"+property+"|base",c);}
    public static bool Apply(Track e,SimBullet b,BulletWorld world=null,int frame=0)
    {
        if(e.eventPreset=="clear-bullets"||e.eventPreset=="clear-danmaku"){b.dead=true;return false;}
        if(e.eventPreset=="stop-bullets")b.speed=0;
        if(e.eventPreset=="reset-motion"){b.speed=e.eventRestoreBirth?b.baseSpeed:Value(e,"controlValue",b,world,frame);b.direction=e.eventRestoreBirth?b.baseDirection:Value(e,"controlFrom",b,world,frame);b.acceleration=Value(e,"eventAcceleration",b,world,frame);b.turn=Value(e,"eventTurn",b,world,frame);b.motionReset=e.eventDisableMotion;}
        if(e.eventPreset=="group-set")Set(b,e.eventProperty,Value(e,"controlValue",b,world,frame));
        return true;
    }
    public static readonly string[] Properties={"speed","direction","acceleration","turn","scale","alpha","color"};
    public static readonly string[] PropertyNames={"速度","方向","加速度","每帧转向","大小","不透明度","颜色（色相0–360）"};
    public static void Set(SimBullet b,string property,double value){switch(property){case "speed":b.speed=value;break;case "direction":b.direction=value;break;case "acceleration":b.acceleration=value;break;case "turn":b.turn=value;break;case "scale":b.scale=Math.Max(0,value);break;case "alpha":b.alpha=Math.Max(0,Math.Min(1,value));break;case "color":b.color=SpawnColors.Hue(value);break;}}
    public static string[] NativeObjects(Track t){return (t.eventNativeObjects??"").Split(new[]{',','，',';','；','\r','\n'},StringSplitOptions.RemoveEmptyEntries).Select(s=>s.Trim()).Where(s=>s.Length>0).Distinct().ToArray();}
    public static void Scale(Track t,double ratio){if(!IsEvent(t))return;double divisor=t.eventPreset=="group-set"?(t.eventProperty=="speed"||t.eventProperty=="turn"?ratio:t.eventProperty=="acceleration"?ratio*ratio:1):ratio;t.controlValue/=divisor;t.eventAcceleration/=ratio*ratio;t.eventTurn/=ratio;t.eventCopyLife=Math.Max(1,BulletSystem.Round(t.eventCopyLife*ratio));ScalarExpressions.ScaleBinding(t.expressions,"controlValue",1/divisor);ScalarExpressions.ScaleBinding(t.expressions,"eventAcceleration",1/(ratio*ratio));ScalarExpressions.ScaleBinding(t.expressions,"eventTurn",1/ratio);}
}

internal sealed partial class MainForm
{
    private void AddEventPreset(string key,int at)
    {AddTrack("control",t=>{t.start=Math.Max(0,Math.Min(state.duration,at));EventPresets.ApplyPreset(t,key);});SetStatus("事件已放到时间轴；默认只在触发帧执行，可拉长为持续执行。");}
    private void EventFields(NativeProperties bag,Track t)
    {
        Field(bag,"事件类型","事件",()=>EventPresets.Names[Array.IndexOf(EventPresets.Keys,t.eventPreset)],value=>{t.eventPreset=EventPresets.Keys[Array.IndexOf(EventPresets.Names,value)];t.name=value;QueueInspectorUpdate();},EventPresets.Names);
        if(EventPresets.GroupEvent(t)){GroupEventFields(bag,t);return;}
        bag.Add("执行范围","事件",typeof(string),()=>t.start==t.end?"仅触发帧已有弹幕":"时段内每帧执行，包含新生弹幕",null);
        bag.Add("说明","事件",typeof(string),()=>t.eventPreset=="clear-bullets"?"立即清除飞行子弹，保留未释放的整体对象；之后发射的新子弹照常出现。":t.eventPreset=="clear-danmaku"?"立即清除飞行子弹、当前整体对象和可视对象。发射器保留，未来物件仍可出场。":t.eventPreset=="stop-bullets"?"只把当前弹速设为0；后续加速和行为仍可生效。需要取消后续运动时使用重设运动事件。":"重设速度、方向、加速度与持续转向；可停止后续运动行为，保留外观、寿命和生成行为。",null);
        if(t.eventPreset=="reset-motion"){
            Field(bag,"恢复出生速度与方向","运动重设",()=>t.eventRestoreBirth,v=>{t.eventRestoreBirth=v;QueueInspectorUpdate();});
            if(!t.eventRestoreBirth||EventPresets.NativeObjects(t).Length>0){EventNumber(bag,t,"controlValue","重设速度","运动重设");EventNumber(bag,t,"controlFrom","重设方向 / 度","运动重设");}
            EventNumber(bag,t,"eventAcceleration","重设加速度 / 每帧","运动重设");EventNumber(bag,t,"eventTurn","重设转向 / 度每帧","运动重设");Field(bag,"停止后续运动行为","运动重设",()=>t.eventDisableMotion,v=>t.eventDisableMotion=v);
        }
        Field(bag,"额外原生弹幕对象","原生对象",()=>t.eventNativeObjects,v=>{var probe=t.Snapshot();probe.eventNativeObjects=v;var names=EventPresets.NativeObjects(probe);foreach(string name in names)if(!document.inventory.objects.Contains(name)||name.StartsWith("objRuf",StringComparison.Ordinal))throw new ArgumentException("请填写工程中的原生弹幕对象名，多个用逗号分隔。");t.eventNativeObjects=String.Join(",",names);QueueInspectorUpdate();},null,"留空仅作用于RufMaker弹幕。填写原生子弹类对象名，包含其子类；不自动猜测玩家、地形或控制器。");
        if(EventPresets.NativeObjects(t).Length>0)bag.Add("原生限制","原生对象",typeof(string),()=>"原生对象在F5/导出时执行；底片录像不随事件改变。运动重设使用自定义值，只重设GM内建运动，原生代码仍可再次改写。",null);
    }
}
