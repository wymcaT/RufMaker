using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

internal static class UpdaterTests
{
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static void Notices(string root)
    {
        var release=new GitRelease{tag_name="v"+AppVersion.Current,assets=new[]{new ReleaseAsset{name="RufMaker-"+AppVersion.Current+".zip"},new ReleaseAsset{name="RufMaker-"+AppVersion.Current+".zip.sha256"},new ReleaseAsset{name="RufMaker-"+AppVersion.Current+"-Windows-Portable.zip"}}};
        var current=Updater.Notice(release,AppVersion.Current);Check(!current.Available&&current.Message.Contains("已是最新版本"),"Current version does not show a latest-version notice");
        var available=Updater.Notice(release,"1.7.0");Check(available.Available&&available.Message.Contains("本机版本：v1.7.0")&&available.Message.Contains("GitHub 最新版本：v"+AppVersion.Current)&&available.Message.Contains("是否现在下载并更新"),"Update confirmation lacks both versions or the question");
        Check(!Updater.Notice(release,"99.0.0").Available,"Updater offers a downgrade");Check(Updater.Notice(null,AppVersion.Current).Message.Contains("暂无正式发布版"),"Missing releases are reported as current");
        var assets=Updater.UpdateAssets(release,AppVersion.Current);Check(assets.Length==2&&assets[0].name=="RufMaker-"+AppVersion.Current+".zip","Updater chose the first-install ZIP");
        Language.Select(root,"en");Check(Language.Text(current.Message).Contains("up to date")&&Language.Text(available.Message).Contains("Installed version: v1.7.0"),"Update notices are not translated");
        Console.WriteLine("PASS: latest-version popup text, both-version confirmation, no downgrade, release absence, update asset selection and English notices.");
    }
    private static void Restart(string scratch,string stub)
    {
        string root=Path.Combine(scratch,"installed"),payload=Path.Combine(scratch,"payload"),helper=Path.Combine(scratch,"RufMaker.Update.exe");Directory.CreateDirectory(root);Directory.CreateDirectory(payload);
        var files=new Dictionary<string,string>{{"RufMaker.exe",null},{"README.md","new guide"},{"studio/bridge.mjs","new service"},{"runtime/node.exe","new runtime"},{"runtime/fmodex.dll","new audio"},{"desktop/assets/sprRufBlock.png","new block"},{"desktop/assets/sprRufPlatform.png","new platform"}};
        foreach(var p in files){string file=Updater.Inside(payload,p.Key);Directory.CreateDirectory(Path.GetDirectoryName(file));if(p.Value==null)File.Copy(stub,file);else File.WriteAllText(file,p.Value);}
        File.WriteAllText(Path.Combine(payload,"update-manifest.json"),Json.Write(new UpdateManifest{version=AppVersion.Current,files=files.Keys.Select(name=>new UpdateFile{path=name,sha256=Updater.Hash(Updater.Inside(payload,name))}).ToArray()}));
        File.WriteAllText(Path.Combine(root,"RufMaker.exe"),"old executable");Directory.CreateDirectory(Path.Combine(root,"studio"));File.WriteAllText(Path.Combine(root,"studio","bridge.mjs"),"old service");
        var userFiles=new[]{"projects/game/song.ogg","projects/game/cache/native-film/base.rufvideo","user-data/settings.json","user-presets/keep.rufpreset",".rufmaker-view.json","GameMaker8.2/keep.txt"};
        foreach(string relative in userFiles){string file=Updater.Inside(root,relative);Directory.CreateDirectory(Path.GetDirectoryName(file));File.WriteAllText(file,"keep "+relative);}
        File.Copy(Application.ExecutablePath,helper);
        using(var parent=Process.Start(new ProcessStartInfo(helper,"--start-update \""+root+"\" \""+payload+"\""){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden})){Check(parent.WaitForExit(10000),"Update parent did not exit");Check(parent.ExitCode==0,"Update parent failed");}
        string marker=Path.Combine(root,"restart-confirmed.txt");var watch=Stopwatch.StartNew();while(!File.Exists(marker)&&watch.ElapsedMilliseconds<15000)Thread.Sleep(50);
        Check(File.Exists(marker),"Updater did not restart the isolated application");Check(File.ReadAllText(Path.Combine(root,"studio","bridge.mjs"))=="new service","Service was not updated");
        foreach(string relative in userFiles)Check(File.ReadAllText(Updater.Inside(root,relative))=="keep "+relative,"Updater changed user data: "+relative);
        Check(File.ReadAllText(Path.Combine(scratch,"previous","RufMaker.exe"))=="old executable","Original program backup is missing");
        Console.WriteLine("PASS: actual update helper waits for parent exit, replaces app files, keeps project/music/cache/settings/presets/GM installation, backs up and restarts an invisible test app.");
    }
    private static void Online(string scratch,string expected)
    {
        var release=Updater.Latest();Check(release!=null,"No public release");var notice=Updater.Notice(release,expected);Check(notice.Latest==expected&&!notice.Available,"Public latest version mismatch: "+(release==null?"none":release.tag_name));
        var assets=Updater.UpdateAssets(release,expected);string zip=Path.Combine(scratch,assets[0].name),payload=Path.Combine(scratch,"downloaded-update");
        using(var client=Updater.Client()){client.DownloadFile(assets[0].browser_download_url,zip);string hash=client.DownloadString(assets[1].browser_download_url).Split(new[]{' ','\t','\r','\n'},StringSplitOptions.RemoveEmptyEntries)[0];Check(Updater.Hash(zip)==hash.Trim().ToLowerInvariant(),"Published update ZIP checksum mismatch");}
        Updater.Extract(zip,payload,expected);Check(File.Exists(Path.Combine(payload,"RufMaker.exe")),"Published update has no executable");
        Console.WriteLine("PASS: live GitHub latest API reports v"+notice.Latest+"; app updater downloads the public ZIP, verifies SHA256 and extracts every manifest file.");
        var portable=release.assets.FirstOrDefault(a=>a.name=="RufMaker-"+expected+"-Windows-Portable.zip");Check(portable!=null,"Published release has no full Windows app");Console.WriteLine("Full app: "+portable.browser_download_url);
    }
    [STAThread] public static int Main(string[] args)
    {
        try{
            if(args[0]=="--apply-update"){Updater.Apply(args);return 0;}
            if(args[0]=="--start-update"){Updater.Start(new UpdatePlan{root=args[1],payload=args[2],helper=Application.ExecutablePath});return 0;}
            if(args[0]=="--online"){Online(args[1],args[2]);return 0;}
            Notices(args[0]);Restart(args[0],args[1]);return 0;
        }catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
