using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

internal static class ScrollbarThemeTests
{
    private sealed class SurfaceForm : Form
    {protected override bool ShowWithoutActivation{get{return true;}}public SurfaceForm(){StartPosition=FormStartPosition.Manual;Location=new Point(-20000,-20000);ShowInTaskbar=false;ClientSize=new Size(1000,700);}}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static Bitmap Render(Control c){var bitmap=new Bitmap(c.Width,c.Height);c.DrawToBitmap(bitmap,new Rectangle(0,0,c.Width,c.Height));return bitmap;}
    static void DarkBar(Control control,bool vertical,string path)
    {using(var image=Render(control)){image.Save(path);int bright=0,total=0;var bar=vertical?new Rectangle(Math.Max(0,image.Width-SystemInformation.VerticalScrollBarWidth+2),22,Math.Max(1,SystemInformation.VerticalScrollBarWidth-5),Math.Max(1,image.Height-44)):new Rectangle(22,Math.Max(0,image.Height-SystemInformation.HorizontalScrollBarHeight+2),Math.Max(1,image.Width-44),Math.Max(1,SystemInformation.HorizontalScrollBarHeight-5));bar.Intersect(new Rectangle(0,0,image.Width,image.Height));for(int y=bar.Top;y<bar.Bottom;y++)for(int x=bar.Left;x<bar.Right;x++){var color=image.GetPixel(x,y);if(color.R>220&&color.G>220&&color.B>220)bright++;total++;}Check(total>0&&bright/(double)total<.1,"Scrollbar still has a light background: "+Path.GetFileName(path)+" "+bright+"/"+total);}}
    [STAThread]static int Main(string[] args)
    {
        try{Application.EnableVisualStyles();DarkScrollbars.Initialize();Check(DarkScrollbars.Available,"Native dark scrollbars unavailable on this Windows version");
            using(var form=new SurfaceForm())using(var surface=new Panel{Dock=DockStyle.Fill,BackColor=Theme.Panel}){
                form.Controls.Add(surface);var horizontal=new HScrollBar{Bounds=new Rectangle(10,10,440,SystemInformation.HorizontalScrollBarHeight),Maximum=200,LargeChange=25,Value=30};var vertical=new VScrollBar{Bounds=new Rectangle(470,10,SystemInformation.VerticalScrollBarWidth,190),Maximum=200,LargeChange=25,Value=30};surface.Controls.Add(horizontal);surface.Controls.Add(vertical);
                var panel=new Panel{Bounds=new Rectangle(10,50,200,230),AutoScroll=true,BackColor=Theme.Panel};panel.Controls.Add(new Label{Location=new Point(300,900),Text="end",ForeColor=Theme.Text});surface.Controls.Add(panel);
                var list=new ListBox{Bounds=new Rectangle(230,50,220,230),BackColor=Theme.Panel,ForeColor=Theme.Text,BorderStyle=BorderStyle.None,HorizontalScrollbar=true};for(int i=0;i<100;i++)list.Items.Add("row "+i+new string(' ',100)+"end");surface.Controls.Add(list);
                var tree=new TreeView{Bounds=new Rectangle(510,10,220,270),BackColor=Theme.Panel,ForeColor=Theme.Text,BorderStyle=BorderStyle.None};for(int i=0;i<100;i++)tree.Nodes.Add("node "+i);surface.Controls.Add(tree);
                var resources=new ListView{Bounds=new Rectangle(750,10,230,270),BackColor=Theme.Panel,ForeColor=Theme.Text,BorderStyle=BorderStyle.None,View=View.Details,HeaderStyle=ColumnHeaderStyle.None};resources.Columns.Add("resource",500);for(int i=0;i<100;i++)resources.Items.Add("resource "+i);surface.Controls.Add(resources);
                var code=new TextBox{Bounds=new Rectangle(10,320,460,220),Multiline=true,WordWrap=false,ScrollBars=ScrollBars.Both,BorderStyle=BorderStyle.None,BackColor=Theme.Panel,ForeColor=Theme.Text,Text=String.Join("\r\n",Enumerable.Range(0,100).Select(i=>"line "+i+new string('x',160)).ToArray())};surface.Controls.Add(code);
                DarkScrollbars.Attach(form);form.Show();Application.DoEvents();int events=0;horizontal.ValueChanged+=(s,e)=>events++;horizontal.Value=80;Check(events==1&&horizontal.Value==80,"Theming changed scrollbar values/events");panel.AutoScrollPosition=new Point(0,100);Check(panel.AutoScrollPosition.Y<0,"Theming disabled panel scrolling");list.TopIndex=30;Check(list.TopIndex==30,"Theming reset list scroll position");
                DarkBar(horizontal,false,Path.Combine(args[0],"horizontal.png"));DarkBar(vertical,true,Path.Combine(args[0],"vertical.png"));DarkBar(panel,true,Path.Combine(args[0],"auto-panel.png"));DarkBar(list,true,Path.Combine(args[0],"listbox.png"));DarkBar(tree,true,Path.Combine(args[0],"tree.png"));DarkBar(resources,true,Path.Combine(args[0],"resources.png"));DarkBar(code,true,Path.Combine(args[0],"gml.png"));DarkBar(code,false,Path.Combine(args[0],"gml-horizontal.png"));
                code.BorderStyle=BorderStyle.FixedSingle;Application.DoEvents();DarkBar(code,true,Path.Combine(args[0],"gml-recreated.png"));var later=new TextBox{Bounds=new Rectangle(510,320,470,220),Multiline=true,ScrollBars=ScrollBars.Vertical,BackColor=Theme.Panel,ForeColor=Theme.Text,Text=String.Join("\r\n",Enumerable.Range(0,100).Select(i=>"late "+i).ToArray())};surface.Controls.Add(later);later.CreateControl();Application.DoEvents();DarkBar(later,true,Path.Combine(args[0],"dynamic.png"));using(var all=Render(surface))all.Save(Path.Combine(args[0],"all-scrollbars.png"));form.Close();
            }Console.WriteLine("PASS: dark H/V scrollbars, auto-scroll panels, list/tree/resource controls, GML text, handle recreation and dynamically added controls; native values/events and scroll positions retained.");return 0;
        }catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
