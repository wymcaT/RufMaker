using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class TimelineCursorRenderingTests
{
    static readonly BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr window,IntPtr dc,uint options);
    sealed class PaintHost:Form
    {protected override bool ShowWithoutActivation {get{return true;}}protected override CreateParams CreateParams {get{var p=base.CreateParams;p.ExStyle|=0x08000000;return p;}}public PaintHost(Control child){StartPosition=FormStartPosition.Manual;Location=new Point(-30000,-30000);FormBorderStyle=FormBorderStyle.None;ClientSize=child.Size;ShowInTaskbar=false;child.Dock=DockStyle.Fill;Controls.Add(child);DarkScrollbars.Attach(this);Show();Application.DoEvents();}}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Mouse(Control c,string method,int x,int y){typeof(Control).GetMethod(method,flags).Invoke(c,new object[]{new MouseEventArgs(MouseButtons.Left,1,x,y,0)});}
    static void Paint(TimelineCanvas timeline,Graphics graphics){typeof(Control).GetMethod("OnPaint",flags).Invoke(timeline,new object[]{new PaintEventArgs(graphics,timeline.ClientRectangle)});}
    static Bitmap Snapshot(Form host,string path){host.Update();var image=new Bitmap(host.ClientSize.Width,host.ClientSize.Height);using(var graphics=Graphics.FromImage(image)){var dc=graphics.GetHdc();try{Check(PrintWindow(host.Handle,dc,1),"Could not render own offscreen test window");}finally{graphics.ReleaseHdc(dc);}}image.Save(path);return image;}
    static int Column(Bitmap image,int x,bool gold){int n=0;for(int y=30;y<image.Height-30;y++){var c=image.GetPixel(x,y);if(gold?c.R>220&&c.G>160&&c.B<160:c.R>200&&c.G<150&&c.B>70&&c.B<220)n++;}return n;}
    public static void Run(string folder){
        var item=new Track{id="guide",name="guide",kind="object",start=30,end=60};Motion.SetKey(item,"x",42,100);var scene=new EditorState{duration=1000};scene.tracks.Add(item);Motion.Normalize(scene);
        using(var timeline=new TimelineCanvas{State=scene,Size=new Size(1400,460),PixelsPerSecond=500})using(var host=new PaintHost(timeline)){
            timeline.PrepareCursorTargets=()=>CursorSnapping.Capture(scene,null,1,true);int frames=0;timeline.Seek=f=>{int before=timeline.Frame;timeline.Frame=f;timeline.PlayheadMoved(before);frames++;};
            Mouse(timeline,"OnMouseDown",614,5);using(var warm=Snapshot(host,Path.Combine(folder,"cursor-guide-initial.png"))){}int beforeFrames=frames,presents=timeline.PresentRequests,layers=timeline.LayerBuilds;Mouse(timeline,"OnMouseMove",615,5);
            Check(frames==beforeFrames&&Math.Abs(timeline.PlayheadX-615)<.01,"Pixel motion within a game frame was lost");Check(timeline.PresentRequests>presents,"Moving within one frame did not request an immediate paint");Check(timeline.LayerBuilds==layers,"Cursor paint rebuilt the static timeline");
            using(var image=Snapshot(host,Path.Combine(folder,"cursor-guide-held.png"))){Check(Column(image,610,true)>60,"Dashed candidate line missing");Check(Column(image,615,false)>250,"Raw playhead did not follow the mouse pixel");}
            Mouse(timeline,"OnMouseUp",615,5);Check(timeline.Frame==42&&Math.Abs(timeline.PlayheadX-610)<.01,"Release did not land on hinted frame");using(var image=Snapshot(host,Path.Combine(folder,"cursor-guide-released.png"))){Check(Column(image,610,false)>250,"Committed playhead missing");Check(Column(image,615,false)<15,"Old mouse cursor left a paint trail");}
            timeline.ReadModifiers=()=>Keys.Alt;Mouse(timeline,"OnMouseDown",614,5);using(var image=Snapshot(host,Path.Combine(folder,"cursor-guide-alt.png")))Check(Column(image,610,true)<15,"Alt still drew the guide");Mouse(timeline,"OnMouseUp",614,5);
        }
        var dense=new Track{id="dense",name="dense manual keys",kind="object",start=0,end=10000};for(int f=0;f<=10000;f++){dense.keyframes.Add(new Keyframe{frame=f,property="x",value=f});dense.keyframes.Add(new Keyframe{frame=f,property="alpha",value=1});}scene=new EditorState{duration=20000};scene.tracks.Add(dense);Motion.Normalize(scene);
        using(var timeline=new TimelineCanvas{State=scene,Size=new Size(1400,500),PixelsPerSecond=500})using(var host=new PaintHost(timeline)){
            timeline.SetTrackSelection(new[]{dense},dense);timeline.Wave=new WaveData{duration=400,peaks=new double[400000]};for(int i=0;i<timeline.Wave.peaks.Length;i++)timeline.Wave.peaks[i]=.3+.5*Math.Abs(Math.Sin(i*.013));timeline.RefreshLayout();timeline.PresentPlayhead();
            using(var image=new Bitmap(timeline.Width,timeline.Height))using(var graphics=Graphics.FromImage(image)){
                var watch=Stopwatch.StartNew();for(int i=0;i<8;i++){timeline.Invalidate();Paint(timeline,graphics);}double full=watch.Elapsed.TotalMilliseconds/8;
                int builds=0;timeline.PrepareCursorTargets=()=>{builds++;return CursorSnapping.Capture(scene,null,1,true);};timeline.Seek=f=>{int before=timeline.Frame;timeline.Frame=f;timeline.PlayheadMoved(before);};Mouse(timeline,"OnMouseDown",610,5);Paint(timeline,graphics);int layers=timeline.LayerBuilds,presents=timeline.PresentRequests,paints=timeline.CursorPaints;watch.Restart();for(int i=0;i<240;i++){Mouse(timeline,"OnMouseMove",611+i%180,5);Paint(timeline,graphics);}double overlay=watch.Elapsed.TotalMilliseconds/240;
                Check(builds==1,"Candidate index rebuilt during drag");Check(timeline.LayerBuilds==layers,"Dense keys/waveform redrawn for every mouse sample");Check(timeline.PresentRequests-presents>=240&&timeline.CursorPaints-paints>=240,"Mouse samples did not request immediate paints");Mouse(timeline,"OnMouseUp",790,5);timeline.Invalidate();Paint(timeline,graphics);Check(timeline.LayerBuilds==layers+1,"Scene invalidation did not rebuild the cache");
                File.WriteAllText(Path.Combine(folder,"cursor-paint-profile.txt"),String.Format(System.Globalization.CultureInfo.InvariantCulture,"Dense fixture: 20,002 manual keys, 400,000 waveform peaks\r\nFull timeline repaint: {0:0.000} ms\r\nMouse input + overlay paint: {1:0.000} ms\r\n240 synchronous paint requests + offscreen renders; 0 static rebuilds; 1 target index per gesture.\r\nOffscreen paint cost, not display refresh rate or game FPS.\r\n",full,overlay));Console.WriteLine("PROFILE: full timeline {0:0.000} ms; cursor input/paint {1:0.000} ms (offscreen fixture, not game FPS).",full,overlay);
            }
        }
    }
}
