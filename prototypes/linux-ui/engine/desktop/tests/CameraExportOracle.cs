using System;
using System.Drawing;
using System.Collections.Generic;
internal static class CameraExportOracle {
 static void Main(){
 var result=new List<object>();
 foreach(double angle in new[]{0.0,37.0,90.0})foreach(double width in new[]{0.0,640.0,-10.0})foreach(double height in new[]{0.0,250.0})foreach(int frame in new[]{0,5,10}){
 var t=new Track{id="camera",kind="camera",x=430,y=270,angle=angle,cameraZoom=2,cameraViewWidth=width,cameraViewHeight=height};
 t.keyframes.Add(new Keyframe{property="cameraViewWidth",frame=0,value=width,easing="linear"});t.keyframes.Add(new Keyframe{property="cameraViewWidth",frame=10,value=width+100,easing="linear"});
 var pose=Camera.At(t,frame,new RoomData{width=800,height=600});var points=new[]{new PointF(530,320)};
 using(var matrix=pose.WorldToView(800,600))matrix.TransformPoints(points);
 var view=BulletLifetime.Bounds(pose.x,pose.y,pose.Width(800),pose.Height(600),pose.angle);
 var meta=new SpriteMeta{originX=2,originY=3,colliders=new[]{new SpriteCollider{left=-10,top=-5,right=20,bottom=15}}};
 var box=SpriteMaskLifetime.Bounds(meta,0,530,320,2,-.5,27,view);
 var corners=pose.Corners(800,600);double minx=Double.MaxValue,maxx=Double.MinValue,miny=Double.MaxValue,maxy=Double.MinValue;foreach(var corner in corners){minx=Math.Min(minx,corner.X);maxx=Math.Max(maxx,corner.X);miny=Math.Min(miny,corner.Y);maxy=Math.Max(maxy,corner.Y);}
 result.Add(new{angle,width,height,frame,minx,maxx,miny,maxy,box,maskOutside=SpriteMaskLifetime.Outside(box,pose.Width(800),pose.Height(600),5,true),effectiveWidth=pose.Width(800),effectiveHeight=pose.Height(600),projectX=points[0].X,projectY=points[0].Y});
 }
 Console.WriteLine(Json.Write(result));
 }
}
