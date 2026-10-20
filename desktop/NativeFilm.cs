using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Web.Script.Serialization;

#pragma warning disable 0649
internal class NativeFilmInfo
{
    public int version=1,fps=50,width,height,frameCount,playerX=400,playerY=304;public string room,generatedUtc;public string[] nativeIds=new string[0];public NativeViewPose[] views;
    public NativeViewPose View(int frame){return views==null||views.Length==0?null:views[Math.Max(0,Math.Min(views.Length-1,frame))];}
}
internal sealed class NativeFilmJob:NativeFilmInfo {public string exe,dir,statusFile,frameFile,ackDirectory,output;public string[] players;}
#pragma warning restore 0649
internal sealed class NativeFilmProgress {public int frame,total;public string phase,path,error;}
internal sealed class NativeFilmReader:IDisposable
{
    private readonly FileStream stream;private readonly ZipArchive archive;private readonly Dictionary<int,Bitmap> frames=new Dictionary<int,Bitmap>();private readonly Queue<int> order=new Queue<int>();
    public readonly NativeFilmInfo Info;public readonly HashSet<string> NativeIds;
    public static T ReadJson<T>(string text){return new JavaScriptSerializer{MaxJsonLength=20000000}.Deserialize<T>(text);}
    public static string Json(object value){return new JavaScriptSerializer{MaxJsonLength=20000000}.Serialize(value);}
    public NativeFilmReader(string path)
    {
        stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        try{archive=new ZipArchive(stream,ZipArchiveMode.Read);var manifest=archive.GetEntry("manifest.json");if(manifest==null)throw new IOException("底片缺少索引，请重新生成。");using(var reader=new StreamReader(manifest.Open()))Info=ReadJson<NativeFilmInfo>(reader.ReadToEnd());if(Info==null||Info.version!=1||Info.frameCount<1||Info.fps<1)throw new IOException("底片格式无效，请重新生成。");NativeIds=new HashSet<string>(Info.nativeIds??new string[0]);}
        catch{if(archive!=null)archive.Dispose();stream.Dispose();throw;}
    }
    public Bitmap Frame(int frame)
    {
        frame=Math.Max(0,Math.Min(Info.frameCount-1,frame));Bitmap image;if(frames.TryGetValue(frame,out image))return image;
        var entry=archive.GetEntry(frame.ToString("D6",CultureInfo.InvariantCulture)+".jpg");if(entry==null)throw new IOException("底片缺少第 "+frame+" 帧，请重新生成。");
        using(var memory=new MemoryStream()){using(var input=entry.Open())input.CopyTo(memory);memory.Position=0;using(var original=Image.FromStream(memory))image=new Bitmap(original);}
        frames.Add(frame,image);order.Enqueue(frame);while(order.Count>8){int old=order.Dequeue();frames[old].Dispose();frames.Remove(old);}return image;
    }
    public void Dispose(){foreach(var frame in frames.Values)frame.Dispose();frames.Clear();archive.Dispose();stream.Dispose();}
}
internal static class NativeFilmBuilder
{
    private static NativeViewPose StatusView(string file)
    {
        var values=new Dictionary<string,double>();foreach(string line in File.ReadAllLines(file)){int at=line.IndexOf('=');double value;if(at>0&&Double.TryParse(line.Substring(at+1).Trim(),NumberStyles.Float,CultureInfo.InvariantCulture,out value))values[line.Substring(0,at)]=value;}
        if(!values.ContainsKey("view"))return null;Func<string,double> get=key=>values.ContainsKey(key)?values[key]:0;
        return new NativeViewPose{enabled=get("view")!=0,x=get("x"),y=get("y"),w=get("w"),h=get("h"),angle=get("angle"),portX=get("portX"),portY=get("portY"),portW=get("portW"),portH=get("portH"),outputW=get("outputW"),outputH=get("outputH")};
    }
    private static int StatusFrame(string file)
    {
        try{using(var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var reader=new StreamReader(stream)){string line;while((line=reader.ReadLine())!=null){int at=line.IndexOf('=');double value;if(at>0&&line.Substring(0,at).Trim()=="frame"&&Double.TryParse(line.Substring(at+1).Trim().Trim('"'),NumberStyles.Float,CultureInfo.InvariantCulture,out value))return (int)Math.Round(value);}}}
        catch(IOException){}return -1;
    }
    private static void Ack(string directory,int frame,CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();using(File.Create(Path.Combine(directory,frame.ToString(CultureInfo.InvariantCulture)+".ok"))){}
        if(frame>0){string previous=Path.Combine(directory,(frame-1).ToString(CultureInfo.InvariantCulture)+".ok");if(File.Exists(previous))File.Delete(previous);}
    }
    public static NativeFilmInfo Generate(NativeFilmJob job,CancellationToken cancel,IProgress<NativeFilmProgress> progress=null)
    {
        if(job.frameCount<1||job.fps!=50)throw new IOException("原生底片的帧数或 FPS 无效。");
        Directory.CreateDirectory(Path.GetDirectoryName(job.output));string temporary=job.output+".partial-"+Guid.NewGuid().ToString("N");Process process=null;
        var info=new NativeFilmInfo{fps=job.fps,width=job.width,height=job.height,frameCount=job.frameCount,room=job.room,nativeIds=job.nativeIds,generatedUtc=DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture)};
        try{
            string errorLog=Path.Combine(job.dir,"game_errors.log");foreach(string file in new[]{job.statusFile,job.frameFile,errorLog})if(File.Exists(file))File.Delete(file);
            Directory.CreateDirectory(job.ackDirectory);foreach(string marker in Directory.GetFiles(job.ackDirectory,"*.ok"))File.Delete(marker);
            using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.Read))using(var archive=new ZipArchive(output,ZipArchiveMode.Create))using(var options=new EncoderParameters(1)){
                options.Param[0]=new EncoderParameter(System.Drawing.Imaging.Encoder.Quality,92L);var codec=ImageCodecInfo.GetImageEncoders().First(c=>c.FormatID==ImageFormat.Jpeg.Guid);
                process=Process.Start(new ProcessStartInfo{FileName=job.exe,WorkingDirectory=job.dir,UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});
                int captured=0;var views=new List<NativeViewPose>();
                for(int frame=0;frame<job.frameCount;frame++){
                    var wait=Stopwatch.StartNew();bool ended=false;
                    for(;;){cancel.ThrowIfCancellationRequested();int actual=StatusFrame(job.statusFile);if(actual==frame)break;if(actual>frame)throw new IOException("底片录制跳过了第 "+frame+" 帧，请重新生成。");if(process.HasExited){if(process.ExitCode==0&&frame>0&&(!File.Exists(errorLog)||new FileInfo(errorLog).Length==0)){ended=true;break;}throw new IOException("原生录制程序提前退出，记录目录："+job.dir);}if(wait.Elapsed.TotalSeconds>30)throw new IOException("等待第 "+frame+" 帧超过 30 秒（最后状态 "+actual+"），记录目录："+job.dir);Thread.Sleep(3);}
                    if(ended)break;
                    using(var memory=new MemoryStream(File.ReadAllBytes(job.frameFile)))using(var bitmap=Image.FromStream(memory))using(var encoded=new MemoryStream())using(var entry=archive.CreateEntry(frame.ToString("D6",CultureInfo.InvariantCulture)+".jpg",CompressionLevel.NoCompression).Open()){
                        if(frame==0){info.width=bitmap.Width;info.height=bitmap.Height;}bitmap.Save(encoded,codec,options);encoded.Position=0;encoded.CopyTo(entry);
                    }
                    views.Add(StatusView(job.statusFile));captured=frame+1;Ack(job.ackDirectory,frame,cancel);if(progress!=null)progress.Report(new NativeFilmProgress{frame=captured,total=job.frameCount,phase="rendering",path=job.output});
                }
                info.frameCount=captured;info.views=views.ToArray();
                using(var writer=new StreamWriter(archive.CreateEntry("manifest.json",CompressionLevel.NoCompression).Open()))writer.Write(NativeFilmReader.Json(info));
            }
            cancel.ThrowIfCancellationRequested();if(File.Exists(job.output))File.Replace(temporary,job.output,null);else File.Move(temporary,job.output);return info;
        }
        finally{if(process!=null){try{if(!process.HasExited){process.Kill();process.WaitForExit(3000);}}catch(InvalidOperationException){}finally{process.Dispose();}}if(File.Exists(temporary))File.Delete(temporary);}
    }
    public static int RunJobFile(string jobFile,string statusFile)
    {
        Action<NativeFilmProgress> report=value=>{
            string temp=statusFile+".tmp";File.WriteAllText(temp,NativeFilmReader.Json(value));
            for(int attempt=0;;attempt++){try{if(File.Exists(statusFile))File.Replace(temp,statusFile,null);else File.Move(temp,statusFile);return;}catch(IOException){if(attempt>=100){if(File.Exists(temp))File.Delete(temp);return;}Thread.Sleep(5);}}
        };
        try{var job=NativeFilmReader.ReadJson<NativeFilmJob>(File.ReadAllText(jobFile));report(new NativeFilmProgress{phase="starting",total=job.frameCount});var info=Generate(job,CancellationToken.None,new DirectProgress(report));report(new NativeFilmProgress{phase="done",frame=info.frameCount,total=info.frameCount,path=job.output});return 0;}
        catch(Exception error){report(new NativeFilmProgress{phase="error",error=error.Message});return 1;}
    }
    private sealed class DirectProgress:IProgress<NativeFilmProgress> {private readonly Action<NativeFilmProgress> report;public DirectProgress(Action<NativeFilmProgress> report){this.report=report;}public void Report(NativeFilmProgress value){if(value.frame%10==0||value.frame==value.total)report(value);}}
}
