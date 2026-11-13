using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
internal static class SpriteAxisParityTests
{
 static void Check(bool x,string m){if(!x)throw new Exception(m);}
 static void Near(double a,double b,string m){Check(Math.Abs(a-b)<1e-10,m+": "+a+" != "+b);}
 static Track E(string id="e"){return new Track{id=id,kind="emitter",start=0,end=0,count=1,speed=0,life=30,scale=2,scaleX=-2,scaleY=3,advancedBullets=true,trailMode="fade"};}
 static EditorState S(params Track[] ts){var s=new EditorState{duration=300,tracks=ts.ToList()};Motion.Normalize(s);return s;}
 static void Axes(SimBullet b,double x,double y,double z,string m){Near(b.axisX,x,m+" X");Near(b.axisY,y,m+" Y");Near(b.scale,z,m+" uniform");}
 static void Main(string[] args)
 {
  var e=E();e.end=2;e.interval=2;e.keyframes.Add(new Keyframe{property="scaleX",frame=0,value=-2});e.keyframes.Add(new Keyframe{property="scaleX",frame=2,value=2});
  var w=new BulletWorld(S(e));w.Seek(2);Axes(w.bullets.Single(b=>b.birth==0),-2,3,2,"sample at birth retained");Axes(w.bullets.Single(b=>b.birth==2),2,3,2,"second birth samples new axes");w.Seek(0);Axes(w.bullets.Single(),-2,3,2,"reverse seek");
  e=E();e.count=3;e.expressions["scaleX"]="at(i,-2,0,3)";e.expressions["scaleY"]="i+1";w=new BulletWorld(S(e));w.Seek(0);for(int i=0;i<3;i++)Axes(w.bullets.Single(b=>b.index==i),new[]{-2.0,0,3}[i],i+1,2,"indexed axes");
  e=E();e.bulletActions.Add(new BulletAction{type="scale",start=1,duration=0,to=3,relative=true});var control=new Track{id="c",kind="control",start=2,end=2,targetSource="e",controlAction="scale",controlValue=4};w=new BulletWorld(S(e,control));w.Seek(1);Axes(w.bullets.Single(),-2,3,6,"relative uniform action");w.Seek(2);Axes(w.bullets.Single(),-2,3,4,"uniform control");var h=w.bullets.Single().history.Single(p=>p.frame==2);Near(h.axisX,-2,"history X");Near(h.axisY,3,"history Y");Near(h.scale,4,"history uniform");
  e=E();e.bulletActions.Add(new BulletAction{type="split",start=1,duration=0,count=1,childSpeed=0,maxGeneration=1});w=new BulletWorld(S(e));w.Seek(1);Axes(w.bullets.Single(b=>b.generation==1),-2,3,2,"plain split");
  var child=E("child");child.templateOnly=true;child.scale=3;child.scaleX=4;child.scaleY=-5;e=E();e.bulletActions.Add(new BulletAction{type="split",templateId="child",start=1,duration=0,count=1,childSpeed=0,maxGeneration=1});w=new BulletWorld(S(e,child));w.Seek(1);Axes(w.bullets.Single(b=>b.generation==1),4,-5,3,"template split owns axes");
  e=E();e.lane=0;var copy=new Track{id="copy",kind="control",lane=1,start=2,end=2,eventPreset="group-copy",eventGroup="g",eventCopyLife=20};var s=S(e,copy);s.timelineGroups.Add(new TimelineGroup{id="g",name="Group",lanes=new List<int>{0,1}});Motion.Normalize(s);w=new BulletWorld(s);w.Seek(2);Axes(w.bullets.Single(b=>b.generation==1),-2,3,2,"group copy preserves axes");w.Seek(1);w.Seek(2);Axes(w.bullets.Single(b=>b.generation==1),-2,3,2,"group copy reversible");
  var rod=new Track{id="rod",kind="rod",lane=0,start=0,end=10,count=1,scale=2,scaleX=0,scaleY=-4};s=S(rod,copy);s.timelineGroups.Add(new TimelineGroup{id="g",name="Group",lanes=new List<int>{0,1}});Motion.Normalize(s);w=new BulletWorld(s);w.Seek(2);Axes(w.bullets.Single(),0,-4,2,"fixed rod copy zero axis");
  e=E();e.life=10;e.deathEffect="shrink";e.deathFrames=10;w=new BulletWorld(S(e));w.Seek(5);var dying=w.bullets.Single();Axes(dying,-2,3,2,"death effect keeps raw axes");Near(dying.Sample(5).scale,1,"death history uniform factor");Near(BirthAppearance.BulletAt(dying,5).scale,1,"inherit size omits signed axes");
  e=E();child=E("child");child.templateOnly=true;child.scale=3;child.scaleX=4;child.scaleY=-5;e.bulletActions.Add(new BulletAction{type="spawn",templateId="child",start=1,duration=0,inheritScale=true,maxGeneration=1});w=new BulletWorld(S(e,child));w.Seek(1);var born=w.bullets.Single(p=>p.source.id=="child");Axes(born,4,-5,2,"spawn replaces initial uniform size only");
  if(args.Length>0){var rows=new List<object>();foreach(int f in new[]{0,2,5,7,10}){var t=new Track{id="o",kind="object",scale=2,scaleY=-3};t.keyframes.Add(new Keyframe{property="scaleX",frame=0,value=-2});t.keyframes.Add(new Keyframe{property="scaleX",frame=10,value=2});S(t);rows.Add(new{frame=f,x=Motion.Value(t,"scaleX",f),y=Motion.Value(t,"scaleY",f)});}File.WriteAllText(args[0],Json.Write(rows));}
  Console.WriteLine("PASS: sampled/animated axes, indexed values, zero/mirrors, actions/controls, split/template inheritance, flying/fixed group copies, trails, lifetime, and repeat/reverse seek.");
 }
}
