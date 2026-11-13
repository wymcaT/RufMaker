using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

// Actual C# model trajectories, with a separate generated-GML numeric oracle.
// Neither this program nor the oracle executes GameMaker or Windows UI.
internal static class HomingSamplingTests
{
    static int checks;
    static readonly List<object> oracle=new List<object>();
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static void Near(double actual,double expected,string message){Check(Math.Abs(actual-expected)<1e-9,message+": "+actual+" / "+expected);}
    static Track E(string id="emitter"){return new Track{id=id,kind="emitter",start=0,end=0,x=400,y=400,angle=0,speed=2,count=1,interval=100,life=800,flightMode="turn",turnRate=3,homingDelay=0,advancedBullets=true};}
    static EditorState Scene(params Track[] tracks){var s=new EditorState{duration=800,randomSeed=34567,nativeTest=new NativeTestPosition{x=400,y=304},tracks=tracks.ToList()};Motion.Normalize(s);return s;}
    static SimBullet Only(BulletWorld w){return w.bullets.Single();}
    static void Regressions()
    {
        var t=E();t.expressions["turnRate"]="90+i";var w=new BulletWorld(Scene(t));w.Seek(1);Near(Only(w).direction,90,"dynamic turn reaches target");Near(Only(w).x,400,"dynamic turn x");Near(Only(w).y,398,"dynamic turn y");
        t=E();t.expressions["homingDelay"]="10+i";w=new BulletWorld(Scene(t));w.Seek(1);Near(Only(w).direction,0,"dynamic delay waits");Near(Only(w).x,402,"delayed x");Near(Only(w).y,400,"delayed y");w.Seek(10);Near(Only(w).direction,0,"delay includes its boundary frame");w.Seek(11);Near(Only(w).direction,3,"homing starts strictly after delay");
        foreach(string mode in new[]{"aim","expression"}){t=E();t.flightMode=mode;t.flightExpression="90+frame*0";t.expressions["homingDelay"]="2+i";w=new BulletWorld(Scene(t));w.Seek(2);Near(Only(w).direction,0,mode+" waits at delay boundary");w.Seek(3);Check(Only(w).direction>80,mode+" starts after sampled delay");}
        t=E();t.expressions["turnRate"]="30+i";t.bulletActions.Add(new BulletAction{type="turn",start=0,from=7,to=7});w=new BulletWorld(Scene(t));w.Seek(1);Near(Only(w).homingTurn,30,"homing sample separate from spin");Near(Only(w).turn,7,"angular-spin behavior unchanged");Near(Only(w).direction,37,"homing then angular spin");
    }
    static void LimitsAndCompatibility()
    {
        var t=E();t.expressions["turnRate"]="-9+i";var w=new BulletWorld(Scene(t));w.Seek(3);Near(Only(w).homingTurn,0,"negative dynamic turn clamps to zero");Near(Only(w).direction,0,"negative dynamic turn does not steer");
        t=E();t.expressions["homingDelay"]="1.5+i";w=new BulletWorld(Scene(t));w.Seek(2);Near(Only(w).homingDelay,2,"fractional delay rounds half up");Near(Only(w).direction,0,"rounded delay boundary waits");w.Seek(3);Near(Only(w).direction,3,"rounded delay starts next frame");
        t=E();t.expressions["homingDelay"]="-1+i";w=new BulletWorld(Scene(t));w.Seek(0);Near(Only(w).homingDelay,0,"negative delay clamps to zero");Near(Only(w).direction,0,"negative delay does not home on birth");w.Seek(1);Near(Only(w).direction,3,"clamped delay homes at age one");
        foreach(bool expressions in new[]{false,true}){
            t=E();t.turnRate=7.5;t.homingDelay=2;if(expressions){t.expressions["turnRate"]="7.5+frame*0";t.expressions["homingDelay"]="1.5+frame*0";}
            var s=Scene(t);var expected=BulletFlight.Path(t,0,15,t.x,t.y,t.angle,t.speed,0);w=new BulletWorld(s);
            for(int frame=0;frame<=15;frame++){w.Seek(frame);var b=Only(w);Near(b.x,expected[frame].x,"ordinary path x "+expressions+"/"+frame);Near(b.y,expected[frame].y,"ordinary path y "+expressions+"/"+frame);Near(b.direction,expected[frame].angle,"ordinary path direction "+expressions+"/"+frame);}
        }
        t=E();t.expressions["turnRate"]="-9+frame*0";t.expressions["homingDelay"]="-2+frame*0";Scene(t);var path=BulletFlight.Path(t,0,2,400,400,0,2,0);Near(path[2].angle,0,"ordinary path uses nonnegative turn clamp");
    }
    static void RetentionAndReset()
    {
        var t=E();t.start=t.end=3;t.expressions["turnRate"]="5+frame+i";t.expressions["homingDelay"]="i";var s=Scene(t);var snapshot=s.Snapshot();t.expressions["turnRate"]="200+i";Check(snapshot.tracks[0].expressions["turnRate"]=="5+frame+i","scene snapshot isolates expression dictionary");var w=new BulletWorld(snapshot);w.Seek(4);var b=Only(w);Near(b.homingTurn,8,"birth clock sampled once");Near(b.direction,8,"first turn uses birth sample");var clone=b.Clone();Near(clone.homingTurn,8,"bullet clone retains homing turn");Near(clone.homingDelay,0,"bullet clone retains delay");b.source.turnRate=70;b.source.homingDelay=100;b.source.expressions["turnRate"]="1000+i";w.Seek(5);Near(b.direction,16,"live track mutation does not replace birth samples");
        t=E();t.expressions["turnRate"]="7+frame+i";t.expressions["homingDelay"]="2+i";var reset=new Track{id="reset",kind="control",start=4,end=4,eventPreset="reset-motion",eventRestoreBirth=true,eventDisableMotion=false,eventResetLife=true,eventLife=50};w=new BulletWorld(Scene(t,reset));w.Seek(4);b=Only(w);Check(b.birth==4,"life reset changes age origin");Near(b.homingTurn,7,"life reset retains original turn sample");Near(b.homingDelay,2,"life reset retains original delay sample");w.Seek(6);Near(b.direction,0,"reset age waits through same delay");w.Seek(7);Near(b.direction,7,"reset age resumes original sample");
        reset.eventDisableMotion=true;w=new BulletWorld(Scene(t,reset));w.Seek(8);Check(Only(w).motionReset,"disable-motion reset retained");Near(Only(w).direction,0,"disable-motion suppresses homing");
        t=E();t.expressions["turnRate"]="9+i";t.lane=0;var copy=new Track{id="copy",kind="control",start=2,end=2,lane=1,eventPreset="group-copy",eventGroup="g",eventCopyLife=20,controlValue=2,controlFrom=0};s=Scene(t,copy);s.timelineGroups.Add(new TimelineGroup{id="g",lanes=new List<int>{0,1}});Motion.Normalize(s);w=new BulletWorld(s);w.Seek(4);var copied=w.bullets.Single(x=>x.generation==1);Check(copied.behavior==null&&copied.motionReset,"group copy has no later homing behavior");Near(copied.direction,0,"group copy keeps specified direction");Near(w.bullets.Single(x=>x.generation==0).homingTurn,9,"group copy preserves original sample");
        var host=new Track{id="host",kind="object",start=0,end=0,x=400,y=400};host.spawnActions.Add(new BulletAction{type="spawn",templateId="fixed",start=0});var rod=new Track{id="fixed",kind="rod",templateOnly=true,start=0,end=20,count=1,x=400,y=400,length=0,speed=2,angle=0,flightMode="turn",turnRate=60,homingDelay=20,releaseDirection="fixed"};rod.expressions["turnRate"]="90+i";rod.expressions["homingDelay"]="20+i";reset=new Track{id="detach",kind="control",start=1,end=1,eventPreset="reset-motion",eventRestoreBirth=true,eventDisableMotion=false,eventResetLife=true,eventLife=20};w=new BulletWorld(Scene(host,rod,reset));w.Seek(2);b=Only(w);Check(!b.kinematic,"fixed template member detached");Near(b.homingTurn,3,"fixed member keeps GM default homing turn");Near(b.homingDelay,0,"fixed member keeps GM default homing delay");Near(b.direction,3,"detached fixed member follows default turn");
    }
    static void Checkpoints()
    {
        var t=E();t.end=4;t.interval=2;t.count=3;t.speed=0;t.expressions["turnRate"]=".5+i*.25+random_range(0,.5)";t.expressions["homingDelay"]="2+volley+i";var s=Scene(t);var w=new BulletWorld(s);w.Seek(450);Check(w.CheckpointCount>=3,"checkpoints captured");long before=w.SimulatedSteps;w.Seek(270);Check(w.SimulatedSteps-before<100,"reverse seek uses retained checkpoint");var fresh=new BulletWorld(s);fresh.Seek(270);Check(w.bullets.Count==fresh.bullets.Count,"checkpoint bullet count");for(int i=0;i<w.bullets.Count;i++){var a=w.bullets[i];var b=fresh.bullets[i];Near(a.homingTurn,b.homingTurn,"checkpoint turn sample");Near(a.homingDelay,b.homingDelay,"checkpoint delay sample");Near(a.direction,b.direction,"checkpoint trajectory");}
    }
    static void Capture(BulletWorld w,int end)
    {
        var trajectories=new Dictionary<SimBullet,List<object>>();
        for(int frame=0;frame<=end;frame++){w.Seek(frame);foreach(var b in w.bullets.Where(x=>x.birth<=frame)){List<object> points;if(!trajectories.TryGetValue(b,out points)){points=new List<object>();trajectories[b]=points;}points.Add(new{frame,b.x,b.y,b.direction});}}
        foreach(var pair in trajectories){var b=pair.Key;var owner=b.clockShift==0?null:w.Templates.ObservedInstances.Single(x=>x.shift==b.clockShift);oracle.Add(new{track=b.source.Snapshot(),seed=w.State.randomSeed,b.birth,b.index,b.count,b.volley,b.clockShift,sourceX=owner==null?b.source.x:owner.sourceX,sourceY=owner==null?b.source.y:owner.sourceY,ownerBirth=owner==null?0:owner.birth,ownerSerial=owner==null?0:owner.serial,turn=b.homingTurn,delay=b.homingDelay,trajectory=pair.Value});Check(b.homingTurn>=0&&b.homingDelay>=0&&b.homingDelay==Math.Floor(b.homingDelay),"sample constraints preserved");}
    }
    static void ContextOracle()
    {
        foreach(string scope in new[]{"group","volley","bullet"})foreach(bool perInstance in new[]{false,true}){
            var t=E("context");t.start=2;t.end=4;t.interval=2;t.indexDelay=1;t.count=3;t.x=30;t.y=40;t.speed=1;t.motion="velocity";t.velocityX=4;t.randomScope=scope;t.randomPerInstance=perInstance;
            t.expressions["turnRate"]="2+i+n*.1+volley+frame*.25+source.x*.001+x*.001+random_range(0,2)";t.expressions["homingDelay"]="1.5+i+volley+frame*.1+random_range(-1,1)";
            Capture(new BulletWorld(Scene(t)),10);
            var host=new Track{id="context-host",kind="object",start=10,end=12,x=130,y=240};host.spawnActions.Add(new BulletAction{type="spawn",templateId="shifted",start=0,spawnRepeat=2});var child=t.Snapshot();child.id=child.randomId="shifted";child.start=5;child.end=15;child.interval=4;child.count=2;child.templateOnly=true;
            Capture(new BulletWorld(Scene(host,child)),15);
        }
    }
    static void Main(string[] args)
    {
        Regressions();LimitsAndCompatibility();RetentionAndReset();Checkpoints();ContextOracle();
        if(args.Length==2&&args[0]=="--oracle")File.WriteAllText(args[1],Json.Write(oracle));
        Console.WriteLine("HomingSamplingTests passed "+checks+" checks; "+oracle.Count+" numeric oracle trajectories. C# simulation only, not native GM.");
    }
}
