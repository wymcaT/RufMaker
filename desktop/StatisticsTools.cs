using System;
using System.Drawing;
using System.Windows.Forms;

// Native tab headers use the Windows light frame even with owner-drawn labels.
// Keep the pages mounted and switch visibility without recreating the inspector.
internal sealed class InspectorTabs : UserControl
{
    private readonly FlowLayoutPanel header=new FlowLayoutPanel{Dock=DockStyle.Top,WrapContents=true,BackColor=Theme.Panel,Margin=Padding.Empty,Padding=Padding.Empty};
    private readonly Panel body=new Panel{Dock=DockStyle.Fill,BackColor=Theme.Panel};
    private readonly System.Collections.Generic.List<Control> pages=new System.Collections.Generic.List<Control>();
    private readonly System.Collections.Generic.List<Button> buttons=new System.Collections.Generic.List<Button>();
    private readonly System.Collections.Generic.List<string> titles=new System.Collections.Generic.List<string>();
    private int selected;
    public InspectorTabs(){DoubleBuffered=true;BackColor=Theme.Panel;Font=Theme.Font;Controls.Add(body);Controls.Add(header);}
    private void SizeHeader(){int height=Math.Max(Font.Height+18,header.GetPreferredSize(new Size(Math.Max(1,ClientSize.Width),0)).Height);if(header.Height!=height)header.Height=height;}
    protected override void OnSizeChanged(EventArgs e){base.OnSizeChanged(e);SizeHeader();}
    public int SelectedIndex {get{return selected;}set{if(value<0||value>=pages.Count)return;selected=value;for(int i=0;i<pages.Count;i++){pages[i].Visible=i==value;buttons[i].BackColor=i==value?Theme.Raised:Theme.Panel;buttons[i].ForeColor=i==value?Theme.Accent:Theme.Text;}pages[value].BringToFront();}}
    public void AddPage(string title,Control page)
    {
        int index=pages.Count;titles.Add(title);pages.Add(page);page.Dock=DockStyle.Fill;body.Controls.Add(page);
        var button=new Button{Text=title,AutoSize=true,MinimumSize=new Size(66,Font.Height+16),FlatStyle=FlatStyle.Flat,UseVisualStyleBackColor=false,BackColor=Theme.Panel,ForeColor=Theme.Text,Padding=new Padding(6,3,6,3),Margin=new Padding(0,0,3,3)};
        button.FlatAppearance.BorderColor=Theme.Line;button.Click+=(s,e)=>SelectedIndex=index;buttons.Add(button);header.Controls.Add(button);SelectedIndex=selected;SizeHeader();
    }
    public void RefreshLanguage(){for(int i=0;i<buttons.Count;i++)buttons[i].Text=Language.Text(titles[i]);SizeHeader();}
}

internal sealed class UiGuideDialog : LocalizedForm
{
    public UiGuideDialog(System.Collections.Generic.IReadOnlyList<Tuple<string,string,Action>> commands)
    {
        Text=Language.Text("新手 3 步引导与快捷命令");
        Size=new Size(680,540);
        MinimumSize=new Size(560,440);
        StartPosition=FormStartPosition.CenterParent;
        FormBorderStyle=FormBorderStyle.FixedDialog;
        MaximizeBox=false;
        MinimizeBox=false;
        Font=Theme.Font;
        BackColor=Theme.Panel;
        ForeColor=Theme.Text;
        Icon=AppWindow.LoadIcon();
        ShowInTaskbar=false;
        KeyPreview=true;

        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(16)};
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,112));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,44));

        var steps=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=new Padding(0,0,0,10)};
        for(int i=0;i<3;i++)steps.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33.33f));
        string[][] cardTexts={
            new[]{"1",Language.Text("放入弹幕 / 预设"),Language.Text("从左栏“预设”拖卡片到舞台或时间轴；双击可立即在播放头生成。")},
            new[]{"2",Language.Text("调参与插关键帧"),Language.Text("右栏按住数值上下拖拽平滑调参；点 ◇ 或按 K 为参数插入关键帧。")},
            new[]{"3",Language.Text("试玩与导出游戏"),Language.Text("空格播放音频动画 · F5 试玩 · F6 无敌调试 · 粉色导出按钮生成游戏。")}
        };
        for(int i=0;i<3;i++){
            string badge=cardTexts[i][0],title=cardTexts[i][1],desc=cardTexts[i][2];
            var card=new Panel{Dock=DockStyle.Fill,Margin=new Padding(i==0?0:5,0,i==2?0:5,0),Padding=new Padding(10),BackColor=Theme.Raised};
            card.Paint+=(s,e)=>{
                Theme.FillRoundRect(e.Graphics,card.ClientRectangle,7,Theme.Raised);
                Theme.DrawRoundRect(e.Graphics,card.ClientRectangle,7,Theme.LineBright,1f);
                Theme.FillRoundRect(e.Graphics,new Rectangle(10,10,20,20),10,Theme.Accent);
                Theme.Label(e.Graphics,badge,new Rectangle(10,10,20,20),Color.FromArgb(20,22,30),Theme.SmallBold,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter,false);
            };
            card.Controls.Add(new Label{Text=desc,Location=new Point(10,36),Size=new Size(188,60),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,ForeColor=Theme.Muted,Font=Theme.Small,BackColor=Color.Transparent});
            card.Controls.Add(new Label{Text=title,Location=new Point(36,11),AutoSize=true,ForeColor=Theme.Text,Font=Theme.Bold,BackColor=Color.Transparent});
            steps.Controls.Add(card,i,0);
        }
        layout.Controls.Add(steps,0,0);

        var filter=new TextBox{Dock=DockStyle.Fill,Margin=new Padding(0,4,0,6),BackColor=Theme.Input,ForeColor=Theme.Text,BorderStyle=BorderStyle.FixedSingle,Font=Theme.Font};
        layout.Controls.Add(filter,0,1);

        var list=new ListBox{Dock=DockStyle.Fill,BackColor=Theme.Input,ForeColor=Theme.Text,BorderStyle=BorderStyle.FixedSingle,IntegralHeight=false,ItemHeight=Math.Max(26,Font.Height+10),DrawMode=DrawMode.OwnerDrawFixed};
        DarkScrollbars.Attach(list);
        Action refreshList=()=>{
            string q=(filter.Text??"").Trim();
            list.BeginUpdate();list.Items.Clear();
            foreach(var item in commands)if(String.IsNullOrEmpty(q)||item.Item1.IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0||item.Item2.IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0)list.Items.Add(item);
            if(list.Items.Count>0)list.SelectedIndex=0;
            list.EndUpdate();
        };
        list.DrawItem+=(s,e)=>{
            if(e.Index<0||e.Index>=list.Items.Count)return;
            var item=(Tuple<string,string,Action>)list.Items[e.Index];
            bool sel=(e.State&DrawItemState.Selected)!=0;
            using(var bg=new SolidBrush(sel?Theme.AccentSoft:Theme.Input))e.Graphics.FillRectangle(bg,e.Bounds);
            if(sel)using(var bar=new SolidBrush(Theme.Accent))e.Graphics.FillRectangle(bar,e.Bounds.X,e.Bounds.Y+4,3,e.Bounds.Height-8);
            Theme.Label(e.Graphics,item.Item1,new Rectangle(e.Bounds.X+12,e.Bounds.Y,Math.Max(40,e.Bounds.Width-150),e.Bounds.Height),sel?Color.White:Theme.Text,Theme.Font,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis,false);
            Theme.Label(e.Graphics,item.Item2,new Rectangle(e.Bounds.Right-136,e.Bounds.Y,126,e.Bounds.Height),sel?Theme.Accent:Theme.Muted,Theme.Small,TextFormatFlags.Right|TextFormatFlags.VerticalCenter,false);
        };
        Action runSelected=()=>{
            var chosen=list.SelectedItem as Tuple<string,string,Action>;
            if(chosen==null)return;
            Close();
            chosen.Item3();
        };
        list.DoubleClick+=(s,e)=>runSelected();
        filter.TextChanged+=(s,e)=>refreshList();
        KeyDown+=(s,e)=>{
            if(e.KeyCode==Keys.Escape){Close();e.Handled=true;}
            else if(e.KeyCode==Keys.Enter){runSelected();e.Handled=true;}
            else if(e.KeyCode==Keys.Down&&!list.Focused&&list.Items.Count>0){list.SelectedIndex=Math.Min(list.Items.Count-1,list.SelectedIndex+1);e.Handled=true;}
            else if(e.KeyCode==Keys.Up&&!list.Focused&&list.Items.Count>0){list.SelectedIndex=Math.Max(0,list.SelectedIndex-1);e.Handled=true;}
        };
        refreshList();
        layout.Controls.Add(list,0,2);

        var foot=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(0,6,0,0)};
        var closeBtn=new Button{Text=Language.Text("关闭"),Width=88,Height=30,FlatStyle=FlatStyle.Flat,BackColor=Theme.Raised,ForeColor=Theme.Text};closeBtn.FlatAppearance.BorderColor=Theme.LineBright;closeBtn.Click+=(s,e)=>Close();
        var runBtn=new Button{Text=Language.Text("执行所选"),Width=104,Height=30,FlatStyle=FlatStyle.Flat,BackColor=Theme.Accent,ForeColor=Color.FromArgb(20,22,30),Font=Theme.Bold};runBtn.FlatAppearance.BorderSize=0;runBtn.Click+=(s,e)=>runSelected();
        foot.Controls.Add(closeBtn);foot.Controls.Add(runBtn);
        layout.Controls.Add(foot,0,3);
        Controls.Add(layout);
        Shown+=(s,e)=>filter.Focus();
    }
}
internal sealed partial class MainForm
{
    private readonly ObjectStatisticsControl objectStatistics=new ObjectStatisticsControl();
    private readonly ToolStripStatusLabel objectCount=new ToolStripStatusLabel{Text="obj: …",IsLink=true,LinkColor=Theme.Text,ActiveLinkColor=Theme.Accent};
    private readonly InspectorTabs inspectorTabs=new InspectorTabs{Dock=DockStyle.Fill};
    private ObjectStatisticsSnapshot lastStatistics;
    private Panel propertiesHost,optionalModuleShelf;private FlowLayoutPanel optionalModulesFlow;private Button optionalModuleToggle;private bool optionalModulesExpanded;
    private void InitOptionalModuleShelf()
    {
        if(propertiesHost!=null)return;
        propertiesHost=properties.Parent as Panel;
        if(propertiesHost==null)return;
        optionalModuleShelf=new Panel{Dock=DockStyle.Bottom,AutoSize=true,Visible=false,Padding=new Padding(8,3,8,4),BackColor=Theme.Void};
        optionalModuleShelf.Paint+=(s,e)=>{using(var pen=new Pen(Theme.Line))e.Graphics.DrawLine(pen,0,0,optionalModuleShelf.Width,0);};
        optionalModuleToggle=new Button{Text="＋ 可选属性模块 ▸",Dock=DockStyle.Top,Height=24,TextAlign=ContentAlignment.MiddleLeft,FlatStyle=FlatStyle.Flat,BackColor=Theme.Void,ForeColor=Theme.Muted,Font=Theme.SmallBold,Cursor=Cursors.Hand,TabStop=false,Padding=Padding.Empty};
        optionalModuleToggle.FlatAppearance.BorderSize=0;
        optionalModuleToggle.FlatAppearance.MouseOverBackColor=Theme.Raised;
        Language.ToolTip(tips,optionalModuleToggle,"点击展开/收起可选属性模块");
        optionalModuleToggle.Click+=(s,e)=>{optionalModulesExpanded=!optionalModulesExpanded;RefreshOptionalModuleShelf();};
        optionalModulesFlow=new FlowLayoutPanel{Name="optionalModulesFlow",Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Visible=false,Padding=new Padding(0,3,0,2),BackColor=Theme.Void};
        optionalModuleShelf.Controls.Add(optionalModulesFlow);
        optionalModuleShelf.Controls.Add(optionalModuleToggle);
        propertiesHost.Controls.Add(optionalModuleShelf);
        properties.BringToFront();
        RefreshOptionalModuleShelf();
    }
    private void RefreshOptionalModuleShelf()
    {
        if(optionalModuleShelf==null||optionalModulesFlow==null)return;
        var flow=optionalModulesFlow;
        flow.SuspendLayout();
        flow.Controls.Clear();
        bool show=selectedTrack!=null&&selectedTrack.kind!="native"&&timeline.SelectedTracks.Count<=1;
        int added=0,enabledCount=0;
        if(show){
            for(int i=0;i<ObjectProperties.Keys.Length;i++){
                string key=ObjectProperties.Keys[i];
                if(!ObjectProperties.Supports(selectedTrack,key))continue;
                int index=i;bool active=ObjectProperties.Shows(selectedTrack,index);if(active)enabledCount++;
                var chip=new Button{Text=(active?"✓ ":"＋ ")+Language.Text(ObjectProperties.Names[index]),AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Height=22,Margin=new Padding(0,2,5,2),Padding=new Padding(6,0,6,0),FlatStyle=FlatStyle.Flat,BackColor=active?Theme.BadgeGreenFill:Theme.Input,ForeColor=active?Theme.BadgeGreen:Theme.Muted,Font=Theme.Small,Cursor=Cursors.Hand,TabStop=false};
                chip.FlatAppearance.BorderSize=1;
                chip.FlatAppearance.BorderColor=active?Theme.BadgeGreen:Theme.LineBright;
                chip.FlatAppearance.MouseOverBackColor=Theme.CardHover;
                Language.ToolTip(tips,chip,active?"已挂载该属性模块；点击前往或刷新，可在属性中隐藏":"点击立即挂载此属性模块到当前物件");
                var target=selectedTrack;
                chip.Click+=async(s,e)=>{await UsePreset(ObjectProperties.Kind,key,target,frame);};
                flow.Controls.Add(chip);
                added++;
            }
        }
        optionalModuleShelf.Visible=added>0;
        if(optionalModuleToggle!=null)optionalModuleToggle.Text=(optionalModulesExpanded?"▾ ":"▸ ")+String.Format(Language.Text("可选属性模块（{0}/{1} 已启用）"),enabledCount,added);
        flow.Visible=optionalModulesExpanded&&added>0;
        flow.ResumeLayout(true);
    }
    private void ShowGuidePalette()
    {
        var items=new System.Collections.Generic.List<Tuple<string,string,Action>>{
            Tuple.Create<string,string,Action>(Language.Text("打开本地入门教程 (README)"),"Ctrl+Shift+K",(Action)OpenLocalReadme),
            Tuple.Create<string,string,Action>(Language.Text("试玩游戏"),"F5",(Action)(async()=>await RunGame(false))),
            Tuple.Create<string,string,Action>(Language.Text("无敌调试试玩"),"F6",(Action)(async()=>await RunGame(true))),
            Tuple.Create<string,string,Action>(Language.Text("导出独立游戏"),"CTA",(Action)(async()=>await ExportGame())),
            Tuple.Create<string,string,Action>(Language.Text("为所有常用参数添加关键帧"),"Ctrl+K",(Action)AddAllKeys),
            Tuple.Create<string,string,Action>(Language.Text("适应房间舞台视图"),"Ctrl+0",(Action)(()=>stage.ResetView())),
            Tuple.Create<string,string,Action>(Language.Text("切换网格显示"),Language.Text("预览"),(Action)(()=>{stage.Grid=!stage.Grid;stage.DisplayOptionsChanged();RefreshShell();})),
            Tuple.Create<string,string,Action>(Language.Text("切换碰撞判定框"),Language.Text("预览"),(Action)(()=>{stage.Hitboxes=!stage.Hitboxes;stage.DisplayOptionsChanged();RefreshShell();})),
            Tuple.Create<string,string,Action>(Language.Text("切换渲染拉镜预览"),Language.Text("预览"),(Action)(()=>{stage.RenderCamera=!stage.RenderCamera;stage.DisplayOptionsChanged();RefreshShell();})),
            Tuple.Create<string,string,Action>(Language.Text("整理时间轴轨道排列"),Language.Text("时间轴"),(Action)OrganizeTimeline),
            Tuple.Create<string,string,Action>(Language.Text("定位时间轴到当前播放头"),Language.Text("时间轴"),(Action)(()=>timeline.EnsureFrameVisible())),
            Tuple.Create<string,string,Action>(Language.Text("缩放到全曲总览"),Language.Text("时间轴"),(Action)(()=>timeline.FitAll())),
            Tuple.Create<string,string,Action>(Language.Text("展开/编辑当前所选 GML"),Language.Text("高级"),(Action)(async()=>await EditSelection())),
            Tuple.Create<string,string,Action>(Language.Text("切换简体中文界面"),Language.Text("语言"),(Action)(()=>ChangeShellLanguage("zh-CN"))),
            Tuple.Create<string,string,Action>(Language.Text("Switch to English UI"),"Language",(Action)(()=>ChangeShellLanguage("en")))
        };
        using(var dialog=new UiGuideDialog(items))Language.ShowDialog(dialog,this);
    }
    private Control BuildInspectorTabs()
    {
        inspectorTabs.AddPage("属性",properties);inspectorTabs.AddPage("行为",BuildBehaviorWorkspace());inspectorTabs.AddPage("统计",objectStatistics);
        inspectorTabs.SelectedIndex=restoredView!=null?(restoredView.objectStatistics?2:Math.Max(0,Math.Min(2,restoredView.inspectorPage))):0;
        inspectorDock.AddPage("general","常规设置",inspectorTabs);InitOptionalModuleShelf();inspectorDock.AddPage("keys","关键帧",BuildKeyframeProperties());inspectorDock.BeforeLayoutChange=()=>{CommitInspectorEdits();behaviorProperties.CommitPending();};inspectorDock.Restore(restoredView==null?null:restoredView.inspectorDock,restoredView==null||restoredView.keyframesOpen,restoredView==null?0:restoredView.keyframesHeight);
        objectCount.Click+=(s,e)=>{inspectorTabs.SelectedIndex=2;inspectorDock.Activate("general");};Language.ToolTip(tips,objectStatistics,"统计当前预览帧的实例数量；来源物件是分类明细，不重复计入总数。");
        stage.StatisticsPending=PendingStatistics;stage.StatisticsReady=data=>{lastStatistics=data;objectStatistics.SetPending(data.frame);objectStatistics.ShowStatistics(data);objectCount.Text=(data.nativeMissing?"obj ≥ ":"obj: ")+data.total.ToString("N0");};return inspectorDock;
    }
    private void PendingStatistics(int at){objectStatistics.SetPending(at);if(lastStatistics==null)objectCount.Text="obj: …";}
    private void RefreshStatisticsLanguage(){inspectorTabs.RefreshLanguage();inspectorDock.RefreshLanguage();RefreshKeyWorkspace();objectStatistics.RefreshLanguage();if(lastStatistics!=null)objectStatistics.ShowStatistics(lastStatistics);}
}
