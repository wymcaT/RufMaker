using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

internal sealed class TimelineCanvas : UserControl
{
    private sealed class Lane {public Track track;public string property;}
    public EditorState State;public WaveData Wave;public Track SelectedTrack;public Keyframe SelectedKey;public string SelectedProperty="x";public int Frame;
    public Action<int> Seek;
    public Func<int,int> SnapCursor;
    public Action<string,string,Track,int> PresetDropped;
    public Action<Track,Keyframe> SelectTrack;
    public Action<Track,string,int> InsertKey;
    public Action<NativeEvent,bool> SelectNativeEvent;
    public Action BeginEdit,Changed,PreviewChanged;
    public readonly HashSet<Track> SelectedTracks=new HashSet<Track>();
    public readonly HashSet<Keyframe> SelectedKeys=new HashSet<Keyframe>();
    public bool NotifyingSelection {get;private set;}
    public bool KeyboardPreviewNavigation {get;private set;}
    public float PixelsPerSecond=80;
    private readonly HScrollBar horizontal=new HScrollBar();private readonly VScrollBar vertical=new VScrollBar();
    private readonly HashSet<string> expanded=new HashSet<string>();private readonly List<Lane> lanes=new List<Lane>();
    private const int Names=190,Header=76,Row=30;
    private string dragMode;private Point dragStart;private Track dragTrack;private Keyframe dragKey;
    private readonly Dictionary<Keyframe,int> groupFrames=new Dictionary<Keyframe,int>();
    private sealed class BarState {public int start,end;public Dictionary<Keyframe,int> keys;}
    private readonly Dictionary<Track,BarState> bars=new Dictionary<Track,BarState>();
    private bool boxTracks;private HashSet<Track> boxTrackInitial=new HashSet<Track>();
    private HashSet<Keyframe> boxInitial=new HashSet<Keyframe>();private PointF boxStart,boxEnd;
    private bool dragChanged;private int dragStartX;private Track hoverTrack;private string hoverEdge;
    private readonly ToolTip tips=new ToolTip();
    public TimelineCanvas()
    {
        DoubleBuffered=true;ResizeRedraw=true;BackColor=Theme.Panel;TabStop=true;AllowDrop=true;
        horizontal.Dock=DockStyle.Bottom;vertical.Dock=DockStyle.Right;Controls.Add(horizontal);Controls.Add(vertical);
        horizontal.ValueChanged+=(s,e)=>Invalidate();vertical.ValueChanged+=(s,e)=>Invalidate();
    }
    public void Expand(Track track,bool value){if(track==null)return;if(value)expanded.Add(track.id);else expanded.Remove(track.id);RefreshLayout();}
    public TimelineView CaptureView(){return new TimelineView{pixelsPerSecond=PixelsPerSecond,horizontal=horizontal.Value,vertical=vertical.Value,expanded=expanded.ToArray()};}
    public void RestoreView(TimelineView view)
    {if(view==null)return;PixelsPerSecond=Math.Max(.5f,Math.Min(2400,view.pixelsPerSecond));expanded.Clear();foreach(string id in view.expanded??new string[0])expanded.Add(id);RefreshLayout();horizontal.Value=Math.Max(0,Math.Min(horizontal.Maximum-horizontal.LargeChange+1,view.horizontal));vertical.Value=Math.Max(0,Math.Min(vertical.Maximum-vertical.LargeChange+1,view.vertical));Invalidate();}
    public void ResetSelection(Track track,Keyframe key){SelectedTracks.Clear();if(track!=null)SelectedTracks.Add(track);SelectOnly(key);}
    public void SetTrackSelection(IEnumerable<Track> tracks,Track primary=null)
    {SelectedTracks.Clear();SelectedTracks.UnionWith(tracks.Where(t=>State.tracks.Contains(t)&&t.kind!="native"));ClearKeySelection();NotifySelection(primary!=null&&SelectedTracks.Contains(primary)?primary:SelectedTracks.FirstOrDefault(),null);}
    public void MoveTracks(int delta)
    {
        if(SelectedTracks.Count==0)return;
        int first=SelectedTracks.Min(t=>Math.Min(t.start,t.keyframes.Count==0?t.start:t.keyframes.Min(k=>k.frame)));
        int last=SelectedTracks.Max(t=>Math.Max(t.end,t.keyframes.Count==0?t.end:t.keyframes.Max(k=>k.frame)));
        delta=Math.Max(-first,Math.Min(State.duration-last,delta));
        foreach(var t in SelectedTracks){t.start+=delta;t.end+=delta;foreach(var k in t.keyframes)k.frame+=delta;}
    }
    public void TrimTracks(bool left,int delta)
    {
        if(SelectedTracks.Count==0)return;
        int min=left?-SelectedTracks.Min(t=>t.start):SelectedTracks.Max(t=>t.start-t.end);
        int max=left?SelectedTracks.Min(t=>t.end-t.start):State.duration-SelectedTracks.Max(t=>t.end);
        delta=Math.Max(min,Math.Min(max,delta));foreach(var t in SelectedTracks)if(left)t.start+=delta;else t.end+=delta;
    }
    public void ClearKeySelection(){SelectedKeys.Clear();SelectedKey=null;Invalidate();}
    public void SelectOnly(Keyframe key){SelectedKeys.Clear();if(key!=null)SelectedKeys.Add(key);SelectedKey=key;Invalidate();}
    public void SetSelection(IEnumerable<Keyframe> keys,Track primaryTrack,Keyframe primaryKey){SelectedKeys.Clear();SelectedKeys.UnionWith(keys);SelectedTracks.Clear();if(primaryTrack!=null)SelectedTracks.Add(primaryTrack);NotifySelection(primaryTrack,primaryKey);}
    public void SelectAllKeys(Track track){if(track==null)return;SetSelection(track.keyframes,track,track.keyframes.FirstOrDefault());}
    public void MoveSelectedTo(int targetFrame)
    {
        if(SelectedKey==null||SelectedKeys.Count==0)return;int delta=targetFrame-SelectedKey.frame;delta=Math.Max(-SelectedKeys.Min(k=>k.frame),Math.Min(State.duration-SelectedKeys.Max(k=>k.frame),delta));
        foreach(var key in SelectedKeys)key.frame+=delta;
        foreach(var track in State.tracks)foreach(var key in track.keyframes.Where(k=>SelectedKeys.Contains(k)).ToArray()){track.keyframes.RemoveAll(k=>!SelectedKeys.Contains(k)&&k.property==key.property&&k.frame==key.frame);track.start=Math.Min(track.start,key.frame);track.end=Math.Max(track.end,key.frame);}Invalidate();
    }
    public Track Owner(Keyframe key){return State==null?null:State.tracks.FirstOrDefault(t=>t.keyframes.Contains(key));}
    private void NotifySelection(Track track,Keyframe key)
    {SelectedTrack=track;SelectedKey=key;NotifyingSelection=true;try{if(SelectTrack!=null)SelectTrack(track,key);}finally{NotifyingSelection=false;}Invalidate();}
    public void RefreshLayout()
    {
        if(horizontal==null||vertical==null)return;
        lanes.Clear();if(State!=null)foreach(var t in State.tracks){lanes.Add(new Lane{track=t});if(expanded.Contains(t.id)){if(t.kind=="native"&&State.nativeProject!=null){foreach(string file in State.nativeProject.events.Select(e=>e.file).Distinct())lanes.Add(new Lane{track=t,property="@gm:"+file});}else foreach(var p in Motion.Fields(t).Where(p=>Motion.HasKeys(t,p)))lanes.Add(new Lane{track=t,property=p});}}
        if(State!=null){SelectedKeys.RemoveWhere(k=>Owner(k)==null);SelectedTracks.RemoveWhere(t=>!State.tracks.Contains(t));}
        int width=Math.Max(1,Width-Names-vertical.Width),height=Math.Max(1,Height-Header-horizontal.Height);
        int total=State==null?width:(int)Math.Ceiling(State.duration/(double)State.fps*PixelsPerSecond)+40;
        horizontal.LargeChange=width;horizontal.SmallChange=50;horizontal.Maximum=Math.Max(width,total)-1;horizontal.Value=Math.Min(horizontal.Value,Math.Max(0,horizontal.Maximum-width+1));horizontal.Enabled=total>width;
        vertical.LargeChange=height;vertical.SmallChange=Row;vertical.Maximum=Math.Max(height,lanes.Count*Row)-1;vertical.Value=Math.Min(vertical.Value,Math.Max(0,vertical.Maximum-height+1));vertical.Enabled=lanes.Count*Row>height;Invalidate();
    }
    public void SetZoom(float value,int anchorX=-1)
    {
        if(State==null)return;if(anchorX<Names)anchorX=Names+Math.Max(1,Width-Names-vertical.Width)/2;
        double seconds=(anchorX-Names+horizontal.Value)/PixelsPerSecond;PixelsPerSecond=Math.Max(.5f,Math.Min(2400,value));RefreshLayout();
        horizontal.Value=Math.Max(0,Math.Min(horizontal.Maximum-horizontal.LargeChange+1,(int)(seconds*PixelsPerSecond-(anchorX-Names))));Invalidate();
    }
    public void ViewSeconds(double seconds){SetZoom((float)(Math.Max(1,Width-Names-vertical.Width-25)/Math.Max(.1,seconds)));horizontal.Value=0;Invalidate();}
    public void FitAll(){if(State!=null)ViewSeconds(State.duration/(double)State.fps);}
    public void EnsureFrameVisible()
    {
        float x=X(Frame);if(x<Names+10||x>Width-vertical.Width-25)horizontal.Value=Math.Max(0,Math.Min(horizontal.Maximum-horizontal.LargeChange+1,(int)(Frame/(double)State.fps*PixelsPerSecond-horizontal.LargeChange*.2)));
    }
    public void PlayheadMoved(int oldFrame)
    {int a=(int)X(oldFrame),b=(int)X(Frame);Invalidate(new Rectangle(Math.Max(Names,Math.Min(a,b)-8),0,Math.Abs(a-b)+18,Height-horizontal.Height));}
    protected override void OnResize(EventArgs e){base.OnResize(e);RefreshLayout();}
    private float X(int frame){return State==null?Names:Names+frame/(float)State.fps*PixelsPerSecond-horizontal.Value;}
    private int ToFrame(int x){int frame=Math.Max(0,Math.Min(State.duration,(int)Math.Round((x-Names+horizontal.Value)/PixelsPerSecond*State.fps)));return (ModifierKeys&Keys.Alt)!=0?frame:BeatGrid.Snap(State,frame);}
    private int CursorFrame(int x){int value=Math.Max(0,Math.Min(State.duration,(int)Math.Round((x-Names+horizontal.Value)/PixelsPerSecond*State.fps)));return SnapCursor==null?ToFrame(x):SnapCursor(value);}
    protected override void OnDragEnter(DragEventArgs e){string text=e.Data.GetData(DataFormats.Text) as string;e.Effect=text!=null&&new[]{"presets|","camera-presets|","effect-presets|","custom-presets|"}.Any(text.StartsWith)?DragDropEffects.Copy:DragDropEffects.None;}
    protected override void OnDragOver(DragEventArgs e){OnDragEnter(e);}
    protected override void OnDragDrop(DragEventArgs e){var text=e.Data.GetData(DataFormats.Text) as string;if(State==null||text==null||PresetDropped==null)return;int split=text.IndexOf('|');if(split<0)return;var p=PointToClient(new Point(e.X,e.Y));int row=(p.Y-Header+vertical.Value)/Row;var target=p.Y>=Header&&row>=0&&row<lanes.Count?lanes[row].track:null;PresetDropped(text.Substring(0,split),text.Substring(split+1),target,p.X<Names?Frame:ToFrame(p.X));}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);var g=e.Graphics;g.Clear(Theme.Panel);if(State==null)return;
        int width=Width-vertical.Width,height=Height-horizontal.Height;if(width<=Names||height<=Header)return;
        using(var brush=new SolidBrush(Theme.Back))g.FillRectangle(brush,Names,0,width-Names,Header);
        using(var pen=new Pen(Theme.Line)){g.DrawLine(pen,Names,0,Names,height);g.DrawLine(pen,0,Header,width,Header);}
        Theme.Label(g,SelectedKeys.Count>1?"已选 "+SelectedKeys.Count+" 个关键帧":SelectedTracks.Count>1?"已选 "+SelectedTracks.Count+" 个物件":"时间轴 · ▸ 展开 · 空白框选",new Rectangle(8,0,Names-12,28),Theme.Text,Theme.Bold);
        Theme.Label(g,"♫  "+(String.IsNullOrEmpty(State.musicTitle)?System.IO.Path.GetFileName(State.music):State.musicTitle),new Rectangle(16,28,Names-25,48),Theme.Muted,localize:false);
        var clip=g.Save();g.SetClip(new Rectangle(Names,0,width-Names,height));
        double target=65/PixelsPerSecond;double[] steps={.02,.05,.1,.2,.5,1,2,5,10,15,30,60,120,300,600};double step=steps.FirstOrDefault(v=>v>=target);if(step==0)step=600;
        double left=horizontal.Value/PixelsPerSecond,right=(horizontal.Value+width-Names)/PixelsPerSecond;
        using(var pen=new Pen(Color.FromArgb(38,47,63)))for(double second=Math.Floor(left/step)*step;second<=right;second+=step)
        {float x=Names+(float)second*PixelsPerSecond-horizontal.Value;g.DrawLine(pen,x,25,x,height);Theme.Label(g,step<1?second.ToString("0.00")+"s":String.Format("{0:00}:{1:00}",(int)second/60,(int)second%60),new Rectangle((int)x+4,0,65,26),Theme.Muted,Theme.Small);}
        if(Wave!=null&&Wave.peaks!=null&&Wave.duration>0)using(var pen=new Pen(Color.FromArgb(100,192,203)))for(int x=Names;x<width;x+=2)
        {double sec=(x-Names+horizontal.Value)/PixelsPerSecond;int index=(int)(sec/Wave.duration*Wave.peaks.Length);if(index<0||index>=Wave.peaks.Length)continue;float peak=(float)Math.Min(1,Wave.peaks[index])*19;g.DrawLine(pen,x,51-peak,x,51+peak);}
        float endX=X(State.duration);using(var pen=new Pen(Color.FromArgb(160,210,180,95),2)){pen.DashStyle=DashStyle.Dash;g.DrawLine(pen,endX,0,endX,height);}g.Restore(clip);
        int first=Math.Max(0,vertical.Value/Row),last=Math.Min(lanes.Count,(height-Header+vertical.Value)/Row+1);
        for(int i=first;i<last;i++)
        {
            var lane=lanes[i];var t=lane.track;int y=Header+i*Row-vertical.Value;var rowClip=g.Save();g.SetClip(new Rectangle(0,Header,width,height-Header));
            bool chosen=(SelectedTracks.Contains(t)||t==SelectedTrack)&&(lane.property==null||lane.property==SelectedProperty);
            using(var bg=new SolidBrush(chosen?Color.FromArgb(47,44,62):lane.property!=null?Color.FromArgb(23,29,39):i%2==0?Theme.Panel:Color.FromArgb(29,35,47)))g.FillRectangle(bg,0,y,width,Row);
            Color color=t.enabled?Theme.TrackColor(t.kind):Color.FromArgb(80,87,100);
            Theme.Label(g,lane.property==null?(expanded.Contains(t.id)?"▾ ":"▸ ")+t.name:"    ◇  "+(lane.property.StartsWith("@gm:")?System.IO.Path.GetFileNameWithoutExtension(lane.property.Substring(4)):Language.Text(Motion.Name(lane.property))),new Rectangle(8,y,Names-36,Row),lane.property==null?Theme.Text:chosen?Theme.Accent:Theme.Muted,lane.property==null?Theme.Font:Theme.Small,localize:false);
            if(lane.property==null)Theme.Label(g,t.kind=="native"?"G":t.enabled?"●":"○",new Rectangle(Names-28,y,24,Row),color);
            g.SetClip(new Rectangle(Names,Header,width-Names,height-Header));
            float x=X(t.start),end=X(t.end);
            if(lane.property==null)
            {
                using(var brush=new SolidBrush(Color.FromArgb(chosen?210:135,color)))g.FillRectangle(brush,x,y+6,Math.Max(5,end-x),18);
                if(t==hoverTrack&&hoverEdge!=null)using(var pen=new Pen(Color.White,2))
                {float edge=hoverEdge=="left"?x:end;g.DrawLine(pen,edge,y+4,edge,y+26);g.DrawLine(pen,edge-5,y+15,edge+5,y+15);g.DrawLine(pen,edge-5,y+15,edge-2,y+12);g.DrawLine(pen,edge+5,y+15,edge+2,y+18);}
            }
            else using(var pen=new Pen(Color.FromArgb(80,color)))g.DrawLine(pen,x,y+15,end,y+15);
            if(t.kind=="native")foreach(var item in NativeEvents(lane)){float at=X(item.frame);if(at<Names||at>width)continue;using(var pen=new Pen(Color.FromArgb(255,205,110),2))g.DrawLine(pen,at,y+7,at,y+23);}
            foreach(var key in t.keyframes)
            {
                if(lane.property==null&&expanded.Contains(t.id)||lane.property!=null&&key.property!=lane.property)continue;float kx=X(key.frame);if(kx<Names-7||kx>width+7)continue;
                PointF[] diamond={new PointF(kx,y+8),new PointF(kx+5,y+15),new PointF(kx,y+22),new PointF(kx-5,y+15)};
                using(var brush=new SolidBrush(SelectedKeys.Contains(key)?Color.White:lane.property==null?Color.FromArgb(200,230,217,191):Color.FromArgb(255,218,137)))g.FillPolygon(brush,diamond);
                if(SelectedKeys.Contains(key))using(var pen=new Pen(Theme.Accent,1.5f))g.DrawPolygon(pen,diamond);
            }
            g.Restore(rowClip);
        }
        DrawBeatGrid(g,width,height);
        if(dragMode=="box")
        {
            var saved=g.Save();g.SetClip(new Rectangle(Names,Header,width-Names,height-Header));var rect=BoxRectangle();rect.Offset(Names-horizontal.Value,Header-vertical.Value);
            using(var brush=new SolidBrush(Color.FromArgb(40,Theme.Accent)))g.FillRectangle(brush,rect);using(var pen=new Pen(Theme.Accent)){pen.DashStyle=DashStyle.Dash;g.DrawRectangle(pen,rect.X,rect.Y,rect.Width,rect.Height);}g.Restore(saved);
        }
        float playhead=X(Frame);if(playhead>=Names&&playhead<width)
        {using(var pen=new Pen(Theme.Accent,1.5f))g.DrawLine(pen,playhead,8,playhead,height);using(var brush=new SolidBrush(Theme.Accent))g.FillPolygon(brush,new[]{new PointF(playhead-6,0),new PointF(playhead+6,0),new PointF(playhead+6,9),new PointF(playhead,15),new PointF(playhead-6,9)});}
    }
    private void DrawBeatGrid(Graphics g,int width,int height)
    {
        if(!State.beatSnap)return;double step=BeatGrid.StepSeconds(State),offset=State.beatOffsetMs/1000;
        double stride=Math.Max(1,Math.Ceiling(5/(step*PixelsPerSecond))),left=horizontal.Value/PixelsPerSecond,right=(horizontal.Value+width-Names)/PixelsPerSecond;
        double first=Math.Floor((left-offset)/step/stride)*stride;var saved=g.Save();g.SetClip(new Rectangle(Names,24,width-Names,height-24));
        for(double tick=first;offset+tick*step<=right;tick+=stride){double second=offset+tick*step;if(second<0)continue;float x=Names+(float)second*PixelsPerSecond-horizontal.Value;bool quarter=Math.Abs(tick*4/State.beatDivision-Math.Round(tick*4/State.beatDivision))<.001;using(var pen=new Pen(quarter?Color.FromArgb(70,213,180,94):Color.FromArgb(28,200,173,115)))g.DrawLine(pen,x,25,x,height);}
        g.Restore(saved);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);if(State==null)return;Focus();dragStart=e.Location;dragStartX=e.X+horizontal.Value;dragChanged=false;dragMode=null;
        var lane=LaneAt(e.Location);var key=KeyAt(lane,e.Location);string edge=EdgeAt(lane,e.Location);
        KeyboardPreviewNavigation=e.Button==MouseButtons.Left&&(lane==null||e.X>=Names&&key==null&&edge==null&&(lane.track.kind=="native"?NativeAt(lane,e.X)==null:lane.property!=null||e.X<X(lane.track.start)||e.X>X(lane.track.end)));
        if(lane!=null&&lane.track.kind=="native"){
            ResetSelection(lane.track,null);NotifySelection(lane.track,null);
            if(e.Button==MouseButtons.Left){if(e.X<27&&lane.property==null)Expand(lane.track,!expanded.Contains(lane.track.id));else if(e.X>=Names){var item=NativeAt(lane,e.X);if(SelectNativeEvent!=null)SelectNativeEvent(item,false);if(Seek!=null)Seek(item==null?CursorFrame(e.X):item.frame);dragMode="seek";Capture=true;}}return;
        }
        bool additive=(ModifierKeys&(Keys.Control|Keys.Shift))!=0;
        if(lane!=null)
        {
            if(lane.property!=null)SelectedProperty=lane.property;
            if(e.Button==MouseButtons.Right)
            {
                if(key!=null){if(!SelectedKeys.Contains(key)){ResetSelection(lane.track,key);}NotifySelection(lane.track,key);}
                else {if(!SelectedTracks.Contains(lane.track))ResetSelection(lane.track,null);NotifySelection(lane.track,null);}return;
            }
            if(e.Button!=MouseButtons.Left)return;
            if(e.X<Names)
            {
                if(lane.property==null&&e.X<27){Expand(lane.track,!expanded.Contains(lane.track.id));return;}
                if(lane.property==null&&e.X>Names-30){if(BeginEdit!=null)BeginEdit();lane.track.enabled=!lane.track.enabled;if(Changed!=null)Changed();return;}
                PickTrack(lane.track,additive);return;
            }
            if(edge!=null)
            {if(!SelectedTracks.Contains(lane.track))PickTrack(lane.track,additive);else {ClearKeySelection();NotifySelection(lane.track,null);}PrepareBar(lane.track,edge);}
            else if(key!=null)
            {
                SelectedTracks.Clear();SelectedTracks.Add(lane.track);
                if(additive&&SelectedKeys.Contains(key)){SelectedKeys.Remove(key);var remaining=SelectedKeys.FirstOrDefault();NotifySelection(remaining==null?lane.track:Owner(remaining),remaining);return;}
                if(!SelectedKeys.Contains(key)){if(!additive)SelectedKeys.Clear();SelectedKeys.Add(key);}
                NotifySelection(lane.track,key);dragTrack=lane.track;dragKey=key;dragMode="key";
                groupFrames.Clear();foreach(var selected in SelectedKeys)groupFrames[selected]=selected.frame;
            }
            else if(lane.property==null&&e.X>=X(lane.track.start)&&e.X<=X(lane.track.end))
            {
                if(additive){if(!PickTrack(lane.track,true))return;}
                else if(!SelectedTracks.Contains(lane.track))PickTrack(lane.track,false);
                else {ClearKeySelection();NotifySelection(lane.track,null);}
                PrepareBar(lane.track,"bar");
            }
            else BeginBox(e.Location,lane.property==null,additive);
        }
        else if(e.X>=Names&&e.Button==MouseButtons.Left)
        {
            if(e.Y<Header){dragMode="seek";if(Seek!=null)Seek(CursorFrame(e.X));}
            else BeginBox(e.Location,true,additive);
        }
        if(dragMode!=null)Capture=true;
    }
    private bool PickTrack(Track track,bool additive)
    {
        ClearKeySelection();if(!additive)SelectedTracks.Clear();
        if(additive&&SelectedTracks.Contains(track)){SelectedTracks.Remove(track);NotifySelection(SelectedTracks.FirstOrDefault(),null);return false;}
        SelectedTracks.Add(track);NotifySelection(track,null);return true;
    }
    private Lane LaneAt(Point point){int i=(point.Y-Header+vertical.Value)/Row;return point.Y>=Header&&i>=0&&i<lanes.Count?lanes[i]:null;}
    private string EdgeAt(Lane lane,Point point)
    {if(lane==null||lane.track.kind=="native"||lane.property!=null||point.X<Names)return null;double left=Math.Abs(point.X-X(lane.track.start)),right=Math.Abs(point.X-X(lane.track.end));if(Math.Min(left,right)>8)return null;return left<right?"left":"right";}
    private IEnumerable<NativeEvent> NativeEvents(Lane lane){if(State.nativeProject==null)return new NativeEvent[0];return State.nativeProject.events.Where(e=>lane.property==null||lane.property=="@gm:"+e.file);}
    private NativeEvent NativeAt(Lane lane,int x){return NativeEvents(lane).OrderBy(e=>Math.Abs(X(e.frame)-x)).FirstOrDefault(e=>Math.Abs(X(e.frame)-x)<8);}
    private Keyframe KeyAt(Lane lane,Point point)
    {if(lane==null||point.X<Names||lane.property==null&&expanded.Contains(lane.track.id))return null;return lane.track.keyframes.Where(k=>lane.property==null||k.property==lane.property).OrderBy(k=>Math.Abs(X(k.frame)-point.X)).FirstOrDefault(k=>Math.Abs(X(k.frame)-point.X)<8);}
    private void PrepareBar(Track track,string mode)
    {
        dragTrack=track;dragMode=mode;bars.Clear();
        foreach(var t in SelectedTracks)bars[t]=new BarState{start=t.start,end=t.end,keys=t.keyframes.ToDictionary(k=>k,k=>k.frame)};
    }
    private PointF ContentPoint(Point point){return new PointF(point.X-Names+horizontal.Value,point.Y-Header+vertical.Value);}
    private RectangleF BoxRectangle(){return RectangleF.FromLTRB(Math.Min(boxStart.X,boxEnd.X),Math.Min(boxStart.Y,boxEnd.Y),Math.Max(boxStart.X,boxEnd.X),Math.Max(boxStart.Y,boxEnd.Y));}
    private void BeginBox(Point point,bool tracks,bool additive)
    {
        dragMode="box";boxTracks=tracks;boxStart=boxEnd=ContentPoint(point);
        boxInitial=additive&&!tracks?new HashSet<Keyframe>(SelectedKeys):new HashSet<Keyframe>();
        boxTrackInitial=additive&&tracks?new HashSet<Track>(SelectedTracks):new HashSet<Track>();
        SelectedKeys.Clear();if(tracks)SelectedTracks.Clear();UpdateBox(point);
    }
    private void UpdateBox(Point point)
    {
        boxEnd=ContentPoint(point);var rect=BoxRectangle();SelectedKeys.Clear();SelectedKeys.UnionWith(boxInitial);
        if(boxTracks){SelectedTracks.Clear();SelectedTracks.UnionWith(boxTrackInitial);}
        for(int i=0;i<lanes.Count;i++)
        {
            var lane=lanes[i];float y=i*Row+15;if(y<rect.Top||y>rect.Bottom)continue;
            if(lane.track.kind=="native")continue;
            if(boxTracks)
            {if(lane.property==null&&lane.track.start/(float)State.fps*PixelsPerSecond<=rect.Right&&lane.track.end/(float)State.fps*PixelsPerSecond>=rect.Left)SelectedTracks.Add(lane.track);continue;}
            if(lane.property==null)continue;
            foreach(var key in lane.track.keyframes){if(key.property!=lane.property)continue;float x=key.frame/(float)State.fps*PixelsPerSecond;if(x>=rect.Left&&x<=rect.Right)SelectedKeys.Add(key);}
        }
        Invalidate();
    }
    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);if(State==null||e.X<Names||e.Y<Header)return;int i=(e.Y-Header+vertical.Value)/Row;
        if(i>=0&&i<lanes.Count&&lanes[i].track.kind=="native"){var item=NativeAt(lanes[i],e.X);if(item!=null&&SelectNativeEvent!=null)SelectNativeEvent(item,true);return;}
        if(i>=0&&i<lanes.Count&&lanes[i].property!=null&&InsertKey!=null)InsertKey(lanes[i].track,lanes[i].property,ToFrame(e.X));
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);if(State==null)return;
        if(dragMode==null){var nativeLane=LaneAt(e.Location);if(nativeLane!=null&&nativeLane.track.kind=="native"){var item=NativeAt(nativeLane,e.X);Cursor=Cursors.Hand;Language.ToolTip(tips,this,item==null?"GM 原生轨道：只读定位；双击编辑原 GML。":item.file+":"+item.line+" · "+item.expression+"\n"+item.snippet);return;}}
        if(dragMode==null){var lane=LaneAt(e.Location);string edge=EdgeAt(lane,e.Location);var key=KeyAt(lane,e.Location);hoverTrack=edge==null?null:lane.track;hoverEdge=edge;Cursor=edge!=null?Cursors.SizeWE:key!=null?Cursors.SizeAll:lane!=null&&lane.property==null&&e.X>=X(lane.track.start)&&e.X<=X(lane.track.end)?Cursors.SizeAll:Cursors.Default;string tip=edge=="left"?"左右拖动：修改开始帧（不移动关键帧）":edge=="right"?"左右拖动：修改结束帧（不移动关键帧）":key!=null?"拖动移动所选关键帧；Ctrl / Shift 加选；Delete 删除":"主行空白框选物件；属性行空白框选关键帧；Ctrl / Shift 加选";if(tips.GetToolTip(this)!=tip)Language.ToolTip(tips,this,tip);Invalidate();return;}
        if(e.X>Width-vertical.Width-15)ScrollHorizontal(20);else if(e.X<Names+5)ScrollHorizontal(-20);
        if(dragMode=="seek"){if(Seek!=null)Seek(CursorFrame(e.X));return;}
        if(dragMode=="box"){if(e.Y>Height-horizontal.Height-12)ScrollVertical(Row/2);else if(e.Y<Header+5)ScrollVertical(-Row/2);UpdateBox(e.Location);return;}
        if(!dragChanged&&Math.Abs(e.X-dragStart.X)+Math.Abs(e.Y-dragStart.Y)<3)return;
        if(!dragChanged){if(BeginEdit!=null)BeginEdit();dragChanged=true;}
        int delta=(int)Math.Round((e.X+horizontal.Value-dragStartX)/PixelsPerSecond*State.fps);
        if(dragMode=="key")
        {delta=Math.Max(-groupFrames.Values.Min(),Math.Min(State.duration-groupFrames.Values.Max(),delta));foreach(var pair in groupFrames)pair.Key.frame=pair.Value+delta;if(Seek!=null)Seek(dragKey.frame);}
        else if(bars.Count>0)
        {
            if(dragMode=="bar")
            {int earliest=bars.Values.Min(v=>Math.Min(v.start,v.keys.Count==0?v.start:v.keys.Values.Min())),latest=bars.Values.Max(v=>Math.Max(v.end,v.keys.Count==0?v.end:v.keys.Values.Max()));delta=Math.Max(-earliest,Math.Min(State.duration-latest,delta));}
            else if(dragMode=="left")delta=Math.Max(-bars.Values.Min(v=>v.start),Math.Min(bars.Values.Min(v=>v.end-v.start),delta));
            else delta=Math.Max(bars.Values.Max(v=>v.start-v.end),Math.Min(State.duration-bars.Values.Max(v=>v.end),delta));
            foreach(var pair in bars)
            {var t=pair.Key;var v=pair.Value;t.start=v.start+(dragMode=="right"?0:delta);t.end=v.end+(dragMode=="left"?0:delta);if(dragMode=="bar")foreach(var k in v.keys)k.Key.frame=k.Value+delta;}
        }
        if(PreviewChanged!=null)PreviewChanged();Invalidate();
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);FinishGesture();Capture=false;
    }
    private void FinishGesture()
    {
        string mode=dragMode;dragMode=null;
        if(mode=="key"&&dragChanged)
        {foreach(var t in State.tracks){var keys=t.keyframes.Where(k=>SelectedKeys.Contains(k)).ToArray();foreach(var k in keys){t.keyframes.RemoveAll(other=>!SelectedKeys.Contains(other)&&other.property==k.property&&other.frame==k.frame);t.start=Math.Min(t.start,k.frame);t.end=Math.Max(t.end,k.frame);}}}
        if(mode=="box"){if(boxTracks)NotifySelection(SelectedTracks.FirstOrDefault(),null);else{var key=SelectedKeys.FirstOrDefault();SelectedTracks.Clear();if(key!=null)SelectedTracks.Add(Owner(key));NotifySelection(key==null?null:Owner(key),key);}}
        if(dragChanged&&mode!="box"&&mode!="seek"&&Changed!=null)Changed();dragTrack=null;dragKey=null;groupFrames.Clear();bars.Clear();dragChanged=false;Cursor=Cursors.Default;Invalidate();
    }
    protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture&&dragMode!=null)FinishGesture();}
    protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);if(dragMode==null){hoverTrack=null;hoverEdge=null;Cursor=Cursors.Default;Invalidate();}}
    private void ScrollVertical(int pixels){vertical.Value=Math.Max(0,Math.Min(vertical.Maximum-vertical.LargeChange+1,vertical.Value+pixels));}
    private void ScrollHorizontal(int pixels){horizontal.Value=Math.Max(0,Math.Min(horizontal.Maximum-horizontal.LargeChange+1,horizontal.Value+pixels));}
    protected override void OnMouseWheel(MouseEventArgs e)
    {if((ModifierKeys&Keys.Control)!=0)SetZoom(PixelsPerSecond*(e.Delta>0?1.25f:.8f),e.X);else if((ModifierKeys&Keys.Shift)!=0||!vertical.Enabled)ScrollHorizontal(e.Delta>0?-100:100);else vertical.Value=Math.Max(0,Math.Min(vertical.Maximum-vertical.LargeChange+1,vertical.Value-(e.Delta>0?Row*2:-Row*2)));}
}
