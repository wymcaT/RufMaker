using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed class GmPathEditor : LocalizedForm
{
    private readonly ParameterInspector inspector=new ParameterInspector{Dock=DockStyle.Fill,Compact=true,HelpVisible=false};
    private readonly GmPathCanvas canvas;
    private readonly GmPath draft;
    private readonly Button save,remove;
    private readonly Func<GmPath,Task> persist;
    private bool saving;
    public GmPathEditor(GmAssetData asset,RoomData room,Bitmap background,Func<GmPath,Task> persist)
    {
        draft=asset.path.Copy();this.persist=persist;
        Text=Language.Text("GM 路径")+" · "+asset.name;Size=new Size(1040,700);MinimumSize=new Size(690,420);StartPosition=FormStartPosition.CenterParent;
        ShowInTaskbar=false;Font=Theme.Font;BackColor=Theme.Panel;ForeColor=Theme.Text;Icon=AppWindow.LoadIcon();
        canvas=new GmPathCanvas(draft,room,background){Dock=DockStyle.Fill};
        canvas.SelectionChanged+=UpdateInspector;canvas.PointMoved+=()=>inspector.Refresh();
        var table=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,290));
        table.Controls.Add(canvas,0,0);table.Controls.Add(inspector,1,0);
        var tip=new Label{Dock=DockStyle.Top,Height=34,Padding=new Padding(10,0,0,0),TextAlign=ContentAlignment.MiddleLeft,ForeColor=Theme.Muted,
            Text="双击空白添加点，拖动移动。选中后可改坐标与速度。"};
        var footer=GmResourceUi.Footer();var cancel=GmResourceUi.Button("取消",()=>Close());
        save=GmResourceUi.Button("保存路径",async()=>await Save());remove=GmResourceUi.Button("删除路径点",()=>canvas.Remove());
        footer.Controls.Add(cancel);footer.Controls.Add(save);footer.Controls.Add(remove);
        Controls.Add(table);Controls.Add(tip);Controls.Add(footer);CancelButton=cancel;
        FormClosing+=(s,e)=>{if(saving)e.Cancel=true;};
        UpdateInspector();
    }
    private void UpdateInspector()
    {
        var bag=new NativeProperties();const string path="路径";
        bag.Add("平滑",path,typeof(bool),()=>draft.smooth,v=>{draft.smooth=(bool)v;canvas.GeometryChanged();});
        bag.Add("闭合",path,typeof(bool),()=>draft.closed,v=>{draft.closed=(bool)v;canvas.GeometryChanged();});
        bag.Add("曲线精度",path,typeof(int),()=>draft.precision,v=>{draft.precision=Math.Max(1,Math.Min(8,(int)v));canvas.GeometryChanged();inspector.Refresh();});
        var point=canvas.SelectedPoint;remove.Enabled=point!=null;
        if(point!=null){
            string category="路径点";
            bag.Add("点编号",category,typeof(int),()=>canvas.Selected+1,null);
            bag.Add("X",category,typeof(double),()=>point.x,v=>{point.x=(double)v;canvas.GeometryChanged();});
            bag.Add("Y",category,typeof(double),()=>point.y,v=>{point.y=(double)v;canvas.GeometryChanged();});
            bag.Add("速度 / %",category,typeof(double),()=>point.speed,v=>point.speed=Math.Max(0,Math.Min(10000,(double)v)));
        }
        inspector.SelectedObject=bag;
    }
    private async Task Save()
    {
        if(saving)return;
        inspector.CommitPending();saving=true;save.Enabled=false;
        try{await persist(draft);saving=false;DialogResult=DialogResult.OK;Close();}
        catch(Exception error){Language.Message(this,error.Message,Text);}
        finally{saving=false;if(!IsDisposed)save.Enabled=true;}
    }
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData)
    {
        if(keyData==(Keys.Control|Keys.S)){SaveFromKey();return true;}
        if(keyData==Keys.Delete&&canvas.Focused){canvas.Remove();return true;}
        return base.ProcessCmdKey(ref msg,keyData);
    }
    private async void SaveFromKey(){await Save();}
}

internal sealed class GmPathCanvas : Control
{
    private readonly GmPath path;
    private readonly RoomData room;
    private readonly Bitmap background;
    private PointF origin;
    private float scale=1;
    private int selected=-1;
    private bool dragging;
    public event Action SelectionChanged,PointMoved;
    public int Selected {get{return selected;}}
    public GmPathPoint SelectedPoint {get{return selected>=0&&selected<path.points.Count?path.points[selected]:null;}}
    public GmPathCanvas(GmPath path,RoomData room,Bitmap background)
    {this.path=path;this.room=room;this.background=background;DoubleBuffered=true;ResizeRedraw=true;TabStop=true;}
    private void Fit()
    {
        float left=0,top=0,right=room.width,bottom=room.height;
        foreach(var point in path.points){left=Math.Min(left,(float)point.x);top=Math.Min(top,(float)point.y);right=Math.Max(right,(float)point.x);bottom=Math.Max(bottom,(float)point.y);}
        scale=Math.Max(.00001f,Math.Min(Math.Max(1,Width-48)/(right-left),Math.Max(1,Height-48)/(bottom-top)));
        origin=new PointF((Width-scale*(right+left))*.5f,(Height-scale*(bottom+top))*.5f);
    }
    private PointF ScreenPoint(double x,double y){return new PointF(origin.X+(float)x*scale,origin.Y+(float)y*scale);}
    private PointF WorldPoint(Point point){return new PointF((point.X-origin.X)/scale,(point.Y-origin.Y)/scale);}
    public void GeometryChanged(){path.Changed();Invalidate();}
    public void Remove()
    {
        if(SelectedPoint==null)return;
        path.points.RemoveAt(selected);Select(Math.Min(selected,path.points.Count-1));GeometryChanged();
    }
    private void Select(int index){selected=index;if(SelectionChanged!=null)SelectionChanged();Invalidate();}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);if(!dragging)Fit();
        var g=e.Graphics;Theme.Workspace(g,ClientRectangle);
        var bounds=new RectangleF(origin.X,origin.Y,room.width*scale,room.height*scale);
        g.InterpolationMode=InterpolationMode.NearestNeighbor;
        if(background!=null)g.DrawImage(background,bounds);
        else using(var fill=new SolidBrush(Theme.Back))g.FillRectangle(fill,bounds);
        g.SmoothingMode=SmoothingMode.AntiAlias;
        var vertices=path.Curve.Outline().Select(v=>ScreenPoint(v.X,v.Y)).ToArray();
        using(var pen=new Pen(Color.FromArgb(126,219,242),2))if(vertices.Length>1)g.DrawLines(pen,vertices);
        for(int i=0;i<path.points.Count;i++){
            var p=ScreenPoint(path.points[i].x,path.points[i].y);
            using(var fill=new SolidBrush(i==selected?Theme.Accent:Theme.Text))g.FillEllipse(fill,p.X-5,p.Y-5,10,10);
            if(i==selected||i==0)using(var label=new SolidBrush(Theme.Text))g.DrawString((i+1).ToString(),Theme.Small,label,p.X+7,p.Y+2);
        }
    }
    private int Hit(Point point)
    {
        int best=-1;double distance=100;
        for(int i=0;i<path.points.Count;i++){var p=ScreenPoint(path.points[i].x,path.points[i].y);double dx=p.X-point.X,dy=p.Y-point.Y,squared=dx*dx+dy*dy;if(squared<=distance){distance=squared;best=i;}}
        return best;
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;Focus();Fit();
        int index=Hit(e.Location);
        if(index<0&&e.Clicks>1&&path.points.Count<4096){
            var point=WorldPoint(e.Location);path.points.Add(new GmPathPoint{x=Math.Round(point.X),y=Math.Round(point.Y)});index=path.points.Count-1;GeometryChanged();
        }
        Select(index);if(index>=0&&e.Clicks==1){dragging=true;Capture=true;}
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);if(!dragging||SelectedPoint==null)return;
        var point=WorldPoint(e.Location);double snap=(ModifierKeys&Keys.Shift)!=0?16:1;
        SelectedPoint.x=Math.Round(point.X/snap)*snap;SelectedPoint.y=Math.Round(point.Y/snap)*snap;
        GeometryChanged();if(PointMoved!=null)PointMoved();
    }
    protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(dragging){dragging=false;Capture=false;Invalidate();}}
    protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture){dragging=false;Invalidate();}}
    protected override void Dispose(bool disposing){if(disposing&&background!=null)background.Dispose();base.Dispose(disposing);}
}
