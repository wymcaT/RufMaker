using System;
using System.IO;
using System.Linq;
internal static class LinuxPortSmokeTests
{
 static int Main(string[] args){try{
 var a1=new Track{id="a",name="parent",kind="group"};var b1=new Track{id="b",name="child",parentId="a"};var c1=new Track{id="c",name="other"};var state=new EditorState();state.tracks.AddRange(new[]{a1,b1,c1});
 if(StudioOutline.Rows(state,"").Length!=3)throw new Exception("outline completeness");
 if(StudioOutline.Rows(state,"child").Length!=2||StudioOutline.Parent(state,b1)!=a1)throw new Exception("ancestor preservation");
 a1.parentId="b";if(StudioOutline.Parent(state,b1)!=null)throw new Exception("cycle safety");
 if(state.tracks.Count!=3)throw new Exception("source mutated");Console.WriteLine("PASS outline completeness, ancestor search, cycle safety, non-mutation");
 if(Environment.OSVersion.Platform==PlatformID.Win32NT)throw new Exception("Linux required");
 using(var a=new NativeAudio()){a.Load(args[0]);if(a.Duration<1)throw new Exception("duration");Console.WriteLine("PASS Linux NativeAudio metadata: "+a.Duration);}
 var wave=AudioAnalyzer.Analyze(args[0]);if(wave.duration<1||wave.peaks==null||!wave.peaks.Any(p=>p>0))throw new Exception("waveform");Console.WriteLine("PASS FFmpeg waveform decode: "+wave.peaks.Length+" bins");
 Console.WriteLine("PASS Linux audio disposal; no Windows FMOD invoked");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
