using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

internal sealed class EngineInspection
{
    public string Exe;public string[] Missing=new string[0];
    public bool Ready {get{return Missing.Length==0;}}
}
internal sealed class EngineSetupProgress
{
    public string Text;public int Percent=-1;
    public EngineSetupProgress(string text,int percent=-1){Text=text;Percent=percent;}
}
internal static class EngineSetup
{
    public const string Website="https://gm82.cherry-treehouse.com/";
    public const string PortablePage="https://www.mediafire.com/file/6l3h4dr7ajj6km2/Game_Maker_8.2_portable.7z/file";
    public const string VcUrl="https://aka.ms/vs/17/release/vc_redist.x86.exe";
    public const string DxUrl="https://download.microsoft.com/download/1/7/1/1718CCC4-6315-4D8E-9543-8E28A4E18C4C/dxwebsetup.exe";
    internal static readonly string[] Required={"gm82save.dll","rundata","fnames","gm82room.exe",
        "extensions/Game Maker 8.2 Core.ged","extensions/Game Maker 8.2 Core.dat",
        "extensions/Game Maker 8.2 Sound.ged","extensions/Game Maker 8.2 Sound.dat",
        "extensions/Game Maker 8.2 DirectX9.ged","extensions/Game Maker 8.2 DirectX9.dat"};
    private static string SettingsFile(string root){return Path.Combine(root,"user-data","settings.json");}
    private static Dictionary<string,string> Settings(string root)
    {string file=SettingsFile(root);return File.Exists(file)?Json.Read<Dictionary<string,string>>(File.ReadAllText(file)):new Dictionary<string,string>();}
    private static void SetSetting(string root,string key,string value)
    {var settings=Settings(root);settings[key]=value;string file=SettingsFile(root);Directory.CreateDirectory(Path.GetDirectoryName(file));File.WriteAllText(file,Json.Write(settings),new UTF8Encoding(false));}
    public static void SkipStartup(string root){SetSetting(root,"engineSetupSkipped","true");}
    public static bool OfferAtStartup(string root,EngineInspection found)
    {string skipped;return !found.Ready&&(!Settings(root).TryGetValue("engineSetupSkipped",out skipped)||skipped!="true");}
    public static EngineInspection Inspect(string selection)
    {
        string exe=String.IsNullOrWhiteSpace(selection)?"":Path.GetFullPath(selection);if(Directory.Exists(exe))exe=Path.Combine(exe,"GameMaker.exe");
        var missing=new List<string>();if(!File.Exists(exe))missing.Add("GameMaker.exe");else if(!IsExecutable(exe))missing.Add("GameMaker.exe（不是有效的 Windows 程序）");
        string folder=String.IsNullOrEmpty(exe)?"":Path.GetDirectoryName(exe);foreach(string relative in Required)if(!File.Exists(Path.Combine(folder,relative)))missing.Add(relative);
        return new EngineInspection{Exe=exe,Missing=missing.ToArray()};
    }
    public static EngineInspection Find(string root)
    {
        var candidates=new List<string>();string chosen;if(Settings(root).TryGetValue("gameMaker",out chosen))candidates.Add(chosen);candidates.Add(Environment.GetEnvironmentVariable("RUFMAKER_GAMEMAKER"));
        candidates.Add(Path.Combine(root,"GameMaker8.2","GameMaker.exe"));candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"GameMaker8.2","GameMaker.exe"));
        foreach(var hive in new[]{Registry.CurrentUser,Registry.LocalMachine})using(var key=hive.OpenSubKey(@"Software\NSIS_GM82")){if(key!=null){string install=key.GetValue("Install_Dir") as string;if(!String.IsNullOrWhiteSpace(install))candidates.Add(Path.Combine(install,"GameMaker.exe"));}}
        EngineInspection incomplete=null;foreach(string candidate in candidates.Where(c=>!String.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase)){
            var result=Inspect(candidate);if(result.Ready)return result;if(incomplete==null&&File.Exists(result.Exe))incomplete=result;
        }
        return incomplete??Inspect(Path.Combine(root,"GameMaker8.2","GameMaker.exe"));
    }
    public static EngineInspection Use(string root,string selection)
    {var found=Inspect(selection);if(!found.Ready)throw new InvalidDataException("这个目录缺少 GM8.2 必需文件：\r\n"+String.Join("\r\n",found.Missing));SetSetting(root,"gameMaker",found.Exe);SetSetting(root,"engineSetupSkipped","false");return found;}
    public static string[] MissingRuntimes(string engineDirectory)
    {
        string system=Environment.Is64BitOperatingSystem?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"SysWOW64"):Environment.SystemDirectory;
        return new[]{"vcruntime140.dll","msvcp140.dll","d3dx9_43.dll"}.Where(name=>!File.Exists(Path.Combine(system,name))&&!File.Exists(Path.Combine(engineDirectory??"",name))).ToArray();
    }
    private sealed class DownloadClient:WebClient
    {
        public DownloadClient(){ServicePointManager.SecurityProtocol|=(SecurityProtocolType)3072;Encoding=Encoding.UTF8;Headers[HttpRequestHeader.UserAgent]="Mozilla/5.0 (Windows NT 10.0; Win64; x64) RufMaker/"+AppVersion.Current;}
        protected override WebRequest GetWebRequest(Uri address){var request=base.GetWebRequest(address);request.Timeout=60000;var http=request as HttpWebRequest;if(http!=null)http.ReadWriteTimeout=60000;return request;}
    }
    internal static string Attribute(string tag,string name)
    {var found=Regex.Match(tag,@"\b"+Regex.Escape(name)+@"\s*=\s*([""'])(.*?)\1",RegexOptions.Singleline|RegexOptions.IgnoreCase);return found.Success?WebUtility.HtmlDecode(found.Groups[2].Value):null;}
    internal static Uri PortableLink(string html)
    {
        foreach(Match anchor in Regex.Matches(html,@"<a\b[^>]*>",RegexOptions.IgnoreCase|RegexOptions.Singleline)){
            string link=Attribute(anchor.Value,"href");Uri uri;if(link!=null&&link.IndexOf("Game_Maker_8.2_portable.",StringComparison.OrdinalIgnoreCase)>=0&&Uri.TryCreate(link,UriKind.Absolute,out uri)&&uri.Scheme=="https"&&(uri.Host=="www.mediafire.com"||uri.Host=="gm82.cherry-treehouse.com"))return uri;
        }throw new InvalidDataException("官网没有返回便携包地址，可导入已下载的 ZIP / 7z。请稍后重试。 ");
    }
    internal static Uri DownloadLink(string html)
    {
        foreach(Match anchor in Regex.Matches(html,@"<a\b[^>]*>",RegexOptions.IgnoreCase|RegexOptions.Singleline))if(Attribute(anchor.Value,"id")=="downloadButton"){
            Uri uri;if(Uri.TryCreate(Attribute(anchor.Value,"href"),UriKind.Absolute,out uri)&&uri.Scheme=="https"&&uri.Host.EndsWith(".mediafire.com",StringComparison.OrdinalIgnoreCase))return uri;
        }throw new InvalidDataException("下载站没有返回文件直链。可以重试，或导入本地 ZIP / 7z。");
    }
    private static async Task<string> ReadPage(Uri address,CancellationToken cancellation)
    {cancellation.ThrowIfCancellationRequested();using(var client=new DownloadClient())using(cancellation.Register(client.CancelAsync)){try{string result=await client.DownloadStringTaskAsync(address);cancellation.ThrowIfCancellationRequested();return result;}catch(WebException){cancellation.ThrowIfCancellationRequested();throw;}}}
    private static void Report(IProgress<EngineSetupProgress> progress,string message,int percent=-1)
    {if(progress!=null)progress.Report(new EngineSetupProgress(message,percent));}
    private static async Task Download(Uri address,string destination,CancellationToken cancellation,IProgress<EngineSetupProgress> progress)
    {
        cancellation.ThrowIfCancellationRequested();
        using(var client=new DownloadClient())using(cancellation.Register(client.CancelAsync)){
            client.DownloadProgressChanged+=(s,e)=>Report(progress,"正在下载 · "+(e.BytesReceived/1048576.0).ToString("0.0")+" MB"+(e.TotalBytesToReceive>0?" / "+(e.TotalBytesToReceive/1048576.0).ToString("0.0")+" MB":""),e.TotalBytesToReceive>0?e.ProgressPercentage:-1);
            try{await client.DownloadFileTaskAsync(address,destination);cancellation.ThrowIfCancellationRequested();}catch(WebException){cancellation.ThrowIfCancellationRequested();throw;}
        }
    }
    private static bool IsExecutable(string file)
    {using(var stream=File.OpenRead(file))return stream.Length>1024&&stream.ReadByte()=='M'&&stream.ReadByte()=='Z';}
    private static string Job(string root)
    {string path=Path.Combine(root,"build","engine-setup","job-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);return path;}
    private static void RemoveJob(string root,string path)
    {string parent=Path.GetFullPath(Path.Combine(root,"build","engine-setup")).TrimEnd(Path.DirectorySeparatorChar),full=Path.GetFullPath(path);if(Path.GetDirectoryName(full)!=parent||!Path.GetFileName(full).StartsWith("job-",StringComparison.Ordinal))throw new IOException("引擎准备临时目录异常。");if(Directory.Exists(full))Directory.Delete(full,true);}
    internal static string ArchivePath(string folder,string name)
    {
        name=name.Replace('\\','/');if(Path.IsPathRooted(name)||name.Split('/').Any(p=>p==".."||p.Contains(":")))throw new InvalidDataException("压缩包包含无效路径。");
        return Updater.Inside(folder,name);
    }
    private static async Task<string> RunTar(string arguments,CancellationToken cancellation)
    {
        string windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows),exe=Path.Combine(windows,Environment.Is64BitOperatingSystem&&!Environment.Is64BitProcess?"Sysnative":"System32","tar.exe");
        if(!File.Exists(exe))throw new FileNotFoundException("系统不支持直接解压 7z，请先解压并选择已有目录，或导入 ZIP。");
        using(var process=new Process{StartInfo=new ProcessStartInfo(exe,arguments){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8}}){
            process.Start();var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
            using(cancellation.Register(()=>{try{if(!process.HasExited)process.Kill();}catch(InvalidOperationException){}}))await Task.Run(()=>process.WaitForExit());
            string text=await output,errors=await error;cancellation.ThrowIfCancellationRequested();if(process.ExitCode!=0)throw new InvalidDataException("压缩包解压失败："+errors);return text;
        }
    }
    private static string Quote(string value){return "\""+value.TrimEnd('\\')+"\"";}
    internal static async Task Extract(string archive,string destination,CancellationToken cancellation)
    {
        byte[] signature=new byte[6];using(var input=File.OpenRead(archive))input.Read(signature,0,signature.Length);Directory.CreateDirectory(destination);
        if(signature[0]=='P'&&signature[1]=='K'){
            await Task.Run(()=>{using(var stream=File.OpenRead(archive))using(var zip=new ZipArchive(stream,ZipArchiveMode.Read))foreach(var entry in zip.Entries){
                cancellation.ThrowIfCancellationRequested();string trimmed=entry.FullName.TrimEnd('/','\\');if(trimmed==""||trimmed==".")continue;string file=ArchivePath(destination,entry.FullName);
                if(((entry.ExternalAttributes>>16)&0xF000)==0xA000)throw new InvalidDataException("压缩包不能包含符号链接。");
                if(entry.Name.Length==0){Directory.CreateDirectory(file);continue;}Directory.CreateDirectory(Path.GetDirectoryName(file));using(var source=entry.Open())using(var target=new FileStream(file,FileMode.CreateNew)){var buffer=new byte[65536];int count;while((count=source.Read(buffer,0,buffer.Length))>0){cancellation.ThrowIfCancellationRequested();target.Write(buffer,0,count);}}
            }},cancellation);return;
        }
        if(!signature.SequenceEqual(new byte[]{0x37,0x7A,0xBC,0xAF,0x27,0x1C}))throw new InvalidDataException("下载内容不是有效的 ZIP / 7z 压缩包，请重试或导入本地文件。");
        string entries=await RunTar("-tf "+Quote(archive),cancellation);foreach(string name in entries.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries))if(name.TrimEnd('/','\\')!=".")ArchivePath(destination,name);
        string details=await RunTar("-tvf "+Quote(archive),cancellation);if(Regex.IsMatch(details,@"^[lh]",RegexOptions.Multiline))throw new InvalidDataException("压缩包不能包含符号链接。");
        await RunTar("-xf "+Quote(archive)+" -C "+Quote(destination),cancellation);
    }
    private static void CopyExtras(string source,string target,CancellationToken cancellation)
    {
        if(!Directory.Exists(source))return;if((File.GetAttributes(source)&FileAttributes.ReparsePoint)!=0)throw new IOException("扩展目录不能是符号链接。");Directory.CreateDirectory(target);
        foreach(string file in Directory.GetFiles(source)){cancellation.ThrowIfCancellationRequested();string destination=Path.Combine(target,Path.GetFileName(file));if(!File.Exists(destination))File.Copy(file,destination);}
        foreach(string folder in Directory.GetDirectories(source))CopyExtras(folder,Path.Combine(target,Path.GetFileName(folder)),cancellation);
    }
    internal static EngineInspection Activate(string root,string extracted,CancellationToken cancellation)
    {
        var choices=new[]{Path.Combine(extracted,"GameMaker.exe")}.Concat(Directory.GetFiles(extracted,"GameMaker.exe",SearchOption.AllDirectories)).Distinct();
        var found=choices.Select(Inspect).FirstOrDefault(i=>i.Ready);if(found==null)throw new InvalidDataException("压缩包中没有完整的 GM8.2，请使用官网 Portable 便携包。");
        string candidate=Path.GetDirectoryName(found.Exe),target=Path.GetFullPath(Path.Combine(root,"GameMaker8.2")),backup=Path.Combine(root,"build","engine-setup","previous-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6));
        if(!candidate.StartsWith(Path.GetFullPath(Path.Combine(root,"build","engine-setup"))+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("引擎准备临时目录异常。");
        CopyExtras(Path.Combine(target,"extensions"),Path.Combine(candidate,"extensions"),cancellation);cancellation.ThrowIfCancellationRequested();bool replaced=false;
        try{if(Directory.Exists(target)){Directory.Move(target,backup);replaced=true;}Directory.Move(candidate,target);}catch{if(replaced&&!Directory.Exists(target))Directory.Move(backup,target);throw;}
        return Use(root,Path.Combine(target,"GameMaker.exe"));
    }
    public static async Task<EngineInspection> InstallArchive(string root,string archive,CancellationToken cancellation,IProgress<EngineSetupProgress> progress)
    {
        string job=Job(root);try{Report(progress,"正在解压并检查引擎文件…");string contents=Path.Combine(job,"contents");await Extract(archive,contents,cancellation);Report(progress,"正在配置 GM8.2…");return await Task.Run(()=>Activate(root,contents,cancellation));}finally{RemoveJob(root,job);}
    }
    public static async Task<EngineInspection> DownloadPortable(string root,CancellationToken cancellation,IProgress<EngineSetupProgress> progress)
    {
        string job=Job(root);try{
            Report(progress,"正在获取官网便携包地址…");Uri page;try{page=PortableLink(await ReadPage(new Uri(Website),cancellation));}catch(WebException){cancellation.ThrowIfCancellationRequested();page=new Uri(PortablePage);}
            Uri file=page.Host.EndsWith("mediafire.com",StringComparison.OrdinalIgnoreCase)?DownloadLink(await ReadPage(page,cancellation)):page;
            string archive=Path.Combine(job,"portable.7z");Report(progress,"正在连接下载站…");await Download(file,archive,cancellation,progress);
            Directory.CreateDirectory(Path.Combine(root,"build","logs"));File.AppendAllText(Path.Combine(root,"build","logs","engine-setup.log"),DateTime.UtcNow.ToString("u")+" "+page+" SHA256="+Updater.Hash(archive)+Environment.NewLine);
            Report(progress,"正在解压并检查引擎文件…");string contents=Path.Combine(job,"contents");await Extract(archive,contents,cancellation);Report(progress,"正在配置 GM8.2…");return await Task.Run(()=>Activate(root,contents,cancellation));
        }finally{RemoveJob(root,job);}
    }
    public static async Task<string> DownloadRuntime(string root,bool vc,CancellationToken cancellation,IProgress<EngineSetupProgress> progress)
    {
        string folder=Path.Combine(root,"build","engine-setup","dependencies",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);string exe=Path.Combine(folder,vc?"vc_redist.x86.exe":"dxwebsetup.exe");
        await Download(new Uri(vc?VcUrl:DxUrl),exe,cancellation,progress);if(!IsExecutable(exe))throw new InvalidDataException("下载的运行库安装程序无效，请重试。");return exe;
    }
}
