using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

internal static class GenerationPresets
{
    public static readonly string[] Names={"整体扩散 · 保持形状","整体扩散 · 统一速度","360° 随机弹","对玩家自机狙","溅射弹幕","复制obj"};
    public static readonly string[] Keys={"formation-scale","formation-uniform","random-360","aim-player","splash",CopyGeneration.Key};
    public static bool Formation(string key){return key==Keys[0]||key==Keys[1];}
    public static string Name(string key){int i=Array.IndexOf(Keys,key);return i<0?key:Names[i];}
    public static BulletAction Apply(EditorState state,Track source,string key)
    {
        if(!TemplateRuntime.Eligible(source)||!Keys.Contains(key))throw new ArgumentException("请将弹幕生成模板拖到物件、整体对象、发射器或分组上。");
        if(Formation(key)&&source.kind!="rod")throw new ArgumentException("整体扩散模板需要拖到整体对象上。");
        if(key==CopyGeneration.Key)return CopyGeneration.Apply(state,source);
        if(state.tracks.Count>=200)throw new ArgumentException("工程物件数量已达上限。");
        bool formation=Formation(key);var shot=new Track{id=Guid.NewGuid().ToString("N"),kind="emitter",name="模板 · "+Name(key),lane=source.lane,start=0,end=0,x=0,y=0,angle=0,speed=3,scale=.75,count=formation||key==Keys[3]?1:12,spread=0,life=180,interval=25,advancedBullets=true,templateOnly=true,rotateSprite=false,emissionFacing=key==Keys[3]?"aim":"fan",randomPerInstance=true,sprite=source.kind=="rod"||source.kind=="emitter"?source.sprite:"sprBullet",color=source.kind=="rod"||source.kind=="emitter"?source.color:"#FFFFFF",deathEffect="shrink-fade",deathFrames=15};
        if(formation)shot.bulletActions.Add(new BulletAction{type="direction",start=0,duration=0,expression="point_direction(source.x,source.y,x,y)"});
        if(key==Keys[0])shot.bulletActions.Add(new BulletAction{type="speed",start=0,duration=0,expression="point_distance(x,y,source.x,source.y)*speed/100"});
        if(key==Keys[2]){shot.expressions["angle"]="random_range(0,360)";shot.randomScope="bullet";}
        if(key==Keys[3])shot.spread=10;
        if(key==Keys[4]){shot.expressions["spread"]="360/n";shot.expressions["angle"]="(n-1)*180/n";}
        var action=new BulletAction{type="spawn",generationPreset=key,templateId=shot.id,start=0,duration=0,spawnRepeat=Math.Max(1,state.fps/2),spawnOrigin=formation?"each":"center",inheritAngle=false,inheritAppearance=source.kind=="rod"||source.kind=="emitter",inheritScale=formation,destroyParent=false,maxGeneration=4,spawnDirectionMode=key==Keys[4]?"random":"fixed"};
        state.tracks.Add(shot);(source.kind=="emitter"?source.bulletActions:source.spawnActions).Add(action);source.advancedBullets=true;RandomParameters.Attach(state);return action;
    }
    public static double Phase(BulletAction a,Track source,int age,int index,int frame)
    {
        int at=age-a.start-index*a.stagger,wave=a.spawnRepeat>0?Math.Max(0,at/a.spawnRepeat):0;double phase=a.spawnAngle;
        if(a.spawnDirectionMode=="add")phase+=wave*a.spawnWaveAngleStep;
        if(a.spawnDirectionMode=="random"){
            int slot=source.spawnActions.Concat(source.bulletActions).ToList().IndexOf(a);
            phase+=RandomExpression.Evaluate("random(360)",source.expressionSeed,(source.randomId??source.id)+"|spawnAngle"+slot+"|base",new ExpressionContext{frame=frame,randomFrame=frame,randomOffset=wave*1009});
        }return phase;
    }
    public static void Fields(NativeProperties bag,EditorState state,Track source,BulletAction action,Action<Action> edit)
    {
        if(String.IsNullOrEmpty(action.generationPreset))return;var t=state.tracks.FirstOrDefault(row=>row.id==action.templateId);if(t==null)return;
        if(action.generationPreset==CopyGeneration.Key){CopyGeneration.Fields(bag,state,source,action,edit);return;}
        bag.Add("弹幕生成模板","快捷生成参数",typeof(string),()=>Language.Text(Name(action.generationPreset)),null);
        bag.Add(action.generationPreset==Keys[0]?"扩散速度（距中心100像素处）":"新弹幕速度","快捷生成参数",typeof(double),()=>t.speed,value=>edit(()=>t.speed=(double)value));
        if(!Formation(action.generationPreset))bag.Add("每次发射数量","快捷生成参数",typeof(int),()=>t.count,value=>edit(()=>t.count=Math.Max(1,Math.Min(300,(int)value))));
        if(action.generationPreset==Keys[2])bag.Add("新弹幕方向 / 度或表达式","快捷生成参数",typeof(string),()=>{string expression;return t.expressions.TryGetValue("angle",out expression)?expression:t.angle.ToString(CultureInfo.InvariantCulture);},value=>{
            string expression=((string)value??"").Trim();RandomExpression.Evaluate(expression,state.randomSeed,(t.randomId??t.id)+"|angle|base",ExpressionContext.For(t,t.start));
            edit(()=>{t.expressions["angle"]=expression;t.randomScope="bullet";t.randomPerInstance=true;});
        },null,"每颗新弹幕独立取样。可填 random_range(0,360)、random_range(30,150)、choose(0,90,180,270) 或固定角度。0度向右，90度向上。");
        bag.Add("寿命模式","快捷生成参数",typeof(string),()=>BulletLifetime.ModeNames[Array.IndexOf(BulletLifetime.Modes,BulletLifetime.Mode(t))],value=>edit(()=>t.lifetimeMode=BulletLifetime.Modes[Array.IndexOf(BulletLifetime.ModeNames,(string)value)]),BulletLifetime.ModeNames);
        bag.Add("固定寿命 / 帧","快捷生成参数",typeof(int),()=>t.life,value=>edit(()=>t.life=Math.Max(1,(int)value)),null,"仅固定寿命模式使用；出屏幕和出视野模式按边界销毁。");
        bag.Add("出界余量 / 像素","新弹幕寿命",typeof(double),()=>t.despawnMargin,value=>edit(()=>t.despawnMargin=(double)value));
        bag.Add("销毁过渡","新弹幕寿命",typeof(string),()=>BulletLifetime.EffectNames[Array.IndexOf(BulletLifetime.Effects,BulletLifetime.Effect(t))],value=>edit(()=>t.deathEffect=BulletLifetime.Effects[Array.IndexOf(BulletLifetime.EffectNames,(string)value)]),BulletLifetime.EffectNames);
        bag.Add("过渡帧数","新弹幕寿命",typeof(int),()=>t.deathFrames,value=>edit(()=>t.deathFrames=Math.Max(0,(int)value)));
        bag.Add("拖影样式","新弹幕拖影",typeof(string),()=>Trails.Names[Array.IndexOf(Trails.Values,Trails.Mode(t))],value=>edit(()=>Trails.SetMode(t,Trails.Values[Array.IndexOf(Trails.Names,(string)value)])),Trails.Names);
        bag.Add("生成间隔 / 帧","新弹幕拖影",typeof(int),()=>t.trailInterval,value=>edit(()=>t.trailInterval=Math.Max(1,(int)value)));
        bag.Add("拖影寿命 / 帧","新弹幕拖影",typeof(int),()=>t.trailLife,value=>edit(()=>t.trailLife=Math.Max(1,Math.Min(300,(int)value))));
        bag.Add("初始不透明度","新弹幕拖影",typeof(double),()=>t.trailStartAlpha,value=>edit(()=>t.trailStartAlpha=Math.Max(0,Math.Min(1,(double)value))));
        bag.Add("大小衰减 / 帧","新弹幕拖影",typeof(double),()=>t.trailScaleDecay,value=>edit(()=>t.trailScaleDecay=(double)value));
        bag.Add("不透明度衰减 / 帧","新弹幕拖影",typeof(double),()=>t.trailAlphaDecay,value=>edit(()=>t.trailAlphaDecay=(double)value));
        bag.Add("移动速度 / 像素每帧","新弹幕拖影",typeof(double),()=>t.trailSpeed,value=>edit(()=>t.trailSpeed=(double)value));
        bag.Add("移动方向 / 度","新弹幕拖影",typeof(double),()=>t.trailDirection,value=>edit(()=>t.trailDirection=(double)value));
        bag.Add("随机移动方向","新弹幕拖影",typeof(bool),()=>t.trailRandom,value=>edit(()=>t.trailRandom=(bool)value));
        bag.Add("新弹幕行为","快捷生成参数",typeof(string),()=>t.bulletActions.Count+Language.Text(" 项 · 点击上方“新弹幕行为…”编辑"),null);
        if(action.generationPreset==Keys[4])DirectionFields(bag,action,edit);
    }
    public static void DirectionFields(NativeProperties bag,BulletAction a,Action<Action> edit)
    {
        string[] labels={"固定方向","每轮重新随机方向","每轮递增角度"},values={"fixed","random","add"};
        bag.Add("每轮方向模式","生成方向",typeof(string),()=>labels[Math.Max(0,Array.IndexOf(values,a.spawnDirectionMode))],value=>edit(()=>a.spawnDirectionMode=values[Array.IndexOf(labels,(string)value)]),labels);
        bag.Add("每轮递增角度 / 度","生成方向",typeof(double),()=>a.spawnWaveAngleStep,value=>edit(()=>a.spawnWaveAngleStep=(double)value));
        bag.Add("初始方向偏移 / 度","生成方向",typeof(double),()=>a.spawnAngle,value=>edit(()=>a.spawnAngle=(double)value));
    }
}

internal sealed partial class MainForm
{
    private void OpenGeneratedBulletBehavior()
    {
        bool template;int index;if(!GetBehaviorSelection(out template,out index))return;var source=selectedTrack;var action=(template?source.spawnActions:source.bulletActions)[index];var target=state.tracks.FirstOrDefault(t=>t.id==action.templateId);if(target==null||!new[]{"emitter","rod"}.Contains(target.kind))return;
        StopPlaying();properties.CommitPending();behaviorProperties.CommitPending();using(var dialog=new BulletBehaviorForm(target,state,false,-1,4))if(Language.ShowDialog(dialog,this)==System.Windows.Forms.DialogResult.OK){PushUndo();target.bulletActions=dialog.Actions;target.advancedBullets=true;MarkChanged();RefreshBehavior();SetStatus("已更新新弹幕行为；来源物件保持选中。");}
    }
    private void ApplyGenerationPreset(string key,Track target)
    {
        if(state==null)return;try{
            if(!TemplateRuntime.Eligible(target)){SetStatus("请将弹幕生成模板拖到物件、整体对象、发射器或分组上。");return;}
            if(GenerationPresets.Formation(key)&&target.kind!="rod"){SetStatus("整体扩散模板需要拖到整体对象上。");return;}
            if(state.tracks.Count>=200)throw new ArgumentException("工程物件数量已达上限。");
            StopPlaying();properties.CommitPending();PushUndo();GenerationPresets.Apply(state,target,key);SelectTrack(target,null);timeline.Expand(target,true);MarkChanged();bool age=target.kind=="emitter"&&key!=CopyGeneration.Key;FocusBehavior(!age,(age?target.bulletActions:target.spawnActions).Count-1);SetStatus("已添加循环生成："+GenerationPresets.Name(key)+"；在右栏调整间隔、速度和数量。");
        }catch(Exception error){Error(error);}
    }
    private void SetGeneratorSource(Track source,BulletAction action,bool bullets)
    {PushUndo();source.spawnActions.Remove(action);source.bulletActions.Remove(action);var actions=bullets?source.bulletActions:source.spawnActions;actions.Add(action);source.advancedBullets=true;MarkChanged();FocusBehavior(!bullets,actions.Count-1);}
}
