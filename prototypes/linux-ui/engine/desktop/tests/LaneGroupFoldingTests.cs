using System;
using System.Linq;
using System.Collections.Generic;
using System.Web.Script.Serialization;
internal static class LaneGroupFoldingTests
{
    private static int checks;
    private static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
    public static void Main()
    {
        var state=new EditorState{laneCount=6};var group=new TimelineGroup{id="g",name="Sparse",lanes=new List<int>{0,2,5}};var other=new TimelineGroup{id="h",lanes=new List<int>{3,4}};state.timelineGroups.Add(group);state.timelineGroups.Add(other);
        var a=new Track{id="a",lane=0,start=1,end=5,enabled=false};var b=new Track{id="b",lane=2,start=9,end=15};var c=new Track{id="c",lane=1,start=3,end=8};var d=new Track{id="d",lane=0,start=20,end=30};state.tracks.AddRange(new[]{a,b,c,d});
        var collapsed=new HashSet<string>{"g"};string before=new JavaScriptSerializer().Serialize(state.Snapshot());
        var rows=LaneGroupProjection.Build(state,collapsed);Check(rows.Count==4,"sparse three lanes replaced once");Check(rows[0].group==group&&rows[0].index==-1,"summary has no hidden lane");Check(rows[1].index==1&&rows[2].index==3&&rows[3].index==4,"noncontiguous order");Check(LaneGroupProjection.Members(state,group).Count()==3,"multiple clips and inactive clip counted");
        collapsed.Add("h");rows=LaneGroupProjection.Build(state,collapsed);Check(rows.Count==3&&rows[2].group==other,"multiple folded groups including empty lanes");
        collapsed.Clear();Check(LaneGroupProjection.Build(state,collapsed).Select(r=>r.index).SequenceEqual(Enumerable.Range(0,6)),"expand restores exact lane numbers");
        collapsed.Add("missing");Check(LaneGroupProjection.Build(state,collapsed).Count==6,"stale missing fold ignored");
        var duplicate=new TimelineGroup{id="g",lanes=new List<int>{1}};state.timelineGroups.Add(duplicate);collapsed.Add("g");Check(!LaneGroupProjection.CanFold(state,group)&&LaneGroupProjection.Build(state,collapsed).Count==6,"ambiguous IDs cannot share a fold");state.timelineGroups.Remove(duplicate);
        var missing=new TimelineGroup{id="",lanes=new List<int>{1}};state.timelineGroups.Add(missing);collapsed.Add("");Check(!LaneGroupProjection.CanFold(state,missing),"empty ID not folded or normalized");state.timelineGroups.Remove(missing);
        var empty=new TimelineGroup{id="empty",lanes=new List<int>()};state.timelineGroups.Add(empty);collapsed.Add("empty");Check(!LaneGroupProjection.Build(state,collapsed).Any(r=>r.group==empty),"empty group does not invent row");state.timelineGroups.Remove(empty);
        Check(new JavaScriptSerializer().Serialize(state.Snapshot())==before,"fold projection leaves complete snapshot identical");
        var source=new Track{id="source",lane=4};var parent=new Track{id="parent",kind="group",lane=2,templateOnly=true};var child=new Track{id="child",parentId="parent",lane=5,templateOnly=true};source.spawnActions.Add(new BulletAction{type="spawn",templateId="parent"});state.tracks.AddRange(new[]{source,parent,child});
        var reveal=LaneGroupProjection.RevealLanes(state,new[]{child});Check(reveal.SetEquals(new[]{5,2,4}),"reveal child membership, logical parent and generation owner");
        source.parentId=child.id;Check(LaneGroupProjection.RevealLanes(state,new[]{child}).Count==3,"cyclic references terminate");
        Check(LaneGroupProjection.UniqueTrack(state,"a")==a,"unique source resolved");state.tracks.Add(new Track{id="a"});Check(LaneGroupProjection.UniqueTrack(state,"a")==null,"duplicate source rejected");Check(LaneGroupProjection.UniqueTrack(state,"")==null&&LaneGroupProjection.UniqueTrack(state,"absent")==null,"missing source rejected");
        var view=new TimelineView{collapsedLaneGroups=new[]{"g","h"}};var json=new JavaScriptSerializer().Serialize(view);Check(new JavaScriptSerializer().Deserialize<TimelineView>(json).collapsedLaneGroups.SequenceEqual(view.collapsedLaneGroups),"view-only persistence");
        var dragState=new EditorState{laneCount=5};dragState.timelineGroups.Add(new TimelineGroup{id="hidden",lanes=new List<int>{3}});var folds=new HashSet<string>{"hidden"};
        Check(!LaneGroupProjection.CanMoveIndividualLanes(dragState,folds,new[]{0,1},2),"secondary drag target cannot land in hidden lane");
        Check(LaneGroupProjection.CanMoveIndividualLanes(dragState,folds,new[]{0},2),"ordinary visible destination accepted");
        Check(!LaneGroupProjection.CanMoveIndividualLanes(dragState,folds,new[]{0},3),"primary hidden destination rejected");
        Console.WriteLine("PASS "+checks+" pure lane-group folding checks");
    }
}
