using System;
using System.Collections.Generic;
using System.Linq;

internal static class TimelineLayout
{
    public static int ClockStart(Track t){return t.clockStart??t.start;}
    public static int ClockEnd(Track t){return t.clockEnd??t.end;}
    public static void ShiftClock(Track t,int delta){if(t.clockStart.HasValue)t.clockStart+=delta;if(t.clockEnd.HasValue)t.clockEnd+=delta;MotionPresets.Shift(t,delta);}
    public static bool CutOverlaps(EditorState state,IEnumerable<Track> dragged)
    {
        var held=new HashSet<Track>(dragged);var candidates=state.tracks.Where(t=>!held.Contains(t)&&t.kind!="native"&&!t.templateOnly&&held.Any(h=>h.lane==t.lane&&h.start<=t.end&&h.end>=t.start)).ToList();
        var candidateSet=new HashSet<Track>(candidates);candidates.RemoveAll(t=>{var p=t.parent;while(p!=null){if(candidateSet.Contains(p))return true;p=p.parent;}foreach(var h in held){p=h.parent;while(p!=null){if(p==t)return true;p=p.parent;}}return false;});
        var result=new List<Track>(state.tracks);
        foreach(var victim in candidates){
            var ranges=new List<int[]>{new[]{victim.start,victim.end}};
            foreach(var h in held.Where(t=>t.lane==victim.lane))ranges=ranges.SelectMany(r=>h.end<r[0]||h.start>r[1]?new[]{r}:new[]{new[]{r[0],Math.Min(r[1],h.start-1)},new[]{Math.Max(r[0],h.end+1),r[1]}}.Where(p=>p[0]<=p[1])).ToList();
            var graph=new List<Track>{victim};bool more;do{more=false;foreach(var t in state.tracks)if(graph.Any(p=>p.id==t.parentId)&&!graph.Contains(t)){graph.Add(t);more=true;}}while(more);
            bool referenced=state.tracks.Any(t=>!graph.Contains(t)&&TemplateRuntime.Actions(t).Any(a=>a.templateId==victim.id));
            int at=result.IndexOf(victim);foreach(var row in graph)result.Remove(row);
            if(referenced){foreach(var row in graph){var definition=row.Snapshot();if(row==victim)definition.templateOnly=true;result.Add(definition);}}
            for(int segment=0;segment<ranges.Count;segment++){
                var copies=graph.Select(t=>t.Snapshot()).ToList();if(segment>0||referenced)copies=BulletSystem.CloneTracks(copies);
                var range=ranges[segment];for(int i=0;i<copies.Count;i++){var t=copies[i];var original=graph[i];int start=Math.Max(original.start,range[0]),end=Math.Min(original.end,range[1]);bool missingParent=false;for(var parent=original.parent;parent!=null&&graph.Contains(parent);parent=parent.parent)missingParent|=parent.end<range[0]||parent.start>range[1];if(start>end||missingParent)continue;
                    t.clockStart=ClockStart(original);t.clockEnd=ClockEnd(original);t.start=start;t.end=end;t.clipSourceId=String.IsNullOrEmpty(original.clipSourceId)?original.id:original.clipSourceId;
                    result.Insert(Math.Min(at++,result.Count),t);
                }
            }
        }
        state.tracks=result;return true;
    }
    public static void Normalize(EditorState state)
    {
        int next=Math.Max(0,state.tracks.Select(t=>t.lane).DefaultIfEmpty(-1).Max()+1);
        foreach(var t in state.tracks)if(t.lane<0)t.lane=next++;
        state.laneCount=Math.Max(state.laneCount,state.tracks.Select(t=>t.lane+1).DefaultIfEmpty(1).Max());
    }
    // Template definitions inside a generation group have local time, not song time.
    // Other clips occupy inclusive frame ranges, including disabled clips and groups.
    public static int EnsureExclusive(EditorState state,IEnumerable<Track> preferred=null)
    {
        if(state==null||state.tracks.Count<2)return 0;var bound=BoundTemplates(state);
        var visible=state.tracks.Where(t=>t.kind!="native"&&!bound.Contains(t.id)).ToList();
        bool conflict=visible.GroupBy(t=>t.lane).Any(g=>{int end=-1;foreach(var t in g.OrderBy(t=>t.start).ThenBy(t=>t.end)){if(t.start<=end)return true;end=Math.Max(end,t.end);}return false;});
        if(!conflict)return 0;
        var eligible=new HashSet<Track>(visible);var priority=(preferred??Enumerable.Empty<Track>()).Where(eligible.Contains).Distinct().ToList();var first=new HashSet<Track>(priority);var ordered=priority.Concat(visible.Where(t=>!first.Contains(t))).ToList();
        var placed=new Dictionary<int,List<Track>>();int moved=0;
        foreach(var t in ordered){int lane=t.lane;List<Track> rows;
            Func<int,bool> free=n=>!placed.TryGetValue(n,out rows)||!rows.Any(other=>t.start<=other.end&&other.start<=t.end);
            if(!free(lane)){var group=TimelineGroups.At(state,lane);lane=0;while(lane<state.laneCount&&(!free(lane)||TimelineGroups.At(state,lane)!=group))lane++;while(!free(lane))lane++;if(group!=null&&!group.lanes.Contains(lane))group.lanes.Add(lane);t.lane=lane;moved++;}
            if(!placed.TryGetValue(lane,out rows)){rows=new List<Track>();placed.Add(lane,rows);}rows.Add(t);
        }
        // Changing lane numbers must not reorder execution, seeded randomness or drawing.
        state.preserveTrackOrder=true;state.laneCount=Math.Max(state.laneCount,visible.Select(t=>t.lane+1).DefaultIfEmpty(1).Max());return moved;
    }
    public static void Order(EditorState state){state.tracks=state.tracks.OrderBy(t=>t.kind=="native"?-1:state.preserveTrackOrder?0:t.lane).ToList();}
    public static bool HasGeneration(Track track){return track.spawnActions.Any(a=>a.type=="spawn")||track.bulletActions.Any(a=>a.type=="spawn");}
    public static HashSet<string> BoundTemplates(EditorState state)
    {
        var map=state.tracks.ToDictionary(t=>t.id);var bound=new HashSet<string>();var seen=new HashSet<string>();var queue=new Queue<Track>(state.tracks.Where(t=>!t.templateOnly));
        while(queue.Count>0){var source=queue.Dequeue();if(!seen.Add(source.id))continue;foreach(var action in TemplateRuntime.Actions(source).Where(a=>a.type=="spawn")){
            Track target;if(!map.TryGetValue(action.templateId??"",out target))continue;if(target.templateOnly){bound.Add(target.id);bool more;do{more=false;foreach(var child in state.tracks)if(bound.Contains(child.parentId))more|=bound.Add(child.id);}while(more);}queue.Enqueue(target);
        }}return bound;
    }
    public static int[] TemplateDisplayLanes(EditorState state,Track target)
    {
        var result=new HashSet<int>();var seen=new HashSet<string>();var queue=new Queue<Track>();queue.Enqueue(target);
        while(queue.Count>0){var current=queue.Dequeue();if(!seen.Add(current.id))continue;foreach(var source in state.tracks.Where(t=>TemplateRuntime.Actions(t).Any(a=>a.type=="spawn"&&a.templateId==current.id))){if(source.templateOnly)queue.Enqueue(source);else result.Add(source.lane);}}
        return result.OrderBy(l=>l).ToArray();
    }
}
