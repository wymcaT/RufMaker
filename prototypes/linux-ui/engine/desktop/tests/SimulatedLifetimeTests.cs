using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
internal static class SimulatedLifetimeTests
{
    private static int checks;
    private static void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
    private static Track E(string id="source"){return new Track{id=id,name="同名",kind="emitter",start=0,end=0,count=1,interval=5,life=2,speed=0,x=100,y=100,deathEffect="none",advancedBullets=true};}
    private static EditorState S(params Track[] tracks){var s=new EditorState{duration=40,tracks=tracks.ToList()};Motion.Normalize(s);return s;}
    private static LifetimeResult Run(EditorState state,int end=20,LifetimeOptions options=null,RoomData room=null,Dictionary<string,SpriteMeta> metadata=null){return SimulatedLifetime.Analyze(new LifetimeSnapshot(state,room??new RoomData(),metadata),options??new LifetimeOptions{EndFrame=end,MaxSeconds=60},CancellationToken.None);}
    private static LifetimeRow Row(LifetimeResult r,string id,string category="飞行弹幕"){return r.Rows.Single(x=>x.Id==id&&x.Category==category);}
    private static string Span(LifetimeSeries s){return String.Join(",",s.Intervals.Select(i=>i.First+"-"+i.Last+(i.RightCensored?"+":"")));}
    private static void Main(string[] args)
    {
        if(args.Length>0&&(args[0]=="--smoke"||args[0]=="--bounded-smoke")){
            bool bounded=args[0]=="--bounded-smoke";int offset=bounded?2:0;
            if(args.Length!=5+offset)throw new ArgumentException("--smoke SCENE ROOM METADATA OUTPUT; --bounded-smoke SECONDS END SCENE ROOM METADATA OUTPUT");
            var input=Json.Read<EditorState>(System.IO.File.ReadAllText(args[1+offset]));
            var roomInput=Json.Read<RoomData>(System.IO.File.ReadAllText(args[2+offset]));
            var masks=Json.Read<Dictionary<string,SpriteMeta>>(System.IO.File.ReadAllText(args[3+offset]));
            var output=SimulatedLifetime.Analyze(new LifetimeSnapshot(input,roomInput,masks),LifetimeOptions.ForWindow(bounded?Int32.Parse(args[2]):input.duration,bounded?Double.Parse(args[1],System.Globalization.CultureInfo.InvariantCulture):15),CancellationToken.None);
            System.IO.File.WriteAllText(args[4+offset],Json.Write(output));
            Console.WriteLine("SMOKE stop="+output.Stop+" last_frame="+output.LastFrame+" requested_end="+output.RequestedEnd+" seconds="+output.Seconds+" rows="+output.Rows.Count+" error="+output.Error);return;
        }
        var e=E();e.end=10;var state=S(e);string before=Json.Write(state);var r=Run(state);
        Check(r.Complete&&r.LastFrame==20,"full requested window sampled");Check(Span(Row(r,e.id).Alive)=="0-1,5-6,10-11","separate occupied runs, not enclosing duration");Check(Row(r,e.id).Alive.Peak==1&&Row(r,e.id).Alive.Samples==6,"counts are observations not declared span");Check(Json.Write(state)==before,"analysis does not mutate source");
        r=Run(state,10);Check(Span(Row(r,e.id).Alive)=="0-1,5-6,10-10+","horizon censors only final still-occupied run");
        e=E();e.count=2;e.indexDelay=5;r=Run(S(e),7);Check(Span(Row(r,e.id).Alive)=="0-1,5-6","delayed births exclude scheduled entries");Check(Span(Row(r,e.id,"已排程 / 尚未出生").Alive)=="0-4","scheduling separately reported");
        e=E();e.life=10;e.bulletActions.Add(new BulletAction{type="clear",start=3,duration=0});r=Run(S(e),10);Check(Span(Row(r,e.id).Alive)=="0-2","clear action truncates observed lifetime");
        e.deathEffect="fade";e.deathFrames=3;r=Run(S(e),10);Check(Span(Row(r,e.id).Alive)=="0-5","destroy transition remains alive");Check(Span(Row(r,e.id).Fading)=="3-5","transition separately observed");
        e=E();e.alpha=0;e.life=4;r=Run(S(e),5);Check(Span(Row(r,e.id).Alive)=="0-3"&&Span(Row(r,e.id).Transparent)=="0-3","transparent is not dead");
        e=E();e.fadeInFrames=2;r=Run(S(e),4);Check(Span(Row(r,e.id).Transparent)=="0-0","birth fade has zero effective alpha while alive");
        e=E();e.life=100;e.lifetimeMode="room";e.x=799;e.speed=2;r=Run(S(e),5);Check(Span(Row(r,e.id).Alive)=="0-0","room boundary observed from motion not life");
        e=E();e.life=100;var control=new Track{id="clear",kind="control",start=2,end=2,controlAction="clear",targetSource=e.id};r=Run(S(e,control));Check(Span(Row(r,e.id).Alive)=="0-1","full scene controller changes lifetime");
        e=E();e.enabled=false;var twin=E("twin");r=Run(S(e,twin));Check(Row(r,e.id).Alive.Intervals.Count==0&&Row(r,twin.id).Alive.Samples==2,"same names separate by stable ID including disabled source");
        var obj=new Track{id="object",kind="object",start=2,end=5};r=Run(S(obj),8);Check(Span(Row(r,obj.id,"直接物件 / 整体成员").Alive)=="2-5","ordinary object availability not silently absent");
        var rod=new Track{id="rod",kind="rod",start=0,end=5,releaseFrame=3,count=2,life=3,speed=0};r=Run(S(rod),8);Check(Span(Row(r,rod.id,"直接物件 / 整体成员").Alive)=="0-2","fixed rod ends at release boundary");Check(Span(Row(r,rod.id).Alive)=="3-5"&&Row(r,rod.id).Alive.Peak==2,"released rod measured independently");
        rod.releaseFrame=-1;var clear=new Track{id="clear-all",kind="control",eventPreset="clear-danmaku",start=2,end=2};r=Run(S(rod,clear),8);Check(Span(Row(r,rod.id,"直接物件 / 整体成员").Alive)=="0-1","simulation hide events remove direct member availability");
        var group=new Track{id="group",kind="group",start=2,end=3};e=E();e.end=8;e.interval=1;e.life=2;e.parentId=group.id;r=Run(S(group,e),8);Check(Span(Row(r,e.id).Alive)=="2-4","parent gating and child survival beyond parent");
        var parent=E("mother");parent.life=10;state=S(parent);var action=GenerationPresets.Apply(state,parent,GenerationPresets.ChildBurst);action.start=2;var child=state.tracks.Single(t=>t.id==action.templateId);child.count=1;child.life=4;child.lifetimeMode="fixed";child.deathEffect="none";Motion.Normalize(state);r=Run(state,10);
        Check(Span(Row(r,child.id).Alive)=="2-5","generated child survives template birth/controller span");Check(Row(r,child.id).MaxGeneration==1,"generated definition identity and generation preserved");Check(Span(Row(r,child.id,"生成模板容器").Alive)=="2-2","template container distinct from surviving children");
        var template=new Track{id="fixed",kind="object",start=0,end=2,templateOnly=true};obj=new Track{id="host",kind="object",start=0,end=8};obj.spawnActions.Add(new BulletAction{type="spawn",templateId=template.id,start=1,duration=0,maxGeneration=2});r=Run(S(obj,template),8);Check(Span(Row(r,template.id,"生成的固定成员").Alive)=="1-3","generated fixed members use world liveness");Check(Row(r,template.id,"直接物件 / 整体成员").Alive.Intervals.Count==0,"template definition isn't active direct object");
        var pattern=new Track{id="clip",kind="pattern",start=0,end=10,repeatCount=2,repeatGap=2,children=new List<Track>{E("inside")}};r=Run(S(pattern),10);Check(r.Rows.Any(x=>x.Id=="clip/0/inside")&&r.Rows.Any(x=>x.Id=="clip/1/inside"),"expanded source IDs kept exact without suffix guessing");Check(r.Rows.Any(x=>x.Id=="clip"&&x.Category.Contains("声明")),"pattern container is declaration not child lifetime");
        e=E();e.previewHidden=true;r=Run(S(e),3);Check(Row(r,e.id).Alive.Samples==2&&r.Warnings.Any(w=>w.Contains("隐藏")),"analysis ignores UI-only visibility filter and labels full scene");
        var script=new Track{id="code",name="原生脚本",kind="script",start=0,end=1,code="instance_destroy();"};var native=new Track{id="native",kind="native-instance",start=0,end=20};r=Run(S(script,native),3);Check(Row(r,script.id,"声明信息（非存活）").Unknown&&Row(r,native.id,"声明信息（非存活）").Unknown,"native/GML not labeled zero-lifetime");Check(r.Warnings.Any(w=>w.Contains("未执行")),"native warnings explicit");
        e=E();e.despawnBounds="sprite-mask";r=Run(S(e));Check(r.Stop=="error"&&r.LastFrame==-1&&r.Error.Contains("metadata"),"missing required sprite metadata stops without invented result");
        var room=new RoomData();room.views=new[]{new RoomView{x=2}};state=S(E());state.nativePaths["p"]=new GmPath{points=new List<GmPathPoint>{new GmPathPoint{x=12}}};var metadata=new Dictionary<string,SpriteMeta>{{"sprBullet",new SpriteMeta{colliders=new[]{new SpriteCollider{left=1}}}}};var snap=new LifetimeSnapshot(state,room,metadata);state.tracks[0].life=99;state.nativePaths["p"].points[0].x=88;room.views[0].x=9;metadata["sprBullet"].colliders[0].left=9;Check(snap.Scene.tracks[0].life==2&&snap.Scene.nativePaths["p"].points[0].x==12&&snap.Room.views[0].x==2&&snap.Metadata["sprBullet"].colliders[0].left==1,"full snapshot isolates scene/path/room/mask data");
        e=E();e.nativePath="missing-path";r=Run(S(e),3);Check(r.Stop=="error"&&r.LastFrame==-1,"missing native path refuses false success");
        var film=new NativeFilmInfo{fps=50,views=new[]{new NativeViewPose{enabled=true,x=0,y=0,w=800,h=608}}};
        r=SimulatedLifetime.Analyze(new LifetimeSnapshot(S(E()),new RoomData(),null,film),new LifetimeOptions{EndFrame=3},CancellationToken.None);Check(r.Warnings.Any(w=>w.Contains("最后一个镜头")),"camera horizon extrapolation explicit");
        var first=SimulatedLifetime.Analyze(snap,new LifetimeOptions{EndFrame=10},CancellationToken.None);var second=SimulatedLifetime.Analyze(snap,new LifetimeOptions{EndFrame=10},CancellationToken.None);Check(Span(Row(first,"source").Alive)==Span(Row(second,"source").Alive),"same snapshot can be analyzed again deterministically");
        e=E();e.life=50;state=S(e);var cancel=new CancellationTokenSource();r=SimulatedLifetime.Analyze(new LifetimeSnapshot(state,new RoomData()),new LifetimeOptions{EndFrame=100},cancel.Token,f=>{if(f==0)cancel.Cancel();});Check(r.Stop=="cancelled"&&r.LastFrame==0&&Span(Row(r,e.id).Alive)=="0-0+","cancellation preserves only completed frames with censoring");
        r=Run(state,100,new LifetimeOptions{EndFrame=100,MaxFrames=3});Check(r.Stop=="frame-limit"&&r.LastFrame==2,"frame budget partial result");
        r=Run(state,100,new LifetimeOptions{EndFrame=100,MaxSamples=2});Check(r.Stop=="sample-limit"&&r.LastFrame==1&&Row(r,e.id).Alive.Samples==2,"sample budget commit is atomic");
        e=E();e.end=10;e.life=1;r=Run(S(e),20,new LifetimeOptions{EndFrame=20,MaxIntervals=1});Check(r.Stop=="interval-limit"&&r.LastFrame==4&&Span(Row(r,e.id).Alive)=="0-0","interval limit doesn't extend previous run or commit next");
        e=E();e.start=e.end=1;e.count=30;state=S(e);r=Run(state,10,new LifetimeOptions{EndFrame=10,MaxEntities=10});Check(r.Stop=="entity-limit"&&r.LastFrame==0&&Row(r,e.id).Alive.Samples==0,"mid-step entity abort discards whole partial frame");
        r=Run(state,10,new LifetimeOptions{EndFrame=10,MaxOperations=6});Check(r.Stop=="work-limit"&&r.LastFrame<1,"deterministic work guard caps uncommitted spawning");
        obj=new Track{id="clone-host",kind="object",start=0,end=5};template=new Track{id="clone-template",kind="object",start=0,end=5,templateOnly=true};obj.spawnActions.Add(new BulletAction{type="spawn",templateId=template.id,start=1,duration=0,maxGeneration=2});template.keyframes.Add(new Keyframe{frame=0,property="x",value=20});
        r=Run(S(obj,template),5,new LifetimeOptions{EndFrame=5,MaxCloneItems=1});Check(r.Stop=="clone-limit"&&r.LastFrame==0,"definition clone budget checked before retained allocation and partial frame discarded");
        bool rejected=false;try{var huge=new Track{id="huge",kind="pattern",repeatCount=Int32.MaxValue,children=new List<Track>{E()}};new LifetimeSnapshot(S(huge),new RoomData());}catch(InvalidOperationException){rejected=true;}Check(rejected,"huge pattern rejected before expansion/clone");
        rejected=false;try{e=E();e.bulletActions.Add(new BulletAction{type="spawn",templateId=e.id,maxGeneration=100000});new LifetimeSnapshot(S(e),new RoomData());}catch(InvalidOperationException){rejected=true;}Check(rejected,"deep recursion bounded before simulation");
        rejected=false;try{e=E();for(int i=0;i<100;i++)e.keyframes.Add(new Keyframe{frame=i,property="x",value=10});var heavy=new Track{id="heavy",kind="pattern",repeatCount=3000,children=new List<Track>{e}};new LifetimeSnapshot(S(heavy),new RoomData());}catch(InvalidOperationException){rejected=true;}Check(rejected,"preflight multiplies retained key/action weight by pattern repeats");
        e=E();e.life=10;e.trailMode="fade";e.trailLife=100;var plainTrail=Run(S(e),12);Check(Span(Row(plainTrail,e.id).Alive)=="0-9","render-only trail pruning does not change survival");
        var formation=new Track{id="formation",kind="rod",start=0,end=10,count=2,life=4,releaseFrame=2,trailMode="fade"};var releaseTrail=Run(S(formation),10);Check(Span(Row(releaseTrail,formation.id).Alive)=="2-5","released trail members keep full lifetime");
        state=S(E());var w1=new BulletWorld(state.Snapshot());var w2=new BulletWorld(state.Snapshot());int observed=0;w2.AnalysisCheckpoint=()=>observed++;for(int f=0;f<8;f++){w1.Seek(f);w2.Seek(f);Check(w1.bullets.Count==w2.bullets.Count&&String.Join(";",w1.bullets.Select(b=>b.birth+":"+b.x+":"+b.dead))==String.Join(";",w2.bullets.Select(b=>b.birth+":"+b.x+":"+b.dead)),"no-op guard parity at "+f);}Check(observed>0,"analysis hook exercised");
        // Null/default, no-op analysis hooks and disabled history checkpoints must
        // preserve generated/linked/copy simulation as well as simple emitters.
        obj=new Track{id="linked",kind="rod",start=0,end=4,count=2,speed=1};state=S(obj);var copy=CopyGeneration.Apply(state,obj);copy.copyCount=1;copy.copyUp=false;copy.copyDown=false;copy.copyLeft=false;copy.copyRight=true;copy.spawnRepeat=1;copy.copyLinked=true;Motion.Normalize(state);
        w1=new BulletWorld(state.Snapshot());w2=new BulletWorld(state.Snapshot());w2.AnalysisCheckpoint=()=>{};w2.AnalysisCloneCheckpoint=tracks=>{};w2.CheckpointBudget=0;w2.CheckpointLimit=0;
        for(int f=0;f<6;f++){w1.Seek(f);w2.Seek(f);Func<BulletWorld,string> signature=w=>String.Join(";",w.bullets.Select(b=>b.source.id+":"+b.birth+":"+b.x+":"+b.y+":"+b.dead+":"+b.kinematic+":"+b.generation));Check(signature(w1)==signature(w2)&&w1.Templates.InstanceCount==w2.Templates.InstanceCount,"guard/checkpoint parity for linked template frame "+f);}
        Check(w2.CheckpointCount==0&&w1.CheckpointCount>0,"zero analysis checkpoint budget avoids snapshots only in opt-in world");
        var quick=LifetimeOptions.ForWindow(100,15);var thorough=LifetimeOptions.ForWindow(100,180);Check(quick.MaxSeconds==15&&thorough.MaxSeconds==180&&thorough.MaxSamples==quick.MaxSamples*12&&thorough.MaxOperations==quick.MaxOperations*12,"explicit time budget scales compute limits");Check(quick.MaxEntities==thorough.MaxEntities&&quick.MaxCloneItems==thorough.MaxCloneItems&&quick.MaxIntervals==thorough.MaxIntervals,"extended compute budget keeps fixed memory caps");
        // Progress distinguishes a completed prefix from a frame still executing.
        var updates=new List<LifetimeProgress>();state=S(E());
        r=SimulatedLifetime.Analyze(new LifetimeSnapshot(state,new RoomData()),new LifetimeOptions{EndFrame=4},CancellationToken.None,null,p=>updates.Add(p));
        Check(updates.Count>=2&&updates[0].LastCompletedFrame==-1&&updates[0].ActiveFrame==-1&&updates[0].Phase=="prepare","initial progress has no accepted frame");
        var terminal=updates.Last();Check(terminal.LastCompletedFrame==4&&terminal.RequestedEnd==4&&terminal.Phase=="complete"&&terminal.ElapsedSeconds>=updates[0].ElapsedSeconds,"terminal progress reports exact committed prefix");
        Check(updates[0].LastCompletedFrame==-1&&updates[0].Phase=="prepare","published progress snapshot remains immutable");
        r=SimulatedLifetime.Analyze(new LifetimeSnapshot(state,new RoomData()),new LifetimeOptions{EndFrame=4},CancellationToken.None,null,p=>{throw new InvalidOperationException("observer failed");});
        Check(r.Stop=="error"&&r.LastFrame==-1&&r.Error=="observer failed","initial observer failure returns an error result");
        r=SimulatedLifetime.Analyze(new LifetimeSnapshot(state,new RoomData()),new LifetimeOptions{EndFrame=0},CancellationToken.None,null,p=>{if(p.Phase=="complete")throw new InvalidOperationException("terminal failed");});
        Check(r.Stop=="error"&&r.LastFrame==0&&r.Error=="terminal failed"&&Span(Row(r,"source").Alive)=="0-0+","terminal observer failure preserves completed prefix without escaping");
        r=SimulatedLifetime.Analyze(new LifetimeSnapshot(state,new RoomData()),new LifetimeOptions{EndFrame=4},CancellationToken.None,f=>{throw new InvalidOperationException("legacy failed");});
        Check(r.Stop=="error"&&r.LastFrame==0&&Span(Row(r,"source").Alive)=="0-0+","legacy observer failure remains accepted-prefix error");
        // One expensive frame generates thousands of particles. Cancellation is
        // requested from its throttled heartbeat, not from a completed-frame callback.
        var costly=new List<Track>();for(int i=0;i<20;i++){var source=E("costly-"+i);source.start=source.end=1;source.count=300;source.life=100;costly.Add(source);}
        for(int i=0;i<150;i++)costly.Add(new Track{id="cost-control-"+i,kind="control",start=1,end=1,controlAction="speed",controlValue=1,includeFuture=true});
        state=S(costly.ToArray());cancel=new CancellationTokenSource();LifetimeProgress interrupted=null;
        r=SimulatedLifetime.Analyze(new LifetimeSnapshot(state,new RoomData()),new LifetimeOptions{EndFrame=2,MaxSeconds=10},cancel.Token,null,p=>{if(p.ActiveFrame==1&&p.Phase=="simulate"){interrupted=p;cancel.Cancel();}});
        Check(interrupted!=null&&r.Stop=="cancelled"&&r.LastFrame==0&&interrupted.LastCompletedFrame==0,"expensive-frame heartbeat cancels during simulation with prior prefix intact");
        Check(r.Rows.All(x=>x.Alive.Samples==0&&x.MaxGeneration==0),"interrupted expensive frame contributes no counts or generations");
        var mutable=E();Check(Motion.Base(mutable,"speed")==0&&Motion.Base(mutable,"notAField")==0,"metadata cache retains numeric and missing-field lookup");mutable.speed=12;Check(Motion.Base(mutable,"speed")==12,"metadata cache never stores mutable field values");
        var gate=new LifetimeRequestGate();int old=gate.Begin(),next=gate.Begin();Check(!gate.Accept(old,true)&&gate.Accept(next,true)&&!gate.Accept(next,false),"request and scene generation gate");gate.Invalidate();Check(!gate.Accept(next,true),"invalidated results rejected");next=gate.Begin();gate.Close();Check(!gate.Accept(next,true),"close rejects late callbacks");
        Language.Select(System.IO.Directory.GetCurrentDirectory(),"en");Check(Language.Text("模拟存活区间")=="Simulated survival intervals","English dialog entry");Check(Language.Text(SimulatedLifetimeView.ScopeNote).StartsWith("Observes the full C# scene"),"English scope note");
        Console.WriteLine("PASS "+checks+" pure simulated lifetime checks");
    }
}
