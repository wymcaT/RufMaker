using System;
using System.Drawing;

internal static class Trails
{
    public static readonly string[] Values={"off","fade","shrink","shrink-fade","appear","custom"};
    public static readonly string[] Names={"关闭","逐渐透明","逐渐缩小","缩小并变透明","透明度逐渐增大","自定义"};
    public static string Mode(Track t){return t.trailMode=="legacy"?(t.trail?"fade":"off"):t.trailMode;}
    public static void SetMode(Track t,string mode){t.trailMode=mode;t.trail=mode!="off";}
    public static int Life(Track t){return Math.Max(1,Math.Min(300,t.trailLife));}
    public static int Interval(Track t){return Math.Max(1,t.trailInterval);}
    public static double Direction(Track t,int seed)
    {double n=Math.Sin((seed+1)*12.9898)*43758.5453;return t.trailRandom?(n-Math.Floor(n))*360:t.trailDirection;}
    public static void Draw(Graphics g,SpriteStore sprites,Track t,int seed,int age,double x,double y,double scale,double angle,double alpha,Color tint)
    {
        string mode=Mode(t);if(mode=="off"||age<0||age>=Life(t))return;
        double size=1,opacity=t.trailStartAlpha;
        if(mode=="shrink"||mode=="shrink-fade")size=1-age/(double)Life(t);
        if(mode=="fade"||mode=="shrink-fade")opacity*=1-age/(double)Life(t);
        if(mode=="appear")opacity*=age/(double)Life(t);
        if(mode=="custom"){size=Math.Max(0,1-t.trailScaleDecay*age);opacity=t.trailStartAlpha-t.trailAlphaDecay*age;}
        double rad=Direction(t,seed)*Math.PI/180,drift=t.trailSpeed*age;
        if(size<=0||opacity<=0)return;
        sprites.Draw(g,t.sprite,x+Math.Cos(rad)*drift,y-Math.Sin(rad)*drift,scale*size,angle,alpha*Math.Max(0,Math.Min(1,opacity)),tint);
    }
}
