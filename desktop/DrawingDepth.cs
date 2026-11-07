using System;
using System.Collections.Generic;
using System.Linq;

internal static class DrawingDepth
{
    public static int Track(Track track){return track.depth??(track.kind=="object"?-7000000:-100);}
    public static double Bullet(SimBullet bullet)
    {return bullet.drawDepth??(bullet.generation>0&&!bullet.source.depth.HasValue?-7000000.0-bullet.generation:(double)Track(bullet.source)-bullet.generation);}
}

internal sealed class DepthLayers
{
    private readonly SortedDictionary<double,List<Action>> layers=new SortedDictionary<double,List<Action>>(Comparer<double>.Create((a,b)=>b.CompareTo(a)));
    public void Add(double depth,Action draw)
    {List<Action> group;if(!layers.TryGetValue(depth,out group)){group=new List<Action>();layers.Add(depth,group);}group.Add(draw);}
    public void Draw(){foreach(var group in layers.Values)foreach(var draw in group)draw();}
}
