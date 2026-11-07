using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

internal sealed class RoomPresentation
{
    private sealed class Drawable
    {
        public double Depth;
        public RoomInstance Instance;
        public RoomTile Tile;
    }

    private readonly Drawable[] drawables;
    private readonly Dictionary<double,Drawable[]> layers;
    public IEnumerable<double> Depths {get{return layers.Keys;}}
    public RoomPresentation(RoomData room)
    {
        drawables=room.tiles.Select(t=>new Drawable{Depth=t.depth,Tile=t})
            .Concat(room.instances.Select(i=>new Drawable{Depth=i.depth,Instance=i}))
            .OrderByDescending(item=>item.Depth).ToArray();
        layers=drawables.GroupBy(item=>item.Depth).ToDictionary(group=>group.Key,group=>group.ToArray());
    }

    public static Color Blend(long packed)
    {return Color.FromArgb((int)((packed>>24)&255),(int)(packed&255),(int)((packed>>8)&255),(int)((packed>>16)&255));}

    public static long Pack(Color color)
    {return ((long)color.A<<24)|((long)color.B<<16)|((long)color.G<<8)|color.R;}

    public static void DrawBackgrounds(Graphics g,RoomData room,SpriteStore sprites,int frame,bool foreground)
    {
        if(!room.backgrounds.Any(b=>b.foreground==foreground))return;
        var saved=g.Save();
        try{
            g.IntersectClip(new Rectangle(0,0,room.width,room.height));
            foreach(var background in room.backgrounds){
                if(background.foreground!=foreground)continue;
                var image=sprites.GetBackground(background.source);
                if(image==null)continue;
                var tint=sprites.Tint(1,Color.White);
                if(background.stretch){
                    g.DrawImage(image,new Rectangle(0,0,room.width,room.height),0,0,image.Width,image.Height,GraphicsUnit.Pixel,tint);
                    continue;
                }
                double x=background.x+frame*background.speedX,y=background.y+frame*background.speedY;
                if(background.tileX)x=x-Math.Ceiling(x/image.Width)*image.Width;
                if(background.tileY)y=y-Math.Ceiling(y/image.Height)*image.Height;
                double right=background.tileX?room.width:x+image.Width,bottom=background.tileY?room.height:y+image.Height;
                for(double py=y;py<bottom;py+=image.Height)
                    for(double px=x;px<right;px+=image.Width)
                        g.DrawImage(image,new Rectangle((int)Math.Round(px),(int)Math.Round(py),image.Width,image.Height),0,0,image.Width,image.Height,GraphicsUnit.Pixel,tint);
            }
        }finally{g.Restore(saved);}
    }

    public void Draw(Graphics g,Dictionary<string,string> objectSprites,SpriteStore sprites,bool hitboxes,HashSet<string> baked,PreviewCache cache,double? depth=null)
    {
        var items=depth.HasValue?layers[depth.Value]:drawables;
        for(int n=0;n<items.Length;n++){
            if((n&127)==0)cache.ThrowIfCancelled();
            var drawable=items[n];
            if(drawable.Tile!=null){if(baked==null)DrawTile(g,sprites,drawable.Tile);continue;}
            var instance=drawable.Instance;
            string sprite;
            if(baked!=null&&baked.Contains(instance.id)||!objectSprites.TryGetValue(instance.name,out sprite)||String.IsNullOrEmpty(sprite))continue;
            var tint=Blend(instance.blend);
            if(instance.visible)sprites.DrawExt(g,sprite,instance.x,instance.y,instance.scaleX,instance.scaleY,instance.angle,tint.A/255.0,tint);
            if(hitboxes)using(var pen=new Pen(Color.FromArgb(110,94,212,197)))g.DrawPolygon(pen,Corners(sprites,sprite,instance));
        }
    }

    private static void DrawTile(Graphics g,SpriteStore sprites,RoomTile tile)
    {
        var image=sprites.GetBackground(tile.source);
        if(image==null||tile.scaleX==0||tile.scaleY==0)return;
        var tint=Blend(tile.blend);
        var saved=g.Save();
        g.TranslateTransform((float)tile.x,(float)tile.y);
        g.ScaleTransform((float)tile.scaleX,(float)tile.scaleY);
        g.DrawImage(image,new Rectangle(0,0,tile.width,tile.height),tile.left,tile.top,tile.width,tile.height,GraphicsUnit.Pixel,sprites.Tint(tint.A/255.0,tint));
        g.Restore(saved);
    }

    public static PointF[] Corners(SpriteStore sprites,string sprite,RoomInstance instance)
    {
        var r=sprites.Bounds(sprite,0,0);
        var points=new[]{new PointF(r.Left,r.Top),new PointF(r.Right,r.Top),new PointF(r.Right,r.Bottom),new PointF(r.Left,r.Bottom)};
        using(var matrix=new System.Drawing.Drawing2D.Matrix()){
            matrix.Translate((float)instance.x,(float)instance.y);
            matrix.Rotate((float)-instance.angle);
            matrix.Scale((float)instance.scaleX,(float)instance.scaleY);
            matrix.TransformPoints(points);
        }
        return points;
    }
}
