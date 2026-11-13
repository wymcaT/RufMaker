using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class LaneExclusivityTests
{
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static object Get(object target,string name){return target.GetType().GetField(name,Flags).GetValue(target);}
    static void Set(object target,string name,object value){target.GetType().GetField(name,Flags).SetValue(target,value);}
    static object Call(object target,string name,params object[] args){return target.GetType().GetMethods(Flags).Single(m=>m.Name==name&&m.GetParameters().Length==args.Length).Invoke(target,args);}
    static Track Clip(string id,int start,int end,int lane=0,string kind="object"){return new Track{id=id,name=id,kind=kind,start=start,end=end,lane=lane,sprite="sprCherry"};}
    static EditorState Scene(params Track[] tracks){var s=new EditorState{fps=50,duration=300,randomSeed=765,tracks=tracks.ToList()};Motion.Normalize(s);return s;}
    static void Unique(EditorState state){var bound=TimelineLayout.BoundTemplates(state);var clips=state.tracks.Where(t=>t.kind!="native"&&!bound.Contains(t.id)).ToArray();foreach(var a in clips)foreach(var b in clips)if(a!=b&&a.lane==b.lane)Check(a.end<b.start||b.end<a.start,"Clips overlap: "+a.id+" / "+b.id);}
    static string Sample(EditorState state,int frame){var world=new BulletWorld(state);world.Seek(frame);return Json.Write(world.bullets.Where(b=>!b.dead&&b.birth<=frame).Select(b=>new{source=b.source.id,b.birth,b.index,b.x,b.y,b.speed,b.direction}).ToArray());}
    static void Migration(string root)
    {
        var a=Clip("first",0,99,0,"emitter");a.interval=10;a.speed=3;a.randomPerInstance=true;a.expressions["angle"]="random_range(0,360)";
        var b=Clip("second",50,120,0,"emitter");b.interval=11;b.speed=2;
        var c=Clip("adjacent",100,180);var parent=Clip("parent",0,90,2,"group");var child=Clip("child",0,90,2);child.parentId=parent.id;
        var template=Clip("template",0,99);template.templateOnly=true;a.spawnActions.Add(new BulletAction{type="spawn",templateId=template.id});
        var state=Scene(a,b,c,parent,child,template);var order=state.tracks.Select(t=>t.id).ToArray();string before=Sample(state,70);int count=TimelineLayout.EnsureExclusive(state);
        Check(count>=2,"Existing overlapping clips not separated");Unique(state);Check(c.lane==0,"Adjacent inclusive clips should share a lane");Check(child.parentId==parent.id,"Parent relationship changed");Check(template.lane==0,"Bound template was treated as a song clip");Check(state.preserveTrackOrder&&state.tracks.Select(t=>t.id).SequenceEqual(order),"Original execution order changed");Motion.Normalize(state);Check(Sample(state,70)==before,"Migration changed seeded motion or execution");
        Check(TimelineLayout.EnsureExclusive(state)==0,"Repeated repair moved clips again");File.WriteAllText(Path.Combine(root,"migrated.json"),Json.Write(state));
        var lone=Clip("standalone-template",0,99);lone.templateOnly=true;var ordinary=Clip("ordinary",0,99);var standalone=Scene(lone,ordinary);Check(TimelineLayout.EnsureExclusive(standalone)==1&&lone.lane!=ordinary.lane,"Standalone template shared an occupied lane");
        var many=Enumerable.Range(0,200).Select(i=>Clip("bulk"+i,0,100)).ToArray();var full=Scene(many);Check(TimelineLayout.EnsureExclusive(full)==199,"200 simultaneous objects did not fit");Unique(full);Check(full.tracks.Max(t=>t.lane)==199,"Lane allocation exceeded supported range");
    }
    static void Editing(string root)
    {
        var a=Clip("a",0,49);var b=Clip("b",50,99);var state=Scene(a,b);
        using(var form=new MainForm(root)){
            Set(form,"state",state);Set(form,"document",new LoadData{state=state,room=new RoomData(),project=root,projectName="lane test",inventory=new Inventory{objects=new string[0],sprites=new[]{"sprCherry","sprBullet"},scripts=new string[0]},waveform=new WaveData()});
            var timeline=(TimelineCanvas)Get(form,"timeline");timeline.State=state;Call(form,"SelectTrack",a,null);Set(form,"frame",0);
            Call(form,"AddTrack","object",null);var created=(Track)Get(form,"selectedTrack");Check(created.lane==0&&a.lane!=0,"New clip should keep target lane without erasing its occupant");Unique(state);Check(state.tracks.Contains(a)&&a.start==0&&a.end==49,"Adding removed or trimmed original");
            Call(form,"Undo",false);state=(EditorState)Get(form,"state");a=state.tracks.Single(t=>t.id=="a");b=state.tracks.Single(t=>t.id=="b");Check(state.tracks.Count==2&&a.lane==0,"Undo did not restore pre-add state");
            Call(form,"SelectTrack",a,null);Call(form,"CopySelection");((System.Threading.Tasks.Task)Call(form,"PasteSelection")).GetAwaiter().GetResult();state=(EditorState)Get(form,"state");Unique(state);Check(state.tracks.Count==3&&state.tracks.Any(t=>t.id=="a"),"Paste lost original");
            Call(form,"Undo",false);state=(EditorState)Get(form,"state");a=state.tracks.Single(t=>t.id=="a");b=state.tracks.Single(t=>t.id=="b");Call(form,"SelectTrack",a,null);((SelectionTimeControl)Get(form,"rightTime")).EditRange(0,75);Unique(state);Check(a.end==75&&b.start==50&&b.end==99,"Numeric trim changed another clip's content");
            Call(form,"Undo",false);state=(EditorState)Get(form,"state");Unique(state);Check(state.tracks.Single(t=>t.id=="a").end==49&&state.tracks.Single(t=>t.id=="b").lane==0,"Undo did not restore lane and bounds");
            a=state.tracks.Single(t=>t.id=="a");b=state.tracks.Single(t=>t.id=="b");var c=Clip("overlap",0,75,1);state.tracks.Add(c);Call(form,"SelectTrack",c,null);timeline.SetTrackSelection(new[]{a,c},c);Call(form,"MoveSelectionToLane",0);Unique(state);Check(c.lane==0&&state.tracks.Count==3&&c.start==0&&c.end==75,"Multi-selection lane move lost content or primary lane");Call(form,"Undo",false);state=(EditorState)Get(form,"state");Check(state.tracks.Single(t=>t.id=="overlap").lane==1,"Undo did not restore multi-selection lanes");
        }
    }
    [STAThread]static int Main(string[] args){try{if(args[0]=="--migrate"){var state=Json.Read<EditorState>(File.ReadAllText(args[1]));Motion.Normalize(state);TimelineLayout.EnsureExclusive(state);File.WriteAllText(args[2],Json.Write(state));return 0;}Application.EnableVisualStyles();Migration(args[0]);Editing(args[0]);Console.WriteLine("PASS: inclusive exclusivity, old-project migration, generation templates, groups, deterministic execution, 200 clips, add/paste/range edits and undo.");return 0;}catch(Exception error){Console.Error.WriteLine(error);return 1;}}
}
