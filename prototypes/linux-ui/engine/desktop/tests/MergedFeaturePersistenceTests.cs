using System;
using System.Linq;
using System.Collections.Generic;
internal static class MergedFeaturePersistenceTests
{
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Verify(Track t){Check(t.moveOnBirth&&t.scaleX==-2&&t.scaleY==3&&t.spriteFrame==2&&t.blendMode=="add"&&t.cameraViewWidth==640&&t.cameraViewHeight==360,"Merged fields lost");Check(t.expressions["speed"]=="at(i,2,4,6)"&&t.keyframes.Any(k=>k.property=="spriteFrame"&&k.value==3),"Merged formula or key lost");}
 static void Main(){var t=new Track{id="merged",name="Merged",kind="emitter",start=0,end=10,moveOnBirth=true,scaleX=-2,scaleY=3,spriteFrame=2,blendMode="add",cameraViewWidth=640,cameraViewHeight=360};t.expressions["speed"]="at(i,2,4,6)";t.keyframes.Add(new Keyframe{property="spriteFrame",frame=5,value=3});var s=new EditorState{tracks=new List<Track>{t}};Motion.Normalize(s);Verify(s.tracks[0]);Verify(s.Snapshot().tracks[0]);Verify(Json.Read<EditorState>(Json.Write(s)).tracks[0]);Verify(BulletSystem.CloneTracks(s.tracks)[0]);Verify(TrackPreset.Capture("Merged",s.tracks,50).Instantiate(0,50)[0]);Console.WriteLine("PASS: new motion features coexist with sprite frame, signed axes, blending and camera dimensions through normalize, snapshot, JSON, copy and preset.");}
}
