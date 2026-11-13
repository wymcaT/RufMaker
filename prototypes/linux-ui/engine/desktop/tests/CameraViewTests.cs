using System;
using System.Drawing;
using System.Linq;
internal static class CameraViewTests
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Near(double actual,double expected){Check(Math.Abs(actual-expected)<.001,"Expected "+expected+", got "+actual);}
    static PointF Project(CameraPose pose,float x,float y){var p=new[]{new PointF(x,y)};using(var m=pose.WorldToView(800,600))m.TransformPoints(p);return p[0];}
    static void Main()
    {
        var room=new RoomData{width=800,height=600};
        var track=new Track{kind="camera",x=400,y=304,cameraZoom=2};
        var pose=Camera.At(track,0,room);Near(pose.Width(800),400);Near(pose.Height(600),300);
        var p=Project(pose,450,329);Near(p.X,500);Near(p.Y,350);
        track.cameraViewWidth=1400;track.cameraViewHeight=850;track.angle=90;
        pose=Camera.At(track,0,room);p=Project(pose,500,304);Near(p.X,400);Near(p.Y,300+600.0/850*100);
        track.angle=45;pose=Camera.At(track,0,room);p=Project(pose,500,354);Near(p.X,400+800.0/1400*50/Math.Sqrt(2));Near(p.Y,300+600.0/850*150/Math.Sqrt(2));
        var bounds=BulletLifetime.Bounds(pose.x,pose.y,pose.Width(800),pose.Height(600),pose.angle);
        Check(bounds.Contains(400,304,0),"Center culled");Check(!bounds.Contains(1400,1304,0),"Outside point retained");
        track.cameraViewHeight=0;pose=Camera.At(track,0,room);Near(pose.Width(800),1400);Near(pose.Height(600),300);
        Check(Motion.Fields(track).Contains("cameraViewWidth")&&Motion.Fields(track).Contains("cameraViewHeight"),"Missing timeline fields");
        Motion.EditValue(track,"cameraViewWidth",0,1234.567);Near(track.cameraViewWidth,1234.567);Check(track.keyframes.Count==0,"Base edit inserted key");
        var copy=track.Snapshot();Near(copy.cameraViewWidth,1234.567);var restored=Json.Read<Track>(Json.Write(track));Near(restored.cameraViewWidth,1234.567);
        track.keyframes.Add(new Keyframe{property="cameraViewWidth",frame=0,value=1400,easing="step"});track.keyframes.Add(new Keyframe{property="cameraViewWidth",frame=10,value=200});
        Near(Camera.At(track,9,room).Width(800),1400);Near(Camera.At(track,10,room).Width(800),200);
        Console.WriteLine("PASS: independent camera dimensions, legacy zoom, 90/45-degree markers, culling, timeline, editing, snapshot and JSON");
    }
}
