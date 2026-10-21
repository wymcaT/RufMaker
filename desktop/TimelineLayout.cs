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
}
