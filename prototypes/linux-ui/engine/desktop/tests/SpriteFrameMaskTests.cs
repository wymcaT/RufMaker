using System;
using System.Collections.Generic;
using System.Linq;
internal static class SpriteFrameMaskTests
{
 static int checks;
 static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static Track E(){return new Track{id="e",kind="emitter",start=0,end=0,count=1,interval=1,speed=0,life=20,x=-4,y=10,scale=1,sprite="Mask",despawnBounds="sprite-mask"};}
 static SpriteMeta Meta(){return new SpriteMeta{frames=2,colliders=new[]{new SpriteCollider{left=0,right=0,top=0,bottom=0},new SpriteCollider{left=8,right=9,top=0,bottom=0}},revision="frame-mask-test"};}
 static BulletWorld World(Track t){var s=new EditorState{duration=30,tracks=new List<Track>{t}};Motion.Normalize(s);return new BulletWorld(s,new RoomData{width=100,height=100},null,n=>Meta());}
 static void Main()
 {
  foreach(bool rainbow in new[]{false,true})foreach(double frame in new[]{0.0,1.0,2.0,3.9,101.0}){
   var t=E();t.rainbow=rainbow;t.rainbowSprite="Mask";t.spriteFrame=frame;t.rainbowOffset=0;var w=World(t);w.Seek(0);bool alive=((int)Math.Floor(frame)%2)==1;
   Check((w.bullets.Count==1)==alive,"explicit frame selects collision mask: "+frame+" rainbow="+rainbow);w.Seek(5);Check((w.bullets.Count==1)==alive,"frozen frame remains at later age");w.Seek(0);Check((w.bullets.Count==1)==alive,"backward seek reproduces mask decision");
  }
  var a=E();a.spriteFrame=-1;a.imageIndex=1;a.imageSpeed=1;var animated=World(a);animated.Seek(0);Check(animated.bullets.Count==1,"automatic animation starts on frame 1");animated.Seek(1);Check(animated.bullets.Count==0,"automatic animation changes mask next frame");
  var r=E();r.rainbow=true;r.rainbowSprite="Mask";r.rainbowDensity=1;r.rainbowOffset=0;r.count=2;r.expressions["spriteFrame"]="at(i,0,-1)";var mixed=World(r);mixed.Seek(0);Check(mixed.bullets.Count==1&&mixed.bullets[0].index==1,"negative per-bullet explicit sample stays rainbow; no context-free resampling");Check(ObjectDynamics.Image(mixed.bullets[0],0)==1,"rainbow fallback picks sequence frame");mixed.Seek(1);mixed.Seek(0);Check(mixed.bullets.Count==1&&mixed.bullets[0].index==1,"mixed sample repeatability");
  var key=E();key.despawnBounds="none";key.x=10;key.end=2;key.imageIndex=6;key.imageSpeed=1;key.keyframes.Add(new Keyframe{property="spriteFrame",frame=0,value=1,easing="step"});key.keyframes.Add(new Keyframe{property="spriteFrame",frame=1,value=-1,easing="step"});key.keyframes.Add(new Keyframe{property="spriteFrame",frame=2,value=3,easing="step"});var keyed=World(key);keyed.Seek(2);Check(ObjectDynamics.Image(keyed.bullets.Single(b=>b.birth==0),2)==1,"old explicit bullet retains birth sample");Check(ObjectDynamics.Image(keyed.bullets.Single(b=>b.birth==1),2)==7,"negative birth sample uses retained animation");Check(ObjectDynamics.Image(keyed.bullets.Single(b=>b.birth==2),2)==3,"new explicit sample applies at next birth");
  var local=new Track{kind="emitter",start=5,rainbow=true,rainbowDynamic=true,rainbowDensity=1,rainbowOffset=2,rainbowSpeed=1};var bLocal=new SimBullet{source=local,birth=105,clockShift=100,sequence=1};ObjectDynamics.Initialize(bLocal,local,105,5,new ExpressionContext());Check(ObjectDynamics.Image(bLocal,108)==6,"template-local rainbow clock matches world start 105");bLocal.frozenImage=9;Check(ObjectDynamics.Image(bLocal,108)==9,"inherited frozen frame overrides rainbow");
  Console.WriteLine("SpriteFrameMaskTests passed "+checks+" checks; C# simulation only, no native GM execution.");
 }
}
