using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// Pure helpers only: never construct a window, control, game process, or project.
internal static class PlayerTraceSessionTests
{
    private static int checks;
    private static void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
    private static void Sessions()
    {
        var requests=new PlayerTraceRequests();var state=new EditorState();
        var first=new LoadData{project="same-path"};var reload=new LoadData{project="same-path"};
        var a=requests.Begin(first,state);
        Check(requests.IsCurrent(a,first,state),"initial request accepted");
        Check(!requests.IsCurrent(a,reload,state),"same path with new document identity rejected");
        Check(!requests.IsCurrent(a,first,new EditorState()),"undo/replaced state identity rejected");
        Check(!requests.IsCurrent(a,null,state)&&!requests.IsCurrent(a,first,null),"missing context rejected");
        var b=requests.Begin(first,state);
        Check(!requests.IsCurrent(a,first,state)&&requests.IsCurrent(b,first,state),"new request retires old same-document callback");
        Check(!requests.Cancel(a)&&requests.IsCurrent(b,first,state),"old completion/cancel cannot stop newer request");
        Check(requests.Cancel(b)&&!requests.IsCurrent(b,first,state),"cancel retires matching request");
        Check(!requests.Cancel(b)&&!requests.Cancel(null),"repeated cancellation is harmless");
        var c=requests.Begin(first,state);requests.Stop();
        Check(!requests.IsCurrent(c,first,state)&&requests.Current==null,"committed load/close stops pending or watching request");
        var d=requests.Begin(reload,state);
        Check(!requests.Cancel(c)&&requests.IsCurrent(d,reload,state),"late callback after reload cannot stop replacement");
        Check(requests.IsCurrent(d,reload,state),"uncommitted cancelled load does not mutate current session");
        requests.Cancel(d);var e=requests.Begin(reload,state);
        Check(requests.IsCurrent(e,reload,state)&&!requests.IsCurrent(d,reload,state),"finish and restart never reuse token");
    }
    private static void Reads(string dir)
    {
        string path=Path.Combine(dir,"live.csv");List<PlayerTracePoint> points;
        Check(!PlayerTrace.TryReadLive(null,out points)&&points==null,"missing path deferred");
        Check(!PlayerTrace.TryReadLive(path,out points),"missing file deferred");
        File.WriteAllText(path,"");Check(!PlayerTrace.TryReadLive(path,out points),"truncate window deferred");
        File.WriteAllText(path,"0,100,200\n1,110,210\n");
        Check(PlayerTrace.TryReadLive(path,out points)&&points.Count==2,"complete trace read");var old=points;
        Check(PlayerTrace.TryReadLive(path,out points)&&PlayerTrace.Same(old,points),"unchanged file has equal complete values");
        var fixedStamp=new DateTime(2020,1,2,3,4,5,DateTimeKind.Utc);File.SetLastWriteTimeUtc(path,fixedStamp);
        DateTime stamp=File.GetLastWriteTimeUtc(path);long size=new FileInfo(path).Length;
        File.WriteAllText(path,"0,700,500\n1,710,510\n");File.SetLastWriteTimeUtc(path,stamp);
        Check(new FileInfo(path).Length==size&&File.GetLastWriteTimeUtc(path)==stamp,"same length and timestamp fixture");
        Check(PlayerTrace.TryReadLive(path,out points)&&points[0].x==700&&!PlayerTrace.Same(old,points),"same count/length/timestamp rewrite detected by values");
        File.WriteAllText(path,"0,900,500\n1,910,51");
        Check(!PlayerTrace.TryReadLive(path,out points),"numerically valid unfinished row does not publish prefix");
        Check(PlayerTrace.Read(path).Count==2,"manual legacy import accepts final row without newline");
        File.AppendAllText(path,"0\n");Check(PlayerTrace.TryReadLive(path,out points)&&points[1].y==510,"completion after partial write reloads");
        File.WriteAllText(path,"0,100,200\r\n");Check(PlayerTrace.TryReadLive(path,out points)&&points.Count==1,"CRLF accepted");
        File.WriteAllText(path,"0,100,200\r");Check(PlayerTrace.TryReadLive(path,out points)&&points.Count==1,"CR legacy accepted");
        File.WriteAllText(path,"not,a,trace\n0,NaN,2\n1,2,Infinity\n");Check(!PlayerTrace.TryReadLive(path,out points),"invalid and nonfinite coordinates do not publish empty replacement");
        using(var file=new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.ReadWrite))
        using(var writer=new StreamWriter(file)){
            writer.WriteLine("0,400,304,sprPlayerRunning,2,-1,2,0.5,45,1");writer.Flush();
            Check(PlayerTrace.TryReadLive(path,out points)&&points.Count==1,"open shared writer read succeeds");
            Check(points[0].sprite=="sprPlayerRunning"&&points[0].action=="run"&&points[0].subimage==2&&points[0].facing==-1&&points[0].yscale==2&&points[0].alpha==.5&&points[0].angle==45&&points[0].dead,"sprite format semantics unchanged");
        }
        File.WriteAllText(path,"0,100,200,-1,-2,3,jump,1,1,0\n");
        Check(PlayerTrace.TryReadLive(path,out points)&&points[0].vspeed==-2&&points[0].hspeed==3&&points[0].jumping&&points[0].shooting&&points[0].sprite=="sprPlayerJump","numeric action format unchanged");
        File.WriteAllText(path,"0,100,200\n1,110,210\n0,700,500\n1,710,510\n");
        Check(PlayerTrace.TryReadLive(path,out points)&&points.Count==2&&points[0].x==700,"R-style appended frame rewind retains newest segment");
        using(var file=new FileStream(path,FileMode.Create,FileAccess.Write)){file.SetLength((long)PlayerTrace.MaxLiveBytes+1);}
        Check(!PlayerTrace.TryReadLive(path,out points)&&points==null,"oversized live snapshot declined before allocation");
    }
    private static void Equality()
    {
        var point=new PlayerTracePoint{frame=3,x=4,y=5};
        var a=new List<PlayerTracePoint>{point};
        Check(PlayerTrace.Same(a,a)&&PlayerTrace.Same(a,new List<PlayerTracePoint>{point.Snapshot()}),"same values and independent snapshots equal");
        Check(!PlayerTrace.Same(a,null)&&!PlayerTrace.Same(a,new List<PlayerTracePoint>()),"null and count differences detected");
        Check(!PlayerTrace.Same(a,new List<PlayerTracePoint>{null}),"null point safely differs");
        Action<PlayerTracePoint>[] edits={p=>p.frame++,p=>p.x++,p=>p.y++,p=>p.sprite="other",p=>p.subimage++,p=>p.xscale++,p=>p.yscale++,p=>p.alpha++,p=>p.angle++,p=>p.facing=-1,p=>p.vspeed++,p=>p.hspeed++,p=>p.action="fall",p=>p.jumping=true,p=>p.shooting=true,p=>p.dead=true};
        foreach(var edit in edits){var copy=point.Snapshot();edit(copy);Check(!PlayerTrace.Same(a,new List<PlayerTracePoint>{copy}),"every persisted point field affects change detection");}
        point.vspeed=Double.NaN;Check(PlayerTrace.Same(a,new List<PlayerTracePoint>{point.Snapshot()}),"unchanged optional NaN does not cause endless redraw");
    }
    public static int Main(string[] args)
    {
        string root=args.Length>0?args[0]:Path.Combine(Path.GetTempPath(),"rufmaker-trace-tests");
        string dir=Path.Combine(root,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        try{Sessions();Reads(dir);Equality();Console.WriteLine("PASS "+checks+" pure player trace session checks");return 0;}
        finally{Directory.Delete(dir,true);}
    }
}
