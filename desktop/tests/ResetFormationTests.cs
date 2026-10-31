using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Drawing;
internal static class ResetFormationTests
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static object Rows(BulletWorld w){return w.bullets.Where(b=>b.birth<=w.Frame&&!b.dead).Select(b=>new{source=b.source.id,b.birth,b.index,b.generation,b.x,b.y,b.direction,b.speed,scale=b.scale*BulletLifetime.Scale(b),alpha=b.alpha*BulletLifetime.Alpha(b),b.kinematic,b.clockShift,b.sourceX,b.sourceY,b.collision,b.deathStart,sprite=b.sprite??b.source.sprite,color=b.color.R+b.color.G*256+b.color.B*65536,depth=b.generation>0?-7000000-b.generation:-100,image=b.frozenImage>=0?b.frozenImage:Rainbow.Index(b.source,b.sequence,w.Frame-b.clockShift,(b.kinematic?w.Frame:b.birth)-b.clockShift),b.baseSpeed,b.baseDirection,b.motionReset,ownerBound=b.templateOwner!=0,imageAngle=((BulletWorld.BulletAngle(b,b.direction)%360)+360)%360}).ToArray();}
    static Track Rod(string id,int count=5){return new Track{id=id,name=id,kind="rod",shape="star2",count=count,x=310,y=240,radius=60,speed=2,start=0,end=12,sprite="sprCherry",life=8,lane=0};}
    static Track Reset(string id,int frame,string group=""){var e=new Track{id=id,start=frame,lane=9};EventPresets.ApplyPreset(e,group==""?"reset-motion":"group-set");e.eventRestoreBirth=false;e.controlValue=4;e.controlFrom=90;e.eventGroup=group;e.eventLife=80;return e;}
    static EditorState Scene()
    {
        var source=Rod("circle");source.rainbow=true;source.rainbowDynamic=true;source.rotateSprite=true;source.spriteAngle=17;source.keyframes.Add(new Keyframe{frame=0,property="x",value=300});source.keyframes.Add(new Keyframe{frame=12,property="x",value=420});
        source.spawnActions.Add(new BulletAction{type="spawn",generationPreset=CopyGeneration.Key,copyMode="circle",copyCount=3,copyRadius=90,copyFacing="inward",copyColorMode="rainbow",templateId=source.id,copyLinked=true,maxGeneration=4});
        var grid=Rod("grid",4);grid.x=1100;grid.y=300;grid.shape="ring";grid.radius=42;grid.spawnActions.Add(new BulletAction{type="spawn",generationPreset=CopyGeneration.Key,copyMode="grid",copySpacingX=200,copySpacingY=160,copyWidth=800,copyHeight=608,copyAngle=25,copyLinked=true,copyCenter=false,templateId=grid.id,maxGeneration=4});
        var group=new Track{id="bundle",kind="group",start=0,end=12,templateOnly=true,x=0,y=0,lane=2};var child=Rod("child",4);child.parentId=group.id;child.x=20;child.y=30;child.radius=25;
        child.spawnActions.Add(new BulletAction{type="spawn",generationPreset=CopyGeneration.Key,copyMode="circle",copyCount=2,copyRadius=60,copyLinked=true,templateId=child.id,maxGeneration=4});
        var picture=new Track{id="picture",kind="object",start=0,end=12,parentId=group.id,x=75,y=40,sprite="sprCherry",scale=.8};
        var generator=new Track{id="generator",kind="object",start=0,end=7,x=500,y=330};generator.spawnActions.Add(new BulletAction{type="spawn",templateId=group.id,start=2,maxGeneration=4});
        var sameFrame=new Track{id="same-frame",kind="object",start=0,end=12,x=500,y=100};var newborn=Rod("newborn",3);newborn.templateOnly=true;newborn.end=12;sameFrame.spawnActions.Add(new BulletAction{type="spawn",templateId=newborn.id,start=10,maxGeneration=4});
        var futureA=Rod("futureA",3);futureA.start=17;futureA.end=55;futureA.lane=4;var futureB=Rod("futureB",3);futureB.start=17;futureB.end=55;futureB.lane=5;
        var all=Reset("all",10);var scoped=Reset("scoped",20,"A");scoped.end=22;scoped.expressions["controlValue"]="random_range(2,4)+i/10";scoped.expressions["controlFrom"]="point_direction(x,y,400,304)";scoped.eventAcceleration=.1;scoped.eventTurn=1;
        var restore=Reset("restore",30);restore.eventRestoreBirth=true;restore.eventResetLife=false;
        var emitter=new Track{id="emitter",kind="emitter",start=0,end=40,x=100,y=100,count=1,interval=7,speed=1,life=80,advancedBullets=true};
        var s=new EditorState{fps=50,duration=110,randomSeed=451,tracks=new List<Track>{source,grid,group,child,picture,generator,sameFrame,newborn,futureA,futureB,emitter,all,scoped,restore},timelineGroups=new List<TimelineGroup>{new TimelineGroup{id="A",name="A",lanes=new List<int>{4}},new TimelineGroup{id="B",name="B",lanes=new List<int>{5}}}};Motion.Normalize(s);return s;
    }
    static void Inspector(string root,EditorState scene)
    {
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        using(var form=new MainForm(root)){
            typeof(MainForm).GetField("state",flags).SetValue(form,scene);typeof(MainForm).GetField("document",flags).SetValue(form,new LoadData{state=scene,room=new RoomData(),inventory=new Inventory{sprites=new[]{"sprCherry"},objects=new string[0],scripts=new string[0]}});
            var timeline=(TimelineCanvas)typeof(MainForm).GetField("timeline",flags).GetValue(form);timeline.State=scene;
            var select=typeof(MainForm).GetMethod("SelectTrack",flags);var undo=typeof(MainForm).GetMethod("Undo",flags);var e=scene.tracks.Single(t=>t.id=="all");e.eventRestoreBirth=true;
            Func<NativeProperties> bag=()=>{select.Invoke(form,new object[]{e,null});return (NativeProperties)((ParameterInspector)typeof(MainForm).GetField("properties",flags).GetValue(form)).SelectedObject;};
            var fields=bag();Check(fields.GetProperties()["重设速度"]!=null&&fields.GetProperties()["重设方向 / 度"]!=null,"Birth mode hides editable motion fields");fields.GetProperties()["重设速度"].SetValue(fields,9.0);Check(!e.eventRestoreBirth&&e.controlValue==9,"Numeric edit ignored by birth mode");undo.Invoke(form,new object[]{false});scene=(EditorState)typeof(MainForm).GetField("state",flags).GetValue(form);e=scene.tracks.Single(t=>t.id=="all");Check(e.eventRestoreBirth&&e.controlValue==4,"Undo did not restore motion mode and value together");
            fields=bag();((ValueProperty)fields.GetProperties()["重设方向 / 度"]).Actions.SetExpression("choose(30,60)");Check(!e.eventRestoreBirth&&e.expressions["controlFrom"]=="choose(30,60)","Expression edit ignored by birth mode");undo.Invoke(form,new object[]{false});scene=(EditorState)typeof(MainForm).GetField("state",flags).GetValue(form);e=scene.tracks.Single(t=>t.id=="all");Check(e.eventRestoreBirth&&!e.expressions.ContainsKey("controlFrom"),"Expression undo lost birth mode");
        }
    }
    [STAThread] public static int Main(string[] args)
    {
        try{
            if(args.Length>0&&args[0]=="--samples"){var input=Json.Read<EditorState>(File.ReadAllText(args[1]));Motion.Normalize(input);var w=new BulletWorld(input);var result=new Dictionary<string,object>();foreach(int f in args[2].Split(',').Select(Int32.Parse)){w.Seek(f);result[f.ToString()]=Rows(w);}File.WriteAllText(args[3],Json.Write(result));return 0;}
            var s=Scene();Directory.CreateDirectory(args[0]);File.WriteAllText(Path.Combine(args[0],"scene.json"),Json.Write(s));var world=new BulletWorld(s);world.Seek(10);
            var held=world.bullets.Where(b=>b.source.kind!="emitter"&&!b.dead).ToArray();Check(held.Length>100,"Fixture lacks grid/nested copies");Check(held.All(b=>!b.kinematic&&b.templateOwner==0&&b.speed==4&&b.direction==90&&b.birth==10&&b.life==80),"Some copied members did not detach/reset");
            world.Seek(15);Check(world.bullets.Count(b=>b.source.kind!="emitter")==held.Length,"Detached members lost at source/container end");
            world.Seek(20);Check(!world.Templates.Visible(world.State.tracks.Single(t=>t.id=="futureA"))&&world.Templates.Visible(world.State.tracks.Single(t=>t.id=="futureB")),"Scoped reset changed wrong formation");
            var saved=Json.Write(Rows(world));world.Seek(9);world.Seek(20);Check(Json.Write(Rows(world))==saved,"Rewind changed reset state");
            Check(!Reset("default",1).eventRestoreBirth,"New reset event should use custom fields by default");Inspector(Path.GetFullPath(Path.Combine(args[0],"../..")),s.Snapshot());
            Console.WriteLine("PASS: global/scoped resets detach grid, circular, nested group, child copies and same-frame templates; source expiry, formulas and rewind.");return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
