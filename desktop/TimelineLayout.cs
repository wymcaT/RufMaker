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
        if(result.Count>200)return false;state.tracks=result;return true;
    }
    public static void Normalize(EditorState state)
    {
        int next=Math.Max(0,state.tracks.Select(t=>t.lane).DefaultIfEmpty(-1).Max()+1);
        foreach(var t in state.tracks)if(t.lane<0)t.lane=next++;
        state.laneCount=Math.Max(state.laneCount,state.tracks.Select(t=>t.lane+1).DefaultIfEmpty(1).Max());
    }
    public static Dictionary<Track,int> Stack(IEnumerable<Track> tracks)
    {
        var ends=new List<int>();var result=new Dictionary<Track,int>();
        foreach(var t in tracks.OrderBy(t=>t.start).ThenBy(t=>t.end)){int row=ends.FindIndex(end=>end<t.start);if(row<0){row=ends.Count;ends.Add(t.end);}else ends[row]=t.end;result[t]=row;}return result;
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
