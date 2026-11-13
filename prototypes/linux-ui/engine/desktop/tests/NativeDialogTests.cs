using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

internal static class NativeDialogTests
{
    [STAThread] public static int Main(string[] args)
    {
        try{
            AppWindow.RefreshFileIcon(Application.ExecutablePath);
            using(var embedded=AppWindow.LoadIcon())using(var expected=new Icon(args[0],32,32))using(var actual=embedded.ToBitmap())using(var reference=expected.ToBitmap())
            {for(int y=0;y<32;y++)for(int x=0;x<32;x++)if(actual.GetPixel(x,y)!=reference.GetPixel(x,y))throw new Exception("Embedded window icon differs from icon.ico");}
            foreach(float scale in new[]{1f,1.25f,1.5f,2f})using(var form=new NativeReplayProgressForm(null,new{},1882)){
                form.Font=new Font(Theme.Font.FontFamily,Theme.Font.Size*scale);var layout=(TableLayoutPanel)form.Controls[0];var label=layout.Controls.OfType<Label>().Single();
                label.Text="正在重放至播放头 · 1570 / 1882 帧\r\n画面复杂时可能较慢；有帧进度就会继续等待。";
                form.CreateControl();form.PerformLayout();layout.PerformLayout();var progress=layout.Controls.OfType<ProgressBar>().Single();var cancel=layout.Controls.OfType<Button>().Single();
                if(!label.AutoSize||label.Height<label.GetPreferredSize(new Size(label.Width,0)).Height||label.Bottom>progress.Top||progress.Bottom>cancel.Top)throw new Exception("Progress layout clips or overlaps at "+scale);
            }
            Console.WriteLine("PASS: embedded window icon and replay layout at 100/125/150/200% font sizes");return 0;
        }catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
