using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

internal static class PreviewPlaybackProfile
{
    // Pump the normal WinForms synchronization context without showing a window or sending input.
    internal static void Run(LoadData d,int start,int frames,string output)
    {
        using(var sprites=new SpriteStore{Project=d.project,Metadata=d.spriteMeta})
        using(var stage=new StageCanvas{State=d.state,Room=d.room,Sprites=sprites,ObjectSprites=d.inventory.objectSprites,
            Size=new Size(720,550),Frame=start-12,Visible=false})
        using(var canvas=new Bitmap(stage.Width,stage.Height))
        using(var g=Graphics.FromImage(canvas))
        using(var paintArgs=new PaintEventArgs(g,new Rectangle(Point.Empty,stage.Size))){
            IntPtr handle=stage.Handle;
            var method=typeof(StageCanvas).GetMethod("OnPaint",BindingFlags.Instance|BindingFlags.NonPublic);
            var paint=(Action<PaintEventArgs>)Delegate.CreateDelegate(typeof(Action<PaintEventArgs>),stage,method);
            Exception failure=null;stage.RenderFailed=e=>failure=e;
            var clock=Stopwatch.StartNew();
            stage.SceneChanged();
            for(int f=start-12;f<start;f++){
                stage.ShowFrame(f);double timeout=clock.Elapsed.TotalSeconds+10;
                while(stage.DisplayedFrame!=f){
                    Application.DoEvents();Thread.Yield();
                    if(failure!=null)throw failure;
                    if(clock.Elapsed.TotalSeconds>timeout)throw new Exception("Preview warmup timed out");
                }
                paint(paintArgs);
            }
            var elapsed=new double[frames];var worker=new double[frames];var painting=new double[frames];var shown=new bool[frames];
            double origin=clock.Elapsed.TotalMilliseconds;
            for(int i=0;i<frames;i++){
                int frame=start+i%60;stage.ShowFrame(frame);
                double due=origin+20*(i+1);
                do{
                    Application.DoEvents();
                    if(failure!=null)throw failure;
                    if(!shown[i]&&stage.DisplayedFrame==frame){
                        double paintStart=clock.Elapsed.TotalMilliseconds;
                        var saved=g.Save();try{paint(paintArgs);}finally{g.Restore(saved);}
                        painting[i]=clock.Elapsed.TotalMilliseconds-paintStart;worker[i]=stage.LastRenderMilliseconds;
                        elapsed[i]=clock.Elapsed.TotalMilliseconds-origin-20*i;shown[i]=true;
                    }
                    Thread.Yield();
                }while(clock.Elapsed.TotalMilliseconds<due);
            }
            string file=Path.Combine(output,"paced-playback.csv");
            using(var writer=new StreamWriter(file)){
                writer.WriteLine("sample,frame,painted,request_to_paint_ms_from_deadline,worker_ms,paint_ms");
                for(int i=0;i<frames;i++)writer.WriteLine(i+","+(start+i%60)+","+(shown[i]?"1":"0")+","+(shown[i]?elapsed[i].ToString("F4"):"")+","+worker[i].ToString("F4")+","+painting[i].ToString("F4"));
            }
            int within=Enumerable.Range(0,frames).Count(i=>shown[i]&&elapsed[i]<=20);
            var completed=elapsed.Where((v,i)=>shown[i]).OrderBy(v=>v).ToArray();
            if(completed.Length==0)throw new Exception("No frames reached the preview canvas");
            File.WriteAllText(Path.Combine(output,"paced-summary.json"),Json.Write(new {
                width=stage.Width,height=stage.Height,hz=50,frames,painted=completed.Length,
                onTime=within,missed=frames-completed.Length,overBudget=frames-within,
                average=completed.Average(),p95=completed[(int)Math.Ceiling(completed.Length*.95)-1],max=completed.Last()
            }));
            Console.WriteLine("50 Hz offscreen stage: "+within+"/"+frames+" within budget, "+shown.Count(v=>v)+" painted. "+file);
        }
    }
}
