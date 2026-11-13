using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

// Observed occupancy, not a declared end-start duration or per-particle ancestry.
// All frame ranges are inclusive, sampled AFTER the existing simulation step.
internal sealed class LifetimeInterval
{
    public int First,Last;public bool RightCensored;
    public override string ToString(){return First+(Last==First?"":"–"+Last)+(RightCensored?"+":"");}
}
internal sealed class LifetimeSeries
{
    public readonly List<LifetimeInterval> Intervals=new List<LifetimeInterval>();
    public long Samples;public int Peak;
    internal bool NewInterval(int frame,int count){return count>0&&(Intervals.Count==0||Intervals[Intervals.Count-1].Last!=frame-1);}
    internal void Observe(int frame,int count)
    {
        if(count<=0)return;Samples+=count;Peak=Math.Max(Peak,count);
        if(NewInterval(frame,count))Intervals.Add(new LifetimeInterval{First=frame,Last=frame});else Intervals[Intervals.Count-1].Last=frame;
    }
    internal void Finish(int last){if(Intervals.Count>0&&Intervals[Intervals.Count-1].Last==last)Intervals[Intervals.Count-1].RightCensored=true;}
}
internal sealed class LifetimeRow
{
    public string Id,Name,Kind,Category,ClipSourceId,Declaration;public bool Unknown;
    public readonly LifetimeSeries Alive=new LifetimeSeries(),Fading=new LifetimeSeries(),Transparent=new LifetimeSeries();
    public int MaxGeneration;
}
internal sealed class LifetimeResult
{
    public int RequestedEnd,LastFrame=-1;public string Stop="complete",Error="";public double Seconds;
    public readonly List<LifetimeRow> Rows=new List<LifetimeRow>();
    public readonly List<string> Warnings=new List<string>();
    public bool Complete {get{return Stop=="complete"&&LastFrame==RequestedEnd;}}
}
// Immutable worker-thread heartbeat. LastCompletedFrame is the accepted prefix;
// ActiveFrame may be in progress and must never be presented as measured output.
internal sealed class LifetimeProgress
{
    public int LastCompletedFrame {get;private set;}public int RequestedEnd {get;private set;}
    public double ElapsedSeconds {get;private set;}public double MaxSeconds {get;private set;}
    public int ActiveFrame {get;private set;}public string Phase {get;private set;}
    public LifetimeProgress(int lastCompletedFrame,int requestedEnd,double elapsedSeconds,double maxSeconds,int activeFrame,string phase)
    {LastCompletedFrame=lastCompletedFrame;RequestedEnd=requestedEnd;ElapsedSeconds=elapsedSeconds;MaxSeconds=maxSeconds;ActiveFrame=activeFrame;Phase=phase;}
}
internal sealed class LifetimeOptions
{
    public int EndFrame=1500,MaxFrames=20001,MaxSources=4000,MaxEntities=30000,MaxIntervals=100000;
    public long MaxSamples=5000000,MaxOperations=2000000,MaxCloneItems=200000;public double MaxSeconds=15;
    internal static LifetimeOptions ForWindow(int end,double seconds)
    {
        if(seconds<5||seconds>180||Double.IsNaN(seconds))throw new ArgumentOutOfRangeException("seconds");
        var options=new LifetimeOptions{EndFrame=end,MaxSeconds=seconds};double factor=Math.Max(1,seconds/15);
        options.MaxOperations=(long)(options.MaxOperations*factor);options.MaxSamples=(long)(options.MaxSamples*factor);return options;
    }
    internal void Validate(){if(EndFrame<0||EndFrame>1000000||MaxFrames<1||MaxSources<1||MaxEntities<1||MaxIntervals<1||MaxSamples<1||MaxOperations<1||MaxCloneItems<1||MaxSeconds<=0||Double.IsNaN(MaxSeconds)||Double.IsInfinity(MaxSeconds))throw new ArgumentOutOfRangeException("LifetimeOptions");}
}
internal sealed class LifetimeSnapshot
{
    public readonly EditorState Scene;public readonly RoomData Room;
    public readonly Dictionary<string,SpriteMeta> Metadata;public readonly NativeFilmInfo Film;
    public LifetimeSnapshot(EditorState scene,RoomData room,Dictionary<string,SpriteMeta> metadata=null,NativeFilmInfo film=null)
    {
        if(scene==null||room==null)throw new ArgumentNullException();
        Preflight(scene,new LifetimeOptions());
        Scene=scene.Snapshot();Scene.nativePaths=scene.nativePaths.ToDictionary(p=>p.Key,p=>p.Value.Copy());
        Room=Json.Read<RoomData>(Json.Write(room));
        Metadata=metadata==null?new Dictionary<string,SpriteMeta>():Json.Read<Dictionary<string,SpriteMeta>>(Json.Write(metadata));
        // Only camera samples are needed. Do not copy film images/count histories.
        Film=film==null?null:new NativeFilmInfo{fps=film.fps,frameCount=film.frameCount,views=film.views==null?null:film.views.Select(v=>v==null?null:new NativeViewPose{enabled=v.enabled,x=v.x,y=v.y,w=v.w,h=v.h,angle=v.angle,portX=v.portX,portY=v.portY,portW=v.portW,portH=v.portH,outputW=v.outputW,outputH=v.outputH}).ToArray()};
    }
    internal static void Preflight(EditorState scene,LifetimeOptions options)
    {
        long expanded=0,items=0;Action<IEnumerable<Track>,long,int> visit=null;
        visit=(tracks,multiplier,depth)=>{
            if(depth>8)throw new InvalidOperationException("分析输入超过结构上限");
            foreach(var t in tracks){
                long weight=1L+t.keyframes.Count+t.expressions.Count+t.curvePoints.Count;
                foreach(var action in t.spawnActions.Concat(t.bulletActions))weight+=1+(action.expressions==null?0:action.expressions.Count);
                items+=weight*multiplier;
                if(t.spawnActions.Concat(t.bulletActions).Any(a=>a.maxGeneration>16))throw new InvalidOperationException("分析输入超过递归生成上限（16 代）");
                if(items>200000)throw new InvalidOperationException("分析输入超过结构上限");
                if(t.kind=="pattern"){
                    long repetitions=Math.Max(1,t.repeatCount);if(multiplier>options.MaxSources/repetitions)throw new InvalidOperationException("分析输入超过结构上限");
                    // Even disabled nested inputs are bounded before snapshot cloning.
                    visit(t.children,multiplier*repetitions,depth+1);
                }else {expanded+=multiplier;if(expanded>options.MaxSources)throw new InvalidOperationException("分析输入超过结构上限");if(t.children.Count>0)visit(t.children,multiplier,depth+1);}
            }
        };visit(scene.tracks,1,0);
    }
}
internal static class SimulatedLifetime
{
    private sealed class StopAnalysis : Exception {public string Reason;public StopAnalysis(string reason){Reason=reason;}}
    private sealed class Counts {public LifetimeRow Row;public int Alive,Fading,Transparent,Generation;}
    internal static string Key(string id,string category){return category+"\0"+id;}
    public static LifetimeResult Analyze(LifetimeSnapshot input,LifetimeOptions options,CancellationToken cancellation,Action<int> progress=null,Action<LifetimeProgress> report=null)
    {
        options.Validate();var result=new LifetimeResult{RequestedEnd=options.EndFrame};var clock=Stopwatch.StartNew();
        var rows=new Dictionary<string,LifetimeRow>(StringComparer.Ordinal);long operations=0,totalSamples=0,cloneItems=0;int intervals=0;BulletWorld world=null;
        int activeFrame=-1;string phase="prepare";double nextReport=0;
        Action<bool> publish=force=>{if(report==null)return;double elapsed=clock.Elapsed.TotalSeconds;if(force||elapsed>=nextReport){nextReport=elapsed+.1;try{report(new LifetimeProgress(result.LastFrame,options.EndFrame,elapsed,options.MaxSeconds,activeFrame,phase));}catch{report=null;throw;}}};
        Action guard=()=>{
            if(cancellation.IsCancellationRequested)throw new StopAnalysis("cancelled");
            publish(false);if(cancellation.IsCancellationRequested)throw new StopAnalysis("cancelled");
            if(clock.Elapsed.TotalSeconds>=options.MaxSeconds)throw new StopAnalysis("time-limit");
            if(++operations>options.MaxOperations)throw new StopAnalysis("work-limit");
            if(world!=null&&world.bullets.Count+world.Templates.InstanceCount>=options.MaxEntities)throw new StopAnalysis("entity-limit");
        };
        Func<Track,string,LifetimeRow> row=(t,category)=>{
            string key=Key(t.id??"",category);LifetimeRow found;if(rows.TryGetValue(key,out found))return found;
            if(rows.Count>=options.MaxSources*6)throw new StopAnalysis("row-limit");
            found=new LifetimeRow{Id=t.id,Name=t.name,Kind=t.kind,Category=category,ClipSourceId=t.clipSourceId,Declaration=TimelineRelations.Timing(input.Scene,TimelineRelations.Resolve(input.Scene,t.id)??t),Unknown=!new[]{"emitter","object","rod","group","control","variable","camera","effect","pattern"}.Contains(t.kind)};
            rows.Add(key,found);result.Rows.Add(found);return found;
        };
        try{
            guard();LifetimeSnapshot.Preflight(input.Scene,options);
            var scene=input.Scene.Snapshot();scene.nativePaths=input.Scene.nativePaths.ToDictionary(p=>p.Key,p=>p.Value.Copy());
            RandomParameters.Attach(scene);guard();
            world=new BulletWorld(scene,input.Room,f=>input.Film==null?null:input.Film.View((int)Math.Round(f*input.Film.fps/(double)Math.Max(1,scene.fps))),name=>{SpriteMeta meta;return input.Metadata.TryGetValue(name,out meta)?meta:null;});
            world.CheckpointLimit=0;world.CheckpointBudget=0;world.AnalysisCheckpoint=guard;
            world.AnalysisCloneCheckpoint=tracks=>{
                var pending=new Stack<Track>(tracks);
                while(pending.Count>0){guard();var track=pending.Pop();cloneItems+=1+track.keyframes.Count+track.expressions.Count+track.curvePoints.Count;
                    foreach(var action in track.spawnActions.Concat(track.bulletActions))cloneItems+=1+(action.expressions==null?0:action.expressions.Count);
                    if(cloneItems>options.MaxCloneItems)throw new StopAnalysis("clone-limit");foreach(var child in track.children)pending.Push(child);
                }
            };
            if(world.State.tracks.Any(t=>String.IsNullOrEmpty(t.id))||world.State.tracks.GroupBy(t=>t.id).Any(g=>g.Count()!=1))throw new InvalidOperationException("物件 ID 缺失或重复，无法归属模拟结果");
            if(world.State.tracks.Any(t=>!String.IsNullOrEmpty(t.nativePath)&&!scene.nativePaths.ContainsKey(t.nativePath)))throw new InvalidOperationException("缺少原生路径数据，无法完成模拟");
            foreach(var t in world.State.tracks)row(t,t.kind=="emitter"?"飞行弹幕":t.kind=="object"||t.kind=="rod"?"直接物件 / 整体成员":"声明信息（非存活）");
            foreach(var t in input.Scene.tracks.Where(t=>t.kind=="pattern"))row(t,"片段声明（展开 ID 另列）");
            var requirements=NativePreviewCoverage.Collect(input.Scene);
            if(requirements.HasNative||input.Room.instances.Any(i=>i.name!="objRufController"))result.Warnings.Add("原生对象、房间实例和 GML 未在此执行；其影响未知，C# 结果不能代替 F5 验证。");
            if(requirements.unexecuted.Count>0)result.Warnings.Add(Language.Text("未执行内容：")+String.Join(", ",requirements.unexecuted));
            if(input.Film!=null&&input.Film.views!=null&&input.Film.views.Length>0&&Math.Round(options.EndFrame*input.Film.fps/(double)Math.Max(1,scene.fps))>=input.Film.views.Length)result.Warnings.Add("分析终点超过底片镜头样本；后续沿用最后一个镜头样本，并非后续真实镜头记录。");
            if(input.Film!=null)result.Warnings.Add("镜头边界使用已有底片的镜头样本；缓存来源与当前工程是否一致未在此验证。");
            if(world.State.tracks.Any(t=>BulletLifetime.Mode(t)=="view"||SpriteMaskLifetime.Enabled(t)&&t.despawnSpace=="view"))result.Warnings.Add("视野边界沿用 C# 镜头；无镜头样本时回退房间范围，不能推断原生动态镜头。");
            if(world.State.tracks.Any(t=>t.previewHidden))result.Warnings.Add("仅预览隐藏标记不参与本分析；使用完整场景，隐藏不代表死亡。");
            var direct=world.State.tracks.Where(t=>t.kind=="object"||t.kind=="rod").ToArray();
            for(int f=0;f<=options.EndFrame;f++){
                activeFrame=f;phase="simulate";guard();if(f>=options.MaxFrames)throw new StopAnalysis("frame-limit");world.Seek(f);phase="collect";
                var counts=new Dictionary<string,Counts>(StringComparer.Ordinal);
                Action<Track,string,bool,bool,int> add=(t,category,fading,transparent,generation)=>{
                    string key=Key(t.id,category);Counts count;if(!counts.TryGetValue(key,out count)){count=new Counts{Row=row(t,category)};counts.Add(key,count);}
                    count.Alive++;if(fading)count.Fading++;if(transparent)count.Transparent++;count.Generation=Math.Max(count.Generation,generation);
                };
                foreach(var b in world.bullets){guard();
                    // Trail histories are render-only. Analysis never draws or
                    // seeks backwards; keep physical state, not cached pixels.
                    b.history.Clear();if(b.dead)continue;
                    if(f<b.birth){add(b.source,"已排程 / 尚未出生",false,false,b.generation);continue;}
                    add(b.source,b.kinematic?"生成的固定成员":"飞行弹幕",b.deathStart>=0||b.deathFactor<1,b.alpha*BulletLifetime.Alpha(b)<=0,b.generation);
                }
                foreach(var t in direct){guard();if(!BulletSystem.Active(t,f)||!world.Templates.Visible(t)||t.kind=="rod"&&t.releaseFrame>=0&&f>=t.releaseFrame)continue;
                    if(t.kind=="object")add(t,"直接物件 / 整体成员",false,Motion.At(t,f).alpha<=0,0);
                    else {int n=Motion.RodPoints(t,f).Length;for(int i=0;i<n;i++){guard();var member=world.FixedMember(t,i,f);if(member!=null)add(t,"直接物件 / 整体成员",false,member.alpha<=0,0);}}
                }
                foreach(var instance in world.Templates.ObservedInstances){guard();if(!instance.ended&&instance.birth<=f)add(instance.definition.root,"生成模板容器",false,false,instance.generation);}
                // Commit a whole sampled frame atomically. A guard/limit/error in
                // the step or collection never extends intervals to that frame.
                long samples=counts.Values.Sum(c=>(long)c.Alive);int additions=counts.Values.Sum(c=>(c.Row.Alive.NewInterval(f,c.Alive)?1:0)+(c.Row.Fading.NewInterval(f,c.Fading)?1:0)+(c.Row.Transparent.NewInterval(f,c.Transparent)?1:0));
                if(totalSamples+samples>options.MaxSamples)throw new StopAnalysis("sample-limit");
                if(intervals+additions>options.MaxIntervals)throw new StopAnalysis("interval-limit");guard();
                foreach(var c in counts.Values){c.Row.Alive.Observe(f,c.Alive);c.Row.Fading.Observe(f,c.Fading);c.Row.Transparent.Observe(f,c.Transparent);c.Row.MaxGeneration=Math.Max(c.Row.MaxGeneration,c.Generation);}
                totalSamples+=samples;intervals+=additions;result.LastFrame=f;if(progress!=null&&(f%25==0||f==options.EndFrame))progress(f);
            }
        }catch(StopAnalysis stop){result.Stop=stop.Reason;}catch(OperationCanceledException){result.Stop="cancelled";}catch(Exception error){result.Stop="error";result.Error=error.Message;}
        finally{if(world!=null){world.AnalysisCheckpoint=null;world.AnalysisCloneCheckpoint=null;}foreach(var r in result.Rows){r.Alive.Finish(result.LastFrame);r.Fading.Finish(result.LastFrame);r.Transparent.Finish(result.LastFrame);}result.Seconds=clock.Elapsed.TotalSeconds;phase=result.Stop;try{publish(true);}catch(OperationCanceledException){result.Stop="cancelled";}catch(Exception error){result.Stop="error";result.Error=error.Message;}}
        return result;
    }
    internal static string StopText(string stop)
    {
        switch(stop){case "complete":return "已到分析终点";case "cancelled":return "已取消（仅保留完成帧）";case "error":return "分析失败（仅保留完成帧）";case "time-limit":return "达到分析时间上限";case "frame-limit":return "达到分析帧数上限";default:return "达到分析资源上限";}
    }
    internal static string Intervals(LifetimeSeries series,int maximum)
    {if(series.Intervals.Count==0)return Language.Text("未观察到");return String.Join(", ",series.Intervals.Take(maximum).Select(i=>i.ToString()))+(series.Intervals.Count>maximum?String.Format(Language.Text(" …（共 {0} 段）"),series.Intervals.Count):"");}
}

// UI-free generation gate. Replacing a request, changing the scene, or closing
// invalidates progress AND completion. It never compares worker-owned snapshots.
internal sealed class LifetimeRequestGate
{
    private int generation;private bool closed;
    public int Begin(){if(closed)throw new ObjectDisposedException("LifetimeRequestGate");return ++generation;}
    public bool Accept(int request,bool current){return !closed&&current&&request==generation;}
    public void Invalidate(){generation++;}
    public void Close(){closed=true;generation++;}
}
