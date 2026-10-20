using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

internal static class Theme
{
    public static readonly Color Back=Color.FromArgb(19,23,31),Panel=Color.FromArgb(26,31,42),Raised=Color.FromArgb(35,42,56),Line=Color.FromArgb(48,57,75),Text=Color.FromArgb(225,231,241),Muted=Color.FromArgb(141,155,179),Accent=Color.FromArgb(242,113,153);
    public static readonly Font Font=new Font("Microsoft YaHei UI",9f),Small=new Font("Microsoft YaHei UI",8f),Bold=new Font("Microsoft YaHei UI",9f,FontStyle.Bold);
    public static Color TrackColor(string kind){return kind=="rod"?Color.FromArgb(175,130,230):kind=="emitter"?Color.FromArgb(218,96,140):kind=="effect"?Color.FromArgb(206,165,83):kind=="script"?Color.FromArgb(75,170,146):Color.FromArgb(83,144,210);}
    public static string KindName(string kind){return kind=="rod"?"转转棒":kind=="emitter"?"弹幕":kind=="object"?"对象":kind=="effect"?"特效":"GML";}
    public static bool Spatial(string kind){return kind=="rod"||kind=="emitter"||kind=="object";}
    public static void Label(Graphics g,string text,Rectangle bounds,Color color,Font font=null,TextFormatFlags flags=TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis)
    {TextRenderer.DrawText(g,text,font??Font,bounds,color,flags|TextFormatFlags.NoPrefix);}
}

internal sealed class StageCanvas : Control
{
    public EditorState State;public RoomData Room;public SpriteStore Sprites;
    public HashSet<Track> SelectedTracks=new HashSet<Track>();
    public Action<Track[],Track> SelectTracks;
    private bool selectingBox;private PointF boxFirst,boxLast;private HashSet<Track> boxInitial;
    private readonly Dictionary<Track,Dictionary<string,double>> positions=new Dictionary<Track,Dictionary<string,double>>();
    private PointF pointerDown;
    public Track SelectedTrack;public Keyframe SelectedKey;public RoomInstance SelectedInstance;
    public int Frame;public bool Grid=true,Hitboxes;
    public Action<Track,Keyframe> SelectTrack;public Action<RoomInstance> SelectInstance;
    public Action BeginEdit,Changed;public Action<RoomInstance> InstanceMoved;
    public Action<string,string,PointF> ResourceDropped;public Action<PointF> PointerMoved;
    public System.Collections.Generic.Dictionary<string,string> ObjectSprites=new System.Collections.Generic.Dictionary<string,string>();
    public double ViewZoom=1,ViewX,ViewY;
    private bool panning;private Point panStart;private double panX,panY;
    private RectangleF view;private Track draggingTrack;private RoomInstance draggingInstance;private string handle;
    private Point down;private bool moved;
    private EditorState renderState;private RoomData renderRoom;private System.Collections.Generic.Dictionary<string,string> renderObjects;
    private string renderProject;private System.Collections.Generic.Dictionary<string,SpriteMeta> renderMetadata;
    private Bitmap image;private bool rendering;private int serial,finished=-1;
    private SpriteStore workerSprites;private readonly PreviewCache previewCache=new PreviewCache();
    private SpriteStore assetOwner;private int assetVersion,workerAssetVersion=-1;
    public double LastRenderMilliseconds {get;private set;}
    public StageCanvas(){DoubleBuffered=true;ResizeRedraw=true;BackColor=Theme.Back;AllowDrop=true;TabStop=true;}
    public void SceneChanged()
    {
        if(State==null||Room==null||Sprites==null)return;
        if(assetOwner!=Sprites){assetOwner=Sprites;assetVersion++;}
        renderState=new EditorState{version=State.version,fps=State.fps,bpm=State.bpm,duration=State.duration,room=State.room,music=State.music,timelineInitialized=State.timelineInitialized,tracks=State.tracks.Select(t=>t.Snapshot()).ToList()};renderRoom=Room.Snapshot();renderObjects=ObjectSprites;renderProject=Sprites.Project;renderMetadata=Sprites.Metadata;serial++;QueueRender();Invalidate();
    }
    public void ShowFrame(int frame){if(Frame==frame&&image!=null)return;Frame=frame;serial++;QueueRender();Invalidate();}
    public void DisplayOptionsChanged(){serial++;QueueRender();Invalidate();}
    private async void QueueRender()
    {
        if(rendering||renderState==null||!IsHandleCreated||IsDisposed||finished==serial)return;
        rendering=true;int request=serial,frame=Frame,assets=assetVersion;var state=renderState;var room=renderRoom;var objects=renderObjects;string project=renderProject;var metadata=renderMetadata;bool grid=Grid,hitboxes=Hitboxes;
        var clock=System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var next=await System.Threading.Tasks.Task.Run(()=>{if(workerSprites==null||workerSprites.Project!=project||workerAssetVersion!=assets){if(workerSprites!=null)workerSprites.Dispose();workerSprites=new SpriteStore{Project=project};workerAssetVersion=assets;previewCache.Clear();}workerSprites.Metadata=metadata;return PreviewRenderer.Render(state,room,objects,workerSprites,frame,grid,hitboxes,previewCache);});
            LastRenderMilliseconds=clock.Elapsed.TotalMilliseconds;
            if(IsDisposed){next.Dispose();return;}var previous=image;image=next;if(previous!=null)previous.Dispose();finished=request;Invalidate();
        }
        catch(Exception error){if(!IsDisposed){System.Diagnostics.Debug.WriteLine(error);finished=request;}}
        finally{rendering=false;if(IsDisposed){if(workerSprites!=null){workerSprites.Dispose();workerSprites=null;}previewCache.Dispose();}else if(finished!=serial)QueueRender();}
    }
    private void Fit()
    {
        float rw=Room==null?800:Room.width,rh=Room==null?608:Room.height;
        float scale=Math.Max(.01f,Math.Min((Width-48)/rw,(Height-48)/rh))*(float)ViewZoom;
        view=new RectangleF(Width/2f-(rw/2+(float)ViewX)*scale,Height/2f-(rh/2+(float)ViewY)*scale,rw*scale,rh*scale);
    }
    public void SetView(double zoom,double x,double y){ViewZoom=Math.Max(.1,Math.Min(32,zoom));ViewX=x;ViewY=y;Fit();Invalidate();}
    public void ResetView(){SetView(1,0,0);}
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if((ModifierKeys&Keys.Control)==0||!ContainsFocus){base.OnMouseWheel(e);return;}
        var before=Position(e.Location);ViewZoom=Math.Max(.1,Math.Min(32,ViewZoom*Math.Pow(1.2,e.Delta/120.0)));Fit();
        float rw=Room==null?800:Room.width,rh=Room==null?608:Room.height,scale=view.Width/rw;
        ViewX=before.X-rw/2-(e.X-Width/2f)/scale;ViewY=before.Y-rh/2-(e.Y-Height/2f)/scale;Fit();Invalidate();
    }
    protected override void OnMouseDoubleClick(MouseEventArgs e){base.OnMouseDoubleClick(e);if(e.Button==MouseButtons.Middle){panning=false;ResetView();}}
    private PointF Position(Point p){Fit();return new PointF((p.X-view.Left)/view.Width*(Room==null?800:Room.width),(p.Y-view.Top)/view.Height*(Room==null?608:Room.height));}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);Fit();var g=e.Graphics;g.Clear(Color.FromArgb(203,211,222));
        if(image!=null){g.InterpolationMode=InterpolationMode.NearestNeighbor;g.DrawImage(image,view);}else{Theme.Label(g,"正在绘制房间…",ClientRectangle,Theme.Muted,null,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);QueueRender();}
        if(State==null||Room==null)return;
        var saved=g.Save();g.SetClip(view);g.TranslateTransform(view.Left,view.Top);g.ScaleTransform(view.Width/Room.width,view.Height/Room.height);g.SmoothingMode=SmoothingMode.AntiAlias;
        if(SelectedInstance!=null&&Sprites!=null)
        {string sprite;if(ObjectSprites.TryGetValue(SelectedInstance.name,out sprite)){var r=Sprites.Bounds(sprite,SelectedInstance.x,SelectedInstance.y);using(var pen=new Pen(Theme.Accent,2))g.DrawRectangle(pen,r.X,r.Y,r.Width,r.Height);}}
        foreach(var item in SelectedTracks.Where(t=>t!=SelectedTrack&&Theme.Spatial(t.kind)))
        {var q=Motion.At(item,Frame);using(var pen=new Pen(Theme.Accent,2))g.DrawRectangle(pen,(float)q.x-16,(float)q.y-16,32,32);}
        if(selectingBox)
        {var r=RectangleF.FromLTRB(Math.Min(boxFirst.X,boxLast.X),Math.Min(boxFirst.Y,boxLast.Y),Math.Max(boxFirst.X,boxLast.X),Math.Max(boxFirst.Y,boxLast.Y));using(var fill=new SolidBrush(Color.FromArgb(40,Theme.Accent)))g.FillRectangle(fill,r);using(var pen=new Pen(Theme.Accent,1.5f))g.DrawRectangle(pen,r.X,r.Y,r.Width,r.Height);}
        if(SelectedTrack!=null&&Theme.Spatial(SelectedTrack.kind))
        {
            var p=Motion.At(SelectedTrack,Frame);
            using(var pen=new Pen(Theme.Accent,1.5f))
            {
                g.DrawEllipse(pen,(float)p.x-14,(float)p.y-14,28,28);g.DrawLine(pen,(float)p.x-23,(float)p.y,(float)p.x+23,(float)p.y);g.DrawLine(pen,(float)p.x,(float)p.y-23,(float)p.x,(float)p.y+23);
                if(SelectedTrack.kind=="rod"){var ends=Motion.Endpoints(SelectedTrack,Frame);g.DrawLine(pen,ends[0],ends[1]);foreach(var point in ends){g.FillRectangle(Brushes.White,point.X-5,point.Y-5,10,10);g.DrawRectangle(pen,point.X-6,point.Y-6,12,12);}}
            }
        }
        g.Restore(saved);using(var pen=new Pen(Theme.Line))g.DrawRectangle(pen,view.X,view.Y,view.Width,view.Height);
                using(var brush=new SolidBrush(Color.FromArgb(215,Theme.Panel)))g.FillRectangle(brush,8,Height-31,Math.Min(Width-16,420),24);
        Theme.Label(g,Room.name+" · "+Frame+" F · "+(view.Width/Room.width*100).ToString("0")+"% · Ctrl+滚轮 / 中键平移",new Rectangle(14,Height-31,Math.Max(1,Width-28),24),Theme.Text,Theme.Small);
    }
    private static bool Near(PointF a,PointF b,double radius){return Math.Abs(a.X-b.X)<radius&&Math.Abs(a.Y-b.Y)<radius;}
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);if(State==null||Room==null)return;Focus();if(e.Button==MouseButtons.Middle){panning=true;panStart=e.Location;panX=ViewX;panY=ViewY;Cursor=Cursors.Hand;Capture=true;return;}var p=Position(e.Location);down=e.Location;pointerDown=p;moved=false;positions.Clear();
        if(SelectedTracks.Count<=1&&SelectedTrack!=null&&SelectedTrack.kind=="rod")
        {var ends=Motion.Endpoints(SelectedTrack,Frame);for(int i=0;i<2;i++)if(Near(ends[i],p,14)){draggingTrack=SelectedTrack;handle=i==0?"first":"second";if(e.Button==MouseButtons.Left){if(BeginEdit!=null)BeginEdit();Capture=true;}else draggingTrack=null;return;}}
        foreach(var t in State.tracks.Where(t=>t.enabled&&Theme.Spatial(t.kind)&&Frame>=t.start&&Frame<=t.end).Reverse())
        {
            var q=Motion.At(t,Frame);bool hit=Near(new PointF((float)q.x,(float)q.y),p,20);
            if(t.kind=="rod")hit=hit||Motion.RodPoints(t,Frame).Any(point=>Near(point,p,14));
            if(!hit)continue;
            bool additive=(ModifierKeys&(Keys.Control|Keys.Shift))!=0;
            if(additive){var group=new HashSet<Track>(SelectedTracks);if(!group.Add(t))group.Remove(t);if(SelectTracks!=null)SelectTracks(group.ToArray(),t);}
            else if(!SelectedTracks.Contains(t)){if(SelectTrack!=null)SelectTrack(t,null);}
            if(e.Button==MouseButtons.Left&&SelectedTracks.Contains(t))
            {
                draggingTrack=t;handle="center";
                foreach(var item in SelectedTracks.Where(item=>Theme.Spatial(item.kind)))positions[item]=new[]{"x","y","x1","y1","x2","y2"}.ToDictionary(field=>field,field=>Motion.Value(item,field,Frame));
                if(BeginEdit!=null)BeginEdit();Capture=true;
            }
            return;
        }
        foreach(var instance in Room.instances.AsEnumerable().Reverse())
        {string name;if(!ObjectSprites.TryGetValue(instance.name,out name)||String.IsNullOrEmpty(name))continue;if(Sprites.Bounds(name,instance.x,instance.y).Contains(p)){if(SelectInstance!=null)SelectInstance(instance);if(e.Button==MouseButtons.Left){if(BeginEdit!=null)BeginEdit();draggingInstance=instance;Capture=true;}return;}}
        if(e.Button==MouseButtons.Left&&view.Contains(e.Location))
        {selectingBox=true;boxFirst=boxLast=p;boxInitial=(ModifierKeys&(Keys.Control|Keys.Shift))!=0?new HashSet<Track>(SelectedTracks):new HashSet<Track>();Capture=true;}
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);if(Room==null)return;if(panning){Fit();double scale=view.Width/Room.width;ViewX=panX-(e.X-panStart.X)/scale;ViewY=panY-(e.Y-panStart.Y)/scale;Fit();Invalidate();return;}var p=Position(e.Location);if(PointerMoved!=null)PointerMoved(p);
        if(selectingBox)
        {
            boxLast=p;var r=RectangleF.FromLTRB(Math.Min(boxFirst.X,p.X),Math.Min(boxFirst.Y,p.Y),Math.Max(boxFirst.X,p.X),Math.Max(boxFirst.Y,p.Y));SelectedTracks.Clear();SelectedTracks.UnionWith(boxInitial);
            foreach(var t in State.tracks.Where(t=>t.enabled&&Theme.Spatial(t.kind)&&Frame>=t.start&&Frame<=t.end))
            {var q=Motion.At(t,Frame);if(r.Contains((float)q.x,(float)q.y)||t.kind=="rod"&&Motion.RodPoints(t,Frame).Any(point=>r.Contains(point)))SelectedTracks.Add(t);}
            Invalidate();return;
        }
        if(draggingTrack==null&&draggingInstance==null)return;
        if(!moved&&Math.Abs(e.X-down.X)+Math.Abs(e.Y-down.Y)<3)return;moved=true;double x=Math.Round(p.X),y=Math.Round(p.Y);if((ModifierKeys&Keys.Shift)!=0){x=Math.Round(x/16)*16;y=Math.Round(y/16)*16;}
        if(draggingTrack!=null)
        {
            var t=draggingTrack;
            if(handle=="center")
            {
                double dx=Math.Round(p.X-pointerDown.X),dy=Math.Round(p.Y-pointerDown.Y);if((ModifierKeys&Keys.Shift)!=0){dx=Math.Round(dx/16)*16;dy=Math.Round(dy/16)*16;}
                foreach(var pair in positions)
                {var item=pair.Key;string[] fields=item.kind=="rod"&&item.rodMode=="endpoints"?new[]{"x1","y1","x2","y2"}:item.kind=="rod"&&item.rodMode=="anchor"?new[]{"x","y","x2","y2"}:new[]{"x","y"};foreach(string field in fields)Motion.EditValue(item,field,Frame,pair.Value[field]+(field.StartsWith("x")?dx:dy));}
            }
            else if(t.rodMode=="center")
            {var center=Motion.At(t,Frame);double dx=x-center.x,dy=y-center.y;Motion.EditValue(t,"length",Frame,2*Math.Sqrt(dx*dx+dy*dy));Motion.EditValue(t,"angle",Frame,Math.Atan2(-dy,dx)*180/Math.PI+(handle=="first"?180:0)-t.angularSpeed*Math.Max(0,Frame-t.start));}
            else{string px=handle=="first"?(t.rodMode=="anchor"?"x":"x1"):"x2",py=handle=="first"?(t.rodMode=="anchor"?"y":"y1"):"y2";Motion.EditValue(t,px,Frame,x);Motion.EditValue(t,py,Frame,y);}
        }
        else{draggingInstance.x=x;draggingInstance.y=y;}SceneChanged();
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {base.OnMouseUp(e);if(panning||e.Button==MouseButtons.Middle){panning=false;Capture=false;Cursor=Cursors.Default;return;}if(selectingBox){selectingBox=false;var chosen=SelectedTracks.ToArray();if(SelectTracks!=null)SelectTracks(chosen,chosen.FirstOrDefault());Invalidate();}Capture=false;if(moved&&draggingTrack!=null&&Changed!=null)Changed();if(moved&&draggingInstance!=null&&InstanceMoved!=null)InstanceMoved(draggingInstance);draggingTrack=null;draggingInstance=null;}
    protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture&&panning){panning=false;Cursor=Cursors.Default;}}
    protected override void OnDragEnter(DragEventArgs e){if(e.Data.GetDataPresent(DataFormats.Text))e.Effect=DragDropEffects.Copy;}
    protected override void OnDragDrop(DragEventArgs e){var text=e.Data.GetData(DataFormats.Text) as string;if(text==null||ResourceDropped==null)return;var parts=text.Split('|');if(parts.Length==2)ResourceDropped(parts[0],parts[1],Position(PointToClient(new Point(e.X,e.Y))));}
    protected override void Dispose(bool disposing){if(disposing){if(image!=null){image.Dispose();image=null;}if(!rendering){if(workerSprites!=null){workerSprites.Dispose();workerSprites=null;}previewCache.Dispose();}}base.Dispose(disposing);}
}
