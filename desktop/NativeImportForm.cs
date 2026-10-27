using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed class NativeImportForm:LocalizedForm
{
    private readonly Func<object,Task<NativeScan>> scan;
    private readonly TextBox folder=new TextBox(),name=new TextBox(),destination=new TextBox(),report=new TextBox();
    private readonly ComboBox entry=new WheelSafeComboBox(),room=new WheelSafeComboBox(),controller=new WheelSafeComboBox(),music=new WheelSafeComboBox();
    private readonly NumericUpDown duration=new WheelSafeNumericUpDown{Minimum=0,Maximum=20000,DecimalPlaces=2,Width=130};
    private readonly Button import=new Button(),rescan=new Button();private bool updating;private int revision;private NativeScan analysis;private string automaticName;
    public NativeImportOptions Options {get;private set;}
    public NativeImportForm(string initial,string parent,Func<object,Task<NativeScan>> scan)
    {
        this.scan=scan;Text="导入非 RufMaker 工程";Font=Theme.Font;BackColor=Theme.Panel;ForeColor=Theme.Text;Size=new Size(850,780);MinimumSize=new Size(650,560);StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;Icon=AppWindow.LoadIcon();
        var table=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=10,Padding=new Padding(12)};table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,110));
        for(int i=0;i<9;i++)table.RowStyles.Add(new RowStyle(SizeType.Absolute,40));table.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        folder.Text=initial;destination.Text=parent;
        var browse=new Button{Text="选择目录",Dock=DockStyle.Fill};browse.Click+=async(s,e)=>{using(var dialog=new FolderBrowserDialog{Description="选择原生 gm82 工程文件夹",SelectedPath=Directory.Exists(folder.Text)?folder.Text:initial})if(Language.ShowDialog(dialog,this)==DialogResult.OK){folder.Text=dialog.SelectedPath;await Analyze(false);}};
        var output=new Button{Text="保存到…",Dock=DockStyle.Fill};output.Click+=(s,e)=>{using(var dialog=new FolderBrowserDialog{Description="选择导入后工程的保存目录",SelectedPath=destination.Text})if(Language.ShowDialog(dialog,this)==DialogResult.OK)destination.Text=dialog.SelectedPath;};
        Control[] controls={folder,entry,room,controller,music,name,destination,duration};string[] labels={"原工程目录","GM82 入口","耐久房间","控制对象","编辑器歌曲","新工程名称","保存父目录","预留时长 / 秒"};
        for(int i=0;i<controls.Length;i++){table.Controls.Add(new Label{Text=labels[i],AutoSize=true,MaximumSize=new Size(210,0),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,i);controls[i].Dock=DockStyle.Fill;controls[i].Margin=new Padding(2,5,6,5);table.Controls.Add(controls[i],1,i);if(i!=0&&i!=6)table.SetColumnSpan(controls[i],2);}
        table.Controls.Add(browse,2,0);table.Controls.Add(output,2,6);rescan.Text="重新检测";rescan.Dock=DockStyle.Fill;rescan.Click+=async(s,e)=>await Analyze(false);table.Controls.Add(rescan,2,8);
        var hint=new Label{Text="导入到新副本；保留原 GML、贴图、背景、绘图和房间。时长 0 表示自动估算。",Dock=DockStyle.Fill,ForeColor=Theme.Muted};table.Controls.Add(hint,0,8);table.SetColumnSpan(hint,2);
        report.Multiline=true;report.ReadOnly=true;report.ScrollBars=ScrollBars.Vertical;report.Dock=DockStyle.Fill;report.BackColor=Theme.Back;report.ForeColor=Theme.Text;report.BorderStyle=BorderStyle.None;Language.WatchText(report);table.Controls.Add(report,0,9);table.SetColumnSpan(report,3);
        foreach(var box in new[]{entry,room,controller,music}){((WheelSafeComboBox)box).LocalizeItems=false;box.DropDownStyle=ComboBoxStyle.DropDownList;box.BackColor=Theme.Raised;box.ForeColor=Theme.Text;box.DropDownWidth=650;}
        entry.SelectionChangeCommitted+=async(s,e)=>await Analyze(true);room.SelectionChangeCommitted+=async(s,e)=>await Analyze(true);controller.SelectionChangeCommitted+=async(s,e)=>await Analyze(true);
        folder.TextChanged+=(s,e)=>{revision++;analysis=null;import.Enabled=false;};
        var foot=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(8)};import.Text="导入副本";import.Enabled=false;import.AutoSize=true;import.Padding=new Padding(14,7,14,7);import.Click+=(s,e)=>{if(analysis==null)return;Options=new NativeImportOptions{entry=analysis.entry,room=analysis.room,controller=analysis.controller,music=music.SelectedIndex>0?Convert.ToString(music.SelectedItem):"",name=name.Text.Trim(),parent=destination.Text.Trim(),duration=(double)duration.Value};if(String.IsNullOrEmpty(Options.name)||String.IsNullOrEmpty(Options.parent)){Language.Message(this,"请填写工程名称和保存目录。","导入工程");return;}DialogResult=DialogResult.OK;};
        var cancel=new Button{Text="取消",AutoSize=true,Padding=new Padding(14,7,14,7),DialogResult=DialogResult.Cancel};foot.Controls.Add(import);foot.Controls.Add(cancel);Controls.Add(table);Controls.Add(foot);CancelButton=cancel;Shown+=async(s,e)=>{if(Directory.Exists(folder.Text))await Analyze(false);};
    }
    private async Task Analyze(bool preserve)
    {
        if(updating)return;int request=++revision;import.Enabled=false;rescan.Enabled=false;report.Text="正在识别房间、控制对象、原生 Timeline 与音乐…";
        try{
            var selectedRoom=room.SelectedItem as NativeRoomCandidate;var result=await scan(new{folder=folder.Text,entry=preserve?Convert.ToString(entry.SelectedItem):"",room=preserve&&selectedRoom!=null?selectedRoom.name:"",controller=preserve&&controller.ContainsFocus?Convert.ToString(controller.SelectedItem):""});if(IsDisposed||request!=revision)return;analysis=result;updating=true;
            entry.Items.Clear();entry.Items.AddRange(result.entries);entry.SelectedItem=result.entry;room.Items.Clear();room.Items.AddRange(result.rooms);room.SelectedItem=result.rooms.FirstOrDefault(r=>r.name==result.room);controller.Items.Clear();controller.Items.AddRange(result.controllers);controller.SelectedItem=result.controller;
            music.Items.Clear();music.Items.Add(Language.Text("（不导入编辑器歌曲，原游戏音乐保留）"));music.Items.AddRange(result.musicChoices);music.SelectedItem=result.music;if(music.SelectedIndex<0)music.SelectedIndex=0;if(String.IsNullOrEmpty(name.Text)||name.Text==automaticName){automaticName=result.name+Language.Text("-导入");name.Text=automaticName;}
            report.Text="检测结果："+result.confidence+Environment.NewLine+"房间："+result.room+"    对象："+result.controller+Environment.NewLine+"计时："+(String.IsNullOrEmpty(result.timeline)?result.clock:result.timeline+"（原生 Timeline）")+"    "+result.fps+" FPS"+Environment.NewLine+"可定位事件："+result.events.Length+"    未换算表达式："+result.unresolved.Length+Environment.NewLine+Environment.NewLine+String.Join(Environment.NewLine+Environment.NewLine,result.warnings);import.Enabled=true;
        }catch(Exception error){if(!IsDisposed&&request==revision){report.Text="识别失败："+error.Message+Environment.NewLine+"建议：检查所选文件夹是否包含文本 .gm82 入口和完整资源；可在 GM8.2 中打开并另存后重试。";Language.Message(this,report.Text,"原生工程识别失败",MessageBoxButtons.OK,MessageBoxIcon.Warning);}}
        finally{updating=false;if(!IsDisposed)rescan.Enabled=true;}
    }
}
internal sealed partial class MainForm
{
    private async Task ImportNativeProject()
    {
        if(busy||bridge==null)return;StopPlaying();string parent=Path.Combine(root,"projects","imported");Directory.CreateDirectory(parent);
        using(var dialog=new NativeImportForm(Path.Combine(root,"non-ruf projects"),parent,p=>bridge.Call<NativeScan>("native-scan",p)))
        {
            if(Language.ShowDialog(dialog,this)!=DialogResult.OK)return;CommitInspectorEdits();if(!await SaveNow(false))return;NativeImportResult result=null;SetBusy(true);Enabled=false;
            try{SetStatus("正在复制原工程并建立 GM 原生轨道…");result=await bridge.Call<NativeImportResult>("native-import",dialog.Options);}catch(Exception error){Error(error);}finally{Enabled=true;SetBusy(false);}
            if(result==null)return;await SwitchProject(result.path);if(result.warnings.Length>0)Language.Message(this,"导入完成，识别到 "+result.events+" 个事件。"+Environment.NewLine+Environment.NewLine+String.Join(Environment.NewLine+Environment.NewLine,result.warnings),"原生工程兼容提示",MessageBoxButtons.OK,MessageBoxIcon.Information);
        }
    }
}
