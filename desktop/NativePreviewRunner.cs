using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

#pragma warning disable 0649 // Session fields are populated by the bridge JSON response.
internal sealed class NativeSession {public string exe,dir,commandFile,statusFile,frameFile;public bool cached;public int width,height;}
#pragma warning restore 0649
internal sealed class NativeRunnerStatus
{
    public int sequence,frame,target,test;public bool playing,rewind;public string room;
}
internal sealed class NativePreviewRunner:IDisposable
{
    private readonly NativeSession session;private Process process;private int serial,generation;
    public NativePreviewRunner(NativeSession session){this.session=session;}
    public Process Process {get{return process;}}
    public bool Alive {get{return process!=null&&!process.HasExited;}}
    public async Task Restart(int frame,bool playing)
    {
        int current=++generation;var previous=process;process=null;
        if(previous!=null){
            try{if(!previous.HasExited){previous.Kill();if(!await Task.Run(()=>previous.WaitForExit(3000)))throw new IOException("上一次原生预览尚未退出，请稍后重试。");}}
            finally{previous.Dispose();}
        }
        if(current!=generation)return;
        foreach(string file in new[]{session.statusFile,session.frameFile})if(!String.IsNullOrEmpty(file)&&File.Exists(file))File.Delete(file);
        WriteControl(frame,playing,true);
        process=System.Diagnostics.Process.Start(new ProcessStartInfo{FileName=session.exe,WorkingDirectory=session.dir,UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});
    }
    public void WriteControl(int frame,bool playing,bool seek)
    {
        int next=serial+1;
        string data="[control]\r\nserial="+next+"\r\nframe="+Math.Max(0,frame)+"\r\nplaying="+(playing?1:0)+"\r\nplayable=0\r\nseek="+(seek?1:0)+"\r\n",temp=session.commandFile+".tmp";
        File.WriteAllText(temp,data);if(File.Exists(session.commandFile))File.Replace(temp,session.commandFile,null);else File.Move(temp,session.commandFile);serial=next;
    }
    private static bool Number(Dictionary<string,string> values,string key,out int value)
    {
        string text;double parsed;value=0;
        if(!values.TryGetValue(key,out text)||!Double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out parsed)||Double.IsNaN(parsed)||Double.IsInfinity(parsed)||parsed<Int32.MinValue||parsed>Int32.MaxValue)return false;
        value=(int)Math.Round(parsed);return true;
    }
    public NativeRunnerStatus ReadStatus()
    {
        try{
            var values=new Dictionary<string,string>();foreach(string line in File.ReadAllLines(session.statusFile)){int at=line.IndexOf('=');if(at>0)values[line.Substring(0,at).Trim()]=line.Substring(at+1).Trim().Trim('"');}
            var status=new NativeRunnerStatus();int flag;
            if(!Number(values,"sequence",out status.sequence)||status.sequence<serial||!Number(values,"frame",out status.frame)||!Number(values,"target",out status.target))return null;
            status.playing=Number(values,"playing",out flag)&&flag!=0;status.rewind=Number(values,"rewind",out flag)&&flag!=0;Number(values,"test",out status.test);values.TryGetValue("room",out status.room);return status;
        }catch(IOException){return null;}
    }
    public void Stop()
    {
        generation++;var owned=process;process=null;
        if(owned!=null){try{if(!owned.HasExited)owned.Kill();}catch(InvalidOperationException){}finally{owned.Dispose();}}
    }
    public void Dispose(){Stop();}
}
