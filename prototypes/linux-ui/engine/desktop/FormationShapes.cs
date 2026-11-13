using System;
using System.Linq;

// Unit outlines use screen coordinates (positive Y points down).
// Heart and star2 retain the geometry of the supplied avoidance scripts.
internal static class FormationShapes
{
    internal struct PointD {public double X,Y;public PointD(double x,double y){X=x;Y=y;}}
    public static readonly string[] Values={"rod","ring","polygon","heart","star2","star","ellipse","flower","infinity"};
    public static readonly string[] Names={"转转棒","圆环","多边形","心形（heart）","星星（star2）","星形轮廓","椭圆","花朵","∞ 形"};
    public static string Name(string shape){return Names[Math.Max(0,Array.IndexOf(Values,shape))];}
    public static string[] Fields(string shape)
    {
        if(shape=="rod")return new[]{"spriteAngle","x1","y1","x2","y2","length","rollSpeed","count"};
        if(shape=="ring")return new[]{"spriteAngle","radius","rollSpeed","count"};
        var result=new[]{"spriteAngle","radius","orientation"};
        if(new[]{"polygon","star","star2","flower"}.Contains(shape))result=result.Concat(new[]{"sides"}).ToArray();
        if(shape!="polygon")result=result.Concat(new[]{"shapeAspect"}).ToArray();
        if(shape=="star"||shape=="flower")result=result.Concat(new[]{"shapeInner"}).ToArray();
        return result.Concat(new[]{"rollSpeed","count"}).ToArray();
    }
    public static PointD Point(string shape,double u,double radius,double orientation,int sides,double aspect,double inner)
    {
        u-=Math.Floor(u);sides=Math.Max(3,Math.Min(64,sides));aspect=Math.Max(-10,Math.Min(10,aspect));inner=Math.Max(.01,Math.Min(1,inner));
        double theta=u*2*Math.PI,px,py;
        if(shape=="heart")
        {
            double s=Math.Sin(theta);
            px=s*s*s;py=(-13*Math.Cos(theta)+5*Math.Cos(2*theta)+2*Math.Cos(3*theta)+Math.Cos(4*theta))/16;
        }
        else if(shape=="star"||shape=="star2"||shape=="polygon")
        {
            int vertices=shape=="star"?sides*2:sides;double v=u*vertices,segment=Math.Floor(v),mix=v-segment;
            double a=segment*2*Math.PI/vertices,b=(segment+(shape=="star2"?2:1))*2*Math.PI/vertices;
            double ra=shape=="star"&&((int)segment%2)==1?inner:1,rb=shape=="star"&&((int)segment%2)==0?inner:1;
            px=Math.Cos(a)*ra*(1-mix)+Math.Cos(b)*rb*mix;py=-Math.Sin(a)*ra*(1-mix)-Math.Sin(b)*rb*mix;
        }
        else if(shape=="infinity")
        {
            double s=Math.Sin(theta),denominator=1+s*s;
            px=Math.Cos(theta)/denominator;py=-2*s*Math.Cos(theta)/denominator;
        }
        else
        {
            double r=shape=="flower"?inner+(1-inner)*(.5+.5*Math.Cos(sides*theta)):1;
            px=r*Math.Cos(theta);py=-r*Math.Sin(theta);
        }
        // Existing polygon geometry has no aspect setting.
        py*=shape=="polygon"?1:aspect;
        double rotation=orientation*Math.PI/180,c=Math.Cos(rotation),sine=Math.Sin(rotation);
        return new PointD(radius*(px*c+py*sine),radius*(-px*sine+py*c));
    }
    public static double Perimeter(string shape,double radius,int sides,double aspect,double inner)
    {
        if(shape=="ring")return 2*Math.PI*radius;
        if(shape=="polygon")return 2*sides*radius*Math.Sin(Math.PI/sides);
        if(shape=="star2"||shape=="star")
        {
            int vertices=shape=="star"?2*sides:sides;double total=0;
            for(int i=0;i<vertices;i++){var a=Point(shape,i/(double)vertices,radius,0,sides,aspect,inner);var b=Point(shape,(i+(shape=="star2"?2:1))/(double)vertices,radius,0,sides,aspect,inner);double dx=b.X-a.X,dy=b.Y-a.Y;total+=Math.Sqrt(dx*dx+dy*dy);}
            return total;
        }
        // The same fixed subdivision is used by the GM helper for scrolling speed.
        const int samples=128;double length=0;var previous=Point(shape,0,radius,0,sides,aspect,inner);
        for(int i=1;i<=samples;i++){var point=Point(shape,i/(double)samples,radius,0,sides,aspect,inner);double dx=point.X-previous.X,dy=point.Y-previous.Y;length+=Math.Sqrt(dx*dx+dy*dy);previous=point;}
        return length;
    }
}
