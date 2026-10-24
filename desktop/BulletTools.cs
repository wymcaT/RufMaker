using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

internal sealed partial class MainForm
{
    private List<Track> CloneBulletTracks(IEnumerable<Track> tracks){return BulletSystem.CloneTracks(tracks);}
    private void BuildBulletTools(Control bar)
    {
        bar.Controls.Add(Button("弹幕行为…",OpenBulletBehavior,false,100));bar.Controls.Add(Button("添加弹幕控制",()=>AddBulletControl(),false,110));
        BuildTraceTools(bar);
    }
    private void OpenBulletBehavior()
    {
        if(selectedTrack==null||!new[]{"emitter","rod"}.Contains(selectedTrack.kind)){SetStatus("请先选中弹幕发射器或整体对象。");return;}StopPlaying();properties.CommitPending();var t=selectedTrack;
        using(var dialog=new BulletBehaviorForm(t,state))if(Language.ShowDialog(dialog,this)==DialogResult.OK){PushUndo();t.bulletActions=dialog.Actions;t.advancedBullets=true;MarkChanged();ShowProperties();SetStatus("已应用弹幕出生后的行为；时间单位为弹幕年龄。");}
    }
    private void AddBulletControl()
    {var source=selectedTrack!=null&&new[]{"emitter","rod"}.Contains(selectedTrack.kind)?selectedTrack.id:"";AddTrack("control",t=>{t.name="弹幕控制 · 停止";t.targetSource=source;t.end=t.start;t.controlAction="speed";t.controlValue=0;});}
    private void BulletFields(NativeProperties bag,Track t)
    {
        if(new[]{"emitter","rod","object","group"}.Contains(t.kind)){
            Field(bag,"弹幕标签","分组与作用范围",()=>t.tag,v=>t.tag=(v??"").Trim());var groups=state.tracks.Where(g=>g.kind=="group"&&g!=t&&!Ancestors(g).Contains(t)).ToArray();var names=new[]{"无父级"}.Concat(groups.Select(g=>g.name+" · "+g.id.Substring(0,Math.Min(6,g.id.Length)))).ToArray();
            Field(bag,"父级分组","分组与作用范围",()=>names[Math.Max(0,Array.FindIndex(groups,g=>g.id==t.parentId)+1)],v=>{int i=Array.IndexOf(names,v)-1;t.parentId=i<0?"":groups[i].id;},names,"有父级时坐标、角度、大小为组内局部值。右键创建分组会保留当前摆放。");
        }
        if(t.kind=="group") {EaseField(bag,t,null);MotionControls(bag,t);AnimatedFields(bag,t);bag.Add("说明","分组",typeof(string),()=>"控制子物件发射位置与整体对象的变换。普通弹幕发出后独立飞行。",null);}
        if(t.kind=="emitter"){
            Field(bag,"启用进阶弹幕控制","发射阵列",()=>t.advancedBullets,v=>t.advancedBullets=v);Field(bag,"生成形状","发射阵列",()=>BulletSystem.ShapeNames[Math.Max(0,Array.IndexOf(BulletSystem.Shapes,t.emissionShape))],v=>{t.emissionShape=BulletSystem.Shapes[Array.IndexOf(BulletSystem.ShapeNames,v)];t.advancedBullets=true;BeginInvoke(new Action(ShowProperties));},BulletSystem.ShapeNames);
            string[] facing={"扇形方向","固定方向","从中心向外","沿切线","出生时瞄准玩家"},values={"fan","fixed","radial","tangent","aim"};Field(bag,"发射朝向","发射阵列",()=>facing[Math.Max(0,Array.IndexOf(values,t.emissionFacing))],v=>{t.emissionFacing=values[Array.IndexOf(facing,v)];t.advancedBullets=true;},facing);
            Field(bag,"每颗延迟 / 帧","发射阵列",()=>t.indexDelay,v=>t.indexDelay=Math.Max(0,v),null,"编号 i 从 0 开始，第 i 颗在本轮开始后 i × 延迟帧出生。");Field(bag,"每颗弹速递增","发射阵列",()=>t.indexSpeed,v=>t.indexSpeed=v);Field(bag,"每颗方向递增","发射阵列",()=>t.indexAngle,v=>t.indexAngle=v);Field(bag,"每颗大小比例递增","发射阵列",()=>t.indexScale,v=>t.indexScale=v);
            string[] scopes={"每颗一次","每轮齐射一次","整个物件共用一次"},scopeValues={"bullet","volley","group"};Field(bag,"出生随机取样范围","发射阵列",()=>scopes[Math.Max(0,Array.IndexOf(scopeValues,t.randomScope))],v=>{t.randomScope=scopeValues[Array.IndexOf(scopes,v)];t.advancedBullets=true;},scopes);
            bag.Add("弹幕行为","弹幕行为",typeof(string),()=>t.bulletActions.Count+" 项 · 点击下栏“弹幕行为…”编辑",null);if(t.emissionShape=="bezier")bag.Add("曲线控制点","发射阵列",typeof(string),()=>"选中物件后拖动画布中的四个方块；也可右键重置曲线。",null);
        }
        if(t.kind=="rod"){
            Field(bag,"释放队形的歌曲帧（-1 关闭）","释放队形",()=>t.releaseFrame,v=>t.releaseFrame=Math.Max(-1,Math.Min(state.duration,v)),null,"释放后保留当时位置，按弹速和弹幕行为独立飞行。");string[] names={"径向散开","沿切线","指定角度","朝玩家"},values={"radial","tangent","fixed","aim"};Field(bag,"释放方向","释放队形",()=>names[Math.Max(0,Array.IndexOf(values,t.releaseDirection))],v=>t.releaseDirection=values[Array.IndexOf(names,v)],names);Field(bag,"释放弹速","释放队形",()=>t.speed,v=>t.speed=v);Field(bag,"释放后寿命 / 帧","释放队形",()=>t.life,v=>t.life=Math.Max(1,v));bag.Add("弹幕行为","释放队形",typeof(string),()=>t.bulletActions.Count+" 项 · 点击下栏“弹幕行为…”编辑",null);
        }
        if(t.kind=="control"){
            var sources=state.tracks.Where(s=>new[]{"emitter","rod"}.Contains(s.kind)).ToArray();var names=new[]{"全部来源"}.Concat(sources.Select(s=>s.name+" · "+s.id.Substring(0,Math.Min(6,s.id.Length)))).ToArray();Field(bag,"来源物件","作用范围",()=>names[Math.Max(0,Array.FindIndex(sources,s=>s.id==t.targetSource)+1)],v=>{int i=Array.IndexOf(names,v)-1;t.targetSource=i<0?"":sources[i].id;},names);
            Field(bag,"标签（留空不限）","作用范围",()=>t.targetTag,v=>t.targetTag=(v??"").Trim());Field(bag,"齐射编号（-1 不限）","作用范围",()=>t.targetVolley,v=>t.targetVolley=Math.Max(-1,v));string[] parities={"全部编号","偶数编号 0,2,4…","奇数编号 1,3,5…"},pvalues={"all","even","odd"};Field(bag,"编号筛选","作用范围",()=>parities[Math.Max(0,Array.IndexOf(pvalues,t.targetParity))],v=>t.targetParity=pvalues[Array.IndexOf(parities,v)],parities);Field(bag,"最小年龄 / 帧","作用范围",()=>t.targetMinAge,v=>t.targetMinAge=Math.Max(0,v));Field(bag,"最大年龄 / 帧","作用范围",()=>t.targetMaxAge,v=>t.targetMaxAge=Math.Max(t.targetMinAge,v));Field(bag,"包含期间新生弹幕","作用范围",()=>t.includeFuture,v=>t.includeFuture=v,null,"关闭：仅在事件开始帧选中当时已有弹幕。开启：在整个事件时段接收符合条件的新弹幕。");
            var types=BulletSystem.ActionTypes.Where(v=>v!="split").Concat(new[]{"color"}).ToArray();var labels=types.Select(v=>v=="color"?"颜色":BulletSystem.ActionName(v)).ToArray();Field(bag,"控制动作","控制参数",()=>labels[Math.Max(0,Array.IndexOf(types,t.controlAction))],v=>{t.controlAction=types[Array.IndexOf(labels,v)];t.name="弹幕控制 · "+v;BeginInvoke(new Action(ShowProperties));},labels);
            if(t.controlAction=="color")Field(bag,"目标颜色","控制参数",()=>Motion.ColorOf(t.controlColor),v=>t.controlColor=ColorTranslator.ToHtml(v));else {Field(bag,"起始值","控制参数",()=>t.controlFrom,v=>t.controlFrom=v);Field(bag,"结束值","控制参数",()=>t.controlValue,v=>t.controlValue=v);Field(bag,"逐帧公式（留空用数值）","控制参数",()=>t.controlExpression,v=>{if(!String.IsNullOrWhiteSpace(v))RandomExpression.Evaluate(v,state.randomSeed,t.id);t.controlExpression=(v??"").Trim();});Field(bag,"控制缓动","控制参数",()=>Motion.EaseNames[Math.Max(0,Array.IndexOf(Motion.EaseValues,t.controlEasing))],v=>t.controlEasing=Motion.EaseValues[Array.IndexOf(Motion.EaseNames,v)],Motion.EaseNames);}
        }
        if(t.kind=="pattern"){
            Field(bag,"循环次数","片段",()=>t.repeatCount,v=>{t.repeatCount=Math.Max(1,Math.Min(100,v));UpdatePatternEnd(t);});Field(bag,"循环间隔 / 帧","片段",()=>t.repeatGap,v=>{t.repeatGap=Math.Max(0,v);UpdatePatternEnd(t);});Field(bag,"片段播放倍率","片段",()=>t.timeScale,v=>{t.timeScale=Math.Max(.1,Math.Min(10,v));UpdatePatternEnd(t);});bag.Add("包含物件","片段",typeof(int),()=>t.children.Count,null);bag.Add("编辑方法","片段",typeof(string),()=>"右键拆开片段后可编辑内部物件，再重新组合。支持存为自定义预设跨工程复用。",null);
        }
    }
    private IEnumerable<Track> Ancestors(Track t){var seen=new HashSet<string>();while(t!=null&&seen.Add(t.id)){t=state.tracks.FirstOrDefault(g=>g.id==t.parentId&&g.kind=="group");if(t!=null)yield return t;}}
    private Track[] WithGroupChildren(IEnumerable<Track> tracks)
    {var result=new HashSet<Track>(tracks);bool added;do{added=false;foreach(var t in state.tracks)if(result.Any(g=>g.kind=="group"&&g.id==t.parentId))added|=result.Add(t);}while(added);return state.tracks.Where(result.Contains).ToArray();}
    private void GroupSelection()
    {
        var roots=timeline.SelectedTracks.Where(t=>Theme.Visual(t.kind)||t.kind=="group").Where(t=>!Ancestors(t).Any(timeline.SelectedTracks.Contains)).ToArray();if(roots.Length==0)return;if(roots.Select(t=>t.parentId).Distinct().Count()>1){SetStatus("创建分组前，请选中同一父级中的物件。");return;}PushUndo();double x=roots.Average(t=>Motion.Value(t,"x",frame)),y=roots.Average(t=>Motion.Value(t,"y",frame));var group=new Track{id=Guid.NewGuid().ToString("N"),name="弹幕分组",kind="group",x=x,y=y,parentId=roots[0].parentId,start=roots.Min(t=>t.start),end=roots.Max(t=>t.end),lane=Math.Min(199,state.laneCount++)};
        foreach(var t in roots){foreach(string p in new[]{"x","y","x1","y1","x2","y2"}){double delta=p.StartsWith("x")?x:y;Motion.SetBase(t,p,Motion.Base(t,p)-delta);foreach(var k in t.keyframes.Where(k=>k.property==p))k.value-=delta;}t.parentId=group.id;}state.tracks.Add(group);MarkChanged();SelectTrack(group,null);SetStatus("已创建父级分组；子物件坐标改为组内坐标。");
    }
    private void PackPattern()
    {
        var selected=WithGroupChildren(timeline.SelectedTracks).Where(t=>t.kind!="native"&&t.kind!="script").ToArray();if(selected.Length==0)return;if(selected.Any(t=>!String.IsNullOrEmpty(t.parentId)&&!selected.Any(p=>p.id==t.parentId))){SetStatus("请将父级分组一并选中后再组合片段。");return;}PushUndo();int start=selected.Min(t=>t.start);var clip=new Track{id=Guid.NewGuid().ToString("N"),name="弹幕片段",kind="pattern",start=start,lane=selected.Min(t=>t.lane),x=0,y=0};
        foreach(var original in selected){var t=original.Snapshot();t.start-=start;t.end-=start;foreach(var k in t.keyframes)k.frame-=start;if(t.releaseFrame>=0)t.releaseFrame-=start;clip.children.Add(t);state.tracks.Remove(original);}UpdatePatternEnd(clip);state.tracks.Add(clip);MarkChanged();SelectTrack(clip,null);
    }
    private void UpdatePatternEnd(Track t){int span=t.children.Select(c=>c.end).DefaultIfEmpty(0).Max()+1;t.end=t.start+(int)BulletSystem.Round(t.repeatCount*span/t.timeScale+(t.repeatCount-1)*t.repeatGap)-1;state.duration=Math.Max(state.duration,t.end);}
    private void UnpackPattern()
    {
        var t=selectedTrack;if(t==null||t.kind!="pattern")return;PushUndo();var temporary=new EditorState{fps=state.fps,duration=state.duration,tracks=new List<Track>{t},nativeTest=state.nativeTest,randomSeed=state.randomSeed};var items=BulletSystem.Expand(temporary).tracks;state.tracks.Remove(t);var map=items.ToDictionary(c=>c.id,c=>Guid.NewGuid().ToString("N"));foreach(var c in items){string id=c.id;c.id=map[id];string renamed;if(map.TryGetValue(c.parentId,out renamed))c.parentId=renamed;if(map.TryGetValue(c.targetSource,out renamed))c.targetSource=renamed;foreach(var a in c.bulletActions)if(map.TryGetValue(a.templateId,out renamed))a.templateId=renamed;c.lane=t.lane;state.tracks.Add(c);}MarkChanged();timeline.SetTrackSelection(items,items.FirstOrDefault());
    }
    private void AddBulletContext(ContextMenuStrip menu)
    {
        if(selectedTrack!=null&&new[]{"emitter","rod"}.Contains(selectedTrack.kind))menu.Items.Add("编辑弹幕年龄行为…",null,(s,e)=>OpenBulletBehavior());if(timeline.SelectedTracks.Any(t=>Theme.Visual(t.kind)||t.kind=="group"))menu.Items.Add("将所选物件创建为分组",null,(s,e)=>GroupSelection());if(timeline.SelectedTracks.Count>0&&timeline.SelectedTracks.All(t=>t.kind!="native"&&t.kind!="script"))menu.Items.Add("组合为可循环弹幕片段",null,(s,e)=>PackPattern());if(selectedTrack!=null&&selectedTrack.kind=="pattern")menu.Items.Add("拆开片段以编辑内部物件",null,(s,e)=>UnpackPattern());menu.Items.Add("添加弹幕控制事件",null,(s,e)=>AddBulletControl());
    }
}
