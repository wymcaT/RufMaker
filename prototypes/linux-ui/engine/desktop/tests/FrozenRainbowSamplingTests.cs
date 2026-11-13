using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.IO;
internal static class FrozenRainbowSamplingTests
{
 static int checks;static List<object> oracle=new List<object>();
 static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static int Palette(double value){return (int)((Math.Floor(value)%32+32)%32);}
 static void NegativeFrames(){
  foreach(int image in new[]{-1,-2,-33,0,7}){
   var t=new Track{id="animated",kind="emitter",imageIndex=4,imageSpeed=2};var b=new SimBullet{source=t,birth=0};ObjectDynamics.Initialize(b,t,0,0,new ExpressionContext());
   TemplateRuntime.Inherit(b,new TemplateInstance{appearance=new BirthAppearance{sprite="Inherited",image=image,color=Color.White}});
   foreach(int frame in new[]{0,1,10})Check(ObjectDynamics.Image(b,frame)==image,"captured negative animation remains frozen: "+image+" at "+frame);
   var copy=b.Clone();Check(ObjectDynamics.Image(copy,10)==image,"checkpoint clone preserves frozen state");
  }
 }
 static void RainbowSamples(){
  foreach(bool reverse in new[]{false,true}){
   var t=new Track{id="rainbow",kind="emitter",start=0,end=2,interval=2,count=3,x=30,y=30,speed=0,life=20,rainbow=true,rainbowDynamic=true,rainbowReverse=reverse};
   t.expressions["rainbowDensity"]="2+i";t.expressions["rainbowOffset"]="i*3+n+volley*7+frame";t.expressions["rainbowSpeed"]="1+i";
   var s=new EditorState{duration=20,tracks=new List<Track>{t}};Motion.Normalize(s);var w=new BulletWorld(s);w.Seek(3);
   foreach(var b in w.bullets){double expected=b.index*3+b.count+b.volley*7+b.birth+b.sequence*(2+b.index)+3*(1+b.index);if(reverse)expected=-expected;Check(ObjectDynamics.Image(b,3)==Palette(expected),"birth-context rainbow parameters retained: i="+b.index+" volley="+b.volley);}
  }
 }
 static void CaptureFlows(){
  var emitter=new Track{id="copy-source",kind="emitter",start=0,end=0,life=650,count=1,speed=0,imageIndex=-2,imageSpeed=1,lane=0};Trails.SetMode(emitter,"fade");
  var copyEvent=new Track{id="copy",kind="control",start=1,end=1,lane=1,eventPreset="group-copy",eventGroup="g",eventCopyLife=650,eventCopyTrail=true,controlValue=0};
  var s=new EditorState{duration=700,timelineGroups=new List<TimelineGroup>{new TimelineGroup{id="g",lanes=new List<int>{0,1}}},tracks=new List<Track>{emitter,copyEvent}};Motion.Normalize(s);var w=new BulletWorld(s);w.Seek(2);var copied=w.bullets.Single(b=>b.generation==1);Check(ObjectDynamics.Image(copied,2)==-1,"group copy freezes negative current frame");foreach(int at in new[]{1,2,10})Check(ObjectDynamics.Image(copied,at)==-1,"trail lookup keeps frozen negative frame");w.Seek(450);Check(w.CheckpointCount>=3,"post-copy checkpoints captured");long before=w.SimulatedSteps;w.Seek(270);Check(w.SimulatedSteps-before<100,"reverse seek restores post-copy checkpoint rather than rebuilding from frame zero");Check(ObjectDynamics.Image(w.bullets.Single(b=>b.generation==1),270)==-1,"checkpoint restore preserves negative freeze");
  foreach(string timing in new[]{"after-move","action-time"}){
   var parent=new Track{id="parent",kind="emitter",start=0,end=0,life=50,count=1,speed=0,imageIndex=-2,imageSpeed=1};parent.bulletActions.Add(new BulletAction{type="spawn",templateId="child",start=1,inheritAppearance=true,spawnTiming=timing});
   var child=new Track{id="child",kind="emitter",templateOnly=true,start=0,end=0,life=30,count=1,speed=0,imageIndex=7,imageSpeed=3};s=new EditorState{duration=50,tracks=new List<Track>{parent,child}};Motion.Normalize(s);w=new BulletWorld(s);w.Seek(5);var shot=w.bullets.Single(b=>b.source.id=="child");Check(ObjectDynamics.Image(shot,5)==-1,timing+" inherited spawn freezes negative parent frame");w.Seek(0);w.Seek(5);Check(ObjectDynamics.Image(w.bullets.Single(b=>b.source.id=="child"),5)==-1,timing+" reverse seek frozen capture");
  }
  var host=new Track{id="host",kind="object",start=0,end=0};host.spawnActions.Add(new BulletAction{type="spawn",templateId="rod",start=0});var rod=new Track{id="rod",kind="rod",templateOnly=true,start=0,end=20,count=1,imageIndex=-2,imageSpeed=1,speed=0};var reset=new Track{id="reset",kind="control",start=1,end=1,eventPreset="reset-motion",eventDisableMotion=true,eventResetLife=true,eventLife=20};s=new EditorState{duration=30,tracks=new List<Track>{host,rod,reset}};Motion.Normalize(s);w=new BulletWorld(s);w.Seek(4);var released=w.bullets.Single(b=>b.source.id=="rod");Check(!released.kinematic&&ObjectDynamics.Image(released,4)==-1,"detaching live template member freezes negative frame before kinematic state changes");
 }
 static void SampledOracle(){
  foreach(string scope in new[]{"group","volley","bullet"})foreach(bool random in new[]{false,true})foreach(bool reverse in new[]{false,true}){
   var t=new Track{id="oracle",kind="emitter",start=2,end=4,interval=2,count=3,x=30,y=40,speed=0,life=30,rainbow=true,rainbowDynamic=true,rainbowReverse=reverse,randomScope=scope,rainbowRandom=random};
   t.expressions["rainbowDensity"]="2+i+random_range(0,2)";t.expressions["rainbowOffset"]="n+volley*7+frame+source.x*.1+random_range(-4,4)";t.expressions["rainbowSpeed"]=".5+i*.25+random_range(0,.5)";
   var s=new EditorState{randomSeed=34567,duration=30,tracks=new List<Track>{t}};Motion.Normalize(s);var w=new BulletWorld(s);w.Seek(7);
   foreach(var b in w.bullets)oracle.Add(new{track=b.source.Snapshot(),seed=s.randomSeed,worldFrame=7,b.birth,b.index,b.count,b.volley,b.clockShift,b.sourceX,b.sourceY,b.sequence,density=b.rainbowDensity,offset=b.rainbowOffset,paletteSpeed=b.rainbowSpeed,image=ObjectDynamics.Image(b,7)});
   var images=w.bullets.Select(b=>ObjectDynamics.Image(b,7)).ToArray();w.Seek(2);w.Seek(7);Check(images.SequenceEqual(w.bullets.Select(b=>ObjectDynamics.Image(b,7))),"random rainbow seek reproducibility "+scope+" random="+random+" reverse="+reverse);
  }
  foreach(string scope in new[]{"group","volley","bullet"})foreach(bool random in new[]{false,true}){
   var host=new Track{id="oracle-host",kind="object",start=10,end=12,x=130,y=240};host.spawnActions.Add(new BulletAction{type="spawn",templateId="shifted",start=0,spawnRepeat=2});
   var t=new Track{id="shifted",kind="emitter",templateOnly=true,start=5,end=15,interval=20,indexDelay=1,count=2,x=30,y=40,speed=0,life=30,rainbow=true,rainbowDynamic=true,randomScope=scope,randomPerInstance=true,rainbowRandom=random};
   t.expressions["rainbowDensity"]="2+i+random_range(0,2)";t.expressions["rainbowOffset"]="n+volley*7+frame+source.x*.1+random_range(-4,4)";t.expressions["rainbowSpeed"]=".5+i*.25+random_range(0,.5)";
   var s=new EditorState{randomSeed=34567,duration=40,tracks=new List<Track>{host,t}};Motion.Normalize(s);var w=new BulletWorld(s);w.Seek(15);Check(w.bullets.Count==4,"two shifted template instances emit delayed members");
   foreach(var b in w.bullets){var owner=w.Templates.ObservedInstances.Single(i=>i.shift==b.clockShift);oracle.Add(new{track=b.source.Snapshot(),seed=s.randomSeed,worldFrame=15,b.birth,b.index,b.count,b.volley,b.clockShift,b.sourceX,b.sourceY,b.sequence,ownerBirth=owner.birth,ownerSerial=owner.serial,density=b.rainbowDensity,offset=b.rainbowOffset,paletteSpeed=b.rainbowSpeed,image=ObjectDynamics.Image(b,15)});}
   var images=w.bullets.Select(b=>ObjectDynamics.Image(b,15)).ToArray();w.Seek(10);w.Seek(15);Check(images.SequenceEqual(w.bullets.Select(b=>ObjectDynamics.Image(b,15))),"template random per-instance sampling repeats after seek");
  }
  var keyed=new Track{id="keys",kind="emitter",start=0,end=2,interval=2,count=1,speed=0,life=20,rainbow=true,rainbowDynamic=false,rainbowOffset=1};keyed.keyframes.Add(new Keyframe{property="rainbowOffset",frame=0,value=1,easing="step"});keyed.keyframes.Add(new Keyframe{property="rainbowOffset",frame=2,value=9,easing="step"});var state=new EditorState{tracks=new List<Track>{keyed}};Motion.Normalize(state);var world=new BulletWorld(state);world.Seek(4);Check(ObjectDynamics.Image(world.bullets.Single(b=>b.birth==0),4)==1,"flying rainbow keeps old keyframe birth value");Check(ObjectDynamics.Image(world.bullets.Single(b=>b.birth==2),4)==10,"new rainbow birth uses changed keyframe and sequence");var live=new SimBullet{source=keyed,kinematic=true,sequence=0};Check(ObjectDynamics.Image(live,0)==1&&ObjectDynamics.Image(live,2)==9,"kinematic rainbow continues live sampling");
  foreach(string mode in new[]{"sequence","distance","angle"})foreach(bool random in new[]{false,true}){
   var rod=new Track{id="released-rod",kind="rod",start=3,end=20,releaseFrame=5,count=3,speed=0,life=20,rainbow=true,rainbowDynamic=true,rainbowRandom=random,rainbowMode=mode,rainbowDensity=2,rainbowOffset=4,rainbowSpeed=.5};var st=new EditorState{randomSeed=7654,tracks=new List<Track>{rod}};Motion.Normalize(st);var rw=new BulletWorld(st);rw.Seek(8);foreach(var b in rw.bullets)Check(ObjectDynamics.Image(b,8)==Rainbow.Index(b.source,b.sequence,8,5,false),"released rod preserves prior sequence mapping "+mode+" random="+random);
  }
 }
 static void Main(string[] args){if(args.Length==0||args[0]=="--negative"||args[0]=="--oracle")NegativeFrames();if(args.Length==0||args[0]=="--rainbow"||args[0]=="--oracle")RainbowSamples();if(args.Length==0||args[0]=="--oracle"){CaptureFlows();SampledOracle();}if(args.Length>0&&args[0]=="--oracle")File.WriteAllText(args[1],Json.Write(oracle));Console.WriteLine("FrozenRainbowSamplingTests passed "+checks+" checks; C# simulation only.");}
}
