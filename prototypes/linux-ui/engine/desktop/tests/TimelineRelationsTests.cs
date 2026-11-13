using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
internal static class TimelineRelationsTests
{
    private static int checks;
    private static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    private static Track T(string id,int lane,bool template=false){return new Track{id=id,name="Same name",kind="object",lane=lane,start=10,end=200,templateOnly=template};}
    private static void Spawn(Track t,string id,bool bullet=false,bool enabled=true){(bullet?t.bulletActions:t.spawnActions).Add(new BulletAction{type="spawn",templateId=id,enabled=enabled});}
    private static EditorState Fixture(){var a=T("a",0);var b=T("b",1);var shared=T("shared",2,true);var leaf=T("leaf",3,true);Spawn(a,"shared");Spawn(b,"shared",true,false);Spawn(shared,"leaf");Spawn(leaf,"shared");Spawn(leaf,"external-id");return new EditorState{duration=500,fps=50,laneCount=6,tracks=new List<Track>{a,b,shared,leaf}};}
    private static void Pure()
    {
        var s=Fixture();string before=Json.Write(s);var leaf=s.tracks[3];var shared=s.tracks[2];
        Check(TimelineRelations.Resolve(s,"a")==s.tracks[0],"stable ID resolves, not duplicate name");
        var refs=TimelineRelations.References(s,shared);Check(refs.Count(r=>r.relation=="引用此模板的来源")==3,"all shared and recursive inbound references");
        Check(refs.Any(r=>r.id=="b"&&r.detail.Contains(Language.Text("规则已禁用"))),"disabled declared reference remains marked");
        Check(TimelineRelations.Upstream(s,"leaf").SetEquals(new[]{"a","b","shared","leaf"}),"recursive upstream finite and complete");
        var external=TimelineRelations.References(s,leaf).Single(r=>r.id=="external-id");Check(TimelineRelations.Resolve(s,external.id)==null,"missing source cannot navigate");Check(TimelineRelations.ReferenceName(s,external).Contains("external-id"),"missing stable ID retained");
        Check(before==Json.Write(s),"read-only reference analysis leaves model unchanged");
        var copy=T("a",4);s.tracks.Add(copy);Check(TimelineRelations.Resolve(s,"a")==null,"duplicate stable ID rejected");s.tracks.Remove(copy);
        var c=T("controller",4);c.kind="control";c.targetSource="lineage";var one=T("split1",4);one.clipSourceId="lineage";var two=T("split2",5);two.clipSourceId="lineage";s.tracks.AddRange(new[]{c,one,two});
        refs=TimelineRelations.References(s,c);Check(refs.Count(r=>r.relation=="选择器匹配的裁剪片段")==2,"selector exposes multiple lineage matches");Check(refs.Single(r=>r.selector).id=="lineage","selector not presented as unique spawn origin");
        Check(TimelineRelations.Controller(c),"ordinary controller distinct from event");Check(TimelineRelations.EventTitle(c).StartsWith(Language.Text("[控制区间] ")),"controller label explicit");c.eventPreset="clear-bullets";Check(!TimelineRelations.Controller(c),"event not residency");Check(TimelineRelations.Timing(shared).Contains(Language.Text("模板局部时间范围（非粒子寿命）")),"template not particle lifetime");
        var restored=s.Snapshot();Check(TimelineRelations.Resolve(restored,"a")!=s.tracks[0],"snapshot resolves new current object");
        Check(TimelineRelations.ResolveCurrent(restored,s,"a")==null,"modal navigation rejects replaced scene even when IDs match");
        Check(TimelineRelations.ResolveCurrent(s,s,"a")==s.tracks[0],"same scene may resolve requested ID");
        Language.Select(System.IO.Directory.GetCurrentDirectory(),"en");
        Check(TimelineRelations.Timing(shared).Contains("Template local-time range (not particle lifetime)"),"English range translation");
        c.eventPreset="";Check(TimelineRelations.EventTitle(c).StartsWith("[Control window] "),"English controller caption");
        c.eventPreset="group-copy";Check(TimelineRelations.Timing(c).StartsWith("Copy-event trigger frame: 10 f"),"copy event is point, not declared end span");
        c.enabled=false;Check(TimelineRelations.Timing(c).Contains("Object disabled"),"disabled event explicitly labeled");
        Check(TimelineRelations.Identity(shared)=="Same name [shared]","names and IDs are never translated");
        var parent=T("parent",0);parent.kind="group";var child=T("child",1);child.parentId="parent";s.tracks.AddRange(new[]{parent,child});
        Check(TimelineRelations.References(s,child).Single(r=>r.relation=="逻辑父组").id=="parent","logical parent distinct from source and lane group");
        Check(TimelineRelations.References(s,parent).Any(r=>r.relation=="逻辑子物件"&&r.id=="child"),"logical child navigable by stable ID");
        Check(TimelineRelations.Resolve(s,"")==null&&TimelineRelations.Resolve(null,"a")==null,"empty and unavailable scene safely rejected");
        var script=T("script",0);script.kind="script";script.start=50;script.end=90;script.clockStart=60;
        Check(TimelineRelations.Timing(script).Contains("Original script trigger frame: 60 f")&&TimelineRelations.Timing(script).Contains("Trigger is inside the clip"),"script uses original clock inside current clip");
        script.clockStart=10;Check(TimelineRelations.Timing(script).Contains("Original script trigger frame: 10 f")&&TimelineRelations.Timing(script).Contains("this segment does not fire the script"),"clipped-away script trigger is not invented at start");
        Check(TimelineRelations.EventTitle(script).StartsWith("[Script clip] "),"clipped script marker labeled as clip, not actual trigger");
        script.clockStart=null;Check(TimelineRelations.Timing(script).Contains("Original script trigger frame: 50 f"),"unclipped script defaults to start");
        Language.Select(System.IO.Directory.GetCurrentDirectory(),"zh-CN");
        Semantics();
        Console.WriteLine("Pure reference checks: "+checks);
    }
    private static void Semantics()
    {
        Language.Select(System.IO.Directory.GetCurrentDirectory(),"en");
        var source=T("source",0);source.clockStart=-5;var target=T("target",1,true);var group=T("group",2,true);group.kind="group";var child=T("child",3);child.parentId=group.id;
        var state=new EditorState{tracks=new List<Track>{source,target,group,child}};
        var rule=new BulletAction{type="spawn",templateId=target.id,copyLinked=true,start=4,duration=8};source.bulletActions.Add(rule);
        string before=Json.Write(state),detail=TimelineRelations.References(state,source).Single(r=>r.id==target.id).detail;
        Check(!detail.Contains("Linked copies"),"ordinary bullet spawn must not inherit live-copy label");
        Check(TimelineRelations.LocalDefinition(state,child)&&TimelineRelations.Timing(state,child).StartsWith("Template local-time range"),"unnormalized logical child gets template clock");
        Check(!TimelineRelations.LocalDefinition(state,source),"ordinary source uses song clock");
        Check(before==Json.Write(state),"timing and references do not normalize or attach input");
        rule.generationPreset=CopyGeneration.Key;Check(!TimelineRelations.LiveCopy(rule,true),"bullet-age copy is independent even with copyLinked");
        source.bulletActions.Clear();source.spawnActions.Add(rule);
        var rows=TimelineRelations.References(state,source);Check(rows.Any(r=>r.relation=="实时联动来源引用"&&r.id==source.id),"live copy resolves actual source graph");
        Check(rows.Any(r=>r.relation=="保留的独立副本模板"&&r.id==target.id),"retained independent copy definition remains inspectable");
        Check(!TimelineRelations.References(state,target).Any(r=>r.relation=="引用此模板的来源"),"retained independent target not claimed as live generated target");
        Check(!TimelineRelations.Upstream(state,target.id).Contains(source.id),"live copies do not imply active template ancestry");
        rule.copyLinked=false;Check(TimelineRelations.References(state,target).Any(r=>r.relation=="引用此模板的来源"),"independent mode restores actual declared target");
        source.spawnActions.Clear();source.bulletActions.Add(rule);rule.generationPreset="";rule.condition="frame >= 20";rule.once=true;rule.destroyParent=true;rule.spawnTiming="action-time";
        detail=TimelineRelations.SpawnDetail(source,rule,true,0);
        Check(detail.Contains("check every frame")&&detail.Contains("Condition: frame >= 20"),"conditional zero interval not described as one-shot start");
        Check(detail.Contains("before movement")&&detail.Contains("song time")&&detail.Contains("At most one successful spawn")&&detail.Contains("Retire the parent"),"within-frame order, once and successful retirement explicit");
        Check(!TemplateRuntime.Trigger(rule,3,0,0)&&TemplateRuntime.Trigger(rule,4,0,0)&&TemplateRuntime.Trigger(rule,12,0,0)&&!TemplateRuntime.Trigger(rule,13,0,0),"condition window start and inclusive end align runtime");
        rule.condition="";Check(TemplateRuntime.Trigger(rule,4,0,0)&&!TemplateRuntime.Trigger(rule,5,0,0),"unconditional zero interval is a point");
        rule.spawnRepeat=3;rule.duration=0;Check(TemplateRuntime.Trigger(rule,100,0,0)&&!TemplateRuntime.Trigger(rule,101,0,0),"repeated zero duration window is open but interval checked");
        Check(TimelineRelations.SpawnDetail(source,rule,true,0).Contains("duration=0 leaves the rule window open"),"repeat-zero-duration documented accurately");
        rule.spawnTiming="after-move";Check(TimelineRelations.SpawnDetail(source,rule,true,0).Contains("source clock"),"after-move condition frame not falsely called song clock");
        Check(TimelineRelations.SpawnDetail(source,rule,false,0).Contains("(-5 f)"),"track rule timing retains original clipped clock");
        var control=T("control",4);control.kind="control";control.targetTag="same tag";control.start=10;control.end=20;control.clockStart=0;control.clockEnd=30;state.tracks.Add(control);source.tag=target.tag="same tag";
        rows=TimelineRelations.References(state,control);Check(rows.Count(r=>r.relation=="标签与来源匹配的声明")==2,"tag-only control lists declared candidates");
        var tag=rows.Single(r=>r.relation=="控制目标标签选择器");Check(TimelineRelations.NavigationId(tag)==null&&TimelineRelations.ReferenceName(state,tag)=="same tag","tag literal cannot navigate to same-string object ID");
        control.targetSource=source.id;rows=TimelineRelations.References(state,control);Check(rows.Count(r=>r.relation=="标签与来源匹配的声明")==1,"source and tag intersection excludes other tag candidates");
        control.includeFuture=false;Check(TimelineRelations.Timing(state,control).Contains("only at the start")&&TimelineRelations.Timing(state,control).Contains("0–30 f"),"start acquisition separated from original interpolation");
        control.includeFuture=true;Check(TimelineRelations.Timing(state,control).Contains("during the window"),"includeFuture permits later acquisition");
        control.eventPreset="clear-bullets";Check(!TimelineRelations.UsesSourceSelector(control)&&TimelineRelations.References(state,control).Any(r=>r.relation=="保留的来源选择器（当前类型不使用）"),"non-orbit event does not falsely claim ordinary selectors");
        Check(!TimelineRelations.References(state,control).Any(r=>r.relation=="标签与来源匹配的声明"),"unused event tag has no false candidate matches");
        control.eventPreset="orbit-bullets";Check(TimelineRelations.UsesSourceSelector(control),"orbit uses source and tag");
        control.eventPreset="group-copy";Check(TimelineRelations.Timing(state,control).Contains("Copy once at the start"),"long group-copy remains one-shot");
        control.eventPreset="clear-bullets";Check(TimelineRelations.Timing(state,control).Contains("inclusive range"),"long clear event checks inclusive interval");
        control.end=control.start;Check(TimelineRelations.Timing(state,control).Contains("only at its declared frame"),"point preset timing labeled");
        var script=T("script",5);script.kind="script";script.start=10;script.end=20;script.clockStart=15;Check(TimelineRelations.EventTitle(script).Contains("Original trigger frame 15 f"),"script clip label includes valid original trigger");
        script.clockStart=9;Check(TimelineRelations.EventTitle(script).Contains("No script trigger in this clip"),"clipped-out trigger not implied by marker");
        script.templateOnly=true;Check(TimelineRelations.Timing(state,script).StartsWith("Original script trigger frame"),"templateOnly script does not invent template dispatch");
        group.parentId=child.id;child.kind="group";Check(TimelineRelations.LocalDefinition(state,child),"local ancestry cycle detection terminates");group.templateOnly=false;Check(!TimelineRelations.LocalDefinition(state,child),"unmarked cyclic ancestry safely terminates");group.parentId="";
        var pattern=T("pattern",6);pattern.kind="pattern";var nested=T("nested",0);var shadow=T("target",1);pattern.children.AddRange(new[]{nested,shadow});Spawn(nested,"target");state.tracks.Add(pattern);
        Check(TimelineRelations.Resolve(state,"nested")==null,"nested raw ID never resolves as editable global object");
        Check(!TimelineRelations.References(state,target).Any(r=>r.navigationId==pattern.id),"local pattern sibling shadows top-level template ID");
        pattern.children.Remove(shadow);Check(TimelineRelations.References(state,target).Any(r=>r.id==nested.id&&r.navigationId==pattern.id&&r.displayName.Contains("pattern")),"external nested reference navigates outer container with path");
        rows=TimelineRelations.References(state,pattern);Check(rows.Any(r=>r.relation=="循环片段内声明"&&TimelineRelations.NavigationId(r)==pattern.id),"pattern nested declaration navigation stays at container");
        var second=T("second-pattern",7);second.kind="pattern";second.children.Add(T("nested",0));state.tracks.Add(second);Check(TimelineRelations.Resolve(state,"nested")==null,"same child ID in separate patterns remains non-global");
        var duplicate=T("source",8);state.tracks.Add(duplicate);Check(TimelineRelations.Resolve(state,"source")==null,"duplicate top ID safely rejects navigation");
        Language.Select(System.IO.Directory.GetCurrentDirectory(),"zh-CN");
    }
    private static string Hash(byte[] value){using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(value)).Replace("-","").ToLowerInvariant();}
    private static void Scene(string input,string output)
    {
        string path=System.IO.Path.GetFullPath(input);if(path==System.IO.Path.GetFullPath(output))throw new ArgumentException("Read-only source cannot be the output");string raw=System.IO.File.ReadAllText(path);var state=Json.Read<EditorState>(raw);string before=Json.Write(state);
        Check(state!=null&&state.tracks!=null,"scene deserialized");
        Language.Select(System.IO.Directory.GetCurrentDirectory(),"en");
        var records=new List<object>();int spawnRules=0,linked=0;
        foreach(var track in state.tracks){
            Check(!String.IsNullOrEmpty(track.id),"top-level ID retained");
            var refs=TimelineRelations.References(state,track);string timing=TimelineRelations.Timing(state,track);Check(!String.IsNullOrEmpty(timing),"declaration timing available");
            foreach(bool bullet in new[]{false,true})foreach(var action in (bullet?track.bulletActions:track.spawnActions).Where(a=>a.type=="spawn")){spawnRules++;if(TimelineRelations.LiveCopy(action,bullet))linked++;Check(TimelineRelations.SpawnDetail(track,action,bullet,0).Contains(Language.Text("实时联动副本"))==TimelineRelations.LiveCopy(action,bullet),"linked-copy label matches runtime branch");}
            var entries=new List<object>();foreach(var reference in refs){string id=TimelineRelations.NavigationId(reference);var resolved=TimelineRelations.Resolve(state,id);Check(id!=null||resolved==null,"informational references never navigate");entries.Add(new{relation=reference.relation,id=reference.id,name=TimelineRelations.ReferenceName(state,reference),detail=reference.detail,navigationId=id,resolved=resolved!=null});}
            records.Add(new{id=track.id,kind=track.kind,clock=TimelineRelations.LocalDefinition(state,track)?"template-local":track.kind=="pattern"?"pattern-container":"song",timing=timing,referenceCount=refs.Count,references=entries});
        }
        int nested=0;var queue=new Queue<Track>(state.tracks.Where(t=>t.kind=="pattern").SelectMany(t=>t.children));var seen=new HashSet<Track>();while(queue.Count>0){var t=queue.Dequeue();if(!seen.Add(t))continue;nested++;if(t.kind=="pattern")foreach(var child in t.children)queue.Enqueue(child);}
        Check(before==Json.Write(state),"read-only inspection preserves full model");Check(raw==System.IO.File.ReadAllText(path),"source bytes unchanged");
        var result=new{schemaVersion=1,status="passed",checks=checks,sourcePath=path,sourceSha256=Hash(System.IO.File.ReadAllBytes(path)),trackCount=state.tracks.Count,nestedDefinitionCount=nested,fps=state.fps,duration=state.duration,counts=new{ordinaryControllers=state.tracks.Count(TimelineRelations.Controller),eventPresets=state.tracks.Count(EventPresets.IsEvent),scripts=state.tracks.Count(t=>t.kind=="script"),templateRoots=state.tracks.Count(t=>t.templateOnly),localDefinitions=state.tracks.Count(t=>TimelineRelations.LocalDefinition(state,t)),spawnRules=spawnRules,linkedSpawnRules=linked,sourceSelectors=state.tracks.Count(t=>TimelineRelations.UsesSourceSelector(t)&&!String.IsNullOrEmpty(t.targetSource)),tagSelectors=state.tracks.Count(t=>TimelineRelations.UsesSourceSelector(t)&&!String.IsNullOrEmpty(t.targetTag)),patterns=state.tracks.Count(t=>t.kind=="pattern")},tracks=records,limitations=new[]{"Declaration inspection only; no native UI, GameMaker, arbitrary GML or particle simulation executed.","Nested declarations navigate to their outer pattern, not a runtime instance."}};
        System.IO.File.WriteAllText(output,Json.Write(result));Console.WriteLine("PASS "+checks+" read-only scene reference checks");
    }
    private static void Ui()
    {
        var s=Fixture();var target=s.tracks[3];var old=s.tracks[1];old.keyframes.Add(new Keyframe{frame=20,property="x"});string before=Json.Write(s);
        using(var timeline=new TimelineCanvas{State=s,Size=new Size(900,450),SelectionKeyframesOnly=true,Frame=123}){
            timeline.CreateControl();timeline.RefreshLayout();timeline.SetTrackSelection(new[]{old});timeline.SelectOnly(old.keyframes[0]);foreach(var t in s.tracks)timeline.SetExpanded(t,false);
            Track callback=null;timeline.SelectTrack=(t,k)=>{callback=t;Check(k==null,"navigation clears primary key");};
            Check(timeline.RevealReference(target.id),"multi-hop recursive reference revealed");Check(callback==target&&timeline.SelectedTrack==target,"normal selection callback receives exact track");Check(timeline.SelectedTracks.SetEquals(new[]{target})&&timeline.SelectedKeys.Count==0,"old hidden selections cleared");Check(timeline.Frame==123,"local template never seeks song clock");Check(before==Json.Write(s),"navigation leaves all scene data and order unchanged");
            Check(!timeline.ClipBounds(target).IsEmpty,"referenced template has real projected hit-test bounds");
            using(var image=new Bitmap(900,450)){timeline.DrawToBitmap(image,timeline.ClientRectangle);image.Save("reference-navigation.png");}
            Check(!timeline.RevealReference("missing")&&timeline.SelectedTrack==target,"missing reference does not change selection");
            timeline.HideTrackRows(new[]{s.tracks[0],s.tracks[1],target});Check(timeline.RevealReference(target.id)&&!timeline.IsTrackFiltered(target)&&timeline.Frame==123,"filtered reference revealed without seeking song time");Check(before==Json.Write(s),"filtered reference reveal is scene-read-only");
            var snapshot=s.Snapshot();timeline.State=snapshot;Check(timeline.RevealReference("leaf")&&timeline.SelectedTrack==snapshot.tracks[3],"navigation after undo-like replacement resolves fresh track");
            // A child of a bound group definition lacks a normal standalone row.
            var group=snapshot.tracks[2];group.kind="group";var child=T("child",5);child.parentId=group.id;snapshot.tracks.Add(child);timeline.RefreshLayout();Check(timeline.RevealReference("child"),"bound child gets exact editable definition card");Check(!timeline.ClipBounds(child).IsEmpty,"bound child card uses real layout");
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;typeof(TimelineCanvas).GetField("dragMode",flags).SetValue(timeline,"bar");Check(!timeline.RevealReference("a")&&timeline.SelectedTrack==child,"navigation refuses mid-edit gesture");typeof(TimelineCanvas).GetField("dragMode",flags).SetValue(timeline,null);
        }
        Console.WriteLine("Native-control checks: "+checks);
    }
    [STAThread] private static int Main(string[] args){try{if(args.Length==3&&args[0]=="--scene"){Scene(args[1],args[2]);return 0;}if(args.Contains("--ui"))Ui();else Pure();Console.WriteLine("PASS");return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
