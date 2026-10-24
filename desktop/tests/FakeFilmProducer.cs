using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;

internal static class FakeFilmProducer
{
    public static void Main()
    {
        int total=Int32.Parse(File.ReadAllText("count.txt"));bool green=File.ReadAllText("color.txt")=="green";
        for(int frame=0;frame<total;frame++){
            using(var image=new Bitmap(64,48))using(var g=Graphics.FromImage(image)){g.Clear(green?Color.FromArgb(0,150+frame*10,0):Color.FromArgb(0,0,150+frame*10));image.Save("frame.bmp",ImageFormat.Bmp);}
            File.WriteAllText("status.tmp","frame="+frame+"\ndone=0\nobjects=world:1|player:1|objNativeBullet:"+(frame<3?5:7)+"|\n"+(File.Exists("view.txt")?"view=1\nx=4\ny=2\nw=32\nh=24\nangle=0\nportX=0\nportY=0\nportW=64\nportH=48\noutputW=64\noutputH=48\n":""));if(File.Exists("status.txt"))File.Delete("status.txt");File.Move("status.tmp","status.txt");while(!File.Exists(Path.Combine("ack",frame+".ok")))Thread.Sleep(1);
        }
    }
}
