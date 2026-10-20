using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed partial class MainForm : Form
{
    private readonly string root;
    private NativeBridge bridge;
    private LoadData document;
    private EditorState state;
    private SpriteStore sprites;
    private Track selectedTrack;
    private Keyframe selectedKey;
    private RoomInstance selectedInstance;
    private string resourceKind="objects",resourceName;
    private int frame;
    private bool dirty,busy,loading,closing;
    private int revision;
    private readonly Stack<string> undo=new Stack<string>(),redo=new Stack<string>();
    private readonly StageCanvas stage=new StageCanvas();
    private readonly TimelineCanvas timeline=new TimelineCanvas();
    private readonly ParameterInspector properties=new ParameterInspector();
    private readonly SelectionTimeControl rightTime=new SelectionTimeControl();
    private readonly SplitContainer verticalSplit=new SplitContainer(),leftSplit=new SplitContainer(),rightSplit=new SplitContainer();
    private readonly System.Threading.SemaphoreSlim saveGate=new System.Threading.SemaphoreSlim(1,1);
    private WorkspaceView restoredView;
    private bool restoredDocumentView,closeInProgress,parameterScrubbing;
    private readonly ListView resources=new ListView();
    private readonly TextBox search=new TextBox();
    private readonly Label projectTitle=new Label(),selectionTitle=new Label(),timeLabel=new Label();
    private readonly ToolStripStatusLabel status=new ToolStripStatusLabel(),coordinates=new ToolStripStatusLabel();
    private readonly NumericUpDown frameInput=new WheelSafeNumericUpDown(),durationInput=new WheelSafeNumericUpDown();
    private readonly Timer saveTimer=new Timer(),playTimer=new Timer(),inspectorTimer=new Timer();
    private readonly NativeAudio audio=new NativeAudio();
    private readonly List<KeyClipboardItem> copiedKeys=new List<KeyClipboardItem>();
    private string copiedKind;private readonly List<Track> copiedTracks=new List<Track>();private RoomInstance copiedInstance;
    private bool durationLoading;
    private readonly Stopwatch playClock=new Stopwatch();
    private int playStart;
    private Button runButton,exportButton,playButton,codeButton,keyButton,deleteButton;
    private readonly ToolTip tips=new ToolTip();
    private ContextMenuStrip effectMenu;
    private readonly CheckBox keySnap=new CheckBox{Text="游标吸附关键帧",AutoSize=true,Margin=new Padding(8,9,5,0)};

    public MainForm(string root)
    {
        this.root=root;Text="RufMaker · 原生耐久制作器";BackColor=Theme.Back;ForeColor=Theme.Text;Font=Theme.Font;Icon=System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        StartPosition=FormStartPosition.CenterScreen;Size=new Size(1500,940);MinimumSize=new Size(1100,740);AutoScaleMode=AutoScaleMode.None;KeyPreview=true;
        restoredView=WorkspaceView.Load(root);if(restoredView!=null&&restoredView.positioned){StartPosition=FormStartPosition.Manual;Bounds=restoredView.VisibleBounds();if(restoredView.maximized)WindowState=FormWindowState.Maximized;}
        BuildLayout();
        saveTimer.Interval=400;saveTimer.Tick+=async(s,e)=>{saveTimer.Stop();await SaveNow(false);};
        playTimer.Interval=15;playTimer.Tick+=(s,e)=>{if(state==null)return;audio.Update();SetFrame(playStart+(int)(playClock.Elapsed.TotalSeconds*state.fps));timeline.EnsureFrameVisible();if(frame>=state.duration)StopPlaying();};
        inspectorTimer.Interval=100;inspectorTimer.Tick+=(s,e)=>{inspectorTimer.Stop();properties.Refresh();};
        Shown+=async(s,e)=>{try{currentProjectPath=restoredView!=null&&File.Exists(restoredView.projectPath)?restoredView.projectPath:"";bridge=new NativeBridge(root,currentProjectPath);await LoadProject();}catch(Exception error){Error(error);}};
        FormClosing+=OnClosing;
        FormClosed+=(s,e)=>{saveTimer.Dispose();playTimer.Dispose();inspectorTimer.Dispose();audio.Dispose();if(effectMenu!=null)effectMenu.Dispose();if(bridge!=null)bridge.Dispose();if(sprites!=null)sprites.Dispose();if(pendingUpdate!=null)Updater.Start(pendingUpdate);};
    }

    private Button Button(string text,Action click,bool primary=false,int width=90)
    {
        var button=new Button{Text=text,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,MinimumSize=new Size(Math.Max(width,TextRenderer.MeasureText(text,Theme.Font).Width+22),Math.Max(32,Theme.Font.Height+14)),Margin=new Padding(4,5,4,5),FlatStyle=FlatStyle.Flat,BackColor=primary?Theme.Accent:Theme.Raised,ForeColor=primary?Color.FromArgb(22,25,34):Theme.Text,UseVisualStyleBackColor=false,TabStop=false};
        button.FlatAppearance.BorderSize=0;button.FlatAppearance.MouseOverBackColor=primary?Color.FromArgb(255,148,180):Color.FromArgb(50,61,80);
        button.Click+=(s,e)=>click();return button;
    }
    private Label Heading(string text)
    {return new Label{Text=text,Dock=DockStyle.Fill,ForeColor=Theme.Text,BackColor=Theme.Panel,Font=Theme.Bold,Padding=new Padding(12,0,0,0),TextAlign=ContentAlignment.MiddleLeft};}
    private void BuildLayout()
    {
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Margin=Padding.Empty,Padding=new Padding(8)};
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,26));Controls.Add(layout);
        var top=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true,Margin=Padding.Empty};
        top.Controls.Add(new Label{Text="RufMaker",Font=new Font("Segoe UI",19,FontStyle.Bold),ForeColor=Theme.Accent,AutoSize=true,Margin=new Padding(2,0,16,4)});
        projectTitle.AutoSize=true;projectTitle.MaximumSize=new Size(320,0);projectTitle.Margin=new Padding(0,4,16,4);projectTitle.ForeColor=Theme.Muted;projectTitle.Text="原生 Windows 编辑器";top.Controls.Add(projectTitle);
        var topActions=top;BuildProjectActions(topActions);
        exportButton=Button("导出游戏",async()=>await ExportGame(),true,96);topActions.Controls.Add(exportButton);
        runButton=Button("试玩 F5",async()=>await RunGame(),false,96);tips.SetToolTip(runButton,"F5：从播放头进入真实 GameMaker 游戏");topActions.Controls.Add(runButton);
        topActions.Controls.Add(Button("保存",async()=>await SaveNow(true),false,64));
        topActions.Controls.Add(Button("GameMaker",async()=>{try{await SaveNow(false);await bridge.Call<object>("open-native",new{});SetStatus("已打开 GameMaker 工程");}catch(Exception error){Error(error);}},false,100));
        topActions.Controls.Add(Button("重新读取",async()=>{await SaveNow(false);await LoadProject();},false,92));
        topActions.Controls.Add(Button("工程设置",OpenProjectSettings,false,92));topActions.Controls.Add(Button("检测更新",async()=>await CheckUpdates(),false,92));layout.Controls.Add(top,0,0);

        var vertical=verticalSplit;vertical.Dock=DockStyle.Fill;vertical.Orientation=Orientation.Horizontal;vertical.SplitterWidth=6;vertical.BackColor=Theme.Back;vertical.FixedPanel=FixedPanel.Panel2;vertical.Margin=Padding.Empty;
        layout.Controls.Add(vertical,0,1);
        var left=leftSplit;left.Dock=DockStyle.Fill;left.SplitterWidth=6;left.FixedPanel=FixedPanel.Panel1;left.BackColor=Theme.Back;vertical.Panel1.Controls.Add(left);
        var right=rightSplit;right.Dock=DockStyle.Fill;right.SplitterWidth=6;right.FixedPanel=FixedPanel.Panel2;right.BackColor=Theme.Back;left.Panel2.Controls.Add(right);
        BuildResources(left.Panel1);BuildStage(right.Panel1);BuildInspector(right.Panel2);BuildTimeline(vertical.Panel2);
        Shown+=(s,e)=>RestorePanelLayout();
        var strip=new StatusStrip{Dock=DockStyle.Fill,BackColor=Theme.Panel,ForeColor=Theme.Muted,SizingGrip=true};status.Spring=true;status.TextAlign=ContentAlignment.MiddleLeft;status.Text="正在打开工程…";coordinates.Text="";strip.Items.Add(status);strip.Items.Add(coordinates);layout.Controls.Add(strip,0,2);
    }
    private void BuildResources(Control parent)
    {
        var table=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,BackColor=Theme.Panel,Margin=Padding.Empty};
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));table.RowStyles.Add(new RowStyle(SizeType.AutoSize));table.RowStyles.Add(new RowStyle(SizeType.Absolute,Font.Height+16));table.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var header=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true,Margin=Padding.Empty};header.Controls.Add(new Label{Text="资源库",AutoSize=true,Font=Theme.Bold,Margin=new Padding(9,10,12,8)});var import=Button("导入",async()=>await ImportAssets(),false,58);header.Controls.Add(import);table.Controls.Add(header,0,0);
        var tabs=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true,Margin=Padding.Empty};
        foreach(var item in new[]{new[]{"objects","对象"},new[]{"sprites","贴图"},new[]{"scripts","脚本"},new[]{"presets","运动预设"}})
        {string kind=item[0];var button=Button(item[1],()=>{resourceKind=kind;resourceName=null;FillResources();},false,62);button.Margin=new Padding(3,2,0,2);tabs.Controls.Add(button);}table.Controls.Add(tabs,0,1);
        search.Dock=DockStyle.Fill;search.Margin=new Padding(9,3,9,5);search.BackColor=Theme.Raised;search.ForeColor=Theme.Text;search.BorderStyle=BorderStyle.FixedSingle;search.TextChanged+=(s,e)=>FillResources();tips.SetToolTip(search,"按名称筛选对象、贴图或脚本");table.Controls.Add(search,0,2);
        resources.Dock=DockStyle.Fill;resources.View=View.Details;resources.HeaderStyle=ColumnHeaderStyle.None;resources.Columns.Add("资源",196);resources.FullRowSelect=true;resources.MultiSelect=false;resources.HideSelection=false;resources.BorderStyle=BorderStyle.None;resources.BackColor=Theme.Panel;resources.ForeColor=Theme.Text;
        resources.Resize+=(s,e)=>{if(resources.Columns.Count>0)resources.Columns[0].Width=Math.Max(60,resources.ClientSize.Width-6);};
        resources.SelectedIndexChanged+=(s,e)=>{if(resources.SelectedItems.Count==0)return;resourceName=resources.SelectedItems[0].Text;SelectResource();};
        resources.DoubleClick+=async(s,e)=>{if(resourceName==null)return;if(resourceKind=="presets"){ApplyMotionPreset(resourceName,selectedTrack);return;}if(resourceKind=="sprites")ApplySprite(resourceName);else await EditResource(resourceKind,resourceName);};
        resources.ItemDrag+=(s,e)=>{if(resources.SelectedItems.Count>0)resources.DoDragDrop(resourceKind+"|"+resources.SelectedItems[0].Text,DragDropEffects.Copy);};
        table.Controls.Add(resources,0,3);parent.Controls.Add(table);
    }
    private void BuildStage(Control parent)
    {
        var table=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1,Margin=Padding.Empty};table.RowStyles.Add(new RowStyle(SizeType.AutoSize));table.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var header=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true,BackColor=Theme.Panel};header.Controls.Add(new Label{Text="房间画布",AutoSize=true,Font=Theme.Bold,Margin=new Padding(8,8,12,8)});
        var tools=new FlowLayoutPanel{AutoSize=true,WrapContents=true};
        var grid=new CheckBox{Text="网格",Checked=restoredView==null||restoredView.grid,ForeColor=Theme.Muted,AutoSize=true,Margin=new Padding(5,9,5,0)};stage.Grid=grid.Checked;grid.CheckedChanged+=(s,e)=>{stage.Grid=grid.Checked;stage.DisplayOptionsChanged();};
        var hitbox=new CheckBox{Text="碰撞框",Checked=restoredView!=null&&restoredView.hitboxes,ForeColor=Theme.Muted,AutoSize=true,Margin=new Padding(5,9,0,0)};stage.Hitboxes=hitbox.Checked;hitbox.CheckedChanged+=(s,e)=>{stage.Hitboxes=hitbox.Checked;stage.DisplayOptionsChanged();};var renderCamera=new CheckBox{Text="渲染拉镜",Checked=restoredView!=null&&restoredView.renderCamera,ForeColor=Theme.Muted,AutoSize=true,Margin=new Padding(8,9,5,0)};stage.RenderCamera=renderCamera.Checked;renderCamera.CheckedChanged+=(s,e)=>{stage.RenderCamera=renderCamera.Checked;stage.DisplayOptionsChanged();};tools.Controls.Add(grid);tools.Controls.Add(hitbox);tools.Controls.Add(renderCamera);tools.Controls.Add(Button("适应房间",()=>stage.ResetView(),false,80));tips.SetToolTip(stage,"Ctrl+滚轮：缩放；中键拖动：平移；Ctrl+0 / 双击中键：适应房间");header.Controls.Add(tools);tools.BringToFront();table.Controls.Add(header,0,0);
        stage.Dock=DockStyle.Fill;stage.Margin=Padding.Empty;stage.SelectedTracks=timeline.SelectedTracks;stage.SelectTracks=(group,primary)=>timeline.SetTrackSelection(group,primary);stage.SelectTrack=SelectTrack;stage.SelectInstance=SelectInstance;stage.BeginEdit=PushUndo;stage.Changed=MarkChanged;
        stage.InstanceMoved=async instance=>await SaveInstance(instance);stage.ResourceDropped=async(kind,name,p)=>await DropResource(kind,name,p);
        stage.PointerMoved=p=>coordinates.Text=String.Format("X {0:0}    Y {1:0}    Shift：16 像素吸附",p.X,p.Y);stage.ContextMenuStrip=CreateContext(false);table.Controls.Add(stage,0,1);tools.Controls.Add(nativeRender);tools.Controls.Add(regenerateNativeFilm);nativeRender.CheckedChanged+=(s,e)=>ToggleNativeFilm();regenerateNativeFilm.Click+=async(s,e)=>await GenerateNativeFilm();tips.SetToolTip(regenerateNativeFilm,"首次生成后会保存。改动原生代码后，可点此重新生成。");parent.Controls.Add(table);
    }
    private void BuildInspector(Control parent)
    {
        var outer=new Panel{Dock=DockStyle.Fill,AutoScroll=false,BackColor=Theme.Panel};
        var table=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,BackColor=Theme.Panel,Margin=Padding.Empty};
        table.RowStyles.Add(new RowStyle(SizeType.Absolute,Font.Height+20));table.RowStyles.Add(new RowStyle(SizeType.Absolute,rightTime.PreferredSize.Height));table.RowStyles.Add(new RowStyle(SizeType.Percent,100));table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        selectionTitle.Text="属性检查器";selectionTitle.Dock=DockStyle.Fill;selectionTitle.AutoEllipsis=true;selectionTitle.Padding=new Padding(8,0,0,0);selectionTitle.TextAlign=ContentAlignment.MiddleLeft;selectionTitle.Font=Theme.Bold;table.Controls.Add(selectionTitle,0,0);
        rightTime.Dock=DockStyle.Fill;rightTime.Margin=Padding.Empty;rightTime.SnapFrame=value=>SnapCursor(value,Math.Max(1,state==null?1:10.0*(selectedTrack==null?state.duration:Math.Max(1,selectedTrack.end-selectedTrack.start))/Math.Max(100,rightTime.Width)));rightTime.Seek=value=>{StopPlaying();SetFrame(value);timeline.EnsureFrameVisible();};table.Controls.Add(rightTime,0,1);
        properties.Dock=DockStyle.Fill;properties.BeginScrub=()=>{StopPlaying();PushUndo();parameterScrubbing=true;saveTimer.Stop();};properties.EndScrub=()=>{parameterScrubbing=false;timeline.RefreshLayout();UpdateTitle();if(state!=null)rightTime.SetView(state.fps,state.duration,frame,selectedTrack);if(selectedInstance!=null)SaveInstanceFromProperty(selectedInstance);else if(dirty){saveTimer.Stop();saveTimer.Start();}};
        table.Controls.Add(properties,0,2);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true,Padding=new Padding(4),Margin=Padding.Empty};
        codeButton=Button("编辑 GML",async()=>await EditSelection(),false,90);keyButton=Button("＋ 属性关键帧 (K)",AddKey,false,150);deleteButton=Button("删除所选 (Delete)",RemoveSelection,false,150);deleteButton.ForeColor=Theme.Accent;
        properties.ContextMenuStrip=CreateContext(true);
        foreach(var button in new[]{codeButton,keyButton,deleteButton}){button.Margin=new Padding(2);actions.Controls.Add(button);}table.Controls.Add(actions,0,3);outer.Controls.Add(table);parent.Controls.Add(outer);
        Action fit=()=>{int timeHeight=rightTime.GetPreferredSize(new Size(Math.Max(160,outer.ClientSize.Width),0)).Height;table.RowStyles[1].Height=timeHeight;};
        outer.Resize+=(a,b)=>fit();actions.SizeChanged+=(a,b)=>fit();rightTime.RangeChanged=fit;fit();
    }
    private void BuildTimeline(Control parent)
    {
        var table=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,BackColor=Theme.Panel,Margin=Padding.Empty};table.RowStyles.Add(new RowStyle(SizeType.AutoSize));table.RowStyles.Add(new RowStyle(SizeType.AutoSize));table.RowStyles.Add(new RowStyle(SizeType.AutoSize));table.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var bar=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true,Margin=Padding.Empty};
        playButton=Button("▶",TogglePlaying,false,36);bar.Controls.Add(playButton);bar.Controls.Add(Button("■",()=>{StopPlaying();SetFrame(0);},false,36));
        timeLabel.Text="00:00.00";timeLabel.ForeColor=Theme.Text;timeLabel.Font=new Font("Consolas",13);timeLabel.Width=TextRenderer.MeasureText("000:00.00",timeLabel.Font).Width+12;timeLabel.Height=Math.Max(40,timeLabel.Font.Height+12);timeLabel.TextAlign=ContentAlignment.MiddleCenter;bar.Controls.Add(timeLabel);
        frameInput.Width=Math.Max(80,TextRenderer.MeasureText("1000000",Font).Width+30);frameInput.Height=28;frameInput.Margin=new Padding(2,9,14,0);frameInput.BackColor=Theme.Raised;frameInput.ForeColor=Theme.Text;frameInput.Maximum=100000;frameInput.ValueChanged+=(s,e)=>{if(!loading){StopPlaying();SetFrame((int)frameInput.Value);}};bar.Controls.Add(frameInput);
        foreach(var item in new[]{new[]{"emitter","添加弹幕"},new[]{"rod","添加整体对象"},new[]{"camera","添加拉镜"},new[]{"object","添加对象"},new[]{"effect","添加特效"},new[]{"script","添加 GML 事件"}})
        {string kind=item[0];var button=Button(item[1],()=>{if(kind=="rod")ShowShapeMenu((Control)bar.Controls.Cast<Control>().First(c=>c.Text=="添加整体对象"));else if(kind=="effect")ShowEffectMenu((Control)bar.Controls.Cast<Control>().First(c=>c.Text=="添加特效"));else AddTrack(kind);},false,96);button.AutoSize=true;button.AutoSizeMode=AutoSizeMode.GrowAndShrink;button.MinimumSize=new Size(TextRenderer.MeasureText(item[1],Theme.Font).Width+24,32);bar.Controls.Add(button);}
        tips.SetToolTip(playButton,"空格：带音乐播放 / 暂停；F5：原生试玩");table.Controls.Add(bar,0,0);
        var zoomBar=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true,Margin=Padding.Empty};
        zoomBar.Controls.Add(new Label{Text="视野",AutoSize=true,ForeColor=Theme.Muted,Margin=new Padding(8,9,3,0)});
        var zoom=new WheelSafeComboBox{Width=Math.Max(105,Font.Height*6),DropDownStyle=ComboBoxStyle.DropDownList,Margin=new Padding(2,5,2,0),BackColor=Theme.Raised,ForeColor=Theme.Text};zoom.Items.AddRange(new object[]{"全曲","10 秒","30 秒","1 分钟","5 分钟"});zoom.SelectedIndexChanged+=(s,e)=>{if(state==null)return;if(zoom.SelectedIndex==0)timeline.FitAll();else timeline.ViewSeconds(new[]{10,30,60,300}[zoom.SelectedIndex-1]);};zoomBar.Controls.Add(zoom);
        zoomBar.Controls.Add(Button("－",()=>timeline.SetZoom(timeline.PixelsPerSecond*.8f),false,32));zoomBar.Controls.Add(Button("＋",()=>timeline.SetZoom(timeline.PixelsPerSecond*1.25f),false,32));zoomBar.Controls.Add(Button("定位播放头",()=>timeline.EnsureFrameVisible(),false,96));
        keySnap.Checked=restoredView!=null&&restoredView.keySnap;keySnap.Enabled=false;zoomBar.Controls.Add(keySnap);tips.SetToolTip(keySnap,"仅对单个所选物件生效；接近关键帧时吸附，Alt 临时关闭。输入帧数和逐帧按钮不吸附。");timeline.SnapCursor=value=>SnapCursor(value,state==null?1:Math.Max(1,10.0*state.fps/timeline.PixelsPerSecond));
        zoomBar.Controls.Add(new Label{Text="总时长 / 秒",AutoSize=true,ForeColor=Theme.Muted,Margin=new Padding(12,9,3,0)});
        durationInput.DecimalPlaces=2;durationInput.Increment=10;durationInput.Maximum=66666;durationInput.Minimum=1;durationInput.Width=Math.Max(90,TextRenderer.MeasureText("66666.00",Font).Width+30);durationInput.Margin=new Padding(2,5,4,0);durationInput.BackColor=Theme.Raised;durationInput.ForeColor=Theme.Text;durationInput.ValueChanged+=(s,e)=>{if(!durationLoading&&state!=null)ChangeDuration((double)durationInput.Value);};zoomBar.Controls.Add(durationInput);
        zoomBar.Controls.Add(Button("＋60 秒",()=>ChangeDuration(state==null?60:state.duration/(double)state.fps+60),false,80));zoomBar.Controls.Add(Button("按音乐长度",()=>{if(document!=null)ChangeDuration(document.waveform.duration);},false,98));
        zoomBar.Controls.Add(new Label{Text="音量",AutoSize=true,ForeColor=Theme.Muted,Margin=new Padding(10,9,0,0)});var volume=new WheelSafeTrackBar{Minimum=0,Maximum=100,Value=65,Width=95,Height=32,TickStyle=TickStyle.None,Margin=new Padding(0,4,0,0)};volume.ValueChanged+=(s,e)=>audio.SetVolume(volume.Value/100f);zoomBar.Controls.Add(volume);tips.SetToolTip(volume,"时间轴音乐音量");
        zoomBar.Controls.Add(new Label{Text="Ctrl+滚轮缩放 · Shift+滚轮横移",AutoSize=true,ForeColor=Theme.Muted,Margin=new Padding(6,9,0,0)});table.Controls.Add(zoomBar,0,1);
        table.Controls.Add(BuildBeatControls(),0,2);
        timeline.Dock=DockStyle.Fill;timeline.Margin=Padding.Empty;timeline.Seek=value=>{StopPlaying();SetFrame(value);};timeline.PresetDropped=ApplyMotionPreset;timeline.SelectTrack=SelectTrack;timeline.BeginEdit=PushUndo;timeline.Changed=MarkChanged;
        timeline.SelectNativeEvent=async(item,edit)=>{selectedNativeEvent=item;ShowProperties();if(edit&&item!=null)await EditNativeEvent();};
        timeline.PreviewChanged=()=>{stage.SceneChanged();inspectorTimer.Stop();inspectorTimer.Start();};
        timeline.InsertKey=(t,p,f)=>{SelectTrack(t,null);timeline.SelectedProperty=p;SetFrame(f);AddKey();};
        timeline.ContextMenuStrip=CreateContext(false);table.Controls.Add(timeline,0,3);parent.Controls.Add(table);
    }

    private void ChangeDuration(double seconds)
    {
        if(state==null)return;int requested=Math.Max(state.fps,Math.Min(1000000,(int)Math.Ceiling(seconds*state.fps)));
        int content=ContentEnd();
        if(requested<content){SetStatus("总时长不能短于已有内容（第 "+content+" 帧）；请先移动或删除末尾轨道。");UpdateTitle();return;}
        PushUndo();state.duration=requested;SetFrame(frame);MarkChanged();
    }

    private void RestorePanelLayout()
    {
        var view=restoredView??new WorkspaceView();
        leftSplit.Panel1MinSize=180;rightSplit.Panel1MinSize=350;rightSplit.Panel2MinSize=250;verticalSplit.Panel1MinSize=260;verticalSplit.Panel2MinSize=180;
        leftSplit.SplitterDistance=Math.Max(180,Math.Min(view.leftWidth,Math.Max(180,leftSplit.Width-620)));
        rightSplit.SplitterDistance=Math.Max(350,Math.Min(rightSplit.Width-rightSplit.SplitterWidth-250,rightSplit.Width-rightSplit.SplitterWidth-view.rightWidth));
        verticalSplit.SplitterDistance=Math.Max(260,Math.Min(verticalSplit.Height-verticalSplit.SplitterWidth-180,verticalSplit.Height-verticalSplit.SplitterWidth-view.timelineHeight));
    }
    private void RestoreDocumentView()
    {
        if(restoredDocumentView)return;restoredDocumentView=true;if(restoredView==null)return;var view=restoredView;
        resourceKind=new[]{"objects","sprites","scripts","presets"}.Contains(view.resourceKind)?view.resourceKind:"objects";search.Text=view.search??"";FillResources();
        stage.SetView(view.cameraZoom,view.cameraX,view.cameraY);SetFrame(view.frame);timeline.RestoreView(view.timeline);timeline.SelectedProperty=view.selectedProperty??"x";
        var track=state.tracks.FirstOrDefault(t=>t.id==view.selectedTrack);if(track!=null)SelectTrack(track,null);
    }
    private void SaveWorkspaceView()
    {
        Rectangle bounds=WindowState==FormWindowState.Normal?Bounds:RestoreBounds;
        new WorkspaceView{positioned=true,x=bounds.X,y=bounds.Y,width=bounds.Width,height=bounds.Height,maximized=WindowState==FormWindowState.Maximized,
            leftWidth=leftSplit.Panel1.Width,rightWidth=rightSplit.Panel2.Width,timelineHeight=verticalSplit.Panel2.Height,
            projectPath=currentProjectPath,frame=frame,resourceKind=resourceKind,search=search.Text,selectedTrack=selectedTrack==null?null:selectedTrack.id,selectedProperty=timeline.SelectedProperty,
            grid=stage.Grid,hitboxes=stage.Hitboxes,renderCamera=stage.RenderCamera,keySnap=keySnap.Checked,cameraZoom=stage.ViewZoom,cameraX=stage.ViewX,cameraY=stage.ViewY,timeline=timeline.CaptureView()}.Save(root);
    }

    private ContextMenuStrip CreateContext(bool inspector)
    {
        var menu=new ContextMenuStrip{BackColor=Theme.Panel,ForeColor=Theme.Text,ShowImageMargin=false};
        menu.Opening+=(s,e)=>
        {
            menu.Items.Clear();if(state==null){e.Cancel=true;return;}
            if(selectedTrack!=null&&selectedTrack.kind=="native"){if(selectedNativeEvent!=null)menu.Items.Add("编辑此事件代码块",null,async(a,b)=>await EditNativeEvent());menu.Items.Add("编辑整个源文件",null,async(a,b)=>await EditNativeSource());menu.Items.Add("展开原生事件来源",null,(a,b)=>timeline.Expand(selectedTrack,true));menu.Items.Add("收起原生事件来源",null,(a,b)=>timeline.Expand(selectedTrack,false));menu.Items.Add("查看识别提示",null,(a,b)=>MessageBox.Show(this,String.Join(Environment.NewLine+Environment.NewLine,state.nativeProject.warnings),"原生工程兼容提示"));return;}
            string property=CurrentProperty(inspector);
            if(selectedInstance!=null)
            {menu.Items.Add("编辑对象 GML",null,async(a,b)=>await EditSelection());menu.Items.Add("复制房间实例    Ctrl+C",null,(a,b)=>CopySelection());var del=menu.Items.Add("删除房间实例    Delete",null,(a,b)=>RemoveSelection());del.Enabled=selectedInstance.name!="objRufController";menu.Items.Add(new ToolStripSeparator());}
            if(selectedTrack!=null)
            {
                var t=selectedTrack;
                menu.Items.Add("复制所选物件（含全部关键帧）    Ctrl+C",null,(a,b)=>{timeline.ClearKeySelection();CopySelection();});
                menu.Items.Add("展开属性轨道",null,(a,b)=>timeline.Expand(t,true));menu.Items.Add("收起属性轨道",null,(a,b)=>timeline.Expand(t,false));
                if(Motion.Fields(t).Length>0)
                {
                    menu.Items.Add("为 "+Motion.Name(property)+" 添加关键帧    K",null,(a,b)=>AddPropertyKey(property));
                    var add=new ToolStripMenuItem("选择属性添加关键帧");foreach(string item in Motion.Fields(t)){string p=item;add.DropDownItems.Add(Motion.Name(p),null,(a,b)=>AddPropertyKey(p));}menu.Items.Add(add);
                    menu.Items.Add("所有属性在播放头加键    Ctrl+K",null,(a,b)=>AddAllKeys());
                    menu.Items.Add("清除此属性的全部关键帧",null,(a,b)=>{PushUndo();double v=Motion.Value(t,property,frame);t.keyframes.RemoveAll(k=>k.property==property);var f=typeof(Track).GetField(property);if(f!=null)f.SetValue(t,f.FieldType==typeof(int)?(object)(int)Math.Round(v):v);SelectTrack(t,null);MarkChanged();});
                    var easing=new ToolStripMenuItem(timeline.SelectedKeys.Count==0?"轨道默认缓动":"所选 "+timeline.SelectedKeys.Count+" 个关键帧的缓动");for(int i=0;i<Motion.EaseValues.Length;i++){string value=Motion.EaseValues[i];easing.DropDownItems.Add(Motion.EaseNames[i],null,(a,b)=>{PushUndo();if(timeline.SelectedKeys.Count==0)t.easing=value;else foreach(var key in timeline.SelectedKeys)key.easing=value;MarkChanged();});}menu.Items.Add(easing);
                    if(Theme.Visual(t.kind)){
                    var motion=new ToolStripMenuItem("运动逻辑");foreach(var pair in new[]{new[]{"keyframes","关键帧"},new[]{"velocity","速度 / 加速度"},new[]{"orbit","圆周运动"}}){string value=pair[0];motion.DropDownItems.Add(pair[1],null,(a,b)=>{PushUndo();t.motion=value;if(value=="velocity"&&t.velocityX==0&&t.velocityY==0)t.velocityX=1;ShowProperties();MarkChanged();});}motion.DropDownItems.Add("匀速自转 1°/帧",null,(a,b)=>{PushUndo();t.angularSpeed=1;MarkChanged();});motion.DropDownItems.Add("停止自转",null,(a,b)=>{PushUndo();t.angularSpeed=0;MarkChanged();});menu.Items.Add(motion);
                    var trailsMenu=new ToolStripMenuItem("拖影样式");for(int n=0;n<Trails.Values.Length;n++){string mode=Trails.Values[n];trailsMenu.DropDownItems.Add(Trails.Names[n],null,(a,b)=>{PushUndo();foreach(var item in timeline.SelectedTracks.Where(item=>Theme.Visual(item.kind)))Trails.SetMode(item,mode);ShowProperties();MarkChanged();});}menu.Items.Add(trailsMenu);}
                }
                if(timeline.SelectedKeys.Count>0){menu.Items.Add("复制所选 "+timeline.SelectedKeys.Count+" 个关键帧    Ctrl+C",null,(a,b)=>CopyKey());menu.Items.Add("删除所选关键帧    Delete",null,(a,b)=>RemoveSelection());}
                menu.Items.Add("选择本轨道全部关键帧    Ctrl+A",null,(a,b)=>timeline.SelectAllKeys(t));
                var paste=menu.Items.Add("粘贴    Ctrl+V",null,async(a,b)=>await PasteSelection());paste.Enabled=copiedKind!=null;
                menu.Items.Add("复制轨道到播放头    Ctrl+D",null,(a,b)=>DuplicateTrack());
                menu.Items.Add(t.enabled?"禁用轨道":"启用轨道",null,(a,b)=>{PushUndo();t.enabled=!t.enabled;MarkChanged();});
                menu.Items.Add("上移轨道",null,(a,b)=>MoveTrack(-1));menu.Items.Add("下移轨道",null,(a,b)=>MoveTrack(1));
                menu.Items.Add("删除整条轨道",null,(a,b)=>{selectedKey=null;timeline.ClearKeySelection();RemoveSelection();});menu.Items.Add(new ToolStripSeparator());
            }
            if(selectedTrack==null&&copiedKind!=null)menu.Items.Add("粘贴    Ctrl+V",null,async(a,b)=>await PasteSelection());
            var emitters=new ToolStripMenuItem("添加弹幕预设");foreach(string template in new[]{"环形弹幕","扇形弹幕","旋转螺旋","渐变弹速"}){string name=template;emitters.DropDownItems.Add(name,null,(a,b)=>AddPreset(name));}menu.Items.Add(emitters);
            var rods=new ToolStripMenuItem("添加整体对象");rods.DropDownItems.Add("圆环",null,(a,b)=>AddShape("ring"));rods.DropDownItems.Add("多边形",null,(a,b)=>AddShape("polygon"));foreach(var pair in new[]{new[]{"center","中心 + 角度 + 长度"},new[]{"anchor","自身端点 + 手动端点"},new[]{"endpoints","两个手动端点"}}){string mode=pair[0];rods.DropDownItems.Add(pair[1],null,(a,b)=>{AddTrack("rod");selectedTrack.rodMode=mode;if(mode!="center")selectedTrack.angularSpeed=0;ShowProperties();MarkChanged();});}menu.Items.Add(rods);
            menu.Items.Add("添加拉镜",null,(a,b)=>AddTrack("camera"));menu.Items.Add("添加贴图对象",null,(a,b)=>AddTrack("object"));var effects=new ToolStripMenuItem("添加画面特效");FillEffectMenu(effects.DropDownItems);menu.Items.Add(effects);menu.Items.Add("添加 GML 事件",null,(a,b)=>AddTrack("script"));
            menu.Items.Add(new ToolStripSeparator());menu.Items.Add("撤销    Ctrl+Z",null,(a,b)=>Undo(false)).Enabled=undo.Count>0;menu.Items.Add("重做    Ctrl+Y",null,(a,b)=>Undo(true)).Enabled=redo.Count>0;
        };
        return menu;
    }
    private void FillEffectMenu(ToolStripItemCollection items)
    {for(int i=0;i<Effects.Values.Length;i++){string effect=Effects.Values[i];items.Add(Effects.Names[i],null,(s,e)=>AddEffect(effect));}}
    private void ShowEffectMenu(Control anchor)
    {
        if(state==null)return;
        if(effectMenu==null){effectMenu=new ContextMenuStrip{BackColor=Theme.Panel,ForeColor=Theme.Text};FillEffectMenu(effectMenu.Items);}
        effectMenu.Show(anchor,new Point(0,anchor.Height));
    }
    private ContextMenuStrip shapeMenu;
    private void ShowShapeMenu(Control anchor)
    {
        if(state==null)return;if(shapeMenu==null){shapeMenu=new ContextMenuStrip{BackColor=Theme.Panel,ForeColor=Theme.Text};foreach(string kind in new[]{"rod","ring","polygon"}){string shape=kind;shapeMenu.Items.Add(shape=="rod"?"转转棒":shape=="ring"?"圆环":"多边形",null,(s,e)=>AddShape(shape));}}shapeMenu.Show(anchor,new Point(0,anchor.Height));
    }
    private void AddShape(string shape)
    {AddTrack("rod",t=>{t.shape=shape;t.name=shape=="rod"?"转转棒":shape=="ring"?"圆环":"多边形";if(shape!="rod"){t.count=shape=="ring"?24:30;t.orientation=90;}});}
    private void AddEffect(string effect)
    {
        AddTrack("effect",t=>{t.effect=effect;t.name=Effects.Name(effect);t.color=effect=="flash"?"#FFFFFF":"#000000";
        t.end=Math.Min(state.duration,t.start+(effect=="flash"?10:effect=="shake"?50:state.fps*2));t.strength=effect=="vignette"?75:effect=="rgb"?4:8;});
    }
    private void AddAllKeys()
    {if(selectedTrack==null)return;var t=selectedTrack;PushUndo();foreach(string p in Motion.Fields(t))Motion.SetKey(t,p,frame,Motion.Value(t,p,frame));SelectTrack(t,null);timeline.Expand(t,true);MarkChanged();}
    private void AddPreset(string name)
    {
        AddTrack("emitter",t=>{t.name=name;
        if(name=="扇形弹幕"){t.count=7;t.spread=12;}
        if(name=="旋转螺旋"){t.count=3;t.spread=120;t.interval=5;t.angularSpeed=3;}
        if(name=="渐变弹速"){Motion.SetKey(t,"speed",t.start,1);Motion.SetKey(t,"speed",t.end,6);t.easing="sin-inout";}});
    }
    private void MoveTrack(int delta)
    {if(selectedTrack==null||selectedTrack.kind=="native")return;int i=state.tracks.IndexOf(selectedTrack),j=i+delta;if(j<0||j>=state.tracks.Count||state.tracks[j].kind=="native")return;PushUndo();state.tracks.RemoveAt(i);state.tracks.Insert(j,selectedTrack);MarkChanged();}
    private void CopySelection()
    {
        properties.CommitPending();
        if(selectedTrack!=null&&selectedTrack.kind=="native"){SetStatus("原生轨道保留整套 GML；请通过资源编辑代码。");return;}
        if(timeline.SelectedKeys.Count>0){CopyKey();return;}
        if(selectedTrack!=null)
        {
            copiedTracks.Clear();foreach(var t in state.tracks.Where(t=>t.kind!="native"&&(timeline.SelectedTracks.Contains(t)||t==selectedTrack)))copiedTracks.Add(t.Snapshot());
            copiedKind="tracks";SetStatus("已复制 "+copiedTracks.Count+" 个物件及全部关键帧；Ctrl+V 原位粘贴");
        }
        else if(selectedInstance!=null&&selectedInstance.name!="objRufController")
        {copiedInstance=selectedInstance.Snapshot();copiedKind="instance";SetStatus("已复制房间实例；Ctrl+V 原位粘贴");}
    }
    private async Task PasteSelection()
    {
        if(state==null)return;properties.CommitPending();
        if(copiedKind=="keys"){PasteKey();return;}
        if(copiedKind=="tracks"&&copiedTracks.Count>0)
        {
            PushUndo();var pasted=new List<Track>();
            foreach(var source in copiedTracks)
            {var t=source.Snapshot();t.id=Guid.NewGuid().ToString("N");state.duration=Math.Max(state.duration,Math.Max(t.end,t.keyframes.Count==0?t.end:t.keyframes.Max(k=>k.frame)));state.tracks.Add(t);pasted.Add(t);timeline.Expand(t,true);}
            frameInput.Maximum=state.duration;timeline.SetTrackSelection(pasted,pasted.First());MarkChanged();SetStatus("已原位粘贴 "+pasted.Count+" 个物件；位置、时段及关键帧与原件一致");
        }
        else if(copiedKind=="instance"&&copiedInstance!=null)
        {
            PushUndo();try{ReplaceRoom(await bridge.Call<RoomData>("clone-instance",new{room=state.room,raw=copiedInstance.raw}));SelectInstance(document.room.instances.Last());SetStatus("房间实例已原位粘贴");}catch(Exception error){Error(error);}
        }
    }
    private async void PasteFromKeyboard(){await PasteSelection();}
    private void CopyKey()
    {
        if(timeline.SelectedKeys.Count==0)return;copiedKind="keys";copiedKeys.Clear();int anchor=timeline.SelectedKeys.Min(k=>k.frame);
        foreach(var key in timeline.SelectedKeys){var owner=timeline.Owner(key);if(owner!=null)copiedKeys.Add(new KeyClipboardItem{trackId=owner.id,key=Motion.Copy(key),offset=key.frame-anchor});}
        SetStatus("已复制 "+copiedKeys.Count+" 个关键帧（保留相对时间）");
    }
    private void PasteKey()
    {
        if(selectedTrack==null||copiedKeys.Count==0)return;PushUndo();bool singleTrack=copiedKeys.Select(k=>k.trackId).Distinct().Count()==1;int start=Math.Max(0,Math.Min(frame,state.duration-copiedKeys.Max(k=>k.offset)));var pasted=new List<Keyframe>();Track lastTrack=selectedTrack;
        foreach(var item in copiedKeys)
        {var target=singleTrack?selectedTrack:state.tracks.FirstOrDefault(t=>t.id==item.trackId);if(target==null||!Motion.Fields(target).Contains(item.key.property))continue;var key=Motion.Copy(item.key);key.frame=start+item.offset;target.keyframes.RemoveAll(k=>k.property==key.property&&k.frame==key.frame);target.keyframes.Add(key);target.start=Math.Min(target.start,key.frame);target.end=Math.Max(target.end,key.frame);pasted.Add(key);lastTrack=target;timeline.Expand(target,true);}
        if(pasted.Count>0)timeline.SetSelection(pasted,lastTrack,pasted.Last());MarkChanged();
    }
    private void DuplicateTrack()
    {
        if(selectedTrack==null||selectedTrack.kind=="native")return;PushUndo();var originals=timeline.SelectedTracks.Count>0?timeline.SelectedTracks.Where(t=>t.kind!="native").ToArray():new[]{selectedTrack};
        int first=originals.Min(t=>Math.Min(t.start,t.keyframes.Count==0?t.start:t.keyframes.Min(k=>k.frame))),last=originals.Max(t=>Math.Max(t.end,t.keyframes.Count==0?t.end:t.keyframes.Max(k=>k.frame)));
        int delta=Math.Max(-first,Math.Min(state.duration-last,frame-originals.Min(t=>t.start)));var copies=new List<Track>();
        foreach(var original in originals){var t=original.Snapshot();t.id=Guid.NewGuid().ToString("N");t.name+=" 副本";t.start+=delta;t.end+=delta;foreach(var key in t.keyframes)key.frame+=delta;state.tracks.Add(t);copies.Add(t);timeline.Expand(t,true);}timeline.SetTrackSelection(copies);MarkChanged();
    }

    private async Task LoadProject(LoadData loaded=null)
    {
        bool wasBusy=busy,wasEnabled=Enabled;SetBusy(true);Enabled=false;
        try
        {
            if(bridge==null)return;StopPlaying();nativeRender.Checked=false;stage.NativeFilmPath=null;SetStatus("正在读取工程…");document=loaded??await bridge.Call<LoadData>("load",new{});state=document.state;nativeRender.Enabled=state.nativeProject!=null;Motion.Normalize(state);currentProjectPath=document.descriptor;await LoadCurrentMusic();RefreshBeatControls();
            bool migrated=!state.timelineInitialized;if(migrated){if(state.nativeProject==null&&state.duration==1500)state.duration=Math.Max(state.tracks.Count==0?state.fps:state.tracks.Max(t=>Math.Max(t.end,t.keyframes.Count==0?0:t.keyframes.Max(k=>k.frame))),(int)Math.Ceiling(document.waveform.duration*state.fps));state.timelineInitialized=true;}
            if(sprites!=null)sprites.Dispose();sprites=new SpriteStore{Project=document.project,Metadata=document.spriteMeta};
            selectedTrack=null;selectedKey=null;selectedInstance=null;selectedNativeEvent=null;resourceName=null;dirty=false;undo.Clear();redo.Clear();
            stage.State=state;stage.Room=document.room;stage.Sprites=sprites;stage.ObjectSprites=document.inventory.objectSprites;
            timeline.ClearKeySelection();timeline.State=state;timeline.Wave=document.waveform;frameInput.Maximum=state.duration;FillResources();SelectProject();SetFrame(Math.Min(frame,state.duration));timeline.RefreshLayout();timeline.FitAll();RestoreDocumentView();UpdateTitle();stage.SceneChanged();

            if(state.nativeProject!=null){var original=state.tracks.FirstOrDefault(t=>t.kind=="native");if(original!=null){original.end=Math.Min(state.duration,state.nativeProject.duration);timeline.Expand(original,true);}}
            if(migrated)MarkChanged();await SaveNow(false);SetStatus(state.nativeProject==null?"工程已读取 · 空格带音乐播放 · F5 原生试玩 · 右键编辑轨道":"原生工程已读取 · 勾选“原生游戏渲染”执行原 GML 画面；静态画布仅显示资源布局。");
        }
        catch(Exception error){Error(error);}
        finally{Enabled=wasEnabled;SetBusy(wasBusy);}
        await EnsureNativeFilm();
    }
    private void FillResources()
    {
        if(document==null)return;string[] names=resourceKind=="presets"?MotionPresets.Names:resourceKind=="objects"?document.inventory.objects:resourceKind=="sprites"?document.inventory.sprites:document.inventory.scripts;
        resources.BeginUpdate();resources.Items.Clear();var previous=resources.SmallImageList;var icons=new ImageList{ImageSize=new Size(24,24),ColorDepth=ColorDepth.Depth32Bit};
        foreach(string name in names.Where(n=>n.IndexOf(search.Text,StringComparison.OrdinalIgnoreCase)>=0))
        {
            string spriteName=name;if(resourceKind=="objects")document.inventory.objectSprites.TryGetValue(name,out spriteName);
            var icon=new Bitmap(24,24);using(var g=Graphics.FromImage(icon))
            {
                var image=resourceKind=="scripts"||resourceKind=="presets"?null:sprites.Get(spriteName);g.Clear(Color.Transparent);
                if(image!=null){float ratio=Math.Min(22f/image.Width,22f/image.Height);g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;g.DrawImage(image,(24-image.Width*ratio)/2,(24-image.Height*ratio)/2,image.Width*ratio,image.Height*ratio);}
                else Theme.Label(g,resourceKind=="scripts"?"{}":resourceKind=="presets"?"↗":"◇",new Rectangle(0,0,24,24),Theme.Accent,Theme.Bold,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
            }
            icons.Images.Add(icon);resources.Items.Add(new ListViewItem(name,icons.Images.Count-1));
        }
        resources.SmallImageList=icons;if(previous!=null)previous.Dispose();resources.EndUpdate();
    }
    private void UpdateTitle(){if(state!=null){projectTitle.Text=String.Format("{0} / {1}\n{2} FPS    {3} BPM    {4:0.##} 秒",String.IsNullOrEmpty(state.name)?document.projectName:state.name,state.room,state.fps,state.bpm,state.duration/(double)state.fps);durationLoading=true;durationInput.Value=Math.Max(durationInput.Minimum,Math.Min(durationInput.Maximum,(decimal)(state.duration/(double)state.fps)));durationLoading=false;}}
    private void SetStatus(string text){status.Text=text;}
    private void Error(Exception error){SetStatus("操作失败："+error.Message);MessageBox.Show(this,error.Message,"RufMaker",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    private void SetFrame(int value)
    {
        if(state==null)return;int previous=frame;frame=Math.Max(0,Math.Min(state.duration,value));stage.ShowFrame(frame);timeline.Frame=frame;loading=true;frameInput.Maximum=state.duration;frameInput.Value=frame;loading=false;
        var time=TimeSpan.FromSeconds(frame/(double)state.fps);timeLabel.Text=String.Format("{0:00}:{1:00}.{2:00}",(int)time.TotalMinutes,time.Seconds,time.Milliseconds/10);rightTime.SetFrame(frame);timeline.PlayheadMoved(previous);inspectorTimer.Stop();inspectorTimer.Start();
    }
    private string Snapshot(){return Json.Write(new UndoSnapshot{state=state,room=document.room});}
    private void PushUndo(){if(state==null||parameterScrubbing)return;string snapshot=Snapshot();if(undo.Count==0||undo.Peek()!=snapshot)undo.Push(snapshot);redo.Clear();}
    private void MarkChanged()
    {if(state==null)return;foreach(var t in state.tracks.Where(t=>t.kind=="native")){t.start=0;t.end=Math.Min(state.duration,state.nativeProject.duration);t.enabled=true;}dirty=true;revision++;saveTimer.Stop();if(!parameterScrubbing&&!closeInProgress)saveTimer.Start();stage.SceneChanged();RefreshBeatControls();if(!parameterScrubbing){timeline.RefreshLayout();properties.Refresh();UpdateTitle();rightTime.SetView(state.fps,state.duration,frame,selectedTrack);SetStatus("正在同步修改…");}}
    private int ContentEnd(){int tracks=state.tracks.Where(t=>t.kind!="native").Select(t=>Math.Max(t.end,t.keyframes.Count==0?0:t.keyframes.Max(k=>k.frame))).DefaultIfEmpty(0).Max();int native=state.nativeProject==null?0:state.nativeProject.events.Select(e=>e.frame).DefaultIfEmpty(0).Max();return Math.Max(tracks,native);}
    private async Task<bool> SaveNow(bool notify)
    {
        if(state==null||bridge==null)return true;await saveGate.WaitAsync();saveTimer.Stop();int current=revision;
        try
        {
            var snapshot=Json.Read<EditorState>(Json.Write(state));var result=await bridge.Call<SaveResult>("save",snapshot);ReplaceRoom(result.room);
            if(current==revision)dirty=false;SetStatus("已保存到 GameMaker 工程");if(notify)stage.Focus();return true;
        }
        catch(Exception error){Error(error);return false;}
        finally{saveGate.Release();if(dirty&&!closeInProgress&&!parameterScrubbing){saveTimer.Stop();saveTimer.Start();}}
    }
    private void UpdateSelection()
    {stage.SelectedTrack=selectedTrack;stage.SelectedKey=selectedKey;stage.SelectedInstance=selectedInstance;timeline.SelectedTrack=selectedTrack;timeline.SelectedKey=selectedKey;keySnap.Enabled=selectedTrack!=null&&selectedTrack.kind!="native"&&timeline.SelectedTracks.Count==1;if(state!=null)rightTime.SetView(state.fps,state.duration,frame,selectedTrack);stage.Invalidate();timeline.Invalidate();}
    private void SelectTrack(Track track,Keyframe key)
    {if(!timeline.NotifyingSelection)timeline.ResetSelection(track,key);selectedTrack=track;selectedKey=key;selectedInstance=null;resourceName=null;if(key!=null)timeline.SelectedProperty=key.property;if(track!=null&&!Motion.Fields(track).Contains(timeline.SelectedProperty))timeline.SelectedProperty=Motion.Fields(track).FirstOrDefault()??"x";UpdateSelection();ShowProperties();}
    private void SelectInstance(RoomInstance instance)
    {timeline.ResetSelection(null,null);selectedInstance=instance;selectedTrack=null;selectedKey=null;resourceName=null;UpdateSelection();ShowProperties();}
    private void SelectResource()
    {if(resourceKind=="presets"){SetStatus("将运动预设拖到画布或时间轴上的物件；双击应用到单个所选物件。");return;}if(resourceKind=="sprites"&&selectedTrack!=null&&Theme.Visual(selectedTrack.kind)){ApplySprite(resourceName);return;}timeline.ResetSelection(null,null);selectedTrack=null;selectedKey=null;selectedInstance=null;UpdateSelection();ShowProperties();}
    private void SelectProject(){timeline.ResetSelection(null,null);selectedTrack=null;selectedKey=null;selectedInstance=null;resourceName=null;UpdateSelection();ShowProperties();}
    private void Field<T>(NativeProperties bag,string name,string category,Func<T> get,Action<T> set,string[] choices=null,string description="")
    {bag.Add(name,category,typeof(T),()=>get(),value=>{PushUndo();set((T)value);MarkChanged();},choices,description);}
    private static double ClampValue(string property,double value)
    {if(property=="alpha"||property=="fxOpacity"||property=="fxRetention")return Math.Max(0,Math.Min(1,value));if(property=="count")return Math.Max(1,Math.Min(300,Math.Round(value)));if(property=="life")return Math.Max(1,value);if(property=="sides")return Math.Max(3,Math.Min(64,Math.Round(value)));if(property=="cameraZoom")return Math.Max(.05,Math.Min(20,value));if(property=="radius"||property=="length"||property=="strength")return Math.Max(0,value);return value;}
    private string CurrentProperty(bool inspector=false)
    {if(selectedTrack==null)return "x";if(inspector&&properties.SelectedLabel!=null){string label=properties.SelectedLabel;foreach(string p in Motion.Fields(selectedTrack))if(Motion.Name(p)==label)return p;}return selectedKey!=null?selectedKey.property:timeline.SelectedProperty;}
    private void EaseField(NativeProperties bag,Track t,Keyframe key)
    {
        var names=key==null?Motion.EaseNames:new[]{"继承轨道"}.Concat(Motion.EaseNames).ToArray();var values=key==null?Motion.EaseValues:new[]{""}.Concat(Motion.EaseValues).ToArray();
        Field(bag,key==null?"默认缓动":timeline.SelectedKeys.Count>1?"整组缓动":"至下一关键帧的缓动",key==null?"运动":"所选关键帧",()=>names[Math.Max(0,Array.IndexOf(values,key==null?t.easing:key.easing??""))],value=>{string easing=values[Array.IndexOf(names,value)];if(key==null)t.easing=easing;else foreach(var selected in timeline.SelectedKeys)selected.easing=easing;},names);
    }
    private void AnimatedFields(NativeProperties bag,Track track)
    {
        foreach(string item in Motion.Fields(track).Where(p=>!Rainbow.Parameters.Contains(p)))
        {string property=item;bool keyed=Motion.HasKeys(track,property);bag.Add(Motion.Name(property),keyed?"关键帧属性 · 当前播放头":"全局参数 · 整个时段生效",typeof(double),()=>Motion.Value(track,property,frame),value=>{PushUndo();Motion.EditValue(track,property,frame,ClampValue(property,(double)value));timeline.SelectedProperty=property;MarkChanged();},null,keyed?"此属性已启用关键帧，修改播放头处的值。按住数字上下拖动；Ctrl 微调，Shift 加速。":"此属性没有关键帧，修改整个物件的全局值。需要动画时右键添加关键帧。按住数字上下拖动；Ctrl 微调，Shift 加速。");}
    }
    private void ShowProperties()
    {
        if(state==null)return;var bag=new NativeProperties();codeButton.Enabled=false;keyButton.Enabled=false;deleteButton.Enabled=false;
        if(selectedInstance!=null)
        {
            var instance=selectedInstance;selectionTitle.Text="房间实例 · "+instance.name;
            bag.Add("对象","实例",typeof(string),()=>instance.name,null);
            bag.Add("X","位置",typeof(double),()=>instance.x,value=>{PushUndo();instance.x=Math.Round((double)value);stage.SceneChanged();if(!parameterScrubbing)SaveInstanceFromProperty(instance);});
            bag.Add("Y","位置",typeof(double),()=>instance.y,value=>{PushUndo();instance.y=Math.Round((double)value);stage.SceneChanged();if(!parameterScrubbing)SaveInstanceFromProperty(instance);});codeButton.Enabled=true;deleteButton.Enabled=instance.name!="objRufController";
        }
        else if(selectedTrack!=null&&selectedTrack.kind=="native")
        {NativeProperties(bag);}
        else if(selectedTrack!=null&&timeline.SelectedTracks.Count>1)
        {ShowGroupProperties(bag);deleteButton.Enabled=true;}
        else if(selectedTrack!=null)
        {
            var track=selectedTrack;selectionTitle.Text=timeline.SelectedKeys.Count>1?"已选择 "+timeline.SelectedKeys.Count+" 个关键帧":Theme.KindName(track.kind)+" · "+track.name;
            Field(bag,"名称","轨道",()=>track.name,value=>track.name=value);Field(bag,"启用","轨道",()=>track.enabled,value=>track.enabled=value);
            Field(bag,"开始帧","轨道",()=>track.start,value=>track.start=Math.Max(0,Math.Min(track.end,value)));Field(bag,"结束帧","轨道",()=>track.end,value=>track.end=Math.Max(track.start,Math.Min(state.duration,value)));
            if(track.kind=="rod"){
                string[] shapes={"转转棒","圆环","多边形"},shapeValues={"rod","ring","polygon"};
                Field(bag,"种类","整体对象",()=>shapes[Math.Max(0,Array.IndexOf(shapeValues,track.shape))],value=>{bool autoName=shapes.Contains(track.name);track.shape=shapeValues[Array.IndexOf(shapes,value)];if(autoName)track.name=value;BeginInvoke(new Action(ShowProperties));},shapes);
            }
            if(Theme.Visual(track.kind))
            {
                keyButton.Enabled=true;Field(bag,"贴图","外观",()=>track.sprite,value=>{Rainbow.SetSprite(track,value);BeginInvoke(new Action(ShowProperties));},document.inventory.sprites);
                if(track.rainbow)bag.Add("颜色（彩虹固定白色）","外观",typeof(Color),()=>Color.White,null);
                else Field(bag,"颜色","外观",()=>Motion.ColorOf(track.color),value=>track.color=String.Format("#{0:X2}{1:X2}{2:X2}",value.R,value.G,value.B));
                RainbowFields(bag,new[]{track});
                if(track.kind!="object")Field(bag,"贴图跟随发射 / 棒角度","外观",()=>track.rotateSprite,value=>track.rotateSprite=value,null,"默认关闭。弹幕发射方向、转转棒方向与贴图角度分别控制。");
                TrailFields(bag,new[]{track});
                EaseField(bag,track,null);
                Field(bag,"Power 指数","运动",()=>track.easePower,value=>track.easePower=Math.Max(.1,value));Field(bag,"ExpSin 周期","运动",()=>track.easePeriod,value=>track.easePeriod=Math.Max(.01,value));
                string[] motionNames={"关键帧","速度 / 加速度","圆周运动","抛物线（起点 / 终点 / 峰）"},motionValues={"keyframes","velocity","orbit","parabola"};
                Field(bag,"运动逻辑","运动",()=>motionNames[Math.Max(0,Array.IndexOf(motionValues,track.motion))],value=>{track.motion=motionValues[Array.IndexOf(motionNames,value)];BeginInvoke(new Action(ShowProperties));},motionNames,"运动叠加在关键帧位置上，时间单位为帧。");
                Field(bag,"自转速度 °/帧","运动",()=>track.angularSpeed,value=>track.angularSpeed=value);
                if(track.motion=="velocity"){Field(bag,"X 速度","运动",()=>track.velocityX,value=>track.velocityX=value);Field(bag,"Y 速度","运动",()=>track.velocityY,value=>track.velocityY=value);Field(bag,"X 加速度","运动",()=>track.accelerationX,value=>track.accelerationX=value);Field(bag,"Y 加速度","运动",()=>track.accelerationY,value=>track.accelerationY=value);}
                if(track.motion=="orbit")Field(bag,"公转速度 °/帧","运动",()=>track.orbitSpeed,value=>track.orbitSpeed=value);
                if(track.motion=="parabola")bag.Add("路径说明","运动",typeof(string),()=>"设置起点、终点和峰（通过点）；路径进度 0→1 沿连续曲线运动。",null);
                AnimatedFields(bag,track);
                keyButton.Text="＋ "+Motion.Name(CurrentProperty())+" 关键帧  (K)";
            }
            if(track.kind=="emitter")
            {
                Field(bag,"发射间隔","发射器",()=>track.interval,value=>track.interval=Math.Max(1,value));
            }
            if(track.kind=="rod")
            {
                if(track.shape=="rod"){
                string[] names={"中心 + 角度 + 长度","自身为端点1 + 手动端点2","手动端点1 + 手动端点2"},values={"center","anchor","endpoints"};
                Field(bag,"端点模式","转转棒",()=>names[Math.Max(0,Array.IndexOf(values,track.rodMode))],value=>track.rodMode=values[Array.IndexOf(names,value)],names);
                Field(bag,"循环滚动","转转棒",()=>track.rolling,value=>track.rolling=value,null,"关闭时含两个端点等距摆放；开启时保持数量，在半开线段内循环移动。滚动速度单位为像素/帧。");
                }else {Field(bag,"沿轮廓滚动","整体对象",()=>track.rolling,value=>track.rolling=value,null,"按滚动速度沿闭合轮廓移动，可叠加自转。单位为像素/帧。");bag.Add("排列方式","整体对象",typeof(string),()=>"闭合轮廓等距排列；角度控制弹幕沿轮廓旋转。",null);}
            }
            if(track.kind=="camera")
            {
                keyButton.Enabled=true;keyButton.Text="＋ "+Motion.Name(CurrentProperty())+" 关键帧  (K)";EaseField(bag,track,null);
                Field(bag,"Power 指数","运动",()=>track.easePower,value=>track.easePower=Math.Max(.1,value));Field(bag,"ExpSin 周期","运动",()=>track.easePeriod,value=>track.easePeriod=Math.Max(.01,value));AnimatedFields(bag,track);
                bag.Add("说明","拉镜",typeof(string),()=>"X/Y 为镜头中心；缩放大于 1 拉近。时段外恢复房间视图；重叠时下方轨道优先。",null);
            }
            if(track.kind=="effect")
            {
                keyButton.Enabled=true;keyButton.Text="＋ "+Motion.Name(CurrentProperty())+" 关键帧  (K)";
                Field(bag,"效果","特效",()=>Effects.Name(track.effect),value=>{bool autoName=track.name==Effects.Name(track.effect)||track.name=="闪屏";track.effect=Effects.Values[Array.IndexOf(Effects.Names,value)];if(autoName)track.name=Effects.Name(track.effect);BeginInvoke(new Action(ShowProperties));},Effects.Names);
                Field(bag,"遮罩 / 闪屏颜色","特效",()=>Motion.ColorOf(track.color),value=>track.color=String.Format("#{0:X2}{1:X2}{2:X2}",value.R,value.G,value.B));
                if(track.effect=="shake")Field(bag,"震动频率","特效",()=>track.fxFrequency,value=>track.fxFrequency=Math.Max(.01,value));
                if(track.effect=="blur"||track.effect=="motion-blur")Field(bag,"模糊采样数","特效",()=>track.fxSamples,value=>track.fxSamples=Math.Max(2,Math.Min(16,value)));
                EaseField(bag,track,null);AnimatedFields(bag,track);
                bag.Add("提示","特效",typeof(string),()=>"强度：像素；暗角：0–100。方向：角度。",null);
            }
            if(track.kind=="script"){bag.Add("触发帧","GML",typeof(int),()=>track.start,null);codeButton.Enabled=true;}
            if(selectedKey!=null)
            {
                var key=selectedKey;
                bag.Add("属性","所选关键帧",typeof(string),()=>Motion.Name(key.property),null);
                if(timeline.SelectedKeys.Count>1)bag.Add("选中数量","所选关键帧",typeof(int),()=>timeline.SelectedKeys.Count,null);
                Field(bag,timeline.SelectedKeys.Count>1?"整组移动 · 主帧":"帧","所选关键帧",()=>key.frame,value=>{timeline.MoveSelectedTo(Math.Max(0,Math.Min(state.duration,value)));SetFrame(key.frame);});
                Field(bag,"数值","所选关键帧",()=>key.value,value=>key.value=ClampValue(key.property,value));EaseField(bag,track,key);
            }
            deleteButton.Enabled=true;
        }
        else if(resourceName!=null)
        {
            string name=resourceName;selectionTitle.Text="资源 · "+name;bag.Add("名称","资源",typeof(string),()=>name,null);bag.Add("类型","资源",typeof(string),()=>resourceKind,null);codeButton.Enabled=resourceKind!="sprites";
        }
        else
        {
            selectionTitle.Text="属性检查器";bag.Add("操作","编辑",typeof(string),()=>"选中画布或时间轴物件以编辑参数。工程设置请点击顶栏按钮。",null);
        }
        properties.SelectedObject=bag;
    }
    private void ShowGroupProperties(NativeProperties bag)
    {
        var group=timeline.SelectedTracks.ToArray();var primary=selectedTrack;
        selectionTitle.Text="已选择 "+group.Length+" 个物件";
        bag.Add("选中数量","批量编辑",typeof(int),()=>group.Length,null);
        Field(bag,"主物件开始帧 · 整组平移","批量时间",()=>primary.start,value=>timeline.MoveTracks(value-primary.start),null,"所有物件及关键帧平移相同帧数，保留相对时间。");
        Field(bag,"开始边界 · 整组拉伸","批量时间",()=>primary.start,value=>timeline.TrimTracks(true,value-primary.start),null,"仅改变各物件开始边界，关键帧不移动。");
        Field(bag,"结束边界 · 整组拉伸","批量时间",()=>primary.end,value=>timeline.TrimTracks(false,value-primary.end));
        Field(bag,"主物件长度 / 帧","批量时间",()=>primary.end-primary.start,value=>timeline.TrimTracks(false,value-(primary.end-primary.start)));
        Field(bag,"启用全部","批量编辑",()=>group.All(t=>t.enabled),value=>{foreach(var t in group)t.enabled=value;});
        if(group.All(t=>Theme.Spatial(t.kind)))
        {
            foreach(string property in new[]{"x","y"})
            {string p=property;Field(bag,"整组 "+p.ToUpper()+" · 主物件","批量位置",()=>Motion.Value(primary,p,frame),value=>{double delta=value-Motion.Value(primary,p,frame);foreach(var t in group)Motion.EditValue(t,p,frame,Motion.Value(t,p,frame)+delta);});}
            if(group.All(t=>Theme.Visual(t.kind))){
            foreach(string property in new[]{"scale","alpha"})
            {string p=property;Field(bag,"统一"+Motion.Name(p),"批量外观",()=>Motion.Value(primary,p,frame),value=>{foreach(var t in group)Motion.EditValue(t,p,frame,ClampValue(p,value));});}
            Field(bag,"统一贴图","批量外观",()=>primary.sprite,value=>{foreach(var t in group)Rainbow.SetSprite(t,value);BeginInvoke(new Action(ShowProperties));},document.inventory.sprites);
            RainbowFields(bag,group);TrailFields(bag,group);}
        }
        bag.Add("操作提示","批量编辑",typeof(string),()=>"拖长条平移，拖边缘改长度；Delete 批量删除。",null);
    }
    private void RainbowFields(NativeProperties bag,Track[] group)
    {
        if(!group.All(Rainbow.IsCherry))return;var t=group[0];
        Field(bag,"彩虹","彩虹 Cherry",()=>group.All(track=>track.rainbow),value=>{foreach(var track in group)Rainbow.Enable(track,value);BeginInvoke(new Action(ShowProperties));},null,"使用 sprrainbowcherry，固定白色与 image_speed=0。中心转转棒按离中心的距离分配颜色。");
        if(!group.All(track=>track.rainbow))return;
        Field(bag,"反转","彩虹 Cherry",()=>t.rainbowReverse,value=>{foreach(var track in group)track.rainbowReverse=value;});
        Field(bag,"动态色彩","彩虹 Cherry",()=>t.rainbowDynamic,value=>{foreach(var track in group)track.rainbowDynamic=value;});
        foreach(string item in Rainbow.Parameters)
        {
            string property=item;
            bag.Add(Motion.Name(property),"彩虹 Cherry",typeof(double),()=>Motion.Value(t,property,frame),value=>{PushUndo();foreach(var track in group)Motion.EditValue(track,property,frame,property=="rainbowDensity"?Math.Max(0,(double)value):(double)value);timeline.SelectedProperty=property;MarkChanged();},null,property=="rainbowDensity"?"相邻序号间跨过多少颜色帧。1 表示逐帧递增；0 表示同色。支持独立关键帧。":"支持独立关键帧。动态开启后，按时间更新 image_index，image_speed 仍保持 0。");
        }
    }
    private void TrailFields(NativeProperties bag,Track[] group)
    {
        var t=group[0];Action<Action<Track>> apply=change=>{foreach(var track in group)change(track);};
        Field(bag,"拖影样式","拖影",()=>Trails.Names[Math.Max(0,Array.IndexOf(Trails.Values,Trails.Mode(t)))],value=>{apply(track=>Trails.SetMode(track,Trails.Values[Array.IndexOf(Trails.Names,value)]));BeginInvoke(new Action(ShowProperties));},Trails.Names);
        if(Trails.Mode(t)=="off")return;
        Field(bag,"生成间隔 / 帧","拖影",()=>t.trailInterval,value=>apply(track=>track.trailInterval=Math.Max(1,Math.Min(300,value))));
        Field(bag,"拖影寿命 / 帧","拖影",()=>t.trailLife,value=>apply(track=>track.trailLife=Math.Max(1,Math.Min(300,value))));
        Field(bag,"初始不透明度","拖影",()=>t.trailStartAlpha,value=>apply(track=>track.trailStartAlpha=Math.Max(0,Math.Min(1,value))));
        if(Trails.Mode(t)=="custom")
        {
            Field(bag,"大小衰减 / 帧","拖影",()=>t.trailScaleDecay,value=>apply(track=>track.trailScaleDecay=value),null,"每帧减少相对于原贴图的大小；负值使拖影逐渐变大。");
            Field(bag,"不透明度衰减 / 帧","拖影",()=>t.trailAlphaDecay,value=>apply(track=>track.trailAlphaDecay=value),null,"正值逐渐透明；负值逐渐不透明。");
        }
        Field(bag,"移动速度 / 像素每帧","拖影",()=>t.trailSpeed,value=>apply(track=>track.trailSpeed=value));
        Field(bag,"随机移动方向","拖影",()=>t.trailRandom,value=>apply(track=>track.trailRandom=value));
        Field(bag,"移动方向 / 度","拖影",()=>t.trailDirection,value=>apply(track=>track.trailDirection=value),null,"0°向右，90°向上。开启随机后每个拖影采用独立的固定方向。");
    }
    private void AddTrack(string kind,Action<Track> configure=null)
    {
        if(state==null)return;properties.CommitPending();PushUndo();var track=new Track{id=Guid.NewGuid().ToString("N"),kind=kind,name=kind=="camera"?"拉镜":kind=="rod"?"转转棒":kind=="emitter"?"新弹幕":kind=="object"?"新对象":kind=="effect"?"闪屏":"GML 事件",start=Math.Min(frame,state.duration-1)};
        track.end=Math.Min(state.duration,track.start+(kind=="emitter"?300:kind=="object"?200:10));
        if(kind=="emitter"||kind=="object"||kind=="rod"){track.sprite=kind=="emitter"?"sprBullet":"sprCherry";if(!document.inventory.sprites.Contains(track.sprite))track.sprite=document.inventory.sprites.First();if(kind=="rod"){track.count=5;track.angularSpeed=1;track.end=Math.Min(state.duration,track.start+500);}}
        if(kind=="camera"){track.x=document.room.width/2.0;track.y=document.room.height/2.0;track.end=Math.Min(state.duration,track.start+500);}
        if(kind=="script")track.code="// 在开始帧执行的 GameMaker GML\r\n";
        if(configure!=null)configure(track);state.tracks.Add(track);SelectTrack(track,null);timeline.Expand(track,true);MarkChanged();
    }
    private void AddKey()
    {
        AddPropertyKey(CurrentProperty());
    }
    private void AddPropertyKey(string property)
    {
        if(selectedTrack==null||!Motion.Fields(selectedTrack).Contains(property))return;var track=selectedTrack;
        var existing=track.keyframes.FirstOrDefault(k=>k.frame==frame&&k.property==property);if(existing!=null){SelectTrack(track,existing);timeline.Expand(track,true);return;}
        PushUndo();Motion.SetKey(track,property,frame,Motion.Value(track,property,frame));SelectTrack(track,track.keyframes.First(k=>k.frame==frame&&k.property==property));timeline.Expand(track,true);MarkChanged();
    }
    private void RemoveSelection()
    {
        if(selectedInstance!=null){DeleteInstance();return;}if(selectedTrack==null||selectedTrack.kind=="native")return;PushUndo();if(timeline.SelectedKeys.Count>0){foreach(var track in state.tracks)track.keyframes.RemoveAll(k=>timeline.SelectedKeys.Contains(k));timeline.ClearKeySelection();SelectTrack(selectedTrack,null);}else{var remove=timeline.SelectedTracks.Count>0?timeline.SelectedTracks.ToArray():new[]{selectedTrack};state.tracks.RemoveAll(t=>t.kind!="native"&&remove.Contains(t));SelectProject();}MarkChanged();
    }
    private async void DeleteInstance()
    {if(selectedInstance==null||selectedInstance.name=="objRufController")return;PushUndo();try{var room=await bridge.Call<RoomData>("delete-instance",new{room=state.room,id=selectedInstance.id});selectedInstance=null;ReplaceRoom(room);SelectProject();SetStatus("房间实例已删除 · Ctrl+Z 可撤销");}catch(Exception error){Error(error);}}
    private void ReplaceRoom(RoomData room)
    {string id=selectedInstance==null?null:selectedInstance.id;document.room=room;stage.Room=room;if(id!=null)selectedInstance=room.instances.FirstOrDefault(x=>x.id==id);stage.SelectedInstance=selectedInstance;stage.SceneChanged();}
    private void ApplySprite(string name){if(selectedTrack==null||!Theme.Visual(selectedTrack.kind))return;PushUndo();foreach(var t in timeline.SelectedTracks.Where(t=>Theme.Visual(t.kind)))Rainbow.SetSprite(t,name);ShowProperties();MarkChanged();}
    private async Task DropResource(string kind,string name,PointF position)
    {
        try
        {
            if(kind=="presets"){ApplyMotionPreset(name,stage.TrackAt(position));return;}
            if(kind=="sprites"){AddTrack("object");selectedTrack.sprite=name;selectedTrack.x=Math.Round(position.X);selectedTrack.y=Math.Round(position.Y);ShowProperties();MarkChanged();}
            else if(kind=="objects")
            {PushUndo();ReplaceRoom(await bridge.Call<RoomData>("add-instance",new{room=state.room,name=name,x=(double)position.X,y=(double)position.Y}));SelectInstance(document.room.instances.Last());SetStatus("对象实例已加入 GameMaker 房间");}
        }
        catch(Exception error){Error(error);}
    }
    private async void SaveInstanceFromProperty(RoomInstance instance){await SaveInstance(instance);}
    private async Task SaveInstance(RoomInstance instance)
    {
        try{ReplaceRoom(await bridge.Call<RoomData>("move-instance",new{room=state.room,index=instance.index,x=instance.x,y=instance.y}));UpdateSelection();ShowProperties();SetStatus("房间实例位置已同步");}catch(Exception error){Error(error);}
    }
    private async Task EditSelection()
    {
        if(selectedTrack!=null&&selectedTrack.kind=="native"){if(selectedNativeEvent!=null)await EditNativeEvent();else await EditNativeSource();return;}
        if(selectedTrack!=null&&selectedTrack.kind=="script")
        {var track=selectedTrack;using(var editor=new CodeForm(track.name,track.code,async text=>{PushUndo();track.code=text;MarkChanged();await SaveNow(false);}))editor.ShowDialog(this);}
        else if(selectedInstance!=null)await EditResource("objects",selectedInstance.name);
        else if(resourceName!=null)await EditResource(resourceKind,resourceName);
    }
    private async Task EditResource(string kind,string name)
    {
        try
        {var source=await bridge.Call<SourceData>("source-read",new{kind=kind,name=name});using(var editor=new CodeForm(name+".gml",source.content,async text=>{await bridge.Call<object>("source-save",new{kind=kind,name=name,content=text});SetStatus("GML 已写入 GameMaker 工程");}))editor.ShowDialog(this);}
        catch(Exception error){Error(error);}
    }
    private async Task ImportAssets()
    {
        if(document==null)return;
        using(var dialog=new OpenFileDialog{Title="从素材包导入 PNG 贴图",InitialDirectory=document.pack,Filter="PNG 贴图|*.png",Multiselect=true})
        {
            if(dialog.ShowDialog(this)!=DialogResult.OK)return;
            try
            {
                foreach(string file in dialog.FileNames)
                {
                    string directory=Path.GetDirectoryName(file),folder=Path.GetFileName(directory);
                    if(!String.Equals(Path.GetDirectoryName(directory),document.pack,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("请选择素材包分类文件夹中的 PNG");
                    var result=await bridge.Call<ImportResult>("import",new{folder=folder,name=Path.GetFileName(file)});document.inventory=result.inventory;document.spriteMeta=result.spriteMeta;sprites.Metadata=result.spriteMeta;ApplySprite(result.name);
                }
                FillResources();SetStatus("素材已导入 GameMaker 工程");
            }
            catch(Exception error){Error(error);}
        }
    }
    private void SetBusy(bool value){busy=value;runButton.Enabled=!value;exportButton.Enabled=!value;UseWaitCursor=value;}
    private async Task RunGame()
    {
        if(state==null||busy)return;StopPlaying();saveTimer.Stop();SetBusy(true);SetStatus("正在同步、编译并进入原生试玩…");int target=frame,current=revision;
        try{
            var payload=new{state=Json.Read<EditorState>(Json.Write(state)),frame=target};BuildResult result;
            if(state.nativeProject!=null){using(var dialog=new NativeReplayProgressForm(bridge,payload,target)){dialog.ShowDialog(this);if(dialog.Cancelled){SetStatus("原生试玩重放已取消。");return;}if(dialog.ErrorText!=null)throw new IOException(dialog.ErrorText);result=dialog.Result;}}
            else result=await bridge.Call<BuildResult>("run",payload);
            if(current==revision)dirty=false;SetStatus(result.ready?"原生试玩已进入 "+result.status["room"]+" · 第 "+result.status["frame"]+" 帧":"游戏已启动，请查看游戏窗口");
        }
        catch(Exception error){Error(error);}finally{SetBusy(false);}
    }
    private async Task ExportGame()
    {
        if(state==null||busy)return;StopPlaying();saveTimer.Stop();SetBusy(true);SetStatus("GameMaker 正在编译并打包…");int current=revision;
        try{var result=await bridge.Call<BuildResult>("export",Json.Read<EditorState>(Json.Write(state)));if(current==revision)dirty=false;SetStatus("游戏已导出："+result.exe);Process.Start(new ProcessStartInfo("explorer.exe","\""+result.dir+"\""){UseShellExecute=true});}
        catch(Exception error){Error(error);}finally{SetBusy(false);}
    }
    private void TogglePlaying(){if(state==null)return;if(playTimer.Enabled){StopPlaying();return;}try{if(frame>=state.duration)SetFrame(0);if(audio.Duration>0)audio.Play(frame/(double)state.fps);playStart=frame;playClock.Restart();playTimer.Start();playButton.Text="Ⅱ";SetStatus("时间轴与音乐播放中 · 空格暂停 · F5 进入原生试玩");}catch(Exception error){Error(error);}}
    private void StopPlaying(){playTimer.Stop();playClock.Stop();audio.Pause();playButton.Text="▶";}
    private async void Undo(bool forward)
    {
        var from=forward?redo:undo;var to=forward?undo:redo;if(state==null||from.Count==0||busy)return;StopPlaying();saveTimer.Stop();to.Push(Snapshot());var snapshot=Json.Read<UndoSnapshot>(from.Pop());
        try{state=snapshot.state;document.state=state;stage.State=state;timeline.State=state;await LoadCurrentMusic();RefreshBeatControls();ReplaceRoom(await bridge.Call<RoomData>("restore-room",snapshot.room));SelectProject();SetFrame(frame);MarkChanged();}catch(Exception error){Error(error);}
    }
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData)
    {
        bool editing=beatBar!=null&&beatBar.ContainsFocus||search.ContainsFocus||properties.EditingText||rightTime.ContainsFocus||durationInput.ContainsFocus||frameInput.ContainsFocus;
        if(keyData==(Keys.Control|Keys.N)){NewFromKeyboard();return true;}if(keyData==(Keys.Control|Keys.O)){OpenFromKeyboard();return true;}
        if(keyData==(Keys.Control|Keys.S)){stage.Focus();SaveFromKeyboard();return true;}
        if(keyData==Keys.F5){stage.Focus();RunFromKeyboard();return true;}
        if(!editing)
        {
            if(stage.ContainsFocus&&keyData==(Keys.Control|Keys.D0)){stage.ResetView();return true;}
            if(keyData==Keys.Space||keyData==Keys.P){TogglePlaying();return true;}if(keyData==Keys.K){AddKey();return true;}if(keyData==(Keys.Control|Keys.K)){AddAllKeys();return true;}if(keyData==Keys.Delete){RemoveSelection();return true;}
            if(keyData==(Keys.Control|Keys.D)){DuplicateTrack();return true;}if(keyData==(Keys.Control|Keys.C)){CopySelection();return true;}if(keyData==(Keys.Control|Keys.V)){PasteFromKeyboard();return true;}
            if(keyData==(Keys.Control|Keys.A)){if(timeline.ContainsFocus&&timeline.SelectedKeys.Count==0)timeline.SetTrackSelection(state.tracks);else timeline.SelectAllKeys(selectedTrack);return true;}
            if(keyData==(Keys.Control|Keys.Z)){Undo(false);return true;}if(keyData==(Keys.Control|Keys.Y)){Undo(true);return true;}
        }
        return base.ProcessCmdKey(ref msg,keyData);
    }
    private async void SaveFromKeyboard(){await SaveNow(true);}
    private async void RunFromKeyboard(){await RunGame();}
    private async void OnClosing(object sender,FormClosingEventArgs e)
    {
        if(closing)return;e.Cancel=true;if(closeInProgress)return;if(busy){SetStatus("正在编译，请等待完成后关闭");return;}
        closeInProgress=true;StopPlaying();saveTimer.Stop();
        try{properties.CommitPending();stage.Focus();SaveWorkspaceView();Enabled=false;if(await SaveNow(false)){closing=true;BeginInvoke(new Action(Close));}}
        catch(Exception error){Error(error);}
        finally{if(!closing){closeInProgress=false;Enabled=true;if(dirty)saveTimer.Start();}}
    }
}

internal sealed class CodeForm : Form
{
    private readonly TextBox code=new TextBox();private readonly Button save=new Button();private readonly Func<string,Task> persist;private readonly string newline;
    public CodeForm(string name,string content,Func<string,Task> persist,int line=1)
    {
        this.persist=persist;newline=CodeText.NewlineOf(content);Text="GML · "+name;Size=new Size(920,680);MinimumSize=new Size(600,400);StartPosition=FormStartPosition.CenterParent;BackColor=Theme.Back;ForeColor=Theme.Text;Font=Theme.Font;
        var foot=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=48,FlowDirection=FlowDirection.RightToLeft};save.Text="保存 GML";save.Width=120;save.Height=32;save.Margin=new Padding(8);save.BackColor=Theme.Accent;save.ForeColor=Theme.Back;save.FlatStyle=FlatStyle.Flat;save.Click+=async(s,e)=>await Save();foot.Controls.Add(save);
        var label=new Label{Text="直接编辑 GameMaker GML    ·    Ctrl+S 保存",Dock=DockStyle.Top,Height=34,Padding=new Padding(12,0,0,0),TextAlign=ContentAlignment.MiddleLeft,ForeColor=Theme.Muted};
        var wrap=new CheckBox{Text="自动折行",Checked=true,AutoSize=true,Margin=new Padding(8,15,12,0)};wrap.CheckedChanged+=(s,e)=>{code.WordWrap=wrap.Checked;code.ScrollBars=wrap.Checked?ScrollBars.Vertical:ScrollBars.Both;};foot.Controls.Add(wrap);
        code.Multiline=true;code.AcceptsTab=true;code.AcceptsReturn=true;code.WordWrap=true;code.ScrollBars=ScrollBars.Vertical;code.Dock=DockStyle.Fill;code.Font=new Font("Consolas",11);code.BackColor=Theme.Panel;code.ForeColor=Theme.Text;code.BorderStyle=BorderStyle.None;code.Text=CodeText.ForDisplay(content);
        Controls.Add(code);Controls.Add(label);Controls.Add(foot);
        Shown+=(s,e)=>{code.SelectionStart=CodeText.LineOffset(code.Text,line);code.SelectionLength=0;code.ScrollToCaret();code.Focus();};
    }
    private async Task Save(){save.Enabled=false;try{await persist(CodeText.ForSource(code.Text,newline));DialogResult=DialogResult.OK;Close();}catch(Exception error){MessageBox.Show(this,error.Message,"GML 保存失败",MessageBoxButtons.OK,MessageBoxIcon.Error);}finally{save.Enabled=true;}}
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData){if(keyData==(Keys.Control|Keys.S)){SaveFromKeyboard();return true;}return base.ProcessCmdKey(ref msg,keyData);}
    private async void SaveFromKeyboard(){await Save();}
}
