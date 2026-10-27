using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed class PresetEntry {public string name,key;public bool folder;}
internal static class PresetIcons
{
    public static ImageList Create()
    {
        var icons=new ImageList{ImageSize=new Size(24,24),ColorDepth=ColorDepth.Depth32Bit};
        // Materialize the native list first, so Add copies each bitmap before it is disposed.
        IntPtr handle=icons.Handle;
        foreach(string glyph in new[]{"↗","▸","◆"})using(var icon=new Bitmap(24,24)){
            using(var g=Graphics.FromImage(icon))Theme.Label(g,glyph,new Rectangle(0,0,24,24),Theme.Accent,Theme.Bold,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
            icons.Images.Add(icon);
        }
        return icons;
    }
}
internal sealed class PresetNameForm:LocalizedForm
{
    public string ResultName,Category;
    public PresetNameForm(string title,string name,string category,string[] folders,string hint)
    {
        Text=title;Size=new Size(520,255);MinimumSize=Size;MaximumSize=Size;StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;Font=Theme.Font;BackColor=Theme.Panel;ForeColor=Theme.Text;Icon=AppWindow.LoadIcon();ShowInTaskbar=false;
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=4,Padding=new Padding(14)};layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        var text=new TextBox{Text=name,Dock=DockStyle.Fill,BackColor=Theme.Raised,ForeColor=Theme.Text};var combo=new WheelSafeComboBox{LocalizeItems=false,DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill,BackColor=Theme.Raised,ForeColor=Theme.Text};foreach(string folder in folders)combo.Items.Add(folder==""?Language.Text("（根目录）"):folder);combo.SelectedIndex=Math.Max(0,Array.IndexOf(folders,category));
        layout.Controls.Add(new Label{Text="名称",AutoSize=true,Margin=new Padding(0,6,14,0)},0,0);layout.Controls.Add(text,1,0);layout.Controls.Add(new Label{Text="分类文件夹",AutoSize=true,Margin=new Padding(0,6,14,0)},0,1);layout.Controls.Add(combo,1,1);
        var help=new Label{Text=hint,Dock=DockStyle.Fill,ForeColor=Theme.Muted,Padding=new Padding(0,8,0,0)};layout.Controls.Add(help,0,2);layout.SetColumnSpan(help,2);layout.RowStyles.Add(new RowStyle(SizeType.Absolute,35));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,35));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,38));
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var ok=new Button{Text="确定",Width=90,Height=30};var cancel=new Button{Text="取消",Width=90,Height=30,DialogResult=DialogResult.Cancel};ok.Click+=(s,e)=>{try{ResultName=PresetLibrary.ValidName(text.Text);Category=folders[combo.SelectedIndex];DialogResult=DialogResult.OK;}catch(Exception error){Language.Message(this,error.Message,"预设名称");}};buttons.Controls.Add(ok);buttons.Controls.Add(cancel);layout.Controls.Add(buttons,0,3);layout.SetColumnSpan(buttons,2);Controls.Add(layout);AcceptButton=ok;CancelButton=cancel;Shown+=(s,e)=>{text.SelectAll();text.Focus();};
    }
}

internal sealed partial class MainForm
{
    private PresetLibrary presetLibrary;private string presetFolder="";private FlowLayoutPanel presetTools;private Label presetAddress;
    private PresetLibrary Library {get{return presetLibrary??(presetLibrary=new PresetLibrary(Path.Combine(root,"user-presets")));}}
    private static bool IsPresetKind(string kind){return kind=="presets"||kind=="effect-presets"||kind=="camera-presets"||kind=="custom-presets"||kind=="generation-presets"||kind=="event-presets";}
    private FlowLayoutPanel BuildPresetTools()
    {
        presetTools=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true,Margin=Padding.Empty,Visible=false};
        presetAddress=new Label{Text=Language.Text("公共预设库"),AutoSize=true,MaximumSize=new Size(250,0),ForeColor=Theme.Muted,Margin=new Padding(8,4,8,4)};Language.PreserveText(presetAddress);presetTools.Controls.Add(presetAddress);presetTools.SetFlowBreak(presetAddress,true);
        presetTools.Resize+=(s,e)=>presetAddress.MaximumSize=new Size(Math.Max(60,presetTools.ClientSize.Width-20),0);
        presetTools.Controls.Add(Button("上级",()=>{presetFolder=Path.GetDirectoryName(presetFolder)??"";FillResources();},false,48));
        return presetTools;
    }
    private PresetEntry SelectedPresetEntry(){return resources.SelectedItems.Count==0?null:resources.SelectedItems[0].Tag as PresetEntry;}
    private string PresetDragText()
    {var entry=SelectedPresetEntry();return entry==null||entry.folder?null:resourceKind+"|"+entry.key;}
    private void FillPresetResources()
    {
        bool custom=resourceKind=="custom-presets";presetTools.Visible=custom;
        var entries=new List<PresetEntry>();
        if(custom){if(!Directory.Exists(Library.Resolve(presetFolder)))presetFolder="";string folder=Library.Resolve(presetFolder);presetAddress.Text=Language.Text("公共预设库 / ")+presetFolder.Replace('\\','/');
            entries.AddRange(Directory.GetDirectories(folder).OrderBy(s=>s).Select(s=>new PresetEntry{name=Path.GetFileName(s),key=Library.Relative(s),folder=true}));
            entries.AddRange(Directory.GetFiles(folder,"*.rufpreset").OrderBy(s=>s).Select(s=>new PresetEntry{name=Path.GetFileNameWithoutExtension(s),key=Library.Relative(s)}));
        }else if(resourceKind=="generation-presets"){for(int i=0;i<GenerationPresets.Names.Length;i++)entries.Add(new PresetEntry{name=GenerationPresets.Names[i],key=GenerationPresets.Keys[i]});}
        else if(resourceKind=="event-presets"){for(int i=0;i<EventPresets.Keys.Length;i++)entries.Add(new PresetEntry{name=EventPresets.Names[i],key=EventPresets.Keys[i]});}
        else foreach(string name in resourceKind=="presets"?MotionPresets.Names:resourceKind=="camera-presets"?CameraPresets.Names:EffectPresets.Names)entries.Add(new PresetEntry{name=name,key=name});
        var icons=PresetIcons.Create();resources.BeginUpdate();resources.Items.Clear();var previous=resources.SmallImageList;
        foreach(var entry in entries.Where(e=>e.name.IndexOf(ResourceFilter,StringComparison.OrdinalIgnoreCase)>=0||!custom&&Language.Text(e.name).IndexOf(ResourceFilter,StringComparison.OrdinalIgnoreCase)>=0))resources.Items.Add(new ListViewItem(custom?entry.name:Language.Text(entry.name),entry.folder?1:custom?2:0){Tag=entry,ToolTipText=Language.Text(entry.folder?"双击进入分类；右键重命名 / 移动分类":custom?"拖入时间轴按落点新建轨道；双击从播放头添加。保留原位置和全部关键帧。":resourceKind=="event-presets"?"拖到时间轴创建事件；双击从播放头添加。":resourceKind=="generation-presets"?"拖到来源物件上，自动循环生成弹幕；双击应用到所选物件。":resourceKind=="presets"?"拖到物件或拉镜上应用动作；双击应用到所选轨道。":"拖到同类轨道上应用；拖到空白处或双击可新建轨道。")});
        resources.SmallImageList=icons;if(previous!=null)previous.Dispose();resources.EndUpdate();
    }
    private ContextMenuStrip PresetResourceMenu()
    {
        var menu=new ContextMenuStrip{BackColor=Theme.Panel,ForeColor=Theme.Text,ShowImageMargin=false};menu.Opening+=(s,e)=>{
            menu.Items.Clear();if(!IsPresetKind(resourceKind)){e.Cancel=true;return;}var entry=SelectedPresetEntry();
            if(entry!=null)menu.Items.Add(entry.folder?"打开分类文件夹":"添加 / 应用预设",null,async(a,b)=>await ActivatePresetResource());
            if(resourceKind=="custom-presets"){
                if(entry!=null){menu.Items.Add("重命名 / 移动分类…",null,(a,b)=>RenamePreset());menu.Items.Add("导出所选…",null,(a,b)=>ExportPreset(false));}
                menu.Items.Add("新建分类文件夹…",null,(a,b)=>NewPresetFolder());menu.Items.Add("导入预设 / 预设包…",null,(a,b)=>ImportPresets());menu.Items.Add("导出当前分类（含子文件夹）…",null,(a,b)=>ExportPreset(true));
            }
        };return menu;
    }
    private async Task ActivatePresetResource()
    {
        var entry=SelectedPresetEntry();if(entry==null)return;if(entry.folder){presetFolder=entry.key;search.Clear();FillResources();return;}await UsePreset(resourceKind,entry.key,selectedTrack,frame);
    }
    private void SaveSelectionAsPreset()
    {
        if(state==null||selectedTrack==null)return;StopPlaying();CommitInspectorEdits();var selected=WithGroupChildren(state.tracks.Where(t=>t.kind!="native"&&(timeline.SelectedTracks.Contains(t)||t==selectedTrack))).ToArray();if(selected.Length==0)return;
        try{using(var dialog=new PresetNameForm("存为自定义预设",selected.Length==1?selected[0].name:"组合预设",presetFolder,Library.Folders(),"保存全部参数、关键帧及贴图。多选轨道按相对时间一起保存。手写 GML 继续引用目标工程中的脚本和对象。")){
            if(Language.ShowDialog(dialog,this)!=DialogResult.OK)return;Library.Save(dialog.Category,dialog.ResultName,TrackPreset.Capture(dialog.ResultName,PresetDependencies(selected),state.fps),document.project);resourceKind="custom-presets";presetFolder=dialog.Category;search.Clear();FillResources();SetStatus("已保存到公共预设库，其他工程可直接使用。");
        }}catch(Exception error){Error(error);}
    }
    private void NewPresetFolder()
    {try{using(var dialog=new PresetNameForm("新建预设分类","新文件夹",presetFolder,Library.Folders(),"预设库跨工程共用；可在分类中继续创建子文件夹。")){if(Language.ShowDialog(dialog,this)!=DialogResult.OK)return;Library.CreateFolder(dialog.Category,dialog.ResultName);resourceKind="custom-presets";presetFolder=dialog.Category;FillResources();}}catch(Exception error){Error(error);}}
    private void RenamePreset()
    {var entry=SelectedPresetEntry();if(entry==null)return;try{using(var dialog=new PresetNameForm("重命名 / 移动分类",entry.name,Path.GetDirectoryName(entry.key)??"",Library.Folders(),"选择新的名称与分类文件夹。同名项目不会覆盖。")){if(Language.ShowDialog(dialog,this)!=DialogResult.OK)return;Library.Move(entry.key,dialog.Category,dialog.ResultName);presetFolder=dialog.Category;FillResources();SetStatus("预设名称与分类已更新。");}}catch(Exception error){Error(error);}}
    private void ImportPresets()
    {try{using(var dialog=new OpenFileDialog{Title="导入自定义预设",Filter="RufMaker 预设 / 分类包|*.rufpreset;*.rufpresets",Multiselect=true}){if(Language.ShowDialog(dialog,this)!=DialogResult.OK)return;int count=0;foreach(string file in dialog.FileNames)count+=Library.Import(file,presetFolder);resourceKind="custom-presets";search.Clear();FillResources();SetStatus("已导入 "+count+" 个公共预设；文件夹分类已保留，同名预设自动另存。");}}catch(Exception error){Error(error);}}
    private void ExportPreset(bool currentFolder)
    {try{var entry=currentFolder?null:SelectedPresetEntry();string relative=entry==null?presetFolder:entry.key;bool folder=entry==null||entry.folder;string name=entry==null?(String.IsNullOrEmpty(presetFolder)?"RufMaker预设库":Path.GetFileName(presetFolder)):entry.name;using(var dialog=new SaveFileDialog{Title="导出自定义预设",Filter=folder?"RufMaker 分类预设包|*.rufpresets":"RufMaker 轨道预设|*.rufpreset",FileName=name,DefaultExt=folder?"rufpresets":"rufpreset",AddExtension=true}){if(Language.ShowDialog(dialog,this)!=DialogResult.OK)return;Library.Export(relative,dialog.FileName);SetStatus("预设已导出，包含轨道关键帧、贴图和所选分类。");}}catch(Exception error){Error(error);}}
    private async Task UsePreset(string kind,string name,Track target,int at)
    {
        if(state==null||busy)return;
        try{
            if(kind=="event-presets"){AddEventPreset(name,at);return;}
            if(kind=="generation-presets"){ApplyGenerationPreset(name,target);return;}
            if(kind=="presets"){ApplyMotionPreset(name,target);return;}
            StopPlaying();CommitInspectorEdits();
            if(kind=="custom-presets"){
                bool enabled=Enabled;SetBusy(true);Enabled=false;
                try{if(state.tracks.Count+PresetLibrary.Read(Library.Resolve(name)).tracks.Count>200)throw new InvalidOperationException("添加此预设后会超过 200 条轨道。");var added=Library.Instantiate(name,document.project,Math.Max(0,at),state.fps);GlobalVariables.RemapCopies(added,state);int end=added.Max(t=>Math.Max(t.end,t.keyframes.Select(k=>k.frame).DefaultIfEmpty(t.end).Max()));if(end>1000000)throw new InvalidOperationException("预设超出工程支持的最大帧数。");var assets=await bridge.Call<ImportResult>("asset-info",new{});document.inventory=assets.inventory;document.spriteMeta=assets.spriteMeta;sprites.Metadata=assets.spriteMeta;PushUndo();int firstLane=added.Min(t=>Math.Max(0,t.lane)),baseLane=timeline.ActiveLane>=0?timeline.ActiveLane:Math.Max(0,state.laneCount-1);foreach(var t in added)t.lane=Math.Min(199,baseLane+Math.Max(0,t.lane-firstLane));RandomParameters.Resolve(new EditorState{fps=state.fps,duration=Math.Max(state.duration,end),randomSeed=state.randomSeed,nativeTest=state.nativeTest,tracks=state.tracks.Select(row=>row.Snapshot()).Concat(added).ToList()});state.tracks.AddRange(added);state.laneCount=Math.Max(state.laneCount,added.Max(t=>t.lane)+1);state.duration=Math.Max(state.duration,end);foreach(var t in added)timeline.Expand(t,true);timeline.SetTrackSelection(added,added[0]);MarkChanged();SetStatus("已添加自定义预设 · "+added.Count+" 个物件，关键帧按落点对齐。");}
                finally{Enabled=enabled;SetBusy(false);}return;
            }
            bool camera=kind=="camera-presets";if(!camera&&kind!="effect-presets")return;string type=camera?"camera":"effect";
            Action<Track> apply=t=>{if(camera){CameraPresets.Apply(t,name,Math.Max(t.start,Math.Min(t.end,at)),state.fps);t.name=name;}else EffectPresets.Apply(t,name);};
            if(target!=null&&target.kind==type){if(target.end-target.start<2){SetStatus("时段至少需要 2 帧才能应用预设。");return;}PushUndo();apply(target);SelectTrack(target,null);timeline.Expand(target,true);MarkChanged();}
            else AddTrack(type,t=>{t.start=Math.Max(0,Math.Min(999998,at));t.end=Math.Min(1000000,t.start+Math.Max(2,(int)Math.Round(state.fps*(camera?StylePresets.Seconds(name):EffectPresets.Seconds(name)))));state.duration=Math.Max(state.duration,t.end);apply(t);});
            SetStatus("已应用 "+name+"；可调整独立属性关键帧。"+(camera?"勾选“渲染拉镜”查看效果。":""));
        }catch(Exception error){Error(error);}
    }
}
