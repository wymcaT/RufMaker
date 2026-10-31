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
    public static Color TrackColor(string kind){return kind=="variable"?Color.FromArgb(95,190,177):kind=="native"?Color.FromArgb(97,165,130):kind=="camera"?Color.FromArgb(245,203,104):kind=="rod"?Color.FromArgb(175,130,230):kind=="emitter"?Color.FromArgb(218,96,140):kind=="effect"?Color.FromArgb(206,165,83):kind=="script"?Color.FromArgb(75,170,146):Color.FromArgb(83,144,210);}
    public static string KindName(string kind){return kind=="variable"?"全局变量":kind=="group"?"分组":kind=="control"?"弹幕控制":kind=="pattern"?"弹幕片段":kind=="camera"?"拉镜":kind=="rod"?"整体对象":kind=="emitter"?"弹幕":kind=="object"?"对象":kind=="effect"?"特效":"GML";}
    public static bool Visual(string kind){return kind=="rod"||kind=="emitter"||kind=="object";}
    public static bool Spatial(string kind){return kind=="group"||kind=="camera"||kind=="rod"||kind=="emitter"||kind=="object";}
    public static void Workspace(Graphics g,Rectangle bounds)
    {
        using(var tile=new Bitmap(24,24)){
            using(var t=Graphics.FromImage(tile)){t.Clear(Color.FromArgb(53,55,60));using(var b=new SolidBrush(Color.FromArgb(65,67,72))){t.FillRectangle(b,0,0,12,12);t.FillRectangle(b,12,12,12,12);}}
            using(var brush=new TextureBrush(tile))g.FillRectangle(brush,bounds);
        }
    }
    public static void Label(Graphics g,string text,Rectangle bounds,Color color,Font font=null,TextFormatFlags flags=TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis,bool localize=true)
    {TextRenderer.DrawText(g,localize?Language.Text(text):text,font??Font,bounds,color,flags|TextFormatFlags.NoPrefix);}
}

internal sealed partial class StageCanvas : Control
{
    public EditorState State;public RoomData Room;public SpriteStore Sprites;
    public HashSet<Track> SelectedTracks=new HashSet<Track>();
    public Action<Track[],Track> SelectTracks;
    private bool selectingBox;private PointF boxFirst,boxLast;private HashSet<Track> boxInitial;
    private readonly Dictionary<Track,Dictionary<string,double>> positions=new Dictionary<Track,Dictionary<string,double>>();
    private readonly Dictionary<Track,MotionPresetData> motionPositions=new Dictionary<Track,MotionPresetData>();
    private readonly Dictionary<Track,CameraPresetData> cameraPositions=new Dictionary<Track,CameraPresetData>();
    private PointF pointerDown;
    public Track SelectedTrack;public Keyframe SelectedKey;public RoomInstance SelectedInstance;
    public int Frame;public bool Grid=true,Hitboxes,RenderCamera;
    public string NativeFilmPath,TemplatePreviewId;
    public NativeFilmInfo NativeFilmInfo;
    public bool EditNativeTest;public string NativeTestSprite;public Action NativeTestPreview,NativeTestChanged;private bool draggingNativeTest;
    public HashSet<string> NativeFilmIds=new HashSet<string>();
    public Action<Track,Keyframe> SelectTrack;public Action<RoomInstance> SelectInstance;
    public Action BeginEdit,Changed;public Action<RoomInstance> InstanceMoved;
    public Action<string,string,PointF> ResourceDropped;public Action<PointF> PointerMoved;
    public System.Collections.Generic.Dictionary<string,string> ObjectSprites=new System.Collections.Generic.Dictionary<string,string>();
    public double ViewZoom=1,ViewX,ViewY;
    private bool panning;private Point panStart;private double panX,panY;
    private RectangleF view;private Track draggingTrack;private RoomInstance draggingInstance;private string handle;
    private Point down;private bool moved;
    private sealed class PickTarget {public Track track;public RoomInstance instance;}
    private List<PickTarget> pendingPicks;private bool pendingAdditive;private ContextMenuStrip pickMenu;
    private EditorState renderState;private RoomData renderRoom;private System.Collections.Generic.Dictionary<string,string> renderObjects;
    private string renderProject;private System.Collections.Generic.Dictionary<string,SpriteMeta> renderMetadata;
    private Bitmap image;private bool rendering;private int serial,finished=-1;
    private System.Threading.CancellationTokenSource renderCancellation;
    private string lastRenderFailure;
    public Action<Exception> RenderFailed;
    public int DisplayedFrame {get;private set;}
    private SpriteStore workerSprites;private readonly PreviewCache previewCache=new PreviewCache();
    private SpriteStore assetOwner;private int assetVersion,workerAssetVersion=-1;
    public Action<ObjectStatisticsSnapshot> StatisticsReady;public Action<int> StatisticsPending;
    public double LastRenderMilliseconds {get;private set;}
    public StageCanvas(){DoubleBuffered=true;ResizeRedraw=true;BackColor=Theme.Back;AllowDrop=true;TabStop=true;}
    public void SceneChanged()
    {
        if(State==null||Room==null||Sprites==null)return;
        RandomParameters.Attach(State);
        if(assetOwner!=Sprites){assetOwner=Sprites;assetVersion++;}
        renderState=new EditorState{timelineGroups=State.timelineGroups.Select(g=>g.Snapshot()).ToList(),nativeProject=State.nativeProject==null?null:State.nativeProject.Snapshot(),version=State.version,fps=State.fps,bpm=State.bpm,duration=State.duration,room=State.room,music=State.music,timelineInitialized=State.timelineInitialized,randomSeed=State.randomSeed,usePlayerTrace=State.usePlayerTrace,playerTrace=State.playerTrace.Select(p=>p.Snapshot()).ToList(),nativeTest=new NativeTestPosition{x=State.nativeTest.x,y=State.nativeTest.y},tracks=State.tracks.Select(t=>t.Snapshot()).ToList()};
        if(!String.IsNullOrEmpty(TemplatePreviewId)){renderState.nativeProject=null;var visible=new HashSet<string>{TemplatePreviewId};bool added;do{added=false;foreach(var child in renderState.tracks)if(visible.Contains(child.parentId))added|=visible.Add(child.id);}while(added);foreach(var track in renderState.tracks){track.templateOnly=!visible.Contains(track.id);if(track.templateOnly&&(track.kind=="camera"||track.kind=="effect"))track.enabled=false;if(track.id==TemplatePreviewId){track.enabled=true;track.parentId="";}}}
        renderState=BulletSystem.Expand(renderState);RandomParameters.Attach(renderState);renderRoom=Room.Snapshot();renderObjects=ObjectSprites;renderProject=Sprites.Project;renderMetadata=Sprites.Metadata;RequestRender();
    }
    private void RequestRender(){serial++;if(renderCancellation!=null)renderCancellation.Cancel();QueueRender();Invalidate();}
    public void ShowFrame(int frame){if(Frame==frame&&image!=null)return;Frame=frame;RequestRender();}
    public void DisplayOptionsChanged(){RequestRender();}
    private async void QueueRender()
    {
        if(rendering||renderState==null||!IsHandleCreated||IsDisposed||finished==serial)return;
        if(StatisticsPending!=null)StatisticsPending(Frame);ObjectStatisticsSnapshot statistics=null;
        rendering=true;int request=serial,frame=Frame,assets=assetVersion;var state=renderState;var room=renderRoom;var objects=renderObjects;string project=renderProject,film=String.IsNullOrEmpty(TemplatePreviewId)?NativeFilmPath:null;var metadata=renderMetadata;bool grid=Grid,hitboxes=Hitboxes;var cameraPose=RenderCamera?Camera.At(Camera.Active(state,frame),frame,room):null;Fit();var transform=DisplayMatrix();int width=Math.Max(1,Width),height=Math.Max(1,Height);RectangleF frameBounds=view;RectangleF? clip=RenderCamera?(RectangleF?)view:null;
        var clock=System.Diagnostics.Stopwatch.StartNew();var cancellation=new System.Threading.CancellationTokenSource();renderCancellation=cancellation;
        try
        {
            var next=await System.Threading.Tasks.Task.Run(()=>{previewCache.Cancelled=()=>cancellation.IsCancellationRequested;previewCache.ThrowIfCancelled();if(workerSprites==null||workerSprites.Project!=project||workerAssetVersion!=assets){if(workerSprites!=null)workerSprites.Dispose();workerSprites=new SpriteStore{Project=project};workerAssetVersion=assets;previewCache.Clear();}workerSprites.Metadata=metadata;var bitmap=PreviewRenderer.Render(state,room,objects,workerSprites,frame,grid,hitboxes,previewCache,transform,width,height,clip,frameBounds,film,cameraPose);try{previewCache.ThrowIfCancelled();statistics=ObjectStatistics.Capture(state,room,frame,previewCache);return bitmap;}catch{bitmap.Dispose();throw;}});
            LastRenderMilliseconds=clock.Elapsed.TotalMilliseconds;
            if(IsDisposed||cancellation.IsCancellationRequested||request!=serial||state!=renderState||frame!=Frame){next.Dispose();finished=request;return;}lastRenderFailure=null;if(StatisticsReady!=null)StatisticsReady(statistics);var previous=image;image=next;DisplayedFrame=frame;if(previous!=null)previous.Dispose();finished=request;Invalidate();
        }
        catch(OperationCanceledException){finished=request;}
        catch(Exception error){previewCache.ClearHistory();if(!IsDisposed){System.Diagnostics.Debug.WriteLine(error);finished=request;string key=error.GetType().FullName+error.Message;if(request==serial&&key!=lastRenderFailure){lastRenderFailure=key;if(RenderFailed!=null)RenderFailed(error);}}}
        finally{transform.Dispose();renderCancellation=null;cancellation.Dispose();rendering=false;if(IsDisposed){if(workerSprites!=null){workerSprites.Dispose();workerSprites=null;}previewCache.Dispose();}else if(finished!=serial)QueueRender();}
    }
    private void Fit()
    {
        float rw=Room==null?800:Room.width,rh=Room==null?608:Room.height;
        float scale=Math.Max(.01f,Math.Min((Width-48)/rw,(Height-48)/rh))*(float)ViewZoom;
        view=new RectangleF(Width/2f-(rw/2+(float)ViewX)*scale,Height/2f-(rh/2+(float)ViewY)*scale,rw*scale,rh*scale);
    }
    public void SetView(double zoom,double x,double y){ViewZoom=Math.Max(.1,Math.Min(32,zoom));ViewX=x;ViewY=y;Fit();DisplayOptionsChanged();}
    public void ResetView(){SetView(1,0,0);}
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if((ModifierKeys&Keys.Control)==0||!ContainsFocus){base.OnMouseWheel(e);return;}
        var before=ViewPosition(e.Location);ViewZoom=Math.Max(.1,Math.Min(32,ViewZoom*Math.Pow(1.2,e.Delta/120.0)));Fit();
        float rw=Room==null?800:Room.width,rh=Room==null?608:Room.height,scale=view.Width/rw;
        ViewX=before.X-rw/2-(e.X-Width/2f)/scale;ViewY=before.Y-rh/2-(e.Y-Height/2f)/scale;Fit();DisplayOptionsChanged();
    }
    protected override void OnMouseDoubleClick(MouseEventArgs e){base.OnMouseDoubleClick(e);if(e.Button==MouseButtons.Middle){panning=false;ResetView();}}
    private PointF ViewPosition(Point p){Fit();return new PointF((p.X-view.Left)/view.Width*(Room==null?800:Room.width),(p.Y-view.Top)/view.Height*(Room==null?608:Room.height));}
    private Matrix DisplayMatrix()
    {
        Fit();float scale=view.Width/(Room==null?800:Room.width);var m=new Matrix(scale,0,0,scale,view.Left,view.Top);
        if(RenderCamera&&State!=null&&Room!=null)using(var camera=Camera.At(Camera.Active(String.IsNullOrEmpty(TemplatePreviewId)?State:renderState??State,Frame),Frame,Room).WorldToView(Room.width,Room.height))m.Multiply(camera,MatrixOrder.Prepend);return m;
    }
    private NativeViewPose NativeView {get{return !String.IsNullOrEmpty(TemplatePreviewId)||NativeFilmPath==null||NativeFilmInfo==null||State==null?null:NativeFilmInfo.View((int)Math.Round(Frame*NativeFilmInfo.fps/(double)Math.Max(1,State.fps)));}}
    private Matrix WorldDisplayMatrix(){var m=DisplayMatrix();var pose=NativeView;if(pose!=null&&Room!=null)using(var native=pose.WorldToRoom(Room.width,Room.height))m.Multiply(native,MatrixOrder.Prepend);return m;}
    private PointF TrackPoint(Track track,PointF point){var pose=NativeView;if(track.kind=="camera"&&pose!=null){var points=new[]{point};using(var m=pose.WorldToRoom(Room.width,Room.height))m.TransformPoints(points);return points[0];}return point;}
    private PointF WorldPoint(Track track,PointF point){var pose=NativeView;if(track.kind=="camera"&&pose!=null){var points=new[]{point};using(var m=pose.WorldToRoom(Room.width,Room.height)){m.Invert();m.TransformPoints(points);}return points[0];}return point;}
    private PointF Position(Point p){var points=new[]{new PointF(p.X,p.Y)};using(var m=WorldDisplayMatrix()){m.Invert();m.TransformPoints(points);}return points[0];}
    protected override void OnResize(EventArgs e){base.OnResize(e);if(previewCache!=null)DisplayOptionsChanged();}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);Fit();var g=e.Graphics;Theme.Workspace(g,ClientRectangle);
        if(image!=null){g.InterpolationMode=InterpolationMode.NearestNeighbor;g.DrawImageUnscaled(image,0,0);}else{Theme.Label(g,"正在绘制房间…",ClientRectangle,Theme.Muted,null,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);QueueRender();}
        if(State==null||Room==null)return;
        var saved=g.Save();if(RenderCamera)g.SetClip(view);using(var m=DisplayMatrix())g.Transform=m;g.SmoothingMode=SmoothingMode.AntiAlias;
        foreach(var camera in State.tracks.Where(t=>t.enabled&&t.kind=="camera"&&(t==SelectedTrack||Frame>=t.start&&Frame<=t.end)))
        {var corners=Camera.At(camera,Frame,Room).Corners(Room.width,Room.height);using(var pen=new Pen(camera==SelectedTrack?Theme.Accent:Color.FromArgb(245,203,104),2)){pen.DashStyle=DashStyle.Dash;g.DrawPolygon(pen,corners);}using(var brush=new SolidBrush(Color.FromArgb(245,203,104)))g.DrawString(camera.name+" · 游戏显示范围",Theme.Small,brush,corners[0]);}
        using(var m=WorldDisplayMatrix())g.Transform=m;
        if(EditNativeTest&&State.nativeTest!=null){float x=(float)State.nativeTest.x,y=(float)State.nativeTest.y;
            if(Sprites!=null&&!String.IsNullOrEmpty(NativeTestSprite))Sprites.Draw(g,NativeTestSprite,x,y,1,0,.8,Color.White);
            using(var pen=new Pen(Color.Cyan,1.5f)){g.DrawRectangle(pen,x-12,y-16,24,32);g.DrawLine(pen,x-20,y,x+20,y);g.DrawLine(pen,x,y-23,x,y+23);}using(var brush=new SolidBrush(Color.Cyan))g.DrawString("试玩玩家 · "+x.ToString("0.##")+", "+y.ToString("0.##"),Theme.Small,brush,x+15,y-20);
        }
        PaintPlayerTrace(g);
        if(SelectedInstance!=null&&Sprites!=null)
        {string sprite;if(ObjectSprites.TryGetValue(SelectedInstance.name,out sprite)){var r=Sprites.Bounds(sprite,SelectedInstance.x,SelectedInstance.y);using(var pen=new Pen(Theme.Accent,2))g.DrawRectangle(pen,r.X,r.Y,r.Width,r.Height);}}
        foreach(var item in SelectedTracks.Where(t=>t!=SelectedTrack&&Theme.Spatial(t.kind)))
        {using(var m=item.kind=="camera"?DisplayMatrix():WorldDisplayMatrix())g.Transform=m;var q=Motion.At(item,Frame);using(var pen=new Pen(Theme.Accent,2))g.DrawRectangle(pen,(float)q.x-16,(float)q.y-16,32,32);}
        using(var m=WorldDisplayMatrix())g.Transform=m;
        if(selectingBox)
        {var r=RectangleF.FromLTRB(Math.Min(boxFirst.X,boxLast.X),Math.Min(boxFirst.Y,boxLast.Y),Math.Max(boxFirst.X,boxLast.X),Math.Max(boxFirst.Y,boxLast.Y));using(var fill=new SolidBrush(Color.FromArgb(40,Theme.Accent)))g.FillRectangle(fill,r);using(var pen=new Pen(Theme.Accent,1.5f))g.DrawRectangle(pen,r.X,r.Y,r.Width,r.Height);}
        if(SelectedTrack!=null&&Theme.Spatial(SelectedTrack.kind))
        {
            using(var m=SelectedTrack.kind=="camera"?DisplayMatrix():WorldDisplayMatrix())g.Transform=m;
            var p=Motion.At(SelectedTrack,Frame);
            using(var pen=new Pen(Theme.Accent,1.5f))
            {
                g.DrawEllipse(pen,(float)p.x-14,(float)p.y-14,28,28);g.DrawLine(pen,(float)p.x-23,(float)p.y,(float)p.x+23,(float)p.y);g.DrawLine(pen,(float)p.x,(float)p.y-23,(float)p.x,(float)p.y+23);
                if(SelectedTrack.kind=="rod"&&SelectedTrack.shape=="rod"){var ends=Motion.Endpoints(SelectedTrack,Frame);g.DrawLine(pen,ends[0],ends[1]);foreach(var point in ends){g.FillRectangle(Brushes.White,point.X-5,point.Y-5,10,10);g.DrawRectangle(pen,point.X-6,point.Y-6,12,12);}}
            }
        }
        PaintCurve(g);g.Restore(saved);using(var pen=new Pen(Theme.Line))g.DrawRectangle(pen,view.X,view.Y,view.Width,view.Height);
                using(var brush=new SolidBrush(Color.FromArgb(215,Theme.Panel)))g.FillRectangle(brush,8,Height-31,Math.Min(Width-16,420),24);
        Theme.Label(g,Room.name+" · "+Frame+" F · "+(view.Width/Room.width*100).ToString("0")+Language.Text("% · Ctrl+滚轮 / 中键平移"),new Rectangle(14,Height-31,Math.Max(1,Width-28),24),Theme.Text,Theme.Small,localize:false);
    }
    private static bool Near(PointF a,PointF b,double radius){return Math.Abs(a.X-b.X)<radius&&Math.Abs(a.Y-b.Y)<radius;}
    private void PaintPlayerTrace(Graphics g)
    {
        if (State == null) return;
        if (image == null)
        {
            PreviewRenderer.DrawPlayer(g, State, Sprites, Frame, Hitboxes);
        }

        if (SelectedTrack != null && Theme.Spatial(SelectedTrack.kind))
        {
            int currentFrame = (image != null && DisplayedFrame >= 0) ? DisplayedFrame : Frame;
            var p = Motion.At(SelectedTrack, currentFrame);
            bool isAiming = SelectedTrack.emissionFacing == "aim" ||
                            SelectedTrack.flightMode == "aim" ||
                            SelectedTrack.flightMode == "turn" ||
                            SelectedTrack.releaseDirection == "aim" ||
                            (SelectedTrack.bulletActions != null && SelectedTrack.bulletActions.Any(a => a.enabled && (a.type == "aim" || a.type == "home"))) ||
                            (!string.IsNullOrEmpty(SelectedTrack.flightExpression) && SelectedTrack.flightExpression.Contains("player.")) ||
                            (SelectedTrack.expressions != null && SelectedTrack.expressions.Values.Any(v => v != null && v.Contains("player.")));

            if (isAiming)
            {
                var targetPlayer = BulletSystem.PlayerAt(State, currentFrame);
                float tx = (float)p.x, ty = (float)p.y;
                float px = (float)targetPlayer.x, py = (float)targetPlayer.y;
                double angle = ExpressionContext.Direction(tx, ty, px, py);
                double dist = Math.Sqrt((px - tx) * (px - tx) + (py - ty) * (py - ty));

                using (var pen = new Pen(Color.FromArgb(230, 255, 170, 0), 1.5f))
                {
                    pen.DashStyle = DashStyle.Dash;
                    g.DrawLine(pen, tx, ty, px, py);
                    float arrowLen = 10f;
                    double rad1 = (angle + 150) * Math.PI / 180.0;
                    double rad2 = (angle - 150) * Math.PI / 180.0;
                    g.DrawLine(pen, px, py, px + (float)(arrowLen * Math.Cos(rad1)), py - (float)(arrowLen * Math.Sin(rad1)));
                    g.DrawLine(pen, px, py, px + (float)(arrowLen * Math.Cos(rad2)), py - (float)(arrowLen * Math.Sin(rad2)));
                }

                using (var brush = new SolidBrush(Color.FromArgb(255, 200, 50)))
                {
                    float midX = (tx + px) / 2f, midY = (ty + py) / 2f;
                    g.DrawString(string.Format("自机狙瞄准 · {0:0.#}° ({1:0}px)", angle, dist), Theme.Small, brush, midX + 8, midY - 8);
                }
            }
        }
    }
    public Track TrackAt(PointF p)
    {
        return PickTargets(p).Select(hit=>hit.track).FirstOrDefault(t=>t!=null);
    }
    private bool SpriteHit(string sprite,double x,double y,double scale,double angle,PointF point)
    {
        if(Sprites==null||String.IsNullOrEmpty(sprite)||Math.Abs(scale)<.000001)return false;
        double dx=(point.X-x)/scale,dy=(point.Y-y)/scale,r=angle*Math.PI/180;
        return Sprites.Bounds(sprite,0,0).Contains((float)(dx*Math.Cos(r)-dy*Math.Sin(r)),(float)(dx*Math.Sin(r)+dy*Math.Cos(r)));
    }
    private List<PickTarget> PickTargets(PointF p)
    {
        var hits=new List<PickTarget>();if(State==null)return hits;
        foreach(var t in State.tracks.Where(t=>Theme.Spatial(t.kind)&&PickActive(t)).Reverse()){
            var q=Motion.At(t,Frame);var point=TrackPoint(t,p);bool hit=Near(new PointF((float)q.x,(float)q.y),point,20);
            if(t.kind=="rod")hit|=Motion.RodPoints(t,Frame).Any(member=>Near(member,p,14)||SpriteHit(t.sprite,member.X,member.Y,q.scale,Motion.Value(t,"spriteAngle",Frame)+(t.rotateSprite?q.angle:0),p));
            else if(Theme.Visual(t.kind))hit|=SpriteHit(t.sprite,q.x,q.y,q.scale,t.kind=="object"?q.angle:0,point);
            if(hit)hits.Add(new PickTarget{track=t});
        }
        if(Room!=null)foreach(var instance in Room.instances.AsEnumerable().Reverse()){
            string sprite;if(NativeFilmPath!=null&&NativeFilmIds.Contains(instance.id)||!ObjectSprites.TryGetValue(instance.name,out sprite))continue;
            if(SpriteHit(sprite,instance.x,instance.y,1,0,p))hits.Add(new PickTarget{instance=instance});
        }
        return hits;
    }
    private bool PickActive(Track t)
    {
        if(String.IsNullOrEmpty(TemplatePreviewId))return BulletSystem.Active(t,Frame);
        for(var parent=t;parent!=null;parent=parent.parent){if(!parent.enabled||Frame<parent.start||Frame>parent.end)return false;if(parent.id==TemplatePreviewId)return true;}return false;
    }
    private void ChooseTarget(PickTarget hit,bool additive,bool drag,bool confirm=false)
    {
        if(hit.track!=null){var t=hit.track;
            if(State==null||!State.tracks.Contains(t))return;
            if(additive){var group=new HashSet<Track>(SelectedTracks);if(!group.Add(t))group.Remove(t);if(SelectTracks!=null)SelectTracks(group.ToArray(),t);}
            else if(confirm||!SelectedTracks.Contains(t)){if(SelectTrack!=null)SelectTrack(t,null);}
            if(drag&&SelectedTracks.Contains(t)){
                draggingTrack=t;handle="center";
                foreach(var item in SelectedTracks.Where(item=>Theme.Spatial(item.kind)&&!HasSelectedParent(item))){positions[item]=new[]{"x","y","x1","y1","x2","y2"}.ToDictionary(field=>field,field=>Motion.Value(item,field,Frame));if(MotionPresets.Bound(item))motionPositions[item]=item.motionPreset.Snapshot();if(CameraPresets.Bound(item))cameraPositions[item]=item.cameraPreset.Snapshot();}
                Capture=true;
            }
        }else if(Room!=null&&Room.instances.Contains(hit.instance)){
            if(SelectInstance!=null)SelectInstance(hit.instance);if(drag){draggingInstance=hit.instance;Capture=true;}
        }
    }
    private ContextMenuStrip CreatePickMenu(List<PickTarget> hits,bool additive)
    {
        var menu=new ContextMenuStrip{Font=Theme.Font,ShowImageMargin=false,ShowCheckMargin=true,MaximumSize=new Size(620,480)};DarkStripRenderer.Apply(menu);
        menu.Items.Add(new ToolStripLabel(String.Format(Language.Text("选择重叠物件（{0}）"),hits.Count)){ForeColor=Theme.Text});menu.Items.Add(new ToolStripSeparator());
        foreach(var hit in hits){var target=hit;string text;
            if(hit.track!=null){var t=hit.track;text=t.name+" · "+String.Format(Language.Text("轨道 {0}"),t.lane+1)+" · "+Language.Text(Theme.KindName(t.kind))+" · #"+(State.tracks.IndexOf(t)+1);}
            else {var i=hit.instance;text=i.name+" · "+Language.Text("房间实例")+" · ("+i.x.ToString("0.##")+", "+i.y.ToString("0.##")+") · #"+(Room.instances.IndexOf(i)+1);}
            var item=new ToolStripMenuItem(text){Checked=hit.track!=null?SelectedTracks.Contains(hit.track):hit.instance==SelectedInstance,ToolTipText=text};item.Click+=(s,e)=>{ChooseTarget(target,additive,false,true);Invalidate();};menu.Items.Add(item);
        }
        return menu;
    }
    private void ShowPickMenu(List<PickTarget> hits,Point location,bool additive)
    {
        if(pickMenu!=null)pickMenu.Dispose();pickMenu=CreatePickMenu(hits,additive);
        pickMenu.Show(this,location);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);if(State==null||Room==null)return;Focus();pendingPicks=null;if(e.Button==MouseButtons.Middle){panning=true;panStart=e.Location;panX=ViewX;panY=ViewY;Cursor=Cursors.Hand;Capture=true;return;}var p=Position(e.Location);down=e.Location;pointerDown=p;moved=false;positions.Clear();motionPositions.Clear();cameraPositions.Clear();
        if(EditNativeTest&&e.Button==MouseButtons.Left){if(BeginEdit!=null)BeginEdit();draggingNativeTest=true;Capture=true;PlaceNativeTest(p);return;}
        if(e.Button==MouseButtons.Left&&BeginCurve(p))return;
        if(SelectedTracks.Count<=1&&SelectedTrack!=null&&SelectedTrack.kind=="rod"&&SelectedTrack.shape=="rod")
        {var ends=Motion.Endpoints(SelectedTrack,Frame);for(int i=0;i<2;i++)if(Near(ends[i],p,14)){draggingTrack=SelectedTrack;handle=i==0?"first":"second";if(e.Button==MouseButtons.Left)Capture=true;else draggingTrack=null;return;}}
        var hits=PickTargets(p);bool additive=(ModifierKeys&(Keys.Control|Keys.Shift))!=0;
        if(hits.Count>1&&e.Button==MouseButtons.Left){pendingPicks=hits;pendingAdditive=additive;Capture=true;return;}
        if(hits.Count>0){ChooseTarget(hits[0],additive,e.Button==MouseButtons.Left);return;}
        if(e.Button==MouseButtons.Left)
        {selectingBox=true;boxFirst=boxLast=p;boxInitial=(ModifierKeys&(Keys.Control|Keys.Shift))!=0?new HashSet<Track>(SelectedTracks):new HashSet<Track>();Capture=true;}
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);if(Room==null)return;if(panning){Fit();double scale=view.Width/Room.width;ViewX=panX-(e.X-panStart.X)/scale;ViewY=panY-(e.Y-panStart.Y)/scale;Fit();DisplayOptionsChanged();return;}var p=Position(e.Location);if(PointerMoved!=null)PointerMoved(p);
        if(draggingNativeTest){PlaceNativeTest(p);return;}
        if(curveHandle>=0){DragCurve(p);return;}
        if(pendingPicks!=null){if(Math.Abs(e.X-down.X)+Math.Abs(e.Y-down.Y)<3)return;var hit=pendingPicks.FirstOrDefault(candidate=>candidate.track!=null&&SelectedTracks.Contains(candidate.track)||candidate.instance!=null&&candidate.instance==SelectedInstance)??pendingPicks[0];pendingPicks=null;ChooseTarget(hit,pendingAdditive,true);}
        if(selectingBox)
        {
            boxLast=p;var r=RectangleF.FromLTRB(Math.Min(boxFirst.X,p.X),Math.Min(boxFirst.Y,p.Y),Math.Max(boxFirst.X,p.X),Math.Max(boxFirst.Y,p.Y));SelectedTracks.Clear();SelectedTracks.UnionWith(boxInitial);
            foreach(var t in State.tracks.Where(t=>t.enabled&&Theme.Spatial(t.kind)&&Frame>=t.start&&Frame<=t.end))
            {var q=Motion.At(t,Frame);var center=WorldPoint(t,new PointF((float)q.x,(float)q.y));if(r.Contains(center)||t.kind=="rod"&&Motion.RodPoints(t,Frame).Any(point=>r.Contains(point)))SelectedTracks.Add(t);}
            Invalidate();return;
        }
        if(draggingTrack==null&&draggingInstance==null)return;
        if(!moved&&Math.Abs(e.X-down.X)+Math.Abs(e.Y-down.Y)<3)return;if(!moved){if(BeginEdit!=null)BeginEdit();moved=true;}double x=Math.Round(p.X),y=Math.Round(p.Y);if((ModifierKeys&Keys.Shift)!=0){x=Math.Round(x/16)*16;y=Math.Round(y/16)*16;}
        if(draggingTrack!=null)
        {
            var t=draggingTrack;
            if(handle=="center")
            {
                foreach(var pair in positions)
                {var item=pair.Key;var current=ParentLocalPoint(item,TrackPoint(item,p));var first=ParentLocalPoint(item,TrackPoint(item,pointerDown));double dx=Math.Round(current.X-first.X),dy=Math.Round(current.Y-first.Y);if((ModifierKeys&Keys.Shift)!=0){dx=Math.Round(dx/16)*16;dy=Math.Round(dy/16)*16;}bool controlled=false;if(MotionPresets.Bound(item)&&motionPositions.ContainsKey(item)){item.motionPreset=motionPositions[item].Snapshot();MotionPresets.Translate(item,dx,dy);controlled=true;}if(CameraPresets.Bound(item)&&cameraPositions.ContainsKey(item)){item.cameraPreset=cameraPositions[item].Snapshot();CameraPresets.Translate(item,dx,dy);controlled=true;}if(controlled)continue;string[] fields=item.kind=="rod"&&item.shape=="rod"&&item.rodMode=="endpoints"?new[]{"x1","y1","x2","y2"}:item.kind=="rod"&&item.shape=="rod"&&item.rodMode=="anchor"?new[]{"x","y","x2","y2"}:new[]{"x","y"};foreach(string field in fields)Motion.EditValue(item,field,Frame,pair.Value[field]+(field.StartsWith("x")?dx:dy));}
            }
            else if(t.rodMode=="center")
            {var center=Motion.At(t,Frame);double dx=x-center.x,dy=y-center.y;Motion.EditValue(t,"length",Frame,2*Math.Sqrt(dx*dx+dy*dy));Motion.EditValue(t,"angle",Frame,Math.Atan2(-dy,dx)*180/Math.PI+(handle=="first"?180:0)-t.angularSpeed*Math.Max(0,Frame-TimelineLayout.ClockStart(t)));}
            else{string px=handle=="first"?(t.rodMode=="anchor"?"x":"x1"):"x2",py=handle=="first"?(t.rodMode=="anchor"?"y":"y1"):"y2";Motion.EditValue(t,px,Frame,x);Motion.EditValue(t,py,Frame,y);}
        }
        else{draggingInstance.x=x;draggingInstance.y=y;}SceneChanged();
    }
    private void PlaceNativeTest(PointF point)
    {if(State.nativeTest==null)State.nativeTest=new NativeTestPosition();double x=point.X,y=point.Y;if((ModifierKeys&Keys.Shift)!=0){x=Math.Round(x/16)*16;y=Math.Round(y/16)*16;}State.nativeTest.x=Math.Max(-100000,Math.Min(100000,Math.Round(x,2)));State.nativeTest.y=Math.Max(-100000,Math.Min(100000,Math.Round(y,2)));SceneChanged();if(NativeTestPreview!=null)NativeTestPreview();}
    private void FinishNativeTest(){draggingNativeTest=false;if(NativeTestChanged!=null)NativeTestChanged();}
    protected override void OnMouseUp(MouseEventArgs e)
    {base.OnMouseUp(e);if(pendingPicks!=null){var hits=pendingPicks;pendingPicks=null;Capture=false;ShowPickMenu(hits,e.Location,pendingAdditive);return;}if(curveHandle>=0){curveHandle=-1;Capture=false;if(Changed!=null)Changed();return;}if(draggingNativeTest){FinishNativeTest();Capture=false;return;}if(panning||e.Button==MouseButtons.Middle){panning=false;Capture=false;Cursor=Cursors.Default;return;}if(selectingBox){selectingBox=false;var chosen=SelectedTracks.ToArray();if(SelectTracks!=null)SelectTracks(chosen,chosen.FirstOrDefault());Invalidate();}Capture=false;if(moved&&draggingTrack!=null&&Changed!=null)Changed();if(moved&&draggingInstance!=null&&InstanceMoved!=null)InstanceMoved(draggingInstance);draggingTrack=null;draggingInstance=null;}
    protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture)pendingPicks=null;if(!Capture&&draggingNativeTest)FinishNativeTest();if(!Capture&&panning){panning=false;Cursor=Cursors.Default;}}
    protected override void OnDragEnter(DragEventArgs e){if(e.Data.GetDataPresent(DataFormats.Text))e.Effect=DragDropEffects.Copy;}
    protected override void OnDragDrop(DragEventArgs e){var text=e.Data.GetData(DataFormats.Text) as string;if(text==null||ResourceDropped==null)return;var parts=text.Split('|');if(parts.Length==2)ResourceDropped(parts[0],parts[1],Position(PointToClient(new Point(e.X,e.Y))));}
    protected override void Dispose(bool disposing){if(disposing){if(renderCancellation!=null)renderCancellation.Cancel();if(pickMenu!=null){pickMenu.Dispose();pickMenu=null;}if(image!=null){image.Dispose();image=null;}if(!rendering){if(workerSprites!=null){workerSprites.Dispose();workerSprites=null;}previewCache.Dispose();}}base.Dispose(disposing);}
}
