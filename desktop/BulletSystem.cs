using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

// The editor and exported runner use the same order: spawn, age actions, global commands, move, split.
internal static class BulletSystem
{
    public static int Round(double value){return (int)Math.Round(value,MidpointRounding.AwayFromZero);}
    public static readonly string[] ActionTypes={"speed","direction","scale","alpha","turn","acceleration","aim","home","collision","clear","split","spawn"};
    public static readonly string[] ActionNames={"弹速","方向","大小","不透明度","每帧转向","每帧加速","瞄准一次","限速追踪","碰撞开关","销毁","分裂 / 子发射器","生成模板"};
    public static readonly string[] Shapes={"point","line","ring","arc","ellipse","sine","bezier"};
    public static readonly string[] ShapeNames={"点发射 / 扇形","直线阵列","圆环阵列","圆弧阵列","椭圆阵列","正弦曲线","贝塞尔曲线"};
    public static string ActionName(string type){int i=Array.IndexOf(ActionTypes,type);return i<0?type:ActionNames[i];}
    public static List<Track> CloneTracks(IEnumerable<Track> source)
    {var copies=source.Select(t=>t.Snapshot()).ToList();var map=copies.Where(t=>!String.IsNullOrEmpty(t.id)).GroupBy(t=>t.id).ToDictionary(g=>g.Key,g=>Guid.NewGuid().ToString("N"));foreach(var t in copies){string id;t.id=map.TryGetValue(t.id??"",out id)?id:Guid.NewGuid().ToString("N");if(map.TryGetValue(t.parentId??"",out id))t.parentId=id;if(map.TryGetValue(t.targetSource??"",out id))t.targetSource=id;foreach(var a in TemplateRuntime.Actions(t))if(map.TryGetValue(a.templateId??"",out id))a.templateId=id;}return copies;}
    public static bool Advanced(EditorState state){return state.usePlayerTrace||state.tracks.Any(t=>t.spawnActions.Count>0||t.bulletActions.Any(a=>a.type=="spawn")||t.kind=="control"||t.kind=="pattern"||t.kind=="group"||t.kind=="rod"&&t.releaseFrame>=0||t.kind=="emitter"&&(t.advancedBullets||t.bulletActions.Count>0||t.emissionShape!="point"||t.indexDelay!=0||t.indexSpeed!=0||t.indexAngle!=0||t.indexScale!=0||t.expressions.Values.Any(v=>System.Text.RegularExpressions.Regex.IsMatch(v,@"\b(i|n|u|volley)\b"))));}
    public static NativeTestPosition PlayerAt(EditorState state,int frame)
    {
        var fallback=state.nativeTest??new NativeTestPosition();var trace=state.playerTrace;if(!state.usePlayerTrace||trace==null||trace.Count==0||frame<trace[0].frame||frame>trace[trace.Count-1].frame)return fallback;int lo=0,hi=trace.Count-1;while(lo<hi){int mid=(lo+hi+1)/2;if(trace[mid].frame<=frame)lo=mid;else hi=mid-1;}var a=trace[lo];if(lo==trace.Count-1)return new NativeTestPosition{x=a.x,y=a.y};var b=trace[lo+1];double u=(frame-a.frame)/(double)Math.Max(1,b.frame-a.frame);return new NativeTestPosition{x=a.x+(b.x-a.x)*u,y=a.y+(b.y-a.y)*u};
    }
    public static void Attach(EditorState state)
    {
        foreach(var t in state.tracks){t.owner=state;t.parent=null;t.bulletActions=t.bulletActions??new List<BulletAction>();t.spawnActions=t.spawnActions??new List<BulletAction>();t.curvePoints=t.curvePoints??new List<CurvePoint>();t.children=t.children??new List<Track>();}
        foreach(var t in state.tracks){if(String.IsNullOrEmpty(t.parentId))continue;var p=state.tracks.FirstOrDefault(g=>g.id==t.parentId&&g.kind=="group");var visited=new HashSet<string>{t.id};while(p!=null){if(!visited.Add(p.id))throw new FormatException("分组不能循环引用。");if(p.id==t.parentId)t.parent=p;p=state.tracks.FirstOrDefault(g=>g.id==p.parentId&&g.kind=="group");}}
    }
    public static bool Active(Track t,int frame){return t.enabled&&!t.templateOnly&&frame>=t.start&&frame<=t.end&&(t.parent==null||Active(t.parent,frame));}
    public static Keyframe Transform(Track parent,double frame,Keyframe p)
    {
        if(parent==null)return p;var q=Motion.At(parent,frame);double a=q.angle*Math.PI/180,x=p.x*q.scale,y=p.y*q.scale;
        return new Keyframe{x=q.x+x*Math.Cos(a)+y*Math.Sin(a),y=q.y-x*Math.Sin(a)+y*Math.Cos(a),angle=p.angle+q.angle,scale=p.scale*q.scale,alpha=p.alpha*q.alpha};
    }
    public static PointF[] TransformPoints(Track parent,double frame,PointF[] points)
    {return points.Select(p=>{var q=Transform(parent,frame,new Keyframe{x=p.X,y=p.Y});return new PointF((float)q.x,(float)q.y);}).ToArray();}
    public static PointF LocalPoint(Track t,double i,int n,double frame=0,ExpressionContext context=null)
    {
        Func<string,double> value=p=>context==null?Motion.Value(t,p,frame):Motion.SampleValue(t,p,frame,context);double width=value("emitWidth"),height=value("emitHeight"),arc=value("emitArc"),phase=value("emitPhase"),rotation=value("emitRotation"),waves=value("emitWaves");
        double u=n<=1?.5:i/(double)(n-1),x=0,y=0;
        if(t.emissionShape=="line"){x=(u-.5)*width;}
        else if(t.emissionShape=="sine"){x=(u-.5)*width;y=Math.Sin(u*waves*2*Math.PI+phase*Math.PI/180)*height/2;}
        else if(t.emissionShape=="bezier"){
            var points=t.curvePoints.Count==4?t.curvePoints:new List<CurvePoint>{new CurvePoint{x=-120},new CurvePoint{x=-60,y=-120},new CurvePoint{x=60,y=120},new CurvePoint{x=120}};double v=1-u;
            x=v*v*v*points[0].x+3*v*v*u*points[1].x+3*v*u*u*points[2].x+u*u*u*points[3].x;y=v*v*v*points[0].y+3*v*v*u*points[1].y+3*v*u*u*points[2].y+u*u*u*points[3].y;
        }else if(t.emissionShape!="point"){
            double a=(phase+(t.emissionShape=="arc"?u*arc:i/(double)n*360))*Math.PI/180;x=Math.Cos(a)*width/2;y=-Math.Sin(a)*(t.emissionShape=="ellipse"?height:width)/2;
        }
        double r=rotation*Math.PI/180;return new PointF((float)(x*Math.Cos(r)+y*Math.Sin(r)),(float)(-x*Math.Sin(r)+y*Math.Cos(r)));
    }
    public static double TangentAngle(Track t,int i,int n,int frame,ExpressionContext context=null)
    {
        Func<string,double> value=p=>context==null?Motion.Value(t,p,frame):Motion.SampleValue(t,p,frame,context);double u=n<=1?.5:i/(double)(n-1),x=1,y=0,w=value("emitWidth"),h=value("emitHeight"),phase=value("emitPhase"),waves=value("emitWaves");
        if(t.emissionShape=="line")x=w;
        else if(t.emissionShape=="sine"){x=w;y=Math.Cos(u*waves*2*Math.PI+phase*Math.PI/180)*h/2*waves*2*Math.PI;}
        else if(t.emissionShape=="bezier"){var points=t.curvePoints.Count==4?t.curvePoints:new List<CurvePoint>{new CurvePoint{x=-120},new CurvePoint{x=-60,y=-120},new CurvePoint{x=60,y=120},new CurvePoint{x=120}};double v=1-u;x=3*v*v*(points[1].x-points[0].x)+6*v*u*(points[2].x-points[1].x)+3*u*u*(points[3].x-points[2].x);y=3*v*v*(points[1].y-points[0].y)+6*v*u*(points[2].y-points[1].y)+3*u*u*(points[3].y-points[2].y);}
        else if(t.emissionShape!="point"){double a=(phase+(t.emissionShape=="arc"?u*value("emitArc"):i/(double)n*360))*Math.PI/180;x=-w/2*Math.Sin(a);y=-(t.emissionShape=="ellipse"?h:w)/2*Math.Cos(a);}
        return ExpressionContext.Direction(0,0,x,y)+value("emitRotation");
    }
    public static EditorState Expand(EditorState state)
    {
        if(!state.tracks.Any(t=>t.kind=="pattern")){Attach(state);return state;}
        var result=new EditorState{nativeProject=state.nativeProject,fps=state.fps,duration=state.duration,randomSeed=state.randomSeed,nativeTest=state.nativeTest,usePlayerTrace=state.usePlayerTrace,playerTrace=state.playerTrace,tracks=new List<Track>()};
        foreach(var t in state.tracks){if(t.kind!="pattern"){result.tracks.Add(t.Snapshot());continue;}if(t.enabled)ExpandClip(result,t,0,1,"",0);}
        RandomParameters.Attach(result);Attach(result);return result;
    }
    private static void ExpandClip(EditorState result,Track clip,int offset,double rate,string prefix,int depth)
    {
        if(depth>8)throw new FormatException("片段嵌套不能超过 8 层。");int span=clip.children.Select(t=>t.end).DefaultIfEmpty(0).Max()+1;double scale=rate/Math.Max(.1,clip.timeScale);
        for(int repeat=0;repeat<Math.Max(1,clip.repeatCount);repeat++){
            string idPrefix=prefix+clip.id+"/"+repeat+"/";int origin=offset+(int)BulletSystem.Round((clip.start+repeat*(span/Math.Max(.1,clip.timeScale)+clip.repeatGap))*rate);
            foreach(var child in clip.children){if(child.kind=="pattern"){if(child.enabled)ExpandClip(result,child,origin,scale,idPrefix,depth+1);continue;}var t=child.Snapshot();string old=t.id;t.id=idPrefix+old;if(!String.IsNullOrEmpty(t.parentId))t.parentId=idPrefix+t.parentId;if(!String.IsNullOrEmpty(t.targetSource))t.targetSource=idPrefix+t.targetSource;
                foreach(var a in TemplateRuntime.Actions(t)){a.spawnRepeat=(int)Round(a.spawnRepeat*scale);if(!String.IsNullOrEmpty(a.templateId)&&clip.children.Any(c=>c.id==a.templateId))a.templateId=idPrefix+a.templateId;a.start=(int)BulletSystem.Round(a.start*scale);a.duration=(int)BulletSystem.Round(a.duration*scale);a.stagger=(int)BulletSystem.Round(a.stagger*scale);if(a.type=="speed"||a.type=="turn"||a.type=="home"){a.from/=scale;a.to/=scale;}if(a.type=="acceleration"){a.from/=scale*scale;a.to/=scale*scale;}a.childSpeed/=scale;}
                t.start=origin+(int)BulletSystem.Round(child.start*scale);t.end=Math.Min(result.duration,origin+(int)BulletSystem.Round(child.end*scale));if(t.start>result.duration)continue;
                foreach(var k in t.keyframes){k.frame=origin+(int)BulletSystem.Round(k.frame*scale);if(k.property=="speed"||k.property=="rollSpeed")k.value/=scale;if(k.property=="life")k.value*=scale;}
                t.interval=Math.Max(1,(int)BulletSystem.Round(t.interval*scale));t.life=Math.Max(1,(int)BulletSystem.Round(t.life*scale));t.homingDelay=(int)BulletSystem.Round(t.homingDelay*scale);t.indexDelay=(int)BulletSystem.Round(t.indexDelay*scale);t.speed/=scale;t.indexSpeed/=scale;t.turnRate/=scale;t.angularSpeed/=scale;t.velocityX/=scale;t.velocityY/=scale;t.accelerationX/=scale*scale;t.accelerationY/=scale*scale;t.orbitSpeed/=scale;if(t.releaseFrame>=0)t.releaseFrame=origin+(int)BulletSystem.Round(t.releaseFrame*scale);
                t.rollSpeed/=scale;t.rainbowSpeed/=scale;t.trailSpeed/=scale;t.trailScaleDecay/=scale;t.trailAlphaDecay/=scale;t.trailInterval=Math.Max(1,BulletSystem.Round(t.trailInterval*scale));t.trailLife=Math.Max(1,BulletSystem.Round(t.trailLife*scale));t.targetMinAge=BulletSystem.Round(t.targetMinAge*scale);t.targetMaxAge=BulletSystem.Round(t.targetMaxAge*scale);if(t.controlAction=="speed"||t.controlAction=="turn"||t.controlAction=="home"){t.controlFrom/=scale;t.controlValue/=scale;}if(t.controlAction=="acceleration"){t.controlFrom/=scale*scale;t.controlValue/=scale*scale;}
                result.tracks.Add(t);
            }
        }
    }
}

internal sealed class SimBullet
{
    public bool kinematic;public int templateOwner,clockShift,seenFrame=-1,lastUpdate=-1;public double visualAngle;
    public Track source,behavior;public int birth,index,count,volley,generation;public double x,y,direction,speed,scale,alpha,baseDirection,baseSpeed,baseScale,baseAlpha,turn,acceleration,sequence;public int life;public bool collision=true,dead;
    public Color color;public List<BulletSample> history=new List<BulletSample>();public HashSet<string> commands=new HashSet<string>();
    public BulletSample Sample(int frame){return new BulletSample{frame=frame,x=x,y=y,direction=kinematic?visualAngle:direction,scale=scale,alpha=alpha,sequence=sequence};}
    public SimBullet Clone(){var b=(SimBullet)MemberwiseClone();b.history=new List<BulletSample>();b.commands=new HashSet<string>(commands);return b;}
}
internal sealed class BulletSample {public int frame;public double x,y,direction,scale,alpha,sequence;}
internal sealed class BulletWorld
{
    public EditorState State;public int Frame=-1;public readonly List<SimBullet> bullets=new List<SimBullet>();
    private sealed class Checkpoint {public List<SimBullet> bullets;public TemplateSnapshot templates;}
    public readonly TemplateRuntime Templates;private List<Tuple<SimBullet,BulletAction>> pendingSplits;
    private readonly SortedDictionary<int,Checkpoint> checkpoints=new SortedDictionary<int,Checkpoint>();
    public BulletWorld(EditorState state){State=BulletSystem.Expand(state);Templates=new TemplateRuntime(this);}
    public void Seek(int target)
    {
        if(target<Frame){int tail=State.tracks.Where(t=>Trails.Mode(t)!="off").Select(t=>Math.Min(300,Trails.Life(t))).DefaultIfEmpty(0).Max();int at=checkpoints.Keys.Where(f=>f<=target-tail).DefaultIfEmpty(-1).Max();bullets.Clear();Frame=at;if(at>=0)bullets.AddRange(checkpoints[at].bullets.Select(b=>b.Clone()));Templates.Restore(at>=0?checkpoints[at].templates:null);}
        while(Frame<target){Step(++Frame);if(Frame%200==0){checkpoints[Frame]=new Checkpoint{bullets=bullets.Select(b=>b.Clone()).ToList(),templates=Templates.Capture()};while(checkpoints.Count>8)checkpoints.Remove(checkpoints.Keys.First());}}
    }
    internal SimBullet Spawn(Track t,int birth,int i,int n,int volley,PointF? origin=null,double? angle=null,double? speed=null,int generation=0,bool plain=false,int clockShift=0,TemplateInstance instance=null)
    {
        int sampleTime=birth-clockShift;var context=ExpressionContext.For(t,sampleTime);context.i=i;context.n=n;context.volley=volley;context.sampleRandom=true;var birthPlayer=BulletSystem.PlayerAt(State,birth);context.playerX=birthPlayer.x;context.playerY=birthPlayer.y;context.randomFrame=0;context.randomOffset=t.randomScope=="group"?0:volley*1009+(t.randomScope=="bullet"?i*9176:0);var p=Motion.SampleAt(t,sampleTime,context);var local=BulletSystem.LocalPoint(t,i,n,sampleTime,context);double tangentAngle=BulletSystem.TangentAngle(t,i,n,sampleTime,context);var pp=BulletSystem.Transform(t.parent,sampleTime,new Keyframe{x=local.X,y=local.Y,scale=1});
        if(t.parent!=null){var parent=Motion.At(t.parent,sampleTime);local=new PointF((float)(pp.x-parent.x),(float)(pp.y-parent.y));}
        double d=p.angle+(i-(n-1)/2.0)*Motion.SampleValue(t,"spread",sampleTime,context)+i*t.indexAngle;
        if(t.emissionFacing=="radial")d=p.angle+ExpressionContext.Direction(0,0,local.X,local.Y)-(t.parent==null?0:Motion.At(t.parent,sampleTime).angle);
        if(t.emissionFacing=="tangent")d=p.angle+tangentAngle;
        double x=origin.HasValue?origin.Value.X:p.x+local.X,y=origin.HasValue?origin.Value.Y:p.y+local.Y;
        if(instance!=null&&!origin.HasValue){var point=instance.Point(x,y);x=point.X;y=point.Y;d+=instance.rotation;}
        if(t.emissionFacing=="aim"){var player=BulletSystem.PlayerAt(State,birth);d=ExpressionContext.Direction(x,y,player.x,player.y)+p.angle+(instance==null?0:instance.rotation)-(t.parent==null?0:Motion.At(t.parent,sampleTime).angle);}
        if(t.emissionFacing=="fixed")d=p.angle+(instance==null?0:instance.rotation)+i*t.indexAngle;
        var b=new SimBullet{clockShift=clockShift,source=t,behavior=plain?null:t,birth=birth,index=i,count=n,volley=volley,generation=generation,x=x,y=y,direction=angle??d,speed=speed??(Motion.SampleValue(t,"speed",sampleTime,context)+i*t.indexSpeed),scale=p.scale*(1+i*t.indexScale),alpha=p.alpha,life=(int)Motion.SampleValue(t,"life",sampleTime,context),color=t.rainbow?Color.White:Motion.ColorOf(t.color),sequence=volley*n+i};
        b.baseDirection=b.direction;b.baseSpeed=b.speed;b.baseScale=b.scale;b.baseAlpha=b.alpha;bullets.Add(b);return b;
    }
    private ExpressionContext Context(SimBullet b,int frame)
    {var c=ExpressionContext.For(b.source,frame-b.clockShift);var player=BulletSystem.PlayerAt(State,frame);c.playerX=player.x;c.playerY=player.y;c.x=b.x;c.y=b.y;c.age=frame-b.birth;c.direction=b.direction;c.speed=b.speed;c.i=b.index;c.n=b.count;c.volley=b.volley;return c;}
    private double Expression(SimBullet b,int frame,string expression,string slot,double fallback,bool worldClock=false)
    {return String.IsNullOrWhiteSpace(expression)?fallback:RandomExpression.Evaluate(expression,b.source.expressionSeed,(b.source.randomId??b.source.id)+"|"+slot,ExpressionClock(b,frame,worldClock));}
    private ExpressionContext ExpressionClock(SimBullet b,int frame,bool global){var c=Context(b,frame);if(global)c.frame=frame;return c;}
    internal void TickNew(SimBullet b,int frame){TickBullet(b,frame,pendingSplits);}
    private static bool Match(Track e,SimBullet b,int frame)
    {int age=frame-b.birth;return (String.IsNullOrEmpty(e.targetSource)||e.targetSource==b.source.id)&&(String.IsNullOrEmpty(e.targetTag)||e.targetTag==b.source.tag)&&(e.targetVolley<0||e.targetVolley==b.volley)&&(e.targetParity=="all"||b.index%2==(e.targetParity=="even"?0:1))&&age>=e.targetMinAge&&age<=e.targetMaxAge;}
    private void Apply(SimBullet b,string type,double value)
    {var player=BulletSystem.PlayerAt(State,Frame);switch(type){case "speed":b.speed=value;break;case "direction":b.direction=value;break;case "scale":b.scale=Math.Max(0,value);break;case "alpha":b.alpha=Math.Max(0,Math.Min(1,value));break;case "turn":b.turn=value;break;case "acceleration":b.acceleration=value;break;case "aim":b.direction=ExpressionContext.Direction(b.x,b.y,player.x,player.y)+value;break;case "home":b.direction=ExpressionContext.Turn(b.direction,ExpressionContext.Direction(b.x,b.y,player.x,player.y),value);break;case "collision":b.collision=value>=.5;break;case "clear":b.dead=true;break;}}
    private void Step(int frame)
    {
        pendingSplits=new List<Tuple<SimBullet,BulletAction>>();Templates.Step(frame);
        foreach(var t in State.tracks.Where(t=>t.kind=="emitter"&&BulletSystem.Active(t,frame)&&Templates.Visible(t))){int dt=frame-t.start;if(dt%Math.Max(1,t.interval)!=0)continue;int n=Math.Max(1,Math.Min(300,(int)BulletSystem.Round(Motion.Value(t,"count",frame))));for(int i=0;i<n;i++)Spawn(t,frame+i*Math.Max(0,t.indexDelay),i,n,dt/Math.Max(1,t.interval));}
        foreach(var t in State.tracks.Where(t=>t.kind=="rod"&&t.releaseFrame==frame&&BulletSystem.Active(t,frame)&&Templates.Visible(t))){var points=Motion.RodPoints(t,frame);var p=Motion.At(t,frame);for(int i=0;i<points.Length;i++){if(!Templates.Visible(t,i))continue;double d=t.releaseDirection=="aim"?ExpressionContext.Direction(points[i].X,points[i].Y,t.expressionPlayerX,t.expressionPlayerY):t.releaseDirection=="fixed"?p.angle:ExpressionContext.Direction(p.x,p.y,points[i].X,points[i].Y)+(t.releaseDirection=="tangent"?90:0);var b=Spawn(t,frame,i,points.Length,0,points[i],d,Motion.Value(t,"speed",frame));if(t.rainbow&&Rainbow.Mode(t)!="sequence")b.sequence=Rainbow.RodSequence(t,i,points.Length,frame,points[i],p);}}
        var splits=pendingSplits;
        foreach(var b in bullets.ToArray())TickBullet(b,frame,splits);
        for(int splitIndex=0;splitIndex<splits.Count;splitIndex++){var pair=splits[splitIndex];var b=pair.Item1;var a=pair.Item2;if(b.dead)continue;var template=State.tracks.FirstOrDefault(t=>t.id==a.templateId&&t.kind=="emitter")??b.source;for(int i=0;i<a.count;i++){var child=Spawn(template,frame,i,a.count,b.volley,new PointF((float)b.x,(float)b.y),b.direction+(i-(a.count-1)/2.0)*a.spread,a.childSpeed,b.generation+1,String.IsNullOrEmpty(a.templateId),b.clockShift);if(String.IsNullOrEmpty(a.templateId)){child.color=b.color;child.scale=child.baseScale=b.scale;child.alpha=child.baseAlpha=b.alpha;}TickBullet(child,frame,splits);}}
        bullets.RemoveAll(b=>b.dead);
    }
    private void TickBullet(SimBullet b,int frame,List<Tuple<SimBullet,BulletAction>> splits)
    {

            if(b.lastUpdate==frame)return;b.lastUpdate=frame;
            int age=frame-b.birth;if(age<0)return;if(b.kinematic){if(!Templates.FixedAlive(b,frame)){b.dead=true;return;}b.history.Add(b.Sample(frame));if(b.history.Count>301)b.history.RemoveAt(0);return;}if(age>=b.life){b.dead=true;return;}
            if(b.behavior!=null){var t=b.behavior;if(age>t.homingDelay){if(t.flightMode=="aim")Apply(b,"aim",0);else if(t.flightMode=="turn")Apply(b,"home",t.turnRate);else if(t.flightMode=="expression")b.direction=Expression(b,frame,"("+t.flightExpression+")+0*frame","flight|base",b.direction);}
                foreach(var a in t.bulletActions.Where(a=>a.enabled&&a.type!="spawn")){int at=age-a.start-b.index*a.stagger;if(at<0||at>Math.Max(0,a.duration))continue;if(a.type=="aim"&&at!=0)continue;if(a.type=="split"){if(at==0&&b.generation<a.maxGeneration)splits.Add(Tuple.Create(b,a));continue;}if(a.type=="clear"){if(at==0)b.dead=true;continue;}double u=a.duration==0?1:Motion.Ease(a.easing,at/(double)a.duration),v=a.from+(a.to-a.from)*u;if(a.relative)v=a.type=="scale"?v*b.baseScale:a.type=="alpha"?v*b.baseAlpha:v+(a.type=="direction"?b.baseDirection:a.type=="speed"?b.baseSpeed:0);v=Expression(b,frame,a.expression,"action"+t.bulletActions.IndexOf(a)+"|base",v);Apply(b,a.type,v);}}
            foreach(var e in State.tracks.Where(t=>t.enabled&&t.kind=="control"&&frame>=t.start&&frame<=t.end)){
                if(!b.commands.Contains(e.id)){if(!Match(e,b,frame)||!e.includeFuture&&b.birth>e.start)continue;if(frame!=e.start&&!e.includeFuture)continue;b.commands.Add(e.id);}
                double u=e.end==e.start?1:Motion.Ease(e.controlEasing,(frame-e.start)/(double)(e.end-e.start));double v=Expression(b,frame,e.controlExpression,"control|"+e.id+"|base",e.controlFrom+(e.controlValue-e.controlFrom)*u,true);if(e.controlAction=="color")b.color=Motion.ColorOf(e.controlColor);else Apply(b,e.controlAction,v);
            }
            if(b.dead)return;
            if(age>0){b.speed+=b.acceleration;b.direction+=b.turn;b.x+=Math.Cos(b.direction*Math.PI/180)*b.speed;b.y-=Math.Sin(b.direction*Math.PI/180)*b.speed;}
            b.history.Add(b.Sample(frame));if(b.history.Count>301)b.history.RemoveAt(0);Templates.FromBullet(b,frame);
    }
    public void Draw(Graphics g,SpriteStore sprites,int pass,bool hitboxes)
    {
        foreach(var b in bullets){if(Frame<b.birth||b.dead)continue;var t=b.source;int image=Rainbow.Index(t,b.sequence,Frame-b.clockShift,(b.kinematic?Frame:b.birth)-b.clockShift);double angle=b.kinematic?b.visualAngle:Motion.Value(t,"spriteAngle",b.birth-b.clockShift)+(t.rotateSprite?b.direction:0);
            if(pass==0&&Trails.Mode(t)!="off")foreach(var p in b.history.Where(p=>(p.frame-b.birth)%Trails.Interval(t)==0&&Frame-p.frame<Trails.Life(t)))Trails.Draw(g,sprites,t,p.frame*301+b.index,Frame-p.frame,p.x,p.y,p.scale,b.kinematic?p.direction:Motion.Value(t,"spriteAngle",b.birth-b.clockShift)+(t.rotateSprite?p.direction:0),p.alpha,b.color,Rainbow.Index(t,p.sequence,p.frame-b.clockShift,(b.kinematic?p.frame:b.birth)-b.clockShift));
            if(pass==1){sprites.Draw(g,t.sprite,b.x,b.y,b.scale,angle,b.alpha,b.color,image);if(hitboxes)using(var pen=new Pen(b.collision?Color.Lime:Color.Gray)){var r=sprites.Bounds(t.sprite,b.x,b.y);g.DrawRectangle(pen,r.X,r.Y,r.Width,r.Height);}}
        }
    }
}
