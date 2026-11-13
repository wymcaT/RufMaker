using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

internal static class NativePreviewCoverageTests
{
    private static int checks;
    private static void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
    private static EditorState Scene(){return new EditorState{duration=20,tracks=new List<Track>{new Track{id="live",kind="object",start=0,end=20},new Track{id="clip",name="Actor",kind="native-instance",nativeObject="objActor",start=5,end=10}}};}
    private static NativeFilmInfo Film(EditorState state){return new NativeFilmInfo{coverageVersion=1,playerReference="fixed",sourceSignature="current",room=state.room,fps=50,frameCount=21,nativeTrackIds=new[]{"clip"}};}
    private static NativePreviewCoverageReport Report(EditorState state,NativeFilmInfo film,bool displayed=true,string signature="current",bool changed=false){return NativePreviewCoverage.Evaluate(NativePreviewCoverage.Collect(state),state,film,displayed,signature,changed);}
    public static int Main(string[] args)
    {
        var state=Scene();string original=Json.Write(state);NativePreviewCoverage.Collect(state);Check(Json.Write(state)==original,"coverage analysis does not change source project data");var film=Film(state);var result=Report(state,null);
        Check(result.cacheState=="missing"&&result.missing==1&&result.live==1&&result.cached==0,"absent cache exposes placeholder; regular object remains live");
        result=Report(state,film);Check(result.cacheState=="matching"&&result.cached==1&&!result.Warning&&result.live==1,"matching native film is valid recorded coverage, not unsupported");
        Check(result.Title!="实时预览"&&result.Description.Contains("C#"),"cached source is explicitly distinguished from live");
        result=Report(state,film,true,"changed");Check(result.cacheState=="stale"&&result.missing==1&&result.cached==0,"source/project signature changed");
        Check(Report(state,film,true,"current",true).cacheState=="stale","unsaved native edits invalidate matching disk cache");
        Check(Report(state,film,false).cacheState=="disabled","disabled film never counts as coverage");
        Check(Report(state,film,true,null).cacheState=="unverified","unchecked source not asserted fresh");
        film.sourceSignature=null;Check(Report(state,film).cacheState=="unverified","legacy missing signature");film=Film(state);
        film.coverageVersion=0;Check(Report(state,film).cacheState=="unverified","legacy missing recorder provenance");film=Film(state);
        film.playerReference=null;Check(Report(state,film).cacheState=="unverified","missing reference mode");film=Film(state);
        film.room="different";Check(Report(state,film).cacheState=="incompatible","wrong project room");film=Film(state);
        film.fps=30;Check(Report(state,film).cacheState=="incompatible","wrong recorder rate");film=Film(state);
        film.frameCount=20;Check(Report(state,film).cacheState=="incompatible","inclusive last frame cannot silently clamp");film=Film(state);
        film.nativeTrackIds=new string[0];Check(Report(state,film).missing==1,"base film does not cover separately scheduled clips");film.nativeTrackIds=null;Check(Report(state,film).missing==1,"legacy null clip IDs");film=Film(state);
        state.nativeTest.x=401;result=Report(state,film);Check(result.referenceMismatch&&result.cached==1&&result.missing==0,"different player reference warns without claiming valid recording absent");state.nativeTest.x=400;
        state.usePlayerTrace=true;state.playerTrace.Add(new PlayerTracePoint{frame=0,x=400,y=304});Check(Report(state,film).referenceMismatch,"recording is not relabelled as current trace");state.usePlayerTrace=false;Check(!Report(state,film).referenceMismatch,"inactive trace irrelevant");
        state.tracks.Add(new Track{id="move",kind="control",eventPreset="player-move",start=1,end=1});Check(Report(state,film).referenceMismatch,"player-move history differs from fixed recording");state.tracks.RemoveAt(2);
        state.tracks.Add(new Track{id="gml",name="Custom event",kind="script",code="instance_create(0,0,objActor);",start=3,end=3});result=Report(state,film);Check(result.cached==1&&result.missing==1&&result.details.Any(x=>x.Contains("gml")),"timeline GML remains unexecuted even with fresh native film");
        state.tracks[2].enabled=false;Check(Report(state,film).missing==0,"disabled GML omitted");state.tracks[2].enabled=true;state.tracks[2].code=" ";Check(Report(state,film).missing==0,"empty GML omitted");state.tracks.RemoveAt(2);
        state.tracks.Add(new Track{id="player-options",kind="control",eventPreset="player-set",start=1,end=1});result=Report(state,film);Check(result.missing==1&&result.live==1,"native player flags are not implemented in C# or the film controller");state.tracks.RemoveAt(2);
        state.tracks.Add(new Track{id="native-event",kind="control",eventPreset="clear-bullets",eventNativeObjects="objActor",start=3,end=3});Check(Report(state,film).missing==1,"native targets of live controls not claimed executed in cached film");state.tracks.RemoveAt(2);
        state.tracks[1].enabled=false;Check(NativePreviewCoverage.Collect(state).nativeIds.Count==0,"disabled native clip omitted");state.tracks[1].enabled=true;state.tracks[1].templateOnly=true;Check(NativePreviewCoverage.Collect(state).nativeIds.Count==0,"uninstantiated template omitted");state.tracks[1].templateOnly=false;
        state.tracks.Add(new Track{id="group",kind="group",start=0,end=20,enabled=false});state.tracks[1].parentId="group";Check(NativePreviewCoverage.Collect(state).nativeIds.Count==0,"disabled ancestor omitted");state.tracks[2].enabled=true;state.tracks[2].start=15;Check(NativePreviewCoverage.Collect(state).nativeIds.Count==0,"nonoverlapping ancestor omitted");state.tracks.RemoveAt(2);state.tracks[1].parentId="";
        var child=state.tracks[1];state.tracks.RemoveAt(1);state.tracks.Add(new Track{id="repeat",kind="pattern",start=0,end=20,repeatCount=1,children=new List<Track>{child}});var req=NativePreviewCoverage.Collect(state);Check(req.nativeIds.SequenceEqual(new[]{"repeat/0/clip"}),"pattern IDs match native-film manifest");film.nativeTrackIds=req.nativeIds.ToArray();Check(Report(state,film).cached==1,"pattern recording recognized");state.tracks[1].enabled=false;Check(NativePreviewCoverage.Collect(state).nativeIds.Count==0,"disabled pattern omitted");
        state=Scene();state.tracks.RemoveAt(1);result=Report(state,null);Check(result.live==1&&result.missing==0&&!result.Warning,"ordinary scene unaffected");
        state.nativeProject=new NativeProject{duration=20};film=Film(state);film.nativeTrackIds=new string[0];result=Report(state,film);Check(result.cached==1&&!result.Warning,"base native project/timeline covered by matching film");Check(Report(state,film,true,"old").missing==1,"base native film stale too");
        string key=NativeClip.PreviewKey(state);state.randomSeed++;Check(NativeClip.PreviewKey(state)!=key,"local random seed invalidates recording key");key=NativeClip.PreviewKey(state);state.tracks[0].x++;Check(NativeClip.PreviewKey(state)==key,"regular live object edits reuse recording");state.nativeTest.x++;Check(NativeClip.PreviewKey(state)==key,"fixed recording reference unchanged by editor player");
        // Real serialization and archive manifest loading, not a parallel fake model.
        string root=args.Length>0?args[0]:Path.Combine(Path.GetTempPath(),"ruf-coverage-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);string file=Path.Combine(root,"coverage-"+Guid.NewGuid().ToString("N")+".rufvideo");
        using(var archive=ZipFile.Open(file,ZipArchiveMode.Create)){using(var writer=new StreamWriter(archive.CreateEntry("manifest.json").Open()))writer.Write(NativeFilmReader.Json(film));for(int i=0;i<film.frameCount;i++)archive.CreateEntry(i.ToString("D6")+".jpg");}
        using(var reader=new NativeFilmReader(file)){Check(reader.Info.coverageVersion==1&&reader.Info.playerReference=="fixed","recording provenance survives serialization/archive load");Check(reader.Info.sourceSignature==film.sourceSignature,"source signature preserved");}
        File.Delete(file);
        using(var archive=ZipFile.Open(file,ZipArchiveMode.Create)){using(var writer=new StreamWriter(archive.CreateEntry("manifest.json").Open()))writer.Write(NativeFilmReader.Json(film));archive.CreateEntry("000000.jpg");}
        bool rejected=false;try{using(var reader=new NativeFilmReader(file)){} }catch(IOException){rejected=true;}Check(rejected,"truncated frame archive rejected before being advertised as coverage");File.Delete(file);Console.WriteLine("PASS: "+checks+" preview coverage checks (source, cache, reference, script, patterns, regular object, archive).");return 0;
    }
}
