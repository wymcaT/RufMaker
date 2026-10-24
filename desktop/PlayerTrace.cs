using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

internal static class PlayerTrace
{
    public static List<PlayerTracePoint> Read(string path)
    {
        var points=new List<PlayerTracePoint>();foreach(string line in File.ReadLines(path)){var fields=line.Split(',');int frame;double x,y;if(fields.Length!=3||!Int32.TryParse(fields[0],NumberStyles.Integer,CultureInfo.InvariantCulture,out frame)||!Double.TryParse(fields[1],NumberStyles.Float,CultureInfo.InvariantCulture,out x)||!Double.TryParse(fields[2],NumberStyles.Float,CultureInfo.InvariantCulture,out y)||Double.IsNaN(x)||Double.IsInfinity(x)||Double.IsNaN(y)||Double.IsInfinity(y))continue;if(points.Count>0&&frame<=points[points.Count-1].frame)points.Clear();points.Add(new PlayerTracePoint{frame=frame,x=x,y=y});}return points;
    }
}
internal sealed partial class MainForm
{
    private readonly CheckBox tracePreview=new CheckBox{Text="使用试玩轨迹",AutoSize=true,Margin=new Padding(8,9,5,0)};
    private readonly Timer traceWatch=new Timer{Interval=1000};private int tracePid;private string tracePath,traceProject;
    private void BuildTraceTools(Control bar)
    {
        bar.Controls.Add(tracePreview);tracePreview.CheckedChanged+=(s,e)=>{if(state==null||state.usePlayerTrace==tracePreview.Checked)return;PushUndo();state.usePlayerTrace=tracePreview.Checked;MarkChanged();};bar.Controls.Add(Button("载入玩家轨迹…",()=>ImportPlayerTrace(true),false,110));traceWatch.Tick+=(s,e)=>{try{using(var process=Process.GetProcessById(tracePid))if(!process.HasExited)return;}catch(ArgumentException){}traceWatch.Stop();if(document!=null&&document.project==traceProject)ImportPlayerTrace(false);};FormClosed+=(s,e)=>traceWatch.Dispose();
    }
    private void WatchPlayerTrace(BuildResult result)
    {traceWatch.Stop();tracePid=result.pid;traceProject=document.project;tracePath=Path.Combine(result.dir,"Data","rufmaker_player_trace.csv");traceWatch.Start();}
    private void ImportPlayerTrace(bool pick)
    {
        if(state==null)return;string path=tracePath;if(pick){using(var dialog=new OpenFileDialog{Title="载入 F5 试玩玩家轨迹",Filter="玩家轨迹 CSV|*.csv",InitialDirectory=path!=null&&Directory.Exists(Path.GetDirectoryName(path))?Path.GetDirectoryName(path):document.project}){if(dialog.ShowDialog(this)!=DialogResult.OK)return;path=dialog.FileName;}}
        if(String.IsNullOrEmpty(path)||!File.Exists(path))return;try{var points=PlayerTrace.Read(path);if(points.Count==0){if(pick)SetStatus("轨迹文件没有有效的帧与坐标。");return;}PushUndo();state.playerTrace=points;state.usePlayerTrace=true;tracePreview.Checked=true;MarkChanged();SetStatus("已载入试玩玩家轨迹 · "+points.Count+" 帧；追踪预览按本次走位重放。");}catch(Exception error){if(pick)Error(error);else SetStatus("玩家轨迹未载入："+error.Message);}
    }
}
