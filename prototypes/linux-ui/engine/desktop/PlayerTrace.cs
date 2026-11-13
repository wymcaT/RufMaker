using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

internal static class PlayerTrace
{
    // Bound each live read to a finite snapshot, even while the runner appends.
    internal const int MaxLiveBytes=64*1024*1024;
    public static List<PlayerTracePoint> Read(string path)
    {
        if(!File.Exists(path))return new List<PlayerTracePoint>();
        try
        {
            using(var fs=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
            using(var reader=new StreamReader(fs))return Parse(reader);
        }
        catch{return new List<PlayerTracePoint>();}
    }
    internal static bool TryReadLive(string path,out List<PlayerTracePoint> points)
    {
        points=null;
        if(String.IsNullOrEmpty(path))return false;
        try
        {
            DateTime stamp=File.GetLastWriteTimeUtc(path);
            using(var fs=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
            {
                long length=fs.Length;
                if(length<=0||length>MaxLiveBytes)return false;
                var bytes=new byte[(int)length];int read=0;
                while(read<bytes.Length){int count=fs.Read(bytes,read,bytes.Length-read);if(count==0)return false;read+=count;}
                if(fs.Length!=length||File.GetLastWriteTimeUtc(path)!=stamp)return false;
                // A valid numeric prefix is still an unfinished row. Retry later,
                // rather than replacing the last complete trace with half a write.
                if(bytes[bytes.Length-1]!=10&&bytes[bytes.Length-1]!=13)return false;
                using(var reader=new StreamReader(new MemoryStream(bytes)))points=Parse(reader);
                return points.Count>0;
            }
        }
        catch(IOException){return false;}
        catch(UnauthorizedAccessException){return false;}
    }
    internal static bool Same(IList<PlayerTracePoint> a,IList<PlayerTracePoint> b)
    {
        if(ReferenceEquals(a,b))return true;
        if(a==null||b==null||a.Count!=b.Count)return false;
        for(int i=0;i<a.Count;i++)
        {
            var x=a[i];var y=b[i];
            if(ReferenceEquals(x,y))continue;
            if(x==null||y==null||x.frame!=y.frame||!x.x.Equals(y.x)||!x.y.Equals(y.y)||x.sprite!=y.sprite||x.subimage!=y.subimage||
                !x.xscale.Equals(y.xscale)||!x.yscale.Equals(y.yscale)||!x.alpha.Equals(y.alpha)||!x.angle.Equals(y.angle)||x.facing!=y.facing||
                !x.vspeed.Equals(y.vspeed)||!x.hspeed.Equals(y.hspeed)||x.action!=y.action||x.jumping!=y.jumping||x.shooting!=y.shooting||x.dead!=y.dead)return false;
        }
        return true;
    }
    private static List<PlayerTracePoint> Parse(TextReader reader)
    {
        var points=new List<PlayerTracePoint>();
        string line;
        while((line=reader.ReadLine())!=null)
        {
            if(string.IsNullOrWhiteSpace(line))continue;
            var fields=line.Split(',');
            int frame;
            double x,y;
            if(fields.Length<3||!Int32.TryParse(fields[0],NumberStyles.Integer,CultureInfo.InvariantCulture,out frame)||!Double.TryParse(fields[1],NumberStyles.Float,CultureInfo.InvariantCulture,out x)||!Double.TryParse(fields[2],NumberStyles.Float,CultureInfo.InvariantCulture,out y)||Double.IsNaN(x)||Double.IsInfinity(x)||Double.IsNaN(y)||Double.IsInfinity(y))continue;
            if(points.Count>0&&frame<=points[points.Count-1].frame)points.Clear();
            var pt=new PlayerTracePoint{frame=frame,x=x,y=y};

            if(fields.Length>3)
            {
                double numVal;
                if(Double.TryParse(fields[3],NumberStyles.Float,CultureInfo.InvariantCulture,out numVal))
                {
                    pt.facing=numVal<0?-1:1;
                    pt.xscale=pt.facing;
                    double vspeed;if(fields.Length>4&&Double.TryParse(fields[4],NumberStyles.Float,CultureInfo.InvariantCulture,out vspeed))pt.vspeed=vspeed;
                    double hspeed;if(fields.Length>5&&Double.TryParse(fields[5],NumberStyles.Float,CultureInfo.InvariantCulture,out hspeed))pt.hspeed=hspeed;
                    if(fields.Length>6&&!string.IsNullOrEmpty(fields[6]))
                    {
                        pt.action=fields[6].Trim();
                        if(pt.action=="jump")pt.sprite="sprPlayerJump";
                        else if(pt.action=="fall")pt.sprite="sprPlayerFall";
                        else if(pt.action=="run")pt.sprite="sprPlayerRunning";
                        else pt.sprite="sprPlayerIdle";
                    }
                    int jmp;if(fields.Length>7&&Int32.TryParse(fields[7],out jmp))pt.jumping=jmp!=0;
                    int sht;if(fields.Length>8&&Int32.TryParse(fields[8],out sht))pt.shooting=sht!=0;
                    int dead;if(fields.Length>9&&Int32.TryParse(fields[9],out dead))pt.dead=dead!=0;
                }
                else
                {
                    pt.sprite=fields[3].Trim();
                    int sub;if(fields.Length>4&&Int32.TryParse(fields[4],out sub))pt.subimage=sub;
                    double xs;if(fields.Length>5&&Double.TryParse(fields[5],NumberStyles.Float,CultureInfo.InvariantCulture,out xs)){pt.xscale=xs;pt.facing=xs<0?-1:1;}
                    double ys;if(fields.Length>6&&Double.TryParse(fields[6],NumberStyles.Float,CultureInfo.InvariantCulture,out ys))pt.yscale=ys;
                    double a;if(fields.Length>7&&Double.TryParse(fields[7],NumberStyles.Float,CultureInfo.InvariantCulture,out a))pt.alpha=a;
                    double ang;if(fields.Length>8&&Double.TryParse(fields[8],NumberStyles.Float,CultureInfo.InvariantCulture,out ang))pt.angle=ang;
                    int dead;if(fields.Length>9&&Int32.TryParse(fields[9],out dead))pt.dead=dead!=0;

                    if(pt.sprite.IndexOf("jump",StringComparison.OrdinalIgnoreCase)>=0)pt.action="jump";
                    else if(pt.sprite.IndexOf("fall",StringComparison.OrdinalIgnoreCase)>=0)pt.action="fall";
                    else if(pt.sprite.IndexOf("run",StringComparison.OrdinalIgnoreCase)>=0)pt.action="run";
                    else if(pt.sprite.IndexOf("slide",StringComparison.OrdinalIgnoreCase)>=0)pt.action="sliding";
                    else pt.action="idle";
                }
            }
            points.Add(pt);
        }
        return points;
    }
}
// Ephemeral UI request identity, never serialized into the scene. A path alone
// cannot distinguish a reload of the same project or an undo-replaced state.
internal sealed class PlayerTraceSession
{
    internal readonly object Document;
    internal readonly EditorState State;
    internal int Pid;
    internal string Path;
    internal List<PlayerTracePoint> LastComplete;
    internal PlayerTraceSession(object document,EditorState state){Document=document;State=state;}
}
internal sealed class PlayerTraceRequests
{
    internal PlayerTraceSession Current {get;private set;}
    internal PlayerTraceSession Begin(object document,EditorState state)
    {Current=new PlayerTraceSession(document,state);return Current;}
    internal bool IsCurrent(PlayerTraceSession request,object document,EditorState state)
    {return request!=null&&ReferenceEquals(Current,request)&&document!=null&&state!=null&&ReferenceEquals(request.Document,document)&&ReferenceEquals(request.State,state);}
    internal bool Cancel(PlayerTraceSession request)
    {if(request==null||!ReferenceEquals(Current,request))return false;Current=null;return true;}
    internal void Stop(){Current=null;}
}
internal sealed partial class MainForm
{
    private readonly CheckBox tracePreview=new CheckBox{Text="使用试玩轨迹",AutoSize=true,Margin=new Padding(8,9,5,0)};
    private readonly Timer traceWatch=new Timer{Interval=300};
    private readonly PlayerTraceRequests traceRequests=new PlayerTraceRequests();
    private string tracePath;
    private void BuildTraceTools(Control bar)
    {
        bar.Controls.Add(tracePreview);tracePreview.CheckedChanged+=(s,e)=>{if(state==null||state.usePlayerTrace==tracePreview.Checked)return;if(!tracePreview.Checked)StopWatchingPlayerTrace();PushUndo();state.usePlayerTrace=tracePreview.Checked;MarkChanged();stage.SceneChanged();};bar.Controls.Add(Button("载入玩家轨迹…",()=>ImportPlayerTrace(true),false,110));
        traceWatch.Tick+=(s,e)=>{
            var request=traceRequests.Current;
            if(!IsCurrentPlayerTrace(request)){StopWatchingPlayerTrace(request);return;}
            bool running=false;
            try{using(var process=Process.GetProcessById(request.Pid))if(!process.HasExited)running=true;}catch(ArgumentException){}
            if(running)PollLivePlayerTrace(request);
            else FinishPlayerTrace(request);
        };
        FormClosed+=(s,e)=>{StopWatchingPlayerTrace();traceWatch.Dispose();};
    }
    private bool IsCurrentPlayerTrace(PlayerTraceSession request)
    {return traceRequests.IsCurrent(request,document,state);}
    private PlayerTraceSession BeginPlayerTraceRequest()
    {StopWatchingPlayerTrace();return traceRequests.Begin(document,state);}
    private void StopWatchingPlayerTrace(PlayerTraceSession request=null)
    {
        // A cancelled/finished old request must not stop a newer watch.
        if(request!=null&&!traceRequests.Cancel(request))return;
        traceRequests.Stop();traceWatch.Stop();tracePath=null;
    }
    private void PollLivePlayerTrace(PlayerTraceSession request)
    {
        if(!IsCurrentPlayerTrace(request))return;
        List<PlayerTracePoint> points;
        if(!PlayerTrace.TryReadLive(request.Path,out points)||!IsCurrentPlayerTrace(request))return;
        request.LastComplete=points;
        if(!PlayerTrace.Same(state.playerTrace,points))ApplyPlayerTrace(points,false);
    }
    private bool WatchPlayerTrace(BuildResult result,PlayerTraceSession request)
    {
        if(!IsCurrentPlayerTrace(request)||result==null||String.IsNullOrEmpty(result.dir)||result.pid<=0)return false;
        traceWatch.Stop();request.Pid=result.pid;request.Path=Path.Combine(result.dir,"Data","rufmaker_player_trace.csv");tracePath=request.Path;
        bool enablePreview=!state.usePlayerTrace;state.usePlayerTrace=true;tracePreview.Checked=true;
        if(enablePreview)stage.SceneChanged();traceWatch.Start();return true;
    }
    private void FinishPlayerTrace(PlayerTraceSession request)
    {
        if(!IsCurrentPlayerTrace(request))return;
        traceWatch.Stop();
        try
        {
            List<PlayerTracePoint> points;
            if(!PlayerTrace.TryReadLive(request.Path,out points))points=request.LastComplete;
            if(points!=null&&points.Count>0&&IsCurrentPlayerTrace(request))ApplyPlayerTrace(points,true);
        }
        finally{traceRequests.Cancel(request);}
    }
    private void ApplyPlayerTrace(List<PlayerTracePoint> points,bool persist)
    {
        if(persist)PushUndo();state.playerTrace=points;state.usePlayerTrace=true;tracePreview.Checked=true;
        if(persist)MarkChanged();stage.SceneChanged();
        if(persist)SetStatus("已载入试玩玩家轨迹 · "+points.Count+" 帧；追踪预览按本次走位重放。");
    }
    private void ImportPlayerTrace(bool pick)
    {
        if(state==null||document==null)return;
        var originalDocument=document;var originalState=state;string path=tracePath;
        if(pick){using(var dialog=new OpenFileDialog{Title="载入 F5 试玩玩家轨迹",Filter="玩家轨迹 CSV|*.csv",InitialDirectory=path!=null&&Directory.Exists(Path.GetDirectoryName(path))?Path.GetDirectoryName(path):document.project}){if(dialog.ShowDialog(this)!=DialogResult.OK)return;path=dialog.FileName;}}
        if(!ReferenceEquals(document,originalDocument)||!ReferenceEquals(state,originalState)||String.IsNullOrEmpty(path)||!File.Exists(path))return;
        try
        {
            var points=PlayerTrace.Read(path);if(points.Count==0){if(pick)SetStatus("轨迹文件没有有效的帧与坐标。");return;}
            StopWatchingPlayerTrace();tracePath=path;ApplyPlayerTrace(points,true);
        }
        catch(Exception error){if(pick)Error(error);else SetStatus("玩家轨迹未载入："+error.Message);}
    }
}
