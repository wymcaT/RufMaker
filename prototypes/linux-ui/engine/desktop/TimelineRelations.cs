using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

// Declared references, not a simulation of particle ancestry or lifetime.
internal sealed class TimelineReference
{
    public string relation,id,detail,displayName,navigationId;
    public bool selector,navigable=true;
}
internal static class TimelineRelations
{
    public static Track Resolve(EditorState state,string id)
    {if(state==null||String.IsNullOrEmpty(id))return null;var matches=state.tracks.Where(t=>t.id==id).Take(2).ToArray();return matches.Length==1?matches[0]:null;}
    public static Track ResolveCurrent(EditorState current,EditorState expected,string id)
    {return Object.ReferenceEquals(current,expected)?Resolve(current,id):null;}
    public static string NavigationId(TimelineReference row){return row.navigable?row.navigationId??row.id:null;}
    public static string Identity(Track t){return (t.name??"")+" ["+t.id+"]";}
    public static bool Controller(Track t){return t!=null&&t.kind=="control"&&!EventPresets.IsEvent(t);}
    public static bool UsesSourceSelector(Track t){return Controller(t)||EventPresets.IsEvent(t)&&EventPresets.IsOrbit(t);}
    public static bool LocalDefinition(EditorState state,Track t)
    {
        // Generated instances execute eligible object definitions and their logical
        // group descendants on the template clock. Script/control dispatch differs.
        if(t==null||!TemplateRuntime.Eligible(t))return false;
        var seen=new HashSet<Track>();
        while(t!=null&&seen.Add(t)){if(t.templateOnly)return true;var parent=Resolve(state,t.parentId);t=parent!=null&&parent.kind=="group"?parent:null;}
        return false;
    }
    public static string EventTitle(Track t)
    {
        if(Controller(t))return Language.Text("[控制区间] ")+(t.name??"");
        if(t.kind=="script"&&TimelineLayout.ClockStart(t)!=t.start){int trigger=TimelineLayout.ClockStart(t);return Language.Text("[脚本片段] ")+(t.name??"")+" · "+(trigger>=t.start&&trigger<=t.end?Language.Text("原始触发帧")+" "+trigger+" f":Language.Text("此片段无脚本触发"));}
        return t.name??"";
    }
    public static string Timing(Track t){return Timing(t.owner,t);}
    public static string Timing(EditorState state,Track t)
    {
        var lines=new List<string>();
        if(t.kind=="script"){
            int trigger=TimelineLayout.ClockStart(t);
            lines.Add(Language.Text("脚本原始触发帧")+": "+trigger+" f");
            lines.Add(Language.Text("当前片段范围")+": "+t.start+"–"+t.end+" f");
            lines.Add(Language.Text(trigger>=t.start&&trigger<=t.end?"触发帧在片段内；仍受启用状态约束":"触发帧不在片段内；此片段不触发脚本"));
            lines.Add(Language.Text("脚本说明为导出调度；C# 弹幕预览不执行任意 GML。"));
        }else{
            string label=LocalDefinition(state,t)?"模板局部时间范围（非粒子寿命）":Controller(t)?"控制器可用区间（非连续事件或粒子寿命）":t.kind=="control"&&t.eventPreset=="group-copy"?"复制事件触发帧":EventPresets.IsEvent(t)?"事件声明区间（受启用状态与选择条件约束）":"物件声明区间（非粒子寿命）";
            bool point=t.kind=="control"&&t.eventPreset=="group-copy";
            lines.Add(Language.Text(label)+": "+t.start+(point?"":"–"+t.end)+" f");
            if(LocalDefinition(state,t))lines.Add(Language.Text("模板定义坐标不是歌曲触发帧；生成时间由来源规则决定。"));
            if(Controller(t)){
                lines.Add(Language.Text(t.includeFuture?"区间内可纳入新匹配弹幕；已纳入者持续受控至区间结束。":"仅开始帧纳入匹配弹幕；已纳入者持续受控至区间结束。"));
                lines.Add(Language.Text("控制插值原始范围")+": "+TimelineLayout.ClockStart(t)+"–"+TimelineLayout.ClockEnd(t)+" f");
                lines.Add(Language.Text("纳入时筛选")+": age "+t.targetMinAge+"–"+t.targetMaxAge+"; volley "+t.targetVolley+"; parity "+t.targetParity);
                if(!String.IsNullOrWhiteSpace(t.controlExpression))lines.Add(Language.Text("控制公式（不是触发条件）")+": "+t.controlExpression);
            }else if(EventPresets.IsEvent(t)){
                lines.Add(Language.Text(point?"只在开始帧复制一次；结束帧不重复复制。":t.start==t.end?"仅在声明帧检查事件。":"声明区间内逐帧检查事件（含首尾帧）。"));
                lines.Add(Language.Text("事件检查启用状态和父组时段；作用范围按具体事件类型决定。"));
            }
        }
        lines.Add(Language.Text(t.enabled?"物件已启用":"物件已禁用"));
        if(TemplateRuntime.Eligible(t))lines.Add(Language.Text("启用状态是定义字段；生成实例的根由来源规则激活。"));
        return String.Join("\r\n",lines);
    }
    public static bool LiveCopy(BulletAction action,bool bullet)
    {return !bullet&&action.generationPreset==CopyGeneration.Key&&action.copyLinked;}
    public static string SpawnDetail(Track source,BulletAction a,bool bullet,int index)
    {
        var parts=new List<string>{Language.Text(bullet?"弹幕年龄规则":"轨道局部规则")+" #"+(index+1),Language.Text(a.enabled?"规则已启用":"规则已禁用")};
        bool linked=LiveCopy(a,bullet),conditional=!String.IsNullOrWhiteSpace(a.condition);
        parts.Add("start="+a.start+" f; duration="+a.duration+" f; stagger="+a.stagger+" f/index");
        parts.Add(a.spawnRepeat>0?Language.Text("重复间隔")+"="+a.spawnRepeat+" f · "+Language.Text(a.duration==0?"重复时 duration=0 不限制规则窗口":"重复窗口包含结束帧"):Language.Text(conditional?"无重复间隔：在条件窗口内逐帧检查":"无条件且无重复间隔：仅规则起点检查"));
        parts.Add("index="+a.spawnIndexFrom+".."+a.spawnIndexTo+"; parity="+a.spawnParity+"; generation<"+a.maxGeneration);
        if(bullet){
            parts.Add(Language.Text(a.spawnTiming=="action-time"?"行为顺序内生成（移动前）；条件 frame 为歌曲帧":"移动后生成；条件 frame 为来源时钟帧"));
            parts.Add(Language.Text(a.once?"每颗母弹成功生成后仅一次":"不限制每颗母弹的成功次数"));
        }else parts.Add(Language.Text("规则年龄 = 来源时钟 − 原始开始帧")+" ("+TimelineLayout.ClockStart(source)+" f)");
        if(conditional)parts.Add(Language.Text("条件")+": "+a.condition);
        if(linked)parts.Add(Language.Text("实时联动副本")+" · "+Language.Text("联动使用来源自身；保留的模板引用不用于此规则"));
        else if(a.generationPreset==CopyGeneration.Key)parts.Add(Language.Text("独立模板副本"));
        if(a.destroyParent)parts.Add(Language.Text(linked?"联动模式不退掉来源":"成功生成后退掉母体；缺失模板不退掉"));
        return String.Join(" · ",parts);
    }
    private static IEnumerable<TimelineReference> Spawns(Track source)
    {
        foreach(bool bullet in new[]{false,true}){var actions=bullet?source.bulletActions:source.spawnActions;
            for(int i=0;i<actions.Count;i++){var a=actions[i];if(a.type!="spawn")continue;
                string detail=SpawnDetail(source,a,bullet,i);
                if(LiveCopy(a,bullet)){
                    yield return new TimelineReference{relation="实时联动来源引用",id=source.id,detail=detail};
                    if(!String.IsNullOrEmpty(a.templateId))yield return new TimelineReference{relation="保留的独立副本模板",id=a.templateId,detail=Language.Text("仅关闭联动后才使用此模板")};
                }else yield return new TimelineReference{relation=bullet?"弹幕年龄生成引用":"轨道生成引用",id=a.templateId??"",detail=detail};
            }
        }
    }
    private sealed class Declaration {public Track track,owner;public List<Track> scope;public string path;}
    private static IEnumerable<Declaration> Declarations(EditorState state)
    {
        var queue=new Queue<Declaration>(state.tracks.Select(t=>new Declaration{track=t,owner=t,scope=state.tracks,path=Identity(t)}));var seen=new HashSet<Track>();
        while(queue.Count>0){var node=queue.Dequeue();if(!seen.Add(node.track))continue;yield return node;
            if(node.track.kind=="pattern")foreach(var child in node.track.children)queue.Enqueue(new Declaration{track=child,owner=node.owner,scope=node.track.children,path=node.path+" / "+Identity(child)});
        }
    }
    public static List<TimelineReference> References(EditorState state,Track t)
    {
        var rows=new List<TimelineReference>();
        if(!String.IsNullOrEmpty(t.parentId))rows.Add(new TimelineReference{relation="逻辑父组",id=t.parentId,detail=Language.Text("parentId：不等同于轨道归属组或生成来源")});
        foreach(var child in state.tracks.Where(c=>!String.IsNullOrEmpty(t.id)&&c.parentId==t.id))rows.Add(new TimelineReference{relation="逻辑子物件",id=child.id,detail="parentId"});
        foreach(var node in Declarations(state)){
            bool nested=node.track!=node.owner;
            foreach(var edge in Spawns(node.track).Where(r=>r.id==t.id&&(r.relation=="轨道生成引用"||r.relation=="弹幕年龄生成引用"))){
                // Pattern sibling IDs are local even if a top-level ID is equal.
                if(nested&&(edge.relation=="实时联动来源引用"||node.scope.Any(s=>s.id==edge.id)))continue;
                rows.Add(new TimelineReference{relation="引用此模板的来源",id=node.track.id,navigationId=node.owner.id,displayName=nested?node.path:null,detail=edge.detail+(nested?" · "+Language.Text("定位外层循环片段；子物件 ID 仅在片段内有效"):"")});
            }
            if(t.kind=="pattern"&&node.owner==t&&nested){
                rows.Add(new TimelineReference{relation="循环片段内声明",id=node.track.id,navigationId=t.id,displayName=node.path,detail=Language.Text("定位外层循环片段；子物件 ID 仅在片段内有效")});
                foreach(var edge in Spawns(node.track))rows.Add(new TimelineReference{relation=edge.relation,id=edge.id,navigationId=t.id,displayName=node.path+" → ["+edge.id+"]",detail=edge.detail+" · "+Language.Text("嵌套引用仅展示声明；不按裸 ID 定位运行实例")});
            }
        }
        rows.AddRange(Spawns(t));
        bool applies=UsesSourceSelector(t);
        if(!String.IsNullOrEmpty(t.targetSource)){
            rows.Add(new TimelineReference{relation=applies?"控制目标来源选择器":"保留的来源选择器（当前类型不使用）",id=t.targetSource,selector=true,detail=Language.Text(applies?"选择器不是生成来源；也可匹配裁剪来源 ID":"这是存储字段，不代表该事件会按来源筛选")});
            if(applies)foreach(var alias in state.tracks.Where(a=>a.clipSourceId==t.targetSource&&a.id!=t.targetSource&&(String.IsNullOrEmpty(t.targetTag)||a.tag==t.targetTag)))rows.Add(new TimelineReference{relation="选择器匹配的裁剪片段",id=alias.id,detail="clipSourceId = "+t.targetSource+" · "+Language.Text("仅声明候选，不能证明已有实例被影响")});
        }
        if(!String.IsNullOrEmpty(t.targetTag)){
            rows.Add(new TimelineReference{relation=applies?"控制目标标签选择器":"保留的标签选择器（当前类型不使用）",id="",displayName=t.targetTag,navigable=false,selector=true,detail=Language.Text(applies?"标签与来源取交集；标签不是物件 ID":"这是存储字段，不代表该事件会按标签筛选")});
            if(applies)foreach(var match in state.tracks.Where(a=>a.tag==t.targetTag&&(String.IsNullOrEmpty(t.targetSource)||a.id==t.targetSource||a.clipSourceId==t.targetSource)))rows.Add(new TimelineReference{relation="标签与来源匹配的声明",id=match.id,detail="tag = "+t.targetTag+" · "+Language.Text("仅声明候选，不能证明已有实例被影响")});
        }
        if(EventPresets.IsEvent(t)&&EventPresets.GroupEvent(t)){
            string id=String.IsNullOrEmpty(t.eventGroup)?TimelineGroups.At(state,t.lane)==null?"":TimelineGroups.At(state,t.lane).id:t.eventGroup;
            var groups=state.timelineGroups.Where(g=>g.id==id&&!String.IsNullOrEmpty(id)).ToArray();
            rows.Add(new TimelineReference{relation="事件目标轨道组",id="",navigable=false,displayName=groups.Length==1?groups[0].name+" ["+id+"]":"["+id+"]",detail=Language.Text(groups.Length==1?"轨道组作用范围；不等同于逻辑父组":"未唯一解析目标轨道组；不能证明事件会产生效果")});
        }
        if(!String.IsNullOrEmpty(t.clipSourceId))rows.Add(new TimelineReference{relation="裁剪来源标识",id=t.clipSourceId,detail=Language.Text("原片段可能已被裁剪替换；不代表当前生成来源")});
        return rows;
    }
    public static string ReferenceName(EditorState state,TimelineReference r)
    {
        if(r.displayName!=null)return r.displayName;
        var matches=state.tracks.Where(t=>!String.IsNullOrEmpty(r.id)&&t.id==r.id).ToArray();
        return matches.Length==1?Identity(matches[0]):"["+r.id+"] · "+Language.Text(matches.Length>1?"ID 重复，不能唯一定位":"未在当前场景解析（缺失或外部引用）");
    }
    // Iterative traversal terminates on self-references and recursive template graphs.
    public static HashSet<string> Upstream(EditorState state,string id)
    {
        var reverse=new Dictionary<string,List<string>>();
        foreach(var t in state.tracks)foreach(var edge in Spawns(t).Where(a=>a.relation!="保留的独立副本模板"&&!String.IsNullOrEmpty(a.id))){List<string> sources;if(!reverse.TryGetValue(edge.id,out sources)){sources=new List<string>();reverse.Add(edge.id,sources);}if(!String.IsNullOrEmpty(t.id))sources.Add(t.id);}
        var seen=new HashSet<string>();var queue=new Queue<string>();if(!String.IsNullOrEmpty(id))queue.Enqueue(id);
        while(queue.Count>0){string current=queue.Dequeue();if(!seen.Add(current))continue;List<string> sources;if(reverse.TryGetValue(current,out sources))foreach(string source in sources)queue.Enqueue(source);
            var target=Resolve(state,current);var parent=target==null?null:Resolve(state,target.parentId);if(parent!=null&&parent.kind=="group")queue.Enqueue(parent.id);
        }
        return seen;
    }
}
internal sealed partial class TimelineCanvas
{
    private string revealedReferenceId;
    public bool RevealReference(string id)
    {
        var target=TimelineRelations.Resolve(State,id);if(target==null||target.kind=="native"||dragMode!=null||State.tracks.GroupBy(t=>t.id).Any(g=>g.Count()>1))return false;
        // Existing selection callback binds the stage and both inspectors. Never seek a template's local time as song time.
        foreach(string sourceId in TimelineRelations.Upstream(State,id)){var source=TimelineRelations.Resolve(State,sourceId);if(source!=null)SetExpanded(source,true);}
        revealedReferenceId=id;TemplateSource=null;SetTrackSelection(new[]{target},target);RefreshLayout();
        var row=lanes.FirstOrDefault(l=>l.track==target||l.clips!=null&&l.clips.Contains(target));
        if(row!=null){ActiveLane=row.index;vertical.Value=Math.Max(0,Math.Min(vertical.Maximum-vertical.LargeChange+1,row.top));}
        if(!target.templateOnly&&!TimelineLayout.BoundTemplates(State).Contains(target.id))FitSelection();Invalidate();return row!=null;
    }
}
internal sealed partial class MainForm
{
    private void ShowTimelineRelations()
    {
        var t=selectedTrack;if(t==null||state==null||busy||saving||writingRoomInstance)return;CommitInspectorEdits();var scene=state;string navigate=null;
        using(var dialog=new Form{Text=Language.Text("来源与事件信息"),StartPosition=FormStartPosition.CenterParent,Size=new Size(960,680),MinimumSize=new Size(660,480),BackColor=Theme.Panel,ForeColor=Theme.Text,Font=Theme.Font,MinimizeBox=false,MaximizeBox=false}){
            var info=new TextBox{Dock=DockStyle.Top,Height=170,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BackColor=Theme.Panel,ForeColor=Theme.Text,Text=TimelineRelations.Identity(t)+"\r\n"+TimelineRelations.Timing(scene,t)+"\r\n"+Language.Text("以下是声明引用；多个来源分别列出，可逐层定位。递归引用不代表无限存活。")};Language.PreserveText(info);
            var list=new ListView{Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,MultiSelect=false,HideSelection=false,BackColor=Theme.Back,ForeColor=Theme.Text};list.Columns.Add(Language.Text("关系"),175);list.Columns.Add(Language.Text("物件名称与稳定 ID"),280);list.Columns.Add(Language.Text("规则与说明"),360);
            foreach(var row in TimelineRelations.References(scene,t)){var item=new ListViewItem(Language.Text(row.relation));item.SubItems.Add(TimelineRelations.ReferenceName(scene,row));item.SubItems.Add(row.detail);item.Tag=row;list.Items.Add(item);}
            var details=new TextBox{Dock=DockStyle.Bottom,Height=112,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BackColor=Theme.Panel,ForeColor=Theme.Text};Language.PreserveText(details);
            var panel=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=46,FlowDirection=FlowDirection.RightToLeft};var close=new Button{Text=Language.Text("关闭"),AutoSize=true,DialogResult=DialogResult.Cancel,BackColor=Theme.Raised,ForeColor=Theme.Text,UseVisualStyleBackColor=false};var go=new Button{Text=Language.Text("定位并编辑引用物件"),AutoSize=true,Enabled=false,BackColor=Theme.Raised,ForeColor=Theme.Text,UseVisualStyleBackColor=false};
            Action update=()=>{var row=list.SelectedItems.Count==1?list.SelectedItems[0].Tag as TimelineReference:null;string id=row==null?null:TimelineRelations.NavigationId(row);details.Text=row==null?"":TimelineRelations.ReferenceName(scene,row)+"\r\n"+row.detail;go.Text=Language.Text(row!=null&&row.navigationId!=null&&row.navigationId!=row.id?"定位外层循环片段":"定位并编辑引用物件");go.Enabled=!busy&&!saving&&!writingRoomInstance&&TimelineRelations.ResolveCurrent(state,scene,id)!=null;};list.SelectedIndexChanged+=(s,e)=>update();go.Click+=(s,e)=>{update();if(!go.Enabled)return;navigate=TimelineRelations.NavigationId((TimelineReference)list.SelectedItems[0].Tag);dialog.DialogResult=DialogResult.OK;dialog.Close();};list.DoubleClick+=(s,e)=>{if(go.Enabled)go.PerformClick();};
            panel.Controls.Add(close);panel.Controls.Add(go);dialog.Controls.Add(list);dialog.Controls.Add(details);dialog.Controls.Add(info);dialog.Controls.Add(panel);dialog.CancelButton=close;Language.ShowDialog(dialog,this);
        }
        if(navigate!=null&&(busy||saving||writingRoomInstance||TimelineRelations.ResolveCurrent(state,scene,navigate)==null||!timeline.RevealReference(navigate)))SetStatus("引用无法在当前时间轴唯一定位；未修改工程。");
    }
}
