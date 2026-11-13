using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

internal static class OrbitSelectorTests
{
    static int checks;
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static void Near(double a,double b,string message){Check(Math.Abs(a-b)<.00001,message+": "+a+" != "+b);}
    static EditorState Normalize(EditorState s){Motion.Normalize(s);RandomParameters.Attach(s);return s;}
    static BulletWorld World(EditorState s,int frame){var w=new BulletWorld(Normalize(s),new RoomData{width=800,height=608},null);w.Seek(frame);return w;}
    static SimBullet[] Live(BulletWorld w,string id=null){return w.bullets.Where(b=>!b.dead&&(id==null||b.source.id==id)).OrderBy(b=>b.source.id).ThenBy(b=>b.birth).ThenBy(b=>b.index).ToArray();}
    static Track T(string id,string kind){return new Track{id=id,name=id,kind=kind,lane=0,start=0,end=10,x=100,y=100,life=50,lifetimeMode="fixed",count=2,speed=0,interval=1};}
    static Track Orbit(string key="group-orbit"){var e=T("orbit","control");EventPresets.ApplyPreset(e,key);e.start=e.end=2;e.eventGroup="family";e.eventCenterX=e.eventCenterY=0;e.eventOrbitSpeed=3;e.eventResetLife=false;return e;}
    static EditorState Scene(params Track[] tracks){return new EditorState{fps=50,duration=20,tracks=tracks.ToList(),timelineGroups=new List<TimelineGroup>{new TimelineGroup{id="family",name="Family",lanes=new List<int>{0}},new TimelineGroup{id="other",name="Other",lanes=new List<int>{9}}}};}
    static void RealProbe(string fixture,List<object> report)
    {
        foreach(string key in new[]{"group-orbit","orbit-bullets"})foreach(string select in new[]{"both","source","tag","none","wrong-source","wrong-tag","wrong-group","alias","unsupported"}){
            var s=Normalize(Json.Read<EditorState>(File.ReadAllText(fixture)));var e=s.tracks.Single(t=>t.id=="select-fast");e.eventPreset=key;
            if(select=="source")e.targetTag="";if(select=="tag")e.targetSource="";if(select=="none")e.targetSource=e.targetTag="";
            if(select=="wrong-source")e.targetSource="slow";if(select=="wrong-tag")e.targetTag="slow";if(select=="wrong-group")e.eventGroup="slow-template";
            if(select=="alias"){s.tracks.Single(t=>t.id=="fast").clipSourceId="trimmed-fast";e.targetSource="trimmed-fast";}
            if(select=="unsupported"){e.targetMinAge=900;e.targetMaxAge=901;e.targetVolley=77;e.targetParity="odd";e.includeFuture=false;}
            var w=World(s,2);var fast=Live(w,"fast").Single();var slow=Live(w,"slow").Single();bool match=select!="wrong-source"&&select!="wrong-tag"&&!(select=="wrong-group"&&key=="group-orbit");
            Check(fast.orbiting==match,key+" "+select+" fast selection");Check(slow.orbiting==(select=="none"),key+" "+select+" slow selection");Check(fast.scopeGroup=="family"&&slow.scopeGroup=="family","inherited scope preserved");
            report.Add(new{key=key,selector=select,fast=fast.orbiting,slow=slow.orbiting});
        }
    }
    static EditorState Mixed(string source,string tag,string key="group-orbit")
    {
        var seed=T("seed","group");seed.start=seed.end=0;seed.spawnActions.Add(new BulletAction{type="spawn",templateId="bundle",start=0,inheritAngle=false});
        var bundle=T("bundle","group");bundle.templateOnly=true;
        var root=T("root","object");root.parentId="bundle";root.tag="selected";root.lane=9;
        var fast=T("fast-rod","rod");fast.parentId="bundle";fast.tag="selected";
        var slow=T("slow-rod","rod");slow.parentId="bundle";slow.tag="other";
        var obj=T("slow-object","object");obj.parentId="bundle";obj.tag="other";
        var stream=T("stream","emitter");stream.parentId="bundle";stream.tag="stream";stream.count=1;
        var future=T("future","object");future.parentId="bundle";future.tag="selected";future.start=4;
        var futureRod=T("future-rod","rod");futureRod.parentId="bundle";futureRod.tag="other";futureRod.start=4;
        var e=Orbit(key);e.targetSource=source;e.targetTag=tag;
        return Scene(seed,bundle,root,fast,slow,obj,stream,future,futureRod,e);
    }
    static void SurvivingMembers()
    {
        foreach(string key in new[]{"group-orbit","orbit-bullets"})foreach(string source in new[]{"","root","missing"}){
            var s=Mixed(source,"selected",key);var e=s.tracks.Last();var w=World(s,1);var root=Live(w,"root").Single();var fast=Live(w,"fast-rod");var slow=Live(w,"slow-rod");var obj=Live(w,"slow-object").Single();int owner=root.templateOwner;
            w.Seek(2);Check(w.Templates.InstanceCount==1,"selected event keeps mixed owner");
            Check(root.orbiting==(source!="missing")&&!root.dead,"root selection "+key+"/"+source);Check(root.kinematic==(source=="missing"),"root fixed state");
            Check(fast.All(b=>b.orbiting==(source=="")),"rod member source intersection");Check(slow.All(b=>b.kinematic&&!b.orbiting&&!b.dead),"sibling rods survive fixed");Check(obj.kinematic&&!obj.dead,"sibling object survives fixed");
            int count=w.bullets.Count;w.Templates.ResetRods(2,e);w.Templates.ResetRods(2,e);Check(w.bullets.Count==count,"repeated prepare creates no duplicates");
            var snapshot=w.Templates.Capture();w.Templates.Restore(snapshot);w.Templates.ResetRods(2,e);Check(w.bullets.Count==count,"snapshot restore retains selective member hiding");
            if(source!="missing"){Check(!w.Templates.Visible(root.source,0,owner),"released object slot hidden");Check(w.Templates.Visible(root.source,-1,owner),"released object does not hide descendants");}
            w.Seek(5);Check(Live(w,"stream").Length==6,"emitter descendant continues spawning");Check(Live(w,"future").Single().kinematic,"future matching object remains fixed after one-shot event");Check(Live(w,"future-rod").All(b=>b.kinematic),"future unrelated rods remain fixed");Check(Live(w,"root").Length==1&&Object.ReferenceEquals(root,Live(w,"root").Single()),"released object never resurrects");
            Check(slow.All(b=>Live(w,"slow-rod").Contains(b)),"unrelated fixed identity retained");
            var expected=Live(w).Select(b=>new{b.source.id,b.birth,b.index,b.x,b.y,b.kinematic,b.orbiting}).ToArray();w.Seek(1);w.Seek(5);var actual=Live(w).Select(b=>new{b.source.id,b.birth,b.index,b.x,b.y,b.kinematic,b.orbiting}).ToArray();Check(Json.Write(expected)==Json.Write(actual),"reverse seek reconstructs selective hidden state");
        }
        // A long event still receives later members under the old event timing.
        var ongoing=Mixed("","selected");ongoing.tracks.Last().end=5;var durationWorld=World(ongoing,5);Check(Live(durationWorld,"future").Single().orbiting,"ongoing orbit includes newly active matching source");Check(Live(durationWorld,"slow-object").Single().kinematic,"ongoing orbit keeps unrelated object fixed");
        // Missing selectors deliberately preserve legacy owner-wide disintegration.
        var defaults=Mixed("","");var defaultWorld=World(defaults,2);Check(defaultWorld.Templates.InstanceCount==0,"empty selectors retain legacy owner lifetime");Check(Live(defaultWorld).Where(b=>b.source.id!="stream").All(b=>b.orbiting&&!b.kinematic),"empty selectors release existing members");
        foreach(string key in new[]{"reset-motion","group-set"}){var legacy=Mixed("missing","missing");legacy.tracks.Last().eventPreset=key;Check(World(legacy,2).Templates.InstanceCount==0,"non-orbit events ignore ordinary selectors as before");}
    }
    static void RootRods()
    {
        var fast=T("fast","rod");fast.tag="fast";var slow=T("slow","rod");slow.tag="slow";var e=Orbit();e.targetSource="fast";e.targetTag="fast";var w=World(Scene(fast,slow,e),2);
        Check(Live(w,"fast").Length==2&&Live(w,"fast").All(b=>b.orbiting),"selected root rod materialized");Check(Live(w,"slow").Length==0&&w.Templates.Visible(slow,0),"unselected root rod not materialized or hidden");
        int count=w.bullets.Count;w.Templates.ResetRods(2,e);Check(w.bullets.Count==count,"root repeated prepare does not rematerialize");
        Check(w.Templates.Visible(fast,-1)&&!w.Templates.Visible(fast,0),"root hides only existing slots");
    }
    static void Persistence()
    {
        var source=T("source","emitter");source.tag="fast";source.start=source.end=0;source.interval=100;source.count=1;var e=Orbit("orbit-bullets");e.targetSource=source.id;e.targetTag="fast";var s=Normalize(Scene(source,e));
        foreach(var list in new[]{s.Snapshot().tracks,Json.Read<EditorState>(Json.Write(s)).tracks,BulletSystem.CloneTracks(s.tracks),TrackPreset.Capture("orbit",s.tracks,50).Instantiate(0,50)}){
            var emitter=list.Single(t=>t.kind=="emitter");var ev=list.Single(t=>t.kind=="control");Check(ev.targetSource==emitter.id&&ev.targetTag==emitter.tag,"snapshot/JSON/copy/preset keeps target and remaps id");Check(Live(World(Scene(list.ToArray()),2)).Single().orbiting,"persisted event remains effective");
        }
        var clip=T("clip","pattern");clip.repeatCount=2;clip.repeatGap=2;clip.children=new List<Track>{source,e};clip.end=10;var expanded=BulletSystem.Expand(Normalize(Scene(clip)));var emitters=expanded.tracks.Where(t=>t.kind=="emitter").ToArray();var events=expanded.tracks.Where(t=>t.kind=="control").ToArray();Check(events.Length==2&&emitters.Length==2,"clip copies expanded");for(int i=0;i<2;i++){Check(events[i].targetSource==emitters[i].id,"clip target remapped to corresponding copy");Check(EventPresets.Matches(events[i],emitters[i],"family"),"matching clip copy selected");Check(!EventPresets.Matches(events[i],emitters[1-i],"family"),"other clip copy excluded");}
        var world=World(expanded,events[1].start);Check(Live(world).All(b=>b.orbiting),"both clip-local events select own spawned source");
    }
    public static void Main(string[] args)
    {
        var report=new List<object>();RealProbe(args[0],report);SurvivingMembers();RootRods();Persistence();
        File.WriteAllText(args[1],Json.Write(new{status="passed",checks=checks,realFixture=report,nativeGameMaker="not run",windowsUi="not run"}));
        Console.WriteLine("PASS: "+checks+" orbit selector checks; real fixture, source/tag/group intersection, clip aliases, persistence/remapping, mixed templates, repeated prepare, legacy defaults and reverse seek.");
    }
}
