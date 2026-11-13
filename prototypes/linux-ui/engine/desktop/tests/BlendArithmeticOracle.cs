using System;using System.Drawing;using System.Drawing.Imaging;
internal static class BlendArithmeticOracle {
 static void Main(){foreach(int alpha in new[]{0,64,128,255})foreach(int dest in new[]{0,40,240}){
 using(var d=new Bitmap(1,1,PixelFormat.Format32bppArgb))using(var s=new Bitmap(1,1,PixelFormat.Format32bppArgb)){
 d.SetPixel(0,0,Color.FromArgb(255,dest,dest,dest));s.SetPixel(0,0,Color.FromArgb(alpha,120,80,40));var src=s.GetPixel(0,0);SpriteBlending.Composite(d,s);var got=d.GetPixel(0,0);
 Console.WriteLine(alpha+","+dest+","+src.R+","+src.G+","+src.B+","+got.R+","+got.G+","+got.B);
 }}}
}
