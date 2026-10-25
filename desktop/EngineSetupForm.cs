using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed class EngineSetupForm:LocalizedForm
{
    private readonly string root;private CancellationTokenSource cancellation;private bool working,closeAfterCancel;
    private readonly Label heading=new Label(),intro=new Label(),message=new Label(),source=new Label();
    private readonly TextBox details=new TextBox();private readonly ProgressBar progress=new ProgressBar();
    private readonly Button download=new Button(),choose=new Button(),import=new Button(),rescan=new Button(),vc=new Button(),dx=new Button(),finish=new Button(),skip=new Button(),cancel=new Button();
    private readonly ComboBox language=new WheelSafeComboBox();
    public EngineInspection Result;
    public EngineSetupForm(string root)
    {
        this.root=root;Text="准备 GameMaker 8.2";Icon=AppWindow.LoadIcon();ShowInTaskbar=false;StartPosition=FormStartPosition.CenterParent;Font=Theme.Font;BackColor=Theme.Back;ForeColor=Theme.Text;Size=new Size(820,740);MinimumSize=new Size(700,630);
        var body=new Panel{Dock=DockStyle.Fill,AutoScroll=true};var table=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,RowCount=6,Padding=new Padding(16,16,16,0)};
        for(int i=0;i<6;i++)table.RowStyles.Add(new RowStyle(i==2?SizeType.Absolute:SizeType.AutoSize,i==2?180:0));
        var top=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true};heading.Text="GM8.2 引擎准备";heading.AutoSize=true;heading.Font=new Font(Theme.Font.FontFamily,14,FontStyle.Bold);heading.ForeColor=Theme.Accent;heading.Margin=new Padding(0,4,30,8);top.Controls.Add(heading);
        top.Controls.Add(new Label{Text="切换语言",AutoSize=true,Margin=new Padding(0,9,6,0)});language.DropDownStyle=ComboBoxStyle.DropDownList;language.Width=125;language.BackColor=Theme.Raised;language.ForeColor=Theme.Text;language.Items.AddRange(new object[]{"简体中文","English"});language.SelectedIndex=Language.English?1:0;top.Controls.Add(language);table.Controls.Add(top,0,0);
        intro.Text="F5 试玩、导出游戏和生成原生底片需要 GM8.2。可以一键准备，也可以选择已有安装；暂时跳过仍可编辑工程。";intro.AutoSize=true;intro.Dock=DockStyle.Fill;intro.Margin=new Padding(0,4,0,14);table.Controls.Add(intro,0,1);
        details.Multiline=true;details.ReadOnly=true;details.ScrollBars=ScrollBars.Vertical;details.WordWrap=true;details.Dock=DockStyle.Fill;details.MinimumSize=new Size(0,180);details.BackColor=Theme.Panel;details.ForeColor=Theme.Text;details.BorderStyle=BorderStyle.FixedSingle;details.Margin=new Padding(0,0,0,10);Language.WatchText(details);table.Controls.Add(details,0,2);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true,Margin=Padding.Empty};SetupButton(download,"一键下载并配置",true);SetupButton(choose,"选择已有目录");SetupButton(import,"导入 ZIP / 7z");SetupButton(rescan,"重新检测");foreach(var button in new[]{download,choose,import,rescan})actions.Controls.Add(button);table.Controls.Add(actions,0,3);
        var runtimes=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true,Margin=Padding.Empty};SetupButton(vc,"安装 VC++ x86 运行库");SetupButton(dx,"安装 DirectX9 组件");runtimes.Controls.Add(vc);runtimes.Controls.Add(dx);table.Controls.Add(runtimes,0,4);
        source.Text="GM8.2 从项目官网的 Portable 下载入口获取，运行库从微软下载。准备好的引擎保存在 RufMaker 的 GameMaker8.2 文件夹。外部耐久所需的额外扩展仍需自行安装。";source.AutoSize=true;source.Dock=DockStyle.Fill;source.ForeColor=Theme.Muted;source.Margin=new Padding(0,8,0,10);table.Controls.Add(source,0,5);
        var lower=new TableLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true,ColumnCount=1,RowCount=2,Padding=new Padding(16,4,16,16)};
        var status=new TableLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,ColumnCount=1,RowCount=2,Margin=Padding.Empty};status.RowStyles.Add(new RowStyle(SizeType.AutoSize));status.RowStyles.Add(new RowStyle(SizeType.Absolute,32));message.Text="请选择准备方式。";message.AutoSize=true;message.Dock=DockStyle.Fill;message.Margin=new Padding(0,4,0,8);progress.Dock=DockStyle.Fill;progress.MinimumSize=new Size(0,22);progress.Margin=new Padding(0,0,0,10);status.Controls.Add(message,0,0);status.Controls.Add(progress,0,1);lower.Controls.Add(status,0,0);
        var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,FlowDirection=FlowDirection.RightToLeft,WrapContents=true,Margin=Padding.Empty};SetupButton(finish,"完成配置",true);SetupButton(skip,"暂不配置，继续编辑");SetupButton(cancel,"取消下载 / 解压");cancel.Enabled=false;footer.Controls.Add(finish);footer.Controls.Add(skip);footer.Controls.Add(cancel);lower.Controls.Add(footer,0,1);body.Controls.Add(table);Controls.Add(body);Controls.Add(lower);
        Action wrap=()=>{int width=Math.Max(200,body.ClientSize.Width-SystemInformation.VerticalScrollBarWidth);table.MaximumSize=new Size(width,0);intro.MaximumSize=source.MaximumSize=new Size(Math.Max(150,width-table.Padding.Horizontal),0);message.MaximumSize=new Size(Math.Max(150,ClientSize.Width-lower.Padding.Horizontal),0);};body.SizeChanged+=(s,e)=>wrap();ClientSizeChanged+=(s,e)=>wrap();wrap();
        download.Click+=async(s,e)=>await Work(async(token,p)=>{Result=await EngineSetup.DownloadPortable(root,token,p);},"引擎准备完成，可以 F5 试玩和导出游戏。");
        choose.Click+=async(s,e)=>{using(var dialog=new FolderBrowserDialog{Description="选择包含 GameMaker.exe 的 GM8.2 完整目录",SelectedPath=Result!=null&&Result.Ready?Path.GetDirectoryName(Result.Exe):root})if(Language.ShowDialog(dialog,this)==DialogResult.OK)await Work((token,p)=>{token.ThrowIfCancellationRequested();Result=EngineSetup.Use(root,dialog.SelectedPath);return Task.FromResult(0);},"已保存引擎位置，可以 F5 试玩和导出游戏。");};
        import.Click+=async(s,e)=>{using(var dialog=new OpenFileDialog{Title="导入 GM8.2 便携压缩包",Filter="GM8.2 便携包|*.7z;*.zip"})if(Language.ShowDialog(dialog,this)==DialogResult.OK)await Work(async(token,p)=>{Result=await EngineSetup.InstallArchive(root,dialog.FileName,token,p);},"引擎准备完成，可以 F5 试玩和导出游戏。");};
        vc.Click+=async(s,e)=>await InstallRuntime(true);dx.Click+=async(s,e)=>await InstallRuntime(false);rescan.Click+=(s,e)=>RefreshStatus();
        finish.Click+=(s,e)=>{if(Result!=null&&Result.Ready){Result=EngineSetup.Use(root,Result.Exe);DialogResult=DialogResult.OK;}};
        skip.Click+=(s,e)=>{EngineSetup.SkipStartup(root);DialogResult=DialogResult.Ignore;};cancel.Click+=(s,e)=>{if(cancellation!=null)cancellation.Cancel();};AcceptButton=finish;CancelButton=skip;
        language.SelectionChangeCommitted+=(s,e)=>{Language.Select(root,language.SelectedIndex==1?"en":"zh-CN");var main=Owner as MainForm;if(main!=null)main.RefreshLanguageUI();Language.Apply(this);RefreshStatus();};
        FormClosing+=(s,e)=>{if(working){e.Cancel=true;closeAfterCancel=true;if(cancellation!=null)cancellation.Cancel();}};RefreshStatus();
    }
    private static void SetupButton(Button button,string text,bool primary=false)
    {button.Text=text;button.AutoSize=true;button.AutoSizeMode=AutoSizeMode.GrowAndShrink;button.Padding=new Padding(9,5,9,5);button.MinimumSize=new Size(0,34);button.Margin=new Padding(0,3,8,3);button.FlatStyle=FlatStyle.Flat;button.FlatAppearance.BorderSize=0;button.BackColor=primary?Theme.Accent:Theme.Raised;button.ForeColor=primary?Theme.Back:Theme.Text;button.UseVisualStyleBackColor=false;}
    private void RefreshStatus()
    {
        Result=EngineSetup.Find(root);string folder=String.IsNullOrEmpty(Result.Exe)?"":Path.GetDirectoryName(Result.Exe);string[] missing=EngineSetup.MissingRuntimes(folder);
        details.Text=(Result.Ready?"✓ GM8.2 必需文件齐全":"尚未找到完整的 GM8.2")+"\r\n"+Result.Exe+"\r\n\r\n"+(Result.Ready?"✓ Core / Sound / DirectX9 扩展齐全":"缺少文件：\r\n"+String.Join("\r\n",Result.Missing))+"\r\n\r\n"+(missing.Length==0?"✓ VC++ x86 / DirectX9 运行库已检测到":"未检测到系统运行库："+String.Join(", ",missing)+"\r\n如遇缺少 DLL 的报错，可使用下方按钮安装运行库。安装后点击重新检测。");
        if(!working){finish.Enabled=Result.Ready;vc.Enabled=Array.Exists(missing,n=>n!="d3dx9_43.dll");dx.Enabled=Array.Exists(missing,n=>n=="d3dx9_43.dll");}
    }
    private void Busy(bool value)
    {working=value;foreach(var button in new[]{download,choose,import,rescan,vc,dx,finish,skip})button.Enabled=!value;language.Enabled=!value;cancel.Enabled=value;CancelButton=value?cancel:skip;UseWaitCursor=value;}
    private async Task Work(Func<CancellationToken,IProgress<EngineSetupProgress>,Task> action,string success)
    {
        if(working)return;cancellation=new CancellationTokenSource();Busy(true);message.ForeColor=Theme.Text;progress.Style=ProgressBarStyle.Marquee;
        try{var updates=new Progress<EngineSetupProgress>(p=>{if(IsDisposed||!working)return;message.Text=p.Text;progress.Style=p.Percent<0?ProgressBarStyle.Marquee:ProgressBarStyle.Continuous;if(p.Percent>=0)progress.Value=Math.Max(0,Math.Min(100,p.Percent));});await action(cancellation.Token,updates);message.Text=success;progress.Style=ProgressBarStyle.Continuous;progress.Value=100;}
        catch(OperationCanceledException){message.Text="准备已取消，可以重新下载或选择本地文件。";progress.Style=ProgressBarStyle.Continuous;progress.Value=0;}
        catch(Exception error){message.ForeColor=Theme.Accent;message.Text="准备失败："+error.Message+"\r\n可以重试，或选择已有目录 / 导入本地压缩包。";progress.Style=ProgressBarStyle.Continuous;progress.Value=0;string log=Path.Combine(root,"build","logs","engine-setup.log");Directory.CreateDirectory(Path.GetDirectoryName(log));File.AppendAllText(log,DateTime.UtcNow.ToString("u")+" "+error+Environment.NewLine);}
        finally{cancellation.Dispose();cancellation=null;Busy(false);RefreshStatus();if(closeAfterCancel){DialogResult=DialogResult.Cancel;Close();}}
    }
    private async Task InstallRuntime(bool vcRuntime)
    {await Work(async(token,p)=>{string file=await EngineSetup.DownloadRuntime(root,vcRuntime,token,p);Process.Start(new ProcessStartInfo(file){UseShellExecute=true});},"运行库安装窗口已打开，完成安装后请点击重新检测。");}
}

internal sealed partial class MainForm
{
    internal void RefreshLanguageUI()
    {if(languageChoice.Items.Count>0)languageChoice.SelectedIndex=Language.English?1:0;Language.Apply(this);if(document!=null){FillResources();ShowProperties();UpdateTitle();}RefreshStatisticsLanguage();RefreshShell();SyncResourceTree();stage.Invalidate();timeline.Invalidate();}
    private void OfferEngineAtStartup()
    {var found=EngineSetup.Find(root);if(EngineSetup.OfferAtStartup(root,found))using(var dialog=new EngineSetupForm(root))Language.ShowDialog(dialog,this);}
    private async Task<bool> EnsureEngine()
    {
        if(EngineSetup.Find(root).Ready)return true;await ConfigureEngine();if(EngineSetup.Find(root).Ready)return true;SetStatus("尚未配置 GM8.2；可继续编辑，使用顶栏“引擎设置”完成配置后再试玩或导出。");return false;
    }
    private async Task ConfigureEngine()
    {
        if(busy)return;StopPlaying();properties.CommitPending();if(bridge!=null&&!await SaveNow(false))return;
        using(var dialog=new EngineSetupForm(root))Language.ShowDialog(dialog,this);
        var found=EngineSetup.Find(root);if(!found.Ready||bridge==null||String.Equals(bridge.GameMakerPath,found.Exe,StringComparison.OrdinalIgnoreCase))return;NativeBridge candidate=null;SetBusy(true);
        try{candidate=new NativeBridge(root,currentProjectPath);await candidate.Call<LoadData>("load",new{});var previous=bridge;bridge=candidate;candidate=null;previous.Dispose();SetStatus("引擎设置已生效，可以继续试玩和导出。");}
        catch(Exception error){Error(error);}finally{if(candidate!=null)candidate.Dispose();SetBusy(false);}
    }
}
