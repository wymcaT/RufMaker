using System;
using System.Collections.Generic;
using System.Linq;

internal static class TimelineLayout
{
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
    public static void Order(EditorState state){state.tracks=state.tracks.OrderBy(t=>t.kind=="native"?-1:t.lane).ToList();}
    public static bool HasGeneration(Track track){return track.spawnActions.Any(a=>a.type=="spawn")||track.bulletActions.Any(a=>a.type=="spawn");}
    public static HashSet<string> BoundTemplates(EditorState state)
    {
        var map=state.tracks.ToDictionary(t=>t.id);var bound=new HashSet<string>();var seen=new HashSet<string>();var queue=new Queue<Track>(state.tracks.Where(t=>!t.templateOnly));
        while(queue.Count>0){var source=queue.Dequeue();if(!seen.Add(source.id))continue;foreach(var action in TemplateRuntime.Actions(source).Where(a=>a.type=="spawn")){
            Track target;if(!map.TryGetValue(action.templateId??"",out target))continue;if(target.templateOnly)bound.Add(target.id);queue.Enqueue(target);
        }}return bound;
    }
    public static int[] TemplateDisplayLanes(EditorState state,Track target)
    {
        var result=new HashSet<int>();var seen=new HashSet<string>();var queue=new Queue<Track>();queue.Enqueue(target);
        while(queue.Count>0){var current=queue.Dequeue();if(!seen.Add(current.id))continue;foreach(var source in state.tracks.Where(t=>TemplateRuntime.Actions(t).Any(a=>a.type=="spawn"&&a.templateId==current.id))){if(source.templateOnly)queue.Enqueue(source);else result.Add(source.lane);}}
        return result.OrderBy(l=>l).ToArray();
    }
}
