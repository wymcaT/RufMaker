using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;

internal static class PreviewRevision
{
    private static readonly FieldInfo[] trackFields=typeof(Track).GetFields().Where(f=>!f.IsDefined(typeof(ScriptIgnoreAttribute),true)&&f.Name!="name"&&f.Name!="lane").ToArray();
    private static readonly FieldInfo[] stateFields=typeof(EditorState).GetFields().Where(f=>!f.IsDefined(typeof(ScriptIgnoreAttribute),true)&&f.Name!="tracks").ToArray();
    internal static readonly FieldInfo[] PersistedTrackFields=typeof(Track).GetFields().Where(f=>!f.IsDefined(typeof(ScriptIgnoreAttribute),true)).ToArray();
    private static bool RenderOnly(Track t,bool viewLife){return t.kind=="effect"||t.kind=="script"||t.kind=="native"||t.kind=="camera"&&!viewLife;}
    private static int KeysFrom(Track a,Track b)
    {
        int from=Int32.MaxValue;
        foreach(string property in a.keyframes.Select(k=>k.property).Concat(b.keyframes.Select(k=>k.property)).Distinct()){
            var old=a.keyframes.Where(k=>k.property==property).OrderBy(k=>k.frame).ToArray();var next=b.keyframes.Where(k=>k.property==property).OrderBy(k=>k.frame).ToArray();int index=0;
            while(index<old.Length&&index<next.Length&&SceneComparison.Same(old[index],next[index]))index++;
            if(index==old.Length&&index==next.Length)continue;
            from=Math.Min(from,index==0?Math.Min(a.start,b.start):Math.Min(old[Math.Min(index-1,old.Length-1)].frame,next[Math.Min(index-1,next.Length-1)].frame));
        }
        return from==Int32.MaxValue?Math.Min(a.start,b.start):from;
    }
    // -1: simulation is unchanged, 0: rebuild, positive: preserve earlier checkpoints.
    public static int SimulationFrom(EditorState old,EditorState next,RoomData room,RoomData nextRoom)
    {
        if(old==null||room==null||nextRoom==null||room.width!=nextRoom.width||room.height!=nextRoom.height||old.room!=next.room||old.fps!=next.fps||old.duration!=next.duration||old.randomSeed!=next.randomSeed||old.usePlayerTrace!=next.usePlayerTrace||!SceneComparison.Same(old.nativeTest,next.nativeTest)||next.usePlayerTrace&&!SceneComparison.Same(old.playerTrace,next.playerTrace)||!SceneComparison.Same(old.nativeProject,next.nativeProject))return 0;
        bool viewLife=old.tracks.Concat(next.tracks).Any(t=>BulletLifetime.Mode(t)=="view");
        var before=old.tracks.Where(t=>!RenderOnly(t,viewLife)).ToArray();var after=next.tracks.Where(t=>!RenderOnly(t,viewLife)).ToArray();
        var common=new HashSet<string>(before.Select(t=>t.id).Intersect(after.Select(t=>t.id)));
        if(!before.Where(t=>common.Contains(t.id)).Select(t=>t.id).SequenceEqual(after.Where(t=>common.Contains(t.id)).Select(t=>t.id)))return 0;
        var oldMap=before.GroupBy(t=>t.id).ToDictionary(g=>g.Key,g=>g.First());var newMap=after.GroupBy(t=>t.id).ToDictionary(g=>g.Key,g=>g.First());
        var templates=new HashSet<string>(old.tracks.Concat(next.tracks).Where(t=>t.templateOnly).Select(t=>t.id));
        foreach(var t in old.tracks.Concat(next.tracks))foreach(var action in TemplateRuntime.Actions(t)){if(!String.IsNullOrEmpty(action.templateId))templates.Add(action.templateId);if(action.generationPreset==CopyGeneration.Key&&action.copyLinked)templates.Add(t.id);}
        int first=Int32.MaxValue;
        foreach(string id in oldMap.Keys.Union(newMap.Keys)){
            Track a,b;oldMap.TryGetValue(id,out a);newMap.TryGetValue(id,out b);
            if(a==null||b==null){var item=a??b;if(item.kind=="variable"||item.kind=="group"||templates.Contains(id)||!String.IsNullOrEmpty(item.parentId))return 0;first=Math.Min(first,item.start);continue;}
            var fields=trackFields.Where(f=>!SceneComparison.Same(f.GetValue(a),f.GetValue(b))).ToArray();bool group=TimelineGroups.Id(a)!=TimelineGroups.Id(b);
            if(fields.Length==0&&!group)continue;
            if(a.kind!=b.kind||a.kind=="variable"||a.kind=="group"||templates.Contains(id)||!String.IsNullOrEmpty(a.parentId)||!String.IsNullOrEmpty(b.parentId))return 0;
            int at=fields.Length==1&&fields[0].Name=="keyframes"&&!group?KeysFrom(a,b):Math.Min(a.start,b.start);
            if(a.indexDelay>0||b.indexDelay>0)at=(int)Math.Max(0,(long)at-299L*Math.Max(a.indexDelay,b.indexDelay));
            first=Math.Min(first,Math.Max(0,at));
        }
        return first==Int32.MaxValue?-1:first;
    }
    public static void Apply(EditorState target,EditorState source)
    {
        var variables=target.variableTimeline;var old=target.tracks.GroupBy(t=>t.id).ToDictionary(g=>g.Key,g=>g.First());var tracks=new List<Track>();
        foreach(var incoming in source.tracks){Track track;if(old.TryGetValue(incoming.id,out track)&&track.kind==incoming.kind){foreach(var field in PersistedTrackFields)if(!SceneComparison.Same(field.GetValue(track),field.GetValue(incoming)))field.SetValue(track,field.GetValue(incoming));track.scopeGroup=incoming.scopeGroup;track.expressionWorldShift=incoming.expressionWorldShift;}else track=incoming.Snapshot();tracks.Add(track);}
        foreach(var field in stateFields)field.SetValue(target,field.GetValue(source));target.tracks=tracks;RandomParameters.Attach(target);if(variables!=null)target.variableTimeline=variables;
    }
}
