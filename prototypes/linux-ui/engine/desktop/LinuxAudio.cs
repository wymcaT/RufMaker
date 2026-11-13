using System;
using System.IO;
using System.Diagnostics;
using System.Globalization;
internal sealed class LinuxAudio : IDisposable
{
    private Process player; private string file; private double position,rate=1,duration; private readonly Stopwatch clock=new Stopwatch();
    public double Duration {get{return duration;}} public bool Playing{get;private set;} public double Rate{get{return rate;}}
    public float Volume=.65f;
    private static string Quote(string s){return "\""+s.Replace("\\","\\\\").Replace("\"","\\\"")+"\"";}
    public void Load(string path){Dispose();file=path;var i=new ProcessStartInfo("ffprobe","-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 "+Quote(path)){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};using(var p=Process.Start(i)){var output=p.StandardOutput.ReadToEnd();var error=p.StandardError.ReadToEnd();p.WaitForExit();if(p.ExitCode!=0||!double.TryParse(output.Trim(),NumberStyles.Float,CultureInfo.InvariantCulture,out duration))throw new InvalidOperationException("Linux 音频读取失败："+error);}}
    public double Position{get{return Playing?Math.Max(0,Math.Min(duration,position+clock.Elapsed.TotalSeconds*rate)):position;}}
    public void Play(double seconds,double speed){Pause();if(speed<=0)throw new InvalidOperationException("Linux 版暂不支持倒放音频。");position=Math.Max(0,Math.Min(duration,seconds));rate=speed;if(position>=duration)return;string tempo="";double r=speed;while(r>2){tempo+="atempo=2,";r/=2;}while(r<.5){tempo+="atempo=0.5,";r*=2;}tempo+="atempo="+r.ToString(CultureInfo.InvariantCulture);var i=new ProcessStartInfo("ffplay","-nodisp -autoexit -loglevel error -ss "+position.ToString(CultureInfo.InvariantCulture)+" -volume "+((int)(Volume*100))+" -af "+tempo+" "+Quote(file)){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true};player=Process.Start(i);player.ErrorDataReceived+=(s,e)=>{if(e.Data!=null)Console.Error.WriteLine(e.Data);};player.BeginErrorReadLine();clock.Restart();Playing=true;}
    public void Pause(){position=Position;clock.Stop();Playing=false;if(player!=null){try{if(!player.HasExited)player.Kill();player.WaitForExit();}catch(InvalidOperationException){}player.Dispose();player=null;}}
    public void Seek(double value){if(Playing)Play(value,rate);else position=value;}
    public void SetRate(double value){if(Playing)Play(Position,value);else rate=value;}
    public void SetVolume(float value){Volume=value;if(Playing)Play(Position,rate);}
    public void Update(){if(Playing&&player!=null&&player.HasExited){position=Position;clock.Stop();Playing=false;}}
    public void Dispose(){Pause();duration=0;position=0;rate=1;}
}
