using System;
internal sealed class SpriteCollider {public double left,top,right,bottom;public bool empty;}
internal static class SpriteMaskLifetime
{
    public static bool Enabled(Track t){return t.despawnBounds=="sprite-mask";}
    public static double CurrentScale(SimBullet b,int age){string fx=BulletLifetime.Effect(b.source);if(fx!="shrink"&&fx!="shrink-fade"||b.source.deathFrames<=0)return 1;if(b.deathStart>=0)return b.deathInitial*Math.Max(0,1-(age-b.deathStart)/(double)b.source.deathFrames);return BulletLifetime.Mode(b.source)=="fixed"?Math.Max(0,Math.Min(1,(b.life-age)/(double)Math.Min(b.life,b.source.deathFrames))):1;}
    public static SpriteCollider Bounds(SpriteMeta meta,int frame,double x,double y,double sx,double sy,double angle,LifetimeBounds? view=null)
    {
        if(meta!=null&&!String.IsNullOrEmpty(meta.error))throw new InvalidOperationException(meta.error);
        SpriteCollider box=null;if(meta!=null&&meta.colliders!=null&&meta.colliders.Length>0){int n=meta.colliders.Length;box=meta.colliders[(frame%n+n)%n];}
        if(meta!=null&&meta.colliders==null)throw new InvalidOperationException("Sprite boundary metadata is missing. Reload sprite resources before previewing sprite-mask lifetime.");
        double l=0,r=0,t=0,b=0;if(box!=null&&!box.empty){l=box.left-meta.originX;r=box.right-meta.originX;t=box.top-meta.originY;b=box.bottom-meta.originY;}
        double a=angle*Math.PI/180,c=Math.Cos(a),s=Math.Sin(a);var result=new SpriteCollider{left=Double.PositiveInfinity,top=Double.PositiveInfinity,right=Double.NegativeInfinity,bottom=Double.NegativeInfinity};
        foreach(double xx in new[]{l,r})foreach(double yy in new[]{t,b}){double lx=xx*sx,ly=yy*sy,wx=x+lx*c+ly*s,wy=y-lx*s+ly*c,px=wx,py=wy;if(view.HasValue){var v=view.Value;double dx=wx-v.x,dy=wy-v.y;px=dx*v.cos-dy*v.sin;py=dx*v.sin+dy*v.cos;}result.left=Math.Min(result.left,px);result.right=Math.Max(result.right,px);result.top=Math.Min(result.top,py);result.bottom=Math.Max(result.bottom,py);}return result;
    }
    public static bool Outside(SpriteCollider b,double width,double height,double margin,bool centered=false)
    {return b.right<(centered?-width/2:0)-margin||b.left>(centered?width/2:width)+margin||b.bottom<(centered?-height/2:0)-margin||b.top>(centered?height/2:height)+margin;}
}
internal sealed partial class BulletWorld
{
    private readonly Func<string,SpriteMeta> spriteMetadata;
    internal string SpriteMetadataRevision;
    private bool SpriteBoundary(SimBullet b,int frame)
    {
        if(!SpriteMaskLifetime.Enabled(b.source)||b.deathStart>=0)return true;
        string name=String.IsNullOrEmpty(b.source.boundsSprite)?b.sprite??b.source.sprite:b.source.boundsSprite;
        SpriteMeta meta=null;if(!String.IsNullOrEmpty(name)&&name!="-1"){meta=spriteMetadata==null?null:spriteMetadata(name);if(meta==null)throw new InvalidOperationException("Missing sprite-mask metadata for '"+name+"'. Reload resources or choose an available boundary mask.");}
        int image=ObjectDynamics.Image(b,frame);
        double scale=b.scale*SpriteMaskLifetime.CurrentScale(b,frame-b.birth);bool useView=b.source.despawnSpace=="view";LifetimeBounds? view=useView?(LifetimeBounds?)LifetimeView():null;
        var bounds=SpriteMaskLifetime.Bounds(meta,image,b.x,b.y,scale*b.axisX,scale*b.axisY,BulletAngle(b,b.direction),view);
        if(!SpriteMaskLifetime.Outside(bounds,useView?view.Value.halfWidth*2:lifetimeRoom.width,useView?view.Value.halfHeight*2:lifetimeRoom.height,b.source.despawnMargin,useView))return true;
        if(!BulletLifetime.Request(b,frame-b.birth)){b.dead=true;return false;}return true;
    }
}
