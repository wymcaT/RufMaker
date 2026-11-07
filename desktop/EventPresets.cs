using System;
using System.Linq;
using System.Drawing;

internal static class EventPresets
{
    public static readonly string[] Keys={"clear-bullets","clear-danmaku","stop-bullets","reset-motion","orbit-bullets","group-copy","group-clear","group-set","group-orbit","player-move","player-set"};
    public static readonly string[] Names={"清除全屏子弹","清除全屏所有弹幕","所有子弹速度归零","重设所有弹幕属性","所有弹幕绕点旋转","复制组内所有弹幕一遍","清除组内所有弹幕","重设组内所有弹幕属性","组内弹幕绕点旋转","强制移动玩家位置","设置玩家参数"};
    public static readonly string[] Categories={"弹幕清除与重设","组事件","玩家控制"};
    public static string CategoryOf(string key){return key.StartsWith("group-",StringComparison.Ordinal)?"组事件":key.StartsWith("player-",StringComparison.Ordinal)?"玩家控制":"弹幕清除与重设";}
    public static string[] KeysInCategory(string category){return Keys.Where(k=>CategoryOf(k)==category).ToArray();}
    public static readonly string[] MotionActions={"speed","direction","turn","acceleration","aim","home"};
    public static bool IsEvent(Track t){return t!=null&&t.kind=="control"&&Keys.Contains(t.eventPreset);}
    public static bool Active(Track t,int frame){return IsEvent(t)&&BulletSystem.Active(t,frame);}
    public static NativeTestPosition PlayerTarget(EditorState state,Track track,int frame)
    {
        int at=Math.Max(track.start,Math.Min(track.end,frame));
        var player=at==0?(state.nativeTest??new NativeTestPosition()):BulletSystem.PlayerAt(state,at-1);
        var context=ExpressionContext.For(track,at);
        context.x=context.sourceX=context.playerX=player.x;context.y=context.sourceY=context.playerY=player.y;
        Func<string,double> value=property=>{
            string text;
            return track.expressions.TryGetValue(property,out text)&&!String.IsNullOrWhiteSpace(text)
                ?RandomExpression.Evaluate(text,state.randomSeed,(track.randomId??track.id)+"|event|"+track.id+"|"+property+"|base",context)
                :Motion.Base(track,property);
        };
        double x=value("playerTargetX");context.x=context.playerX=context.sourceX=x;
        return new NativeTestPosition{x=x,y=value("playerTargetY")};
    }
    public static void ApplyPreset(Track t,string key){if(!Keys.Contains(key))throw new ArgumentException("未知事件预设");t.kind="control";t.controlAction="event";t.eventPreset=key;t.name=Names[Array.IndexOf(Keys,key)];t.controlValue=key=="group-copy"?0:3;t.controlFrom=0;t.eventAcceleration=0;t.eventFriction=0;t.eventTurn=0;t.eventCenterX=400;t.eventCenterY=304;t.eventOrbitSpeed=1.5;t.eventRadius=120;t.eventRadiusMode="keep";t.eventRestoreBirth=key!="reset-motion"&&key!="group-set"&&key!="orbit-bullets"&&key!="group-orbit";t.eventDisableMotion=true;t.eventResetLife=true;t.eventLife=160;t.playerTargetX=400;t.playerTargetY=304;t.playerKeepMotion=false;t.playerResetJump=true;t.playerInfJump=false;t.playerInvincible=false;t.playerCanMove=true;t.end=t.start;}
    public static bool GroupEvent(Track t){return t!=null&&(t.eventPreset??"").StartsWith("group-",StringComparison.Ordinal);}
    public static string GroupId(Track e){return String.IsNullOrEmpty(e.eventGroup)?TimelineGroups.Id(e):e.eventGroup;}
    public static bool Matches(Track e,SimBullet b){return !GroupEvent(e)||!String.IsNullOrEmpty(GroupId(e))&&GroupId(e)==b.scopeGroup;}
    public static double Value(Track e,string property,SimBullet b,BulletWorld world,int frame)
    {string text;double value=Motion.Base(e,property);if(!e.expressions.TryGetValue(property,out text)||String.IsNullOrWhiteSpace(text))return value;var c=world.Context(b,frame);c.frame=frame;c.randomMix=true;c.randomOffset=b.birth*104729.0+b.index*9176.0+b.volley*1009.0+b.generation*13007.0;return RandomExpression.Evaluate(text,b.source.expressionSeed,(b.source.randomId??b.source.id)+"|event|"+e.id+"|"+property+"|base",c);}
    public static bool Apply(Track e,SimBullet b,BulletWorld world=null,int frame=0)
    {
        if(e.eventPreset=="clear-bullets"||e.eventPreset=="clear-danmaku"){b.dead=true;return false;}
        if(e.eventPreset=="stop-bullets")b.speed=0;
        if(e.eventPreset=="reset-motion"||e.eventPreset=="group-set"){b.orbiting=false;b.speed=e.eventRestoreBirth?b.baseSpeed:Value(e,"controlValue",b,world,frame);b.direction=e.eventRestoreBirth?b.baseDirection:Value(e,"controlFrom",b,world,frame);b.acceleration=Value(e,"eventAcceleration",b,world,frame);b.friction=Value(e,"eventFriction",b,world,frame);b.turn=Value(e,"eventTurn",b,world,frame);b.motionReset=e.eventDisableMotion;if(e.eventResetLife){if(!b.commands.Contains("reset-life|"+e.id)){b.commands.Add("reset-life|"+e.id);b.birth=frame;if(e.eventLife>0)b.life=e.eventLife;else if(b.source!=null&&b.source.life>0)b.life=b.source.life;}}}
        if(e.eventPreset=="orbit-bullets"||e.eventPreset=="group-orbit"){double cx=Value(e,"eventCenterX",b,world,frame),cy=Value(e,"eventCenterY",b,world,frame),spd=Value(e,"eventOrbitSpeed",b,world,frame);double dx=b.x-cx,dy=b.y-cy,r=Math.Sqrt(dx*dx+dy*dy);if(e.eventRadiusMode=="fixed")r=Value(e,"eventRadius",b,world,frame);double angle=Math.Atan2(-dy,dx)*180/Math.PI;b.orbiting=true;b.orbitCenterX=cx;b.orbitCenterY=cy;b.orbitRadius=r;b.orbitAngle=angle;b.orbitSpeed=spd;b.speed=0;b.acceleration=0;b.friction=0;b.turn=0;b.motionReset=e.eventDisableMotion;if(e.eventResetLife){if(!b.commands.Contains("reset-life|"+e.id)){b.commands.Add("reset-life|"+e.id);b.birth=frame;if(e.eventLife>0)b.life=e.eventLife;else if(b.source!=null&&b.source.life>0)b.life=b.source.life;}}}
        if(e.eventPreset=="group-set"&&new[]{"scale","alpha","color"}.Contains(e.eventProperty))Set(b,e.eventProperty,Value(e,"controlValue",b,world,frame));
        return true;
    }
    public static readonly string[] Properties={"speed","direction","acceleration","friction","turn","scale","alpha","color"};
    public static readonly string[] PropertyNames={"速度","方向","加速度","阻力","每帧转向","大小","不透明度","颜色（色相0–360）"};
    public static void Set(SimBullet b,string property,double value){switch(property){case "speed":b.speed=value;break;case "direction":b.direction=value;break;case "acceleration":b.acceleration=value;break;case "friction":b.friction=Math.Max(0,value);break;case "turn":b.turn=value;break;case "scale":b.scale=Math.Max(0,value);break;case "alpha":b.alpha=Math.Max(0,Math.Min(1,value));break;case "color":b.color=SpawnColors.Hue(value);break;}}
    public static string[] NativeObjects(Track t){return (t.eventNativeObjects??"").Split(new[]{',','，',';','；','\r','\n'},StringSplitOptions.RemoveEmptyEntries).Select(s=>s.Trim()).Where(s=>s.Length>0).Distinct().ToArray();}
    public static void Scale(Track t,double ratio){if(!IsEvent(t))return;double divisor=t.eventPreset=="group-set"&&new[]{"scale","alpha","color"}.Contains(t.eventProperty)?1:ratio;t.controlValue/=divisor;t.eventAcceleration/=ratio*ratio;t.eventFriction/=ratio*ratio;t.eventTurn/=ratio;t.eventOrbitSpeed/=ratio;t.eventCopyLife=Math.Max(1,BulletSystem.Round(t.eventCopyLife*ratio));if(t.eventLife>0)t.eventLife=Math.Max(1,BulletSystem.Round(t.eventLife*ratio));ScalarExpressions.ScaleBinding(t.expressions,"controlValue",1/divisor);ScalarExpressions.ScaleBinding(t.expressions,"eventAcceleration",1/(ratio*ratio));ScalarExpressions.ScaleBinding(t.expressions,"eventFriction",1/(ratio*ratio));ScalarExpressions.ScaleBinding(t.expressions,"eventTurn",1/ratio);ScalarExpressions.ScaleBinding(t.expressions,"eventOrbitSpeed",1/ratio);}
}

internal sealed partial class StageCanvas
{
    private void PaintPlayerMove(Graphics g)
    {
        foreach(var track in State.tracks.Where(t=>t.enabled&&!t.previewHidden&&t.eventPreset=="player-move"&&
            (t==SelectedTrack||EventPresets.Active(t,Frame)))){
            NativeTestPosition target;
            try{target=EventPresets.PlayerTarget(State,track,Frame);}
            catch(FormatException){continue;}
            float x=(float)target.x,y=(float)target.y;
            if(Sprites!=null){
                string sprite=String.IsNullOrEmpty(NativeTestSprite)?"sprPlayerIdle":NativeTestSprite;
                if(Sprites.Get(sprite)!=null)Sprites.Draw(g,sprite,x,y,1,0,.5,Color.White);
            }
            var from=track.start==0?(State.nativeTest??new NativeTestPosition()):BulletSystem.PlayerAt(State,track.start-1);
            using(var pen=new Pen(Color.Cyan,1.5f)){
                pen.DashStyle=System.Drawing.Drawing2D.DashStyle.Dash;
                g.DrawLine(pen,(float)from.x,(float)from.y,x,y);
                g.DrawRectangle(pen,x-10,y-14,20,28);
                pen.DashStyle=System.Drawing.Drawing2D.DashStyle.Solid;
                g.DrawLine(pen,x-17,y,x+17,y);g.DrawLine(pen,x,y-21,x,y+21);
            }
            using(var brush=new SolidBrush(Color.Cyan))
                g.DrawString(Language.Text("玩家传送目标")+" · "+x.ToString("0.##")+", "+y.ToString("0.##"),Theme.Small,brush,x+18,y-19);
        }
    }
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
        if(t.eventPreset=="player-move"){
            bag.Add("说明","事件",typeof(string),()=>"强制移动玩家到指定位置；可设置移动后是否保留原先运动状态，以及是否重置二段跳。",null);
            EventNumber(bag,t,"playerTargetX","目标坐标 X","位置传送");EventNumber(bag,t,"playerTargetY","目标坐标 Y","位置传送");
            Field(bag,"移动后保留原先运动状态","运动状态",()=>t.playerKeepMotion,v=>t.playerKeepMotion=v,null,"关闭：移动后将玩家水平/垂直速度与重力归零。开启：保持原有速度继续飞行。");
            Field(bag,"重置二段跳","跳跃状态",()=>t.playerResetJump,v=>t.playerResetJump=v,null,"开启：传送后刷新二段跳次数，使玩家可在空中再次跳跃。");
            return;
        }
        if(t.eventPreset=="player-set"){
            bag.Add("说明","事件",typeof(string),()=>"设置玩家运行时参数，包括无限跳跃、无敌状态与能否运动。",null);
            Field(bag,"能否无限跳","玩家参数",()=>t.playerInfJump,v=>t.playerInfJump=v,null,"开启后玩家在空中可无限连续跳跃。");
            Field(bag,"是否无敌","玩家参数",()=>t.playerInvincible,v=>t.playerInvincible=v,null,"开启后玩家免疫触碰刺、子弹等判定。");
            Field(bag,"是否可以运动","玩家参数",()=>t.playerCanMove,v=>t.playerCanMove=v,null,"关闭后定住玩家，无法移动且速度重力归零。");
            return;
        }
        bag.Add("说明","事件",typeof(string),()=>t.eventPreset=="clear-bullets"?"立即清除飞行子弹，保留未释放的整体对象；之后发射的新子弹照常出现。":t.eventPreset=="clear-danmaku"?"立即清除飞行子弹、当前整体对象和可视对象。发射器保留，未来物件仍可出场。":t.eventPreset=="stop-bullets"?"只把当前弹速设为0；后续加速和行为仍可生效。需要取消后续运动时使用重设运动事件。":"重设全屏弹幕及整体对象（多边形、转转棒等）的速度、方向、加速度、阻力、转向与寿命；整体对象立即销毁并顺从重设运动。",null);
        if(t.eventPreset=="reset-motion"){
            Field(bag,"恢复出生速度与方向","运动重设",()=>t.eventRestoreBirth,v=>{t.eventRestoreBirth=v;QueueInspectorUpdate();});
            EventNumber(bag,t,"controlValue","重设速度","运动重设");EventNumber(bag,t,"controlFrom","重设方向 / 度","运动重设");
            EventNumber(bag,t,"eventAcceleration","重设加速度 / 每帧","运动重设");EventNumber(bag,t,"eventFriction","重设阻力 / 每帧","运动重设");EventNumber(bag,t,"eventTurn","重设转向 / 度每帧","运动重设");Field(bag,"停止后续运动行为","运动重设",()=>t.eventDisableMotion,v=>t.eventDisableMotion=v);
            Field(bag,"重设弹幕寿命","寿命重设",()=>t.eventResetLife,v=>{t.eventResetLife=v;QueueInspectorUpdate();});
            if(t.eventResetLife)bag.Add("重设寿命 / 帧","寿命重设",typeof(int),()=>t.eventLife,v=>t.eventLife=Math.Max(0,(int)v),null,"大于0设为指定寿命；设为0时保留或恢复该弹幕原属性寿命。");
        }
        if(t.eventPreset=="orbit-bullets"){
            bag.Add("说明","事件",typeof(string),()=>"让全屏弹幕及整体对象（多边形、转转棒等）围绕指定圆心旋转；整体对象立即销毁并顺从旋转运动。",null);
            EventNumber(bag,t,"eventCenterX","旋转圆心 X","绕点旋转");EventNumber(bag,t,"eventCenterY","旋转圆心 Y","绕点旋转");
            EventNumber(bag,t,"eventOrbitSpeed","旋转角速度 / 度每帧","绕点旋转");
            string[] radModes={"keep","fixed"},radNames={"保持当前距圆心距离","固定重设半径"};
            Field(bag,"半径模式","绕点旋转",()=>radNames[Math.Max(0,Array.IndexOf(radModes,t.eventRadiusMode))],v=>{t.eventRadiusMode=radModes[Array.IndexOf(radNames,v)];QueueInspectorUpdate();},radNames);
            if(t.eventRadiusMode=="fixed")EventNumber(bag,t,"eventRadius","重设半径 / 像素","绕点旋转");
            Field(bag,"停止后续运动行为","运动重设",()=>t.eventDisableMotion,v=>t.eventDisableMotion=v);
            Field(bag,"重设弹幕寿命","寿命重设",()=>t.eventResetLife,v=>{t.eventResetLife=v;QueueInspectorUpdate();});
            if(t.eventResetLife)bag.Add("重设寿命 / 帧","寿命重设",typeof(int),()=>t.eventLife,v=>t.eventLife=Math.Max(0,(int)v),null,"大于0设为指定寿命；设为0时保留或恢复该弹幕原属性寿命。");
        }
        Field(bag,"额外原生弹幕对象","原生对象",()=>t.eventNativeObjects,v=>{var probe=t.Snapshot();probe.eventNativeObjects=v;var names=EventPresets.NativeObjects(probe);foreach(string name in names)if(!document.inventory.objects.Contains(name)||name.StartsWith("objRuf",StringComparison.Ordinal))throw new ArgumentException("请填写工程中的原生弹幕对象名，多个用逗号分隔。");t.eventNativeObjects=String.Join(",",names);QueueInspectorUpdate();},null,"留空仅作用于RufMaker弹幕。填写原生子弹类对象名，包含其子类；不自动猜测玩家、地形或控制器。");
        if(EventPresets.NativeObjects(t).Length>0)bag.Add("原生限制","原生对象",typeof(string),()=>"原生对象在F5/导出时执行；底片录像不随事件改变。运动重设使用自定义值，只重设GM内建运动，原生代码仍可再次改写。",null);
    }
}
