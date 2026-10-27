using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed partial class MainForm : LocalizedForm
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
    private readonly Stack<UndoEntry> undo=new Stack<UndoEntry>(),redo=new Stack<UndoEntry>();
    private readonly StageCanvas stage=new StageCanvas();
    private readonly TimelineCanvas timeline=new TimelineCanvas{SelectionKeyframesOnly=true};
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

    private readonly Stopwatch playClock=new Stopwatch();
    private Button runButton,exportButton,playButton,codeButton,keyButton,deleteButton;
    private readonly ToolTip tips=new ToolTip();
    private ContextMenuStrip effectMenu;
    private readonly CheckBox keySnap=new CheckBox{Text="游标吸附物件与关键帧",AutoSize=true,Margin=new Padding(8,9,5,0)};

    public MainForm(string root)
    {
        Language.Initialize(root);
        this.root=root;Text="RufMaker";BackColor=Theme.Back;ForeColor=Theme.Text;Font=Theme.Font;Icon=AppWindow.LoadIcon();
        StartPosition=FormStartPosition.CenterScreen;Size=new Size(1500,940);MinimumSize=new Size(1100,740);AutoScaleMode=AutoScaleMode.None;KeyPreview=true;
        restoredView=WorkspaceView.Load(root);if(restoredView!=null&&restoredView.positioned){StartPosition=FormStartPosition.Manual;Bounds=restoredView.VisibleBounds();if(restoredView.maximized)WindowState=FormWindowState.Maximized;}
        BuildLayout();DarkScrollbars.Attach(this);
        Language.PreserveText(projectTitle);Language.PreserveText(selectionTitle);
        saveTimer.Interval=400;saveTimer.Tick+=async(s,e)=>{saveTimer.Stop();await SaveNow(false);};
        playTimer.Interval=15;playTimer.Tick+=(s,e)=>TickPreview();
        inspectorTimer.Interval=100;inspectorTimer.Tick+=(s,e)=>{inspectorTimer.Stop();properties.Refresh();keyProperties.Refresh();};
        Shown+=async(s,e)=>{try{OfferEngineAtStartup();currentProjectPath=restoredView!=null&&File.Exists(restoredView.projectPath)?restoredView.projectPath:"";bridge=new NativeBridge(root,currentProjectPath);await LoadProject();}catch(Exception error){Error(error);}};
        FormClosing+=OnClosing;
        FormClosed+=(s,e)=>{saveTimer.Dispose();playTimer.Dispose();inspectorTimer.Dispose();nativeWatch.Dispose();DisposeAudioAnalysis();audio.Dispose();if(effectMenu!=null)effectMenu.Dispose();if(bridge!=null)bridge.Dispose();if(sprites!=null)sprites.Dispose();if(pendingUpdate!=null)Updater.Start(pendingUpdate);};
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
    { BuildShellLayout(); }

    private void BuildResources(Control parent)
    { BuildShellResources(parent); }

    private void BuildStage(Control parent)
    { BuildShellStage(parent); }

    private void BuildInspector(Control parent)
    { BuildShellInspector(parent); }

    private void BuildTimeline(Control parent)
    { BuildShellTransport(parent); }

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
        resourceKind=new[]{"objects","sprites","scripts"}.Contains(view.resourceKind)||IsPresetKind(view.resourceKind)?view.resourceKind:"objects";search.Text=(view.search??"").Trim();FillResources();
        stage.SetView(view.cameraZoom,view.cameraX,view.cameraY);SetFrame(view.frame);timeline.RestoreView(view.timeline);timeline.SelectedProperty=view.selectedProperty??"x";
        var track=state.tracks.FirstOrDefault(t=>t.id==view.selectedTrack);if(track!=null)SelectTrack(track,null);
    }
    private void SaveWorkspaceView()
    {
        Rectangle bounds=WindowState==FormWindowState.Normal?Bounds:RestoreBounds;
        new WorkspaceView{positioned=true,x=bounds.X,y=bounds.Y,width=bounds.Width,height=bounds.Height,maximized=WindowState==FormWindowState.Maximized,
            leftWidth=leftSplit.Panel1.Width,rightWidth=rightSplit.Panel2.Width,timelineHeight=verticalSplit.Panel2.Height,
            projectPath=currentProjectPath,frame=frame,resourceKind=resourceKind,search=ResourceFilter,selectedTrack=selectedTrack==null?null:selectedTrack.id,selectedProperty=timeline.SelectedProperty,
            objectStatistics=inspectorTabs.SelectedIndex==2,inspectorPage=inspectorTabs.SelectedIndex,keyframesOpen=keyWorkspaceOpen,keyframesHeight=keyWorkspaceHeight,inspectorDock=CaptureInspectorDock(),stretchKeys=stretchKeys,collapsedSections=properties.CaptureSections(),recentProjects=recentProjects.ToArray(),leftHidden=leftSplit.Panel1Collapsed,rightHidden=rightSplit.Panel2Collapsed,grid=stage.Grid,hitboxes=stage.Hitboxes,renderCamera=stage.RenderCamera,keySnap=keySnap.Checked,cursorSnap=keySnap.Checked,previewRate=previewRate,cameraZoom=stage.ViewZoom,cameraX=stage.ViewX,cameraY=stage.ViewY,timeline=timeline.CaptureView()}.Save(root);
    }

    private ContextMenuStrip CreateContext(bool inspector)
    {
        var menu=new ContextMenuStrip{BackColor=Theme.Panel,ForeColor=Theme.Text,ShowImageMargin=false};
        menu.Opening+=(s,e)=>
        {
            menu.Items.Clear();if(state==null){e.Cancel=true;return;}
            if(selectedTrack!=null&&selectedTrack.kind=="native"){if(selectedNativeEvent!=null)menu.Items.Add("编辑此事件代码块",null,async(a,b)=>await EditNativeEvent());menu.Items.Add("编辑整个源文件",null,async(a,b)=>await EditNativeSource());menu.Items.Add("查看识别提示",null,(a,b)=>Language.Message(this,String.Join(Environment.NewLine+Environment.NewLine,state.nativeProject.warnings),"原生工程兼容提示"));return;}
            AddBulletContext(menu);
            string property=CurrentProperty(inspector&&menu.SourceControl!=keyProperties);
            if(selectedInstance!=null)
            {menu.Items.Add("编辑对象 GML",null,async(a,b)=>await EditSelection());menu.Items.Add("复制房间实例    Ctrl+C",null,(a,b)=>CopySelection());var del=menu.Items.Add("删除房间实例    Delete",null,(a,b)=>RemoveSelection());del.Enabled=selectedInstance.name!="objRufController";menu.Items.Add(new ToolStripSeparator());}
            if(selectedTrack!=null)
            {
                var t=selectedTrack;
                menu.Items.Add("复制所选物件（含全部关键帧）    Ctrl+C",null,(a,b)=>{timeline.ClearKeySelection();CopySelection();});
                menu.Items.Add("存为自定义预设…",null,(a,b)=>SaveSelectionAsPreset());
                if(t.templateOnly)menu.Items.Add("预览模板局部动画",null,(a,b)=>EnterTemplateContext(t));
                menu.Items.Add("在右栏编辑关键帧",null,(a,b)=>OpenKeyWorkspace(t));
                if(Motion.Fields(t).Length>0)
                {
                    menu.Items.Add("为 "+Motion.Name(property)+" 添加关键帧    K",null,(a,b)=>AddPropertyKey(property));
                    var add=new ToolStripMenuItem("选择属性添加关键帧");foreach(string item in Motion.Fields(t)){string p=item;add.DropDownItems.Add(Motion.Name(p),null,(a,b)=>AddPropertyKey(p));}menu.Items.Add(add);
                    var formulas=new ToolStripMenuItem("参数公式 / 填充逐帧关键帧…");foreach(string field in Motion.Fields(t)){string p=field;formulas.DropDownItems.Add(Motion.Name(p),null,(a,b)=>EditParameterExpression(timeline.SelectedTracks.Where(item=>Motion.Fields(item).Contains(p)).DefaultIfEmpty(t).ToArray(),p));}menu.Items.Add(formulas);
                    menu.Items.Add("所有属性在播放头加键    Ctrl+K",null,(a,b)=>AddAllKeys());
                    menu.Items.Add("清除此属性的全部关键帧",null,(a,b)=>{PushUndo();double v=Motion.Value(t,property,frame);t.keyframes.RemoveAll(k=>k.property==property);var f=typeof(Track).GetField(property);if(f!=null)f.SetValue(t,f.FieldType==typeof(int)?(object)(int)Math.Round(v):v);SelectTrack(t,null);MarkChanged();});
                    var easing=new ToolStripMenuItem(timeline.SelectedKeys.Count==0?"物件默认缓动":"所选 "+timeline.SelectedKeys.Count+" 个关键帧的缓动");for(int i=0;i<Motion.EaseValues.Length;i++){string value=Motion.EaseValues[i];easing.DropDownItems.Add(Motion.EaseNames[i],null,(a,b)=>{PushUndo();if(timeline.SelectedKeys.Count==0)t.easing=value;else foreach(var key in timeline.SelectedKeys)key.easing=value;MarkChanged();});}menu.Items.Add(easing);
                    if(Theme.Visual(t.kind)){
                    var motion=new ToolStripMenuItem("运动逻辑");foreach(var pair in new[]{new[]{"keyframes","关键帧"},new[]{"velocity","速度 / 加速度"},new[]{"orbit","圆周运动"}}){string value=pair[0];motion.DropDownItems.Add(pair[1],null,(a,b)=>{PushUndo();MotionPresets.Manual(t);t.motion=value;if(value=="velocity"&&t.velocityX==0&&t.velocityY==0)t.velocityX=1;ShowProperties();MarkChanged();});}motion.DropDownItems.Add("匀速自转 1°/帧",null,(a,b)=>{PushUndo();t.angularSpeed=1;MarkChanged();});motion.DropDownItems.Add("停止自转",null,(a,b)=>{PushUndo();t.angularSpeed=0;MarkChanged();});menu.Items.Add(motion);
                    var trailsMenu=new ToolStripMenuItem("拖影样式");for(int n=0;n<Trails.Values.Length;n++){string mode=Trails.Values[n];trailsMenu.DropDownItems.Add(Trails.Names[n],null,(a,b)=>{PushUndo();foreach(var item in timeline.SelectedTracks.Where(item=>Theme.Visual(item.kind)))Trails.SetMode(item,mode);ShowProperties();MarkChanged();});}menu.Items.Add(trailsMenu);}
                }
                if(timeline.SelectedKeys.Count>0){menu.Items.Add("复制所选 "+timeline.SelectedKeys.Count+" 个关键帧    Ctrl+C",null,(a,b)=>CopyKey());menu.Items.Add("删除所选关键帧    Delete",null,(a,b)=>RemoveSelection());}
                menu.Items.Add("选择此物件全部关键帧    Ctrl+A",null,(a,b)=>timeline.SelectAllKeys(t));
                var paste=menu.Items.Add("粘贴    Ctrl+V",null,async(a,b)=>await PasteSelection());paste.Enabled=copiedKind!=null;
                menu.Items.Add("复制物件到播放头    Ctrl+D",null,(a,b)=>DuplicateTrack());
                menu.Items.Add(t.enabled?"禁用物件":"启用物件",null,(a,b)=>{PushUndo();t.enabled=!t.enabled;MarkChanged();});
                var lanesMenu=new ToolStripMenuItem("移动到轨道");for(int n=0;n<state.laneCount;n++){int destination=n;lanesMenu.DropDownItems.Add(Language.Text("轨道")+" "+(n+1),null,(a,b)=>MoveSelectionToLane(destination));}lanesMenu.DropDownItems.Add("新轨道",null,(a,b)=>MoveSelectionToLane(Math.Min(199,state.laneCount)));menu.Items.Add(lanesMenu);menu.Items.Add("上移一条轨道",null,(a,b)=>MoveSelectionToLane(Math.Max(0,t.lane-1)));menu.Items.Add("下移一条轨道",null,(a,b)=>MoveSelectionToLane(Math.Min(199,t.lane+1)));
                menu.Items.Add("删除所选物件",null,(a,b)=>{selectedKey=null;timeline.ClearKeySelection();RemoveSelection();});menu.Items.Add(new ToolStripSeparator());
            }
            if(selectedTrack==null&&copiedKind!=null)menu.Items.Add("粘贴    Ctrl+V",null,async(a,b)=>await PasteSelection());
            if(timeline.ActiveLane>=0){var removeLane=menu.Items.Add("删除空轨道",null,(a,b)=>{int lane=timeline.ActiveLane;if(state.tracks.Any(item=>item.kind!="native"&&item.lane==lane))return;PushUndo();foreach(var item in state.tracks.Where(item=>item.kind!="native"&&item.lane>lane))item.lane--;state.laneCount=Math.Max(1,state.laneCount-1);timeline.ActiveLane=Math.Min(lane,state.laneCount-1);MarkChanged();});removeLane.Enabled=state.laneCount>1&&!state.tracks.Any(item=>item.kind!="native"&&item.lane==timeline.ActiveLane);}menu.Items.Add("新建空轨道",null,(a,b)=>{if(state.laneCount>=200)return;PushUndo();timeline.ActiveLane=state.laneCount++;MarkChanged();});var emitters=new ToolStripMenuItem("添加弹幕预设");foreach(string template in new[]{"环形弹幕","扇形弹幕","旋转螺旋","渐变弹速"}){string name=template;emitters.DropDownItems.Add(name,null,(a,b)=>AddPreset(name));}menu.Items.Add(emitters);
            var rods=new ToolStripMenuItem("添加整体对象");rods.DropDownItems.Add("圆环",null,(a,b)=>AddShape("ring"));rods.DropDownItems.Add("多边形",null,(a,b)=>AddShape("polygon"));foreach(var pair in new[]{new[]{"center","中心 + 角度 + 长度"},new[]{"anchor","自身端点 + 手动端点"},new[]{"endpoints","两个手动端点"}}){string mode=pair[0];rods.DropDownItems.Add(pair[1],null,(a,b)=>{AddTrack("rod");selectedTrack.rodMode=mode;if(mode!="center")selectedTrack.angularSpeed=0;ShowProperties();MarkChanged();});}menu.Items.Add(rods);
            menu.Items.Add("添加拉镜",null,(a,b)=>AddTrack("camera"));menu.Items.Add("添加贴图对象",null,(a,b)=>AddTrack("object"));var effects=new ToolStripMenuItem("添加画面特效");FillEffectMenu(effects.DropDownItems);menu.Items.Add(effects);menu.Items.Add("添加全局变量",null,(a,b)=>AddGlobalVariable());menu.Items.Add("添加 GML 事件",null,(a,b)=>AddTrack("script"));
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
        Language.Attach(effectMenu);effectMenu.Show(anchor,new Point(0,anchor.Height));
    }
    private ContextMenuStrip shapeMenu;
    private void ShowShapeMenu(Control anchor)
    {
        if(state==null)return;if(shapeMenu==null){shapeMenu=new ContextMenuStrip{BackColor=Theme.Panel,ForeColor=Theme.Text};foreach(string kind in FormationShapes.Values){string shape=kind;shapeMenu.Items.Add(FormationShapes.Name(shape),null,(s,e)=>AddShape(shape));}}Language.Attach(shapeMenu);shapeMenu.Show(anchor,new Point(0,anchor.Height));
    }
    private void AddShape(string shape)
    {AddTrack("rod",t=>{t.shape=shape;t.name=FormationShapes.Name(shape);if(shape!="rod"){t.count=shape=="ring"?24:shape=="polygon"?30:60;t.orientation=new[]{"polygon","star","star2"}.Contains(shape)?90:0;t.angularSpeed=0;if(shape=="ellipse")t.shapeAspect=.65;}});}
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
        CommitInspectorEdits();
        if(selectedTrack!=null&&selectedTrack.kind=="native"){SetStatus("原生轨道保留整套 GML；请通过资源编辑代码。");return;}
        if(timeline.SelectedKeys.Count>0){CopyKey();return;}
        if(selectedTrack!=null)
        {
            copiedTracks.Clear();foreach(var t in PresetDependencies(state.tracks.Where(t=>t.kind!="native"&&(timeline.SelectedTracks.Contains(t)||t==selectedTrack))))copiedTracks.Add(t.Snapshot());
            copiedKind="tracks";SetStatus("已复制 "+copiedTracks.Count+" 个物件及全部关键帧；Ctrl+V 粘贴到播放头");
        }
        else if(selectedInstance!=null&&selectedInstance.name!="objRufController")
        {copiedInstance=selectedInstance.Snapshot();copiedKind="instance";SetStatus("已复制房间实例；Ctrl+V 原位粘贴");}
    }
    private async Task PasteSelection()
    {
        if(state==null)return;CommitInspectorEdits();
        if(copiedKind=="keys"){PasteKey();return;}
        if(copiedKind=="tracks"&&copiedTracks.Count>0)
        {
            PushUndo();var pasted=new List<Track>();int delta=frame-copiedTracks.Min(t=>t.start);
            var clones=CloneBulletTracks(copiedTracks);GlobalVariables.RemapCopies(clones,state);foreach(var t in clones)
            {t.start+=delta;t.end+=delta;TimelineLayout.ShiftClock(t,delta);if(t.releaseFrame>=0)t.releaseFrame+=delta;foreach(var key in t.keyframes)key.frame+=delta;state.duration=Math.Max(state.duration,Math.Max(t.end,t.keyframes.Count==0?t.end:t.keyframes.Max(k=>k.frame)));state.tracks.Add(t);pasted.Add(t);timeline.Expand(t,true);}
            frameInput.Maximum=state.duration;timeline.SetTrackSelection(pasted,pasted.First());MarkChanged();SetStatus("已粘贴 "+pasted.Count+" 个物件到播放头；保留相对时间和全部关键帧");
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
        {var target=singleTrack?selectedTrack:state.tracks.FirstOrDefault(t=>t.id==item.trackId);if(target==null||!Motion.Fields(target).Contains(item.key.property)||MotionPresets.Generated(target,item.key.property))continue;var key=Motion.Copy(item.key);key.frame=start+item.offset;target.keyframes.RemoveAll(k=>k.property==key.property&&k.frame==key.frame);target.keyframes.Add(key);target.start=Math.Min(target.start,key.frame);target.end=Math.Max(target.end,key.frame);pasted.Add(key);lastTrack=target;timeline.Expand(target,true);}
        if(pasted.Count>0)timeline.SetSelection(pasted,lastTrack,pasted.Last());MarkChanged();
    }
    private void DuplicateTrack()
    {
        if(selectedTrack==null||selectedTrack.kind=="native")return;PushUndo();var originals=WithGroupChildren(timeline.SelectedTracks.Count>0?timeline.SelectedTracks.Where(t=>t.kind!="native").ToArray():new[]{selectedTrack});
        int first=originals.Min(t=>Math.Min(t.start,t.keyframes.Count==0?t.start:t.keyframes.Min(k=>k.frame))),last=originals.Max(t=>Math.Max(t.end,t.keyframes.Count==0?t.end:t.keyframes.Max(k=>k.frame)));
        int delta=Math.Max(-first,Math.Min(state.duration-last,frame-originals.Min(t=>t.start)));var copies=new List<Track>();
        foreach(var t in CloneBulletTracks(originals)){t.name+=" 副本";t.start+=delta;t.end+=delta;TimelineLayout.ShiftClock(t,delta);foreach(var key in t.keyframes)key.frame+=delta;state.tracks.Add(t);copies.Add(t);timeline.Expand(t,true);}timeline.SetTrackSelection(copies);MarkChanged();
    }

    private async Task LoadProject(LoadData loaded=null)
    {
        bool wasBusy=busy,wasEnabled=Enabled;SetBusy(true);Enabled=false;
        try
        {
            if(bridge==null)return;StopPlaying();nativeRender.Checked=false;stage.NativeFilmPath=null;SetStatus("正在读取工程…");document=loaded??await bridge.Call<LoadData>("load",new{});state=document.state;nativeRender.Enabled=state.nativeProject!=null;Motion.Normalize(state);int separated=TimelineLayout.EnsureExclusive(state);currentProjectPath=document.descriptor;templateEditing=null;templateOrigin=null;stage.TemplatePreviewId=null;timeline.TemplateSource=null;if(!String.IsNullOrEmpty(currentProjectPath)){recentProjects.RemoveAll(p=>String.Equals(p,currentProjectPath,StringComparison.OrdinalIgnoreCase));recentProjects.Insert(0,currentProjectPath);if(recentProjects.Count>10)recentProjects.RemoveAt(10);}await LoadCurrentMusic();RefreshBeatControls();
            bool migrated=!state.timelineInitialized;if(migrated){if(state.nativeProject==null&&state.duration==1500)state.duration=Math.Max(state.tracks.Count==0?state.fps:state.tracks.Max(t=>Math.Max(t.end,t.keyframes.Count==0?0:t.keyframes.Max(k=>k.frame))),(int)Math.Ceiling(document.waveform.duration*state.fps));state.timelineInitialized=true;}
            if(sprites!=null)sprites.Dispose();sprites=new SpriteStore{Project=document.project,Metadata=document.spriteMeta};
            selectedTrack=null;selectedKey=null;selectedInstance=null;selectedNativeEvent=null;resourceName=null;dirty=false;undo.Clear();redo.Clear();
            stage.State=state;stage.Room=document.room;stage.Sprites=sprites;stage.ObjectSprites=document.inventory.objectSprites;RefreshNativeTestPlayer();
            timeline.ClearKeySelection();timeline.State=state;timeline.Wave=document.waveform;frameInput.Maximum=state.duration;FillResources();SelectProject();SetFrame(Math.Min(frame,state.duration));timeline.RefreshLayout();timeline.FitAll();RestoreDocumentView();UpdateTitle();stage.SceneChanged();

            if(state.nativeProject!=null){var original=state.tracks.FirstOrDefault(t=>t.kind=="native");if(original!=null){original.end=Math.Min(state.duration,state.nativeProject.duration);timeline.Expand(original,true);}}
            if(migrated||separated>0)MarkChanged();await SaveNow(false);SetStatus(separated>0?String.Format(Language.Text("工程已读取 · 已将 {0} 个重叠物件分配到其他轨道，时间与运行顺序保持。"),separated):state.nativeProject==null?"工程已读取 · 空格带音乐播放 · F5 原生试玩 · 右键编辑轨道":"原生工程已读取 · 勾选“原生游戏渲染”执行原 GML 画面；静态画布仅显示资源布局。");
        }
        catch(Exception error){Error(error);}
        finally{Enabled=wasEnabled;SetBusy(wasBusy);}
        await EnsureNativeFilm();
    }
    private string ResourceFilter {get{return search.Text.Trim();}}
    private void FillResources()
    {
        SyncResourceTree();
        if(document==null)return;if(IsPresetKind(resourceKind)){FillPresetResources();return;}presetTools.Visible=false;string[] names=resourceKind=="objects"?document.inventory.objects:resourceKind=="sprites"?document.inventory.sprites:document.inventory.scripts;
        resources.BeginUpdate();resources.Items.Clear();var previous=resources.SmallImageList;var icons=new ImageList{ImageSize=new Size(24,24),ColorDepth=ColorDepth.Depth32Bit};
        foreach(string name in names.Where(n=>n.IndexOf(ResourceFilter,StringComparison.OrdinalIgnoreCase)>=0))
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
    private void UpdateTitle(){RefreshShell();if(state!=null){projectTitle.Text=String.Format(Language.Text("{0} / {1}\n{2} FPS    {3} BPM    {4:0.##} 秒"),String.IsNullOrEmpty(state.name)?document.projectName:state.name,state.room,state.fps,state.bpm,state.duration/(double)state.fps);durationInput.Value=Math.Max(durationInput.Minimum,Math.Min(durationInput.Maximum,(decimal)(state.duration/(double)state.fps)));}}
    private void SetStatus(string text){status.Text=text;RefreshShell();}
    private void Error(Exception error){SetStatus("操作失败："+error.Message);Language.Message(this,error.Message,"RufMaker",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    private void SetFrame(int value)
    {
        if(state==null)return;int previous=frame;frame=templateEditing==null?Math.Max(0,Math.Min(state.duration,value)):Math.Max(templateEditing.start,Math.Min(templateEditing.end,value));stage.ShowFrame(frame);timeline.Frame=frame;loading=true;frameInput.Maximum=state.duration;frameInput.Value=frame;loading=false;
        var time=TimeSpan.FromSeconds(frame/(double)state.fps);timeLabel.Text=String.Format("{0:00}:{1:00}.{2:00}",(int)time.TotalMinutes,time.Seconds,time.Milliseconds/10);rightTime.SetFrame(frame);keyTimeline.Frame=frame;keyTimeline.PlayheadMoved(previous);if(shellTime!=null)shellTime.Text=timeLabel.Text;timeline.PlayheadMoved(previous);inspectorTimer.Stop();inspectorTimer.Start();if(rightTime.Scrubbing){timeline.PresentPlayhead();keyTimeline.PresentPlayhead();}
    }
    private UndoSnapshot CaptureSnapshot()
    {
        return new UndoSnapshot
        {
            state=state,
            room=document==null?null:document.room,
            selectedTrack=selectedTrack==null?null:selectedTrack.id,
            selectedTracks=timeline.SelectedTracks.Select(t=>t.id).ToArray(),
            selectedProperty=timeline.SelectedProperty,
            selectedKey=selectedKey==null?null:new UndoKey{trackId=(timeline.Owner(selectedKey)??selectedTrack)==null?null:(timeline.Owner(selectedKey)??selectedTrack).id,property=selectedKey.property,frame=selectedKey.frame},
            selectedKeys=timeline.SelectedKeys.Select(k=>{var owner=timeline.Owner(k)??selectedTrack;return owner==null?null:new UndoKey{trackId=owner.id,property=k.property,frame=k.frame};}).Where(k=>k!=null).ToArray(),
            selectedInstance=selectedInstance==null?null:selectedInstance.id
        };
    }
    private void PushUndo()
    {
        if(state==null||parameterScrubbing)return;
        var snapshot=new UndoEntry(CaptureSnapshot());
        if(undo.Count>0)
        {
            if(undo.Peek().SameScene(snapshot))
            {
                undo.Pop();
                undo.Push(snapshot);
                return;
            }
        }
        undo.Push(snapshot);
        redo.Clear();
    }
    private void MarkChanged()
    {if(state==null)return;if(selectedKey!=null&&timeline.Owner(selectedKey)==null){selectedKey=null;timeline.ClearKeySelection();QueueInspectorUpdate();}int separated=TimelineLayout.EnsureExclusive(state,(selectedTrack==null?Enumerable.Empty<Track>():new[]{selectedTrack}).Concat(timeline.SelectedTracks));if(separated>0&&selectedTrack!=null)timeline.ActiveLane=selectedTrack.lane;RandomParameters.Attach(state);TimelineLayout.Order(state);foreach(var t in state.tracks.Where(t=>t.kind=="native")){t.start=0;t.end=Math.Min(state.duration,state.nativeProject.duration);t.enabled=true;}dirty=true;revision++;saveTimer.Stop();if(!parameterScrubbing&&!closeInProgress)saveTimer.Start();stage.SceneChanged();RefreshBeatControls();if(!parameterScrubbing){timeline.RefreshLayout();RefreshKeyWorkspace();properties.Refresh();UpdateTitle();rightTime.SetView(state.fps,state.duration,frame,selectedTrack);SetStatus(separated>0?Language.Text("已将重叠物件分配到其他轨道；时间与运行顺序保持。"):"正在同步修改…");}}
    private int ContentEnd(){int tracks=state.tracks.Where(t=>t.kind!="native").Select(t=>Math.Max(t.end,t.keyframes.Count==0?0:t.keyframes.Max(k=>k.frame))).DefaultIfEmpty(0).Max();int native=state.nativeProject==null?0:state.nativeProject.events.Select(e=>e.frame).DefaultIfEmpty(0).Max();return Math.Max(tracks,native);}
    private async Task<bool> SaveNow(bool notify)
    {
        if(state==null||bridge==null)return true;if(notify)CommitInspectorEdits();await saveGate.WaitAsync();saveTimer.Stop();int current=revision;
        try
        {
            var snapshot=state.Snapshot();var result=await Task.Run(()=>bridge.Call<SaveResult>("save",snapshot));ReplaceRoom(result.room);
            if(current==revision)dirty=false;SetStatus("已保存到 GameMaker 工程");if(notify)stage.Focus();return true;
        }
        catch(Exception error){Error(error);return false;}
        finally{saveGate.Release();if(dirty&&!closeInProgress&&!parameterScrubbing){saveTimer.Stop();saveTimer.Start();}}
    }
    private void UpdateSelection()
    {SyncTemplateSelection();stage.SelectedTrack=selectedTrack;stage.SelectedKey=selectedKey;stage.SelectedInstance=selectedInstance;timeline.SelectedTrack=selectedTrack;timeline.SelectedKey=selectedKey;timeline.RefreshLayout();RefreshKeyWorkspace();keySnap.Enabled=state!=null;if(state!=null)rightTime.SetView(state.fps,state.duration,frame,selectedTrack);stage.Invalidate();timeline.Invalidate();}
    private void SelectTrack(Track track,Keyframe key)
    {if(!timeline.NotifyingSelection)timeline.ResetSelection(track,key);selectedTrack=track;if(track!=null&&track.kind!="native")timeline.ActiveLane=track.lane;selectedKey=key;selectedInstance=null;resourceName=null;if(key!=null)timeline.SelectedProperty=key.property;if(track!=null&&!Motion.Fields(track).Contains(timeline.SelectedProperty))timeline.SelectedProperty=Motion.Fields(track).FirstOrDefault()??"x";UpdateSelection();ShowProperties();}
    private void SelectInstance(RoomInstance instance)
    {timeline.ResetSelection(null,null);selectedInstance=instance;selectedTrack=null;selectedKey=null;resourceName=null;UpdateSelection();ShowProperties();}
    private void SelectResource()
    {if(IsPresetKind(resourceKind)){SetStatus(resourceKind=="event-presets"?"拖到时间轴创建事件；双击从播放头添加。":resourceKind=="generation-presets"?"拖到来源物件上，自动循环生成弹幕；双击应用到所选物件。":resourceKind=="presets"?"拖到物件或拉镜轨道应用动作；双击应用到所选轨道。":resourceKind=="custom-presets"?"双击进入分类或添加预设；拖入时间轴按落点新建，右键管理名称、分类与导入导出。":"拖到同类轨道上应用；拖到空白处或双击新建预设轨道。");return;}if(resourceKind=="sprites"&&selectedTrack!=null&&Theme.Visual(selectedTrack.kind)){ApplySprite(resourceName);return;}timeline.ResetSelection(null,null);selectedTrack=null;selectedKey=null;selectedInstance=null;UpdateSelection();ShowProperties();}
    private void SelectProject(){timeline.ResetSelection(null,null);selectedTrack=null;selectedKey=null;selectedInstance=null;resourceName=null;UpdateSelection();ShowProperties();}
    private void Field<T>(NativeProperties bag,string name,string category,Func<T> get,Action<T> set,string[] choices=null,string description="")
    {string property=selectedTrack==null?null:NumericProperty(name);var targets=timeline.SelectedTracks.Count>1?timeline.SelectedTracks.ToArray():new[]{selectedTrack};bag.Add(name,category,typeof(T),()=>property==null?(object)get():Convert.ChangeType(Motion.Value(selectedTrack,property,frame),typeof(T)),value=>{PushUndo();set((T)value);MarkChanged();},choices,description,property==null?null:ParameterControls(targets,property));}
    private static double ClampValue(string property,double value)
    {if(property=="shapeAspect")return Math.Max(-10,Math.Min(10,value));if(property=="shapeInner")return Math.Max(.01,Math.Min(1,value));if(property=="alpha"||property=="fxOpacity"||property=="fxRetention")return Math.Max(0,Math.Min(1,value));if(property=="count")return Math.Max(1,Math.Min(300,Math.Round(value)));if(property=="life")return Math.Max(1,value);if(property=="sides")return Math.Max(3,Math.Min(64,Math.Round(value)));if(property=="cameraZoom")return Math.Max(.05,Math.Min(20,value));if(property=="radius"||property=="length"||property=="strength")return Math.Max(0,value);return value;}
    private string CurrentProperty(bool inspector=false)
    {if(selectedTrack==null)return "x";if(inspector&&properties.SelectedLabel!=null){string label=properties.SelectedLabel;foreach(string p in Motion.Fields(selectedTrack))if(Motion.Name(p)==label)return p;}return selectedKey!=null?selectedKey.property:timeline.SelectedProperty;}
    private void EaseField(NativeProperties bag,Track t,Keyframe key)
    {
        var names=key==null?Motion.EaseNames:new[]{"继承轨道"}.Concat(Motion.EaseNames).ToArray();var values=key==null?Motion.EaseValues:new[]{""}.Concat(Motion.EaseValues).ToArray();
        Field(bag,key==null?"默认缓动":timeline.SelectedKeys.Count>1?"整组缓动":"至下一关键帧的缓动",key==null?"运动":"所选关键帧",()=>names[Math.Max(0,Array.IndexOf(values,key==null?t.easing:key.easing??""))],value=>{string easing=values[Array.IndexOf(names,value)];if(key==null)t.easing=easing;else foreach(var selected in timeline.SelectedKeys)selected.easing=easing;},names);
    }
    private void AnimatedFields(NativeProperties bag,Track track)
    {
        foreach(string item in Motion.Fields(track).Where(p=>!MotionPresets.Generated(track,p)&&!Rainbow.Parameters.Contains(p)&&(p!="life"||BulletLifetime.Mode(track)=="fixed")))
        {string property=item;bag.Add(Motion.Name(property),ParameterCategory(track,property),typeof(double),()=>Motion.Value(track,property,frame),value=>{PushUndo();Motion.EditValue(track,property,frame,ClampValue(property,(double)value));timeline.SelectedProperty=property;MarkChanged();},null,"◇ 开启属性关键帧；◆ 关闭并保留当前值。随机开关支持 random、random_range、choose，结果保持到重新随机。",ParameterControls(new[]{track},property));}
    }
    private bool inspectorUpdatePending;
    private void QueueInspectorUpdate(){if(!IsHandleCreated||IsDisposed||inspectorUpdatePending)return;inspectorUpdatePending=true;BeginInvoke(new Action(()=>{inspectorUpdatePending=false;if(!IsDisposed)ShowProperties();}));}
    private void ShowProperties()
    {
        if(state==null)return;var bag=new NativeProperties();codeButton.Enabled=false;keyButton.Enabled=false;deleteButton.Enabled=false;
        if(selectedInstance!=null)
        {
            var instance=selectedInstance;selectionTitle.Text=Language.Text("房间实例 · ")+instance.name;
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
            var track=selectedTrack;selectionTitle.Text=timeline.SelectedKeys.Count>1?Language.Text("已选择 ")+timeline.SelectedKeys.Count+Language.Text(" 个关键帧"):Language.Text(EventPresets.IsEvent(track)?"事件":Theme.KindName(track.kind))+" · "+track.name;
            Field(bag,"名称","物件",()=>track.name,value=>track.name=value);Field(bag,"启用","物件",()=>track.enabled,value=>track.enabled=value);
            var displayLanes=track.templateOnly?TimelineLayout.TemplateDisplayLanes(state,track):new int[0];
            if(displayLanes.Length>0)bag.Add("所在生成组轨道","物件",typeof(string),()=>String.Join(", ",displayLanes.Select(l=>(l+1).ToString())),null);
            else Field(bag,"轨道编号","物件",()=>track.lane+1,value=>{track.lane=Math.Max(0,Math.Min(199,value-1));state.laneCount=Math.Max(state.laneCount,track.lane+1);});
            if(track.kind=="pattern")bag.Add("结束帧（按循环计算）","物件",typeof(int),()=>track.end,null);
            if(track.kind=="rod"){
                string[] shapes=FormationShapes.Names,shapeValues=FormationShapes.Values;
                Field(bag,"种类","整体对象",()=>shapes[Math.Max(0,Array.IndexOf(shapeValues,track.shape))],value=>{bool autoName=shapes.Contains(track.name);track.shape=shapeValues[Array.IndexOf(shapes,value)];if(autoName)track.name=value;QueueInspectorUpdate();},shapes);
            }
            if(Theme.Visual(track.kind))
            {
                keyButton.Enabled=true;Field(bag,"贴图","外观",()=>track.sprite,value=>{Rainbow.SetSprite(track,value);QueueInspectorUpdate();},document.inventory.sprites);
                if(track.rainbow)bag.Add("颜色（彩虹固定白色）","外观",typeof(Color),()=>Color.White,null);
                else Field(bag,"颜色","外观",()=>Motion.ColorOf(track.color),value=>track.color=String.Format("#{0:X2}{1:X2}{2:X2}",value.R,value.G,value.B));
                RainbowFields(bag,new[]{track});
                if(track.kind!="object")Field(bag,"贴图跟随发射 / 棒角度","外观",()=>track.rotateSprite,value=>track.rotateSprite=value,null,"默认关闭。弹幕发射方向、转转棒方向与贴图角度分别控制。");
                TrailFields(bag,new[]{track});
                EaseField(bag,track,null);
                Field(bag,"Power 指数","运动",()=>track.easePower,value=>track.easePower=Math.Max(.1,value));Field(bag,"ExpSin 周期","运动",()=>track.easePeriod,value=>track.easePeriod=Math.Max(.01,value));
                MotionControls(bag,track);
                AnimatedFields(bag,track);
                keyButton.Text="＋ "+Motion.Name(CurrentProperty())+" 关键帧  (K)";
            }
            if(track.kind=="emitter")
            {
                Field(bag,"发射间隔","发射器",()=>track.interval,value=>track.interval=Math.Max(1,value));FlightFields(bag,track);
            }
            if(track.kind=="rod")
            {
                if(track.shape=="rod"){
                string[] names={"中心 + 角度 + 长度","自身为端点1 + 手动端点2","手动端点1 + 手动端点2"},values={"center","anchor","endpoints"};
                Field(bag,"端点模式","转转棒",()=>names[Math.Max(0,Array.IndexOf(values,track.rodMode))],value=>track.rodMode=values[Array.IndexOf(names,value)],names);
                Field(bag,"循环滚动","转转棒",()=>track.rolling,value=>track.rolling=value,null,"关闭时含两个端点等距摆放；开启时保持数量，在半开线段内循环移动。滚动速度单位为像素/帧。");
                }else {Field(bag,"沿轮廓滚动","整体对象",()=>track.rolling,value=>track.rolling=value,null,"按滚动速度沿闭合轮廓移动，可叠加自转。单位为像素/帧。");bag.Add("排列方式","整体对象",typeof(string),()=>"沿闭合轮廓参数排列；角度控制沿线相位，轮廓朝向控制整体旋转。星星/花朵使用边数设置尖角/花瓣数。",null);}
            }
            VariableFields(bag,track);
            if(track.kind=="camera")
            {
                keyButton.Enabled=true;keyButton.Text="＋ "+Motion.Name(CurrentProperty())+" 关键帧  (K)";Field(bag,"镜像补边","拉镜",()=>track.cameraMirror,value=>track.cameraMirror=value,null,"在渲染拉镜时镜像延伸房间边缘，避免甩镜或旋转时露出黑边。");EaseField(bag,track,null);
                Field(bag,"Power 指数","运动",()=>track.easePower,value=>track.easePower=Math.Max(.1,value));Field(bag,"ExpSin 周期","运动",()=>track.easePeriod,value=>track.easePeriod=Math.Max(.01,value));MotionControls(bag,track);AnimatedFields(bag,track);
                bag.Add("说明","拉镜",typeof(string),()=>"X/Y 为镜头中心；缩放大于 1 拉近。时段外恢复房间视图；重叠时下方轨道优先。",null);
            }
            if(track.kind=="effect")
            {
                keyButton.Enabled=true;keyButton.Text="＋ "+Motion.Name(CurrentProperty())+" 关键帧  (K)";
                Field(bag,"效果","特效",()=>Effects.Name(track.effect),value=>{bool autoName=track.name==Effects.Name(track.effect)||track.name=="闪屏";track.effect=Effects.Values[Array.IndexOf(Effects.Names,value)];if(autoName)track.name=Effects.Name(track.effect);QueueInspectorUpdate();},Effects.Names);
                Field(bag,"遮罩 / 闪屏颜色","特效",()=>Motion.ColorOf(track.color),value=>track.color=String.Format("#{0:X2}{1:X2}{2:X2}",value.R,value.G,value.B));
                if(track.effect=="shake")Field(bag,"震动频率","特效",()=>track.fxFrequency,value=>track.fxFrequency=Math.Max(.01,value));
                if(track.effect=="blur"||track.effect=="motion-blur")Field(bag,"模糊采样数","特效",()=>track.fxSamples,value=>track.fxSamples=Math.Max(2,Math.Min(16,value)));
                if(track.effect=="glitch"){Field(bag,"错位切片数量","特效",()=>track.fxSlices,value=>track.fxSlices=Math.Max(1,Math.Min(32,value)));Field(bag,"顿帧间隔 / 帧","特效",()=>track.fxStutter,value=>track.fxStutter=Math.Max(1,Math.Min(12,value)));}
                EaseField(bag,track,null);AnimatedFields(bag,track);
                bag.Add("提示","特效",typeof(string),()=>track.effect=="rgb-impact"?"效果透明度控制整套冲击；缩放、色散、残像和闪光可分别调整。":track.effect=="glitch"?"强度控制错位距离；透明度控制整套故障。顿帧只影响画面，游戏逻辑继续执行。":"强度：像素；暗角：0–100。方向：角度。",null);
            }
            if(track.kind=="script"){bag.Add("触发帧","GML",typeof(int),()=>track.start,null);codeButton.Enabled=true;}
            BulletFields(bag,track);
            deleteButton.Enabled=true;
        }
        else if(resourceName!=null)
        {
            string name=resourceName;selectionTitle.Text=Language.Text("资源 · ")+name;bag.Add("名称","资源",typeof(string),()=>name,null);bag.Add("类型","资源",typeof(string),()=>resourceKind,null);codeButton.Enabled=resourceKind!="sprites";
        }
        else
        {
            selectionTitle.Text=Language.Text("属性检查器");bag.Add("操作","编辑",typeof(string),()=>"选中画布或时间轴物件以编辑参数。工程设置请点击顶栏按钮。",null);
        }
        properties.SelectedObject=bag;RefreshKeyWorkspace();RefreshBehavior();RefreshShell();
    }
    private void ShowGroupProperties(NativeProperties bag)
    {
        var group=timeline.SelectedTracks.ToArray();var primary=selectedTrack;
        selectionTitle.Text=Language.Text("已选择 ")+group.Length+Language.Text(" 个物件");
        bag.Add("选中数量","批量编辑",typeof(int),()=>group.Length,null);
        Field(bag,"移到轨道编号","批量时间",()=>primary.lane+1,value=>{int lane=Math.Max(0,Math.Min(199,value-1));foreach(var t in group)t.lane=lane;state.laneCount=Math.Max(state.laneCount,lane+1);timeline.ActiveLane=lane;});
        Field(bag,"主物件开始帧 · 整组平移","批量时间",()=>primary.start,value=>timeline.MoveTracks(value-primary.start),null,"所有物件及关键帧平移相同帧数，保留相对时间。");
        Field(bag,"开始边界 · 整组拉伸","批量时间",()=>primary.start,value=>timeline.TrimTracks(true,value-primary.start),null,"仅改变各物件开始边界，关键帧不移动。");
        Field(bag,"结束边界 · 整组拉伸","批量时间",()=>primary.end,value=>timeline.TrimTracks(false,value-primary.end));
        Field(bag,"主物件长度 / 帧","批量时间",()=>primary.end-primary.start,value=>timeline.TrimTracks(false,value-(primary.end-primary.start)));
        Field(bag,"启用全部","批量编辑",()=>group.All(t=>t.enabled),value=>{foreach(var t in group)t.enabled=value;});
        if(group.All(t=>Theme.Spatial(t.kind)))
        {
            foreach(string property in new[]{"x","y"})
            {string p=property;Field(bag,"整组 "+p.ToUpper()+" · 主物件","批量位置",()=>Motion.Value(primary,p,frame),value=>{double delta=value-Motion.Value(primary,p,frame);foreach(var t in group){if(MotionPresets.Bound(t))MotionPresets.Translate(t,p=="x"?delta:0,p=="y"?delta:0);else Motion.EditValue(t,p,frame,Motion.Value(t,p,frame)+delta);}});}
            if(group.All(t=>Theme.Visual(t.kind))){
            foreach(string property in new[]{"scale","alpha"})
            {string p=property;Field(bag,"统一"+Motion.Name(p),"批量外观",()=>Motion.Value(primary,p,frame),value=>{foreach(var t in group)Motion.EditValue(t,p,frame,ClampValue(p,value));});}
            Field(bag,"统一贴图","批量外观",()=>primary.sprite,value=>{foreach(var t in group)Rainbow.SetSprite(t,value);QueueInspectorUpdate();},document.inventory.sprites);
            RainbowFields(bag,group);TrailFields(bag,group);}
        }
        bag.Add("操作提示","批量编辑",typeof(string),()=>"拖长条平移，拖边缘改长度；Delete 批量删除。",null);
    }
    private void RainbowFields(NativeProperties bag,Track[] group)
    {
        if(!group.All(Rainbow.IsCherry))return;var t=group[0];
        Field(bag,"彩虹","彩虹 Cherry",()=>group.All(track=>track.rainbow),value=>{foreach(var track in group)Rainbow.Enable(track,value);QueueInspectorUpdate();},null,"使用 sprrainbowcherry，固定白色与 image_speed=0。中心转转棒按离中心的距离分配颜色。");
        if(!group.All(track=>track.rainbow))return;
        if(group.All(track=>track.kind=="rod"))Field(bag,"彩虹分配方式","彩虹 Cherry",()=>Rainbow.ModeNames[Array.IndexOf(Rainbow.Modes,Rainbow.Mode(t))],value=>{foreach(var track in group)track.rainbowMode=Rainbow.Modes[Array.IndexOf(Rainbow.ModeNames,value)];QueueInspectorUpdate();},Rainbow.ModeNames,"中心使用整体对象的 X/Y。距离模式每像素推进一个颜色帧；角度模式绕中心一圈为一轮彩虹。颜色密度控制重复次数，支持反转和动态色彩。");
        Field(bag,"反转","彩虹 Cherry",()=>t.rainbowReverse,value=>{foreach(var track in group)track.rainbowReverse=value;});
        Field(bag,"动态色彩","彩虹 Cherry",()=>t.rainbowDynamic,value=>{foreach(var track in group)track.rainbowDynamic=value;});
        foreach(string item in Rainbow.Parameters)
        {
            string property=item;
            bag.Add(Motion.Name(property),"彩虹 Cherry",typeof(double),()=>Motion.Value(t,property,frame),value=>{PushUndo();foreach(var track in group)Motion.EditValue(track,property,frame,property=="rainbowDensity"?Math.Max(0,(double)value):(double)value);timeline.SelectedProperty=property;MarkChanged();},null,property=="rainbowDensity"?(t.kind=="rod"&&Rainbow.Mode(t)!="sequence"?"距离模式：每像素跨过的颜色帧。角度模式：绕中心一圈重复彩虹的次数。0 表示同色；支持独立关键帧。":"相邻序号间跨过多少颜色帧。1 表示逐帧递增；0 表示同色。支持独立关键帧。"):"支持独立关键帧。动态开启后，按时间更新 image_index，image_speed 仍保持 0。",ParameterControls(group,property));
        }
    }
    private void TrailFields(NativeProperties bag,Track[] group)
    {
        var t=group[0];Action<Action<Track>> apply=change=>{foreach(var track in group)change(track);};
        Field(bag,"拖影样式","拖影",()=>Trails.Names[Math.Max(0,Array.IndexOf(Trails.Values,Trails.Mode(t)))],value=>{apply(track=>Trails.SetMode(track,Trails.Values[Array.IndexOf(Trails.Names,value)]));QueueInspectorUpdate();},Trails.Names);
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
        if(state==null)return;CommitInspectorEdits();PushUndo();var track=new Track{id=Guid.NewGuid().ToString("N"),kind=kind,name=kind=="camera"?"拉镜":kind=="rod"?"转转棒":kind=="emitter"?"新弹幕":kind=="object"?"新对象":kind=="effect"?"闪屏":"GML 事件",start=Math.Min(frame,state.duration-1)};
        track.end=Math.Min(state.duration,track.start+(kind=="emitter"?300:kind=="object"?200:10));
        if(kind=="emitter"||kind=="object"||kind=="rod"){track.sprite=kind=="emitter"?"sprBullet":"sprCherry";if(!document.inventory.sprites.Contains(track.sprite))track.sprite=document.inventory.sprites.First();if(kind=="rod"){track.count=5;track.angularSpeed=1;track.end=Math.Min(state.duration,track.start+500);}}
        if(kind=="camera"){track.x=document.room.width/2.0;track.y=document.room.height/2.0;track.end=Math.Min(state.duration,track.start+500);}
        if(kind=="script")track.code="// 在开始帧执行的 GameMaker GML\r\n";
        track.lane=timeline.ActiveLane>=0?timeline.ActiveLane:selectedTrack!=null&&selectedTrack.kind!="native"?selectedTrack.lane:Math.Max(0,state.laneCount-1);if(configure!=null)configure(track);state.laneCount=Math.Max(state.laneCount,track.lane+1);state.tracks.Add(track);SelectTrack(track,null);timeline.Expand(track,true);MarkChanged();
    }
    private void AddKey()
    {
        AddPropertyKey(CurrentProperty());
    }
    private void AddPropertyKey(string property)
    {
        if(selectedTrack==null||!Motion.Fields(selectedTrack).Contains(property))return;var track=selectedTrack;
        if(MotionPresets.Generated(track,property)){SetStatus("位置由动作预设逐帧生成；请修改预设参数，或先切换为手动关键帧。");return;}
        var existing=track.keyframes.FirstOrDefault(k=>k.frame==frame&&k.property==property);if(existing!=null){SelectTrack(track,existing);timeline.Expand(track,true);return;}
        PushUndo();string formula;track.expressions.TryGetValue(property,out formula);bool first=!Motion.HasKeys(track,property);Motion.SetKey(track,property,frame,Motion.Value(track,property,frame));if(first&&!String.IsNullOrWhiteSpace(formula)){var created=track.keyframes.First(k=>k.property==property&&k.frame==frame);created.expression=formula;created.randomId="base";track.expressions.Remove(property);}SelectTrack(track,track.keyframes.First(k=>k.frame==frame&&k.property==property));timeline.Expand(track,true);MarkChanged();
    }
    private void RemoveSelection()
    {
        if(selectedInstance!=null){DeleteInstance();return;}if(selectedTrack==null||selectedTrack.kind=="native")return;if(timeline.SelectedKeys.Count==0){var dropping=WithGroupChildren(timeline.SelectedTracks.Count>0?timeline.SelectedTracks.ToArray():new[]{selectedTrack}).ToArray();var remaining=state.tracks.Except(dropping).ToArray();var missing=new HashSet<string>(GlobalVariables.Declarations(dropping).Select(row=>row.variableName).Except(GlobalVariables.Declarations(remaining).Select(row=>row.variableName)));string reference=remaining.SelectMany(GlobalVariables.References).FirstOrDefault(missing.Contains);if(reference!=null){SetStatus("变量仍被引用，请先修改相关公式：global."+reference);return;}}PushUndo();if(timeline.SelectedKeys.Count>0){foreach(var track in state.tracks)track.keyframes.RemoveAll(k=>timeline.SelectedKeys.Contains(k));timeline.ClearKeySelection();SelectTrack(selectedTrack,null);}else{var remove=WithGroupChildren(timeline.SelectedTracks.Count>0?timeline.SelectedTracks.ToArray():new[]{selectedTrack});state.tracks.RemoveAll(t=>t.kind!="native"&&remove.Contains(t));SelectProject();}MarkChanged();
    }
    private async void DeleteInstance()
    {if(selectedInstance==null||selectedInstance.name=="objRufController")return;PushUndo();try{var room=await bridge.Call<RoomData>("delete-instance",new{room=state.room,id=selectedInstance.id});selectedInstance=null;ReplaceRoom(room);SelectProject();SetStatus("房间实例已删除 · Ctrl+Z 可撤销");}catch(Exception error){Error(error);}}
    private void ReplaceRoom(RoomData room)
    {if(document.room!=null&&stage.Room==document.room&&document.room.SameContent(room))return;string id=selectedInstance==null?null:selectedInstance.id;document.room=room;stage.Room=room;if(id!=null)selectedInstance=room.instances.FirstOrDefault(x=>x.id==id);stage.SelectedInstance=selectedInstance;stage.SceneChanged();}
    private void ApplySprite(string name){if(selectedTrack==null||!Theme.Visual(selectedTrack.kind))return;PushUndo();foreach(var t in timeline.SelectedTracks.Where(t=>Theme.Visual(t.kind)))Rainbow.SetSprite(t,name);ShowProperties();MarkChanged();}
    private async Task DropResource(string kind,string name,PointF position)
    {
        try
        {
            if(IsPresetKind(kind)){await UsePreset(kind,name,stage.TrackAt(position),frame);return;}
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
        {var track=selectedTrack;using(var editor=new CodeForm(track.name,track.code,async text=>{PushUndo();track.code=text;MarkChanged();await SaveNow(false);}))Language.ShowDialog(editor,this);}
        else if(selectedInstance!=null)await EditResource("objects",selectedInstance.name);
        else if(resourceName!=null)await EditResource(resourceKind,resourceName);
    }
    private async Task EditResource(string kind,string name)
    {
        try
        {var source=await bridge.Call<SourceData>("source-read",new{kind=kind,name=name});using(var editor=new CodeForm(name+".gml",source.content,async text=>{await bridge.Call<object>("source-save",new{kind=kind,name=name,content=text});SetStatus("GML 已写入 GameMaker 工程");}))Language.ShowDialog(editor,this);}
        catch(Exception error){Error(error);}
    }
    private async Task ImportAssets()
    {
        if(document==null)return;
        using(var dialog=new OpenFileDialog{Title="从素材包导入 PNG 贴图",InitialDirectory=document.pack,Filter="PNG 贴图|*.png",Multiselect=true})
        {
            if(Language.ShowDialog(dialog,this)!=DialogResult.OK)return;
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
    private void SetBusy(bool value){busy=value;runButton.Enabled=!value;exportButton.Enabled=!value;UseWaitCursor=value;RefreshShell();}
    private async Task RunGame()
    {
        if(state==null||busy)return;CommitInspectorEdits();if(templateEditing!=null)ExitTemplateContext();if(!await EnsureEngine())return;StopPlaying();saveTimer.Stop();SetBusy(true);SetStatus("正在同步、编译并进入原生试玩…");int target=frame,current=revision;
        try{
            var payload=new{state=Json.Read<EditorState>(Json.Write(state)),frame=target};BuildResult result;
            if(state.nativeProject!=null){using(var dialog=new NativeReplayProgressForm(bridge,payload,target)){Language.ShowDialog(dialog,this);if(dialog.Cancelled){SetStatus("原生试玩重放已取消。");return;}if(dialog.ErrorText!=null)throw new IOException(dialog.ErrorText);result=dialog.Result;}}
            else result=await bridge.Call<BuildResult>("run",payload);
            if(current==revision)dirty=false;SetStatus(result.ready?"原生试玩已进入 "+result.status["room"]+" · 第 "+result.status["frame"]+" 帧":"游戏已启动，请查看游戏窗口");if(state.nativeProject!=null){nativePhase="";nativeWatch.Start();}
            // A modal progress window reactivates its editor owner when it closes.
            // Transfer input after that modal loop and the busy state have ended.
            WatchPlayerTrace(result);int gamePid=result.pid;BeginInvoke(new Action(()=>AppWindow.FocusGame(gamePid)));
        }
        catch(Exception error){Error(error);}finally{SetBusy(false);}
    }
    private async Task ExportGame()
    {
        if(state==null||busy)return;CommitInspectorEdits();if(!await EnsureEngine())return;StopPlaying();saveTimer.Stop();SetBusy(true);SetStatus("GameMaker 正在编译并打包…");int current=revision;
        try{var result=await bridge.Call<BuildResult>("export",Json.Read<EditorState>(Json.Write(state)));if(current==revision)dirty=false;SetStatus("游戏已导出："+result.exe);Process.Start(new ProcessStartInfo("explorer.exe","\""+result.dir+"\""){UseShellExecute=true});}
        catch(Exception error){Error(error);}finally{SetBusy(false);}
    }
    private void ResolveUndoTracks(UndoSnapshot source,out Track primary,out List<Track> group)
    {
        var ids=(source.selectedTracks??new string[0]).ToList();
        if(!String.IsNullOrEmpty(source.selectedTrack)&&!ids.Contains(source.selectedTrack))ids.Insert(0,source.selectedTrack);
        group=ids.Select(id=>state.tracks.FirstOrDefault(t=>t.id==id)).Where(t=>t!=null&&t.kind!="native").Distinct().ToList();
        primary=String.IsNullOrEmpty(source.selectedTrack)?group.FirstOrDefault():state.tracks.FirstOrDefault(t=>t.id==source.selectedTrack)??group.FirstOrDefault();
    }
    private void ResolveUndoKeys(UndoSnapshot source,out Keyframe primaryKey,out List<Keyframe> keys)
    {
        Func<UndoKey,Keyframe> find=k=>{if(k==null||String.IsNullOrEmpty(k.trackId))return null;var owner=state.tracks.FirstOrDefault(t=>t.id==k.trackId);return owner==null?null:owner.keyframes.FirstOrDefault(item=>item.property==k.property&&item.frame==k.frame);};
        keys=(source.selectedKeys??new UndoKey[0]).Select(find).Where(k=>k!=null).Distinct().ToList();
        primaryKey=find(source.selectedKey)??keys.FirstOrDefault();
        if(primaryKey!=null&&!keys.Contains(primaryKey))keys.Insert(0,primaryKey);
    }
    private void RestoreUndoSelection(UndoSnapshot current,UndoSnapshot restored)
    {
        Track primary;List<Track> group;ResolveUndoTracks(current,out primary,out group);
        bool usedRestoredTracks=false;
        if(primary==null&&group.Count==0){ResolveUndoTracks(restored,out primary,out group);usedRestoredTracks=true;}
        string prop=!String.IsNullOrEmpty(current.selectedProperty)&&primary!=null&&Motion.Fields(primary).Contains(current.selectedProperty)?current.selectedProperty:!String.IsNullOrEmpty(restored.selectedProperty)&&primary!=null&&Motion.Fields(primary).Contains(restored.selectedProperty)?restored.selectedProperty:null;
        if(!String.IsNullOrEmpty(prop))timeline.SelectedProperty=prop;
        Keyframe primaryKey=null;var keys=new List<Keyframe>();
        bool hadCurrentKeys=current.selectedKey!=null||(current.selectedKeys!=null&&current.selectedKeys.Length>0);
        bool hadRestoredKeys=restored.selectedKey!=null||(restored.selectedKeys!=null&&restored.selectedKeys.Length>0);
        if(hadCurrentKeys||usedRestoredTracks)
        {
            ResolveUndoKeys(current,out primaryKey,out keys);
            if(primaryKey==null&&keys.Count==0)ResolveUndoKeys(restored,out primaryKey,out keys);
        }
        else if(hadRestoredKeys&&primary!=null&&restored.selectedTrack==primary.id)
        {
            Keyframe restoredPrimaryKey;List<Keyframe> restoredKeys;ResolveUndoKeys(restored,out restoredPrimaryKey,out restoredKeys);
            if(restoredKeys.Any(rk=>current.state==null||!current.state.tracks.Where(ct=>ct.id==primary.id).SelectMany(ct=>ct.keyframes).Any(ck=>ck.property==rk.property&&ck.frame==rk.frame)))
            {primaryKey=restoredPrimaryKey;keys=restoredKeys;}
        }
        if(primaryKey!=null||keys.Count>0)
        {
            var owner=timeline.Owner(primaryKey)??primary;
            timeline.SetSelection(keys,owner,primaryKey);
        }
        else if(group.Count>1)timeline.SetTrackSelection(group,primary);
        else if(primary!=null)SelectTrack(primary,null);
        else
        {
            string instanceId=!String.IsNullOrEmpty(current.selectedInstance)?current.selectedInstance:restored.selectedInstance;
            var instance=String.IsNullOrEmpty(instanceId)||document==null||document.room==null?null:document.room.instances.FirstOrDefault(i=>i.id==instanceId);
            if(instance!=null)SelectInstance(instance);
            else if(resourceName!=null){UpdateSelection();ShowProperties();}
            else SelectProject();
        }
    }
    private async void Undo(bool forward)
    {
        var from=forward?redo:undo;var to=forward?undo:redo;if(state==null||from.Count==0||busy)return;StopPlaying();saveTimer.Stop();
        var current=CaptureSnapshot();to.Push(new UndoEntry(current));var snapshot=from.Pop().Restore();
        try
        {
            string previousMusic=state.music;var previousRoom=document==null?null:document.room;
            state=snapshot.state;Motion.Normalize(state);if(document!=null)document.state=state;stage.State=state;timeline.State=state;
            if(snapshot.room!=null&&document!=null)ReplaceRoom(snapshot.room);
            RefreshBeatControls();SetFrame(frame);RestoreUndoSelection(current,snapshot);MarkChanged();
            if(document!=null&&previousMusic!=state.music)await LoadCurrentMusic();
            if(bridge!=null&&snapshot.room!=null&&(previousRoom==null||!previousRoom.SameContent(snapshot.room))){ReplaceRoom(await bridge.Call<RoomData>("restore-room",snapshot.room));RestoreUndoSelection(current,snapshot);}
        }
        catch(Exception error){Error(error);}
    }
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData)
    {
        bool editing=behaviorProperties.EditingText||keyProperties.EditingText||beatBar!=null&&beatBar.ContainsFocus||search.ContainsFocus||properties.EditingText||rightTime.ContainsFocus||durationInput.ContainsFocus||frameInput.ContainsFocus||previewSpeed.ContainsFocus||languageChoice.ContainsFocus;
        if((keyData==(Keys.Control|Keys.Z)||keyData==(Keys.Control|Keys.Y))&&(!editing||properties.ContainsFocus||keyProperties.ContainsFocus||behaviorProperties.ContainsFocus))
        {
            if(keyData==(Keys.Control|Keys.Z)&&(properties.CancelUncommittedEdit()||keyProperties.CancelUncommittedEdit()||behaviorProperties.CancelUncommittedEdit()))return true;
            Undo(keyData==(Keys.Control|Keys.Y));
            return true;
        }
        if(!editing&&timeline.Focused&&timeline.KeyboardPreviewNavigation&&(keyData==Keys.Left||keyData==Keys.Right)){BeginTimelineArrow(keyData);return true;}
        if(keyData==(Keys.Control|Keys.N)){NewFromKeyboard();return true;}if(keyData==(Keys.Control|Keys.O)){OpenFromKeyboard();return true;}
        if(keyData==(Keys.Control|Keys.S)){stage.Focus();SaveFromKeyboard();return true;}
        if(keyData==Keys.F5){stage.Focus();RunFromKeyboard();return true;}
        if(!editing)
        {
            if(stage.ContainsFocus&&keyData==(Keys.Control|Keys.D0)){stage.ResetView();return true;}
            if(keyData==Keys.Space||keyData==Keys.P){TogglePlaying();return true;}if(keyData==Keys.K){AddKey();return true;}if(keyData==(Keys.Control|Keys.K)){AddAllKeys();return true;}if(keyData==Keys.Delete){RemoveSelection();return true;}
            if(keyData==(Keys.Control|Keys.D)){DuplicateTrack();return true;}if(keyData==(Keys.Control|Keys.C)){CopySelection();return true;}if(keyData==(Keys.Control|Keys.V)){PasteFromKeyboard();return true;}
            if(keyData==(Keys.Control|Keys.A)){if(keyTimeline.ContainsFocus&&selectedTrack!=null){var keys=selectedTrack.keyframes.Where(k=>k.frame>=selectedTrack.start&&k.frame<=selectedTrack.end&&!MotionPresets.Generated(selectedTrack,k.property)).ToArray();timeline.SetSelection(keys,selectedTrack,keys.FirstOrDefault());}else if(timeline.ContainsFocus&&timeline.SelectedKeys.Count==0)timeline.SetTrackSelection(state.tracks);else timeline.SelectAllKeys(selectedTrack);return true;}
        }
        return base.ProcessCmdKey(ref msg,keyData);
    }
    private async void SaveFromKeyboard(){await SaveNow(true);}
    private async void RunFromKeyboard(){await RunGame();}
    private async void OnClosing(object sender,FormClosingEventArgs e)
    {
        if(closing)return;e.Cancel=true;if(closeInProgress)return;if(busy){SetStatus("正在编译，请等待完成后关闭");return;}
        closeInProgress=true;StopPlaying();saveTimer.Stop();
        try{CommitInspectorEdits();stage.Focus();SaveWorkspaceView();Enabled=false;if(await SaveNow(false)){closing=true;BeginInvoke(new Action(Close));}}
        catch(Exception error){Error(error);}
        finally{if(!closing){closeInProgress=false;Enabled=true;if(dirty)saveTimer.Start();}}
    }
}

internal sealed class CodeForm : LocalizedForm
{
    private readonly TextBox code=new TextBox();private readonly Button save=new Button();private readonly Func<string,Task> persist;private readonly string newline;
    public CodeForm(string name,string content,Func<string,Task> persist,int line=1)
    {
        this.persist=persist;newline=CodeText.NewlineOf(content);Text="GML · "+name;Size=new Size(920,680);MinimumSize=new Size(600,400);StartPosition=FormStartPosition.CenterParent;BackColor=Theme.Back;ForeColor=Theme.Text;Font=Theme.Font;Icon=AppWindow.LoadIcon();ShowInTaskbar=false;
        var foot=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=48,FlowDirection=FlowDirection.RightToLeft};save.Text="保存 GML";save.Width=120;save.Height=32;save.Margin=new Padding(8);save.BackColor=Theme.Accent;save.ForeColor=Theme.Back;save.FlatStyle=FlatStyle.Flat;save.Click+=async(s,e)=>await Save();foot.Controls.Add(save);
        var label=new Label{Text="直接编辑 GameMaker GML    ·    Ctrl+S 保存",Dock=DockStyle.Top,Height=34,Padding=new Padding(12,0,0,0),TextAlign=ContentAlignment.MiddleLeft,ForeColor=Theme.Muted};
        var wrap=new CheckBox{Text="自动折行",Checked=true,AutoSize=true,Margin=new Padding(8,15,12,0)};wrap.CheckedChanged+=(s,e)=>{code.WordWrap=wrap.Checked;code.ScrollBars=wrap.Checked?ScrollBars.Vertical:ScrollBars.Both;};foot.Controls.Add(wrap);
        code.Multiline=true;code.AcceptsTab=true;code.AcceptsReturn=true;code.WordWrap=true;code.ScrollBars=ScrollBars.Vertical;code.Dock=DockStyle.Fill;code.Font=new Font("Consolas",11);code.BackColor=Theme.Panel;code.ForeColor=Theme.Text;code.BorderStyle=BorderStyle.None;code.Text=CodeText.ForDisplay(content);
        Controls.Add(code);Controls.Add(label);Controls.Add(foot);
        Shown+=(s,e)=>{code.SelectionStart=CodeText.LineOffset(code.Text,line);code.SelectionLength=0;code.ScrollToCaret();code.Focus();};
    }
    private async Task Save(){save.Enabled=false;try{await persist(CodeText.ForSource(code.Text,newline));DialogResult=DialogResult.OK;Close();}catch(Exception error){Language.Message(this,error.Message,"GML 保存失败",MessageBoxButtons.OK,MessageBoxIcon.Error);}finally{save.Enabled=true;}}
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData){if(keyData==(Keys.Control|Keys.S)){SaveFromKeyboard();return true;}return base.ProcessCmdKey(ref msg,keyData);}
    private async void SaveFromKeyboard(){await Save();}
}
