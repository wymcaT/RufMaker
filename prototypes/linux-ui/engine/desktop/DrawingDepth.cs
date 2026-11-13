using System;
using System.Collections.Generic;
using System.Linq;

internal static class DrawingDepth
{
    public static readonly string[] Modes={"generation","absolute"},ModeNames={"随生成代数前移","固定深度（忽略代数）"};
    public const string ModeHelp="默认按生成代数前移；固定深度让所有副本及子弹保持该物件的绘制深度。事件指定的深度仍优先。";
    public static void Normalize(Track track)
    {track.depthMode=track.depthMode??"generation";if(!Modes.Contains(track.depthMode))throw new ArgumentException("绘制深度模式无效。");foreach(var child in track.children)Normalize(child);}
    public static int Track(Track track){return track.depth??(track.kind=="object"?-7000000:-100);}
    // Nontrailing background objects must not push the shared trail pass behind themselves.
    public static double TrailBase(IEnumerable<Track> tracks)
    {return Math.Max(-6999999,tracks.Where(t=>t.enabled&&(t.kind=="rod"||t.kind=="emitter"||t.kind=="object"&&Trails.Mode(t)!="off")).Select(t=>(double)Track(t)+1).DefaultIfEmpty(-6999999).Max());}
    public static double TrailLayer(IEnumerable<Track> tracks,IEnumerable<SimBullet> bullets=null)
    {double depth=TrailBase(tracks);if(bullets!=null)foreach(var b in bullets)if(!b.dead)depth=Math.Max(depth,Bullet(b)+1);return depth;}
    public static double Bullet(SimBullet bullet)
    {return bullet.drawDepth??(bullet.source.depthMode=="absolute"?(double)Track(bullet.source):bullet.generation>0&&!bullet.source.depth.HasValue?-7000000.0-bullet.generation:(double)Track(bullet.source)-bullet.generation);}
}

internal sealed class DepthLayers
{
    private readonly SortedDictionary<double,List<Action>> layers=new SortedDictionary<double,List<Action>>(Comparer<double>.Create((a,b)=>b.CompareTo(a)));
    public void Add(double depth,Action draw)
    {List<Action> group;if(!layers.TryGetValue(depth,out group)){group=new List<Action>();layers.Add(depth,group);}group.Add(draw);}
    public void Draw(){foreach(var group in layers.Values)foreach(var draw in group)draw();}
}
