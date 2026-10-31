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
        var points=new List<PlayerTracePoint>();
        if(!File.Exists(path))return points;
        try
        {
            using(var fs=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
            using(var reader=new StreamReader(fs))
            {
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
            }
        }
        catch{}
        return points;
    }
}
internal sealed partial class MainForm
{
    private readonly CheckBox tracePreview=new CheckBox{Text="使用试玩轨迹",AutoSize=true,Margin=new Padding(8,9,5,0)};
    private readonly Timer traceWatch=new Timer{Interval=300};private int tracePid;private string tracePath,traceProject;
    private void BuildTraceTools(Control bar)
    {
        bar.Controls.Add(tracePreview);tracePreview.CheckedChanged+=(s,e)=>{if(state==null||state.usePlayerTrace==tracePreview.Checked)return;PushUndo();state.usePlayerTrace=tracePreview.Checked;MarkChanged();stage.SceneChanged();};bar.Controls.Add(Button("载入玩家轨迹…",()=>ImportPlayerTrace(true),false,110));
        traceWatch.Tick+=(s,e)=>{
            bool running=false;
            try{using(var process=Process.GetProcessById(tracePid))if(!process.HasExited)running=true;}catch(ArgumentException){}
            if(running){PollLivePlayerTrace();}
            else{traceWatch.Stop();if(document!=null&&document.project==traceProject)ImportPlayerTrace(false);}
        };
        FormClosed+=(s,e)=>traceWatch.Dispose();
    }
    private void PollLivePlayerTrace()
    {
        if(state==null||String.IsNullOrEmpty(tracePath)||!File.Exists(tracePath))return;
        try{
            var points=PlayerTrace.Read(tracePath);
            if(points.Count>0&&(state.playerTrace==null||points.Count!=state.playerTrace.Count)){
                state.playerTrace=points;
                state.usePlayerTrace=true;
                tracePreview.Checked=true;
                stage.SceneChanged();
            }
        }catch{}
    }
    private void WatchPlayerTrace(BuildResult result)
    {traceWatch.Stop();tracePid=result.pid;traceProject=document.project;tracePath=Path.Combine(result.dir,"Data","rufmaker_player_trace.csv");if(state!=null){state.usePlayerTrace=true;tracePreview.Checked=true;}traceWatch.Start();}
    private void ImportPlayerTrace(bool pick)
    {
        if(state==null)return;string path=tracePath;if(pick){using(var dialog=new OpenFileDialog{Title="载入 F5 试玩玩家轨迹",Filter="玩家轨迹 CSV|*.csv",InitialDirectory=path!=null&&Directory.Exists(Path.GetDirectoryName(path))?Path.GetDirectoryName(path):document.project}){if(dialog.ShowDialog(this)!=DialogResult.OK)return;path=dialog.FileName;}}
        if(String.IsNullOrEmpty(path)||!File.Exists(path))return;try{var points=PlayerTrace.Read(path);if(points.Count==0){if(pick)SetStatus("轨迹文件没有有效的帧与坐标。");return;}PushUndo();state.playerTrace=points;state.usePlayerTrace=true;tracePreview.Checked=true;MarkChanged();stage.SceneChanged();SetStatus("已载入试玩玩家轨迹 · "+points.Count+" 帧；追踪预览按本次走位重放。");}catch(Exception error){if(pick)Error(error);else SetStatus("玩家轨迹未载入："+error.Message);}
    }
}
