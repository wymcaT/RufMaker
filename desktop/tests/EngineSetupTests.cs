using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class EngineSetupTests
{
    private static void Check(bool value,string text){if(!value)throw new Exception(text);}
    private static void FakeEngine(string folder)
    {Directory.CreateDirectory(folder);var exe=new byte[2048];exe[0]=(byte)'M';exe[1]=(byte)'Z';File.WriteAllBytes(Path.Combine(folder,"GameMaker.exe"),exe);foreach(string name in EngineSetup.Required){string file=Path.Combine(folder,name);Directory.CreateDirectory(Path.GetDirectoryName(file));File.WriteAllText(file,"test engine dependency");}}
    private static void Package(string engine,string zipFile)
    {using(var stream=File.Create(zipFile))using(var zip=new ZipArchive(stream,ZipArchiveMode.Create))foreach(string file in Directory.GetFiles(engine,"*",SearchOption.AllDirectories)){var entry=zip.CreateEntry("portable/"+file.Substring(engine.Length+1).Replace('\\','/'));using(var source=File.OpenRead(file))using(var target=entry.Open())source.CopyTo(target);}}
    private static async Task Offline(string folder)
    {
        string root=Path.Combine(folder,"offline"),existing=Path.Combine(folder,"existing"),archive=Path.Combine(folder,"engine.zip");Directory.CreateDirectory(root);FakeEngine(existing);Package(existing,archive);
        Check(!EngineSetup.Inspect(Path.Combine(folder,"missing")).Ready,"Missing engine was marked ready");Language.Select(root,"en");EngineSetup.Use(root,existing);Check(EngineSetup.Find(root).Exe==Path.Combine(existing,"GameMaker.exe"),"Chosen engine was not persisted");Language.Initialize(root);Check(Language.English,"Saving engine selection erased the language preference");
        EngineSetup.SkipStartup(root);Check(!EngineSetup.OfferAtStartup(root,EngineSetup.Inspect(Path.Combine(folder,"missing"))),"Startup skip was not remembered");
        var found=await EngineSetup.InstallArchive(root,archive,CancellationToken.None,null);Check(found.Ready&&found.Exe==Path.Combine(root,"GameMaker8.2","GameMaker.exe"),"Local archive was not configured");
        string extra=Path.Combine(root,"GameMaker8.2","extensions","My Custom Extension.ged");File.WriteAllText(extra,"keep this plugin");await EngineSetup.InstallArchive(root,archive,CancellationToken.None,null);Check(File.ReadAllText(extra)=="keep this plugin"&&Directory.GetDirectories(Path.Combine(root,"build","engine-setup"),"previous-*").Length==1,"Repair lost extensions or backup");
        string before=Updater.Hash(found.Exe),invalid=Path.Combine(folder,"invalid.zip");using(var stream=File.Create(invalid))using(var zip=new ZipArchive(stream,ZipArchiveMode.Create))zip.CreateEntry("readme.txt");bool rejected=false;
        try{await EngineSetup.InstallArchive(root,invalid,CancellationToken.None,null);}catch(InvalidDataException){rejected=true;}Check(rejected&&Updater.Hash(found.Exe)==before,"Invalid archive changed the working engine");
        var cancelled=new CancellationTokenSource();cancelled.Cancel();bool stopped=false;try{await EngineSetup.InstallArchive(root,archive,cancelled.Token,null);}catch(OperationCanceledException){stopped=true;}Check(stopped&&Updater.Hash(found.Exe)==before&&File.Exists(archive),"Cancellation altered the working engine or user archive");
        string traversal=Path.Combine(folder,"traversal.zip");using(var stream=File.Create(traversal))using(var zip=new ZipArchive(stream,ZipArchiveMode.Create))using(var writer=new StreamWriter(zip.CreateEntry("../escaped.txt").Open()))writer.Write("bad");rejected=false;
        try{await EngineSetup.InstallArchive(root,traversal,CancellationToken.None,null);}catch(InvalidDataException){rejected=true;}Check(rejected&&!File.Exists(Path.Combine(root,"build","engine-setup","escaped.txt")),"Archive traversal was accepted");
        Check(Directory.GetDirectories(Path.Combine(root,"build","engine-setup"),"job-*").Length==0,"Temporary setup jobs were not cleaned");
        Check(EngineSetup.DownloadLink("<a id='downloadButton' href='https://download1.mediafire.com/file.7z?x=1&amp;y=2'>").Query=="?x=1&y=2","Download link parser lost HTML entities");rejected=false;try{EngineSetup.DownloadLink("<a id='downloadButton' href='https://example.com/ad.exe'>");}catch(InvalidDataException){rejected=true;}Check(rejected,"Unrelated download host was accepted");
        Console.WriteLine("PASS: detection, chosen paths, ZIP setup, plugin preservation, backup, invalid packages, cancellation, cleanup and download link parsing.");
    }
    private sealed class Stages:IProgress<EngineSetupProgress>
    {private string last;public void Report(EngineSetupProgress p){string stage=p.Text.StartsWith("正在下载")?"下载中":p.Text;if(stage==last)return;last=stage;Console.WriteLine("SETUP: "+stage);}}
    private static async Task Online(string folder)
    {
        string root=Path.Combine(folder,"online");Directory.CreateDirectory(root);using(var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(3))){var found=await EngineSetup.DownloadPortable(root,timeout.Token,new Stages());Check(found.Ready&&File.Exists(Path.Combine(root,"GameMaker8.2","extensions","Game Maker 8.2 Core.dat")),"Official portable download did not produce a complete engine");}
        Console.WriteLine("PASS: live website -> MediaFire -> 7z download -> Windows extraction -> complete GM8.2 setup in an isolated folder; no GameMaker process started.");
    }
    private static IEnumerable<Control> All(Control c){yield return c;foreach(Control child in c.Controls)foreach(Control item in All(child))yield return item;}
    private static void Interface(string folder)
    {
        string root=Path.Combine(folder,"offline");Language.Initialize(root);using(var form=new EngineSetupForm(root)){
            Language.Attach(form);Check(All(form).OfType<Button>().Any(b=>b.Text=="Download and set up"),"English setup labels are missing");var text=All(form).OfType<TextBox>().Single();Check(text.Text.Contains("Required GM8.2 files are present")&&text.Text.Contains(Path.Combine(root,"GameMaker8.2")),"Engine status or path was not shown");
            var children=form.Controls.Cast<Control>().ToArray();form.Controls.Clear();
            using(var surface=new Panel{Size=form.ClientSize,Font=form.Font,BackColor=form.BackColor,ForeColor=form.ForeColor}){surface.Controls.AddRange(children);surface.PerformLayout();Check(All(surface).OfType<Label>().Where(l=>l.Text.Length>100).All(l=>l.Width<surface.Width&&l.Height>=l.Font.Height*2),"Long setup explanations do not wrap");var bar=All(surface).OfType<ProgressBar>().Single();Check(bar.Width>300&&bar.Height>=22,"Setup progress bar has no usable area");using(var bitmap=new Bitmap(surface.Width,surface.Height)){surface.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(Path.GetDirectoryName(folder),"engine-setup-en.png"),System.Drawing.Imaging.ImageFormat.Png);}surface.Controls.Clear();}form.Controls.AddRange(children);
            Language.Select(root,"zh-CN");Language.Apply(form);Check(All(form).OfType<Button>().Any(b=>b.Text=="一键下载并配置"),"Chinese setup labels are missing");
            foreach(float scale in new[]{1f,1.5f,2f}){form.Font=new Font(Theme.Font.FontFamily,Theme.Font.Size*scale);form.PerformLayout();foreach(Button button in All(form).OfType<Button>())Check(button.AutoSize,"Setup action cannot grow with translated text");}
        }
        Console.WriteLine("PASS: bilingual setup window, status details, unchanged paths and scalable action controls (no application window).");
    }
    [STAThread] public static int Main(string[] args)
    {try{SynchronizationContext.SetSynchronizationContext(null);Offline(args[0]).GetAwaiter().GetResult();if(args.Contains("--online"))Online(args[0]).GetAwaiter().GetResult();Interface(args[0]);return 0;}catch(Exception error){Console.Error.WriteLine(error);return 1;}}
}
