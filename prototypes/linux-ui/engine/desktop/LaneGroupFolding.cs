using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

// A pure view projection. Never normalizes or edits serialized scene data.
internal static class LaneGroupProjection
{
    internal sealed class Row {public int index=-1;public TimelineGroup group;}
    public static bool CanFold(EditorState state,TimelineGroup group)
    {return state!=null&&group!=null&&state.timelineGroups.Contains(group)&&!String.IsNullOrEmpty(group.id)&&state.timelineGroups.Count(g=>g.id==group.id)==1;}
    public static Track UniqueTrack(EditorState state,string id)
    {if(state==null||String.IsNullOrEmpty(id))return null;var matches=state.tracks.Where(t=>t.id==id).Take(2).ToArray();return matches.Length==1?matches[0]:null;}
    public static TimelineGroup FoldAt(EditorState state,ISet<string> collapsed,int lane)
    {var group=TimelineGroups.At(state,lane);return CanFold(state,group)&&collapsed.Contains(group.id)?group:null;}
    public static bool CanMoveIndividualLanes(EditorState state,ISet<string> collapsed,IEnumerable<int> lanes,int shift)
    {return lanes.All(lane=>FoldAt(state,collapsed,lane+shift)==null);}
    public static List<Row> Build(EditorState state,ISet<string> collapsed)
    {
        var result=new List<Row>();if(state==null)return result;var emitted=new HashSet<string>();
        for(int lane=0;lane<state.laneCount;lane++){
            var group=FoldAt(state,collapsed,lane);
            if(group==null)result.Add(new Row{index=lane});
            else if(emitted.Add(group.id))result.Add(new Row{group=group});
        }
        return result;
    }
    public static IEnumerable<Track> Members(EditorState state,TimelineGroup group)
    {return state.tracks.Where(t=>t.kind!="native"&&group.lanes.Contains(t.lane));}
    // Reveal actual membership and every display-owner path for bound templates.
    public static HashSet<int> RevealLanes(EditorState state,IEnumerable<Track> targets)
    {
        var result=new HashSet<int>();var seen=new HashSet<Track>();var queue=new Queue<Track>(targets.Where(t=>t!=null&&state.tracks.Contains(t)));
        while(queue.Count>0){var target=queue.Dequeue();if(!seen.Add(target))continue;result.Add(target.lane);
            if(!String.IsNullOrEmpty(target.parentId)){var parent=UniqueTrack(state,target.parentId);if(parent!=null)queue.Enqueue(parent);}
            if(UniqueTrack(state,target.id)!=target)continue;
            foreach(var source in state.tracks.Where(t=>TemplateRuntime.Actions(t).Any(a=>a.type=="spawn"&&a.templateId==target.id)))queue.Enqueue(source);
        }
        return result;
    }
}

internal sealed partial class TimelineCanvas
{
    private readonly HashSet<string> collapsedLaneGroups=new HashSet<string>();
    private readonly Dictionary<string,TimelineGroup> foldedGroupObjects=new Dictionary<string,TimelineGroup>();
    public TimelineGroup SummaryGroup {get;private set;}
    private bool reconcilingLaneFolds;
    public bool IsLaneFolded(int lane){return LaneGroupProjection.FoldAt(State,collapsedLaneGroups,lane)!=null;}
    public bool IsLaneGroupCollapsed(TimelineGroup group){return LaneGroupProjection.CanFold(State,group)&&collapsedLaneGroups.Contains(group.id);}
    private void PruneLaneFolds()
    {
        foreach(string id in collapsedLaneGroups.ToArray()){
            TimelineGroup original;var current=State.timelineGroups.Where(g=>g.id==id).ToArray();
            if(current.Length!=1||!foldedGroupObjects.TryGetValue(id,out original)||original!=current[0]){collapsedLaneGroups.Remove(id);foldedGroupObjects.Remove(id);}
        }
        if(SummaryGroup!=null&&!IsLaneGroupCollapsed(SummaryGroup))SummaryGroup=null;
    }
    private void RestoreLaneFolds(IEnumerable<string> ids)
    {
        collapsedLaneGroups.Clear();foldedGroupObjects.Clear();SummaryGroup=null;if(State==null)return;
        foreach(string id in ids??Enumerable.Empty<string>()){
            var group=State.timelineGroups.FirstOrDefault(g=>g.id==id);if(!LaneGroupProjection.CanFold(State,group))continue;
            collapsedLaneGroups.Add(id);foldedGroupObjects[id]=group;
        }
    }
    // Finish on the old projection before it changes. State replacement instead aborts
    // gesture references: never apply an old bar/key dictionary to the new document.
    private void FinishProjectionGesture(){if(dragMode!=null)FinishGesture();Capture=false;}
    private void AbortProjectionGesture()
    {
        scrub.Cancel();dragMode=null;Capture=false;TemplateSource=null;dragChanged=false;dragTrack=null;dragKey=null;
        dragRowLanes=null;dragRowTops=null;dragRowHeights=null;bars.Clear();groupFrames.Clear();
        boxInitial.Clear();boxTrackInitial.Clear();hoverTrack=null;hoverEdge=null;clipSnapGuide=null;scrubPosition=null;resizeKey=null;
    }
    public void SetLaneGroupCollapsed(TimelineGroup group,bool value)
    {
        if(!LaneGroupProjection.CanFold(State,group))return;FinishProjectionGesture();
        if(value){revealedDisplayTracks.Clear();revealedReferenceId=null;collapsedLaneGroups.Add(group.id);foldedGroupObjects[group.id]=group;}
        else{collapsedLaneGroups.Remove(group.id);foldedGroupObjects.Remove(group.id);}
        RefreshLayout();ReconcileFoldSelection();if(ActiveLane>=0&&IsLaneFolded(ActiveLane))ActiveLane=-1;
        SummaryGroup=null;RefreshLayout();
    }
    public void ShowAllLaneGroups(){FinishProjectionGesture();collapsedLaneGroups.Clear();foldedGroupObjects.Clear();SummaryGroup=null;RefreshLayout();}
    private void ReconcileFoldSelection()
    {
        if(State==null||reconcilingLaneFolds)return;reconcilingLaneFolds=true;
        try{
            bool changed=SelectedTracks.RemoveWhere(t=>!State.tracks.Contains(t)||HiddenByFoldProjection(t))>0;
            changed|=SelectedKeys.RemoveWhere(k=>Owner(k)==null||KeyHiddenByProjection(k))>0;
            var primary=SelectedTrack;if(primary!=null&&(!State.tracks.Contains(primary)||HiddenByFoldProjection(primary))){primary=SelectedTracks.FirstOrDefault();changed=true;}
            var key=SelectedKey;if(key!=null&&!SelectedKeys.Contains(key)){key=SelectedKeys.FirstOrDefault();changed=true;}
            if(key!=null){primary=Owner(key);if(primary!=null)SelectedTracks.Add(primary); }
            if(changed)NotifySelection(primary,key); // MainForm clears stage/inspector/key workspace too.
        }finally{reconcilingLaneFolds=false;}
    }
    private bool HiddenByFoldProjection(Track track)
    {return track.kind!="native"&&(IsTrackFiltered(track)||IsLaneFolded(track.lane)||(collapsedLaneGroups.Count>0||HiddenTrackCount>0)&&!lanes.Any(l=>l.track==track||l.clips!=null&&l.clips.Contains(track)));}
    public void RevealLaneGroups(IEnumerable<Track> tracks)
    {
        if(State==null||reconcilingLaneFolds)return;var targets=tracks.Where(t=>t!=null&&State.tracks.Contains(t)).ToArray();if(targets.Length==0)return;RevealFilteredTracks(targets);
        var ids=LaneGroupProjection.RevealLanes(State,targets).Select(l=>LaneGroupProjection.FoldAt(State,collapsedLaneGroups,l)).Where(g=>g!=null).Select(g=>g.id).Distinct().ToArray();
        if(ids.Length>0){FinishProjectionGesture();foreach(string id in ids){collapsedLaneGroups.Remove(id);foldedGroupObjects.Remove(id);}RefreshLayout();}SummaryGroup=null;
    }
    public void SelectLaneGroup(TimelineGroup group)
    {if(group==null||State==null||!State.timelineGroups.Contains(group))return;SetLaneGroupCollapsed(group,false);SetTrackSelection(LaneGroupProjection.Members(State,group));}
    public void SelectVisibleTracks()
    {RefreshLayout();var visible=lanes.Where(l=>l.summaryGroup==null).SelectMany(l=>l.clips??(l.track==null?new List<Track>():new List<Track>{l.track})).Where(t=>t.kind!="native").Distinct().ToArray();SelectedTracks.Clear();SelectedTracks.UnionWith(visible);ClearKeySelection();SummaryGroup=null;NotifySelection(visible.FirstOrDefault(),null);}
    private void DrawLaneSummary(Graphics g,Lane lane,int y,int width)
    {
        var group=lane.summaryGroup;var members=LaneGroupProjection.Members(State,group).ToArray();
        using(var background=new SolidBrush(TimelineGroups.Background(group,false)))g.FillRectangle(background,0,y,width,lane.height);
        using(var stripe=new SolidBrush(Motion.ColorOf(group.color)))g.FillRectangle(stripe,0,y,4,lane.height);
        Theme.Label(g,"▸ "+group.name,new Rectangle(8,y,Names-16,lane.height),Theme.Text,Theme.Bold,TimelineTextFlags,false);
        var bound=TimelineLayout.BoundTemplates(State);var clips=members.Where(t=>!t.templateOnly&&!bound.Contains(t.id)).ToArray();
        string scope=String.Format(Language.Text("已折叠 · {0} 个轨道 / {1} 个物件 · 摘要不可编辑"),group.lanes.Count(l=>l>=0&&l<State.laneCount),members.Length);
        if(clips.Length>0)scope+=" · "+String.Format(Language.Text("片段范围 {0}–{1} 帧（非粒子寿命）"),clips.Min(t=>t.start),clips.Max(t=>t.end));
        Theme.Label(g,scope,new Rectangle(Names+8,y,Math.Max(0,width-Names-16),lane.height),Theme.Muted,Theme.Small,TimelineTextFlags,false);
        using(var line=new Pen(Theme.Line))g.DrawLine(line,0,y+lane.height-1,width,y+lane.height-1);
    }
}

internal sealed partial class MainForm
{
    private void ReportLogicalSelectionScope()
    {int count=WithGroupChildren(timeline.SelectedTracks).Count(t=>!timeline.SelectedTracks.Contains(t));if(count>0)SetStatus(String.Format(Language.Text("整体对象操作还包含 {0} 个逻辑子物件（包括折叠或隐藏行）"),count));}
    private void ToggleLaneGroupFold(TimelineGroup group,bool collapsed)
    {CommitInspectorEdits();timeline.SetLaneGroupCollapsed(group,collapsed);}
    private void LaneFoldingMenu(ToolStripItemCollection items)
    {
        if(state==null)return;TrackDisplayMenu(items);
        var context=timeline.SummaryGroup??TimelineGroups.At(state,timeline.ActiveLane>=0?timeline.ActiveLane:selectedTrack==null?-1:selectedTrack.lane);
        if(context!=null){var group=context;bool collapsed=timeline.IsLaneGroupCollapsed(group);
            items.Add(collapsed?"展开轨道组":"折叠轨道组（仅视图）",null,(s,e)=>ToggleLaneGroupFold(group,!collapsed)).Enabled=LaneGroupProjection.CanFold(state,group);
            items.Add("选择组内所有物件（先展开）",null,(s,e)=>timeline.SelectLaneGroup(group));
        }
        var all=new ToolStripMenuItem("轨道组折叠 / 展开");
        foreach(var entry in state.timelineGroups){var group=entry;var item=new ToolStripMenuItem(group.name+" ("+LaneGroupProjection.Members(state,group).Count()+")",null,(s,e)=>ToggleLaneGroupFold(group,!timeline.IsLaneGroupCollapsed(group))){Checked=timeline.IsLaneGroupCollapsed(group),Enabled=LaneGroupProjection.CanFold(state,group)};Language.PreserveText(item);all.DropDownItems.Add(item);}
        all.DropDownItems.Add("展开所有轨道组",null,(s,e)=>timeline.ShowAllLaneGroups());items.Add(all);
        int descendants=WithGroupChildren(timeline.SelectedTracks).Count(t=>!timeline.SelectedTracks.Contains(t));
        if(descendants>0){var notice=new ToolStripMenuItem(String.Format(Language.Text("整体对象操作还包含 {0} 个逻辑子物件（包括折叠或隐藏行）"),descendants)){Enabled=false};Language.PreserveText(notice);items.Add(notice);}
    }
}
