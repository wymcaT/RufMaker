using System;
using System.Collections.Generic;
using System.Linq;
internal static class TrackDisplayFilterTests
{
    private static int checks;
    private static void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
    private static Track T(string id,int lane,int start,int end){return new Track{id=id,name=id,kind="object",lane=lane,start=start,end=end};}
    public static void Main()
    {
        var a=T("a",0,10,20);var b=T("b",0,30,40);var c=T("c",1,10,20);var native=T("n",2,0,100);native.kind="native";
        var state=new EditorState{laneCount=4,tracks=new List<Track>{a,b,c,native}};var filter=new TrackDisplayFilter();filter.Bind(state);string before=Json.Write(state.Snapshot());
        filter.Hide(new[]{a,native,new Track{id="a"}});Check(filter.Hidden.SequenceEqual(new[]{a}),"only exact live editable identity is hidden");
        Check(filter.LaneItems(0,new HashSet<string>()).SequenceEqual(new[]{b}),"same lane visible clip retained");Check(filter.LaneItems(1,new HashSet<string>()).SequenceEqual(new[]{c}),"unrelated lane retained");
        filter.Hide(new[]{b});Check(filter.LaneItems(0,new HashSet<string>()).Length==0,"all objects on lane filtered");filter.Reveal(new[]{a});Check(filter.LaneItems(0,new HashSet<string>()).SequenceEqual(new[]{a}),"per-object reveal retains other hide");
        Check(Json.Write(state.Snapshot())==before,"filter operations preserve complete scene snapshot");
        filter.Hide(new[]{a});var replacement=a.Snapshot();state.tracks[0]=replacement;filter.Prune();Check(!filter.Contains(replacement)&&!filter.Contains(a),"ID reuse cannot inherit hiding");filter.Bind(state.Snapshot());Check(filter.Count==0,"state replacement resets session");filter.Bind(state);filter.Hide(new[]{b});filter.Clear();Check(filter.Count==0,"show all clears filter");
        var source=T("source",2,0,100);var source2=T("source2",3,0,100);var parent=T("parent",4,0,20);parent.kind="group";parent.templateOnly=true;var child=T("child",5,0,20);child.parentId=parent.id;child.templateOnly=true;
        source.spawnActions.Add(new BulletAction{type="spawn",templateId=parent.id});source2.spawnActions.Add(new BulletAction{type="spawn",templateId=parent.id});state.tracks.AddRange(new[]{source,source2,parent,child});
        var owners=TrackDisplayFilter.DisplayOwners(state,new[]{child});Check(new HashSet<Track>(owners).SetEquals(new[]{source,source2,parent,child}),"all bound template display-owner paths and logical parents revealed");
        source.parentId=child.id;Check(TrackDisplayFilter.DisplayOwners(state,new[]{child}).Length==4,"cyclic display ownership terminates");source.parentId="";
        var duplicate=parent.Snapshot();state.tracks.Add(duplicate);Check(TrackDisplayFilter.DisplayOwners(state,new[]{child}).SequenceEqual(new[]{child}),"ambiguous parent not guessed");state.tracks.Remove(duplicate);
        var k1=new Keyframe{property="x",frame=12};var k2=new Keyframe{property="y",frame=15};replacement.keyframes.Add(k1);c.keyframes.Add(k2);Check(new HashSet<Track>(TrackDisplayFilter.KeyOwners(state,new[]{k1,k2})).SetEquals(new[]{replacement,c}),"multi-owner key batch reveals every track");c.keyframes.Add(k1);Check(TrackDisplayFilter.KeyOwners(state,new[]{k1}).Length==2,"aliased key owner safety checks every owner");
        filter.Hide(new[]{b});replacement.start=35;replacement.end=37;
        Check(TrackDisplayFilter.WouldCutHidden(state,new[]{replacement},filter.Hidden),"same visible lane hidden collision protected");string hiddenBefore=Json.Write(b);
        TimelineLayout.EnsureExclusive(state,filter.Hidden.Concat(new[]{replacement}));Check(Json.Write(b)==hiddenBefore&&replacement.lane!=b.lane,"overlap separation keeps hidden clip identical and moves visible clip");
        replacement.lane=0;replacement.start=30;replacement.end=40;Check(!TrackDisplayFilter.WouldCutHidden(state,new[]{replacement,b},new[]{b}),"explicit logical descendants in moved set remain intentional");
        parent.templateOnly=false;parent.lane=0;parent.start=30;parent.end=40;child.lane=5;Check(TrackDisplayFilter.WouldCutHidden(state,new[]{replacement},new[]{child}),"visible victim cannot cut hidden child on another lane");
        replacement.lane=8;Check(!TrackDisplayFilter.WouldCutHidden(state,new[]{replacement},new[]{b,child}),"nonoverlap preserves normal cut path");
        filter.Bind(null);Check(filter.Count==0&&filter.Hidden.Length==0,"null state clears references");
        Console.WriteLine("PASS "+checks+" pure track-display filter checks");
    }
}
