using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

internal sealed partial class TimelineCanvas
{
    public Action<Track,int> EditSpawnRule;
    public Action<Track,int> EditBulletSpawnRule;
    public Action<Track> EditTemplateDefinition;
    private bool TemplateLane(Lane lane){return lane!=null&&lane.track!=null&&lane.property=="@template";}
    private void AppendTemplateKeys(Track target,int lane,int indent,string key)
    {foreach(string property in Motion.Fields(target).Where(p=>Motion.HasKeys(target,p)))lanes.Add(new Lane{index=lane,track=target,property=property,indent=indent+1,bindingKey=key+"/key:"+property});}
    private void AppendGenerationGroup(Track source,int lane,int indent,HashSet<string> path,string key)
    {
        var fields=Motion.Fields(source).Where(p=>Motion.HasKeys(source,p)).ToArray();if(fields.Length==0&&!TimelineLayout.HasGeneration(source))return;
        lanes.Add(new Lane{index=lane,track=source,property="@item",indent=indent,bindingKey=key});
        foreach(string property in fields)lanes.Add(new Lane{index=lane,track=source,property=property,indent=indent+1,bindingKey=key});
        AppendGenerationRows(source,lane,indent,path,key??source.id);
    }
    private void AppendGenerationRows(Track source,int lane,int indent,HashSet<string> path,string key)
    {
        if(!path.Add(source.id))return;
        foreach(bool bullet in new[]{false,true}){
            var actions=bullet?source.bulletActions:source.spawnActions;
            for(int i=0;i<actions.Count;i++){
                var action=actions[i];if(action.type!="spawn")continue;string property=(bullet?"@bullet:":"@spawn:")+i,binding=key+"/"+property;
                lanes.Add(new Lane{index=lane,track=source,property=property,indent=indent+1,generationSource=source,bindingKey=binding});
                var target=State.tracks.FirstOrDefault(t=>t.id==action.templateId);if(target==null)continue;
                lanes.Add(new Lane{index=lane,track=target,property="@template",indent=indent+2,generationSource=source,generationRule=property,bindingKey=binding+"/template"});
                if(!path.Contains(target.id)){
                    if(expanded.Contains(target.id))AppendTemplateKeys(target,lane,indent+2,binding);
                    if(IsExpanded(target))AppendGenerationRows(target,lane,indent+2,new HashSet<string>(path),binding);
                }
            }
        }
    }
    private Rectangle TemplateCardBounds(Lane lane,int y,int width)
    {
        int available=Math.Max(20,width-Names-16),size=Math.Min(560,available);int anchor=lane.track.start;
        if(lane.generationSource!=null){var source=lane.generationSource;var actions=lane.generationRule.StartsWith("@spawn:")?source.spawnActions:source.bulletActions;anchor=source.start+actions[Int32.Parse(lane.generationRule.Substring(lane.generationRule.IndexOf(':')+1))].start;}
        int left=Math.Max(Names+8,Math.Min((int)X(anchor),width-size-8));return new Rectangle(left,y+5,size,Math.Max(40,lane.height-10));
    }
    private string TemplateSummary(Track target)
    {
        int length=Math.Max(0,target.end-target.start),references=State.tracks.Sum(t=>TemplateRuntime.Actions(t).Count(a=>a.type=="spawn"&&a.templateId==target.id));
        return Language.Text(Theme.KindName(target.kind))+" · "+Language.Text(length==0?"单次发射":"局部动画")+" · "+target.start+"–"+target.end+"f · "+target.keyframes.Count+" "+Language.Text("个关键帧")+(references>1?" · "+Language.Text("共用模板"):"");
    }
    private void DrawTemplateCard(Graphics g,Lane lane,int y,int width)
    {
        var rect=TemplateCardBounds(lane,y,width);bool chosen=SelectedTrack==lane.track||SelectedTracks.Contains(lane.track);Color accent=Theme.TrackColor(lane.track.kind);
        using(var brush=new SolidBrush(chosen?Color.FromArgb(57,48,71):Theme.Raised))g.FillRectangle(brush,rect);
        using(var pen=new Pen(chosen?Theme.Accent:accent))g.DrawRectangle(pen,rect);
        string arrow=TimelineLayout.HasGeneration(lane.track)||lane.track.keyframes.Count>0?(IsExpanded(lane.track)?"▾ ":"▸ "):"▣ ";
        Theme.Label(g,arrow+Language.Text("模板")+" · "+lane.track.name,new Rectangle(rect.X+10,rect.Y+3,rect.Width-20,21),Theme.Text,Theme.Bold,localize:false);
        Theme.Label(g,TemplateSummary(lane.track),new Rectangle(rect.X+10,rect.Y+25,rect.Width-20,18),Theme.Muted,Theme.Small,localize:false);
    }
    private void SelectTemplateCard(Lane lane,bool edit)
    {
        PickTrack(lane.track,false);ActiveLane=lane.index;
        if(edit){expanded.Add(lane.track.id);RefreshLayout();if(EditTemplateDefinition!=null)EditTemplateDefinition(lane.track);else if(Seek!=null)Seek(lane.track.start);ActiveLane=lane.index;EnsureFrameVisible();}
    }
    private BulletAction SpawnAction(Lane lane){return lane.property.StartsWith("@spawn:")?lane.track.spawnActions[SpawnIndex(lane)]:lane.track.bulletActions[SpawnIndex(lane)];}
    internal static IEnumerable<int> SpawnFrames(Track t,BulletAction a,int left,int right)
    {
        if(!a.enabled||a.type!="spawn"||t.templateOnly)yield break;
        int count=a.spawnOrigin=="each"&&t.kind=="rod"?Math.Max(1,Math.Min(300,t.count)):1;
        for(int i=0;i<count;i++){
            if(i<a.spawnIndexFrom||a.spawnIndexTo>=0&&i>a.spawnIndexTo||a.spawnParity!="all"&&i%2!=(a.spawnParity=="even"?0:1))continue;
            int first=t.start+a.start+i*a.stagger,last=Math.Min(t.end,a.spawnRepeat>0&&a.duration>0?first+a.duration:t.end);
            if(a.spawnRepeat<=0){if(first>=left&&first<=right&&first<=last)yield return first;continue;}
            long begin=first+(long)Math.Max(0,(int)Math.Ceiling((left-first)/(double)a.spawnRepeat))*a.spawnRepeat;
            for(long at=begin;at<=Math.Min(right,last);at+=a.spawnRepeat)yield return (int)at;
        }
    }
    private bool SpawnLane(Lane lane){return lane!=null&&lane.track!=null&&lane.property!=null&&(lane.property.StartsWith("@spawn:")||lane.property.StartsWith("@bullet:"));}
    private int SpawnIndex(Lane lane){return Int32.Parse(lane.property.Substring(lane.property.IndexOf(':')+1));}
    private string SpawnTitle(Lane lane)
    {var a=SpawnAction(lane);var target=State.tracks.FirstOrDefault(t=>t.id==a.templateId);return "    ↗ "+(target==null?Language.Text("生成模板"):target.name);}
    private IEnumerable<int> LaneFrames(Lane lane,int left,int right)
    {
        if(lane.property.StartsWith("@spawn:"))return SpawnFrames(lane.track,SpawnAction(lane),left,right);
        var t=lane.track;var a=SpawnAction(lane);if(t.templateOnly||t.kind!="emitter")return new int[0];
        var result=new HashSet<int>();int interval=Math.Max(1,t.interval),life=Math.Max(1,t.life);
        int first=t.start+Math.Max(0,(int)Math.Ceiling((left-life-t.start)/(double)interval))*interval;
        for(int birth=first;birth<=Math.Min(t.end,right);birth+=interval){var proxy=new Track{kind="rod",start=birth,end=birth+life-1,count=1};var rule=a.Snapshot();rule.spawnOrigin="each";for(int i=0;i<Math.Min(300,Math.Max(1,t.count));i++){
            proxy.count=1;proxy.start=birth+i*t.indexDelay;proxy.end=proxy.start+life-1;rule.start=a.start+i*a.stagger;if(i<a.spawnIndexFrom||a.spawnIndexTo>=0&&i>a.spawnIndexTo||a.spawnParity!="all"&&i%2!=(a.spawnParity=="even"?0:1))continue;rule.spawnIndexFrom=0;rule.spawnIndexTo=-1;rule.spawnParity="all";rule.stagger=0;foreach(int frame in SpawnFrames(proxy,rule,left,right))result.Add(frame);
        }}return result;
    }
    private void DrawSpawnRule(Graphics g,Lane lane,int y,int width)
    {
        var t=lane.track;int left=Math.Max(0,(int)((horizontal.Value/PixelsPerSecond)*State.fps)),right=Math.Min(State.duration,(int)(((horizontal.Value+width-Names)/PixelsPerSecond)*State.fps)+1);
        int lastX=-1000000;foreach(int at in LaneFrames(lane,left,right).Distinct().OrderBy(f=>f)){
            int x=(int)X(at);if(x-lastX<3)continue;lastX=x;using(var pen=new Pen(Color.FromArgb(119,220,221),2))g.DrawLine(pen,x,y+6,x,y+lane.height-6);
            using(var brush=new SolidBrush(Color.FromArgb(119,220,221)))g.FillPolygon(brush,new[]{new Point(x,y+4),new Point(x+4,y+9),new Point(x-4,y+9)});
        }
        if(t.templateOnly)Theme.Label(g,"模板内生成：随实例的局部时钟执行",new Rectangle(Names+6,y,width-Names-10,lane.height),Theme.Muted,Theme.Small);
    }
    private int SpawnAt(Lane lane,int x)
    {return LaneFrames(lane,Math.Max(0,ToFrame(x)-20),Math.Min(State.duration,ToFrame(x)+20)).OrderBy(f=>Math.Abs(X(f)-x)).DefaultIfEmpty(ToFrame(x)).First();}
}
