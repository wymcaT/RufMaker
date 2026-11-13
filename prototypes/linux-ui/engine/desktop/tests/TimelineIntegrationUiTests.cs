using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
// Joint navigation/folding tests. Requires a supported native display; compilation is not execution.
internal static class TimelineIntegrationUiTests
{
    private static int checks;
    private static readonly BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance;
    private static void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
    private static Track T(string id,int lane,bool template=false){return new Track{id=id,name=id,kind="object",lane=lane,start=10,end=80,templateOnly=template};}
    private static TimelineGroup G(string id,int lane){return new TimelineGroup{id=id,name=id,lanes=new List<int>{lane}};}
    [STAThread] public static void Main()
    {
        var source=T("source",0);var definition=T("definition",1,true);definition.kind="group";
        var child=T("child",2);child.parentId=definition.id;source.spawnActions.Add(new BulletAction{type="spawn",templateId=definition.id});
        var visible=T("visible",3);var akey=new Keyframe{property="x",frame=20};var bkey=new Keyframe{property="x",frame=30};child.keyframes.Add(akey);visible.keyframes.Add(bkey);
        var sg=G("source-group",0);var dg=G("definition-group",1);var cg=G("child-group",2);
        var state=new EditorState{duration=300,fps=50,laneCount=4,tracks=new List<Track>{source,definition,child,visible},timelineGroups=new List<TimelineGroup>{sg,dg,cg}};
        string scene=Json.Write(state);
        using(var timeline=new TimelineCanvas{State=state,Size=new Size(1000,650),SelectionKeyframesOnly=true,Frame=123}){
            timeline.CreateControl();timeline.RefreshLayout();Track boundPrimary=null;Keyframe boundKey=null;
            // Mimics MainForm selection binding's reentrant UpdateSelection -> RefreshLayout.
            timeline.SelectTrack=(track,key)=>{boundPrimary=track;boundKey=key;timeline.RefreshLayout();};
            timeline.SetLaneGroupCollapsed(sg,true);timeline.SetLaneGroupCollapsed(dg,true);timeline.SetLaneGroupCollapsed(cg,true);
            Check(timeline.RevealReference(child.id),"reference to hidden bound child succeeds");
            Check(!timeline.IsLaneGroupCollapsed(sg)&&!timeline.IsLaneGroupCollapsed(dg)&&!timeline.IsLaneGroupCollapsed(cg),"reference reveals own, parent and upstream groups");
            Check(boundPrimary==child&&timeline.SelectedTracks.SetEquals(new[]{child})&&!timeline.ClipBounds(child).IsEmpty,"exact child has editable projected card and synchronized selection");
            Check(timeline.Frame==123,"bound child navigation preserves song frame");
            timeline.SetLaneGroupCollapsed(cg,true);
            Check(timeline.IsLaneGroupCollapsed(cg)&&timeline.ClipBounds(child).IsEmpty&&boundPrimary==null&&timeline.SelectedTracks.Count==0,"fallback cannot resurrect directly collapsed child");
            timeline.RevealReference(child.id);timeline.SetLaneGroupCollapsed(sg,true);
            Check(timeline.IsLaneGroupCollapsed(sg),"upstream fold remains collapsed after callback");
            Check(timeline.SelectedTrack==null||!timeline.ClipBounds(timeline.SelectedTrack).IsEmpty,"explicit fallback selection never becomes invisible under upstream fold");
            timeline.RevealReference(child.id);timeline.SetSelection(new[]{akey,bkey},child,akey);timeline.SetLaneGroupCollapsed(cg,true);
            Check(boundPrimary==visible&&boundKey==bkey&&timeline.SelectedTracks.Contains(visible)&&timeline.SelectedKeys.SetEquals(new[]{bkey}),"fold primary owner keeps surviving key owner inspector binding");
            timeline.SelectVisibleTracks();Check(timeline.IsLaneGroupCollapsed(cg)&&!timeline.SelectedTracks.Contains(child),"Ctrl+A does not reopen fallback child");
            var rows=((IEnumerable)typeof(TimelineCanvas).GetField("lanes",Flags).GetValue(timeline)).Cast<object>();
            var summary=rows.First(r=>r.GetType().GetField("summaryGroup",Flags).GetValue(r)==cg);
            int top=(int)summary.GetType().GetField("top",Flags).GetValue(summary);
            typeof(TimelineCanvas).GetMethod("OnMouseDown",Flags).Invoke(timeline,new object[]{new MouseEventArgs(MouseButtons.Right,1,220,timeline.AudioBottom+top+15,0)});
            Check(timeline.SummaryGroup==cg&&timeline.ActiveLane==-1&&boundPrimary==null,"summary context survives reentrant binding without stale lane");
            var replacement=state.Snapshot();timeline.State=replacement;timeline.RefreshLayout();
            Check(timeline.SelectedTrack==null&&timeline.SelectedKeys.Count==0&&timeline.SummaryGroup==null,"state replacement drops old row and selection references");
            Check(timeline.RevealReference(child.id)&&timeline.SelectedTrack==replacement.tracks[2]&&timeline.SelectedTrack!=child,"replacement navigation resolves current child only");
            Check(!timeline.RevealReference("missing")&&timeline.SelectedTrack==replacement.tracks[2],"missing reference preserves valid current selection");
            Check(Json.Write(state)==scene&&Json.Write(replacement)==scene,"joint navigation folding leaves both scenes unchanged");
        }
        Console.WriteLine("PASS "+checks+" joint native-control checks");
    }
}
