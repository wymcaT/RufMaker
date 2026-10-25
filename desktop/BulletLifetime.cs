using System;
using System.Linq;

internal struct LifetimeBounds
{
    public double x,y,halfWidth,halfHeight,cos,sin;
    public bool Contains(double px,double py,double margin)
    {double dx=px-x,dy=py-y;return Math.Abs(dx*cos-dy*sin)<=halfWidth+margin&&Math.Abs(dx*sin+dy*cos)<=halfHeight+margin;}
}
internal static class BulletLifetime
{
    public static readonly string[] Modes={"fixed","room","view"},ModeNames={"固定寿命","出屏幕销毁","出视野销毁"};
    public static readonly string[] Effects={"none","shrink","fade","shrink-fade"},EffectNames={"无过渡","逐渐缩小","逐渐透明","缩小并透明"};
    public static string Mode(Track t){return Modes.Contains(t.lifetimeMode)?t.lifetimeMode:"fixed";}
    public static string Effect(Track t){return Effects.Contains(t.deathEffect)?t.deathEffect:"none";}
    public static bool Enabled(Track t){return Mode(t)!="fixed"||Effect(t)!="none"&&t.deathFrames>0;}
    public static LifetimeBounds Bounds(double x,double y,double width,double height,double angle)
    {double a=angle*Math.PI/180;return new LifetimeBounds{x=x,y=y,halfWidth=width/2,halfHeight=height/2,cos=Math.Cos(a),sin=Math.Sin(a)};}
    public static bool Request(SimBullet b,int age)
    {
        if(Effect(b.source)=="none"||b.source.deathFrames<=0)return false;
        if(b.deathStart<0){b.deathStart=age;b.deathInitial=Mode(b.source)=="fixed"?Math.Max(0,Math.Min(1,(b.life-age)/(double)Math.Min(b.life,b.source.deathFrames))):1;}b.dead=false;b.collision=false;return true;
    }
    public static bool Expired(SimBullet b,int age)
    {return b.deathStart>=0?age-b.deathStart>=b.source.deathFrames:Mode(b.source)=="fixed"&&age>=b.life;}
    public static bool Update(SimBullet b,int age,RoomData room,LifetimeBounds view)
    {
        var t=b.source;string mode=Mode(t);
        if(b.deathStart<0&&mode!="fixed")
        {
            var bounds=mode=="view"?view:Bounds(room.width/2.0,room.height/2.0,room.width,room.height,0);
            if(!bounds.Contains(b.x,b.y,t.despawnMargin)&&!Request(b,age)){b.dead=true;return false;}
        }
        if(Expired(b,age)){b.dead=true;return false;}
        b.deathFactor=1;
        if(Effect(t)!="none"&&t.deathFrames>0)
        {
            if(b.deathStart>=0)b.deathFactor=b.deathInitial*Math.Max(0,1-(age-b.deathStart)/(double)t.deathFrames);
            else if(mode=="fixed")b.deathFactor=Math.Max(0,Math.Min(1,(b.life-age)/(double)Math.Min(b.life,t.deathFrames)));
        }
        return true;
    }
    public static double Scale(SimBullet b){string fx=Effect(b.source);return fx=="shrink"||fx=="shrink-fade"?b.deathFactor:1;}
    public static double Alpha(SimBullet b){string fx=Effect(b.source);return fx=="fade"||fx=="shrink-fade"?b.deathFactor:1;}
}
internal sealed partial class MainForm
{
    private void LifetimeFields(NativeProperties bag,Track t)
    {
        Field(bag,"寿命模式","弹幕寿命",()=>BulletLifetime.ModeNames[Array.IndexOf(BulletLifetime.Modes,BulletLifetime.Mode(t))],value=>{t.lifetimeMode=BulletLifetime.Modes[Array.IndexOf(BulletLifetime.ModeNames,value)];QueueInspectorUpdate();},BulletLifetime.ModeNames,"用于发射后的弹幕与释放后的成员。出屏幕按房间边界；出视野按游戏镜头。边界按弹幕中心判断，出生即在外也会触发。");
        if(BulletLifetime.Mode(t)!="fixed")Field(bag,"出界余量 / 像素","弹幕寿命",()=>t.despawnMargin,value=>t.despawnMargin=value,null,"正值放宽边界；负值提前在范围内触发，可让销毁过渡在画面内可见。出界模式不受固定寿命限制。");
        Field(bag,"销毁过渡","弹幕寿命",()=>BulletLifetime.EffectNames[Array.IndexOf(BulletLifetime.Effects,BulletLifetime.Effect(t))],value=>{t.deathEffect=BulletLifetime.Effects[Array.IndexOf(BulletLifetime.EffectNames,value)];QueueInspectorUpdate();},BulletLifetime.EffectNames,"固定寿命在结束前播放过渡；出界或销毁行为触发后播放过渡，结束才移除。触发销毁后不再碰撞或继续生成。");
        if(BulletLifetime.Effect(t)!="none")Field(bag,"过渡帧数","弹幕寿命",()=>t.deathFrames,value=>t.deathFrames=Math.Max(0,value));
    }
}
