using System;
using System.Drawing;
using System.Windows.Forms;

internal sealed partial class MainForm
{
    private readonly ObjectStatisticsControl objectStatistics=new ObjectStatisticsControl();
    private readonly ToolStripStatusLabel objectCount=new ToolStripStatusLabel{Text="obj: …",IsLink=true,LinkColor=Theme.Text,ActiveLinkColor=Theme.Accent};
    private readonly TabControl inspectorTabs=new TabControl{Dock=DockStyle.Fill,DrawMode=TabDrawMode.OwnerDrawFixed,SizeMode=TabSizeMode.Fixed,ItemSize=new Size(120,30)};
    private ObjectStatisticsSnapshot lastStatistics;
    private Control BuildInspectorTabs()
    {
        foreach(string title in new[]{"属性","对象统计"})inspectorTabs.TabPages.Add(new TabPage(title){BackColor=Theme.Panel,Padding=Padding.Empty});
        inspectorTabs.TabPages[0].Controls.Add(properties);objectStatistics.Dock=DockStyle.Fill;inspectorTabs.TabPages[1].Controls.Add(objectStatistics);
        inspectorTabs.DrawItem+=(s,e)=>{using(var brush=new SolidBrush(e.Index==inspectorTabs.SelectedIndex?Theme.Raised:Theme.Panel))e.Graphics.FillRectangle(brush,e.Bounds);Theme.Label(e.Graphics,e.Index==0?"属性":"对象统计",e.Bounds,Theme.Text,null,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);};
        inspectorTabs.SelectedIndex=restoredView!=null&&restoredView.objectStatistics?1:0;
        objectCount.Click+=(s,e)=>inspectorTabs.SelectedIndex=1;Language.ToolTip(tips,objectStatistics,"统计当前预览帧的实例数量；来源物件是分类明细，不重复计入总数。");
        stage.StatisticsPending=PendingStatistics;stage.StatisticsReady=data=>{lastStatistics=data;objectStatistics.SetPending(data.frame);objectStatistics.ShowStatistics(data);objectCount.Text=(data.nativeMissing?"obj ≥ ":"obj: ")+data.total.ToString("N0");};return inspectorTabs;
    }
    private void PendingStatistics(int at){objectStatistics.SetPending(at);if(lastStatistics==null)objectCount.Text="obj: …";}
    private void RefreshStatisticsLanguage(){inspectorTabs.TabPages[0].Text=Language.Text("属性");inspectorTabs.TabPages[1].Text=Language.Text("对象统计");inspectorTabs.Invalidate();objectStatistics.RefreshLanguage();if(lastStatistics!=null)objectStatistics.ShowStatistics(lastStatistics);}
}
