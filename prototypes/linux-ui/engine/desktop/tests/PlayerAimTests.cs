using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

internal static class PlayerAimTests
{
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static void Near(double a,double b,string message){Check(Math.Abs(a-b)<.0001,message+": "+a+" / "+b);}
    private static void Heading(double a,double b,string message){Near(((a-b)%360+540)%360-180,0,message);}
    private static Track Shot(string id){return new Track{id=id,name=id,kind="emitter",x=200,y=180,start=0,end=0,count=1,life=120,speed=2,interval=10,advancedBullets=true,trailMode="off"};}
    private static EditorState Fixture()
    {
        var birth=Shot("birth-aim");birth.end=30;birth.count=3;birth.indexDelay=3;birth.emissionFacing="aim";birth.angle=7;
        var release=new Track{id="release-aim",name="release-aim",kind="rod",shape="ring",count=4,x=200,y=180,radius=40,start=0,end=100,releaseFrame=10,releaseDirection="aim",speed=2,trailMode="off"};
        var tracking=Shot("tracking");tracking.flightMode="aim";
        var turning=Shot("turning");turning.flightMode="turn";turning.turnRate=12;
        var action=Shot("age-aim");action.speed=0;action.bulletActions.Add(new BulletAction{type="aim",start=10,to=3});
        var controlled=Shot("control-shot");controlled.speed=0;
        var control=new Track{id="control-aim",name="control-aim",kind="control",start=12,end=12,targetSource=controlled.id,controlAction="aim",controlValue=6};
        var formula=Shot("formula-aim");formula.end=30;formula.expressions["angle"]="point_direction(x,y,player.x,player.y)";
        var template=new Track{id="template-release",name="template-release",kind="rod",shape="ring",count=3,x=100,y=100,radius=30,start=50,end=70,releaseFrame=55,releaseDirection="aim",speed=2,templateOnly=true,trailMode="off"};
        var host=new Track{id="template-host",name="template-host",kind="object",x=500,y=150,start=0,end=60,trailMode="off"};
        host.spawnActions.Add(new BulletAction{type="spawn",start=20,templateId=template.id,maxGeneration=2,spawnDirectionMode="fixed"});
        var templateShot=Shot("template-aim");templateShot.start=50;templateShot.end=60;templateShot.count=2;templateShot.indexDelay=2;templateShot.interval=5;templateShot.templateOnly=true;templateShot.emissionFacing="aim";templateShot.angle=-5;
        var shotHost=new Track{id="shot-host",name="shot-host",kind="object",x=600,y=150,start=0,end=60,trailMode="off"};
        shotHost.spawnActions.Add(new BulletAction{type="spawn",start=20,templateId=templateShot.id,maxGeneration=2,spawnDirectionMode="fixed"});
        var nestedShot=Shot("nested-shot");nestedShot.start=100;nestedShot.end=100;nestedShot.templateOnly=true;
        var nested=new Track{id="nested-host",name="nested-host",kind="object",x=100,y=100,start=50,end=70,templateOnly=true,trailMode="off"};
        nested.spawnActions.Add(new BulletAction{type="spawn",start=5,templateId=nestedShot.id,maxGeneration=3,spawnDirectionMode="fixed",expressions=new Dictionary<string,string>{{"spawnAngle","point_direction(0,0,player.x,player.y)"}}});
        host.spawnActions.Add(new BulletAction{type="spawn",start=20,templateId=nested.id,maxGeneration=2,spawnDirectionMode="fixed"});
        var state=new EditorState{duration=120,fps=50,nativeTest=new NativeTestPosition{x=400,y=304},usePlayerTrace=true,tracks=new List<Track>{birth,release,tracking,turning,action,controlled,control,formula,template,host,templateShot,shotHost,nested,nestedShot},playerTrace=new List<PlayerTracePoint>{new PlayerTracePoint{frame=0,x=600,y=100},new PlayerTracePoint{frame=10,x=100,y=500},new PlayerTracePoint{frame=20,x=700,y=300},new PlayerTracePoint{frame=40,x=120,y=160},new PlayerTracePoint{frame=60,x=620,y=520},new PlayerTracePoint{frame=120,x=300,y=180}}};
        Motion.Normalize(state);RandomParameters.Attach(state);return state;
    }
    private static object Rows(BulletWorld world){return world.bullets.Where(b=>!b.dead&&!b.kinematic&&b.birth<=world.Frame).Select(b=>new{source=b.source.id,birth=b.birth,index=b.index,generation=b.generation,x=b.x,y=b.y,direction=b.direction,speed=b.speed}).ToArray();}
    public static int Main(string[] args)
    {
        try{
            var state=Fixture();var world=new BulletWorld(state);var samples=new Dictionary<string,object>();
            for(int frame=0;frame<=60;frame++){
                var old=world.bullets.Where(b=>b.source.id=="tracking"||b.source.id=="turning").ToDictionary(b=>b.source.id,b=>new[]{b.x,b.y,b.direction});world.Seek(frame);var player=BulletSystem.PlayerAt(state,frame);
                foreach(var b in world.bullets.Where(b=>b.birth==frame&&!b.kinematic)){
                    if(b.source.id=="birth-aim"||b.source.id=="template-aim")Heading(b.direction,ExpressionContext.Direction(b.x,b.y,player.x,player.y)+b.source.angle,"Birth/individual delayed aim must sample current player");
                    if(b.source.id=="release-aim"||b.source.id=="template-release")Heading(b.direction,ExpressionContext.Direction(b.x,b.y,player.x,player.y),"Release aim must sample song time");
                    if(b.source.id=="formula-aim")Heading(b.direction,ExpressionContext.Direction(b.x,b.y,player.x,player.y),"Direction expression must sample current player");
                    if(b.source.id=="nested-shot")Heading(b.direction,ExpressionContext.Direction(0,0,player.x,player.y),"Nested spawn direction formula must not shift world time twice");
                }
                foreach(var row in old){var b=world.bullets.Single(x=>x.source.id==row.Key);double target=ExpressionContext.Direction(row.Value[0],row.Value[1],player.x,player.y);Heading(b.direction,row.Key=="tracking"?target:ExpressionContext.Turn(row.Value[2],target,12),"Continuous/limited tracking must update each frame");}
                if(frame==10)Heading(world.bullets.Single(b=>b.source.id=="age-aim").direction,ExpressionContext.Direction(200,180,player.x,player.y)+3,"Age action uses current player");
                if(frame==12)Heading(world.bullets.Single(b=>b.source.id=="control-shot").direction,ExpressionContext.Direction(200,180,player.x,player.y)+6,"Global control uses current player");
                samples[frame.ToString()]=Rows(world);
            }
            double fixedHeading=world.bullets.Single(b=>b.source.id=="age-aim").direction;var at10=BulletSystem.PlayerAt(state,10);Heading(fixedHeading,ExpressionContext.Direction(200,180,at10.x,at10.y)+3,"A single aim must not become homing");
            string before=Json.Write(Rows(world));world.Seek(5);world.Seek(60);Check(before==Json.Write(Rows(world)),"Rewinding changed player-dependent results");
            foreach(var id in new[]{"tracking","turning"}){var t=state.tracks.Single(x=>x.id==id);var path=BulletFlight.Path(t,0,60,t.x,t.y,t.angle,t.speed,0);var b=world.bullets.Single(x=>x.source.id==id);Near(path[60].x,b.x,"Simple/full flight X");Near(path[60].y,b.y,"Simple/full flight Y");Heading(path[60].angle,b.direction,"Simple/full flight direction");}
            var local=new Track{id="local",kind="object",owner=state,expressionWorldShift=20};var context=ExpressionContext.For(local,5);var at25=BulletSystem.PlayerAt(state,25);Near(context.playerX,at25.x,"Template expression player X must use world time");Near(context.playerY,at25.y,"Template expression player Y must use world time");Near(context.frame,5,"Template expression frame stays local");Near(ExpressionContext.For(local,25,true).playerX,at25.x,"World-time generation must not add the local offset again");
            var manual=state.Snapshot();manual.usePlayerTrace=false;manual.nativeTest.x=250;manual.nativeTest.y=450;Motion.Normalize(manual);var manualWorld=new BulletWorld(manual);manualWorld.Seek(10);var released=manualWorld.bullets.First(b=>b.source.id=="release-aim");Heading(released.direction,ExpressionContext.Direction(released.x,released.y,250,450),"Edited player position is the preview fallback");
            Directory.CreateDirectory(args[0]);File.WriteAllText(Path.Combine(args[0],"scene.json"),Json.Write(state));File.WriteAllText(Path.Combine(args[0],"expected.json"),Json.Write(samples));
            Console.WriteLine("PASS: moving player, per-bullet delayed birth, root/template release, expressions/local clocks, continuous/limited tracking, age/global aim, fixed single-aim direction and backward seek; 61 frames exported for GM comparison.");return 0;
        }catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
