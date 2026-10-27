using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

internal sealed class CopyPlacement {public double x,y,angle;public int index,count;}
internal static class CopyGeneration
{
    public const string Key="copy-object";
    public static readonly string[] Modes={"directions","grid","circle"},Names={"上下左右复制","网格铺满复制","圆周复制"};
    public static readonly string[] Facings={"keep","fixed","inward","outward","tangent"},FacingNames={"保持来源朝向","指定朝向","朝向中心","背向中心","沿圆周切线"};
    public static readonly string[] Colors={"inherit","fixed","rainbow"},ColorNames={"继承来源颜色","固定颜色","按副本顺序彩虹"};
    public static List<CopyPlacement> Placements(BulletAction a)
    {
        if(a.copyCount<1||a.copyCount>300||new[]{a.copySpacingX,a.copySpacingY,a.copyWidth,a.copyHeight,a.copyRadius,a.copyAngle,a.copyFacingAngle,a.copyHue,a.copyColorDensity}.Any(v=>Double.IsNaN(v)||Double.IsInfinity(v)))throw new ArgumentException("复制参数无效");
        var points=new List<CopyPlacement>();Action<double,double> add=(x,y)=>{if(!a.copyCenter&&Math.Abs(x)<.000001&&Math.Abs(y)<.000001)return;points.Add(new CopyPlacement{x=x,y=y,angle=ExpressionContext.Direction(0,0,x,y)});};
        if(a.copyMode=="circle")for(int i=0;i<a.copyCount;i++){double d=(a.copyAngle+360.0*i/a.copyCount)*Math.PI/180;add(a.copyRadius*Math.Cos(d),-a.copyRadius*Math.Sin(d));}
        else if(a.copyMode=="grid"){
            if(a.copySpacingX<=0||a.copySpacingY<=0||a.copyWidth<0||a.copyHeight<0)throw new ArgumentException("网格间距必须大于0，宽高不能为负数。");
            double columnCount=Math.Floor(a.copyWidth/a.copySpacingX)+1,rowCount=Math.Floor(a.copyHeight/a.copySpacingY)+1;if(columnCount*rowCount>300)throw new ArgumentException("每轮最多复制300个物件，请增大间距或缩小网格。");int columns=(int)columnCount,rows=(int)rowCount;double r=a.copyAngle*Math.PI/180;
            for(int y=0;y<rows;y++)for(int x=0;x<columns;x++){double px=(x-(columns-1)/2.0)*a.copySpacingX,py=(y-(rows-1)/2.0)*a.copySpacingY;add(px*Math.Cos(r)+py*Math.Sin(r),-px*Math.Sin(r)+py*Math.Cos(r));}
        }else for(int layer=1;layer<=a.copyCount;layer++){double distance=layer*a.copySpacingX;if(a.copyUp)add(0,-distance);if(a.copyDown)add(0,distance);if(a.copyLeft)add(-distance,0);if(a.copyRight)add(distance,0);}
        if(points.Count>300)throw new ArgumentException("每轮最多复制300个物件，请减少复制数量。");for(int i=0;i<points.Count;i++){points[i].index=i;points[i].count=points.Count;}return points;
    }
    public static double Rotation(BulletAction a,CopyPlacement p,double heading,double anchorAngle)
    {return a.copyFacing=="keep"?heading+a.copyFacingAngle-anchorAngle:a.copyFacing=="fixed"?a.copyFacingAngle-anchorAngle:p.angle+(a.copyFacing=="inward"?180:a.copyFacing=="tangent"?90:0)+a.copyFacingAngle-anchorAngle;}
    public static Color? Tint(BulletAction a,CopyPlacement p)
    {return a.copyColorMode=="fixed"?(Color?)Motion.ColorOf(a.copyColor):a.copyColorMode=="rainbow"?(Color?)SpawnColors.Hue(a.copyHue+360*p.index/(double)Math.Max(1,p.count)*a.copyColorDensity):null;}
    public static List<CopyPlacement> LivePlacements(BulletAction a,RoomData room)
    {
        if(a.copyMode!="grid")return Placements(a);Placements(a);
        double r=a.copyAngle*Math.PI/180,c=Math.Abs(Math.Cos(r)),s=Math.Abs(Math.Sin(r));
        int nx=(int)Math.Ceiling(Math.Max(a.copyWidth,room.width*c+room.height*s)/a.copySpacingX)+1,ny=(int)Math.Ceiling(Math.Max(a.copyHeight,room.width*s+room.height*c)/a.copySpacingY)+1;
        if((double)nx*ny>300)throw new ArgumentException("铺满房间最多复制300个物件，请增大网格间距。");
        var points=new List<CopyPlacement>();for(int y=0;y<ny;y++)for(int x=0;x<nx;x++){double dx=(x-Math.Floor(nx/2.0))*a.copySpacingX,dy=(y-Math.Floor(ny/2.0))*a.copySpacingY;points.Add(new CopyPlacement{x=dx,y=dy,angle=ExpressionContext.Direction(0,0,dx,dy)+a.copyAngle,index=points.Count,count=nx*ny});}return points;
    }
    public static PointF GridPoint(BulletAction a,CopyPlacement cell,RoomData room,double px,double py)
    {
        double r=a.copyAngle*Math.PI/180,c=Math.Cos(r),s=Math.Sin(r),dx=px-room.width/2.0,dy=py-room.height/2.0,u=dx*c-dy*s,v=dx*s+dy*c;
        u=u-Math.Floor(u/a.copySpacingX)*a.copySpacingX+cell.x;v=v-Math.Floor(v/a.copySpacingY)*a.copySpacingY+cell.y;
        return new PointF((float)(room.width/2.0+u*c+v*s),(float)(room.height/2.0-u*s+v*c));
    }
    public static BulletAction Apply(EditorState state,Track source)
    {
        var graph=new List<Track>{source};bool more;do{more=false;foreach(var t in state.tracks)if(graph.Any(p=>p.id==t.parentId)&&!graph.Contains(t)){graph.Add(t);more=true;}}while(more);
        if(state.tracks.Count+graph.Count>200)throw new ArgumentException("复制模板会超过工程200个物件的上限。");var clones=BulletSystem.CloneTracks(graph);var target=clones[0];target.parentId="";target.templateOnly=true;target.name="模板 · 复制 · "+source.name;
        state.tracks.AddRange(clones);
        var action=new BulletAction{type="spawn",generationPreset=Key,templateId=target.id,spawnRepeat=Math.Max(1,state.fps),maxGeneration=4,inheritAppearance=source.kind!="group",inheritScale=true};source.spawnActions.Add(action);source.advancedBullets=true;RandomParameters.Attach(state);return action;
    }
    public static void Fields(NativeProperties bag,EditorState state,Track source,BulletAction a,Action<Action> edit,bool wholeSource=true)
    {
        Action<string,Type,Func<object>,Action<BulletAction,object>,string[]> field=(name,type,get,set,choices)=>bag.Add(name,"复制obj",type,get,v=>{var probe=a.Snapshot();set(probe,v);Placements(probe);edit(()=>set(a,v));},choices);
        if(wholeSource)field("实时联动来源",typeof(bool),()=>a.copyLinked,(target,v)=>target.copyLinked=(bool)v,null);
        field("复制排列",typeof(string),()=>Names[Math.Max(0,Array.IndexOf(Modes,a.copyMode))],(target,v)=>target.copyMode=Modes[Array.IndexOf(Names,(string)v)],Names);
        if(a.copyMode=="directions"){
            foreach(string direction in new[]{"Up","Down","Left","Right"}){string d=direction;string label=d=="Up"?"向上复制":d=="Down"?"向下复制":d=="Left"?"向左复制":"向右复制";field(label,typeof(bool),()=>typeof(BulletAction).GetField("copy"+d).GetValue(a),(target,v)=>typeof(BulletAction).GetField("copy"+d).SetValue(target,v),null);}
            field("每个方向复制层数",typeof(int),()=>a.copyCount,(target,v)=>target.copyCount=Math.Max(1,Math.Min(75,(int)v)),null);field("复制间距 / 像素",typeof(double),()=>a.copySpacingX,(target,v)=>target.copySpacingX=Math.Max(.01,(double)v),null);
        }else if(a.copyMode=="grid"){
            field("网格间距 X",typeof(double),()=>a.copySpacingX,(target,v)=>target.copySpacingX=Math.Max(.01,(double)v),null);field("网格间距 Y",typeof(double),()=>a.copySpacingY,(target,v)=>target.copySpacingY=Math.Max(.01,(double)v),null);
            field("网格宽度 / 像素",typeof(double),()=>a.copyWidth,(target,v)=>target.copyWidth=Math.Max(0,(double)v),null);field("网格高度 / 像素",typeof(double),()=>a.copyHeight,(target,v)=>target.copyHeight=Math.Max(0,(double)v),null);field("网格旋转 / 度",typeof(double),()=>a.copyAngle,(target,v)=>target.copyAngle=(double)v,null);
        }else{field("圆周复制数量",typeof(int),()=>a.copyCount,(target,v)=>target.copyCount=Math.Max(1,Math.Min(300,(int)v)),null);field("圆周复制半径",typeof(double),()=>a.copyRadius,(target,v)=>target.copyRadius=Math.Max(0,(double)v),null);field("圆周起始角 / 度",typeof(double),()=>a.copyAngle,(target,v)=>target.copyAngle=(double)v,null);}
        field("包含中心格点",typeof(bool),()=>a.copyCenter,(target,v)=>target.copyCenter=(bool)v,null);
        field("副本朝向",typeof(string),()=>FacingNames[Math.Max(0,Array.IndexOf(Facings,a.copyFacing))],(target,v)=>target.copyFacing=Facings[Array.IndexOf(FacingNames,(string)v)],FacingNames);field("副本朝向偏移 / 度",typeof(double),()=>a.copyFacingAngle,(target,v)=>target.copyFacingAngle=(double)v,null);
        field("副本配色",typeof(string),()=>ColorNames[Math.Max(0,Array.IndexOf(Colors,a.copyColorMode))],(target,v)=>target.copyColorMode=Colors[Array.IndexOf(ColorNames,(string)v)],ColorNames);
        if(a.copyColorMode=="fixed")field("副本固定颜色",typeof(Color),()=>Motion.ColorOf(a.copyColor),(target,v)=>{var color=(Color)v;target.copyColor=String.Format("#{0:X2}{1:X2}{2:X2}",color.R,color.G,color.B);},null);
        if(a.copyColorMode=="rainbow"){field("副本彩虹起始色相",typeof(double),()=>a.copyHue,(target,v)=>target.copyHue=(double)v,null);field("副本彩虹颜色密度",typeof(double),()=>a.copyColorDensity,(target,v)=>target.copyColorDensity=(double)v,null);}
        bag.Add("副本数量","复制obj",typeof(string),()=>wholeSource&&a.copyLinked&&a.copyMode=="grid"?"按房间大小自动铺满":Placements(a).Count.ToString(),null);bag.Add("说明","复制obj",typeof(string),()=>wholeSource&&a.copyLinked?"副本逐帧跟随来源当前的形状、位置及动画；只建立一组联动副本。网格按每颗成员循环铺满房间，不受整体中心是否出屏影响。关闭联动可使用独立模板和重复生成。":"复制整个物件及其动画，副本从模板起点播放。来源本身保留；每轮最多300份。彩虹配色请使用白色底图。",null);
    }

}
