using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

internal sealed partial class TimelineCanvas : UserControl
{
    private sealed class Lane {public Track track,generationSource;public string property,generationRule,bindingKey;public int index=-1,stack,top,height=30,indent;public List<Track> clips;}
    public int ActiveLane=-1;private int[] dragRowLanes,dragRowTops,dragRowHeights;private int dragLaneCount;
    private EditorState state;public EditorState State {get{return state;}set{if(state!=value){scrub.Cancel();if(dragMode=="seek"){dragMode=null;Capture=false;}}state=value;if(state!=null)TimelineLayout.Normalize(state);Invalidate();}}
    public Track SelectedTrack;public Keyframe SelectedKey;public string SelectedProperty="x";public int Frame;
    public Action<int> Seek;
    public Func<int,int> SnapCursor;
    public Func<Func<int,int?>> PrepareCursorTargets;public Action<int?> CursorGuideChanged;
    private readonly CursorScrub scrub=new CursorScrub();
    internal Func<Keys> ReadModifiers=()=>ModifierKeys;
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
    private readonly HashSet<string> collapsedGenerationGroups=new HashSet<string>();
    public bool SelectionKeyframesOnly,InspectorKeyframes;
    public Action<Track> EditKeyframes;
    private int Names {get{return InspectorKeyframes?Math.Max(110,Font.Height*6):190;}}
    private int Row {get{return InspectorKeyframes?Math.Max(30,Font.Height+12):30;}}
    private string dragMode;private Point dragStart;private Track dragTrack;private Keyframe dragKey;
    private const float EventRadius=7;
    private const TextFormatFlags TimelineTextFlags=TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.PreserveGraphicsClipping;
    private static bool EventMarker(Track track){return track!=null&&(track.kind=="control"||track.kind=="script");}
    private static bool EventCanStretch(Track track){return track.kind=="control"&&track.eventPreset!="group-copy";}
    private readonly Dictionary<Keyframe,int> groupFrames=new Dictionary<Keyframe,int>();
    private Point keyDeltaBounds;
    private sealed class BarState {public int start,end,lane,release,top,height;public int? clockStart,clockEnd;public MotionPresetData preset;public CameraPresetData camera;public Dictionary<Keyframe,int> keys;}
    public bool ClipSnap=true;public string StretchKeys="sync";public Action<string> EditNotice;private int dragVisualRow;private int? clipSnapGuide;private bool paintingDragged;
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
        scrub.Seek=value=>{if(Seek!=null)Seek(value);};scrub.Snap=value=>SnapCursor==null?BeatGrid.Snap(State,value):SnapCursor(value);scrub.PrepareTargets=()=>PrepareCursorTargets!=null?PrepareCursorTargets():SnapCursor!=null?(Func<int,int?>)(value=>SnapCursor(value)):CursorSnapping.Capture(State,SelectedTrack,8.0*State.fps/PixelsPerSecond,true);scrub.GuideChanged=value=>{ShowSnapGuide(value);if(CursorGuideChanged!=null)CursorGuideChanged(value);};
    }
    public bool FocusGenerationGroups;
    private bool IsExpanded(Track track){if(SelectionKeyframesOnly)return TimelineLayout.HasGeneration(track)&&!collapsedGenerationGroups.Contains(track.id)&&(track==SelectedTrack||SelectedTrack!=null&&TemplateRuntime.Actions(track).Any(a=>a.templateId==SelectedTrack.id));return TimelineLayout.HasGeneration(track)?!collapsedGenerationGroups.Contains(track.id)&&(!FocusGenerationGroups||track==SelectedTrack||expanded.Contains(track.id)||SelectedTrack!=null&&TemplateRuntime.Actions(track).Any(a=>a.templateId==SelectedTrack.id)):expanded.Contains(track.id);}
    public void Expand(Track track,bool value){if(track==null)return;if(value){expanded.Add(track.id);collapsedGenerationGroups.Remove(track.id);}else{expanded.Remove(track.id);if(TimelineLayout.HasGeneration(track))collapsedGenerationGroups.Add(track.id);}RefreshLayout();}
    public TimelineView CaptureView(){return new TimelineView{focusGenerationGroups=FocusGenerationGroups,clipSnap=ClipSnap,pixelsPerSecond=PixelsPerSecond,horizontal=horizontal.Value,vertical=vertical.Value,expanded=expanded.ToArray(),collapsedGenerationGroups=collapsedGenerationGroups.ToArray(),audioDisplay=audioDisplay,audioHeight=audioHeight,rowHeights=new Dictionary<string,int>(rowHeights)};}
    public void RestoreView(TimelineView view)
    {if(view==null)return;ClipSnap=view.clipSnap;FocusGenerationGroups=view.focusGenerationGroups;PixelsPerSecond=Math.Max(.5f,Math.Min(2400,view.pixelsPerSecond));audioDisplay=view.audioDisplay=="spectrogram"?"spectrogram":"waveform";audioHeight=Math.Max(20,Math.Min(600,view.audioHeight));rowHeights.Clear();foreach(var pair in view.rowHeights??new Dictionary<string,int>())rowHeights[pair.Key]=Math.Max(Row,Math.Min(600,pair.Value));collapsedGenerationGroups.Clear();foreach(string id in view.collapsedGenerationGroups??new string[0])collapsedGenerationGroups.Add(id);expanded.Clear();foreach(string id in view.expanded??new string[0])expanded.Add(id);RefreshLayout();horizontal.Value=Math.Max(0,Math.Min(horizontal.Maximum-horizontal.LargeChange+1,view.horizontal));vertical.Value=Math.Max(0,Math.Min(vertical.Maximum-vertical.LargeChange+1,view.vertical));Invalidate();if(audioDisplay=="spectrogram"&&SpectrumRequested!=null)SpectrumRequested();}
    public void ResetSelection(Track track,Keyframe key){SelectedTracks.Clear();if(track!=null)SelectedTracks.Add(track);SelectOnly(key);}
    public void SetTrackSelection(IEnumerable<Track> tracks,Track primary=null)
    {SelectedTracks.Clear();SelectedTracks.UnionWith(tracks.Where(t=>State.tracks.Contains(t)&&t.kind!="native"));ClearKeySelection();NotifySelection(primary!=null&&SelectedTracks.Contains(primary)?primary:SelectedTracks.FirstOrDefault(),null);}
    private HashSet<Track> ExpandedSelection(){var result=new HashSet<Track>(SelectedTracks);bool changed;do{changed=false;foreach(var t in State.tracks)if(result.Any(g=>g.kind=="group"&&g.id==t.parentId))changed|=result.Add(t);}while(changed);return result;}
    public void MoveTracks(int delta)
    {
        if(SelectedTracks.Count==0)return;
        int first=SelectedTracks.Min(t=>Math.Min(t.start,t.keyframes.Count==0?t.start:t.keyframes.Min(k=>k.frame)));
        int last=SelectedTracks.Max(t=>Math.Max(t.end,t.keyframes.Count==0?t.end:t.keyframes.Max(k=>k.frame)));
        delta=Math.Max(-first,Math.Min(State.duration-last,delta));
        foreach(var t in ExpandedSelection()){t.start+=delta;t.end+=delta;TimelineLayout.ShiftClock(t,delta);if(t.releaseFrame>=0)t.releaseFrame+=delta;foreach(var k in t.keyframes)k.frame+=delta;}
    }
    public void TrimTracks(bool left,int delta)
    {
        if(SelectedTracks.Count==0)return;
        int min=left?-SelectedTracks.Min(t=>t.start):SelectedTracks.Max(t=>t.start-t.end);
        int max=left?SelectedTracks.Min(t=>t.end-t.start):State.duration-SelectedTracks.Max(t=>t.end);
        delta=Math.Max(min,Math.Min(max,delta));foreach(var t in SelectedTracks)TimelineEditing.Resize(t,t.start,t.end,t.start+(left?delta:0),t.end+(left?0:delta),StretchKeys);
    }
    public void ClearKeySelection(){SelectedKeys.Clear();SelectedKey=null;Invalidate();}
    public void SelectOnly(Keyframe key){SelectedKeys.Clear();if(key!=null)SelectedKeys.Add(key);SelectedKey=key;Invalidate();}
    public void SetSelection(IEnumerable<Keyframe> keys,Track primaryTrack,Keyframe primaryKey){SelectedKeys.Clear();SelectedKeys.UnionWith(keys);SelectedTracks.Clear();if(primaryTrack!=null)SelectedTracks.Add(primaryTrack);NotifySelection(primaryTrack,primaryKey);}
    public void SelectAllKeys(Track track){if(track==null)return;SetSelection(track.keyframes,track,track.keyframes.FirstOrDefault());}
    public void MoveSelectedTo(int targetFrame)
    {
        if(SelectedKey==null||SelectedKeys.Count==0)return;var bounds=KeyMoveBounds(SelectedKeys.ToDictionary(k=>k,k=>k.frame));int delta=Math.Max(bounds.X,Math.Min(bounds.Y,targetFrame-SelectedKey.frame));
        foreach(var key in SelectedKeys)key.frame+=delta;
        MergeSelectedKeys();Invalidate();
    }
    private Point KeyMoveBounds(IDictionary<Keyframe,int> frames)
    {
        int min=-frames.Values.Min(),max=State.duration-frames.Values.Max();
        foreach(var track in State.tracks)foreach(var key in track.keyframes){int origin;if(!frames.TryGetValue(key,out origin))continue;min=Math.Max(min,track.start-origin);max=Math.Min(max,track.end-origin);}
        // A selection already wider than its clips cannot move as a rigid group.
        return min<=max?new Point(min,max):Point.Empty;
    }
    private void MergeSelectedKeys()
    {foreach(var track in State.tracks)foreach(var key in track.keyframes.Where(k=>SelectedKeys.Contains(k)).ToArray())track.keyframes.RemoveAll(k=>!SelectedKeys.Contains(k)&&k.property==key.property&&k.frame==key.frame);}
    public Track Owner(Keyframe key){return State==null?null:State.tracks.FirstOrDefault(t=>t.keyframes.Contains(key));}
    private void NotifySelection(Track track,Keyframe key)
    {SelectedTrack=track;SelectedKey=key;NotifyingSelection=true;try{if(SelectTrack!=null)SelectTrack(track,key);}finally{NotifyingSelection=false;}Invalidate();}
    public void RefreshLayout()
    {
        if(dragMode=="bar"&&bars.Count>0){Invalidate();return;}
        if(horizontal==null||vertical==null)return;lanes.Clear();
        if(State!=null&&InspectorKeyframes){if(SelectedTrack!=null&&State.tracks.Contains(SelectedTrack))foreach(string property in Motion.Fields(SelectedTrack).Where(p=>Motion.HasKeys(SelectedTrack,p)||MotionPresets.Generated(SelectedTrack,p)))lanes.Add(new Lane{track=SelectedTrack,property=property});}
        else if(State!=null){ActiveLane=Math.Min(ActiveLane,State.laneCount-1);
            foreach(var native in State.tracks.Where(t=>t.kind=="native")){lanes.Add(new Lane{track=native});if(IsExpanded(native)&&State.nativeProject!=null)foreach(string file in State.nativeProject.events.Select(e=>e.file).Distinct())lanes.Add(new Lane{track=native,property="@gm:"+file});}
            var bound=TimelineLayout.BoundTemplates(State);
            for(int index=0;index<State.laneCount;index++){
                var all=State.tracks.Where(t=>t.kind!="native"&&t.lane==index).ToList();var items=all.Where(t=>!bound.Contains(t.id)).ToList();if(all.Count>0&&items.Count==0)continue;
                var ordinary=items.Where(t=>!t.templateOnly).ToList();var overview=ordinary.Where(t=>!TimelineLayout.HasGeneration(t)||!IsExpanded(t)).ToList();if(ordinary.Count>0||items.Count==0)lanes.Add(new Lane{index=index,clips=overview});
                foreach(var t in ordinary.Where(IsExpanded))AppendGenerationGroup(t,index,0,new HashSet<string>(),null);
                foreach(var t in items.Where(t=>t.templateOnly)){lanes.Add(new Lane{index=index,track=t,property="@template"});if(!SelectionKeyframesOnly&&expanded.Contains(t.id))AppendTemplateKeys(t,index,0,"standalone:"+t.id);if(IsExpanded(t))AppendGenerationRows(t,index,0,new HashSet<string>(),"standalone:"+t.id);}
            }
            SelectedKeys.RemoveWhere(k=>Owner(k)==null);SelectedTracks.RemoveWhere(t=>!State.tracks.Contains(t));
        }
        int width=Math.Max(1,Width-Names-vertical.Width),height=Math.Max(1,Height-Header-horizontal.Height);
        int total=State==null?width:(int)Math.Ceiling(State.duration/(double)State.fps*PixelsPerSecond)+40;
        horizontal.LargeChange=width;horizontal.SmallChange=50;horizontal.Maximum=Math.Max(width,total)-1;horizontal.Value=Math.Min(horizontal.Value,Math.Max(0,horizontal.Maximum-width+1));horizontal.Enabled=total>width;
        int totalHeight=0;foreach(var lane in lanes){lane.top=totalHeight;lane.height=Math.Max(lane.property=="@template"?58:Row,GetRowHeight(RowKey(lane)));totalHeight+=lane.height;}vertical.LargeChange=height;vertical.SmallChange=Row;vertical.Maximum=Math.Max(height,totalHeight)-1;vertical.Value=Math.Min(vertical.Value,Math.Max(0,vertical.Maximum-height+1));vertical.Enabled=totalHeight>height;Invalidate();
    }
    public void SetZoom(float value,int anchorX=-1)
    {
        if(State==null)return;if(anchorX<Names)anchorX=Names+Math.Max(1,Width-Names-vertical.Width)/2;
        double seconds=(anchorX-Names+horizontal.Value)/PixelsPerSecond;PixelsPerSecond=Math.Max(.5f,Math.Min(2400,value));RefreshLayout();
        horizontal.Value=Math.Max(0,Math.Min(horizontal.Maximum-horizontal.LargeChange+1,(int)(seconds*PixelsPerSecond-(anchorX-Names))));Invalidate();
    }
    public void ViewSeconds(double seconds){SetZoom((float)(Math.Max(1,Width-Names-vertical.Width-25)/Math.Max(.1,seconds)));horizontal.Value=0;Invalidate();}
    public void FitAll(){if(State!=null)ViewSeconds(State.duration/(double)State.fps);}
    public void FitSelection()
    {if(State==null||SelectedTrack==null)return;PixelsPerSecond=Math.Max(.5f,Math.Min(2400,(Width-Names-vertical.Width-24)*State.fps/(float)Math.Max(1,SelectedTrack.end-SelectedTrack.start)));RefreshLayout();horizontal.Value=Math.Max(0,Math.Min(horizontal.Maximum-horizontal.LargeChange+1,(int)(SelectedTrack.start/(double)State.fps*PixelsPerSecond)-10));Invalidate();}
    public void EnsureFrameVisible()
    {
        float x=X(Frame);if(x<Names+10||x>Width-vertical.Width-25)horizontal.Value=Math.Max(0,Math.Min(horizontal.Maximum-horizontal.LargeChange+1,(int)(Frame/(double)State.fps*PixelsPerSecond-horizontal.LargeChange*.2)));
    }
    public void PlayheadMoved(int oldFrame)
    {InvalidateCursor(X(oldFrame),PlayheadX);}
    private int fittedWidth;
    protected override void OnResize(EventArgs e){base.OnResize(e);RefreshLayout();if(InspectorKeyframes&&Width!=fittedWidth){fittedWidth=Width;FitSelection();}}
    private float X(double frame){return State==null?Names:Names+(float)(frame/State.fps*PixelsPerSecond)-horizontal.Value;}
    private int RawFrame(int x){return Math.Max(0,Math.Min(State.duration,(int)Math.Round((x-Names+horizontal.Value)/PixelsPerSecond*State.fps)));}
    private int ToFrame(int x){int value=RawFrame(x);return (ReadModifiers()&Keys.Alt)!=0?value:BeatGrid.Snap(State,value);}
    private void BeginScrub(int x){dragMode="seek";MoveScrub(x,true);}
    protected override void OnDragEnter(DragEventArgs e){string text=e.Data.GetData(DataFormats.Text) as string;e.Effect=text!=null&&new[]{"event-presets|","generation-presets|","presets|","camera-presets|","effect-presets|","custom-presets|"}.Any(text.StartsWith)?DragDropEffects.Copy:DragDropEffects.None;}
    protected override void OnDragOver(DragEventArgs e){OnDragEnter(e);}
    protected override void OnDragDrop(DragEventArgs e)
    {var text=e.Data.GetData(DataFormats.Text) as string;if(State==null||text==null||PresetDropped==null)return;int split=text.IndexOf('|');if(split<0)return;var p=PointToClient(new Point(e.X,e.Y));var lane=LaneAt(p);ActiveLane=lane!=null&&lane.index>=0?lane.index:Math.Max(0,State.laneCount-1);PresetDropped(text.Substring(0,split),text.Substring(split+1),lane==null?null:lane.track,p.X<Names?Frame:ToFrame(p.X));}
    private void DrawTimelineLayer(Graphics g,int width,int height)
    {
        g.Clear(Theme.Panel);if(State==null||width<=Names||height<=Header)return;
        using(var brush=new SolidBrush(Theme.Back))g.FillRectangle(brush,Names,0,width-Names,Header);
        using(var pen=new Pen(Theme.Line)){g.DrawLine(pen,Names,0,Names,height);g.DrawLine(pen,0,Header,width,Header);}
        Theme.Label(g,InspectorKeyframes?"属性关键帧":SelectedKeys.Count>1?"已选 "+SelectedKeys.Count+" 个关键帧":SelectedTracks.Count>1?"已选 "+SelectedTracks.Count+" 个物件":SelectionKeyframesOnly?"时间轴 · 物件剪辑":"时间轴 · ▸ 展开 · 空白框选",new Rectangle(8,0,Names-12,InspectorKeyframes?Ruler:28),Theme.Text,InspectorKeyframes?Font:Theme.Bold);
        var clip=g.Save();g.SetClip(new Rectangle(Names,0,width-Names,height));
        int tickWidth=InspectorKeyframes?Math.Max(65,Font.Height*4):65;double target=tickWidth/PixelsPerSecond;double[] steps={.02,.05,.1,.2,.5,1,2,5,10,15,30,60,120,300,600};double step=steps.FirstOrDefault(v=>v>=target);if(step==0)step=600;
        double left=horizontal.Value/PixelsPerSecond,right=(horizontal.Value+width-Names)/PixelsPerSecond;
        using(var pen=new Pen(Color.FromArgb(38,47,63)))for(double second=Math.Floor(left/step)*step;second<=right;second+=step)
        {float x=Names+(float)second*PixelsPerSecond-horizontal.Value;g.DrawLine(pen,x,Ruler-1,x,height);if(x>=Names-2)Theme.Label(g,step<1?second.ToString("0.00")+"s":String.Format("{0:00}:{1:00}",(int)second/60,(int)second%60),new Rectangle((int)x+4,0,Math.Max(1,Math.Min(tickWidth,width-(int)x-4)),Ruler),Theme.Muted,InspectorKeyframes?Font:Theme.Small);}
        g.Restore(clip);if(!InspectorKeyframes)DrawAudio(g,width);clip=g.Save();g.SetClip(new Rectangle(Names,0,width-Names,height));
        float endX=X(State.duration);using(var pen=new Pen(Color.FromArgb(160,210,180,95),2)){pen.DashStyle=DashStyle.Dash;g.DrawLine(pen,endX,0,endX,height);}g.Restore(clip);
        int first=Math.Max(0,RowIndex(vertical.Value)),last=lanes.Count;
        for(int i=first;i<last;i++){
            var lane=lanes[i];var t=lane.track;int y=Header+lane.top-vertical.Value;if(y>=height)break;var saved=g.Save();g.SetClip(new Rectangle(0,Header,width,height-Header));
            bool chosen=t!=null&&(SelectedTracks.Contains(t)||t==SelectedTrack)&&(lane.property==null||lane.property==SelectedProperty||lane.property=="@item");
            using(var bg=new SolidBrush(chosen?Color.FromArgb(47,44,62):lane.property!=null?Color.FromArgb(23,29,39):lane.index%2==0?Theme.Panel:Color.FromArgb(29,35,47)))g.FillRectangle(bg,0,y,width,lane.height);
            var group=TimelineGroups.At(State,lane.index);if(group!=null){using(var groupBrush=new SolidBrush(TimelineGroups.Background(group,chosen)))g.FillRectangle(groupBrush,0,y,width,lane.height);using(var stripe=new SolidBrush(Motion.ColorOf(group.color)))g.FillRectangle(stripe,0,y,4,lane.height);}
            if(lane.clips!=null){if(lane.stack==0)Theme.Label(g,Language.Text("轨道")+" "+(lane.index+1)+(group==null?"":" · "+group.name),new Rectangle(8,y,Names-16,lane.height),ActiveLane==lane.index?Theme.Accent:Theme.Text,flags:TimelineTextFlags,localize:false);}
            else {string title=TemplateLane(lane)?"    ▣ "+lane.track.name:SpawnLane(lane)?SpawnTitle(lane):lane.property==null?(IsExpanded(t)?"▾ ":"▸ ")+t.name:lane.property=="@item"?"▾ "+t.name:"    ◇ "+(lane.property.StartsWith("@gm:")?System.IO.Path.GetFileNameWithoutExtension(lane.property.Substring(4)):Language.Text(Motion.Name(lane.property)));Theme.Label(g,title,new Rectangle(8,y,Names-14,lane.height),chosen?Theme.Accent:Theme.Muted,InspectorKeyframes?Font:lane.property=="@item"?Theme.Font:Theme.Small,TimelineTextFlags,false);}
            if(lane.bindingKey!=null)using(var pen=new Pen(Color.FromArgb(90,119,220,221),2))g.DrawLine(pen,9+Math.Min(5,lane.indent)*8,y+3,9+Math.Min(5,lane.indent)*8,y+lane.height-3);
            g.SetClip(new Rectangle(Names,Header,width-Names,height-Header));
            if(lane.clips!=null)foreach(var item in lane.clips)DrawItem(g,item,null,y,width,lane.height);
            else if(TemplateLane(lane))DrawTemplateCard(g,lane,y,width);
            else if(SpawnLane(lane))DrawSpawnRule(g,lane,y,width);
            else if(lane.property=="@item"){if(TimelineLayout.HasGeneration(t))DrawItem(g,t,null,y,width,lane.height);}
            else DrawItem(g,t,lane.property,y,width,lane.height);
            using(var pen=new Pen(Theme.Line))g.DrawLine(pen,0,y+lane.height-1,width,y+lane.height-1);g.Restore(saved);
        }
        if(InspectorKeyframes&&lanes.Count==0)Theme.Label(g,SelectedTrack!=null&&MotionPresets.Bound(SelectedTrack)?"位置由动作预设控制":"启用属性旁的 ◇ 来添加关键帧",new Rectangle(8,Header+8,width-16,Font.Height*3),Theme.Muted,Theme.Small);
        DrawBeatGrid(g,width,height);
        if(dragMode=="bar"&&dragChanged){var saved=g.Save();g.SetClip(new Rectangle(Names,Header,width-Names,height-Header));paintingDragged=true;foreach(var pair in bars)if(SelectedTracks.Contains(pair.Key)){var r=DraggedBounds(pair.Key);DrawItem(g,pair.Key,null,(int)r.Y-3,width,(int)r.Height+6);}paintingDragged=false;g.Restore(saved);}
        if(dragMode=="box")
        {
            var saved=g.Save();g.SetClip(new Rectangle(Names,Header,width-Names,height-Header));var rect=BoxRectangle();rect.Offset(Names-horizontal.Value,Header-vertical.Value);
            using(var brush=new SolidBrush(Color.FromArgb(40,Theme.Accent)))g.FillRectangle(brush,rect);using(var pen=new Pen(Theme.Accent)){pen.DashStyle=DashStyle.Dash;g.DrawRectangle(pen,rect.X,rect.Y,rect.Width,rect.Height);}g.Restore(saved);
        }
    }
    private void DrawItem(Graphics g,Track t,string property,int y,int width,int rowHeight)
    {
        if(dragMode=="bar"&&dragChanged&&bars.ContainsKey(t)&&!paintingDragged)return;
        bool chosen=SelectedTracks.Contains(t)||t==SelectedTrack;Color color=t.enabled?Theme.TrackColor(t.kind):Color.FromArgb(80,87,100);float x=X(t.start),end=X(t.end+1);
        if(property==null&&EventMarker(t)){DrawEvent(g,t,y,width,rowHeight,chosen,color,x,end);return;}
        if(property==null){using(var brush=new SolidBrush(Color.FromArgb(chosen?225:155,color)))g.FillRectangle(brush,x,y+3,Math.Max(5,end-x),rowHeight-6);
            float barRight=x+Math.Max(5,end-x),visibleLeft=Math.Max(Names,x),visibleRight=Math.Min(width,barRight);int labelLeft=(int)Math.Ceiling(visibleLeft)+10,labelRight=(int)Math.Floor(visibleRight)-3;
            if(labelRight>labelLeft){var saved=g.Save();g.SetClip(new RectangleF(visibleLeft,y+3,visibleRight-visibleLeft,rowHeight-6),CombineMode.Intersect);Theme.Label(g,(SelectionKeyframesOnly&&!TimelineLayout.HasGeneration(t)?"":IsExpanded(t)?"▾ ":"▸ ")+(t.templateOnly?Language.Text("模板 · 局部时间 · "):"")+t.name,new Rectangle(labelLeft,y+(rowHeight-20)/2,labelRight-labelLeft,20),Color.White,Theme.Small,TimelineTextFlags,false);g.Restore(saved);}
            if(t==hoverTrack&&hoverEdge!=null)using(var pen=new Pen(Color.White,2)){float edge=hoverEdge=="left"?x:end;g.DrawLine(pen,edge,y+2,edge,y+rowHeight-2);g.DrawLine(pen,edge-5,y+rowHeight/2,edge+5,y+rowHeight/2);g.DrawLine(pen,edge-5,y+rowHeight/2,edge-2,y+rowHeight/2-3);g.DrawLine(pen,edge+5,y+rowHeight/2,edge+2,y+rowHeight/2+3);}
            if(State.tracks.Any(other=>other!=t&&other.kind!="native"&&!EventMarker(other)&&other.lane==t.lane&&other.end+1==t.start&&Math.Abs(ClipBounds(other).Y-(y+3))<1))using(var seam=new Pen(Color.White,1))g.DrawLine(seam,x,y+4,x,y+rowHeight-4);
        }else {using(var pen=new Pen(Color.FromArgb(80,color)))g.DrawLine(pen,x,y+rowHeight/2,end,y+rowHeight/2);if(InspectorKeyframes&&MotionPresets.Generated(t,property))Theme.Label(g,"由预设控制",new Rectangle(Names+10,y,Math.Max(0,width-Names-20),rowHeight),Theme.Muted,Theme.Small,TimelineTextFlags);}
        if(t.kind=="native"&&(!SelectionKeyframesOnly||t==SelectedTrack))foreach(var item in NativeEvents(new Lane{track=t,property=property})){float at=X(item.frame);if(at<Names||at>width)continue;using(var pen=new Pen(Color.FromArgb(255,205,110),2))g.DrawLine(pen,at,y+rowHeight/2-8,at,y+rowHeight/2+8);}
        if(SelectionKeyframesOnly&&t!=SelectedTrack)return;
        foreach(var key in t.keyframes){if(key.frame<t.start||key.frame>t.end||SelectionKeyframesOnly&&MotionPresets.Generated(t,key.property)||property==null&&!SelectionKeyframesOnly&&IsExpanded(t)||property!=null&&key.property!=property)continue;float kx=X(key.frame);if(kx<Names-7||kx>width+7)continue;int ky=property==null?y+rowHeight-6:y+rowHeight/2;float size=property==null?3:5;
            var points=new[]{new PointF(kx,ky-size),new PointF(kx+size,ky),new PointF(kx,ky+size),new PointF(kx-size,ky)};using(var brush=new SolidBrush(SelectedKeys.Contains(key)?Color.White:Color.FromArgb(255,218,137)))g.FillPolygon(brush,points);if(SelectedKeys.Contains(key))using(var pen=new Pen(Theme.Accent))g.DrawPolygon(pen,points);
        }
    }
    private Rectangle EventLabelBounds(Track t,int y,int width,int rowHeight)
    {
        float x=X(t.start),end=X(t.end+1);bool span=EventCanStretch(t)&&t.end>t.start;
        if(x-EventRadius>=width||(!span&&x+EventRadius<Names)||(span&&Math.Max(x+EventRadius,end)<Names))return Rectangle.Empty;
        int left=(int)Math.Ceiling(Math.Max(Names+4,x+EventRadius+6)),right=width-4;
        foreach(var other in State.tracks.Where(o=>o!=t&&o.kind!="native"&&!o.templateOnly&&o.lane==t.lane&&o.start>t.start)){
            int candidate=(int)Math.Floor(X(other.start)-(EventMarker(other)?EventRadius+2:2));if(candidate>=right)continue;
            var bounds=ClipBounds(other);if(bounds.IsEmpty||Math.Abs(bounds.Y-(y+3))>=1)continue;
            right=candidate;if(right<=left)return Rectangle.Empty;
        }
        if(right<=left||String.IsNullOrEmpty(t.name))return Rectangle.Empty;
        int measured=TextRenderer.MeasureText(t.name,Theme.Small,Size.Empty,TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix).Width;
        return new Rectangle(left,y+(rowHeight-20)/2,Math.Min(right-left,measured+2),20);
    }
    private void DrawEvent(Graphics g,Track t,int y,int width,int rowHeight,bool chosen,Color color,float x,float end)
    {
        float center=y+rowHeight/2f;
        if(EventCanStretch(t)&&t.end>t.start)using(var pen=new Pen(Color.FromArgb(chosen?210:135,color),2)){g.DrawLine(pen,x,center,end,center);g.DrawLine(pen,end,center-5,end,center+5);}
        var smoothing=g.SmoothingMode;g.SmoothingMode=SmoothingMode.AntiAlias;
        using(var brush=new SolidBrush(Color.FromArgb(chosen?240:205,color)))g.FillEllipse(brush,x-EventRadius,center-EventRadius,EventRadius*2,EventRadius*2);
        using(var pen=new Pen(chosen?Theme.Accent:Color.FromArgb(180,color),chosen?2:1))g.DrawEllipse(pen,x-EventRadius,center-EventRadius,EventRadius*2,EventRadius*2);g.SmoothingMode=smoothing;
        var label=EventLabelBounds(t,y,width,rowHeight);if(!label.IsEmpty)Theme.Label(g,t.name,label,Color.White,Theme.Small,TimelineTextFlags,false);
        if(t==hoverTrack&&hoverEdge!=null){float edge=hoverEdge=="left"?x-EventRadius:Math.Max(end,x+EventRadius+2);using(var pen=new Pen(Color.White,2)){g.DrawLine(pen,edge,center-7,edge,center+7);g.DrawLine(pen,edge-4,center,edge+4,center);}}
    }
    public RectangleF ClipBounds(Track track)
    {if(dragMode=="bar"&&dragChanged&&bars.ContainsKey(track))return DraggedBounds(track);int row=lanes.FindIndex(l=>l.clips!=null&&l.clips.Contains(track)||l.track==track&&(l.property==null||l.property=="@item"&&TimelineLayout.HasGeneration(track)));if(row<0){row=lanes.FindIndex(l=>l.track==track&&l.property=="@template");return row<0?RectangleF.Empty:TemplateCardBounds(lanes[row],Header+lanes[row].top-vertical.Value,Width-vertical.Width);}
        return new RectangleF(X(track.start),Header+lanes[row].top-vertical.Value+3,Math.Max(5,X(track.end+1)-X(track.start)),lanes[row].height-6);}
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
        base.OnMouseDown(e);if(State==null)return;Focus();dragStart=e.Location;dragStartX=e.X+horizontal.Value;dragChanged=false;dragMode=null;if(e.Button==MouseButtons.Left&&BeginHeight(e.Location))return;if(e.Button==MouseButtons.Right&&e.Y>=Ruler&&e.Y<Header)return;
        var lane=LaneAt(e.Location);var t=lane==null?null:lane.track;var key=KeyAt(lane,e.Location);string edge=EdgeAt(lane,e.Location);if(lane!=null&&lane.index>=0)ActiveLane=lane.index;
        KeyboardPreviewNavigation=e.Button==MouseButtons.Left&&(e.Y<Header||e.X>=Names)&&(t==null||key==null&&edge==null&&(t.kind=="native"?NativeAt(lane,e.X)==null:lane.property!=null));
        if(t!=null&&t.kind=="native"){ResetSelection(t,null);NotifySelection(t,null);if(e.Button==MouseButtons.Left){if(e.X<27&&lane.property==null)Expand(t,!IsExpanded(t));else if(e.X>=Names){var item=NativeAt(lane,e.X);if(SelectNativeEvent!=null)SelectNativeEvent(item,false);BeginScrub(e.X);Capture=true;}}return;}
        bool additive=(ModifierKeys&(Keys.Control|Keys.Shift))!=0;
        if(e.Button==MouseButtons.Right){if(t!=null){if(key!=null){if(!SelectedKeys.Contains(key))ResetSelection(t,key);NotifySelection(t,key);}else{if(!SelectedTracks.Contains(t))ResetSelection(t,null);NotifySelection(t,null);}}else if(lane!=null&&lane.clips!=null&&e.X<Names)SetTrackSelection(State.tracks.Where(item=>item.kind!="native"&&item.lane==lane.index));else{ResetSelection(null,null);NotifySelection(null,null);}return;}
        if(e.Button!=MouseButtons.Left)return;
        if(TemplateLane(lane)){
            var rect=TemplateCardBounds(lane,Header+lane.top-vertical.Value,Width-vertical.Width);
            if(SelectionKeyframesOnly&&(e.X<27||e.X>=rect.Left&&e.X<rect.Left+27)&&!TimelineLayout.HasGeneration(t)){SelectTemplateCard(lane,false);if(EditKeyframes!=null)EditKeyframes(t);return;}
            if((e.X<27||e.X>=rect.Left&&e.X<rect.Left+27)&&(TimelineLayout.HasGeneration(t)||t.keyframes.Count>0)){
                bool open=!IsExpanded(t);Expand(t,open);if(open&&t.keyframes.Count>0)SelectTemplateCard(lane,true);return;
            }SelectTemplateCard(lane,false);return;
        }
        if(SpawnLane(lane)){PickTrack(t,false);if(e.X>=Names){BeginScrub(e.X);Capture=true;}return;}
        if(e.Y<Header){if(e.X>=Names){BeginScrub(e.X);}}
        else if(e.X<Names){
            if(t!=null){if(e.X<27&&(lane.property==null||lane.property=="@item")){Expand(t,!IsExpanded(t));return;}if(lane.property!=null&&!lane.property.StartsWith("@"))SelectedProperty=lane.property;PickTrack(t,additive);}
            else if(lane!=null&&lane.clips!=null){SetTrackSelection(State.tracks.Where(item=>item.kind!="native"&&item.lane==lane.index));KeyboardPreviewNavigation=true;}return;
        }
        else if(t!=null&&!EventMarker(t)&&(lane.property==null||lane.property=="@item")&&e.X>=X(t.start)+9&&e.X<X(t.start)+25&&(!SelectionKeyframesOnly||TimelineLayout.HasGeneration(t))){PickTrack(t,additive);Expand(t,!IsExpanded(t));return;}
        else if(edge!=null){if(!SelectedTracks.Contains(t))PickTrack(t,additive);else{ClearKeySelection();NotifySelection(t,null);}PrepareBar(t,edge);}
        else if(key!=null){SelectedTracks.Clear();SelectedTracks.Add(t);if(additive&&SelectedKeys.Contains(key)){SelectedKeys.Remove(key);var remaining=SelectedKeys.FirstOrDefault();NotifySelection(remaining==null?t:Owner(remaining),remaining);return;}if(!SelectedKeys.Contains(key)){if(!additive)SelectedKeys.Clear();SelectedKeys.Add(key);}if(lane.property!=null)SelectedProperty=lane.property;NotifySelection(t,key);if(SelectionKeyframesOnly){if(Seek!=null)Seek(key.frame);if(EditKeyframes!=null)EditKeyframes(t);return;}dragTrack=t;dragKey=key;dragMode="key";groupFrames.Clear();foreach(var selected in SelectedKeys)groupFrames[selected]=selected.frame;keyDeltaBounds=KeyMoveBounds(groupFrames);}
        else if(t!=null&&lane.property==null){if(additive){if(!PickTrack(t,true))return;}else if(!SelectedTracks.Contains(t))PickTrack(t,false);else{ClearKeySelection();NotifySelection(t,null);}PrepareBar(t,"bar");}
        else if(t!=null&&lane.property=="@item"){PickTrack(t,additive);if(TimelineLayout.HasGeneration(t))PrepareBar(t,"bar");}
        else {if(lane!=null&&lane.property!=null)SelectedProperty=lane.property;BeginBox(e.Location,!InspectorKeyframes&&(lane==null||lane.property==null),additive);}
        if(dragMode!=null)Capture=true;
    }
    private bool PickTrack(Track track,bool additive)
    {
        ClearKeySelection();if(!additive)SelectedTracks.Clear();
        if(additive&&SelectedTracks.Contains(track)){SelectedTracks.Remove(track);NotifySelection(SelectedTracks.FirstOrDefault(),null);return false;}
        SelectedTracks.Add(track);NotifySelection(track,null);return true;
    }
    private Lane LaneAt(Point point)
    {
        int i=RowIndex(point.Y-Header+vertical.Value);if(point.Y<Header||i<0||i>=lanes.Count)return null;var lane=lanes[i];if(lane.clips==null||point.X<Names)return lane;
        var marker=lane.clips.Where(item=>EventMarker(item)&&Math.Abs(point.X-X(item.start))<=EventRadius+6).OrderBy(item=>Math.Abs(point.X-X(item.start))).ThenBy(item=>item==SelectedTrack?0:1).FirstOrDefault();
        Func<Track,bool> eventEnd=item=>EventMarker(item)&&EventCanStretch(item)&&point.X>=X(item.start)+EventRadius&&Math.Abs(point.X-Math.Max(X(item.end+1),X(item.start)+EventRadius+2))<=3;
        var inside=lane.clips.Where(item=>(!EventMarker(item)||EventCanStretch(item)&&item.end>item.start)&&point.X>=X(item.start)&&point.X<=X(item.end+1)).ToArray();int y=Header+lane.top-vertical.Value;
        var t=marker??lane.clips.FirstOrDefault(item=>item==SelectedTrack&&eventEnd(item))??inside.FirstOrDefault(item=>item==SelectedTrack)??inside.FirstOrDefault()??lane.clips.FirstOrDefault(eventEnd)??lane.clips.FirstOrDefault(item=>EventMarker(item)&&EventLabelBounds(item,y,Width-vertical.Width,lane.height).Contains(point))??lane.clips.FirstOrDefault(item=>!EventMarker(item)&&point.X>=X(item.start)-8&&point.X<=X(item.end+1)+8);
        return t==null?lane:new Lane{track=t,index=lane.index,stack=lane.stack,top=lane.top,height=lane.height};
    }
    private string EdgeAt(Lane lane,Point point)
    {if(lane==null||lane.track==null||lane.track.kind=="native"||lane.property!=null&&lane.property!="@item"||point.X<Names)return null;var t=lane.track;float x=X(t.start),end=X(t.end+1);if(EventMarker(t)){if(!EventCanStretch(t))return null;float right=Math.Max(end,x+EventRadius+2);if(point.X>=x+EventRadius&&Math.Abs(point.X-right)<=3)return "right";if(t.end>t.start&&point.X<=x-EventRadius+1&&Math.Abs(point.X-(x-EventRadius))<=4)return "left";return null;}double left=Math.Abs(point.X-x),distance=Math.Abs(point.X-end);if(Math.Min(left,distance)>8)return null;return left<distance?"left":"right";}
    private IEnumerable<NativeEvent> NativeEvents(Lane lane){if(State.nativeProject==null)return new NativeEvent[0];return State.nativeProject.events.Where(e=>lane.property==null||lane.property=="@gm:"+e.file);}
    private NativeEvent NativeAt(Lane lane,int x){return NativeEvents(lane).OrderBy(e=>Math.Abs(X(e.frame)-x)).FirstOrDefault(e=>Math.Abs(X(e.frame)-x)<8);}
    private Keyframe KeyAt(Lane lane,Point point)
    {if(lane==null||lane.track==null||point.X<Names||lane.property!=null&&lane.property.StartsWith("@")||SelectionKeyframesOnly&&lane.track!=SelectedTrack||lane.property==null&&!SelectionKeyframesOnly&&IsExpanded(lane.track))return null;int center=Header+lane.top-vertical.Value+(lane.property==null?lane.height-6:lane.height/2);if(Math.Abs(point.Y-center)>8)return null;return lane.track.keyframes.Where(k=>k.frame>=lane.track.start&&k.frame<=lane.track.end&&!MotionPresets.Generated(lane.track,k.property)&&(lane.property==null||k.property==lane.property)).OrderBy(k=>Math.Abs(X(k.frame)-point.X)).FirstOrDefault(k=>Math.Abs(X(k.frame)-point.X)<8);}
    private void PrepareBar(Track track,string mode)
    {
        dragTrack=track;dragMode=mode;bars.Clear();dragRowLanes=lanes.Select(l=>l.index).ToArray();dragRowTops=lanes.Select(l=>l.top).ToArray();dragRowHeights=lanes.Select(l=>l.height).ToArray();dragLaneCount=State.laneCount;
        dragVisualRow=Math.Max(0,lanes.FindIndex(l=>l.track==track&&(l.property==null||l.property=="@item")||l.clips!=null&&l.clips.Contains(track)));
        foreach(var t in mode=="bar"?ExpandedSelection():SelectedTracks){int row=lanes.FindIndex(l=>l.track==t&&(l.property==null||l.property=="@item")||l.clips!=null&&l.clips.Contains(t));bars[t]=new BarState{start=t.start,end=t.end,lane=t.lane,release=t.releaseFrame,clockStart=t.clockStart,clockEnd=t.clockEnd,preset=t.motionPreset==null?null:t.motionPreset.Snapshot(),camera=t.cameraPreset==null?null:t.cameraPreset.Snapshot(),top=row<0?lanes[dragVisualRow].top:lanes[row].top,height=row<0?Row:lanes[row].height,keys=t.keyframes.ToDictionary(k=>k,k=>k.frame)};}
    }
    private RectangleF DraggedBounds(Track track)
    {var b=bars[track];int top=dragVisualRow<dragRowTops.Length?dragRowTops[dragVisualRow]:dragRowTops.Last()+dragRowHeights.Last()+Row*(dragVisualRow-dragRowTops.Length);return new RectangleF(X(track.start),Header+top+b.top-bars[dragTrack].top-vertical.Value+3,Math.Max(5,X(track.end+1)-X(track.start)),b.height-6);}
    private int SnapClipDelta(int delta,string mode)
    {
        clipSnapGuide=null;if(!ClipSnap||(ReadModifiers()&Keys.Alt)!=0)return delta;double best=8.0*State.fps/PixelsPerSecond;int result=delta;
        Action<int,int> consider=(edge,target)=>{int snap=target-edge;double distance=Math.Abs((double)snap-delta);if(distance<best){best=distance;result=snap;clipSnapGuide=target;}};
        foreach(var b in bars.Values){if(mode!="right")consider(b.start,Frame);if(mode!="left")consider(b.end+1,Frame);}
        foreach(var pair in bars)foreach(var other in State.tracks.Where(o=>!bars.ContainsKey(o)&&o.kind!="native"&&!o.templateOnly)){
            var b=pair.Value;if(mode!="right"){consider(b.start,other.start);consider(b.start,other.end+1);}if(mode!="left"){consider(b.end+1,other.start);consider(b.end+1,other.end+1);}
        }return result;
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
        boxEnd=ContentPoint(point);var rect=BoxRectangle();SelectedKeys.Clear();SelectedKeys.UnionWith(boxInitial);if(boxTracks){SelectedTracks.Clear();SelectedTracks.UnionWith(boxTrackInitial);}
        for(int i=0;i<lanes.Count;i++){var lane=lanes[i];float y=lane.top+lane.height/2;bool centerInside=y>=rect.Top&&y<=rect.Bottom;
            if(boxTracks){if(lane.clips!=null)foreach(var t in lane.clips)if((centerInside||EventMarker(t)&&y+EventRadius>=rect.Top&&y-EventRadius<=rect.Bottom)&&BoxIntersects(t,rect))SelectedTracks.Add(t);if(centerInside&&lane.property=="@item"&&BoxIntersects(lane.track,rect))SelectedTracks.Add(lane.track);continue;}
            if(!centerInside)continue;
            if(lane.track==null||lane.track.kind=="native"||lane.property==null||lane.property.StartsWith("@"))continue;
            foreach(var key in lane.track.keyframes){if(key.property!=lane.property||MotionPresets.Generated(lane.track,key.property)||key.frame<lane.track.start||key.frame>lane.track.end)continue;float x=key.frame/(float)State.fps*PixelsPerSecond;if(x>=rect.Left&&x<=rect.Right)SelectedKeys.Add(key);}
        }Invalidate();
    }
    private bool BoxIntersects(Track track,RectangleF bounds)
    {float left=track.start/(float)State.fps*PixelsPerSecond,right=(EventMarker(track)&&!EventCanStretch(track)?track.start:track.end)/(float)State.fps*PixelsPerSecond;if(EventMarker(track)){left-=EventRadius;right+=EventRadius;}return left<=bounds.Right&&right>=bounds.Left;}
    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);if(State==null)return;string boundary=HeightBoundary(e.Location);if(boundary!=null){if(boundary=="@audio")SetAudioHeight(audioDisplay=="spectrogram"?180:50);else SetRowHeight(boundary,Row);return;}if(e.X<Names||e.Y<Header)return;var lane=LaneAt(e.Location);if(lane==null||lane.track==null)return;
        if(lane.track.kind=="native"){var item=NativeAt(lane,e.X);if(item!=null&&SelectNativeEvent!=null)SelectNativeEvent(item,true);return;}
        if(TemplateLane(lane)){SelectTemplateCard(lane,true);return;}
        if(SpawnLane(lane)){if(lane.property.StartsWith("@spawn:")){if(EditSpawnRule!=null)EditSpawnRule(lane.track,SpawnIndex(lane));}else if(EditBulletSpawnRule!=null)EditBulletSpawnRule(lane.track,SpawnIndex(lane));return;}
        if(lane.property!=null&&!lane.property.StartsWith("@")&&InsertKey!=null)InsertKey(lane.track,lane.property,ToFrame(e.X));
        else if(lane.property==null||lane.property=="@item"){if(SelectionKeyframesOnly){PickTrack(lane.track,false);if(EditKeyframes!=null)EditKeyframes(lane.track);}else Expand(lane.track,!IsExpanded(lane.track));}
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);if(State==null)return;if(dragMode=="height"){int value=resizeHeight+e.Y-dragStart.Y;if(resizeKey=="@audio")SetAudioHeight(value);else SetRowHeight(resizeKey,value);return;}
        if(dragMode==null){if(HeightBoundary(e.Location)!=null){Cursor=Cursors.HSplit;Language.ToolTip(tips,this,"拖动下边缘调整此轨道高度；双击还原");return;}if(e.Y>=Ruler&&e.Y<Header){Cursor=Cursors.Default;Language.ToolTip(tips,this,"右键切换波形图 / 频谱图；拖动下边缘调整音频轨高度");return;}var lane=LaneAt(e.Location);var t=lane==null?null:lane.track;
            if(t!=null&&t.kind=="native"){var item=NativeAt(lane,e.X);Cursor=Cursors.Hand;Language.ToolTip(tips,this,item==null?"GM 原生轨道：只读定位；双击编辑原 GML。":item.file+":"+item.line+" · "+item.expression+"\n"+item.snippet);return;}
            if(TemplateLane(lane)){Cursor=Cursors.Hand;Language.ToolTip(tips,this,lane.track.name+"\n"+TemplateSummary(lane.track)+"\n"+Language.Text("单击编辑模板参数；双击打开关联规则的完整编辑窗口。右键可预览模板局部动画。共用模板的修改会影响所有引用。"));return;}
            if(SpawnLane(lane)){Cursor=Cursors.Hand;Language.ToolTip(tips,this,"生成触发位置（歌曲时间）：单击定位；双击编辑此规则。逐成员延迟与重复均已计入。");return;}
            string edge=EdgeAt(lane,e.Location);var key=KeyAt(lane,e.Location);hoverTrack=edge==null?null:t;hoverEdge=edge;Cursor=edge!=null?Cursors.SizeWE:key!=null||t!=null&&lane.property==null?Cursors.SizeAll:Cursors.Default;
            string tip=edge=="left"?"左右拖动：修改开始帧；关键帧按“拉伸关键帧”设置处理":edge=="right"?"左右拖动：修改结束帧；关键帧按“拉伸关键帧”设置处理":SelectionKeyframesOnly&&key!=null?"选中标记：在右栏编辑关键帧":t!=null&&lane.property==null?t.name+" · 拖动移动时间与轨道":"空白框选物件或关键帧；Ctrl / Shift 加选";if(tips.GetToolTip(this)!=tip)Language.ToolTip(tips,this,tip);Invalidate();return;
        }
        if(e.X>Width-vertical.Width-15)ScrollHorizontal(20);else if(e.X<Names+5)ScrollHorizontal(-20);
        if(dragMode=="seek"){MoveScrub(e.X,false);return;}
        if(dragMode=="box"){if(e.Y>Height-horizontal.Height-12)ScrollVertical(Row/2);else if(e.Y<Header+5)ScrollVertical(-Row/2);UpdateBox(e.Location);return;}
        if(dragMode=="bar"){if(e.Y>Height-horizontal.Height-12)ScrollVertical(Row/2);else if(e.Y<Header+5)ScrollVertical(-Row/2);}
        if(!dragChanged&&Math.Abs(e.X-dragStart.X)+Math.Abs(e.Y-dragStart.Y)<3)return;
        if(!dragChanged){if(BeginEdit!=null)BeginEdit();dragChanged=true;}
        int delta=(int)Math.Round((e.X+horizontal.Value-dragStartX)/PixelsPerSecond*State.fps);
        if(bars.Count>0){if(dragMode=="bar"){dragVisualRow=DragRow(e.Y);int target=dragVisualRow<dragRowLanes.Length?dragRowLanes[dragVisualRow]:dragLaneCount+dragVisualRow-dragRowLanes.Length;if(target>=0){int laneDelta=Math.Max(-bars.Values.Min(v=>v.lane),target-bars[dragTrack].lane);foreach(var pair in bars)pair.Key.lane=pair.Value.lane+laneDelta;}}delta=SnapClipDelta(delta,dragMode);}
        if(dragMode=="key")
        {delta=Math.Max(keyDeltaBounds.X,Math.Min(keyDeltaBounds.Y,delta));foreach(var pair in groupFrames)pair.Key.frame=pair.Value+delta;if(Seek!=null)Seek(dragKey.frame);}
        else if(bars.Count>0)
        {
            if(dragMode=="bar")
            {int earliest=bars.Min(p=>p.Key.clockStart.HasValue?p.Value.start:Math.Min(p.Value.start,p.Value.keys.Count==0?p.Value.start:p.Value.keys.Values.Min())),latest=bars.Max(p=>p.Key.clockStart.HasValue?p.Value.end:Math.Max(p.Value.end,p.Value.keys.Count==0?p.Value.end:p.Value.keys.Values.Max()));delta=Math.Max(-earliest,Math.Min(State.duration-latest,delta));}
            else if(dragMode=="left")delta=Math.Max(-bars.Values.Min(v=>v.start),Math.Min(bars.Values.Min(v=>v.end-v.start),delta));
            else delta=Math.Max(bars.Values.Max(v=>v.start-v.end),Math.Min(State.duration-bars.Values.Max(v=>v.end),delta));
            foreach(var pair in bars)
            {var t=pair.Key;var v=pair.Value;t.start=v.start+(dragMode=="right"?0:delta);t.end=v.end+(dragMode=="left"?0:delta);if(dragMode=="bar"){foreach(var k in v.keys)k.Key.frame=k.Value+delta;if(v.release>=0)t.releaseFrame=v.release+delta;t.clockStart=v.clockStart.HasValue?v.clockStart+delta:null;t.clockEnd=v.clockEnd.HasValue?v.clockEnd+delta:null;if(v.preset!=null)t.motionPreset=v.preset.Snapshot();if(v.camera!=null)t.cameraPreset=v.camera.Snapshot();if(v.preset!=null||v.camera!=null)MotionPresets.Shift(t,delta);}}
        }
        if(dragMode=="bar"&&bars.Count>0){int row=DragRow(e.Y),target=row<dragRowLanes.Length?dragRowLanes[row]:dragLaneCount+row-dragRowLanes.Length;
            if(target>=0){int laneDelta=target-bars[dragTrack].lane;laneDelta=Math.Max(-bars.Values.Min(v=>v.lane),laneDelta);foreach(var pair in bars)pair.Key.lane=pair.Value.lane+laneDelta;ActiveLane=dragTrack.lane;State.laneCount=Math.Max(State.laneCount,bars.Keys.Max(t=>t.lane)+1);}RefreshLayout();}
        if(PreviewChanged!=null)PreviewChanged();Invalidate();
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);FinishGesture();Capture=false;
    }
    private void FinishGesture()
    {
        string mode=dragMode;float oldPlayhead=PlayheadX;dragMode=null;if(mode=="seek"){scrub.End((ReadModifiers()&Keys.Alt)!=0);scrubPosition=null;InvalidateCursor(oldPlayhead,PlayheadX);PresentPlayhead();}bool cutFailed=false;
        if(dragChanged&&(mode=="bar"||mode=="left"||mode=="right")){
            if(mode!="bar")foreach(var pair in bars)TimelineEditing.Resize(pair.Key,pair.Value.start,pair.Value.end,pair.Key.start,pair.Key.end,StretchKeys);
            cutFailed=!TimelineLayout.CutOverlaps(State,bars.Keys);RandomParameters.Attach(State);RefreshLayout();
        }
        if(mode=="key"&&dragChanged)MergeSelectedKeys();
        if(mode=="box"){if(boxTracks)NotifySelection(SelectedTracks.FirstOrDefault(),null);else{var key=SelectedKeys.FirstOrDefault();var owner=key==null?(InspectorKeyframes?SelectedTrack:null):Owner(key);SelectedTracks.Clear();if(owner!=null)SelectedTracks.Add(owner);NotifySelection(owner,key);}}
        if(dragChanged&&mode!="box"&&mode!="seek"&&mode!="height"&&Changed!=null)Changed();if(cutFailed&&EditNotice!=null)EditNotice("裁剪后物件数量超过上限，未裁剪其他物件。");clipSnapGuide=null;dragTrack=null;dragKey=null;dragRowTops=null;dragRowHeights=null;dragRowLanes=null;resizeKey=null;groupFrames.Clear();bars.Clear();dragChanged=false;Cursor=Cursors.Default;if(mode!="seek")Invalidate();
    }
    protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture&&dragMode!=null)FinishGesture();}
    protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);if(dragMode==null){hoverTrack=null;hoverEdge=null;Cursor=Cursors.Default;Invalidate();}}
    protected override void OnKeyDown(KeyEventArgs e){if(e.Alt||e.KeyCode==Keys.Menu){scrub.BypassNow();PresentPlayhead();}base.OnKeyDown(e);}
    private void ScrollVertical(int pixels){vertical.Value=Math.Max(0,Math.Min(vertical.Maximum-vertical.LargeChange+1,vertical.Value+pixels));}
    private void ScrollHorizontal(int pixels){horizontal.Value=Math.Max(0,Math.Min(horizontal.Maximum-horizontal.LargeChange+1,horizontal.Value+pixels));}
    protected override void OnMouseWheel(MouseEventArgs e)
    {if((ModifierKeys&Keys.Control)!=0)SetZoom(PixelsPerSecond*(e.Delta>0?1.25f:.8f),e.X);else if((ModifierKeys&Keys.Shift)!=0||!vertical.Enabled)ScrollHorizontal(e.Delta>0?-100:100);else vertical.Value=Math.Max(0,Math.Min(vertical.Maximum-vertical.LargeChange+1,vertical.Value-(e.Delta>0?Row*2:-Row*2)));}
}
