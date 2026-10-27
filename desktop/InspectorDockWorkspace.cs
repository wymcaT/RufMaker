using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

internal sealed class InspectorDockView
{
    public string mode="rows",first="keys",active="general";
    public double rows=.45,columns=.5,details=.65;
}

// Two native pages keep their controls mounted while changing docking arrangement.
internal sealed class InspectorDockWorkspace : UserControl
{
    internal sealed class Page : Panel
    {
        public readonly string Id,Title;
        public readonly Panel Header=new Panel{Dock=DockStyle.Top,Height=29,BackColor=Theme.Raised};
        public readonly Label Caption=new Label{Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(8,0,0,0),AutoEllipsis=true,ForeColor=Theme.Text,Cursor=Cursors.SizeAll};
        public Page(string id,string title,Control content){Id=id;Title=title;Name="DockPage:"+id;BackColor=Theme.Panel;Dock=DockStyle.Fill;content.Dock=DockStyle.Fill;Controls.Add(content);Controls.Add(Header);Header.Controls.Add(Caption);Caption.Name="DockTitle:"+id;}
    }
    private readonly SplitContainer split=new SplitContainer{Dock=DockStyle.Fill,BackColor=Theme.Line,SplitterWidth=7,Size=new Size(650,500)};
    private readonly Panel tabs=new Panel{Dock=DockStyle.Fill,BackColor=Theme.Panel};
    private readonly FlowLayoutPanel tabHeader=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,WrapContents=true,BackColor=Theme.Raised,Padding=Padding.Empty,Margin=Padding.Empty};
    private readonly Panel tabBody=new Panel{Dock=DockStyle.Fill,BackColor=Theme.Panel};
    private readonly Panel preview=new Panel{Visible=false,Enabled=false,BackColor=Theme.Raised};
    private readonly Label previewText=new Label{Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Theme.Accent};
    private readonly Dictionary<string,Page> pages=new Dictionary<string,Page>();
    private readonly Dictionary<string,Label> tabTitles=new Dictionary<string,Label>();
    private readonly ToolTip tips=new ToolTip();
    private InspectorDockView view=new InspectorDockView();private bool arranging,resizing,dragging;private int legacyRowsHeight;private string source,side;private Control dragControl;private Point dragStart;
    public Action BeforeLayoutChange;
    public string Mode {get{return view.mode;}}
    public string First {get{return view.first;}}
    public string ActivePage {get{return view.active;}}
    public SplitContainer Divider {get{return split;}}
    public InspectorDockWorkspace()
    {
        Name="InspectorDockWorkspace";DoubleBuffered=true;BackColor=Theme.Panel;Font=Theme.Font;Dock=DockStyle.Fill;
        tabs.Controls.Add(tabBody);tabs.Controls.Add(tabHeader);Controls.Add(split);Controls.Add(tabs);preview.Controls.Add(previewText);Controls.Add(preview);
        preview.Paint+=(s,e)=>{using(var pen=new Pen(Theme.Accent,2))e.Graphics.DrawRectangle(pen,1,1,Math.Max(0,preview.Width-3),Math.Max(0,preview.Height-3));};
        split.SplitterMoving+=(s,e)=>resizing=true;
        split.SplitterMoved+=(s,e)=>{if(resizing&&!arranging){int total=Extent()-split.SplitterWidth;if(total>0){if(view.mode=="columns")view.columns=split.SplitterDistance/(double)total;else{view.rows=split.SplitterDistance/(double)total;legacyRowsHeight=0;}}}resizing=false;};
        split.Resize+=(s,e)=>SizeDivider();
    }
    public void AddPage(string id,string title,Control content)
    {
        var page=new Page(id,title,content);pages.Add(id,page);WireDrag(page.Caption,id);WireDrag(page.Header,id);
        var menu=new Button{Dock=DockStyle.Right,Width=29,Text="⋮",FlatStyle=FlatStyle.Flat,ForeColor=Theme.Text,BackColor=Theme.Raised,TabStop=false};menu.FlatAppearance.BorderSize=0;page.Header.Controls.Add(menu);menu.Click+=(s,e)=>ShowLayoutMenu(menu,id);
        var tab=new Label{Name="DockTab:"+id,AutoSize=false,Size=new Size(110,29),TextAlign=ContentAlignment.MiddleCenter,ForeColor=Theme.Text,Cursor=Cursors.SizeAll,Margin=new Padding(0,0,2,0),TabStop=true};tabTitles.Add(id,tab);WireDrag(tab,id);tabHeader.Controls.Add(tab);
        RefreshLanguage();if(pages.Count==2)Arrange();
    }
    private void WireDrag(Control control,string id)
    {
        control.MouseDown+=(s,e)=>{if(e.Button!=MouseButtons.Left)return;source=id;dragControl=control;dragStart=control.PointToScreen(e.Location);dragging=false;side=null;control.Capture=true;};
        control.MouseMove+=(s,e)=>{if(dragControl!=control||source!=id||!control.Capture)return;Point point=control.PointToScreen(e.Location);if(!dragging&&Math.Abs(point.X-dragStart.X)<SystemInformation.DragSize.Width&&Math.Abs(point.Y-dragStart.Y)<SystemInformation.DragSize.Height)return;dragging=true;side=DropSide(id,PointToClient(point));ShowPreview(id,side);};
        control.MouseUp+=(s,e)=>{if(e.Button!=MouseButtons.Left||dragControl!=control)return;string moving=source,target=side;bool drop=dragging&&target!=null;CancelDrag();if(drop)DockPage(moving,target);else Activate(id);};
        control.MouseCaptureChanged+=(s,e)=>{if(dragControl==control&&!control.Capture)CancelDrag();};
        tips.SetToolTip(control,Language.Text("拖动标题到边缘分栏，到中间合并页签；右键选择布局。"));
        control.MouseUp+=(s,e)=>{if(e.Button==MouseButtons.Right)ShowLayoutMenu(control,id);};
    }
    private string DropSide(string id,Point point)
    {
        if(!ClientRectangle.Contains(point))return null;var other=pages[id=="keys"?"general":"keys"];Rectangle target=other.Visible?RectangleToClient(other.RectangleToScreen(other.ClientRectangle)):ClientRectangle;if(!target.Contains(point))target=ClientRectangle;
        double x=(point.X-target.Left)/(double)Math.Max(1,target.Width),y=(point.Y-target.Top)/(double)Math.Max(1,target.Height);double[] distances={x,1-x,y,1-y};int nearest=Array.IndexOf(distances,distances.Min());return distances[nearest]>.24?"tabs":new[]{"left","right","top","bottom"}[nearest];
    }
    private void ShowPreview(string id,string location)
    {
        if(location==null){preview.Visible=false;return;}var area=ClientRectangle;
        if(location=="left")area.Width/=2;else if(location=="right"){area.X+=area.Width/2;area.Width-=area.Width/2;}else if(location=="top")area.Height/=2;else if(location=="bottom"){area.Y+=area.Height/2;area.Height-=area.Height/2;}
        preview.Bounds=area;previewText.Text=Language.Text(pages[id].Title)+" · "+Language.Text(location=="tabs"?"合并为页签":location=="left"?"停靠到左侧":location=="right"?"停靠到右侧":location=="top"?"停靠到上方":"停靠到下方");preview.Visible=true;preview.BringToFront();
    }
    private void CancelDrag(){var control=dragControl;dragControl=null;source=side=null;dragging=false;preview.Visible=false;if(control!=null&&control.Capture)control.Capture=false;}
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData){if(keyData==Keys.Escape&&dragControl!=null){CancelDrag();return true;}return base.ProcessCmdKey(ref msg,keyData);}
    public void DockPage(string id,string location)
    {
        if(!pages.ContainsKey(id)||!new[]{"left","right","top","bottom","tabs"}.Contains(location))return;
        if(BeforeLayoutChange!=null)BeforeLayoutChange();if(legacyRowsHeight>0){view.rows=CaptureView().rows;legacyRowsHeight=0;}view.active=id;
        if(location=="tabs")view.mode="tabs";else{view.mode=location=="left"||location=="right"?"columns":"rows";view.first=location=="left"||location=="top"?id:id=="keys"?"general":"keys";}Arrange();
    }
    public void Activate(string id){if(!pages.ContainsKey(id))return;view.active=id;if(view.mode=="tabs")foreach(var pair in pages){pair.Value.Visible=pair.Key==id;if(pair.Key==id)pair.Value.BringToFront();}StyleTabs();}
    public InspectorDockView CaptureView(){int total=Extent()-split.SplitterWidth;return new InspectorDockView{mode=view.mode,first=view.first,active=view.active,rows=legacyRowsHeight>0&&view.mode=="rows"&&total>0?split.SplitterDistance/(double)total:view.rows,columns=view.columns,details=view.details};}
    public void Restore(InspectorDockView saved,bool legacyOpen=true,int legacyHeight=0)
    {
        legacyRowsHeight=saved==null&&legacyOpen?Math.Max(0,legacyHeight):0;view=saved==null?new InspectorDockView{mode=legacyOpen?"rows":"tabs"}:new InspectorDockView{mode=new[]{"rows","columns","tabs"}.Contains(saved.mode)?saved.mode:"rows",first=saved.first=="general"?"general":"keys",active=saved.active=="keys"?"keys":"general",rows=Ratio(saved.rows,.45),columns=Ratio(saved.columns,.5),details=Ratio(saved.details,.65)};Arrange();
    }
    private static double Ratio(double value,double fallback){return Double.IsNaN(value)||Double.IsInfinity(value)?fallback:Math.Max(.05,Math.Min(.95,value));}
    private void Arrange()
    {
        if(pages.Count!=2||arranging)return;arranging=true;SuspendLayout();split.SuspendLayout();tabBody.SuspendLayout();try{
            foreach(var page in pages.Values){if(page.Parent!=null)page.Parent.Controls.Remove(page);page.Header.Visible=view.mode!="tabs";page.Visible=true;}
            split.Visible=view.mode!="tabs";tabs.Visible=view.mode=="tabs";
            if(view.mode=="tabs"){foreach(var page in pages.Values)tabBody.Controls.Add(page);Activate(view.active);}else{split.Panel1MinSize=split.Panel2MinSize=0;split.Orientation=view.mode=="columns"?Orientation.Vertical:Orientation.Horizontal;split.Panel1.Controls.Add(pages[view.first]);split.Panel2.Controls.Add(pages[view.first=="keys"?"general":"keys"]);split.BringToFront();}
        }finally{tabBody.ResumeLayout(true);split.ResumeLayout(true);ResumeLayout(true);arranging=false;}SizeDivider();StyleTabs();
    }
    private int Extent(){return split.Orientation==Orientation.Vertical?split.ClientSize.Width:split.ClientSize.Height;}
    private void SizeDivider()
    {
        if(arranging||resizing||view.mode=="tabs")return;int total=Extent()-split.SplitterWidth;if(total<=0)return;arranging=true;try{split.Panel1MinSize=split.Panel2MinSize=0;int minimum=Math.Min(total/2,view.mode=="columns"?140:80);double ratio=view.mode=="columns"?view.columns:view.rows;int desired=legacyRowsHeight>0&&view.mode=="rows"?legacyRowsHeight:(int)Math.Round(total*ratio);split.SplitterDistance=Math.Max(minimum,Math.Min(total-minimum,desired));split.Panel1MinSize=split.Panel2MinSize=minimum;}finally{arranging=false;}
    }
    private void StyleTabs(){foreach(var pair in tabTitles)pair.Value.BackColor=pair.Key==view.active?Theme.Panel:Theme.Raised;}
    public void RefreshLanguage(){foreach(var page in pages.Values){page.Caption.Text="⠿ "+Language.Text(page.Title);page.Header.Height=Math.Max(29,Font.Height+14);tips.SetToolTip(page.Caption,Language.Text("拖动标题到边缘分栏，到中间合并页签；右键选择布局。"));}foreach(var pair in tabTitles){pair.Value.Text=Language.Text(pages[pair.Key].Title);pair.Value.Size=new Size(Math.Max(90,TextRenderer.MeasureText(pair.Value.Text,Font).Width+30),Math.Max(29,Font.Height+14));}Invalidate(true);}
    private void ShowLayoutMenu(Control anchor,string id)
    {
        var menu=new ContextMenuStrip{BackColor=Theme.Panel,ForeColor=Theme.Text,ShowImageMargin=false};DarkStripRenderer.Apply(menu);
        foreach(var pair in new[]{new[]{"left","停靠到左侧"},new[]{"right","停靠到右侧"},new[]{"top","停靠到上方"},new[]{"bottom","停靠到下方"},new[]{"tabs","合并为页签"}}){string side=pair[0];menu.Items.Add(Language.Text(pair[1]),null,(s,e)=>DockPage(id,side));}menu.Items.Add(new ToolStripSeparator());menu.Items.Add(Language.Text("恢复右栏默认布局"),null,(s,e)=>{if(BeforeLayoutChange!=null)BeforeLayoutChange();Restore(null);});menu.Closed+=(s,e)=>menu.Dispose();menu.Show(anchor,new Point(0,anchor.Height));
    }
    protected override void Dispose(bool disposing){if(disposing){CancelDrag();tips.Dispose();}base.Dispose(disposing);}
}
