using System;
using System.Linq;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;

internal static class PreviewProfile
{
    [STAThread]
    public static int Main(string[] args)
    {
        try{Run(args);return 0;}catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
    private static void Run(string[] args)
    {
        CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
        if(args[1]=="compare"){
            PreviewProfileChecks.Verify(Path.GetDirectoryName(args[0]),int.Parse(args[3]),int.Parse(args[4]),args[0]);
            return;
        }
        if(args[1]=="fallback"){
            PreviewProfileChecks.WithoutLibrary(args[0],int.Parse(args[3]));
            return;
        }
        var d=Json.Read<LoadData>(File.ReadAllText(args[0]));
        var mode=args[1];var fixture=args[2];var output=Path.GetDirectoryName(args[0]);
        int start=int.Parse(args[3]),count=int.Parse(args[4]);
        if(fixture=="stress"){
            foreach(var t in d.state.tracks.Where(t=>t.kind=="rod"&&t.start<=start&&t.end>=start)){
                t.count=Math.Min(300,t.count*5);t.keyframes.RemoveAll(k=>k.property=="count");
            }
            foreach(string fx in new[]{"rgb","blur"})d.state.tracks.Add(new Track{id="profile-"+fx,name=fx,kind="effect",effect=fx,start=0,end=d.state.duration,strength=9,fxAngle=32,fxOpacity=.8,fxSamples=9,enabled=true});
        }
        Motion.Normalize(d.state);
        if(mode=="paced"){PreviewPlaybackProfile.Run(d,start,count,output);return;}
        using(var cache=new PreviewCache())
        using(var sprites=new SpriteStore{Project=d.project,Metadata=d.spriteMeta})
        using(var matrix=new Matrix())
        using(var csv=new StreamWriter(Path.Combine(output,fixture+"-"+mode+".csv"))){
            cache.UseGpu=mode=="gpu";
            csv.WriteLine("frame,total,scene,simulation,trails,bodies,effects,statistics,film,sprites,gpuSprites,gpuEffects,gather,submit,backdrop,crop,readbacks,fused");
            bool usedGpu=false;
            for(int f=start-12;f<start+count;f++){
                int w=d.room.width,h=d.room.height;
                using(var b=PreviewRenderer.Render(d.state,d.room,d.inventory.objectSprites,sprites,f,true,false,cache,matrix,w,h,null,new RectangleF(0,0,w,h))){
                    var t=cache.Timing;long tick=PreviewTiming.Now();ObjectStatistics.Capture(d.state,d.room,f,cache);
                    t.Statistics=PreviewTiming.Since(tick);t.Total+=t.Statistics;
                    if(f<start)continue;
                    usedGpu|=t.GpuSprites+t.GpuEffects>0;
                    csv.WriteLine(String.Join(",",new object[]{f,t.Total,t.Scene,t.Simulation,t.Trails,t.Bodies,t.Effects,t.Statistics,t.Film,t.Sprites,t.GpuSprites,t.GpuEffects,t.Gather,t.Submit,t.Backdrop,t.Crop,t.Readbacks,t.Fused}));
                    if(f==start||f==start+count/2||f==start+count-1)b.Save(Path.Combine(output,fixture+"-"+mode+"-"+f+".png"),System.Drawing.Imaging.ImageFormat.Png);
                    if(f==start)Console.WriteLine(fixture+" "+mode+" "+w+"x"+h+" "+t.Adapter);
                }
            }
            if(cache.UseGpu&&!usedGpu)throw new Exception("No accelerated frames in this sample. Hardware comparison unavailable.");
        }
    }
}
