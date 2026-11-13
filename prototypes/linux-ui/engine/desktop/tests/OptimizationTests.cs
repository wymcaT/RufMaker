using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class OptimizationTests
{
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static EditorState Scene(int duration=24000)
    {var state=new EditorState{duration=duration,tracks=new List<Track>{new Track{id="source",name="source",kind="emitter",start=0,end=duration,count=12,interval=5,life=100,speed=2,advancedBullets=true,trailMode="fade",trailLife=18}}};Motion.Normalize(state);return state;}
    private static EditorState Copy(EditorState state){var next=state.Snapshot();Motion.Normalize(next);return next;}
    private static string Rows(BulletWorld world){return Json.Write(world.bullets.Select(b=>new{source=b.source.id,birth=b.birth,index=b.index,generation=b.generation,x=b.x,y=b.y,direction=b.direction,speed=b.speed,color=b.color.ToArgb(),alpha=b.alpha,scale=b.scale,dead=b.dead}).ToArray());}
    private static void Budget()
    {
        var state=Scene();for(int i=0;i<160;i++)state.tracks[0].keyframes.Add(new Keyframe{property="x",frame=i*10,value=i});var capture=new UndoSnapshot{state=state,room=new RoomData(),selectedTrack="source",selectedTracks=new[]{"source"}};
        var entry=new UndoEntry(capture);var history=new UndoHistory{LimitBytes=entry.EstimatedBytes*3+1024};
        for(int i=0;i<20;i++){state.tracks[0].alpha=i;history.Undo.Push(new UndoEntry(capture));}
        Check(history.Undo.Count==3&&history.RetainedBytes<=history.LimitBytes,"Undo must discard oldest entries within budget");
        Check(history.Undo.Pop().Restore().state.tracks[0].alpha==19&&history.Undo.Peek().Restore().state.tracks[0].alpha==18,"Undo order or recent value lost");
        history.Redo.Push(new UndoEntry(capture));history.LimitBytes=entry.EstimatedBytes*2+1024;Check(history.Undo.Count+history.Redo.Count==2&&history.RetainedBytes<=history.LimitBytes,"Undo and Redo do not share the budget");
        history.LimitBytes=1;Check(history.Undo.Count+history.Redo.Count==1,"One oversized nearest operation should remain available");history.Undo.Clear();history.Redo.Clear();Check(history.RetainedBytes==0,"Clearing history leaked estimated bytes");
        Console.WriteLine("PASS: shared Undo/Redo budget, eviction order, independent restored values and oversized nearest operation.");
    }
    private static void Cache(string folder)
    {
        var room=new RoomData();var state=Scene();var late=new Track{id="late",name="late",kind="emitter",start=4000,end=10000,count=3,interval=20,life=200,speed=1,advancedBullets=true,trailMode="off",keyframes=new List<Keyframe>{new Keyframe{property="x",frame=4000,value=100},new Keyframe{property="x",frame=5000,value=200},new Keyframe{property="x",frame=6000,value=300}}};state.tracks.Add(late);Motion.Normalize(state);
        using(var cache=new PreviewCache()){
            cache.UseScene(state,room);cache.bulletWorld=new BulletWorld(state,room);var world=cache.bulletWorld;var clock=Stopwatch.StartNew();world.Seek(7000);double coldMs=clock.Elapsed.TotalMilliseconds;long steps=world.SimulatedSteps;
            var renamed=Copy(state);renamed.tracks[0].name="Renamed source";clock.Restart();cache.UseScene(renamed,room.Snapshot());cache.bulletWorld.Seek(7000);double renameMs=clock.Elapsed.TotalMilliseconds;
            Check(ReferenceEquals(world,cache.bulletWorld)&&world.SimulatedSteps==steps,"Renaming invalidated simulation history");
            var stats=ObjectStatistics.Capture(renamed,room,7000,cache);Check(stats.sources.ContainsKey("Renamed source"),"Statistics kept the old source name");
            var camera=Copy(renamed);camera.tracks.Add(new Track{id="camera",kind="camera",start=2000,end=10000,x=400,y=304,cameraZoom=2});Motion.Normalize(camera);Check(PreviewRevision.SimulationFrom(renamed,camera,room,room)==-1,"Visual camera reset a simulation with no view lifetime");cache.UseScene(camera,room);Check(ReferenceEquals(world,cache.bulletWorld),"Visual camera did not reuse world");
            var changed=Copy(camera);changed.tracks.Single(t=>t.id=="late").keyframes.Add(new Keyframe{property="x",frame=6500,value=450});Motion.Normalize(changed);Check(PreviewRevision.SimulationFrom(camera,changed,room,room)==6000,"Changed key must invalidate its preceding interpolation segment");
            clock.Restart();cache.UseScene(changed,room);Check(ReferenceEquals(world,cache.bulletWorld)&&world.Frame>=5000&&world.Frame<6000,"Earlier checkpoints were not retained");world.Seek(7000);double partialMs=clock.Elapsed.TotalMilliseconds;long replayed=world.SimulatedSteps-steps;
            var fresh=new BulletWorld(Copy(changed),room);fresh.Seek(7000);Check(Rows(world)==Rows(fresh),"Partial history reuse changed bullet results");
            var view=Copy(changed);view.tracks[0].lifetimeMode="view";cache.UseScene(view,room);var viewWorld=cache.bulletWorld;Check(viewWorld==null,"Changing lifetime from frame zero must rebuild");
            cache.bulletWorld=new BulletWorld(view,room);cache.bulletWorld.Seek(7000);var cameraChanged=Copy(view);cameraChanged.tracks.Single(t=>t.kind=="camera").cameraZoom=3;Check(PreviewRevision.SimulationFrom(view,cameraChanged,room,room)==2000,"View lifetime must depend on camera from its start");cache.UseScene(cameraChanged,room);cache.bulletWorld.Seek(7000);var expected=new BulletWorld(Copy(cameraChanged),room);expected.Seek(7000);Check(Rows(cache.bulletWorld)==Rows(expected),"View-lifetime camera reuse diverged from full replay");
            File.WriteAllText(Path.Combine(folder,"cache-profile.json"),Json.Write(new{coldSeekMs=coldMs,renameSeekMs=renameMs,partialEditSeekMs=partialMs,partialReplaySteps=replayed,coldSteps=steps}));
        }
        var cancelled=new BulletWorld(Scene());try{cancelled.Seek(6000,()=>cancelled.Frame>=25);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}Check(cancelled.Frame==25,"Cancellation interrupted a partial game frame");cancelled.Seek(50);var uninterrupted=new BulletWorld(Scene());uninterrupted.Seek(50);Check(Rows(cancelled)==Rows(uninterrupted),"Cancelled seek could not resume deterministically");
        var checkpoints=new BulletWorld(Scene());checkpoints.Seek(20000);long prior=checkpoints.SimulatedSteps;checkpoints.Seek(6000);Check(checkpoints.SimulatedSteps-prior<2000,"Long reverse seek lost early checkpoints");Check(checkpoints.CheckpointCount<=48&&checkpoints.CheckpointBytes<=checkpoints.CheckpointBudget,"Checkpoint count/memory budget exceeded");checkpoints.CheckpointBudget=1024;checkpoints.Seek(6400);Check(checkpoints.CheckpointBytes<=1024,"Tiny checkpoint memory budget not respected");
        Console.WriteLine("PASS: metadata/camera cache reuse, partial-key replay, current statistics, camera lifetime dependency, atomic cancellation/resume, early and recent checkpoints with memory budget.");
    }
    private static string Sprites(string folder)
    {
        string project=Path.Combine(folder,"preview-assets"),sprite=Path.Combine(project,"sprites","dot");Directory.CreateDirectory(sprite);using(var image=new Bitmap(3,3)){using(var g=Graphics.FromImage(image))g.Clear(Color.White);image.Save(Path.Combine(sprite,"0.png"));}File.WriteAllText(Path.Combine(sprite,"sprite.txt"),"frames=1\norigin_x=0\norigin_y=0\n");return project;
    }
    private static void Templates()
    {
        var state=Scene(8000);state.tracks[0].count=2;state.tracks[0].interval=20;
        var variable=new Track{id="phase",name="phase",kind="variable",start=0,end=8000,variableName="phase",variableExpression="frame/100+1",variableRepeat=100};state.tracks.Add(variable);state.tracks[0].expressions["speed"]="global.phase*0.1";
        var rod=new Track{id="rod",name="rod",kind="rod",shape="star",start=0,end=8000,count=7,x=400,y=304,radius=50,motion="velocity",velocityX=.05,trailMode="off"};
        rod.spawnActions.Add(new BulletAction{type="spawn",generationPreset=CopyGeneration.Key,copyLinked=true,copyMode="grid",copyWidth=800,copyHeight=608,copySpacingX=256,copySpacingY=256,copyCenter=true,maxGeneration=3,expressions=new Dictionary<string,string>{{"copyFacingAngle","random_range(-20,20)+0*frame"}}});state.tracks.Add(rod);
        var late=new Track{id="later",name="later",kind="emitter",start=4000,end=8000,count=1,interval=20,life=100,advancedBullets=true,trailMode="off"};state.tracks.Add(late);Motion.Normalize(state);
        var room=new RoomData();using(var cache=new PreviewCache()){
            cache.UseScene(state,room);cache.bulletWorld=new BulletWorld(state,room);cache.bulletWorld.Seek(6500);var original=cache.bulletWorld;var next=Copy(state);next.tracks.Single(t=>t.id=="later").speed=4;
            Check(PreviewRevision.SimulationFrom(state,next,room,room)==4000,"Unrelated late edit should reuse prior template/variable state");cache.UseScene(next,room);Check(ReferenceEquals(original,cache.bulletWorld),"Template world was needlessly replaced");cache.bulletWorld.Seek(6500);var cold=new BulletWorld(Copy(next),room);cold.Seek(6500);Check(Rows(cache.bulletWorld)==Rows(cold),"Linked action references or variable history changed after partial reuse");
            var changed=Copy(next);changed.tracks.Single(t=>t.id=="rod").radius=80;Check(PreviewRevision.SimulationFrom(next,changed,room,room)==0,"Editing a linked/template definition must rebuild its earlier instances");
        }
        Console.WriteLine("PASS: partial replay preserves linked-copy action identity, fixed members and historical globals; changed template definitions rebuild.");
    }
    private static void LargeProjects()
    {
        var state=new EditorState{duration=1000,tracks=Enumerable.Range(0,201).Select(i=>new Track{id="item"+i,name="Item "+i,kind="object",lane=i,start=0,end=100}).ToList()};state.timelineGroups.Add(new TimelineGroup{id="large",name="large",color="#557799",lanes=new List<int>{200}});Motion.Normalize(state);
        Check(TimelineGroups.Id(state.tracks[200])=="large","Group membership above lane 200 was discarded");
        var moving=state.tracks[0];moving.lane=200;moving.start=40;moving.end=60;Check(TimelineLayout.CutOverlaps(state,new[]{moving})&&state.tracks.Count==202,"Overwriting a clip above the old 200-item limit failed");
        var preset=TrackPreset.Capture("Large",state.tracks.ToArray(),50);Check(preset.Instantiate(0,50).Count==202,"Large custom preset did not retain all items");
        Console.WriteLine("PASS: more than 200 items/lanes, group membership, overlap cuts and portable presets.");
    }
    private static void LargeLaneDrag()
    {
        var state=new EditorState{duration=1000,laneCount=210,tracks=new List<Track>{new Track{id="drag",kind="object",lane=201,start=100,end=160},new Track{id="other",kind="object",lane=202,start=250,end=300}}};Motion.Normalize(state);
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        using(var timeline=new TimelineCanvas{State=state,Size=new Size(900,400),ClipSnap=false,SelectionKeyframesOnly=true}){
            var handle=timeline.Handle;timeline.RefreshLayout();var scroll=(VScrollBar)typeof(TimelineCanvas).GetField("vertical",flags).GetValue(timeline);scroll.Value=scroll.Maximum-scroll.LargeChange+1;
            timeline.SetTrackSelection(state.tracks);var rect=timeline.ClipBounds(state.tracks[0]);
            int x=(int)(rect.Left+rect.Width/2),y=(int)(rect.Top+rect.Height/2);Action<string,int> mouse=(method,row)=>typeof(TimelineCanvas).GetMethod(method,flags).Invoke(timeline,new object[]{new MouseEventArgs(MouseButtons.Left,1,x,row,0)});
            mouse("OnMouseDown",y);mouse("OnMouseMove",y+30);mouse("OnMouseUp",y+30);
            Check(state.tracks[0].lane==202&&state.tracks[1].lane==203,"High-numbered multi-selection drag was clamped or changed lane offsets");
        }
        Console.WriteLine("PASS: native timeline mouse drag moves multiple clips above lane 200 with their spacing intact.");
    }
    private static void Dense(string folder,string project)
    {
        var track=new Track{id="dense",name="dense",kind="emitter",start=0,end=200,count=300,interval=1,life=160,speed=0,sprite="dot",color="#FFFFFF",x=20,y=50,trailMode="off",keyframes=new List<Keyframe>{new Keyframe{property="x",frame=0,value=20,easing="step"},new Keyframe{property="x",frame=80,value=180,easing="step"}}};
        var state=new EditorState{duration=200,tracks=new List<Track>{track}};Motion.Normalize(state);var room=new RoomData{width=200,height=100,bgColor=0};
        using(var cache=new PreviewCache())using(var sprites=new SpriteStore{Project=project})using(var image=PreviewRenderer.Render(state,room,new Dictionary<string,string>(),sprites,159,false,false,cache)){
            Check(!BulletSystem.Advanced(state),"Dense fixture must use the simple rendering path");Check(image.GetPixel(181,51).GetBrightness()>.8,"Bullets beyond the previous 20,000 cap were omitted");Check(ObjectStatistics.Capture(state,room,159,cache).sources["dense"]==48000,"Full count differs from rendered source");image.Save(Path.Combine(folder,"dense-full.png"));
        }
        Console.WriteLine("PASS: 48,000-bullet full preview, including the late waves previously omitted by the 20,000 cap.");
    }
    private static void Requests(string project)
    {
        using(var sprites=new SpriteStore{Project=project})using(var stage=new StageCanvas{Size=new Size(320,240),State=Scene(),Room=new RoomData(),Sprites=sprites,Frame=6000}){
            var seen=new List<int>();Exception failure=null;stage.StatisticsReady=s=>seen.Add(s.frame);stage.RenderFailed=e=>failure=e;var handle=stage.Handle;stage.SceneChanged();stage.ShowFrame(40);
            var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<10000&&(stage.DisplayedFrame!=40||seen.Count==0)){Application.DoEvents();System.Threading.Thread.Sleep(1);}
            Check(failure==null,"Render failed: "+failure);Check(stage.DisplayedFrame==40&&seen.Count==1&&seen[0]==40,"Obsolete frame/statistics was published");
        }
        Console.WriteLine("PASS: native async canvas discards a superseded seek and only publishes the latest frame/statistics.");
    }
    private static void RenderErrors(string project)
    {
        string broken=Path.Combine(project,"sprites","broken");Directory.CreateDirectory(broken);File.WriteAllText(Path.Combine(broken,"0.png"),"invalid PNG fixture");
        var track=new Track{id="broken",kind="object",sprite="broken",start=0,end=100};var scene=new EditorState{duration=100,tracks=new List<Track>{track}};Motion.Normalize(scene);
        using(var sprites=new SpriteStore{Project=project})using(var stage=new StageCanvas{Size=new Size(320,240),State=scene,Room=new RoomData(),Sprites=sprites}){
            int errors=0;stage.RenderFailed=e=>errors++;var handle=stage.Handle;
            Action wait=()=>{var clock=Stopwatch.StartNew();do{Application.DoEvents();System.Threading.Thread.Sleep(1);}while(clock.ElapsedMilliseconds<5000&&(bool)typeof(StageCanvas).GetField("rendering",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(stage));Check(clock.ElapsedMilliseconds<5000,"Render error test timed out");};
            stage.SceneChanged();wait();Check(errors==1,"First render error was not reported");stage.ShowFrame(1);wait();Check(errors==1,"Repeated identical render error was not deduplicated");
            track.sprite="dot";stage.SceneChanged();wait();Check(stage.DisplayedFrame==1,"Preview did not recover after sprite repair");track.sprite="broken";stage.SceneChanged();wait();Check(errors==2,"A new failure after successful rendering was suppressed");
        }
        Console.WriteLine("PASS: repeated preview failures report once, repair recovers and a later failure is reported again.");
    }
    [STAThread]public static int Main(string[] args){try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Directory.CreateDirectory(args[0]);Budget();Cache(args[0]);Templates();LargeProjects();LargeLaneDrag();string project=Sprites(args[0]);Dense(args[0],project);Requests(project);RenderErrors(project);Console.WriteLine("PASS: optimization checks complete.");return 0;}catch(Exception error){Console.Error.WriteLine(error);return 1;}}
}
