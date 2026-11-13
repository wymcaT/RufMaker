using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

internal sealed class GmPathPoint
{
    public double x,y,speed=100;
    public GmPathPoint Copy(){return (GmPathPoint)MemberwiseClone();}
}

internal sealed class GmPath
{
    public bool smooth,closed;
    public int precision=4;
    public List<GmPathPoint> points=new List<GmPathPoint>();
    private GmPathCurve curve;
    [System.Web.Script.Serialization.ScriptIgnore]
    public GmPathCurve Curve {get{return curve??(curve=new GmPathCurve(this));}}
    public void Changed(){curve=null;}
    public GmPath Copy(){return new GmPath{smooth=smooth,closed=closed,precision=precision,points=points.Select(p=>p.Copy()).ToList()};}
}

internal sealed class GmPathCurve
{
    private struct Vector
    {
        public double X,Y;
        public Vector(double x,double y){X=x;Y=y;}
        public static Vector Mid(Vector a,Vector b){return new Vector((a.X+b.X)*.5,(a.Y+b.Y)*.5);}
        public static double DistanceSquared(Vector a,Vector b){double x=a.X-b.X,y=a.Y-b.Y;return x*x+y*y;}
    }
    private readonly Vector[] vertices;
    private readonly double[] distance;
    public GmPathCurve(GmPath path)
    {
        var points=path.points.Select(p=>new Vector(p.x,p.y)).ToArray();
        var samples=new List<Vector>();
        int count=points.Length;
        if(!path.smooth||count<=1)samples.AddRange(points);
        else{
            if(!path.closed)samples.Add(points[0]);
            int corners=path.closed?count:count-2;
            for(int i=0;i<corners;i++){
                Vector a=points[i],b=points[(i+1)%count],c=points[(i+2)%count];
                SampleCorner(samples,Vector.Mid(a,b),b,Vector.Mid(b,c),Math.Max(1,Math.Min(8,path.precision)));
            }
            if(!path.closed)samples.Add(points[count-1]);
        }
        if(path.closed&&samples.Count>1)samples.Add(samples[0]);
        vertices=samples.ToArray();distance=new double[vertices.Length];
        for(int i=1;i<vertices.Length;i++)distance[i]=distance[i-1]+Math.Sqrt(Vector.DistanceSquared(vertices[i-1],vertices[i]));
    }
    private static void SampleCorner(List<Vector> target,Vector a,Vector control,Vector b,int depth)
    {
        if(depth<=0)return;
        var left=Vector.Mid(a,control);var right=Vector.Mid(control,b);var center=Vector.Mid(left,right);
        if(Vector.DistanceSquared(a,control)>16)SampleCorner(target,a,left,center,depth-1);
        target.Add(center);
        if(Vector.DistanceSquared(b,control)>16)SampleCorner(target,center,right,b,depth-1);
    }
    public PointF[] Outline(){return vertices.Select(v=>new PointF((float)v.X,(float)v.Y)).ToArray();}
    public void Sample(double progress,out double x,out double y)
    {
        if(vertices.Length==0){x=y=0;return;}
        if(progress<=0||distance[distance.Length-1]==0){x=vertices[0].X;y=vertices[0].Y;return;}
        if(progress>=1){x=vertices[vertices.Length-1].X;y=vertices[vertices.Length-1].Y;return;}
        double at=progress*distance[distance.Length-1];
        int lo=0,hi=distance.Length-1;
        while(lo+1<hi){int mid=(lo+hi)/2;if(distance[mid]<=at)lo=mid;else hi=mid;}
        double weight=distance[hi]>distance[lo]?(at-distance[lo])/(distance[hi]-distance[lo]):0;
        x=vertices[lo].X+(vertices[hi].X-vertices[lo].X)*weight;
        y=vertices[lo].Y+(vertices[hi].Y-vertices[lo].Y)*weight;
    }
    public static void Offset(Track track,double elapsed,Keyframe position)
    {
        GmPath path;
        if(track.kind!="emitter"||String.IsNullOrEmpty(track.nativePath)||track.owner==null||!track.owner.nativePaths.TryGetValue(track.nativePath,out path))return;
        int duration=Math.Max(1,track.nativePathFrames);
        double progress=(track.nativePathLoop?elapsed%duration:Math.Min(elapsed,duration))/duration;
        double x,y,startX,startY;
        path.Curve.Sample(progress,out x,out y);
        path.Curve.Sample(0,out startX,out startY);
        position.x+=x-startX;position.y+=y-startY;
    }
}
