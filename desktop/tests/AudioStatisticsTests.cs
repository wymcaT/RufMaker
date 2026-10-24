using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

internal static class AudioStatisticsTests
{
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private static readonly BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
    private static object Call(object obj,string name,params object[] args){return obj.GetType().GetMethod(name,flags).Invoke(obj,args);}
    private static void Mouse(TimelineCanvas t,string method,int x,int y){Call(t,method,new MouseEventArgs(MouseButtons.Left,1,x,y,0));}
    private static void Wav(string path)
    {
        const int rate=22050,seconds=4;using(var w=new BinaryWriter(File.Create(path))){w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+rate*seconds*4);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)2);w.Write(rate);w.Write(rate*4);w.Write((short)4);w.Write((short)16);w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(rate*seconds*4);
            for(int i=0;i<rate*seconds;i++){double t=i/(double)rate,frequency=t<1?100:t<2?1000:5000,amplitude=t<3?.8:.08;short v=(short)(Math.Sin(2*Math.PI*frequency*t)*amplitude*32767);w.Write(v);w.Write((short)-v);}}
    }
    private static int Column(SpectrogramData d,double time){return Math.Min(d.columns-1,(int)(time*d.rate/d.hop));}
    private static double PeakFrequency(SpectrogramData d,double time){int c=Column(d,time),row=Enumerable.Range(0,d.bands).OrderByDescending(i=>d.levels[c*d.bands+i]).First();return d.Frequency(row+.5);}
    private static int Energy(SpectrogramData d,double time){return Enumerable.Range(0,d.bands).Max(i=>(int)d.levels[Column(d,time)*d.bands+i]);}
    private static SpectrogramData Spectrum(string folder)
    {
        string file=Path.Combine(folder,"tones.wav"),cache=Path.Combine(folder,"cache");Wav(file);var d=AudioSpectrogram.Load(file,cache,CancellationToken.None);
        Check(Math.Abs(d.duration-4)<.001&&d.columns>=199,"Full PCM duration and columns");
        foreach(var p in new[]{new[]{.5,100.0},new[]{1.5,1000.0},new[]{2.5,5000.0}})Check(Math.Abs(Math.Log(PeakFrequency(d,p[0])/p[1]))<.08,"Frequency/time alignment or anti-phase channel cancellation at "+p[0]);
        Check(Energy(d,2.5)>Energy(d,3.5)+45,"Brightness must reflect energy (20 dB difference)");
        var saved=AudioSpectrogram.Load(file,cache,CancellationToken.None);Check(saved.levels.SequenceEqual(d.levels),"Cached spectrum differs");
        string target=Directory.GetFiles(cache,"*.rufspec").First();File.WriteAllText(target,"broken");Check(AudioSpectrogram.Load(file,cache,CancellationToken.None).levels.SequenceEqual(d.levels),"Invalid cache must rebuild");
        var token=new CancellationTokenSource();token.Cancel();bool cancelled=false;try{AudioSpectrogram.Load(file,cache,token.Token);}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"Cancellation must work even with a saved cache");
        string before=AudioSpectrogram.SourceKey(file);File.SetLastWriteTimeUtc(file,File.GetLastWriteTimeUtc(file).AddSeconds(1));Check(before!=AudioSpectrogram.SourceKey(file),"Changed song must invalidate cache");
        var wave=AudioAnalyzer.Analyze(file);Check(wave.duration==4&&wave.peaks.Max()>.7,"Waveform decoder regression");
        var longSong=new AudioSpectrogram.Builder(1000,1,305);for(int i=0;i<305000;i++){longSong.AddChannel(0,i>=301000?Math.Sin(2*Math.PI*100*i/1000):0);longSong.EndFrame();}var tail=longSong.Finish();Check(Energy(tail,302)>200&&Energy(tail,299)==0,"Spectrum must include audio beyond the BPM analyzer's 300-second limit");
        Console.WriteLine("PASS: decoded stereo STFT, logarithmic frequency/time mapping, dB brightness, full-song coverage, cache invalidation and cancellation");return d;
    }
    private static void Timeline(string folder,SpectrogramData d)
    {
        var a=new Track{id="a",name="测试发射器",kind="emitter",start=10,end=90,lane=0};var b=new Track{id="b",name="圆环",kind="rod",start=40,end=180,lane=1};a.keyframes.Add(new Keyframe{property="x",frame=50,value=200});var state=new EditorState{duration=200,musicTitle="100 Hz → 1 kHz → 5 kHz",tracks=new List<Track>{a,b}};Motion.Normalize(state);
        using(var form=new Form{ClientSize=new Size(1100,650),BackColor=Theme.Back})using(var t=new TimelineCanvas{Dock=DockStyle.Fill,State=state,PixelsPerSecond=200}){
            form.Controls.Add(t);form.CreateControl();t.CreateControl();int edits=0,changes=0;t.BeginEdit=()=>edits++;t.Changed=()=>changes++;t.RefreshLayout();Check(t.AudioBottom==76,"Default geometry compatibility");
            using(var menu=t.CreateAudioMenu()){((ToolStripMenuItem)menu.Items[1]).PerformClick();Check(t.AudioDisplay=="spectrogram"&&t.AudioHeight==180,"Audio menu switch");}t.Spectrum=d;
            Mouse(t,"OnMouseDown",500,t.AudioBottom);Mouse(t,"OnMouseMove",500,306);Mouse(t,"OnMouseUp",500,306);Check(t.AudioHeight==280,"Audio divider drag");
            int edge=(int)t.ClipBounds(a).Bottom+3;Mouse(t,"OnMouseDown",50,edge);Mouse(t,"OnMouseMove",50,edge+70);Mouse(t,"OnMouseUp",50,edge+70);Check(t.TrackHeight(a)==100&&t.TrackHeight(b)==30,"Independent lane height");Check(edits==0&&changes==0&&a.start==10,"Resize must not edit the scene or undo stack");
            var view=Json.Read<TimelineView>(Json.Write(t.CaptureView()));using(var other=new TimelineCanvas{Size=t.Size,State=state}){other.RestoreView(view);Check(other.AudioDisplay=="spectrogram"&&other.AudioHeight==280&&other.TrackHeight(a)==100,"View persistence");}
            var rect=t.ClipBounds(b);t.SetTrackSelection(new[]{b},b);Mouse(t,"OnMouseDown",(int)rect.Right-1,(int)rect.Top+10);Mouse(t,"OnMouseMove",(int)rect.Right+39,(int)rect.Top+10);Mouse(t,"OnMouseUp",(int)rect.Right+39,(int)rect.Top+10);Check(b.end==190&&a.start==10,"Clip trim hit testing after lane resize");
            t.Expand(a,true);using(var bitmap=new Bitmap(t.Width,t.Height)){t.DrawToBitmap(bitmap,t.ClientRectangle);bitmap.Save(Path.Combine(folder,"spectrogram-timeline.png"),ImageFormat.Png);}
        }Console.WriteLine("PASS: right-click modes, independent audio/lane resizing, saved layout and trim hit testing");
    }
    private static ObjectStatisticsSnapshot Count(EditorState state,int frame,string folder)
    {Motion.Normalize(state);using(var sprites=new SpriteStore{Project=folder})using(var cache=new PreviewCache()){using(var image=PreviewRenderer.Render(state,new RoomData{width=800,height=608},new Dictionary<string,string>(),sprites,frame,false,false,cache)){}return ObjectStatistics.Capture(state,new RoomData(),frame,cache);}}
    private static void Statistics(string folder)
    {
        var emitter=new Track{id="emitter",name="发射器 A",kind="emitter",count=3,start=0,end=20,interval=5,life=10,alpha=0};var rod=new Track{id="rod",name="圆环 B",kind="rod",count=5,start=0,end=30};var drawing=new Track{id="drawing",name="装饰",kind="object",start=0,end=30};var state=new EditorState{duration=100,tracks=new List<Track>{emitter,rod,drawing}};
        var data=Count(state,10,folder);Check(data.types["objRufBullet"]==11&&data.sources["发射器 A"]==6&&data.sources["圆环 B"]==5&&data.drawOnly==1&&data.total==13,"Simple live counts/lifetime/pure drawing separation");
        emitter.advancedBullets=true;emitter.trail=true;var advanced=Count(state,10,folder);Check(advanced.total==data.total&&advanced.sources["发射器 A"]==6,"Advanced counts must agree, invisible bodies still count and trails must not add instances");
        emitter.indexDelay=3;var delayed=Count(state,0,folder);Check(delayed.sources["发射器 A"]==3,"Delayed bullets are already real GM instances before birth");
        var template=new Track{id="template",name="生成的心形",kind="rod",shape="heart",templateOnly=true,start=100,end=110,count=5};var host=new Track{id="host",name="母体",kind="object",start=0,end=20};host.spawnActions.Add(new BulletAction{type="spawn",templateId="template",start=2});
        var spawning=new EditorState{duration=100,tracks=new List<Track>{template,host}};Motion.Normalize(spawning);
        using(var sprites=new SpriteStore{Project=folder})using(var cache=new PreviewCache()){foreach(int f in new[]{2,13,2}){using(var image=PreviewRenderer.Render(spawning,new RoomData(),new Dictionary<string,string>(),sprites,f,false,false,cache)){}var s=ObjectStatistics.Capture(spawning,new RoomData(),f,cache);Check(f==2?s.types["objRufTemplate"]==1&&s.types["objRufBullet"]==5&&s.sources["生成的心形"]==5:!s.types.ContainsKey("objRufTemplate")&&!s.types.ContainsKey("objRufBullet"),"Template lifetime/counts and rewind must reuse the same simulated world correctly");}}
        var imported=new EditorState{nativeProject=new NativeProject{imported=true}};var partial=Count(imported,0,folder);Check(partial.nativeMissing&&partial.total==0,"Old native film must not invent counts");imported.tracks.Add(new Track{id="clip",kind="pattern",start=0,end=10});Check(Count(imported,0,folder).nativeMissing,"Pattern expansion must retain native import metadata");
        using(var control=new ObjectStatisticsControl{Size=new Size(340,540)}){control.CreateControl();control.SetPending(10);control.ShowStatistics(advanced);using(var bitmap=new Bitmap(control.Width,control.Height)){control.DrawToBitmap(bitmap,control.ClientRectangle);bitmap.Save(Path.Combine(folder,"object-statistics.png"),ImageFormat.Png);}
            Language.Select(folder,"en");Language.Apply(control);control.RefreshLanguage();control.ShowStatistics(advanced);var tree=control.Controls.OfType<TreeView>().Single();Check(tree.Nodes[0].Text=="GameMaker object types"&&tree.Nodes[1].Text=="RufMaker sources"&&tree.Nodes[1].Nodes.Cast<TreeNode>().Any(n=>n.Text.StartsWith("发射器 A")),"English grouping must preserve user object names");using(var menu=new TimelineCanvas().CreateAudioMenu())Check(menu.Items[0].Text=="Waveform"&&menu.Items[1].Text=="Spectrogram","Audio menu localization");using(var bitmap=new Bitmap(control.Width,control.Height)){control.DrawToBitmap(bitmap,control.ClientRectangle);bitmap.Save(Path.Combine(folder,"object-statistics-en.png"),ImageFormat.Png);}Language.Select(folder,"zh-CN");}
        Console.WriteLine("PASS: simple/advanced instance totals and source counts, delayed births, hidden bodies, draw-only objects, trails and missing native metadata");
    }
    [STAThread] public static int Main(string[] args){try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Directory.SetCurrentDirectory(args[0]);Language.Initialize(args[0]);var d=Spectrum(args[1]);Timeline(args[1],d);Statistics(args[1]);return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
