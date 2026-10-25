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

internal sealed partial class MainForm
{
    private readonly ObjectStatisticsControl objectStatistics=new ObjectStatisticsControl();
    private readonly ToolStripStatusLabel objectCount=new ToolStripStatusLabel{Text="obj: …",IsLink=true,LinkColor=Theme.Text,ActiveLinkColor=Theme.Accent};
    private readonly InspectorTabs inspectorTabs=new InspectorTabs{Dock=DockStyle.Fill};
    private ObjectStatisticsSnapshot lastStatistics;
    private Control BuildInspectorTabs()
    {
        inspectorTabs.AddPage("属性",properties);inspectorTabs.AddPage("行为",BuildBehaviorWorkspace());inspectorTabs.AddPage("统计",objectStatistics);
        inspectorTabs.SelectedIndex=restoredView!=null?(restoredView.objectStatistics?2:Math.Max(0,Math.Min(2,restoredView.inspectorPage))):0;
        objectCount.Click+=(s,e)=>inspectorTabs.SelectedIndex=2;Language.ToolTip(tips,objectStatistics,"统计当前预览帧的实例数量；来源物件是分类明细，不重复计入总数。");
        stage.StatisticsPending=PendingStatistics;stage.StatisticsReady=data=>{lastStatistics=data;objectStatistics.SetPending(data.frame);objectStatistics.ShowStatistics(data);objectCount.Text=(data.nativeMissing?"obj ≥ ":"obj: ")+data.total.ToString("N0");};return inspectorTabs;
    }
    private void PendingStatistics(int at){objectStatistics.SetPending(at);if(lastStatistics==null)objectCount.Text="obj: …";}
    private void RefreshStatisticsLanguage(){inspectorTabs.RefreshLanguage();objectStatistics.RefreshLanguage();if(lastStatistics!=null)objectStatistics.ShowStatistics(lastStatistics);}
}
