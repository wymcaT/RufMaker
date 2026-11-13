using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

internal static class PreviewRenderingTests
{
    private static int CheckSolid(Bitmap image,Matrix worldToScreen,int width,int height)
    {
        int holes=0;using(var inverse=worldToScreen.Clone()){
            inverse.Invert();var p=new PointF[1];
            for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++){
                p[0]=new PointF(x+.5f,y+.5f);inverse.TransformPoints(p);
                if(p[0].X<2||p[0].Y<2||p[0].X>width-2||p[0].Y>height-2)continue;
                Color c=image.GetPixel(x,y);if(c.R<245||c.G<245||c.B<245)holes++;
            }
        }return holes;
    }
    [STAThread] public static int Main(string[] args)
    {
        try{
            string scratch=args[0],output=args[1];bool baseline=args.Length>2&&args[2]=="baseline";Directory.CreateDirectory(Path.Combine(scratch,"sprites","tile"));
            using(var tile=new Bitmap(32,32)){using(var g=Graphics.FromImage(tile))g.Clear(Color.White);tile.Save(Path.Combine(scratch,"sprites","tile","0.png"));}
            Directory.CreateDirectory(Path.Combine(scratch,"sprites","transparent"));using(var transparent=new Bitmap(32,32)){using(var g=Graphics.FromImage(transparent))g.FillRectangle(Brushes.Red,8,8,16,16);transparent.Save(Path.Combine(scratch,"sprites","transparent","0.png"));}
            const int rw=256,rh=192,cellW=380,cellH=300;var room=new RoomData{width=rw,height=rh,bgColor=0x202020};for(int y=0;y<rh;y+=32)for(int x=0;x<rw;x+=32)room.instances.Add(new RoomInstance{name="tile",x=x,y=y});
            var state=new EditorState{duration=100};var objects=new Dictionary<string,string>{{"tile","tile"}};int total=0,index=0;double[] zooms={.66,1,1.13,1.65},angles={0,.7,15,45};
            using(var sprites=new SpriteStore{Project=scratch})using(var sheet=new Bitmap(cellW*4,cellH*4))using(var page=Graphics.FromImage(sheet)){
                page.Clear(Color.FromArgb(20,24,32));
                foreach(double angle in angles)foreach(double zoom in zooms){
                    using(var transform=new Matrix((float)zoom,0,0,(float)zoom,(cellW-rw*(float)zoom)/2+.35f,(cellH-rh*(float)zoom)/2+.45f))using(var camera=new CameraPose{x=rw/2.0,y=rh/2.0,zoom=1.04,angle=angle}.WorldToView(rw,rh)){
                        transform.Multiply(camera,MatrixOrder.Prepend);
                        using(var image=PreviewRenderer.Render(state,room,objects,sprites,0,false,false,null,transform,cellW,cellH)){
                            int holes=CheckSolid(image,transform,rw,rh);total+=holes;page.DrawImageUnscaled(image,index%4*cellW,index/4*cellH);
                            using(var font=new Font("Segoe UI",10))page.DrawString(String.Format("zoom {0:0.00}, angle {1:0.0}, gaps {2}",zoom,angle,holes),font,Brushes.Yellow,index%4*cellW+8,index/4*cellH+6);
                            Console.WriteLine("zoom="+zoom+", angle="+angle+", uncovered interior pixels="+holes);
                        }
                    }index++;
                }
                sheet.Save(output,ImageFormat.Png);
                // Edge clamping must preserve genuine transparent sprite borders, alpha and tint.
                sprites.Metadata["transparent"]=new SpriteMeta{originX=16,originY=16};
                using(var transparent=new Bitmap(128,128))using(var g=Graphics.FromImage(transparent)){
                    g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;sprites.Draw(g,"transparent",64.25,64.75,1.4,23,.5,Color.FromArgb(128,255,255));
                    Color center=transparent.GetPixel(64,65);if(Math.Abs(center.A-128)>1||Math.Abs(center.R-128)>2||center.G!=0||center.B!=0)throw new Exception("Sprite alpha or tint changed");
                    var points=new[]{new PointF(14,14)};using(var matrix=new Matrix()){matrix.Translate(64.25f,64.75f);matrix.Rotate(-23);matrix.Scale(1.4f,1.4f);matrix.TransformPoints(points);}if(transparent.GetPixel((int)points[0].X,(int)points[0].Y).A!=0)throw new Exception("Transparent sprite border became opaque");
                }
            }
            if(total>0&&!baseline)throw new Exception("Tiled terrain has uncovered interior pixels: "+total);
            Console.WriteLine("Total uncovered pixels: "+total+". Render: "+output);return 0;
        }catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
