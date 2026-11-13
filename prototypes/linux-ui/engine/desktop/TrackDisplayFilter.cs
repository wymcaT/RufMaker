using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

// Session-only view state, keyed by exact object identity. No scene fields, IDs,
// preview visibility, runtime semantics or project serialization are changed.
internal sealed class TrackDisplayFilter
{
    private EditorState state;
    private readonly HashSet<Track> hidden=new HashSet<Track>();
    public void Bind(EditorState value){if(!Object.ReferenceEquals(state,value)){state=value;hidden.Clear();}Prune();}
    public void Prune(){hidden.RemoveWhere(t=>state==null||!state.tracks.Contains(t)||t.kind=="native");}
    public int Count {get{return hidden.Count;}}
    public Track[] LaneItems(int lane,ISet<string> bound)
    {return VisibleItems(state.tracks.Where(t=>t.kind!="native"&&t.lane==lane),bound);}
    public Track[] VisibleItems(IEnumerable<Track> candidates,ISet<string> bound)
    {return candidates.Where(t=>!hidden.Contains(t)&&!bound.Contains(t.id)).ToArray();}
    public bool Contains(Track track){return track!=null&&hidden.Contains(track);}
    public Track[] Hidden {get{Prune();return state==null?new Track[0]:state.tracks.Where(hidden.Contains).ToArray();}}
    public void Hide(IEnumerable<Track> tracks){if(state==null)return;foreach(var t in tracks)if(t!=null&&t.kind!="native"&&state.tracks.Contains(t))hidden.Add(t);}
    public void Reveal(IEnumerable<Track> tracks){foreach(var t in tracks)hidden.Remove(t);}
    public void Clear(){hidden.Clear();}
    public static Track[] DisplayOwners(EditorState state,IEnumerable<Track> tracks)
    {
        if(state==null)return new Track[0];var seen=new HashSet<Track>();var queue=new Queue<Track>(tracks.Where(t=>t!=null&&state.tracks.Contains(t)));
        while(queue.Count>0){var current=queue.Dequeue();if(!seen.Add(current))continue;
            var parent=LaneGroupProjection.UniqueTrack(state,current.parentId);if(parent!=null)queue.Enqueue(parent);
            if(LaneGroupProjection.UniqueTrack(state,current.id)!=current)continue;
            foreach(var source in state.tracks.Where(t=>TemplateRuntime.Actions(t).Any(a=>a.type=="spawn"&&a.templateId==current.id)))queue.Enqueue(source);
        }return seen.ToArray();
    }
    public static Track[] KeyOwners(EditorState state,IEnumerable<Keyframe> keys)
    {var set=new HashSet<Keyframe>(keys);return state==null?new Track[0]:state.tracks.Where(t=>t.keyframes.Any(set.Contains)).ToArray();}
    // A cut replaces the victim and its logical descendants, even on other lanes.
    // Treat this conservatively before CutOverlaps can clone/remove hidden objects.
    public static bool WouldCutHidden(EditorState state,IEnumerable<Track> moved,IEnumerable<Track> protectedTracks)
    {
        var held=new HashSet<Track>(moved);var hidden=new HashSet<Track>(protectedTracks.Where(t=>!held.Contains(t)));
        var victims=new HashSet<Track>(state.tracks.Where(t=>!held.Contains(t)&&t.kind!="native"&&!t.templateOnly&&held.Any(h=>h.lane==t.lane&&h.start<=t.end&&h.end>=t.start)));
        bool more;do{more=false;foreach(var t in state.tracks)if(!victims.Contains(t)&&victims.Any(p=>p.id==t.parentId))more|=victims.Add(t);}while(more);
        return victims.Overlaps(hidden);
    }
}

internal sealed partial class TimelineCanvas
{
    private readonly TrackDisplayFilter trackFilter=new TrackDisplayFilter();
    private readonly HashSet<Track> revealedDisplayTracks=new HashSet<Track>();
    private readonly HashSet<Keyframe> hiddenProjectionKeys=new HashSet<Keyframe>();
    public int HiddenTrackCount {get{return trackFilter.Count;}}
    public Track[] HiddenTracks {get{return trackFilter.Hidden;}}
    public bool IsTrackFiltered(Track track){return trackFilter.Contains(track);}
    private bool DisplayEligible(Track track){return track!=null&&!IsTrackFiltered(track)&&!IsLaneFolded(track.lane);}
    public IEnumerable<Track> ProtectedDisplayTracks()
    {return State==null?Enumerable.Empty<Track>():State.tracks.Where(t=>t.kind!="native"&&HiddenByFoldProjection(t)).ToArray();}
    public void HideTrackRows(IEnumerable<Track> tracks)
    {
        if(State==null||InspectorKeyframes)return;FinishProjectionGesture();trackFilter.Bind(State);trackFilter.Hide(tracks.ToArray());revealedDisplayTracks.Clear();revealedReferenceId=null;TemplateSource=null;
        RefreshLayout();if(ActiveLane>=0&&!lanes.Any(l=>l.index==ActiveLane))ActiveLane=-1;SummaryGroup=null;NotifyDisplayFilterChanged();
    }
    public void ShowAllTrackRows(){FinishProjectionGesture();trackFilter.Clear();RefreshLayout();NotifyDisplayFilterChanged();}
    private void NotifyDisplayFilterChanged(){Invalidate();}
    private void RevealFilteredTracks(IEnumerable<Track> targets)
    {
        var exact=targets.Where(t=>t!=null&&State.tracks.Contains(t)).Distinct().ToArray();var owners=TrackDisplayFilter.DisplayOwners(State,exact);
        if(owners.Any(trackFilter.Contains)){FinishProjectionGesture();trackFilter.Reveal(owners);NotifyDisplayFilterChanged();}
        var missing=exact.Where(t=>t.kind!="native"&&!lanes.Any(l=>l.track==t||l.clips!=null&&l.clips.Contains(t))).ToArray();
        foreach(var t in missing)revealedDisplayTracks.Add(t);
        // Ordinary property edits must not reopen a deliberately collapsed source.
        foreach(var t in TrackDisplayFilter.DisplayOwners(State,missing))if(TimelineLayout.HasGeneration(t))SetExpanded(t,true);
    }
    private void RefreshHiddenProjectionKeys()
    {
        hiddenProjectionKeys.Clear();if(State==null||HiddenTrackCount==0&&collapsedLaneGroups.Count==0)return;
        foreach(var t in State.tracks.Where(HiddenByFoldProjection))hiddenProjectionKeys.UnionWith(t.keyframes);
    }
    private bool KeyHiddenByProjection(Keyframe key){return hiddenProjectionKeys.Contains(key);}
    public void RevealTrackRow(Track track)
    {
        if(track==null||State==null||!State.tracks.Contains(track))return;
        SetTrackSelection(new[]{track},track);RefreshLayout();var row=lanes.FirstOrDefault(l=>l.track==track||l.clips!=null&&l.clips.Contains(track));
        if(row!=null){ActiveLane=row.index;vertical.Value=Math.Max(0,Math.Min(vertical.Maximum-vertical.LargeChange+1,row.top));}
    }
    private void AddRevealedDisplayRows()
    {
        var requested=new HashSet<Track>(SelectedTracks.Concat(TrackDisplayFilter.KeyOwners(State,SelectedKeys)));if(SelectedTrack!=null)requested.Add(SelectedTrack);
        revealedDisplayTracks.RemoveWhere(t=>!State.tracks.Contains(t)||!requested.Contains(t));
        foreach(var target in revealedDisplayTracks.Where(DisplayEligible))
            if(!lanes.Any(l=>l.track==target||l.clips!=null&&l.clips.Contains(target)))
                lanes.Add(new Lane{track=target,index=target.lane,property="@template",bindingKey="reference:"+target.id});
    }
}

internal sealed partial class MainForm
{
    private void TrackDisplayMenu(ToolStripItemCollection items)
    {
        if(state==null)return;var scene=state;
        items.Add("隐藏所选物件的时间轴行（仅视图）",null,(s,e)=>{if(state!=scene)return;CommitInspectorEdits();timeline.HideTrackRows(timeline.SelectedTracks);}).Enabled=timeline.SelectedTracks.Any(t=>t.kind!="native");
        var hidden=new ToolStripMenuItem(String.Format(Language.Text("隐藏的物件行：{0}（点击恢复）"),timeline.HiddenTrackCount));Language.PreserveText(hidden);
        foreach(var entry in timeline.HiddenTracks){var track=entry;var item=new ToolStripMenuItem(TimelineRelations.Identity(track),null,(s,e)=>{if(state==scene&&state.tracks.Contains(track)){CommitInspectorEdits();timeline.RevealTrackRow(track);}});Language.PreserveText(item);hidden.DropDownItems.Add(item);}
        hidden.DropDownItems.Add("显示全部物件行（仅视图）",null,(s,e)=>{if(state==scene)timeline.ShowAllTrackRows();});items.Add(hidden);
        var note=new ToolStripMenuItem("仅过滤编辑列表；场景与导出保留。切换或撤销工程后重置。"){Enabled=false};items.Add(note);
    }
}
