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
    public string scopeGroup="";public int serial,birth,generation,shift,lastFrame=-1;public double x,y,sourceX,sourceY,scaleMultiplier=1,anchorX,anchorY;private double rotationValue,rotationCos=1,rotationSin;public double rotation {get{return rotationValue;}set{rotationValue=value;double a=value*Math.PI/180;rotationCos=Math.Cos(a);rotationSin=Math.Sin(a);}}public BirthAppearance appearance;public Color? colorOverride;public TemplateDefinition definition;
    public string linkKey;public int linkedOwner;public Track linkedSource;public BulletAction linkedAction;public CopyPlacement placement;public RoomData room;public TemplateInstance sourceInstance;public bool ended;
    public TemplateInstance Clone(){return (TemplateInstance)MemberwiseClone();}
    public PointF Point(double px,double py)
    {double dx=px-anchorX,dy=py-anchorY;var p=new PointF((float)(x+dx*rotationCos+dy*rotationSin),(float)(y-dx*rotationSin+dy*rotationCos));return linkedAction!=null&&linkedAction.copyMode=="grid"?CopyGeneration.GridPoint(linkedAction,placement,room,p.X,p.Y):p;}
    public bool OmitPoint(double px,double py,PointF result)
    {if(linkedAction==null||linkedAction.copyMode!="grid"||linkedAction.copyCenter)return false;var original=sourceInstance==null?new PointF((float)px,(float)py):sourceInstance.Point(px,py);return Math.Abs(original.X-result.X)<.01&&Math.Abs(original.Y-result.Y)<.01;}
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
    private sealed class Geometry {public int time,shift;public Keyframe pose;public PointF[] points;}
    private readonly Dictionary<Track,Geometry> geometry=new Dictionary<Track,Geometry>();private int geometryFrame=-1;
    private Geometry ShapeAt(Track track,int time,int frame){if(geometryFrame!=frame){geometry.Clear();geometryFrame=frame;}Geometry value;if(geometry.TryGetValue(track,out value)&&value.time==time&&value.shift==track.expressionWorldShift)return value;var pose=Motion.At(track,time);value=new Geometry{time=time,shift=track.expressionWorldShift,pose=pose,points=track.kind=="rod"?Motion.RodPoints(track,time):new[]{new PointF((float)pose.x,(float)pose.y)}};geometry[track]=value;return value;}
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
    internal void Hide(Track track,int index,int owner){hidden.Add(Key(owner,track,index));}
    public void DestroyMember(SimBullet b){if(b.kinematic)Hide(b.source,b.index,b.templateOwner);b.dead=true;}
    public void ClearDanmaku(int frame)
    {
        foreach(var t in world.State.tracks.Where(t=>(t.kind=="rod"||t.kind=="object")&&BulletSystem.Active(t,frame)))Hide(t,-1,0);
        foreach(var instance in instances)foreach(var t in instance.definition.scene.tracks.Where(t=>(t.kind=="rod"||t.kind=="object")&&BulletSystem.Active(t,frame-instance.shift)))Hide(t,-1,instance.serial);
    }
    public void ClearGroup(int frame,string group)
    {foreach(var t in world.State.tracks.Where(t=>t.kind=="rod"&&BulletSystem.Active(t,frame)&&TimelineGroups.Id(t)==group))Hide(t,-1,0);foreach(var instance in instances.Where(i=>i.scopeGroup==group))foreach(var t in instance.definition.scene.tracks.Where(t=>t.kind=="rod"&&BulletSystem.Active(t,frame-instance.shift)))Hide(t,-1,instance.serial);}
    public void ResetRods(int frame,Track e)
    {
        foreach(var t in world.State.tracks.Where(t=>t.kind=="rod"&&BulletSystem.Active(t,frame)&&(t.releaseFrame<0||frame<t.releaseFrame)&&Visible(t)&&EventPresets.Matches(e,new SimBullet{scopeGroup=TimelineGroups.Id(t),source=t})).ToArray())
        {
            var points=Motion.RodPoints(t,frame);
            var p=Motion.At(t,frame);
            var player=BulletSystem.PlayerAt(world.State,frame);
            for(int i=0;i<points.Length;i++)
            {
                if(!Visible(t,i))continue;
                double d=t.releaseDirection=="aim"?ExpressionContext.Direction(points[i].X,points[i].Y,player.x,player.y):t.releaseDirection=="fixed"?p.angle:ExpressionContext.Direction(p.x,p.y,points[i].X,points[i].Y)+(t.releaseDirection=="tangent"?90:0);
                var b=world.Spawn(t,frame,i,points.Length,0,points[i],d,Motion.Value(t,"speed",frame),0,false,0,null,new PointF((float)p.x,(float)p.y));
                if(t.rainbow&&Rainbow.Mode(t)!="sequence")b.sequence=Rainbow.RodSequence(t,i,points.Length,frame,points[i],p);
                b.motionReset=e.eventDisableMotion;
            }
            Hide(t,-1,0);
        }
        foreach(var instance in instances.ToArray())
        {
            if(!EventPresets.Matches(e,new SimBullet{scopeGroup=instance.scopeGroup}))continue;
            int time=frame-instance.shift;
            var scene=instance.definition.scene;
            bool affected=false;
            foreach(var t in scene.tracks.Where(t=>(t.kind=="rod"||t.kind=="object")&&BulletSystem.Active(t,time)&&(t.releaseFrame<0||time<t.releaseFrame)&&Visible(t,-1,instance.serial)).ToArray())
            {
                var shape=ShapeAt(t,time,frame);
                var points=shape.points;
                for(int i=0;i<points.Length;i++)
                {
                    if(!Visible(t,t.kind=="rod"?i:-1,instance.serial))continue;
                    string key=Key(instance.serial,t,i);
                    SimBullet b;
                    if(fixedBullets.TryGetValue(key,out b)&&!b.dead)
                    {
                        b.frozenImage=b.frozenImage>=0?b.frozenImage:Rainbow.Index(t,b.sequence,time,time);
                        fixedBullets.Remove(key);b.kinematic=false;b.behavior=t;b.templateOwner=0;
                        b.birth=frame;b.life=Math.Max(1,(int)Motion.Value(t,"life",time));b.lastUpdate=frame-1;b.deathStart=-1;b.deathFactor=1;b.motionReset=e.eventDisableMotion;b.detachedAppearance=true;b.baseAlpha=b.alpha;b.baseScale=b.scale;b.history.Clear();
                        affected=true;
                    }
                }
                Hide(t,-1,instance.serial);
            }
            if(affected)instance.ended=true;
        }
        instances.RemoveAll(t=>t.ended);
    }
    private TemplateDefinition Definition(string id,bool linked=false,Track context=null,int owner=0)
    {
        string cacheKey=(linked?"linked|"+owner+"|":"")+id;TemplateDefinition found;if(definitions.TryGetValue(cacheKey,out found))return found;
        var source=context??world.State.tracks.FirstOrDefault(t=>t.id==id);if(!Eligible(source))return null;
        var selected=new HashSet<string>{id};bool more;
        do{more=false;foreach(var t in world.State.tracks)if(selected.Contains(t.parentId))more|=selected.Add(t.id);}while(more);
        var tracks=world.State.tracks.Where(t=>selected.Contains(t.id)).Select(t=>t.Snapshot()).ToList();var root=tracks.First(t=>t.id==id);root.parentId="";root.enabled=true;root.templateOnly=false;
        var scene=new EditorState{timelineGroups=world.State.timelineGroups.Select(g=>g.Snapshot()).ToList(),preserveTrackOrder=world.State.preserveTrackOrder,globalSource=world.State,fps=world.State.fps,duration=world.State.duration,randomSeed=world.State.randomSeed,nativeTest=world.State.nativeTest,usePlayerTrace=world.State.usePlayerTrace,playerTrace=world.State.playerTrace,tracks=tracks};Motion.Normalize(scene);
        if(linked)root.parent=source.parent;
        found=new TemplateDefinition{scene=scene,root=root,anchor=Motion.At(root,root.start)};definitions[cacheKey]=found;return found;
    }
    public TemplateSnapshot Capture(){return new TemplateSnapshot{serial=serial,instances=instances.Select(t=>t.Clone()).ToList(),hidden=new HashSet<string>(hidden)};}
    public void Restore(TemplateSnapshot snapshot)
    {
        geometry.Clear();geometryFrame=-1;
        instances.Clear();hidden=snapshot==null?new HashSet<string>():new HashSet<string>(snapshot.hidden);serial=snapshot==null?0:snapshot.serial;
        if(snapshot!=null)instances.AddRange(snapshot.instances.Select(t=>t.Clone()));fixedBullets.Clear();foreach(var b in world.bullets.Where(b=>b.kinematic))fixedBullets[Key(b.templateOwner,b.source,b.index)]=b;
    }
    private bool Generate(BulletAction action,int frame,double x,double y,double heading,int generation,double sourceX,double sourceY,int index,Track source,int age,BirthAppearance appearance,TemplateInstance owner=null,bool trackAction=false,string scope=null)
    {
        scope=scope??(owner==null?TimelineGroups.Id(source):owner.scopeGroup);
        if(action.generationPreset!=CopyGeneration.Key)return GenerateOne(action,frame,x,y,heading,generation,sourceX,sourceY,index,source,age,appearance,null,scope);
        if(action.copyLinked&&trackAction){
            string key=(owner==null?0:owner.serial)+"|"+source.id+"|"+source.spawnActions.IndexOf(action)+"|"+index;if(instances.Any(t=>t.linkKey==key))return false;
            var definition=Definition(source.id,true,source,owner==null?0:owner.serial);if(definition==null)return false;
            foreach(var p in CopyGeneration.LivePlacements(action,world.Room)){
                var instance=new TemplateInstance{scopeGroup=scope,serial=++serial,birth=frame,generation=generation+1,definition=definition,linkedSource=source,linkedOwner=owner==null?0:owner.serial,linkedAction=action,placement=p,room=world.Room,linkKey=key};instances.Add(instance);StepInstance(instance,frame);
            }return true;
        }
        bool generated=false;foreach(var copy in CopyGeneration.Placements(action))generated|=GenerateOne(action,frame,x,y,heading,generation,sourceX,sourceY,index,source,age,appearance,copy,scope);return generated;
    }
    private bool GenerateOne(BulletAction action,int frame,double x,double y,double heading,int generation,double sourceX,double sourceY,int index,Track source,int age,BirthAppearance appearance,CopyPlacement copy,string scope)
    {
        var definition=Definition(action.templateId);if(definition==null)return false;
        var context=ExpressionContext.For(source,frame,true);context.age=age;context.i=index;
        double rotation=copy==null?(action.inheritAngle?heading:0)+GenerationPresets.Phase(action,source,age,index,frame)+index*ScalarExpressions.ActionValue(source,action,"spawnAngleStep",context):CopyGeneration.Rotation(action,copy,heading,definition.anchor.angle,source,frame,true),a=rotation*Math.PI/180;
        if(copy!=null){x+=copy.x;y+=copy.y;}
        if(action.spawnAtWorld){x=0;y=0;}
        double offsetX=ScalarExpressions.ActionValue(source,action,"spawnOffsetX",context),offsetY=ScalarExpressions.ActionValue(source,action,"spawnOffsetY",context);
        var instance=new TemplateInstance{scopeGroup=scope,serial=++serial,birth=frame,shift=frame-definition.root.start,generation=generation+1,x=x+offsetX*Math.Cos(a)+offsetY*Math.Sin(a),y=y-offsetX*Math.Sin(a)+offsetY*Math.Cos(a),rotation=rotation,sourceX=sourceX,sourceY=sourceY,definition=definition,anchorX=definition.anchor.x,anchorY=definition.anchor.y};
        if(action.inheritAppearance)instance.appearance=appearance.Clone();
        if(action.inheritScale)instance.scaleMultiplier=appearance.scale/Math.Max(.000001,definition.anchor.scale);
        if(!String.IsNullOrWhiteSpace(action.spawnColorExpression)){int at=age-action.start-index*action.stagger,wave=action.spawnRepeat>0?at/action.spawnRepeat:0;instance.colorOverride=SpawnColors.Evaluate(action.spawnColorExpression,source,"spawnColor"+source.spawnActions.Concat(source.bulletActions).ToList().IndexOf(action),frame,wave);}
        if(copy!=null){var tint=CopyGeneration.Tint(action,copy);if(tint.HasValue)instance.colorOverride=tint;if(source.rainbow&&action.copyColorMode=="inherit")instance.appearance=null;
            if(source.rainbow&&action.copyColorMode=="rainbow"){instance.appearance=appearance.Clone();instance.appearance.color=Color.White;double hue=action.copyHue+360*copy.index/(double)Math.Max(1,copy.count)*action.copyColorDensity;instance.appearance.image=(int)Math.Floor(((hue%360+360)%360)*32/360);instance.colorOverride=Color.White;}}
        instances.Add(instance);StepInstance(instance,frame);return true;
    }
    public void FromBullet(SimBullet bullet,int frame)
    {
        if(bullet.behavior==null)return;
        bool destroy=false;
        foreach(var a in bullet.behavior.bulletActions.Where(a=>a.type=="spawn"))if(Trigger(a,frame-bullet.birth,bullet.index,bullet.generation))
            if(Generate(a,frame,bullet.x,bullet.y,bullet.direction,bullet.generation,bullet.x,bullet.y,bullet.index,bullet.behavior,frame-bullet.birth,BirthAppearance.BulletAt(bullet,frame),null,false,bullet.scopeGroup))destroy|=a.destroyParent;
        if(destroy)bullet.dead=true;
    }
    private void TrackActions(Track track,int time,int frame,int owner,int generation,TemplateInstance instance)
    {
        if(track.spawnActions.Count==0||!Visible(track,-1,owner))return;
        var pose=Motion.At(track,time);var center=instance==null?new PointF((float)pose.x,(float)pose.y):instance.Point(pose.x,pose.y);
        foreach(var action in track.spawnActions)
        {
            if(instance!=null&&instance.linkedAction!=null&&action.generationPreset==CopyGeneration.Key&&action.copyLinked)continue;
            bool each=action.spawnOrigin=="each"&&track.kind=="rod"&&!(action.generationPreset==CopyGeneration.Key&&action.copyLinked);var points=each?Motion.RodPoints(track,time):new[]{new PointF((float)pose.x,(float)pose.y)};
            for(int i=0;i<points.Length;i++)
            {
                int member=each?i:-1;if(!Visible(track,member,owner)||!Trigger(action,time-TimelineLayout.ClockStart(track),i,generation))continue;
                var p=points[i];double heading=each?ExpressionContext.Direction(pose.x,pose.y,p.X,p.Y):pose.angle;var look=BirthAppearance.TrackAt(track,time,i,p,pose,instance==null?0:instance.rotation,points.Length);
                if(instance!=null){look.scale*=instance.scaleMultiplier;if(instance.appearance!=null){look.sprite=instance.appearance.sprite;look.image=instance.appearance.image;look.color=instance.appearance.color;}if(instance.colorOverride.HasValue)look.color=instance.colorOverride.Value;}
                if(instance!=null){p=instance.Point(p.X,p.Y);heading+=instance.rotation;}
                if(Generate(action,frame,p.X,p.Y,heading,generation,center.X,center.Y,i,track,time-TimelineLayout.ClockStart(track),look,instance,true)&&action.destroyParent&&!(action.generationPreset==CopyGeneration.Key&&action.copyLinked))Hide(track,member,owner);
            }
        }
    }
    public void Step(int frame)
    {
        foreach(var t in world.State.tracks.Where(t=>Eligible(t)&&BulletSystem.Active(t,frame)))TrackActions(t,frame,frame,0,0,null);
        foreach(var instance in instances.ToArray())StepInstance(instance,frame);
        instances.RemoveAll(t=>t.ended||frame-t.shift>t.definition.root.end);
        foreach(var key in fixedBullets.Where(p=>p.Value.dead).Select(p=>p.Key).ToArray())fixedBullets.Remove(key);
    }
    private void StepInstance(TemplateInstance instance,int frame)
    {
        if(instance.lastFrame==frame)return;instance.lastFrame=frame;
        if(instance.linkedAction!=null){
            var owner=instance.linkedOwner==0?null:instances.FirstOrDefault(t=>t.serial==instance.linkedOwner);instance.sourceInstance=owner;
            instance.shift=owner==null?0:owner.shift;int sourceTime=frame-instance.shift;
            if(instance.linkedOwner!=0&&(owner==null||owner.ended)||!BulletSystem.Active(instance.linkedSource,sourceTime)||!Visible(instance.linkedSource,-1,instance.linkedOwner)){instance.ended=true;return;}
            var pose=Motion.At(instance.linkedSource,sourceTime);var center=owner==null?new PointF((float)pose.x,(float)pose.y):owner.Point(pose.x,pose.y);double heading=pose.angle+(owner==null?0:owner.rotation);
            instance.anchorX=pose.x;instance.anchorY=pose.y;instance.rotation=CopyGeneration.Rotation(instance.linkedAction,instance.placement,heading,pose.angle,instance.linkedSource,sourceTime);
            double r=instance.rotation*Math.PI/180;bool grid=instance.linkedAction.copyMode=="grid";instance.x=center.X+(grid?0:instance.placement.x)+instance.linkedAction.spawnOffsetX*Math.Cos(r)+instance.linkedAction.spawnOffsetY*Math.Sin(r);instance.y=center.Y+(grid?0:instance.placement.y)-instance.linkedAction.spawnOffsetX*Math.Sin(r)+instance.linkedAction.spawnOffsetY*Math.Cos(r);instance.sourceX=center.X;instance.sourceY=center.Y;instance.scaleMultiplier=owner==null?1:owner.scaleMultiplier;
            instance.colorOverride=CopyGeneration.Tint(instance.linkedAction,instance.placement);
            if(instance.linkedSource.rainbow&&instance.linkedAction.copyColorMode=="rainbow") {var look=BirthAppearance.TrackAt(instance.linkedSource,sourceTime,0,center,pose,0,1);look.color=Color.White;double hue=instance.linkedAction.copyHue+360*instance.placement.index/(double)Math.Max(1,instance.placement.count)*instance.linkedAction.copyColorDensity;look.image=(int)Math.Floor(((hue%360+360)%360)*32/360);instance.appearance=look;instance.colorOverride=Color.White;}
        }
        int time=frame-instance.shift;var scene=instance.definition.scene;foreach(var child in scene.tracks)child.expressionWorldShift=instance.shift;
        if(world.ClearDanmakuThisFrame)foreach(var t in scene.tracks.Where(t=>(t.kind=="rod"||t.kind=="object")&&BulletSystem.Active(t,time)))Hide(t,-1,instance.serial);
        foreach(var t in scene.tracks.Where(t=>Eligible(t)&&BulletSystem.Active(t,time)))TrackActions(t,time,frame,instance.serial,instance.generation,instance);
        foreach(var t in scene.tracks.Where(t=>Eligible(t)&&BulletSystem.Active(t,time)&&Visible(t,-1,instance.serial)))
        {
            if(t.kind=="emitter")
            {
                int dt=time-TimelineLayout.ClockStart(t);if(dt%Math.Max(1,t.interval)!=0)continue;int n=Math.Max(1,Math.Min(300,BulletSystem.Round(Motion.Value(t,"count",time))));
                for(int i=0;i<n;i++){var b=world.Spawn(t,frame+i*Math.Max(0,t.indexDelay),i,n,dt/Math.Max(1,t.interval),null,null,null,instance.generation,false,instance.shift,instance);world.TickNew(b,frame);}continue;
            }
            if(t.kind!="rod"&&t.kind!="object")continue;
            var shape=ShapeAt(t,time,frame);var pose=shape.pose;var points=shape.points;
            if(t.kind=="rod"&&t.releaseFrame>=0&&time>=t.releaseFrame)
            {
                if(time==t.releaseFrame)for(int i=0;i<points.Length;i++)if(Visible(t,i,instance.serial))
                {
                    var p=instance.Point(points[i].X,points[i].Y);if(instance.OmitPoint(points[i].X,points[i].Y,p))continue;var center=instance.Point(pose.x,pose.y);
                    var player=BulletSystem.PlayerAt(world.State,frame);double direction=t.releaseDirection=="fixed"?pose.angle+instance.rotation:t.releaseDirection=="aim"?ExpressionContext.Direction(p.X,p.Y,player.x,player.y):ExpressionContext.Direction(pose.x,pose.y,points[i].X,points[i].Y)+instance.rotation+(t.releaseDirection=="tangent"?90:0);
                    var b=world.Spawn(t,frame,i,points.Length,0,p,direction,Motion.Value(t,"speed",time),instance.generation,false,instance.shift,instance,center);if(t.rainbow&&Rainbow.Mode(t)!="sequence")b.sequence=Rainbow.RodSequence(t,i,points.Length,time,points[i],pose,instance.rotation);world.TickNew(b,frame);
                }continue;
            }
            for(int i=0;i<points.Length;i++)
            {
                if(!Visible(t,t.kind=="rod"?i:-1,instance.serial))continue;string key=Key(instance.serial,t,i);SimBullet b;
                var p=instance.Point(points[i].X,points[i].Y);
                if(instance.OmitPoint(points[i].X,points[i].Y,p))
                {
                    SimBullet old;if(fixedBullets.TryGetValue(key,out old))old.dead=true;
                    continue;
                }
                if(!fixedBullets.TryGetValue(key,out b)||b.dead)
                {b=new SimBullet{scopeGroup=instance.scopeGroup,source=t,birth=t.start+instance.shift,index=i,count=points.Length,kinematic=true,templateOwner=instance.serial,generation=instance.generation,clockShift=instance.shift,life=t.end-t.start+1,collision=t.kind=="rod"};fixedBullets[key]=b;world.bullets.Add(b);}
                var player=BulletSystem.PlayerAt(world.State,frame);
                double heading=t.kind=="rod"?(t.releaseDirection=="fixed"?pose.angle+instance.rotation:t.releaseDirection=="aim"?ExpressionContext.Direction(p.X,p.Y,player.x,player.y):ExpressionContext.Direction(pose.x,pose.y,points[i].X,points[i].Y)+instance.rotation+(t.releaseDirection=="tangent"?90:0)):pose.angle+instance.rotation;
                double speed=Motion.Value(t,"speed",time);
                b.sourceX=instance.sourceX;b.sourceY=instance.sourceY;b.x=p.X;b.y=p.Y;b.scale=pose.scale*instance.scaleMultiplier;b.alpha=pose.alpha;b.color=t.rainbow?Color.White:Motion.ColorOf(t.color);b.visualAngle=t.kind=="object"?pose.angle+instance.rotation:Motion.Value(t,"spriteAngle",time)+(t.rotateSprite?pose.angle+instance.rotation:0);b.sequence=t.kind=="rod"?Rainbow.RodSequence(t,i,points.Length,time,points[i],pose,instance.rotation):0;b.count=points.Length;b.seenFrame=frame;b.baseSpeed=speed;b.speed=speed;b.baseDirection=heading;b.direction=heading;Inherit(b,instance);world.TickNew(b,frame);
            }
        }
    }
    public PointF SampleMember(SimBullet b,int frame){var instance=instances.FirstOrDefault(i=>i.serial==b.templateOwner);if(instance==null)return new PointF((float)b.x,(float)b.y);int time=frame-instance.shift;var points=Motion.RodPoints(b.source,time);return b.index<points.Length?instance.Point(points[b.index].X,points[b.index].Y):new PointF((float)b.x,(float)b.y);}
    public bool FixedAlive(SimBullet b,int frame)
    {return b.seenFrame==frame&&Visible(b.source,b.source.kind=="rod"?b.index:-1,b.templateOwner);}
    public static void Inherit(SimBullet b,TemplateInstance instance)
    {if(instance==null)return;if(instance.appearance!=null){b.sprite=instance.appearance.sprite;b.frozenImage=instance.appearance.image;b.color=instance.appearance.color;}if(instance.colorOverride.HasValue)b.color=instance.colorOverride.Value;}
}
