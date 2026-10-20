using System;

internal static class Rainbow
{
    public const string Sprite="sprrainbowcherry";
    public static readonly string[] Parameters={"rainbowDensity","rainbowOffset","rainbowSpeed"};
    public static bool IsCherry(Track t){return t.sprite!=null&&t.sprite.IndexOf("cherry",StringComparison.OrdinalIgnoreCase)>=0;}
    public static void Enable(Track t,bool enabled)
    {
        if(enabled&&!t.rainbow){t.rainbowSourceSprite=t.sprite;t.rainbowSourceColor=t.color;}
        t.rainbow=enabled;
        if(enabled){t.sprite=String.IsNullOrEmpty(t.rainbowSprite)?Sprite:t.rainbowSprite;t.color="#FFFFFF";}
        else {if(!String.IsNullOrEmpty(t.rainbowSourceSprite))t.sprite=t.rainbowSourceSprite;if(!String.IsNullOrEmpty(t.rainbowSourceColor))t.color=t.rainbowSourceColor;}
    }
    public static void SetSprite(Track t,string sprite){if(t.rainbow&&sprite!=(String.IsNullOrEmpty(t.rainbowSprite)?Sprite:t.rainbowSprite))Enable(t,false);t.sprite=sprite;}
    public static double RodSequence(Track t,int index,int count,double frame)
    {
        if(t.shape!="rod"||t.rodMode!="center")return index;
        double u=count<=1?.5:t.rolling?index/(double)count:index/(double)(count-1);
        if(t.rolling){var ends=Motion.Endpoints(t,frame);double dx=ends[1].X-ends[0].X,dy=ends[1].Y-ends[0].Y,len=Math.Sqrt(dx*dx+dy*dy);if(len>.001)u+=(frame-t.start)*Motion.Value(t,"rollSpeed",frame)/len;u-=Math.Floor(u);}
        return Math.Abs(u-.5)*(count-1);
    }
    public static int Index(Track t,double sequence,double time,double sampleTime=-1)
    {
        if(!t.rainbow)return 0;if(sampleTime<0)sampleTime=time;
        double phase=Motion.Value(t,"rainbowOffset",sampleTime)+sequence*Motion.Value(t,"rainbowDensity",sampleTime);
        if(t.rainbowDynamic)phase+=(time-t.start)*Motion.Value(t,"rainbowSpeed",sampleTime);
        if(t.rainbowReverse)phase=-phase;
        return (int)((Math.Floor(phase)%32+32)%32);
    }
}
