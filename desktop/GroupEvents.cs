using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

internal sealed partial class MainForm
{
    private void EventNumber(NativeProperties bag,Track t,string property,string name,string category)
    {
        bool motion=(t.eventPreset=="reset-motion"||t.eventPreset=="group-set")&&(property=="controlValue"||property=="controlFrom");
        var controls=ParameterControls(new[]{t},property,false);controls.Property=property=="controlValue"?"speed":property=="eventAcceleration"?"accelerationX":property=="eventTurn"?"angularSpeed":"angle";
        if(motion){
            var assign=controls.SetExpression;controls.SetExpression=text=>{assign(text);t.eventRestoreBirth=false;MarkChanged();};
            Func<Action,Action> wrap=original=>()=>{var before=Json.Write(t.expressions);double value=Motion.Base(t,property);original();if(before!=Json.Write(t.expressions)||value!=Motion.Base(t,property)){t.eventRestoreBirth=false;MarkChanged();}};
            controls.EditExpression=wrap(controls.EditExpression);controls.ToggleRandom=wrap(controls.ToggleRandom);
        }
        bag.Add(name,category,typeof(double),()=>Motion.Value(t,property,frame),value=>{PushUndo();t.expressions.Remove(property);Motion.SetBase(t,property,(double)value);if(motion)t.eventRestoreBirth=false;MarkChanged();},null,motion?"每颗弹幕独立计算；修改数值或公式会自动关闭恢复出生速度与方向。":"每颗弹幕独立计算；支持 random、random_range、choose、player.x/y。",controls);
    }
    private void GroupEventFields(NativeProperties bag,Track t)
    {
        var groups=state.timelineGroups.ToArray();var names=new[]{Language.Text("事件所在轨道的组")}.Concat(groups.Select(g=>g.name+" · "+g.id.Substring(0,Math.Min(6,g.id.Length)))).ToArray();bag.Add("目标组","组事件",typeof(string),()=>names[Math.Max(0,Array.FindIndex(groups,g=>g.id==t.eventGroup)+1)],value=>{int i=Array.IndexOf(names,(string)value)-1;PushUndo();t.eventGroup=i<0?"":groups[i].id;MarkChanged();QueueInspectorUpdate();},names,localizeChoices:false);
        bag.Add("当前作用组","组事件",typeof(string),()=>{var g=groups.FirstOrDefault(x=>x.id==EventPresets.GroupId(t));return g==null?Language.Text("未归组：请右键轨道设置所属组。"):g.name;},null);
        if(t.eventPreset=="group-copy"){
            bag.Add("说明","组事件",typeof(string),()=>"仅在开始帧复制一遍，保留当时位置、贴图、颜色、大小。整体对象只复制已显示的成员，不复制生成器或后续行为。",null);EventNumber(bag,t,"controlValue","副本速度","副本运动");EventNumber(bag,t,"controlFrom","副本方向 / 度","副本运动");EventNumber(bag,t,"eventAcceleration","副本加速度 / 每帧","副本运动");EventNumber(bag,t,"eventTurn","副本转向 / 度每帧","副本运动");Field(bag,"是否销毁原弹幕","组事件",()=>t.eventCopyDestroyOriginal,v=>t.eventCopyDestroyOriginal=v,null,"先复制当前弹幕，再立即移除对应原弹幕；保留发射器、未来子弹和未参与复制的物件。");LifetimeModeField(bag,t,"副本寿命","副本寿命模式");if(BulletLifetime.Mode(t)=="fixed")Field(bag,"副本寿命 / 帧","副本寿命",()=>t.eventCopyLife,v=>t.eventCopyLife=Math.Max(1,v));else Field(bag,"出界余量 / 像素","副本寿命",()=>t.despawnMargin,v=>t.despawnMargin=v);Field(bag,"副本使用来源拖影","副本运动",()=>t.eventCopyTrail,v=>t.eventCopyTrail=v);
        }else if(t.eventPreset=="group-set"){
            bag.Add("说明","组事件",typeof(string),()=>"重设本组弹幕及整体对象（多边形、转转棒等）的速度、方向、加速度、转向与寿命；整体对象立即销毁并顺从重设运动。",null);
            Field(bag,"恢复出生速度与方向","运动重设",()=>t.eventRestoreBirth,v=>{t.eventRestoreBirth=v;QueueInspectorUpdate();});
            {
                EventNumber(bag,t,"controlValue","重设速度","运动重设");
                EventNumber(bag,t,"controlFrom","重设方向 / 度","运动重设");
            }
            EventNumber(bag,t,"eventAcceleration","重设加速度 / 每帧","运动重设");
            EventNumber(bag,t,"eventTurn","重设转向 / 度每帧","运动重设");
            Field(bag,"停止后续运动行为","运动重设",()=>t.eventDisableMotion,v=>t.eventDisableMotion=v);
            Field(bag,"重设弹幕寿命","寿命重设",()=>t.eventResetLife,v=>{t.eventResetLife=v;QueueInspectorUpdate();});
            if(t.eventResetLife)bag.Add("重设寿命 / 帧","寿命重设",typeof(int),()=>t.eventLife,v=>t.eventLife=Math.Max(0,(int)v),null,"大于0设为指定寿命；设为0时保留或恢复该弹幕原属性寿命。");
            Field(bag,"额外原生弹幕对象","原生对象",()=>t.eventNativeObjects,v=>{var probe=t.Snapshot();probe.eventNativeObjects=v;var objectNames=EventPresets.NativeObjects(probe);foreach(string name in objectNames)if(!document.inventory.objects.Contains(name)||name.StartsWith("objRuf",StringComparison.Ordinal))throw new ArgumentException("请填写工程中的原生弹幕对象名，多个用逗号分隔。");t.eventNativeObjects=String.Join(",",objectNames);QueueInspectorUpdate();},null,"留空仅作用于RufMaker弹幕。填写原生子弹类对象名，包含其子类；不自动猜测玩家、地形或控制器。");
            if(EventPresets.NativeObjects(t).Length>0)bag.Add("原生限制","原生对象",typeof(string),()=>"原生对象在F5/导出时执行；底片录像不随事件改变。运动重设使用自定义值，只重设GM内建运动，原生代码仍可再次改写。",null);
        }else bag.Add("说明","组事件",typeof(string),()=>"清除本组飞行子弹与当前整体成员，保留发射器和之后出场的物件。拉长事件可持续清除。",null);
    }
}
internal sealed partial class BulletWorld
{
    private readonly Dictionary<Track,Dictionary<int,Tuple<Keyframe,PointF[]>>> fixedGeometry=new Dictionary<Track,Dictionary<int,Tuple<Keyframe,PointF[]>>>();
    internal bool HasFormationProperties {get{return eventTracks.Any(e=>e.eventPreset=="group-set"&&new[]{"scale","alpha","color"}.Contains(e.eventProperty));}}
    private Tuple<Keyframe,PointF[]> FixedGeometry(Track t,int at){Dictionary<int,Tuple<Keyframe,PointF[]>> times;Tuple<Keyframe,PointF[]> result;if(!fixedGeometry.TryGetValue(t,out times)){times=new Dictionary<int,Tuple<Keyframe,PointF[]>>();fixedGeometry.Add(t,times);}if(!times.TryGetValue(at,out result)){result=Tuple.Create(Motion.At(t,at),Motion.RodPoints(t,at));times.Add(at,result);}return result;}
    internal SimBullet FixedMember(Track t,int index,int at)
    {
        if(!BulletSystem.Active(t,at)||t.kind!="rod"||t.releaseFrame>=0&&at>=t.releaseFrame||!Templates.Visible(t,index))return null;var geometry=FixedGeometry(t,at);var pose=geometry.Item1;var points=geometry.Item2;if(index>=points.Length)return null;var p=points[index];var b=new SimBullet{source=t,scopeGroup=TimelineGroups.Id(t),kinematic=true,x=p.X,y=p.Y,sourceX=pose.x,sourceY=pose.y,birth=t.start,index=index,count=points.Length,scale=pose.scale,alpha=pose.alpha,color=t.rainbow?Color.White:Motion.ColorOf(t.color),sequence=Rainbow.RodSequence(t,index,points.Length,at,p,pose),visualAngle=Motion.Value(t,"spriteAngle",at)+(t.rotateSprite?pose.angle:0)};FixedProperties(b,at);return b;
    }
    internal void FixedProperties(SimBullet b,int at)
    {
        if(b.source.kind!="rod")return;
        foreach(var e in eventTracks.Where(e=>e.enabled&&e.eventPreset=="group-set"&&e.start<=at&&e.end>=b.birth&&EventPresets.Matches(e,b)&&new[]{"scale","alpha","color"}.Contains(e.eventProperty)).OrderBy(e=>Math.Min(at,e.end))){int sample=Math.Min(at,e.end);var target=b; // Fixed formations keep their source motion, but appearance events persist.
            if(b.templateOwner==0){var geometry=FixedGeometry(b.source,sample);var pose=geometry.Item1;var points=geometry.Item2;if(b.index>=points.Length)continue;target=b.Clone();target.x=points[b.index].X;target.y=points[b.index].Y;}else {var p=Templates.SampleMember(b,sample);target=b.Clone();target.x=p.X;target.y=p.Y;}
            EventPresets.Set(b,e.eventProperty,EventPresets.Value(e,"controlValue",target,this,sample));}
    }
    private IEnumerable<SimBullet> GroupMembers(string group,int frame)
    {
        foreach(var b in bullets.Where(b=>!b.dead&&b.birth<=frame&&b.scopeGroup==group&&b.source.kind!="object"))yield return b;
        foreach(var t in State.tracks.Where(t=>t.kind=="rod"&&TimelineGroups.Id(t)==group&&BulletSystem.Active(t,frame))){int n=Motion.RodPoints(t,frame).Length;for(int i=0;i<n;i++){var b=FixedMember(t,i,frame);if(b!=null)yield return b;}}
    }
    private void ApplyGroupEvents(int frame)
    {
        foreach(var e in eventTracks.Where(e=>EventPresets.GroupEvent(e)&&EventPresets.Active(e,frame))){string group=EventPresets.GroupId(e);if(String.IsNullOrEmpty(group))continue;
            if(e.eventPreset=="group-clear"){foreach(var b in bullets.Where(b=>b.birth<=frame&&b.scopeGroup==group&&b.source.kind!="object"))b.dead=true;Templates.ClearGroup(frame,group);continue;}
            if(e.eventPreset!="group-copy"||frame!=e.start)continue;var members=GroupMembers(group,frame).ToArray();foreach(var original in members){var source=original.source.Snapshot();source.owner=State;source.scopeGroup=group;source.rotateSprite=false;source.spriteAngle=BulletAngle(original,original.direction);source.keyframes.RemoveAll(k=>k.property=="spriteAngle");source.expressions.Remove("spriteAngle");source.lifetimeMode=BulletLifetime.Mode(e);source.despawnMargin=e.despawnMargin;source.deathEffect="none";source.fadeInFrames=e.fadeInFrames;source.life=e.eventCopyLife;if(!e.eventCopyTrail)Trails.SetMode(source,"off");var b=new SimBullet{scopeGroup=group,source=source,behavior=null,birth=frame,index=original.index,count=original.count,volley=original.volley,generation=original.generation+1,x=original.x,y=original.y,sourceX=original.sourceX,sourceY=original.sourceY,scale=original.scale,alpha=original.alpha,color=original.color,sprite=original.sprite??source.sprite,frozenImage=original.frozenImage>=0?original.frozenImage:Rainbow.Index(source,original.sequence,frame-original.clockShift,(original.kinematic?frame:original.birth)-original.clockShift),life=e.eventCopyLife,motionReset=true,collision=original.collision};
                b.speed=EventPresets.Value(e,"controlValue",original,this,frame);b.direction=EventPresets.Value(e,"controlFrom",original,this,frame);b.acceleration=EventPresets.Value(e,"eventAcceleration",original,this,frame);b.turn=EventPresets.Value(e,"eventTurn",original,this,frame);b.baseSpeed=b.speed;b.baseDirection=b.direction;b.baseScale=b.scale;b.baseAlpha=b.alpha;if(BulletLifetime.Update(b,0,lifetimeRoom,BulletLifetime.Mode(source)=="view"?LifetimeView():new LifetimeBounds())){b.history.Add(b.Sample(frame));b.lastUpdate=frame;bullets.Add(b);}if(e.eventCopyDestroyOriginal)Templates.DestroyMember(original);}
        }
    }
}
