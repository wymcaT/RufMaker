using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

// UI-only projection: one row for every source Track. Never rewrites lanes or objects.
internal static class StudioOutline
{
    public static Track Parent(EditorState state,Track track)
    {
        var p=TimelineRelations.Resolve(state,track.parentId);if(p==null||p==track)return null;
        var seen=new HashSet<Track>{track};for(var t=p;t!=null;t=TimelineRelations.Resolve(state,t.parentId))if(!seen.Add(t))return null;
        return p;
    }
    public static Track[] Rows(EditorState state,string query)
    {
        if(state==null)return new Track[0];query=(query??"").Trim();if(query.Length==0)return state.tracks.ToArray();
        var keep=new HashSet<Track>();foreach(var t in state.tracks.Where(t=>((t.name??"")+" "+t.id+" "+Theme.KindName(t.kind)).IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0))
        {keep.Add(t);for(var p=Parent(state,t);p!=null&&keep.Add(p);p=Parent(state,p)){} }
        return state.tracks.Where(keep.Contains).ToArray();
    }
}
internal sealed partial class MainForm
{
    private TreeView studioOutline;private TextBox outlineSearch;private Label outlineCount,relationSummary;private ToolStripButton outlineTab,libraryTab,relationButton;private Panel outlinePanel,libraryPanel;private ToolStripLabel studioCounts,studioPreviewInfo;private bool updatingOutline;private string outlineSignature;
    private readonly Dictionary<Track,TreeNode> outlineNodes=new Dictionary<Track,TreeNode>();
    private Label studioGuideText;
    private Control BuildStudioGuide()
    {
        var panel=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Padding=new Padding(10),BackColor=Theme.Panel};
        panel.RowStyles.Add(new RowStyle(SizeType.Percent,100));for(int i=0;i<3;i++)panel.RowStyles.Add(new RowStyle(SizeType.Absolute,38));panel.RowStyles.Add(new RowStyle(SizeType.Absolute,36));
        studioGuideText=new Label{Dock=DockStyle.Fill,ForeColor=Theme.Text,AutoEllipsis=true,Font=Theme.Font};panel.Controls.Add(studioGuideText,0,0);
        var a=Button("位置与外观",()=>{inspectorTabs.SelectedIndex=0;},false,250);a.Dock=DockStyle.Fill;panel.Controls.Add(a,0,1);
        var b=Button("编辑行为",()=>{inspectorTabs.SelectedIndex=1;RefreshBehavior();},false,250);b.Dock=DockStyle.Fill;panel.Controls.Add(b,0,2);
        var c=Button("编辑关键帧",()=>{inspectorDock.Activate("keys");},false,250);c.Dock=DockStyle.Fill;panel.Controls.Add(c,0,3);
        panel.Controls.Add(new Label{Dock=DockStyle.Fill,Text="修改后看预览 · Ctrl+Z 撤销 · Ctrl+S 保存",ForeColor=Theme.Muted,Font=Theme.Small,TextAlign=ContentAlignment.MiddleLeft},0,4);return panel;
    }
    private void RefreshStudioGuide()
    {
        if(studioGuideText==null)return;var t=selectedTrack;if(t==null){studioGuideText.Text="在左侧物件列表或下方时间轴中\n选择物件，查看它的设置。";return;}
        var actions=t.spawnActions.Select(a=>"生成 · "+a.start+"f · "+(String.IsNullOrEmpty(a.generationPreset)?BulletSystem.ActionName(a.type):GenerationPresets.Name(a.generationPreset)))
          .Concat(t.bulletActions.Select(a=>"出生后 · "+a.start+"f · "+BulletSystem.ActionName(a.type))).ToArray();
        studioGuideText.Text=Theme.KindName(t.kind)+" · "+t.name+"\n时间范围："+t.start+"–"+t.end+" 帧"+(t.templateOnly?"（模板局部时间）":"")+"\n\n已添加的行为\n"+(actions.Length==0?"没有添加生成行为或出生后的行为。\n运动还可能由属性、关键帧或原生代码控制。":String.Join("\n",actions.Take(4))+(actions.Length>4?"\n另有 "+(actions.Length-4)+" 项，请进入行为页":""))+"\n\n选择下面的按钮，修改对应设置。";
    }
    private void BuildStudioWorkspace()
    {
        // Keep existing asset/preset editor alive; switching panels never changes the document.
        libraryPanel=new Panel{Dock=DockStyle.Fill,BackColor=Theme.Panel};foreach(Control c in leftSplit.Panel1.Controls.Cast<Control>().ToArray()){leftSplit.Panel1.Controls.Remove(c);libraryPanel.Controls.Add(c);}
        var shell=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty,BackColor=Theme.Panel};shell.RowStyles.Add(new RowStyle(SizeType.Absolute,40));shell.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var bar=NewStrip("StudioLibraryTabs");outlineTab=Tool("物件列表",()=>ShowStudioLibrary(false));libraryTab=Tool("素材与预设",()=>ShowStudioLibrary(true));bar.Items.Add(outlineTab);bar.Items.Add(libraryTab);shell.Controls.Add(bar,0,0);
        var body=new Panel{Dock=DockStyle.Fill};body.Controls.Add(libraryPanel);outlinePanel=new Panel{Dock=DockStyle.Fill,BackColor=Theme.Panel};body.Controls.Add(outlinePanel);shell.Controls.Add(body,0,1);leftSplit.Panel1.Controls.Add(shell);
        var rows=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(10),Margin=Padding.Empty};rows.RowStyles.Add(new RowStyle(SizeType.Absolute,0));rows.RowStyles.Add(new RowStyle(SizeType.Absolute,34));rows.RowStyles.Add(new RowStyle(SizeType.Percent,100));rows.RowStyles.Add(new RowStyle(SizeType.Absolute,26));
        var hint=new Label{Text="查找名称、类型或 ID",Dock=DockStyle.Fill,ForeColor=Theme.Muted,TextAlign=ContentAlignment.MiddleLeft};hint.Visible=false;
        outlineSearch=new TextBox{Dock=DockStyle.Fill,BackColor=Theme.Raised,ForeColor=Theme.Text,BorderStyle=BorderStyle.FixedSingle,Margin=new Padding(0,2,0,6)};outlineSearch.TextChanged+=(s,e)=>RefreshStudioWorkspace();rows.Controls.Add(outlineSearch,0,1);tips.SetToolTip(outlineSearch,"按名称、类型或 ID 查找物件，不影响时间轴");
        studioOutline=new TreeView{Dock=DockStyle.Fill,BackColor=Theme.Panel,ForeColor=Theme.Text,BorderStyle=BorderStyle.None,ItemHeight=28,Font=Theme.Font,HideSelection=false,FullRowSelect=true,ShowLines=false,ShowNodeToolTips=true,Indent=16,DrawMode=TreeViewDrawMode.OwnerDrawText};
        studioOutline.DrawNode+=(s,e)=>{var t=e.Node.Tag as Track;bool active=e.Node==studioOutline.SelectedNode;var rect=new Rectangle(e.Bounds.X,e.Bounds.Y,Math.Max(1,studioOutline.Width-e.Bounds.X),28);using(var b=new SolidBrush(active?Theme.Raised:Theme.Panel))e.Graphics.FillRectangle(b,rect);if(t!=null)using(var b=new SolidBrush(Theme.TrackColor(t.kind)))e.Graphics.FillRectangle(b,rect.Left+2,rect.Top+6,3,16);Theme.Label(e.Graphics,e.Node.Text,new Rectangle(rect.Left+12,rect.Top,rect.Width-16,28),active?Theme.Text:Theme.Muted,Theme.Font,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis,false);};
        studioOutline.AfterSelect+=(s,e)=>{if(updatingOutline||state==null)return;var t=e.Node.Tag as Track;if(t!=null&&state.tracks.Contains(t)){CommitInspectorEdits();SelectTrack(t,null);timeline.RevealTrackRow(t);}};
        studioOutline.NodeMouseDoubleClick+=(s,e)=>{var t=e.Node.Tag as Track;if(t!=null){StopPlaying();SelectTrack(t,null);SetFrame(t.start);timeline.FitSelection();}};rows.Controls.Add(studioOutline,0,2);
        outlineCount=new Label{Dock=DockStyle.Fill,ForeColor=Theme.Muted,TextAlign=ContentAlignment.MiddleLeft,Font=Theme.Small};rows.Controls.Add(outlineCount,0,3);outlinePanel.Controls.Add(rows);ShowStudioLibrary(false);
        // A small declared-relationship card, separate from the editable parameter page.
        var inspector=(TableLayoutPanel)rightSplit.Panel2.Controls[0];var content=inspector.GetControlFromPosition(0,1);inspector.Controls.Remove(content);inspector.RowCount=3;inspector.RowStyles[1]=new RowStyle(SizeType.Absolute,32);inspector.RowStyles.Add(new RowStyle(SizeType.Percent,100));inspector.Controls.Add(content,0,2);
        var relationCard=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Padding=new Padding(4,0,4,0),Margin=Padding.Empty,BackColor=Theme.Panel};relationCard.RowStyles.Add(new RowStyle(SizeType.Absolute,0));relationCard.RowStyles.Add(new RowStyle(SizeType.Absolute,32));relationSummary=new Label{Dock=DockStyle.Fill,ForeColor=Theme.Muted,Font=Theme.Small,AutoEllipsis=true,TextAlign=ContentAlignment.MiddleLeft};relationSummary.Visible=false;var relations=NewStrip("StudioRelations");relationButton=Tool("对象关系",ShowStudioRelations);relations.Items.Add(relationButton);relations.Items.Add(Tool("跳到开始",()=>{if(selectedTrack!=null){StopPlaying();SetFrame(selectedTrack.start);timeline.FitSelection();}}));relationCard.Controls.Add(relations,0,1);inspector.Controls.Add(relationCard,0,1);
        properties.HelpVisible=false;
        // Expose save, move unavailable native runtime actions out of the main editing surface.
        mainTools.Items.Insert(0,new ToolStripLabel("RUFMAKER"){Font=Theme.Bold,ForeColor=Theme.Accent,Padding=new Padding(10,0,16,0)});
        var save=Tool("保存 Ctrl+S",async()=>await SaveNow(true));mainTools.Items.Insert(5,save);
        foreach(var item in mainTools.Items.OfType<ToolStripControlHost>())if(item.Control==runButton||item.Control==debugButton||item.Control==exportButton)item.Visible=false;
        previewTools.Items.Insert(0,Tool("物件列表",()=>{leftSplit.Panel1Collapsed=!leftSplit.Panel1Collapsed;}));
        var native=new ToolStripDropDownButton("运行与导出"){Alignment=ToolStripItemAlignment.Right,ForeColor=Theme.Muted};AddMenu(native,Command("试玩 F5",async()=>await RunGame(false)),Command("调试 F6",async()=>await RunGame(true)),Command("导出游戏",async()=>await ExportGame()));native.ToolTipText="Linux 原型尚不支持 GM8.2 原生运行；原版入口保留。";mainTools.Items.Add(native);
        mainTools.Resize+=(s,e)=>shellProject.Width=Math.Max(100,mainTools.Width-650);
        studioPreviewInfo=new ToolStripLabel{ForeColor=Theme.Muted};previewTools.Items.Add(studioPreviewInfo);
        shellTime.Font=new Font(Theme.Font.FontFamily,13,FontStyle.Bold);shellTime.ForeColor=Theme.Accent;shellTime.Padding=new Padding(8,0,8,0);
        var low=transportTools.Items.OfType<ToolStripButton>().FirstOrDefault(i=>i.Text=="整理时间轴");
        var editMenu=new ToolStripMenuItem("时间轴编辑");if(low!=null)editMenu.DropDownItems.Add(Command("整理时间轴",OrganizeTimeline));
        var stretch=transportTools.Items.OfType<ToolStripDropDownButton>().FirstOrDefault(i=>i.Text.Contains("拉伸"));if(stretch!=null){}shellMenu.Items.Insert(2,editMenu);
        studioCounts=new ToolStripLabel{Alignment=ToolStripItemAlignment.Right,ForeColor=Theme.Muted};templateContext.Items.Add(studioCounts);
        Shown+=(s,e)=>{inspectorDock.DockPage("general","tabs");inspectorTabs.SelectedIndex=3;RefreshStudioWorkspace();};
    }
    private void ShowStudioLibrary(bool library){if(libraryPanel==null)return;libraryPanel.Visible=library;outlinePanel.Visible=!library;libraryTab.Checked=library;outlineTab.Checked=!library;if(library)libraryPanel.BringToFront();else outlinePanel.BringToFront();}
    private void RefreshStudioWorkspace()
    {
        if(studioOutline==null)return;RefreshStudioGuide();
        if(state==null){outlineCount.Text="尚未打开工程";return;}
        var visible=StudioOutline.Rows(state,outlineSearch.Text);string signature=outlineSearch.Text+"|"+String.Join("|",state.tracks.Select(t=>t.id+"/"+t.name+"/"+t.kind+"/"+t.parentId));
        updatingOutline=true;
        try{
            if(signature!=outlineSignature){outlineSignature=signature;var open=new HashSet<Track>(outlineNodes.Where(p=>p.Value.IsExpanded).Select(p=>p.Key));studioOutline.BeginUpdate();studioOutline.Nodes.Clear();outlineNodes.Clear();
                foreach(var t in visible)outlineNodes[t]=new TreeNode(t.name??t.id){Tag=t,ToolTipText=Theme.KindName(t.kind)+" · "+t.start+"–"+t.end+" f\nID: "+t.id+"\n双击：跳到开始；单击：选择物件"};
                foreach(var t in visible){var parent=StudioOutline.Parent(state,t);if(parent!=null&&outlineNodes.ContainsKey(parent))outlineNodes[parent].Nodes.Add(outlineNodes[t]);else studioOutline.Nodes.Add(outlineNodes[t]);}
                foreach(var pair in outlineNodes)if(open.Contains(pair.Key)||pair.Value.Parent==null)pair.Value.Expand();studioOutline.EndUpdate();
            }
            if(selectedTrack!=null&&outlineNodes.ContainsKey(selectedTrack)){studioOutline.SelectedNode=outlineNodes[selectedTrack];}
        }finally{updatingOutline=false;}
        outlineCount.Text=visible.Length+" / "+state.tracks.Count+" 个物件";
        if(studioCounts!=null)studioCounts.Text=state.tracks.Count+" 个物件  ·  "+state.laneCount+" 条轨道  ·  手动隐藏 "+timeline.HiddenTrackCount;
        if(studioPreviewInfo!=null)studioPreviewInfo.Text=state.fps+" FPS";
        if(selectedTrack==null){relationSummary.Text="选择物件，查看属性和关联关系";relationButton.Text="对象关系";relationButton.Enabled=false;return;}
        var t2=selectedTrack;var p2=StudioOutline.Parent(state,t2);relationSummary.Text=Theme.KindName(t2.kind)+"  /  "+t2.start+"–"+t2.end+" f"+(t2.templateOnly?"  /  模板局部时间":"  /  时间范围")+"\n"+(p2==null?"顶层物件":"父组："+p2.name);
        relationButton.ToolTipText=relationSummary.Text;relationButton.Enabled=true;relationButton.Text="对象关系  "+TimelineRelations.References(state,t2).Count;
    }
    private void ShowStudioRelations()
    {
        if(state==null||selectedTrack==null)return;var scene=state;var selected=selectedTrack;var menu=new ContextMenuStrip{ShowImageMargin=false};DarkStripRenderer.Apply(menu);
        menu.Items.Add(new ToolStripMenuItem(relationSummary.Text.Replace("\n"," · ")){Enabled=false});menu.Items.Add(new ToolStripSeparator());var rows=TimelineRelations.References(scene,selected);foreach(var r in rows){var item=new ToolStripMenuItem(r.relation+"  →  "+TimelineRelations.ReferenceName(scene,r)){ToolTipText=r.detail};var id=TimelineRelations.NavigationId(r);item.Enabled=TimelineRelations.Resolve(scene,id)!=null;item.Click+=(s,e)=>{var target=TimelineRelations.ResolveCurrent(state,scene,id);if(target!=null){CommitInspectorEdits();SelectTrack(target,null);timeline.RevealTrackRow(target);}};menu.Items.Add(item);}
        if(rows.Count==0)menu.Items.Add(new ToolStripMenuItem("没有关联对象"){Enabled=false});menu.Closed+=(s,e)=>menu.Dispose();menu.Show(relationButton.Owner,new Point(relationButton.Bounds.Left,relationButton.Bounds.Bottom));
    }
}
