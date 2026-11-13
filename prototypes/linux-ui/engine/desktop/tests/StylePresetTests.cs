using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

internal static class StylePresetTests
{
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static bool Same(Bitmap a,Bitmap b){if(a.Size!=b.Size)return false;for(int y=0;y<a.Height;y+=7)for(int x=0;x<a.Width;x+=7)if(a.GetPixel(x,y)!=b.GetPixel(x,y))return false;return true;}
    public static int Main(string[] args)
    {
        try{
            string scratch=args[0];Directory.CreateDirectory(scratch);var generated=new List<Track>();var state=new EditorState{fps=50,duration=1500};
            using(var scene=new Bitmap(320,240)){
                using(var g=Graphics.FromImage(scene)){g.Clear(Color.FromArgb(43,61,83));g.FillRectangle(Brushes.Orange,35,35,70,60);g.FillEllipse(Brushes.Cyan,160,70,90,90);g.FillRectangle(Brushes.White,0,220,320,20);}
                foreach(string name in StylePresets.Cameras){
                    var t=new Track{id=Guid.NewGuid().ToString("N"),kind="camera",name=name,start=10,end=10+(int)(50*StylePresets.Seconds(name)),x=160,y=120};CameraPresets.Apply(t,name,10,50);state.tracks=new List<Track>{t};Motion.Normalize(state);
                    Check(t.cameraMirror&&t.cameraStyle==name&&CameraPresets.Bound(t),"Camera style metadata missing: "+name);Check(!t.keyframes.Any(k=>CameraPresets.ControlledFields.Contains(k.property)),"Preset created camera keys");
                    var start=Camera.At(t,t.start,new RoomData{width=320,height=240});var end=Camera.At(t,t.end,new RoomData{width=320,height=240});Check(Math.Abs(start.x-end.x)<1e-8&&Math.Abs(start.angle-end.angle)<1e-8&&Math.Abs(end.zoom-1)<1e-8,"Style does not return to original camera: "+name);
                    int at=t.start+(int)((t.end-t.start)*.3);using(var image=Effects.CameraFrame(scene,Camera.At(t,at,new RoomData{width=320,height=240}))){Check(!Same(scene,image),"Preset has no visible effect: "+name);for(int y=3;y<image.Height-3;y+=5)for(int x=3;x<image.Width-3;x+=5)Check(image.GetPixel(x,y).ToArgb()!=Color.Black.ToArgb(),"Mirror border has a black gap: "+name);image.Save(Path.Combine(scratch,name+".png"));}
                    t.cameraIntensity=0;using(var image=Effects.CameraFrame(scene,Camera.At(t,at,new RoomData{width=320,height=240})))Check(Same(scene,image),"Zero camera intensity is not identity: "+name);t.cameraIntensity=1;
                    generated.Add(t);
                }
                foreach(string name in new[]{"RGB 冲击","故障切换"}){
                    var t=new Track{id=Guid.NewGuid().ToString("N"),kind="effect",name=name,start=10,end=10+(int)(50*EffectPresets.Seconds(name))};EffectPresets.Apply(t,name);state.tracks=new List<Track>{t};Motion.Normalize(state);Check(Effects.Values.Contains(t.effect),"Effect type not registered");
                    using(var cache=new PreviewCache())using(var identity=Effects.Apply((Bitmap)scene.Clone(),state,t.start,cache))Check(Same(scene,identity),"Effect starts with a pop: "+name);
                    using(var cache=new PreviewCache())using(var image=Effects.Apply((Bitmap)scene.Clone(),state,t.start+(t.end-t.start)/3,cache)){Check(!Same(scene,image),"Compound effect is invisible: "+name);image.Save(Path.Combine(scratch,name+".png"));}generated.Add(t);
                }
            }
            foreach(var name in new[]{"edit-smooth","edit-snap","edit-back","edit-elastic"}){Check(Motion.Ease(name,0)==0&&Motion.Ease(name,1)==1,"Curve endpoints changed");for(int i=0;i<=100;i++)Check(!Double.IsNaN(Motion.Ease(name,i/100.0)),"Invalid style easing");}
            var cameras=generated.Where(t=>t.kind=="camera").ToArray();var portable=TrackPreset.Capture("styles",generated,50);var copies=portable.Instantiate(500,100);Check(copies.Count==6&&copies.Count(t=>!String.IsNullOrEmpty(t.cameraStyle))==4,"Custom preset loses style data");
            state.tracks=generated;File.WriteAllText(Path.Combine(scratch,"styles.json"),Json.Write(state));Console.WriteLine("PASS: parameter-driven camera styles without keys, mirrored borders, zero strength identity, compound FX, easing endpoints and custom preset round trip.");return 0;
        }catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
