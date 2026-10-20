using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

[assembly:System.Reflection.AssemblyVersion("1.2.3.0")]
[assembly:System.Reflection.AssemblyFileVersion("1.2.3.0")]
[assembly:System.Reflection.AssemblyInformationalVersion("1.2.3")]
internal static class AppVersion {public const string Current="1.2.3";}
#pragma warning disable 0649
internal sealed class ReleaseAsset {public string name,browser_download_url;}
internal sealed class GitRelease {public string tag_name;public ReleaseAsset[] assets;}
internal sealed class UpdateFile {public string path,sha256;}
internal sealed class UpdateManifest {public string version;public UpdateFile[] files;}
#pragma warning restore 0649
internal sealed class UpdatePlan {public string root,payload,helper;}
internal static class Updater
{
    public const string Repository="https://api.github.com/repos/wymcaT/RufMaker";
    public static WebClient Client()
    {
        ServicePointManager.SecurityProtocol|=(SecurityProtocolType)3072;
        var client=new TimeoutWebClient();client.Headers[HttpRequestHeader.UserAgent]="RufMaker/"+AppVersion.Current;client.Headers[HttpRequestHeader.Accept]="application/vnd.github+json";client.Encoding=Encoding.UTF8;return client;
    }
    private sealed class TimeoutWebClient:WebClient
    {protected override WebRequest GetWebRequest(Uri address){var request=base.GetWebRequest(address);request.Timeout=60000;var http=request as HttpWebRequest;if(http!=null)http.ReadWriteTimeout=60000;return request;}}
    public static Version ParseVersion(string text)
    {Version result;if(!Version.TryParse((text??"").Trim().TrimStart('v','V'),out result))throw new InvalidDataException("发布版本号格式不正确。");return result;}
    public static GitRelease Latest()
    {using(var client=Client()){try{return Json.Read<GitRelease>(client.DownloadString(Repository+"/releases/latest"));}catch(WebException e){var response=e.Response as HttpWebResponse;if(response!=null&&response.StatusCode==HttpStatusCode.NotFound)return null;throw;}}}
    public static string Hash(string file)
    {using(var stream=File.OpenRead(file))using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
    public static bool Allowed(string path)
    {
        if(String.IsNullOrEmpty(path)||path.Contains("\\")||path.StartsWith("/")||path.Split('/').Any(p=>p==".."||p=="."||p.Contains(":")))return false;
        return path=="RufMaker.exe"||path=="README.md"||path=="LICENSE"||path=="icon.ico"||path=="runtime/node.exe"||path=="runtime/fmodex.dll"||
            (path.StartsWith("studio/",StringComparison.Ordinal)&&path.IndexOf('/',7)<0&&(path.EndsWith(".mjs")||path=="studio/waveform.json"))||
            path=="desktop/assets/sprRufBlock.png"||path=="desktop/assets/sprRufPlatform.png"||path.StartsWith("templates/avoidance.gm82/",StringComparison.Ordinal)||path.StartsWith("Ultimate Avoidance Sprite Pack/",StringComparison.Ordinal);
    }
    public static string Inside(string root,string relative)
    {string full=Path.GetFullPath(Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar))),prefix=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;if(!full.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("更新包文件路径越界。");return full;}
    public static UpdateManifest Validate(string payload,string expectedVersion=null)
    {
        var manifest=Json.Read<UpdateManifest>(File.ReadAllText(Path.Combine(payload,"update-manifest.json")));
        if(manifest.files==null||manifest.files.Length==0||manifest.files.Length>20000||expectedVersion!=null&&ParseVersion(manifest.version)!=ParseVersion(expectedVersion))throw new InvalidDataException("更新清单版本不匹配。");
        var unique=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var file in manifest.files){if(!Allowed(file.path)||!unique.Add(file.path))throw new InvalidDataException("更新清单包含无效或重复路径。");if(!String.Equals(Hash(Inside(payload,file.path)),file.sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("更新文件校验失败："+file.path);}
        foreach(string required in new[]{"RufMaker.exe","studio/bridge.mjs","runtime/node.exe","runtime/fmodex.dll","desktop/assets/sprRufBlock.png","desktop/assets/sprRufPlatform.png"})if(!unique.Contains(required))throw new InvalidDataException("更新包缺少 "+required);
        return manifest;
    }
    public static void Extract(string archive,string payload,string expectedVersion)
    {
        Directory.CreateDirectory(payload);
        using(var stream=File.OpenRead(archive))using(var zip=new ZipArchive(stream,ZipArchiveMode.Read))foreach(var entry in zip.Entries){if(entry.Name.Length==0)continue;if(entry.FullName!="update-manifest.json"&&!Allowed(entry.FullName))continue;string file=Inside(payload,entry.FullName);Directory.CreateDirectory(Path.GetDirectoryName(file));using(var input=entry.Open())using(var output=new FileStream(file,FileMode.CreateNew))input.CopyTo(output);}
        Validate(payload,expectedVersion);
    }
    public static void Start(UpdatePlan plan)
    {
        Process.Start(new ProcessStartInfo{FileName=plan.helper,Arguments="--apply-update "+Quote(plan.root)+" "+Quote(plan.payload)+" "+Process.GetCurrentProcess().Id,WorkingDirectory=plan.root,UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});
    }
    private static string Quote(string text){return "\""+text.TrimEnd('\\').Replace("\"","\\\"")+"\"";}
    public static void Apply(string[] args)
    {
        string root=args.Length>1?Path.GetFullPath(args[1]):"",payload=args.Length>2?Path.GetFullPath(args[2]):"";var applied=new List<UpdateFile>();var existed=new HashSet<string>();string backup=Path.Combine(Path.GetDirectoryName(payload),"previous");
        try
        {
            if(args.Length!=4)throw new ArgumentException("更新参数不完整。");
            try{using(var process=Process.GetProcessById(Int32.Parse(args[3])))if(!process.WaitForExit(90000))throw new IOException("编辑器仍在运行，更新已取消。");}catch(ArgumentException){}
            var manifest=Validate(payload);Directory.CreateDirectory(backup);
            foreach(var file in manifest.files){string destination=Inside(root,file.path),old=Inside(backup,file.path);if(File.Exists(destination)){Directory.CreateDirectory(Path.GetDirectoryName(old));File.Copy(destination,old,true);existed.Add(file.path);}applied.Add(file);Directory.CreateDirectory(Path.GetDirectoryName(destination));File.Copy(Inside(payload,file.path),destination,true);}
            Process.Start(new ProcessStartInfo{FileName=Path.Combine(root,"RufMaker.exe"),WorkingDirectory=root,UseShellExecute=true});
        }
        catch(Exception error)
        {
            var rollback=new List<string>();foreach(var file in applied.AsEnumerable().Reverse()){try{string target=Inside(root,file.path);if(existed.Contains(file.path))File.Copy(Inside(backup,file.path),target,true);else if(File.Exists(target))File.Delete(target);}catch(Exception failure){rollback.Add(failure.Message);}}
            MessageBox.Show("更新失败："+error.Message+(rollback.Count==0?"\n已恢复更新前的程序文件。":"\n部分文件恢复失败："+String.Join("; ",rollback))+"\n备份目录："+backup,"RufMaker 更新",MessageBoxButtons.OK,MessageBoxIcon.Error);
        }
    }
}
internal sealed partial class MainForm
{
    private bool checkingUpdate;private UpdatePlan pendingUpdate;
    private async Task CheckUpdates()
    {
        if(checkingUpdate||busy||state==null)return;checkingUpdate=true;
        try
        {
            SetStatus("正在检测 GitHub 更新 · 当前 v"+AppVersion.Current);var release=await Task.Run(()=>Updater.Latest());
            if(release==null){SetStatus("GitHub 暂无正式发布版 · 当前 v"+AppVersion.Current);return;}
            if(Updater.ParseVersion(release.tag_name)<=Updater.ParseVersion(AppVersion.Current)){SetStatus("当前 v"+AppVersion.Current+" 已是最新版本。");return;}
            string version=Updater.ParseVersion(release.tag_name).ToString();var asset=release.assets.FirstOrDefault(a=>a.name=="RufMaker-"+version+".zip");var checksum=release.assets.FirstOrDefault(a=>a.name=="RufMaker-"+version+".zip.sha256");
            if(asset==null||checksum==null)throw new InvalidDataException("此 Release 尚未提供完整更新包。");
            string stage=Path.Combine(root,"build","updates",version+"-"+Guid.NewGuid().ToString("N").Substring(0,8));Directory.CreateDirectory(stage);string archive=Path.Combine(stage,asset.name),payload=Path.Combine(stage,"payload");
            using(var client=Updater.Client()){
                client.DownloadProgressChanged+=(s,e)=>SetStatus("正在下载 v"+version+" · "+e.ProgressPercentage+"%");await client.DownloadFileTaskAsync(new Uri(asset.browser_download_url),archive);
                string expected=(await client.DownloadStringTaskAsync(new Uri(checksum.browser_download_url))).Trim().Split(new[]{' ','\t','\r','\n'},StringSplitOptions.RemoveEmptyEntries)[0];if(!String.Equals(await Task.Run(()=>Updater.Hash(archive)),expected,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("更新包 SHA256 校验失败，请重新检测更新。");
            }
            SetStatus("正在校验更新文件…");await Task.Run(()=>Updater.Extract(archive,payload,version));
            StopPlaying();properties.CommitPending();saveTimer.Stop();SaveWorkspaceView();if(!await SaveNow(false))return;
            string helper=Path.Combine(stage,"RufMaker.Update.exe");File.Copy(Application.ExecutablePath,helper,true);pendingUpdate=new UpdatePlan{root=root,payload=payload,helper=helper};SetStatus("工程已保存，正在安装更新并重启…");Close();
        }
        catch(Exception error){Error(new Exception("检测更新失败："+error.Message,error));}
        finally{checkingUpdate=false;}
    }
}
