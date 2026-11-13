using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class BulletBrick
{
    public double left,top,right,bottom;
    public BulletBrick Snapshot(){return (BulletBrick)MemberwiseClone();}
}

internal sealed class BulletBoundaries
{
    private const double Epsilon=.0000001;
    private const int CellSize=128;
    private readonly int width,height;
    private readonly BulletBrick[] bricks;
    private readonly List<BulletBrick>[] cells;
    private readonly int cellLeft,cellTop,cellRight,cellBottom,columns;
    private static readonly BulletBrick[] NoBricks=new BulletBrick[0];

    public BulletBoundaries(RoomData room)
    {
        width=room.width;
        height=room.height;
        bricks=room.instances.Where(i=>i.bulletBrick!=null).Select(i=>{
            var b=i.bulletBrick;
            return new BulletBrick{left=i.x+b.left,top=i.y+b.top,right=i.x+b.right,bottom=i.y+b.bottom};
        }).ToArray();
        if(bricks.Length<=16)return;
        double left=Cell(bricks.Min(b=>b.left)),top=Cell(bricks.Min(b=>b.top));
        double right=Cell(bricks.Max(b=>b.right)),bottom=Cell(bricks.Max(b=>b.bottom));
        double cellCount=(right-left+1)*(bottom-top+1);
        if(!(cellCount>0&&cellCount<=4096)||left<Int32.MinValue||top<Int32.MinValue||right>=Int32.MaxValue||bottom>=Int32.MaxValue)return;
        cellLeft=(int)left;cellTop=(int)top;cellRight=(int)right;cellBottom=(int)bottom;
        columns=cellRight-cellLeft+1;
        var grid=new List<BulletBrick>[(int)cellCount];
        int memberships=0;
        foreach(var brick in bricks){
            int endX=(int)Cell(brick.right),endY=(int)Cell(brick.bottom);
            for(int y=(int)Cell(brick.top);y<=endY;y++)
                for(int x=(int)Cell(brick.left);x<=endX;x++){
                    if(++memberships>16000)return;
                    int index=(y-cellTop)*columns+x-cellLeft;
                    if(grid[index]==null)grid[index]=new List<BulletBrick>();
                    grid[index].Add(brick);
                }
        }
        cells=grid;
    }

    private static double Cell(double position){return Math.Floor(position/CellSize);}

    private IList<BulletBrick> Candidates(double left,double top,double right,double bottom)
    {
        if(cells==null)return bricks;
        double x=Cell(left),y=Cell(top),endX=Cell(right),endY=Cell(bottom);
        if(x>cellRight||y>cellBottom||endX<cellLeft||endY<cellTop)return NoBricks;
        // Keep original brick order for sweeps crossing cells and corner ties.
        if(x!=endX||y!=endY)return bricks;
        return (IList<BulletBrick>)cells[((int)y-cellTop)*columns+(int)x-cellLeft]??NoBricks;
    }

    public void Move(SimBullet bullet)
    {
        double radians=bullet.direction*Math.PI/180;
        double dx=Math.Cos(radians)*bullet.speed,dy=-Math.Sin(radians)*bullet.speed;
        string mode=ObjectProperties.Mode(bullet.source);
        bool hitBricks=bullet.source.bounceBricks&&bricks.Length>0;
        if(mode=="none"&&!hitBricks){bullet.x+=dx;bullet.y+=dy;return;}
        if(mode=="wrap"&&!hitBricks){
            if(Math.Abs(dx)>Epsilon||Math.Abs(dy)>Epsilon){
                bullet.x=Wrap(bullet.x,dx,width);
                bullet.y=Wrap(bullet.y,dy,height);
            }
            return;
        }

        // Sweep the bullet's center and spend the remaining movement after each contact.
        for(int pass=0;pass<8&&(Math.Abs(dx)>Epsilon||Math.Abs(dy)>Epsilon);pass++){
            double fraction=1;
            bool hitX=false,hitY=false,wrapX=false,wrapY=false;
            if(mode!="none"){
                double edgeX=Edge(bullet.x,dx,width),edgeY=Edge(bullet.y,dy,height);
                fraction=Math.Min(1,Math.Min(edgeX,edgeY));
                hitX=edgeX<=fraction+Epsilon;
                hitY=edgeY<=fraction+Epsilon;
                wrapX=hitX&&mode=="wrap";
                wrapY=hitY&&mode=="wrap";
            }
            if(hitBricks){
                double left=Math.Min(bullet.x,bullet.x+dx),right=Math.Max(bullet.x,bullet.x+dx);
                double top=Math.Min(bullet.y,bullet.y+dy),bottom=Math.Max(bullet.y,bullet.y+dy);
                var candidates=Candidates(left,top,right,bottom);
                for(int i=0;i<candidates.Count;i++){
                    var brick=candidates[i];
                    if(right<brick.left||left>brick.right||bottom<brick.top||top>brick.bottom)continue;
                    double nearX,farX,nearY,farY;
                    if(!Slab(bullet.x,dx,brick.left,brick.right,out nearX,out farX)||
                       !Slab(bullet.y,dy,brick.top,brick.bottom,out nearY,out farY))continue;
                    double entry=Math.Max(nearX,nearY);
                    if(entry< -Epsilon||entry>fraction+Epsilon||Math.Min(farX,farY)<=entry+Epsilon)continue;
                    if(entry<fraction-Epsilon){hitX=hitY=wrapX=wrapY=false;}
                    fraction=Math.Max(0,Math.Min(fraction,entry));
                    if(nearX>=nearY-Epsilon){hitX=true;wrapX=false;}
                    if(nearY>=nearX-Epsilon){hitY=true;wrapY=false;}
                }
            }
            bullet.x+=dx*fraction;
            bullet.y+=dy*fraction;
            if(!hitX&&!hitY)return;
            if(wrapX)bullet.x=dx>0?0:width;
            else if(hitX){dx=-dx;bullet.direction=180-bullet.direction;}
            if(wrapY)bullet.y=dy>0?0:height;
            else if(hitY){dy=-dy;bullet.direction=-bullet.direction;}
            dx*=1-fraction;
            dy*=1-fraction;
        }
    }

    private static double Wrap(double position,double delta,double extent)
    {
        double end=position+delta;
        if(extent<=0||position<0||position>extent||end>=0&&end<=extent)return end;
        double wrapped=end-Math.Floor(end/extent)*extent;
        return wrapped==0&&end>extent?extent:wrapped;
    }

    private static double Edge(double position,double delta,double extent)
    {
        if(position<0||position>extent)return 2;
        if(delta>0&&position+delta>extent)return (extent-position)/delta;
        if(delta<0&&position+delta<0)return -position/delta;
        return 2;
    }

    private static bool Slab(double position,double delta,double min,double max,out double near,out double far)
    {
        near=Double.NegativeInfinity;
        far=Double.PositiveInfinity;
        if(Math.Abs(delta)<=Epsilon)return position>=min&&position<max;
        near=(min-position)/delta;
        far=(max-position)/delta;
        if(delta<0){double swap=near;near=far;far=swap;}
        return true;
    }
}
