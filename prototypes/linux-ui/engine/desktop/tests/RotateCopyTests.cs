using System;
using System.Linq;
using System.Drawing;
internal static class RotateCopyTests
{
    static int checks;
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static void Near(double a,double b,string message){Check(Math.Abs(a-b)<.001,message+": "+a+" / "+b);}
    static EditorState Scene(int count,bool linked,double parentAngle)
    {
        var s=new EditorState{fps=50,duration=100};
        var parent=new Track{id="parent",name="parent",kind="group",start=0,end=90,x=400,y=304,angle=parentAngle};
        var root=new Track{id="root",name="root",kind="group",parentId="parent",start=0,end=80,x=20,y=10,angle=23};
        root.keyframes.Add(new Keyframe{property="angle",frame=0,value=23});root.keyframes.Add(new Keyframe{property="angle",frame=20,value=63});
        var child=new Track{id="child",name="child",kind="object",parentId="root",start=0,end=80,x=80,y=30};
        child.keyframes.Add(new Keyframe{property="x",frame=0,value=80});child.keyframes.Add(new Keyframe{property="x",frame=20,value=140});
        s.tracks.Add(parent);s.tracks.Add(root);s.tracks.Add(child);Motion.Normalize(s);
        var a=CopyGeneration.Apply(s,root);a.copyMode="rotate";a.copyCount=count;a.copyLinked=linked;a.spawnRepeat=0;Motion.Normalize(s);return s;
    }
    static void Geometry(EditorState s,int count,bool linked,int frame)
    {
        var w=new BulletWorld(s);w.Seek(frame);var root=w.State.tracks.Single(t=>t.id=="root");var pose=Motion.At(root,frame);
        var instances=w.Templates.ObservedInstances.ToArray();Check(instances.Length==count-1,"Instance count");
        foreach(var inst in instances){var child=inst.definition.scene.tracks.First(t=>t.kind=="object");var p=Motion.At(child,frame-inst.shift);var actual=inst.Point(p.x,p.y);double sector=linked?inst.placement.angle:inst.rotation+inst.definition.anchor.angle-Motion.At(root,0).angle;
            {var source=Motion.At(w.State.tracks.Single(t=>t.id=="child"),frame);double r=sector*Math.PI/180;Near(actual.X,pose.x+(source.x-pose.x)*Math.Cos(r)+(source.y-pose.y)*Math.Sin(r),"Rotated child X");Near(actual.Y,pose.y-(source.x-pose.x)*Math.Sin(r)+(source.y-pose.y)*Math.Cos(r),"Rotated child Y");}
            Near(inst.x,Motion.At(root,linked?frame:0).x,"Pivot X");Near(inst.y,Motion.At(root,linked?frame:0).y,"Pivot Y");
        }
    }
    static void DepthModes()
    {
        foreach(string kind in new[]{"object","rod","emitter"})foreach(int? depth in new int?[]{null,-100})foreach(int generation in new[]{0,1,2})
        {
            var t=new Track{kind=kind,depth=depth};var b=new SimBullet{source=t,generation=generation};
            double legacy=generation>0&&!depth.HasValue?-7000000-generation:DrawingDepth.Track(t)-generation;
            Near(DrawingDepth.Bullet(b),legacy,"Legacy depth preserved");t.depthMode="absolute";Near(DrawingDepth.Bullet(b),DrawingDepth.Track(t),"Absolute depth");
            b.drawDepth=123;Near(DrawingDepth.Bullet(b),123,"Event override remains first");
            var saved=Json.Read<Track>(Json.Write(t));Check(saved.depthMode=="absolute"&&saved.depth==t.depth,"Depth JSON");Check(t.Snapshot().depthMode=="absolute","Depth snapshot");
        }
        var old=Json.Read<Track>("{\"kind\":\"rod\"}");DrawingDepth.Normalize(old);Check(old.depthMode=="generation","Old JSON default");
        var bad=new Track{depthMode="typo"};bool rejected=false;try{DrawingDepth.Normalize(bad);}catch(ArgumentException){rejected=true;}Check(rejected,"Invalid mode rejected");
        var nested=new Track();nested.children.Add(new Track{depthMode=null});DrawingDepth.Normalize(nested);Check(nested.children[0].depthMode=="generation","Nested default");
    }
    static void Main()
    {
        DepthModes();
        foreach(int n in new[]{1,2,10}){var a=new BulletAction{copyMode="rotate",copyCount=n,copyCenter=false,copyRadius=0,copyAngle=71};var ps=CopyGeneration.Placements(a);Check(ps.Count==n-1,"Total includes original");for(int i=0;i<ps.Count;i++){Near(ps[i].x,0,"No translation X");Near(ps[i].y,0,"No translation Y");Near(ps[i].angle,360.0*(i+1)/n,"Sector angle");Near(CopyGeneration.Rotation(a,ps[i],83,23),60+ps[i].angle,"Rotated owner context");}
            foreach(bool linked in new[]{true,false})foreach(double parent in new[]{0.0,57.0}){var s=Scene(n,linked,parent);foreach(int frame in new[]{0,10,20})Geometry(s,n,linked,frame);var snapshot=s.Snapshot();var restored=Json.Read<EditorState>(Json.Write(s));Motion.Normalize(restored);Geometry(restored,n,linked,10);Check(restored.tracks.Single(t=>t.id=="root").spawnActions[0].copyMode=="rotate","Mode survives JSON");var child=s.tracks.Single(t=>t.id=="child");Motion.EditValue(child,"x",10,190);Check(snapshot.tracks.Single(t=>t.id=="child").keyframes.Count==2,"Snapshot independent of live edit");if(linked)Geometry(s,n,linked,10);Geometry(snapshot,n,linked,10);Check(s.tracks.Count==snapshot.tracks.Count,"No baked tracks");}
        }
        Console.WriteLine("PASS "+checks+" rotate copy checks");
    }
}
