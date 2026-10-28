using System;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

// Frame positions are integrated from elapsed time, independently of OS key repeat.
internal sealed class PreviewTransport
{
    public double Position {get;private set;}
    public double Elapsed {get;private set;}
    public int Direction {get;private set;}
    public bool Held {get;private set;}
    public bool Running {get;private set;}
    public void Start(int frame,int direction,bool held){Position=frame;Direction=Math.Sign(direction);Held=held;Elapsed=0;Running=Direction!=0;}
    public void Stop(){Running=false;}
    private static double HoldIntegral(double elapsed)
    {
        double t=Math.Max(0,elapsed-.25),u=Math.Min(1,t/.8);
        return .8*(.1*u+.9*(u*u*u-.5*u*u*u*u))+Math.Max(0,t-.8);
    }
    public double Velocity(double multiplier)
    {double u=Math.Max(0,Math.Min(1,(Elapsed-.25)/.8));return !Running||Held&&Elapsed<.25?0:Direction*multiplier*(Held?.1+.9*u*u*(3-2*u):1);}
    public int Advance(double seconds,double multiplier,int fps,int duration)
    {
        if(!Running)return (int)Math.Round(Position);
        double next=Elapsed+Math.Max(0,seconds),distance=Held?HoldIntegral(next)-HoldIntegral(Elapsed):next-Elapsed;
        Position=Math.Max(0,Math.Min(duration,Position+Direction*distance*fps*multiplier));Elapsed=next;
        if(Direction<0&&Position<=0||Direction>0&&Position>=duration)Running=false;
        return Math.Max(0,Math.Min(duration,(int)Math.Round(Position,MidpointRounding.AwayFromZero)));
    }
    public static bool TryRate(string text,out double rate)
    {
        text=(text??"").Trim().TrimEnd('x','X','×','倍').Trim();
        return (Double.TryParse(text,NumberStyles.Float,CultureInfo.CurrentCulture,out rate)||Double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out rate))&&!Double.IsNaN(rate)&&!Double.IsInfinity(rate)&&rate>=.05&&rate<=8;
    }
}

internal static class CursorSnapping
{
    public static Func<int,int?> Capture(EditorState state,Track selected,double tolerance,bool keys)
    {
        var frames=new System.Collections.Generic.HashSet<int>();Action<int> add=f=>{if(f>=0&&f<=state.duration)frames.Add(f);};
        if(keys){foreach(var t in state.tracks){if(t.kind=="native"||t.templateOnly||InTemplate(t))continue;add(t.start);add(t.end);add(t.end+1);foreach(var k in t.keyframes)if(k.frame>=t.start&&k.frame<=t.end&&!MotionPresets.Generated(t,k.property))add(k.frame);}
            if(selected!=null&&(selected.templateOnly||!state.tracks.Contains(selected))){add(selected.start);add(selected.end);add(selected.end+1);foreach(var k in selected.keyframes)if(k.frame>=selected.start&&k.frame<=selected.end&&!MotionPresets.Generated(selected,k.property))add(k.frame);}
            if(state.nativeProject!=null&&state.nativeProject.events!=null)foreach(var e in state.nativeProject.events)add(e.frame);}
        int[] sorted=frames.OrderBy(f=>f).ToArray();var beat=new EditorState{fps=state.fps,duration=state.duration,bpm=state.bpm,beatDivision=state.beatDivision,beatOffsetMs=state.beatOffsetMs,beatSnap=state.beatSnap};
        return value=>{int at=Array.BinarySearch(sorted,value);if(at>=0)return sorted[at];at=~at;int? target=null;if(at>0&&value-(double)sorted[at-1]<=tolerance)target=sorted[at-1];if(at<sorted.Length&&sorted[at]-(double)value<=tolerance&&(!target.HasValue||sorted[at]-(double)value<value-(double)target.Value))target=sorted[at];return target??(beat.beatSnap?(int?)BeatGrid.Snap(beat,value):null);};
    }
    public static int Snap(EditorState state,Track track,int value,double tolerance,bool keys)
    {
        if(keys){int nearest=value;double best=Double.MaxValue;Action<int> consider=f=>{if(f<0||f>state.duration)return;double distance=Math.Abs((double)f-value);if(distance<=tolerance&&(distance<best||distance==best&&f<nearest)){best=distance;nearest=f;}};
            foreach(var t in state.tracks){if(t.kind=="native"||t.templateOnly||InTemplate(t))continue;consider(t.start);consider(t.end);consider(t.end+1);foreach(var k in t.keyframes)if(k.frame>=t.start&&k.frame<=t.end&&!MotionPresets.Generated(t,k.property))consider(k.frame);if(best==0)return nearest;}
            if(track!=null&&(track.templateOnly||!state.tracks.Contains(track))){consider(track.start);consider(track.end);consider(track.end+1);foreach(var k in track.keyframes)if(k.frame>=track.start&&k.frame<=track.end&&!MotionPresets.Generated(track,k.property))consider(k.frame);}
            if(state.nativeProject!=null&&state.nativeProject.events!=null)foreach(var e in state.nativeProject.events)consider(e.frame);
            if(best<Double.MaxValue)return nearest;}
        return BeatGrid.Snap(state,value);
    }
    private static bool InTemplate(Track track){for(var parent=track.parent;parent!=null;parent=parent.parent)if(parent.templateOnly)return true;return false;}
}

// The moving playhead is always raw. A cached search supplies a visual hint;
// release commits the hint, while Alt suppresses both the hint and the snap.
internal sealed class CursorScrub : IDisposable
{
    private int raw,last=-1;private bool bypassed;private Func<int,int?> targets;
    public bool Active {get;private set;}
    public int? Guide {get;private set;}
    public Action<int> Seek;public Action<int?> GuideChanged;public Func<int,int> Snap;public Func<Func<int,int?>> PrepareTargets;
    public void Begin(int frame,bool alt){Cancel();targets=PrepareTargets==null?null:PrepareTargets();Active=true;last=-1;Move(frame,alt);}
    public void Move(int frame,bool alt){if(!Active)return;raw=frame;bypassed=alt;Settle(alt);Deliver(frame);}
    public void BypassNow(){if(!Active)return;bypassed=true;SetGuide(null);}
    private void Deliver(int frame){if(frame==last)return;last=frame;if(Seek!=null)Seek(frame);}
    private void SetGuide(int? value){if(Guide==value)return;Guide=value;if(GuideChanged!=null)GuideChanged(value);}
    internal void Settle(bool alt){if(!Active)return;SetGuide(bypassed||alt?null:targets!=null?targets(raw):Snap==null?null:(int?)Snap(raw));}
    public void End(bool alt){if(!Active)return;Settle(alt);int frame=Guide??raw;Active=false;SetGuide(null);targets=null;Deliver(frame);}
    public void Cancel(){Active=false;SetGuide(null);targets=null;}
    public void Dispose(){Cancel();}
}

internal sealed partial class MainForm
{
    private readonly PreviewTransport previewTransport=new PreviewTransport();
    private readonly ComboBox previewSpeed=new WheelSafeComboBox();
    private double previewRate=1,previewTickTime;
    private Keys timelineArrow=Keys.None;
    private void BuildPreviewPlayback(Control bar)
    {
        previewRate=restoredView!=null&&restoredView.previewRate>=.05&&restoredView.previewRate<=8?restoredView.previewRate:1;
        bar.Controls.Add(new Label{Text="预览播放倍速",AutoSize=true,ForeColor=Theme.Muted,Margin=new Padding(8,9,2,0)});
        previewSpeed.Width=95;previewSpeed.DropDownStyle=ComboBoxStyle.DropDown;previewSpeed.BackColor=Theme.Raised;previewSpeed.ForeColor=Theme.Text;previewSpeed.Margin=new Padding(2,5,10,0);
        previewSpeed.Items.AddRange(new object[]{"0.5×","0.75×","1×","1.25×","1.5×","2×"});previewSpeed.Text=previewRate.ToString("0.##",CultureInfo.CurrentCulture)+"×";
        previewSpeed.SelectionChangeCommitted+=(s,e)=>ApplyPreviewRate();previewSpeed.Validated+=(s,e)=>ApplyPreviewRate();
        previewSpeed.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Enter){ApplyPreviewRate();timeline.Focus();e.SuppressKeyPress=true;}else if(e.KeyCode==Keys.Escape){previewSpeed.Text=previewRate.ToString("0.##",CultureInfo.CurrentCulture)+"×";timeline.Focus();e.SuppressKeyPress=true;}};
        Language.ToolTip(tips,previewSpeed,"预览专用：可输入 0.05～8 倍，回车应用。画面和音乐同步变速，音调随速度变化；不改变工程 FPS、关键帧或导出游戏。");bar.Controls.Add(previewSpeed);
        timeline.LostFocus+=(s,e)=>{if(timelineArrow!=Keys.None)StopPlaying();};Deactivate+=(s,e)=>{if(timelineArrow!=Keys.None)StopPlaying();};
    }
    private void ApplyPreviewRate()
    {
        double value;if(!PreviewTransport.TryRate(previewSpeed.Text,out value)){previewSpeed.Text=previewRate.ToString("0.##",CultureInfo.CurrentCulture)+"×";SetStatus("预览倍速请输入 0.05～8 之间的数字。");return;}
        if(playTimer.Enabled)TickPreview();previewRate=value;previewSpeed.Text=value.ToString("0.##",CultureInfo.CurrentCulture)+"×";
        if(playTimer.Enabled)SyncPreviewAudio();SetStatus("预览倍速 · "+value.ToString("0.##")+"×");
    }
    private void BeginTimelineArrow(Keys key)
    {
        if(state==null||busy||timelineArrow==key)return;StopPlaying();timelineArrow=key;
        int direction=key==Keys.Left?-1:1;SetFrame(Math.Max(0,Math.Min(state.duration,frame+direction)));
        previewTransport.Start(frame,direction,true);previewTickTime=0;playClock.Restart();playTimer.Start();
        SetStatus(direction<0?"向后逐帧 · 持续按住平滑加速倒放，松开停止":"向前逐帧 · 持续按住平滑加速播放，松开停止");
    }
    private void TickPreview()
    {
        if(state==null)return;
        try{double now=playClock.Elapsed.TotalSeconds;int next=previewTransport.Advance(now-previewTickTime,previewRate,state.fps,state.duration);previewTickTime=now;
            if(next!=frame){SetFrame(next);timeline.EnsureFrameVisible();}SyncPreviewAudio();if(!previewTransport.Running||templateEditing!=null&&next>=templateEditing.end)StopPreview(false);}
        catch(Exception error){StopPlaying();Error(error);}
    }
    private void SyncPreviewAudio()
    {
        double speed=previewTransport.Velocity(previewRate),seconds=previewTransport.Position/Math.Max(1,state.fps);
        if(templateEditing!=null){audio.Pause();return;}if(audio.Duration<=0)return;if(speed==0||seconds<0||seconds>=audio.Duration){audio.Pause();return;}
        if(!audio.Playing)audio.Play(seconds,speed);else{audio.SetRate(speed);if(Math.Abs(audio.Position-seconds)>.09)audio.Seek(seconds);audio.Update();}
    }
    private void TogglePlaying()
    {
        if(state==null||busy)return;if(playTimer.Enabled){StopPlaying();return;}
        try{if(templateEditing!=null&&frame>=templateEditing.end)SetFrame(templateEditing.start);else if(frame>=state.duration)SetFrame(0);previewTransport.Start(frame,1,false);previewTickTime=0;SyncPreviewAudio();playClock.Restart();playTimer.Start();playButton.Text="Ⅱ";SetStatus("时间轴与音乐播放中 · "+previewRate.ToString("0.##")+"× · 空格暂停 · F5 试玩");}
        catch(Exception error){StopPlaying();Error(error);}
    }
    private void StopPlaying(){StopPreview(true);}
    private void StopPreview(bool releaseArrow)
    {playTimer.Stop();playClock.Stop();previewTransport.Stop();audio.Pause();if(releaseArrow)timelineArrow=Keys.None;if(playButton!=null)playButton.Text="▶";}
    protected override void OnKeyUp(KeyEventArgs e)
    {if(e.KeyCode==timelineArrow){StopPlaying();e.Handled=true;e.SuppressKeyPress=true;}base.OnKeyUp(e);}
}
