using System;
using System.Drawing;

internal static class Rainbow
{
    public const string Sprite="sprrainbowcherry";
    public static readonly string[] Parameters={"rainbowDensity","rainbowOffset","rainbowSpeed"};
    public static readonly string[] Modes={"sequence","distance","angle"};
    public static readonly string[] ModeNames={"原有顺序","距中心点距离","中心点角度"};
    public static string Mode(Track t){return Array.IndexOf(Modes,t.rainbowMode)<0?"sequence":t.rainbowMode;}
    public static bool IsCherry(Track t){return t.sprite!=null&&t.sprite.IndexOf("cherry",StringComparison.OrdinalIgnoreCase)>=0;}
    public static void Enable(Track t,bool enabled)
    {
        if(enabled&&!t.rainbow){t.rainbowSourceSprite=t.sprite;t.rainbowSourceColor=t.color;}
        t.rainbow=enabled;
        if(enabled){t.sprite=String.IsNullOrEmpty(t.rainbowSprite)?Sprite:t.rainbowSprite;t.color="#FFFFFF";}
        else {if(!String.IsNullOrEmpty(t.rainbowSourceSprite))t.sprite=t.rainbowSourceSprite;if(!String.IsNullOrEmpty(t.rainbowSourceColor))t.color=t.rainbowSourceColor;}
    }
    public static void SetSprite(Track t,string sprite){if(t.rainbow&&sprite!=(String.IsNullOrEmpty(t.rainbowSprite)?Sprite:t.rainbowSprite))Enable(t,false);t.sprite=sprite;}
    public static double RodSequence(Track t,int index,int count,double frame,PointF? point=null,Keyframe center=null,double rotation=0,double? rollOffset=null)
    {
        if(t.rainbowRandom)return index;
        string mode=Mode(t);
        if(mode!="sequence")
        {
            var p=point??Motion.RodPoints(t,frame)[index];var c=center??Motion.At(t,frame);
            // PointF preview coordinates must not flicker across integer color boundaries.
            if(mode=="distance"){double dx=p.X-c.x,dy=p.Y-c.y;return Math.Round(Math.Sqrt(dx*dx+dy*dy),4,MidpointRounding.AwayFromZero);}
            double angle=ExpressionContext.Direction(c.x,c.y,p.X,p.Y)+rotation;
            return Math.Round(((angle%360+360)%360)*32/360,4,MidpointRounding.AwayFromZero);
        }
        if(t.shape!="rod"||t.rodMode!="center")return index;
        double u=count<=1?.5:t.rolling?index/(double)count:index/(double)(count-1);
        if(t.rolling){u+=rollOffset??RollingOffset(t,frame);u-=Math.Floor(u);}
        return Math.Abs(u-.5)*(count-1);
    }
    private static double RollingOffset(Track t,double frame)
    {
        var ends=Motion.Endpoints(t,frame);double dx=ends[1].X-ends[0].X,dy=ends[1].Y-ends[0].Y,len=Math.Sqrt(dx*dx+dy*dy);
        return len>.001?(frame-TimelineLayout.ClockStart(t))*Motion.Value(t,"rollSpeed",frame)/len:0;
    }
    internal static void FormationImages(Track t,int frame,PointF[] points,Keyframe pose,int[] images)
    {
        if(!t.rainbow){
            int image=ObjectDynamics.TrackImage(t,frame,frame);
            for(int k=0;k<images.Length;k++)images[k]=image;
            return;
        }
        double offset=Motion.Value(t,"rainbowOffset",frame),density=Motion.Value(t,"rainbowDensity",frame);
        double motion=t.rainbowDynamic?(frame-TimelineLayout.ClockStart(t))*Motion.Value(t,"rainbowSpeed",frame):0;
        double roll=t.rolling&&t.shape=="rod"&&t.rodMode=="center"&&!t.rainbowRandom&&Mode(t)=="sequence"?RollingOffset(t,frame):0;
        for(int k=0;k<images.Length;k++){
            double sequence=RodSequence(t,k,images.Length,frame,points[k],pose,0,roll);
            images[k]=PhaseIndex(t,sequence,offset,density,motion);
        }
    }
    public static double SampleSequence(Track t,ExpressionContext context)
    {var c=context.Clone();c.randomMix=true;c.randomFrame=0;return Math.Floor(RandomExpression.Evaluate("random(32)",t.expressionSeed,(t.randomId??t.id)+"|rainbowColor|base",c));}
    public static int Index(Track t,double sequence,double time,double sampleTime=-1)
    {
        if(sampleTime<0)sampleTime=time;
        if(!t.rainbow)return ObjectDynamics.TrackImage(t,time,sampleTime);
        double offset=Motion.Value(t,"rainbowOffset",sampleTime),density=Motion.Value(t,"rainbowDensity",sampleTime);
        double motion=t.rainbowDynamic?(time-TimelineLayout.ClockStart(t))*Motion.Value(t,"rainbowSpeed",sampleTime):0;
        return PhaseIndex(t,sequence,offset,density,motion);
    }
    private static int PhaseIndex(Track t,double sequence,double offset,double density,double motion)
    {
        if(t.rainbowRandom&&t.kind!="emitter")sequence=SampleSequence(t,new ExpressionContext{randomOffset=sequence*9176});
        double phase=offset+sequence*density+motion;
        if(t.rainbowReverse)phase=-phase;
        return (int)((Math.Floor(phase)%32+32)%32);
    }
}
