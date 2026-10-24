using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

internal static class PreviewRenderer
{
    public static Bitmap Render(EditorState state,RoomData room,Dictionary<string,string> objectSprites,SpriteStore sprites,int frame,bool grid,bool hitboxes,PreviewCache cache=null,Matrix transform=null,int width=0,int height=0,RectangleF? displayClip=null,RectangleF? effectsFrame=null,string nativeFilmPath=null,CameraPose camera=null,bool applyEffects=true)
    {
        if(cache==null)using(var temporary=new PreviewCache())return Render(state,room,objectSprites,sprites,frame,grid,hitboxes,temporary,transform,width,height,displayClip,effectsFrame,nativeFilmPath,camera,applyEffects);
        if(cache.expandedState==null){cache.expandedState=BulletSystem.Expand(state);BulletSystem.Attach(cache.expandedState);}state=cache.expandedState;
        bool advanced=BulletSystem.Advanced(state);if(advanced){if(cache.bulletWorld==null)cache.bulletWorld=new BulletWorld(state);cache.bulletWorld.Seek(frame);}
        if(camera!=null&&camera.Post){
            Bitmap processed;using(var scene=Render(state,room,objectSprites,sprites,frame,grid,hitboxes,cache,null,room.width,room.height,null,null,nativeFilmPath,null,false))processed=Effects.CameraFrame(scene,camera);
            if(applyEffects)processed=Effects.Apply(processed,state,frame,cache);
            if(transform==null&&width<=0&&height<=0)return processed;
            var output=new Bitmap(width>0?width:room.width,height>0?height:room.height);
            using(processed)using(var g=Graphics.FromImage(output))using(var baseMatrix=transform==null?new Matrix():transform.Clone())using(var inverse=camera.WorldToView(room.width,room.height)){
                inverse.Invert();if(transform!=null)baseMatrix.Multiply(inverse,MatrixOrder.Prepend);Theme.Workspace(g,new Rectangle(0,0,output.Width,output.Height));if(displayClip.HasValue)g.SetClip(displayClip.Value);g.Transform=baseMatrix;g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;g.DrawImage(processed,new Rectangle(0,0,room.width,room.height),0,0,processed.Width,processed.Height,GraphicsUnit.Pixel);
            }return output;
        }
        var film=cache.FilmFrame(nativeFilmPath,frame,state.fps);var baked=film==null?null:cache.FilmNativeIds;
        var signature=new System.Text.StringBuilder().Append(room.width).Append(':').Append(room.height).Append(':').Append(room.bgColor).Append(':').Append(grid).Append(':').Append(hitboxes);
        string roomKey=signature.ToString();
        if(cache.roomLayer==null||cache.roomKey!=roomKey)
        {
            if(cache.roomLayer!=null)cache.roomLayer.Dispose();cache.roomLayer=new Bitmap(room.width,room.height);cache.roomKey=roomKey;
            using(var g=Graphics.FromImage(cache.roomLayer))
            {
                g.Clear(Color.FromArgb(room.bgColor&255,(room.bgColor>>8)&255,(room.bgColor>>16)&255));
                g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
                if(grid)using(var pen=new Pen(Color.FromArgb(35,104,127,160))){for(int x=0;x<room.width;x+=32)g.DrawLine(pen,x,0,x,room.height);for(int y=0;y<room.height;y+=32)g.DrawLine(pen,0,y,room.width,y);}

            }
        }
        var bitmap=new Bitmap(width>0?width:room.width,height>0?height:room.height);
        try
        {
            using(var g=Graphics.FromImage(bitmap))
            {
                Theme.Workspace(g,new Rectangle(0,0,bitmap.Width,bitmap.Height));if(displayClip.HasValue)g.SetClip(displayClip.Value);if(transform!=null)g.Transform=transform;
                g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
                var background=film??cache.roomLayer;
                using(var edge=new System.Drawing.Imaging.ImageAttributes()){edge.SetWrapMode(WrapMode.TileFlipXY);g.DrawImage(background,new Rectangle(0,0,room.width,room.height),0,0,background.Width,background.Height,GraphicsUnit.Pixel,edge);}
                var nativeView=film==null||cache.FilmInfo==null?null:cache.FilmInfo.View((int)Math.Round(frame*cache.FilmInfo.fps/(double)Math.Max(1,state.fps)));
                if(nativeView!=null&&displayClip.HasValue)g.SetClip(nativeView.Viewport(room.width,room.height),CombineMode.Intersect);
                if(nativeView!=null)using(var matrix=nativeView.WorldToRoom(room.width,room.height))g.MultiplyTransform(matrix,MatrixOrder.Prepend);
                if(film!=null&&grid)using(var pen=new Pen(Color.FromArgb(35,104,127,160))){for(int x=0;x<room.width;x+=32)g.DrawLine(pen,x,0,x,room.height);for(int y=0;y<room.height;y+=32)g.DrawLine(pen,0,y,room.width,y);}
                g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
                for(int pass=0;pass<2;pass++)
                {
                if(advanced)cache.bulletWorld.Draw(g,sprites,pass,hitboxes);
                if(pass==1)foreach(var instance in room.instances)
                {if(baked!=null&&baked.Contains(instance.id))continue;string sprite;if(!objectSprites.TryGetValue(instance.name,out sprite)||String.IsNullOrEmpty(sprite))continue;sprites.Draw(g,sprite,instance.x,instance.y,1,0,1,Color.White);if(hitboxes)using(var pen=new Pen(Color.FromArgb(110,94,212,197))){var r=sprites.Bounds(sprite,instance.x,instance.y);g.DrawRectangle(pen,r.X,r.Y,r.Width,r.Height);}}
                foreach(var track in state.tracks)
                {
                    if(!track.enabled||track.templateOnly||advanced&&!cache.bulletWorld.Templates.Visible(track))continue;Color tint=track.rainbow?Color.White:Motion.ColorOf(track.color);
                    if(BulletSystem.Active(track,frame)&&(track.kind=="object"||track.kind=="rod"&&(track.releaseFrame<0||frame<track.releaseFrame)))
                    {
                        var p=Motion.At(track,frame);
                        if(pass==0&&Trails.Mode(track)!="off")
                        for(int sample=(frame/Trails.Interval(track))*Trails.Interval(track);sample>=Math.Max(track.start,frame-Trails.Life(track)+1);sample-=Trails.Interval(track))
                        {
                            var q=Motion.At(track,sample);int tail=frame-sample;
                            if(track.kind=="object")Trails.Draw(g,sprites,track,sample,tail,q.x,q.y,q.scale,q.angle,q.alpha,tint,Rainbow.Index(track,0,sample));
                            else {var points=Motion.RodPoints(track,sample);for(int k=0;k<points.Length;k++)if(!advanced||cache.bulletWorld.Templates.Visible(track,k))Trails.Draw(g,sprites,track,sample*301+k,tail,points[k].X,points[k].Y,q.scale,Motion.Value(track,"spriteAngle",sample)+(track.rotateSprite?q.angle:0),q.alpha,tint,Rainbow.Index(track,Rainbow.RodSequence(track,k,points.Length,sample),sample));}
                        }
                        if(pass==1){if(track.kind=="object")sprites.Draw(g,track.sprite,p.x,p.y,p.scale,p.angle,p.alpha,tint,Rainbow.Index(track,0,frame));
                        else {var points=Motion.RodPoints(track,frame);for(int k=0;k<points.Length;k++)if(!advanced||cache.bulletWorld.Templates.Visible(track,k))sprites.Draw(g,track.sprite,points[k].X,points[k].Y,p.scale,Motion.Value(track,"spriteAngle",frame)+(track.rotateSprite?p.angle:0),p.alpha,tint,Rainbow.Index(track,Rainbow.RodSequence(track,k,points.Length,frame),frame));}}
                    }
                    if(track.kind!="emitter")continue;
                    if(advanced)continue;
                    // Interval is track-level; speed/spread/count/lifetime are sampled independently at birth.
                    int interval=Math.Max(1,track.interval),maxLife=(int)Math.Max(track.life,1);
                    foreach(var key in track.keyframes)if(key.property=="life")maxLife=Math.Max(maxLife,(int)key.value);
                    if(track.expressions.ContainsKey("life")&&RandomExpression.IsDynamic(track.expressions["life"])||track.keyframes.Exists(k=>k.property=="life"&&!String.IsNullOrWhiteSpace(k.expression)&&RandomExpression.IsDynamic(k.expression)))maxLife=state.duration+1;
                    int first=track.start+Math.Max(0,(int)Math.Ceiling((frame-maxLife+1-track.start)/(double)interval))*interval,drawn=0;
                    double ordinal=0;
                    if(track.rainbow)
                    {if(Motion.HasKeys(track,"count")){for(int b=track.start;b<first;b+=interval)ordinal+=Math.Max(1,Math.Min(300,(int)Math.Round(Motion.Value(track,"count",b))));}else ordinal=(first-track.start)/interval*Math.Max(1,Math.Min(300,track.count));}
                    for(int birth=first;birth<=Math.Min(frame,track.end)&&drawn<20000;birth+=interval)
                    {
                        int count=Math.Max(1,Math.Min(300,(int)Math.Round(Motion.Value(track,"count",birth))));double birthOrdinal=ordinal;ordinal+=count;
                        int age=frame-birth;if(age<0||age>=Motion.Value(track,"life",birth))continue;var p=Motion.At(track,birth);
                        double speed=Motion.Value(track,"speed",birth),spread=Motion.Value(track,"spread",birth);
                        for(int j=0;j<count&&drawn<20000;j++,drawn++)
                        {
                            double angle=p.angle+(j-(count-1)/2.0)*spread,rad=angle*Math.PI/180,dx=Math.Cos(rad)*speed,dy=-Math.Sin(rad)*speed;
                            var path=BulletFlight.Curved(track)?BulletFlight.Path(track,birth,age,p.x,p.y,angle,speed,j):null;double x=path==null?p.x+dx*age:path[age].x,y=path==null?p.y+dy*age:path[age].y;
                            double sequence=birthOrdinal+j;
                            double imageAngle=Motion.Value(track,"spriteAngle",birth)+(track.rotateSprite?(path==null?angle:path[age].angle):0);
                            if(pass==0&&Trails.Mode(track)!="off")for(int sample=(age/Trails.Interval(track))*Trails.Interval(track);sample>=Math.Max(0,age-Trails.Life(track)+1);sample-=Trails.Interval(track))
                                Trails.Draw(g,sprites,track,(birth+sample)*301+j,age-sample,path==null?p.x+dx*sample:path[sample].x,path==null?p.y+dy*sample:path[sample].y,p.scale,Motion.Value(track,"spriteAngle",birth)+(track.rotateSprite?(path==null?angle:path[sample].angle):0),p.alpha,tint,Rainbow.Index(track,sequence,birth+sample,birth));
                            if(pass==1)sprites.Draw(g,track.sprite,x,y,p.scale,imageAngle,p.alpha,tint,Rainbow.Index(track,sequence,frame,birth));
                        }
                    }
                }
                }
            }
            Bitmap result=bitmap;
            if(applyEffects&&effectsFrame.HasValue)
            {
                bool active=state.tracks.Exists(t=>t.enabled&&t.kind=="effect"&&frame>=t.start&&frame<=t.end);
                if(active)
                {
                    var area=new Bitmap(room.width,room.height);
                    using(var g=Graphics.FromImage(area))g.DrawImage(bitmap,new RectangleF(0,0,room.width,room.height),effectsFrame.Value,GraphicsUnit.Pixel);
                    area=Effects.Apply(area,state,frame,cache);
                    using(area)using(var g=Graphics.FromImage(bitmap))g.DrawImage(area,effectsFrame.Value);
                }
            }
            else if(applyEffects)result=Effects.Apply(bitmap,state,frame,cache);
            if(displayClip.HasValue)using(var g=Graphics.FromImage(result)){g.SetClip(displayClip.Value,CombineMode.Exclude);Theme.Workspace(g,new Rectangle(0,0,result.Width,result.Height));}
            return result;
        }
        catch{bitmap.Dispose();throw;}
    }
}
