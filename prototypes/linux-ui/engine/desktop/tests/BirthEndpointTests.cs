using System;
using System.Collections.Generic;
using System.Linq;
internal static class BirthEndpointTests
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Near(double actual,double expected,string message){Check(Math.Abs(actual-expected)<.00001,message+": "+actual+" != "+expected);}
    static void Main()
    {
        var t=new Track{id="endpointBirth",kind="emitter",enabled=true,start=10,end=10,count=3,speed=0,life=20,emissionShape="line",emissionFacing="fixed",x1=0,y1=50,x2=100,y2=50};
        t.expressions["x"]="(x1+x2)/2";t.expressions["y"]="(y1+y2)/2";t.expressions["emitWidth"]="point_distance(x1,y1,x2,y2)";
        t.keyframes.Add(new Keyframe{property="x1",frame=0,value=0});t.keyframes.Add(new Keyframe{property="x1",frame=10,value=200});
        t.keyframes.Add(new Keyframe{property="x2",frame=0,value=100});t.keyframes.Add(new Keyframe{property="x2",frame=10,value=400});
        var state=new EditorState();state.tracks.Add(t);Motion.Normalize(state);
        var context=ExpressionContext.For(t,10);context.sampleRandom=true;context.randomFrame=0;
        var pose=Motion.SampleAt(t,10,context);Near(pose.x,300,"Birth center");Near(context.x1,200,"Sampled x1");Near(context.x2,400,"Sampled x2");Near(Motion.SampleValue(t,"emitWidth",10,context),200,"Sampled width");
        var world=new BulletWorld(state);world.Seek(10);Check(world.bullets.Count==3,"Line member count");
        for(int i=0;i<3;i++){Near(world.bullets[i].x,200+100*i,"World line member X");Near(world.bullets[i].y,50,"World line member Y");}
        t.keyframes.Clear();t.x1=7;t.expressions["x1"]="200+i*10+volley+random_range(0,0)";t.expressions["x2"]="x1+100";t.expressions["scale"]="random_range(1,2)";Motion.Normalize(state);
        context=ExpressionContext.For(t,10);context.i=2;context.n=3;context.volley=5;context.sampleRandom=true;context.randomFrame=0;context.randomOffset=23456;context.randomMix=true;
        var raw=context.Clone();double expectedScale=RandomExpression.Evaluate(t.expressions["scale"],t.expressionSeed,t.randomId+"|scale|base",raw);
        pose=Motion.SampleAt(t,10,context);Near(context.x1,225,"Member and volley preserved");Near(context.x2,107,"Endpoint expressions use original raw endpoints");Near(pose.x,166,"Indexed center");Near(pose.scale,expectedScale,"Random birth seed unchanged");
        Check(context.i==2&&context.n==3&&context.volley==5&&context.randomFrame==0&&context.randomOffset==23456&&context.randomMix&&context.sampleRandom,"Context metadata preserved");
        Console.WriteLine("PASS: animated emitter endpoints, birth geometry order, per-member context and seeded random sampling");
    }
}
