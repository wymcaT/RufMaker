using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
// Requires a real supported WinForms display. Compilation alone is not a UI pass.
internal static class LaneGroupFoldingUiTests
{
    private static int checks;private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
    private static object Call(object instance,string name,params object[] args){return instance.GetType().GetMethod(name,Private).Invoke(instance,args);}
    private static object Field(object instance,string name){return instance.GetType().GetField(name,Private|BindingFlags.Public).GetValue(instance);}
    private static Track T(string id,int lane){return new Track{id=id,name=id,kind="object",lane=lane,start=10,end=80};}
    [STAThread] public static void Main()
    {
        var a=T("a",0);var b=T("b",2);var c=T("c",1);var second=T("second",0);second.start=90;second.end=110;
        var key=new Keyframe{property="x",frame=20,value=42};b.keyframes.Add(key);
        var group=new TimelineGroup{id="g",name="Sparse",lanes=new List<int>{0,2}};
        var state=new EditorState{duration=300,fps=50,laneCount=4,tracks=new List<Track>{a,b,c,second},timelineGroups=new List<TimelineGroup>{group}};
        string before=Json.Write(state);
        using(var timeline=new TimelineCanvas{State=state,Size=new Size(1000,650),SelectionKeyframesOnly=true}){
            timeline.CreateControl();timeline.SetTrackHeight(c,75);timeline.RefreshLayout();Track callback=null;Keyframe callbackKey=null;
            timeline.SelectTrack=(t,k)=>{callback=t;callbackKey=k;timeline.RefreshLayout();};timeline.SetTrackSelection(new[]{b});timeline.SetSelection(new[]{key},b,key);
            timeline.SetLaneGroupCollapsed(group,true);Check(callback==null&&callbackKey==null,"fold callback clears primary and key");Check(timeline.SelectedTracks.Count==0&&timeline.SelectedKeys.Count==0,"fold clears individual sets");Check(timeline.ClipBounds(a).IsEmpty&&timeline.ClipBounds(b).IsEmpty,"hidden clips have no hit bounds");Check(!timeline.ClipBounds(c).IsEmpty,"unrelated clip stays editable");
            var rows=((IEnumerable)Field(timeline,"lanes")).Cast<object>().ToArray();Check(rows.Length==3,"one summary plus two ordinary rows");var summary=rows[0];Check((int)Field(summary,"index")==-1&&Field(summary,"track")==null&&Field(summary,"clips")==null,"summary is not representative child");Check((int)Field(rows[1],"height")==75,"custom heights preserved");
            timeline.SelectVisibleTracks();Check(timeline.SelectedTracks.SetEquals(new[]{c}),"Ctrl+A visible objects only");Check(timeline.IsLaneGroupCollapsed(group),"Ctrl+A does not unfold");
            int y=timeline.AudioBottom+15;Call(timeline,"OnMouseDown",new MouseEventArgs(MouseButtons.Right,1,220,y,0));Check(timeline.SummaryGroup==group&&timeline.ActiveLane==-1&&timeline.SelectedTrack==null,"summary context clears stale lane/primary");
            var summaryHit=Call(timeline,"LaneAt",new Point(220,y));Check(Field(summaryHit,"summaryGroup")==group,"summary hit uses same projection");Check(Call(timeline,"KeyAt",summaryHit,new Point(220,y))==null&&Call(timeline,"EdgeAt",summaryHit,new Point(220,y))==null,"summary cannot key or resize");
            Call(timeline,"BeginBox",new Point(200,y),true,false);Call(timeline,"UpdateBox",new Point(980,600));Call(timeline,"FinishGesture");Check(timeline.SelectedTracks.SetEquals(new[]{c}),"box never selects summary members");
            var visibleKey=new Keyframe{property="x",frame=20,value=7};c.keyframes.Add(visibleKey);timeline.ShowAllLaneGroups();timeline.SetSelection(new[]{key,visibleKey},b,key);timeline.SetLaneGroupCollapsed(group,true);Check(timeline.SelectedKeys.SetEquals(new[]{visibleKey})&&timeline.SelectedTrack==c&&timeline.SelectedTracks.Contains(c),"fold preserves visible multi-owner key and rebinds primary");Check(callback==c&&callbackKey==visibleKey&&timeline.IsLaneGroupCollapsed(group),"reentrant callback preserves fold and visible owner");c.keyframes.Remove(visibleKey);timeline.ClearKeySelection();
            timeline.SelectLaneGroup(group);Check(!timeline.IsLaneGroupCollapsed(group)&&timeline.SelectedTracks.SetEquals(new[]{a,b,second}),"explicit group expands and selects all clips including repeated lane");
            timeline.SetLaneGroupCollapsed(group,true);timeline.SetTrackSelection(new[]{b});Check(!timeline.IsLaneGroupCollapsed(group)&&timeline.SelectedTrack==b,"programmatic selection reveals actual lane");
            timeline.SetLaneGroupCollapsed(group,true);timeline.SetSelection(new[]{key},b,key);Check(!timeline.IsLaneGroupCollapsed(group)&&timeline.SelectedKeys.Contains(key),"programmatic key target revealed");
            timeline.SetLaneGroupCollapsed(group,true);var view=timeline.CaptureView();timeline.ShowAllLaneGroups();timeline.RestoreView(view);Check(timeline.IsLaneGroupCollapsed(group),"view restore restores fold");
            var replacement=state.Snapshot();timeline.State=replacement;timeline.RefreshLayout();Check(timeline.SelectedTrack==null&&timeline.SelectedTracks.Count==0&&timeline.CaptureView().collapsedLaneGroups.Length==0,"state replacement clears references and fold scope");timeline.SetTrackSelection(new[]{replacement.tracks[1]});Check(timeline.SelectedTrack==replacement.tracks[1]&&timeline.SelectedTrack!=b,"undo target is fresh instance");
            var newGroup=replacement.timelineGroups[0];timeline.SetLaneGroupCollapsed(newGroup,true);replacement.timelineGroups[0]=newGroup.Snapshot();timeline.RefreshLayout();Check(!timeline.IsLaneGroupCollapsed(replacement.timelineGroups[0]),"recreated same ID not accidentally folded");
            timeline.State=state;timeline.RefreshLayout();timeline.SetTrackSelection(new[]{c});Call(timeline,"PrepareBar",c,"bar");timeline.SetLaneGroupCollapsed(group,true);Check(Field(timeline,"dragMode")==null&&((IDictionary)Field(timeline,"bars")).Count==0,"fold finishes captured gesture first");
            using(var image=new Bitmap(1000,650)){timeline.DrawToBitmap(image,timeline.ClientRectangle);image.Save("lane-fold-summary.png");}
            Check(Json.Write(state)==before,"fold, view and selection did not mutate source model");
            var template=T("template",3);template.templateOnly=true;a.spawnActions.Add(new BulletAction{type="spawn",templateId=template.id});state.tracks.Add(template);timeline.ShowAllLaneGroups();timeline.SetTrackSelection(new[]{template});timeline.RefreshLayout();Check(!timeline.ClipBounds(template).IsEmpty,"template visible through source before fold");timeline.SetLaneGroupCollapsed(group,true);Check(timeline.SelectedTrack==null&&timeline.ClipBounds(template).IsEmpty,"source fold clears externally-laned template selection");
            timeline.SetTrackSelection(new[]{template});Check(!timeline.IsLaneGroupCollapsed(group),"programmatic template reveals upstream source group");
            var templateGroup=new TimelineGroup{id="template-group",lanes=new List<int>{3}};state.timelineGroups.Add(templateGroup);timeline.SetLaneGroupCollapsed(templateGroup,true);Check(timeline.ClipBounds(template).IsEmpty,"folded member template suppressed under visible source");timeline.SelectVisibleTracks();Check(timeline.IsLaneGroupCollapsed(templateGroup)&&!timeline.SelectedTracks.Contains(template),"Ctrl+A cannot select folded shared card");
        }
        Console.WriteLine("PASS "+checks+" native-control folding checks");
    }
}
