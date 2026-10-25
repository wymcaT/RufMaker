using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

internal sealed class TemplateDefinition
{
    public EditorState scene;public Track root;public Keyframe anchor;
}
internal sealed class TemplateInstance
{
    public int serial,birth,generation,shift,lastFrame=-1;public double x,y,rotation;public TemplateDefinition definition;
    public TemplateInstance Clone(){return (TemplateInstance)MemberwiseClone();}
    public PointF Point(double px,double py)
    {double a=rotation*Math.PI/180,dx=px-definition.anchor.x,dy=py-definition.anchor.y;return new PointF((float)(x+dx*Math.Cos(a)+dy*Math.Sin(a)),(float)(y-dx*Math.Sin(a)+dy*Math.Cos(a)));}
}
internal sealed class TemplateSnapshot
{
    public int serial;public List<TemplateInstance> instances;public HashSet<string> hidden;
}
internal sealed class TemplateRuntime
{
    private readonly BulletWorld world;private readonly Dictionary<string,TemplateDefinition> definitions=new Dictionary<string,TemplateDefinition>();
    private readonly Dictionary<string,SimBullet> fixedBullets=new Dictionary<string,SimBullet>();
    private readonly List<TemplateInstance> instances=new List<TemplateInstance>();private HashSet<string> hidden=new HashSet<string>();private int serial;
    public TemplateRuntime(BulletWorld world){this.world=world;}
    public int InstanceCount {get{return instances.Count;}}
    public static bool Eligible(Track t){return t!=null&&new[]{"object","rod","emitter","group"}.Contains(t.kind);}
    public static IEnumerable<BulletAction> Actions(Track t){return t.bulletActions.Concat(t.spawnActions);}
    public static bool Trigger(BulletAction a,int age,int index,int generation)
    {
        if(!a.enabled||a.type!="spawn"||generation>=a.maxGeneration||index<a.spawnIndexFrom||a.spawnIndexTo>=0&&index>a.spawnIndexTo)return false;
        if(a.spawnParity!="all"&&index%2!=(a.spawnParity=="even"?0:1))return false;
        int at=age-a.start-index*a.stagger;if(at<0)return false;
        return a.spawnRepeat>0?(a.duration==0||at<=a.duration)&&at%a.spawnRepeat==0:at==0;
    }
    private static string Key(int owner,Track track,int index){return owner+"|"+track.id+"|"+index;}
    public bool Visible(Track track,int index=-1,int owner=0)
    {
        for(var t=track;t!=null;t=t.parent)if(hidden.Contains(Key(owner,t,-1)))return false;
        return index<0||!hidden.Contains(Key(owner,track,index));
    }
    private void Hide(Track track,int index,int owner){hidden.Add(Key(owner,track,index));}
    private TemplateDefinition Definition(string id)
    {
        TemplateDefinition found;if(definitions.TryGetValue(id,out found))return found;
        var source=world.State.tracks.FirstOrDefault(t=>t.id==id);if(!Eligible(source))return null;
        var selected=new HashSet<string>{id};bool more;
        do{more=false;foreach(var t in world.State.tracks)if(selected.Contains(t.parentId))more|=selected.Add(t.id);}while(more);
        var tracks=world.State.tracks.Where(t=>selected.Contains(t.id)).Select(t=>t.Snapshot()).ToList();var root=tracks.First(t=>t.id==id);root.parentId="";root.enabled=true;root.templateOnly=false;
        var scene=new EditorState{fps=world.State.fps,duration=world.State.duration,randomSeed=world.State.randomSeed,nativeTest=world.State.nativeTest,usePlayerTrace=world.State.usePlayerTrace,playerTrace=world.State.playerTrace,tracks=tracks};Motion.Normalize(scene);
        found=new TemplateDefinition{scene=scene,root=root,anchor=Motion.At(root,root.start)};definitions[id]=found;return found;
    }
    public TemplateSnapshot Capture(){return new TemplateSnapshot{serial=serial,instances=instances.Select(t=>t.Clone()).ToList(),hidden=new HashSet<string>(hidden)};}
    public void Restore(TemplateSnapshot snapshot)
    {
        instances.Clear();hidden=snapshot==null?new HashSet<string>():new HashSet<string>(snapshot.hidden);serial=snapshot==null?0:snapshot.serial;
        if(snapshot!=null)instances.AddRange(snapshot.instances.Select(t=>t.Clone()));fixedBullets.Clear();foreach(var b in world.bullets.Where(b=>b.kinematic))fixedBullets[Key(b.templateOwner,b.source,b.index)]=b;
    }
    private bool Generate(BulletAction action,int frame,double x,double y,double heading,int generation)
    {
        var definition=Definition(action.templateId);if(definition==null)return false;
        double rotation=action.inheritAngle?heading:0,a=rotation*Math.PI/180;
        var instance=new TemplateInstance{serial=++serial,birth=frame,shift=frame-definition.root.start,generation=generation+1,x=x+action.spawnOffsetX*Math.Cos(a)+action.spawnOffsetY*Math.Sin(a),y=y-action.spawnOffsetX*Math.Sin(a)+action.spawnOffsetY*Math.Cos(a),rotation=rotation,definition=definition};
        instances.Add(instance);StepInstance(instance,frame);return true;
    }
    public void FromBullet(SimBullet bullet,int frame)
    {
        if(bullet.behavior==null)return;
        bool destroy=false;
        foreach(var a in bullet.behavior.bulletActions.Where(a=>a.type=="spawn"))if(Trigger(a,frame-bullet.birth,bullet.index,bullet.generation))
            if(Generate(a,frame,bullet.x,bullet.y,bullet.direction,bullet.generation))destroy|=a.destroyParent;
        if(destroy)bullet.dead=true;
    }
    private void TrackActions(Track track,int time,int frame,int owner,int generation,TemplateInstance instance)
    {
        if(track.spawnActions.Count==0||!Visible(track,-1,owner))return;
        var pose=Motion.At(track,time);
        foreach(var action in track.spawnActions)
        {
            bool each=action.spawnOrigin=="each"&&track.kind=="rod";var points=each?Motion.RodPoints(track,time):new[]{new PointF((float)pose.x,(float)pose.y)};
            for(int i=0;i<points.Length;i++)
            {
                int member=each?i:-1;if(!Visible(track,member,owner)||!Trigger(action,time-track.start,i,generation))continue;
                var p=points[i];double heading=each?ExpressionContext.Direction(pose.x,pose.y,p.X,p.Y):pose.angle;
                if(instance!=null){p=instance.Point(p.X,p.Y);heading+=instance.rotation;}
                if(Generate(action,frame,p.X,p.Y,heading,generation)&&action.destroyParent)Hide(track,member,owner);
            }
        }
    }
    public void Step(int frame)
    {
        foreach(var t in world.State.tracks.Where(t=>Eligible(t)&&BulletSystem.Active(t,frame)))TrackActions(t,frame,frame,0,0,null);
        foreach(var instance in instances.ToArray())StepInstance(instance,frame);
        instances.RemoveAll(t=>frame-t.shift>t.definition.root.end);
        foreach(var key in fixedBullets.Where(p=>p.Value.dead).Select(p=>p.Key).ToArray())fixedBullets.Remove(key);
    }
    private void StepInstance(TemplateInstance instance,int frame)
    {
        if(instance.lastFrame==frame)return;instance.lastFrame=frame;int time=frame-instance.shift;var scene=instance.definition.scene;
        foreach(var t in scene.tracks.Where(t=>Eligible(t)&&BulletSystem.Active(t,time)))TrackActions(t,time,frame,instance.serial,instance.generation,instance);
        foreach(var t in scene.tracks.Where(t=>Eligible(t)&&BulletSystem.Active(t,time)&&Visible(t,-1,instance.serial)))
        {
            if(t.kind=="emitter")
            {
                int dt=time-t.start;if(dt%Math.Max(1,t.interval)!=0)continue;int n=Math.Max(1,Math.Min(300,BulletSystem.Round(Motion.Value(t,"count",time))));
                for(int i=0;i<n;i++){var b=world.Spawn(t,frame+i*Math.Max(0,t.indexDelay),i,n,dt/Math.Max(1,t.interval),null,null,null,instance.generation,false,instance.shift,instance);world.TickNew(b,frame);}continue;
            }
            if(t.kind!="rod"&&t.kind!="object")continue;
            var pose=Motion.At(t,time);var points=t.kind=="rod"?Motion.RodPoints(t,time):new[]{new PointF((float)pose.x,(float)pose.y)};
            if(t.kind=="rod"&&t.releaseFrame>=0&&time>=t.releaseFrame)
            {
                if(time==t.releaseFrame)for(int i=0;i<points.Length;i++)if(Visible(t,i,instance.serial))
                {
                    var p=instance.Point(points[i].X,points[i].Y);var center=instance.Point(pose.x,pose.y);
                    double direction=t.releaseDirection=="fixed"?pose.angle+instance.rotation:t.releaseDirection=="aim"?ExpressionContext.Direction(p.X,p.Y,BulletSystem.PlayerAt(world.State,frame).x,BulletSystem.PlayerAt(world.State,frame).y):ExpressionContext.Direction(center.X,center.Y,p.X,p.Y)+(t.releaseDirection=="tangent"?90:0);
                    var b=world.Spawn(t,frame,i,points.Length,0,p,direction,Motion.Value(t,"speed",time),instance.generation,false,instance.shift);if(t.rainbow&&Rainbow.Mode(t)!="sequence")b.sequence=Rainbow.RodSequence(t,i,points.Length,time,points[i],pose,instance.rotation);world.TickNew(b,frame);
                }continue;
            }
            for(int i=0;i<points.Length;i++)
            {
                if(!Visible(t,t.kind=="rod"?i:-1,instance.serial))continue;string key=Key(instance.serial,t,i);SimBullet b;
                if(!fixedBullets.TryGetValue(key,out b)||b.dead)
                {b=new SimBullet{source=t,birth=t.start+instance.shift,index=i,count=points.Length,kinematic=true,templateOwner=instance.serial,generation=instance.generation,clockShift=instance.shift,life=t.end-t.start+1,collision=t.kind=="rod"};fixedBullets[key]=b;world.bullets.Add(b);}
                var p=instance.Point(points[i].X,points[i].Y);b.x=p.X;b.y=p.Y;b.scale=pose.scale;b.alpha=pose.alpha;b.color=t.rainbow?Color.White:Motion.ColorOf(t.color);b.visualAngle=t.kind=="object"?pose.angle+instance.rotation:Motion.Value(t,"spriteAngle",time)+(t.rotateSprite?pose.angle+instance.rotation:0);b.sequence=t.kind=="rod"?Rainbow.RodSequence(t,i,points.Length,time,points[i],pose,instance.rotation):0;b.count=points.Length;b.seenFrame=frame;world.TickNew(b,frame);
            }
        }
    }
    public bool FixedAlive(SimBullet b,int frame)
    {return b.seenFrame==frame&&Visible(b.source,b.source.kind=="rod"?b.index:-1,b.templateOwner);}
}
