using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

internal static class PreviewPlaybackTests
{
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static void Close(double actual,double expected,double error,string message){Check(Math.Abs(actual-expected)<=error,message+": "+actual+" / "+expected);}
    private static void Transport()
    {
        var hold=new PreviewTransport();hold.Start(101,1,true);Check(hold.Advance(.2,1,50,1000)==101,"A light tap does not repeat before the hold delay");hold.Advance(.05,1,50,1000);hold.Advance(.8,1,50,1000);Close(hold.Position,123,.001,"Hold accelerates continuously");Close(hold.Velocity(1),1,.001,"Held right reaches normal playback rate");hold.Advance(1,1,50,1000);Close(hold.Position,173,.001,"Steady held playback advances at FPS");
        var coarse=new PreviewTransport();var fine=new PreviewTransport();coarse.Start(500,-1,true);fine.Start(500,-1,true);coarse.Advance(2.5,.75,50,1000);for(int i=0;i<250;i++)fine.Advance(.01,.75,50,1000);Close(coarse.Position,fine.Position,.00001,"Reverse acceleration is independent of timer cadence");Check(coarse.Position<500&&coarse.Velocity(.75)<0,"Held left reverses playback");
        coarse.Stop();double stopped=coarse.Position;coarse.Advance(5,1,50,1000);Close(coarse.Position,stopped,0,"Release stops the transport");
        var normal=new PreviewTransport();normal.Start(100,1,false);normal.Advance(2,.5,50,1000);Close(normal.Position,150,.001,"Half speed");normal.Advance(2,.75,50,1000);Close(normal.Position,225,.001,"Changing playback rate does not jump to a new origin");normal.Advance(100,1,50,1000);Check(normal.Position==1000&&!normal.Running,"Playback stops at the project end");normal.Start(1,-1,true);normal.Advance(5,1,50,1000);Check(normal.Position==0&&!normal.Running,"Reverse stops at frame zero");
        double rate;Check(PreviewTransport.TryRate("0.75×",out rate)&&rate==.75&&PreviewTransport.TryRate("1.35",out rate)&&rate==1.35,"Preset and custom rates parse");Check(!PreviewTransport.TryRate("0",out rate)&&!PreviewTransport.TryRate("NaN",out rate)&&!PreviewTransport.TryRate("oops",out rate),"Invalid rates are rejected");
    }
    private static void Snapping()
    {
        var state=new EditorState{fps=50,duration=1000,bpm=120,beatDivision=4,beatSnap=true,nativeProject=new NativeProject{events=new[]{new NativeEvent{frame=101},new NativeEvent{frame=203},new NativeEvent{frame=203}}}};
        var native=new Track{kind="native"};Check(CursorSnapping.Snap(state,native,201,3,true)==203,"Native code markers take precedence over BPM");Check(CursorSnapping.Snap(state,native,201,1,true)==200,"Outside marker tolerance falls back to BPM");Check(CursorSnapping.Snap(state,native,201,3,false)==200,"Disabling key snap preserves BPM snapping");state.beatSnap=false;Check(CursorSnapping.Snap(state,native,201,1,true)==201,"No forced snap outside tolerance");
        var track=new Track{kind="object"};track.keyframes.Add(new Keyframe{frame=202,property="x"});Check(CursorSnapping.Snap(state,track,201,3,true)==202,"RufMaker property keys remain supported");
    }
    private static void KeyLifecycle(string folder)
    {
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;using(var editor=new MainForm(folder)){
            var state=new EditorState{fps=50,duration=1000,beatSnap=true,bpm=120};typeof(MainForm).GetField("state",flags).SetValue(editor,state);typeof(MainForm).GetField("frame",flags).SetValue(editor,100);
            var begin=typeof(MainForm).GetMethod("BeginTimelineArrow",flags);begin.Invoke(editor,new object[]{Keys.Right});begin.Invoke(editor,new object[]{Keys.Right});Check((int)typeof(MainForm).GetField("frame",flags).GetValue(editor)==101,"OS key repeat cannot add extra taps or snap the one-frame step");
            var up=new KeyEventArgs(Keys.Right);typeof(MainForm).GetMethod("OnKeyUp",flags).Invoke(editor,new object[]{up});Check(up.Handled&&up.SuppressKeyPress,"Releasing a scrub key is consumed");
            begin.Invoke(editor,new object[]{Keys.Left});Check((int)typeof(MainForm).GetField("frame",flags).GetValue(editor)==100,"A left tap moves exactly one frame");typeof(MainForm).GetMethod("StopPlaying",flags).Invoke(editor,null);
        }
    }
    private static void Audio(string file)
    {
        using(var audio=new NativeAudio()){audio.Load(file);audio.SetVolume(0);foreach(double speed in new[]{.5,.75,1.35,-1.0}){
            audio.Play(2,speed);double start=audio.Position;var clock=Stopwatch.StartNew();while(clock.Elapsed.TotalSeconds<.24){audio.Update();Thread.Sleep(5);}audio.Update();double moved=audio.Position-start;Close(moved,clock.Elapsed.TotalSeconds*speed,.075,"Music follows rate "+speed);audio.Pause();}
            audio.Play(2,.5);audio.SetRate(2);audio.Seek(1.25);Close(audio.Position,1.25,.04,"Music seeks while changing speed");audio.Dispose();Check(audio.Duration==0&&!audio.Playing,"Disposed music is unavailable");}
    }
    [STAThread] public static int Main(string[] args)
    {try{Transport();Snapping();KeyLifecycle(args[0]);Audio(args[1]);Console.WriteLine("PASS: tap/hold/reverse, rate changes, marker snapping, key release and muted FMOD playback");return 0;}catch(Exception error){Console.Error.WriteLine(error);return 1;}}
}
