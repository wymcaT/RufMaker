using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Web.Script.Serialization;
internal static class ObjectMotionTests
{
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
 static void Near(double got,double want,string text){Check(Math.Abs(got-want)<1e-9,text+": "+got+" != "+want);}
 static Track E(bool on){return new Track{id="e",name="Emitter",kind="emitter",start=10,end=10,x=100,y=100,count=1,speed=5,life=20,moveOnBirth=on,advancedBullets=true};}
 static EditorState Scene(params Track[] tracks){var s=new EditorState{duration=500,tracks=tracks.ToList()};Motion.Normalize(s);return s;}
 static SimBullet First(BulletWorld w){return w.bullets.Single(b=>b.generation==0);}
 static void Main(string[] args)
 {
  if(args.Length>0&&args[0]=="--sample"){Sample(args);return;}
  var off=new BulletWorld(Scene(E(false)));off.Seek(10);Near(First(off).x,100,"legacy birth");off.Seek(11);Near(First(off).x,105,"legacy next");
  var t=E(true);var state=Scene(t);var w=new BulletWorld(state);w.Seek(10);Near(First(w).x,105,"opted birth");w.Seek(11);Near(First(w).x,110,"opted next");w.Seek(10);Near(First(w).x,105,"backward seek");
  t=E(true);t.bulletActions.Add(new BulletAction{type="speed",start=0,duration=0,to=-4});t.bulletActions.Add(new BulletAction{type="acceleration",start=0,duration=0,to=1});
  var c=new Track{id="c",kind="control",start=10,end=10,targetSource="e",controlAction="speed",controlValue=-6};w=new BulletWorld(Scene(t,c));w.Seek(10);Near(First(w).speed,-5,"signed action-control-accel order");Near(First(w).x,95,"signed movement");
  t=E(true);t.bulletActions.Add(new BulletAction{type="turn",start=0,duration=0,to=90});w=new BulletWorld(Scene(t));w.Seek(10);Near(First(w).x,100,"birth turn x");Near(First(w).y,95,"birth turn before movement");
  t=E(true);t.count=2;t.indexDelay=2;t.spread=0;w=new BulletWorld(Scene(t));w.Seek(10);Near(w.bullets.Single(b=>b.index==1).x,100,"delayed remains unmoved");w.Seek(12);Near(w.bullets.Single(b=>b.index==1).x,105,"delayed actual birth moved");
  t=E(true);t.bulletActions.Add(new BulletAction{type="split",start=0,duration=0,count=1,childSpeed=2,maxGeneration=1});w=new BulletWorld(Scene(t));w.Seek(10);Near(First(w).x,105,"parent moves before split");Near(w.bullets.Single(b=>b.generation==1).x,107,"child born at post-move parent then own birth movement");
  t=E(true);var reset=new Track{id="r",kind="control",start=12,end=12,eventPreset="reset-motion",eventRestoreBirth=true,eventDisableMotion=true,eventResetLife=true,eventLife=20};w=new BulletWorld(Scene(t,reset));w.Seek(12);Near(First(w).x,110,"reset age does not replay birth move");Check(!First(w).birthMovePending,"birth flag consumed");w.Seek(13);Near(First(w).x,115,"post reset normal step");
  t=E(true);t.expressions["speed"]="at(i,2,4,6)";state=Scene(t);var undo=state.Snapshot();state.tracks[0].moveOnBirth=false;Check(undo.tracks[0].moveOnBirth,"undo snapshot isolated");Check(BulletSystem.CloneTracks(undo.tracks)[0].moveOnBirth,"copy");var json=new JavaScriptSerializer();var roundtrip=json.Deserialize<EditorState>(json.Serialize(undo));Motion.Normalize(roundtrip);Check(roundtrip.tracks[0].moveOnBirth,"JSON roundtrip");Check(!json.Deserialize<Track>("{\"id\":\"old\",\"kind\":\"emitter\"}").moveOnBirth,"old JSON default");
  var preset=TrackPreset.Capture("Indexed",undo.tracks,50);var instances=json.Deserialize<TrackPreset>(json.Serialize(preset)).Instantiate(30,50);Check(instances[0].moveOnBirth&&instances[0].expressions["speed"]=="at(i,2,4,6)","preset and expressions");
  double[] indices={-1e100,-.1,0,.99,1.99,2,1e100},expected={11,11,11,11,22,33,33};for(int i=0;i<indices.Length;i++)Near(RandomExpression.Evaluate("at(i,11,22,33)",1,"x",new ExpressionContext{i=indices[i]}),expected[i],"at bounds");
  foreach(string bad in new[]{"at()","at(0)"}){bool failed=false;try{RandomExpression.Parse(bad);}catch(FormatException){failed=true;}Check(failed,"at empty rejected");}
  string list="at(i,"+String.Join(",",Enumerable.Range(0,100).Select(i=>(i*.123-5).ToString("R",System.Globalization.CultureInfo.InvariantCulture)))+")";for(int i=0;i<100;i++)Near(RandomExpression.Evaluate(list,1,"x",new ExpressionContext{i=i}),i*.123-5,"100-entry expression");
  Near(RandomExpression.Evaluate("at(1,1,2)+random(100)",123,"x"),2+RandomExpression.Evaluate("random(100)",123,"x"),"no random consumption");
  Check(ExpressionEditor.Templates.Contains("at(i,2,4,6,8)")&&ExpressionEditor.TemplateNames.Length==ExpressionEditor.Templates.Length,"native template picker");
  PreviewHiddenAndOrbitTests.TestOrbitEventAgeBoundary();
  Console.WriteLine("PASS: legacy/default, birth/control/acceleration/turn/split order, delayed birth, signed speed, reset life, seek, snapshots, copy, JSON, presets, indexed expressions and picker arrays.");
 }
 static void Sample(string[] args)
 {
  var json=new JavaScriptSerializer{MaxJsonLength=Int32.MaxValue};var s=json.Deserialize<EditorState>(File.ReadAllText(args[1]));Motion.Normalize(s);var w=new BulletWorld(s);var result=new List<object>();
  foreach(int f in args[3].Split(',').Select(Int32.Parse)){w.Seek(f);result.Add(new{frame=f,bullets=w.bullets.Where(b=>f>=b.birth&&!b.dead).Select(b=>new{source=b.source.id,index=b.index,birth=b.birth,x=b.x,y=b.y,speed=b.speed,direction=b.direction,scale=b.scale,alpha=b.alpha,sprite=b.source.sprite,generation=b.generation}).ToArray()});}
  File.WriteAllText(args[2],json.Serialize(result));Console.WriteLine("Sampled "+result.Count+" frames with actual C# BulletWorld.");
 }
}
