using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
// Native UI acceptance source. Compile-only on cloud; do not infer interaction pass.
internal static class TrackDisplayFilterUiTests
{
    private static int checks;private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
    private static object Call(object obj,string name,params object[] args){return obj.GetType().GetMethod(name,Private).Invoke(obj,args);}
    private static object Field(object obj,string name){return obj.GetType().GetField(name,Private|BindingFlags.Public).GetValue(obj);}
    private static Track T(string id,int lane,int start=10,int end=80){return new Track{id=id,name=id,kind="object",lane=lane,start=start,end=end};}
    [STAThread] public static void Main()
    {
        var a=T("a",0);var b=T("b",0,90,120);var c=T("c",1);var k1=new Keyframe{property="x",frame=20};var k2=new Keyframe{property="x",frame=30};a.keyframes.Add(k1);c.keyframes.Add(k2);
        var state=new EditorState{duration=300,laneCount=3,tracks=new List<Track>{a,b,c}};string before=Json.Write(state);
        using(var timeline=new TimelineCanvas{State=state,Size=new Size(1000,650),SelectionKeyframesOnly=true}){
            timeline.CreateControl();timeline.RefreshLayout();Track callback=null;timeline.SelectTrack=(t,k)=>{callback=t;timeline.RefreshLayout();};
            timeline.SetSelection(new[]{k1,k2},a,k1);timeline.HideTrackRows(new[]{a});Check(timeline.SelectedKeys.SetEquals(new[]{k2})&&callback==c,"hide synchronizes surviving key owner and inspector");Check(timeline.ClipBounds(a).IsEmpty&&!timeline.ClipBounds(b).IsEmpty,"shared lane hides exact object only");
            timeline.SelectVisibleTracks();Check(timeline.SelectedTracks.SetEquals(new[]{b,c})&&timeline.IsTrackFiltered(a),"Ctrl+A cannot unhide or select filtered object");
            Call(timeline,"BeginBox",new Point(195,timeline.AudioBottom+1),true,false);Call(timeline,"UpdateBox",new Point(980,600));Call(timeline,"FinishGesture");Check(!timeline.SelectedTracks.Contains(a),"box selection excludes filtered clip");
            Call(timeline,"OnMouseDown",new MouseEventArgs(MouseButtons.Right,1,60,timeline.AudioBottom+15,0));Check(timeline.SelectedTracks.SetEquals(new[]{b})&&timeline.IsTrackFiltered(a),"lane header context uses projected clips only");
            Track dropped=null;timeline.PresetDropped=(kind,name,dropTarget,at)=>dropped=dropTarget;var clip=timeline.ClipBounds(b);var dropPoint=timeline.PointToScreen(new Point((int)(clip.Left+clip.Width/2),(int)(clip.Top+clip.Height/2)));
            Call(timeline,"OnDragDrop",new DragEventArgs(new DataObject(DataFormats.Text,"presets|sample"),0,dropPoint.X,dropPoint.Y,DragDropEffects.Copy,DragDropEffects.Copy));Check(dropped==b&&timeline.IsTrackFiltered(a),"preset drop resolves visible clip not hidden same-lane representative");
            timeline.HideTrackRows(new[]{b});Check(timeline.ClipBounds(b).IsEmpty&&timeline.HiddenTrackCount==2,"whole filtered lane disappears with discoverable count");
            timeline.SetTrackSelection(new[]{a});Check(!timeline.IsTrackFiltered(a)&&timeline.IsTrackFiltered(b)&&!timeline.ClipBounds(a).IsEmpty,"programmatic stage selection reveals exact target");
            timeline.HideTrackRows(new[]{a,c});timeline.SetSelection(new[]{k1,k2},a,k1);Check(!timeline.IsTrackFiltered(a)&&!timeline.IsTrackFiltered(c)&&timeline.SelectedKeys.Count==2,"key batch reveals every owner");
            timeline.ShowAllTrackRows();Check(timeline.HiddenTrackCount==0,"show all");Check(Json.Write(state)==before,"view changes preserve full scene");
            var group=new TimelineGroup{id="group",lanes=new List<int>{0}};state.timelineGroups.Add(group);timeline.HideTrackRows(new[]{b});timeline.SetLaneGroupCollapsed(group,true);Check(timeline.ClipBounds(a).IsEmpty&&timeline.HiddenTrackCount==1,"group summary composes with object filter");timeline.RevealTrackRow(b);Check(!timeline.IsTrackFiltered(b)&&!timeline.IsLaneGroupCollapsed(group),"individual reveal opens group");
            var target=T("target",2);target.templateOnly=true;a.spawnActions.Add(new BulletAction{type="spawn",templateId=target.id});state.tracks.Add(target);timeline.SetTrackSelection(new[]{target});timeline.HideTrackRows(new[]{a});Check(timeline.ClipBounds(target).IsEmpty&&timeline.SelectedTrack==null,"hidden sole source clears its invisible selected template");timeline.SetTrackSelection(new[]{target});Check(!timeline.IsTrackFiltered(a)&&!timeline.ClipBounds(target).IsEmpty,"template selection reveals source path");
            timeline.SelectionKeyframesOnly=false;timeline.SetExpanded(a,false);timeline.RefreshLayout();timeline.SetTrackSelection(new[]{a});timeline.RevealLaneGroups(new[]{a});Check(!(bool)Call(timeline,"IsExpanded",a),"normal selected-source reveal preserves deliberate generation collapse");timeline.SelectionKeyframesOnly=true;
            timeline.HideTrackRows(new[]{target});Check(timeline.ClipBounds(target).IsEmpty&&!timeline.ClipBounds(a).IsEmpty,"template hidden under every visible source");timeline.RevealTrackRow(target);Check(!timeline.ClipBounds(target).IsEmpty,"template individual reveal");
            var logical=T("logical",3);logical.kind="group";logical.templateOnly=true;var child=T("child",4);child.parentId=logical.id;var childKey=new Keyframe{property="x",frame=25};child.keyframes.Add(childKey);a.spawnActions.Add(new BulletAction{type="spawn",templateId=logical.id});state.tracks.AddRange(new[]{logical,child});state.laneCount=5;timeline.RefreshLayout();
            var childGroup=new TimelineGroup{id="child-group",lanes=new List<int>{4}};state.timelineGroups.Add(childGroup);timeline.SetLaneGroupCollapsed(childGroup,true);timeline.SetTrackSelection(new[]{child});Check(timeline.SelectedTrack==child&&!timeline.IsLaneGroupCollapsed(childGroup)&&!timeline.ClipBounds(child).IsEmpty,"stage selection of folded bound logical child retains pending fallback through intermediate refresh");
            timeline.SetLaneGroupCollapsed(group,true);timeline.SetSelection(new[]{childKey,k2},child,childKey);Check(!timeline.IsLaneGroupCollapsed(group)&&!timeline.ClipBounds(child).IsEmpty&&timeline.SelectedKeys.Contains(childKey),"multi-owner keys reveal folded upstream bound-child fallback");timeline.SelectVisibleTracks();Check(timeline.SelectedTracks.Contains(child)&&!timeline.ClipBounds(child).IsEmpty,"Ctrl+A includes currently projected selected fallback without clearing it");timeline.SetTrackSelection(new[]{c});Check(timeline.ClipBounds(child).IsEmpty,"fallback history does not leave extra cards after deselection");
            timeline.ShowAllTrackRows();timeline.HideTrackRows(new[]{b});timeline.SetTrackSelection(new[]{c});Call(timeline,"PrepareBar",c,"bar");string protectedBefore=Json.Write(b);c.lane=b.lane;c.start=95;c.end=105;typeof(TimelineCanvas).GetField("dragChanged",Private).SetValue(timeline,true);Call(timeline,"FinishGesture");Check(state.tracks.Contains(b)&&Json.Write(b)==protectedBefore&&c.lane!=b.lane,"native drag collision retains hidden object identity and data");
            timeline.SetTrackSelection(new[]{c});Call(timeline,"PrepareBar",c,"bar");timeline.HideTrackRows(new[]{c});Check(Field(timeline,"dragMode")==null&&((IDictionary)Field(timeline,"bars")).Count==0,"filter ends captured gesture before projection changes");
            var replacement=state.Snapshot();timeline.State=replacement;timeline.RefreshLayout();Check(timeline.HiddenTrackCount==0&&timeline.SelectedTrack==null,"undo/document replacement resets filter and stale selections");timeline.HideTrackRows(new[]{replacement.tracks[0]});replacement.tracks[0]=replacement.tracks[0].Snapshot();timeline.RefreshLayout();Check(timeline.HiddenTrackCount==0,"same-state ID reuse visible");
        }
        Console.WriteLine("PASS "+checks+" native track-display filter checks");
    }
}
