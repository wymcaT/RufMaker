using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;

internal sealed class KeyframeCycle
{
    public int length;public string stamp="";public List<Keyframe> keys=new List<Keyframe>();
    public KeyframeCycle Snapshot(){return new KeyframeCycle{length=length,stamp=stamp,keys=keys.Select(k=>k.Snapshot()).ToList()};}
}
internal static class TimelineEditing
{
    public static readonly string[] Modes={"keep","sync","scale","repeat"},Names={"保持关键帧时间","同步关键帧（密集预设重复）","全部按比例缩放","重复样式填满"};
    private static string Stamp(IEnumerable<Keyframe> keys,int origin)
    {uint hash=2166136261;foreach(var k in keys.OrderBy(k=>k.frame).ThenBy(k=>k.property)){string s=(k.frame-origin)+"|"+k.property+"|"+k.value.ToString("R",CultureInfo.InvariantCulture)+"|"+k.easing+"|"+k.expression+"|"+k.randomId;foreach(char c in s){hash^=c;hash*=16777619;}}return hash.ToString("X8");}
    private static bool Dense(Track t){return (t.kind=="camera"||t.kind=="effect")&&t.keyframes.GroupBy(k=>k.property).Any(g=>g.Count()>=8);}
    private static Keyframe Sample(Track track,IEnumerable<Keyframe> source,string property,int at)
    {var keys=source.Where(k=>k.property==property).OrderBy(k=>k.frame).ToArray();var before=keys.LastOrDefault(k=>k.frame<=at)??keys[0];var after=keys.FirstOrDefault(k=>k.frame>=at)??keys[keys.Length-1];double u=after.frame==before.frame?0:Motion.Ease(String.IsNullOrEmpty(before.easing)?track.easing:before.easing,(at-before.frame)/(double)(after.frame-before.frame),track.easePower,track.easePeriod);var key=before.Snapshot();key.frame=at;key.value=before.value+(after.value-before.value)*u;key.expression="";return key;}
    private static KeyframeCycle Capture(Track track,int start,int end)
    {var cycle=new KeyframeCycle{length=Math.Max(1,end-start+1)};foreach(string property in track.keyframes.Select(k=>k.property).Distinct()){
        var keys=track.keyframes.Where(k=>k.property==property&&k.frame>=start&&k.frame<=end).Select(k=>k.Snapshot()).ToList();if(!keys.Any(k=>k.frame==start))keys.Add(Sample(track,track.keyframes,property,start));if(!keys.Any(k=>k.frame==end))keys.Add(Sample(track,track.keyframes,property,end));foreach(var key in keys){key.frame-=start;cycle.keys.Add(key);}
    }return cycle;}
    public static void Resize(Track t,int oldStart,int oldEnd,int start,int end,string mode)
    {
        t.start=start;t.end=Math.Max(start,end);bool bound=MotionPresets.Bound(t);
        if(bound)t.keyframes.RemoveAll(k=>MotionPresets.Generated(t,k.property));
        ResizeKeys(t,oldStart,oldEnd,start,end,mode);
        if(bound){t.motionPreset.start=t.start;t.motionPreset.end=t.end;MotionPresets.Bake(t);}
    }
    private static void ResizeKeys(Track t,int oldStart,int oldEnd,int start,int end,string mode)
    {
        if(mode=="keep"||t.keyframes.Count==0||oldStart==start&&oldEnd==end)return;
        bool repeat=mode=="repeat"||mode=="sync"&&(t.keyframeCycle!=null||Dense(t));
        if(repeat){
            string stamp=Stamp(t.keyframes,oldStart);if(t.keyframeCycle==null||t.keyframeCycle.stamp!=stamp)t.keyframeCycle=Capture(t,oldStart,oldEnd);
            var cycle=t.keyframeCycle;long count=((long)(t.end-t.start)/Math.Max(1,cycle.length)+1)*cycle.keys.Count;if(count>2010000)throw new ArgumentException("重复后的关键帧超过上限，请缩短物件或降低样式采样密度。");
            var output=new Dictionary<string,Keyframe>();for(int offset=0;offset<=t.end-t.start;offset+=Math.Max(1,cycle.length))foreach(var sample in cycle.keys){int at=t.start+offset+sample.frame;if(at>t.end)continue;var key=sample.Snapshot();key.frame=at;output[key.property+":"+at]=key;}
            int phase=(t.end-t.start)%Math.Max(1,cycle.length);foreach(string property in cycle.keys.Select(k=>k.property).Distinct())if(!output.ContainsKey(property+":"+t.end)){var key=Sample(t,cycle.keys,property,phase);key.frame=t.end;output[property+":"+t.end]=key;}
            t.keyframes=output.Values.OrderBy(k=>k.frame).ToList();cycle.stamp=Stamp(t.keyframes,t.start);
        }else{
            double ratio=(t.end-t.start)/(double)Math.Max(1,oldEnd-oldStart);foreach(var key in t.keyframes)key.frame=Math.Max(t.start,Math.Min(t.end,t.start+BulletSystem.Round((key.frame-oldStart)*ratio)));t.keyframes=t.keyframes.GroupBy(k=>k.property+":"+k.frame).Select(g=>g.Last()).OrderBy(k=>k.frame).ToList();t.keyframeCycle=null;
        }t.keyIndex=null;
    }
    public static void ScaleCycle(Track t,double ratio,bool includeRainbowSpeed=true)
    {if(t.keyframeCycle==null)return;t.keyframeCycle.length=Math.Max(1,BulletSystem.Round(t.keyframeCycle.length*ratio));foreach(var k in t.keyframeCycle.keys){k.frame=BulletSystem.Round(k.frame*ratio);if(k.property=="speed"||k.property=="rollSpeed"||includeRainbowSpeed&&k.property=="rainbowSpeed")k.value/=ratio;if(k.property=="life")k.value*=ratio;}t.keyframeCycle.stamp=Stamp(t.keyframes.Where(k=>!MotionPresets.Generated(t,k.property)),t.start);}
    private static string Class(Track t){return t.kind=="control"?(EventPresets.IsEvent(t)?"01事件":"02控制/"+t.controlAction):t.kind=="variable"?"00变量":t.kind=="camera"?"03拉镜":t.kind=="effect"?"04特效/"+t.effect:t.kind=="script"?"05脚本":t.kind=="group"?"06分组":t.kind=="emitter"?"07发射器/"+t.emissionShape+"/"+t.flightMode:t.kind=="rod"?"08整体/"+t.shape:"09对象";}
    public static void Organize(EditorState state)
    {
        state.preserveTrackOrder=true;var bound=TimelineLayout.BoundTemplates(state);var visible=state.tracks.Where(t=>t.kind!="native"&&!bound.Contains(t.id)).ToList();int lane=0;
        foreach(var category in visible.GroupBy(t=>Class(t)).OrderBy(g=>g.Key,StringComparer.Ordinal)){
            var ends=new List<int>();int first=lane;foreach(var t in category.OrderBy(t=>t.start).ThenBy(t=>t.end)){int at=ends.FindIndex(end=>end<t.start);if(at<0){at=ends.Count;ends.Add(t.end);}else ends[at]=t.end;t.lane=first+at;}lane+=ends.Count;
        }
        foreach(var t in state.tracks.Where(t=>bound.Contains(t.id))){var owners=TimelineLayout.TemplateDisplayLanes(state,t);if(owners.Length>0)t.lane=owners[0];}state.laneCount=Math.Max(1,lane);
    }
}
internal sealed partial class MainForm
{
    private string stretchKeys="sync";
    private ToolStripDropDownButton StretchTools()
    {
        stretchKeys=restoredView!=null&&TimelineEditing.Modes.Contains(restoredView.stretchKeys)?restoredView.stretchKeys:"sync";timeline.StretchKeys=stretchKeys;var menu=new ToolStripDropDownButton("拉伸关键帧"){ForeColor=Theme.Text};
        for(int i=0;i<TimelineEditing.Modes.Length;i++){string mode=TimelineEditing.Modes[i];var item=new ToolStripMenuItem(TimelineEditing.Names[i]){Checked=mode==stretchKeys};item.Click+=(s,e)=>{stretchKeys=mode;timeline.StretchKeys=mode;foreach(ToolStripMenuItem choice in menu.DropDownItems)choice.Checked=choice==item;};menu.DropDownItems.Add(item);}return menu;
    }
    private void OrganizeTimeline(){if(state==null)return;StopPlaying();CommitInspectorEdits();PushUndo();TimelineEditing.Organize(state);timeline.ActiveLane=selectedTrack==null?-1:selectedTrack.lane;MarkChanged();ShowProperties();SetStatus("已按种类和功能整理时间轴；时间、参数与执行顺序保持，Ctrl+Z可撤销。");}
}
