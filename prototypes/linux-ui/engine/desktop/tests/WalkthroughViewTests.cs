using System;
using System.Linq;
internal static class WalkthroughViewTests
{
    private static void Check(bool value,string text){if(!value)throw new Exception(text);}
    public static void Main(){
        var source=new EditorState();source.tracks.Add(new Track{id="terrain",name="chosen terrain",kind="native-instance",nativeObject="objSolid",start=0,end=100});source.tracks.Add(new Track{id="group",kind="group"});source.tracks.Add(new Track{id="child",parentId="group",kind="object"});
        source.nativePaths["path"]=new GmPath();source.nativePaths["path"].points.Add(new GmPathPoint{x=1});
        var room=new RoomData{backgrounds=new[]{new RoomBackground{x=4}},views=new[]{new RoomView{x=5}}};room.instances.Add(new RoomInstance{id="native",x=10});string before=Json.Write(source),roomBefore=Json.Write(room);
        var film=new NativeFilmInfo{nativeIds=new[]{"native"},nativeTrackIds=new[]{"terrain"},views=new[]{new NativeViewPose()}};var ids=new System.Collections.Generic.HashSet<string>{"native"};var a=new WalkthroughSnapshot(source,room,film,ids);Check(a.Film.nativeIds[0]=="native"&&a.Film.views.Length==1&&a.FilmIds.Contains("native"),"film metadata preserved");film.nativeIds[0]="changed";film.nativeTrackIds[0]="changed";film.views[0]=null;ids.Clear();Check(a.Film.nativeIds[0]=="native"&&a.Film.nativeTrackIds[0]=="terrain"&&a.Film.views[0]!=null&&a.FilmIds.Contains("native"),"film metadata and ID set isolated");a.Hide(new[]{0,1,-1,99,0});Check(a.VisibleRows.SequenceEqual(new[]{2}),"exact selected rows only, no descendant inference");Check(a.Scene.tracks.Count==3&&a.Scene.tracks[0].enabled==source.tracks[0].enabled,"render scene retains hidden rows");
        a.ShowAll();Check(a.VisibleRows.SequenceEqual(new[]{0,1,2}),"show all restores order");a.Hide(new[]{0,1,2});Check(a.VisibleRows.Length==0,"empty filter allowed");Check(Json.Write(source)==before&&Json.Write(room)==roomBefore,"source unchanged");Check(Json.Write(a.Scene)==before,"filter never changes scene JSON");
        source.tracks[0].name="later source edit";source.tracks[0].keyframes.Add(new Keyframe{frame=10});room.instances[0].x=50;room.backgrounds[0].x=40;room.views[0].x=50;source.nativePaths["path"].points[0].x=100;Check(a.Room.backgrounds[0].x==4&&a.Room.views[0].x==5&&a.Scene.nativePaths["path"].points[0].x==1,"room art, views and native paths are isolated");Check(a.Scene.tracks[0].name=="chosen terrain"&&a.Scene.tracks[0].keyframes.Count==0&&a.Room.instances[0].x==10,"late callbacks cannot alter snapshot");
        var reopened=new WalkthroughSnapshot(source,room);Check(reopened.VisibleRows.Length==3&&reopened.Scene.tracks[0].name=="later source edit","reopen captures latest data and resets visibility");
        var switched=new EditorState();switched.tracks.Add(new Track{id="terrain",name="other project"});var b=new WalkthroughSnapshot(switched,new RoomData());Check(b.VisibleRows.Length==1&&b.Scene.tracks[0].name=="other project","project identity cannot leak via same IDs");Check(a.VisibleRows.Length==0,"old snapshot independent");
        a.Scene.tracks[0].name="accidental snapshot edit";Check(source.tracks[0].name=="later source edit","snapshot mutations cannot flow back");
        Console.WriteLine("PASS: exact filtering, descendants, all hidden, show-all, render retention, source serialization, late edits, reopen and project-switch snapshot isolation.");
    }
}
