using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;

internal static class PreviewRenderer
{
    public static Bitmap Render(EditorState state,RoomData room,Dictionary<string,string> objectSprites,SpriteStore sprites,int frame,bool grid,bool hitboxes,PreviewCache cache=null,Matrix transform=null,int width=0,int height=0,RectangleF? displayClip=null,RectangleF? effectsFrame=null,string nativeFilmPath=null,CameraPose camera=null,bool applyEffects=true,bool roomOnly=false)
    {
        if(cache==null)using(var temporary=new PreviewCache())return Render(state,room,objectSprites,sprites,frame,grid,hitboxes,temporary,transform,width,height,displayClip,effectsFrame,nativeFilmPath,camera,applyEffects,roomOnly);
        long start=PreviewTiming.Now();var timing=new PreviewTiming();cache.Timing=timing;
        sprites.Timing=timing;
        try{return RenderScene(state,room,objectSprites,sprites,frame,grid,hitboxes,cache,transform,width,height,displayClip,effectsFrame,nativeFilmPath,camera,applyEffects,roomOnly);}
        finally{
            sprites.Timing=null;timing.Total=PreviewTiming.Since(start);
            timing.Scene=Math.Max(0,timing.Total-timing.Film-timing.Simulation-timing.Trails-timing.Bodies-timing.Effects-timing.Fused);
        }
    }
    private static Bitmap RenderScene(EditorState state,RoomData room,Dictionary<string,string> objectSprites,SpriteStore sprites,int frame,bool grid,bool hitboxes,PreviewCache cache=null,Matrix transform=null,int width=0,int height=0,RectangleF? displayClip=null,RectangleF? effectsFrame=null,string nativeFilmPath=null,CameraPose camera=null,bool applyEffects=true,bool roomOnly=false)
    {
        cache.ThrowIfCancelled();cache.UseScene(state,room);state=cache.expandedState;cache.TrimFormations(frame);
        long phase=PreviewTiming.Now();var film=cache.FilmFrame(nativeFilmPath,frame,state.fps);cache.Timing.Film+=PreviewTiming.Since(phase);
        phase=PreviewTiming.Now();bool advanced=BulletSystem.Advanced(state);if(advanced){if(cache.bulletWorld==null)cache.bulletWorld=new BulletWorld(state,room,f=>cache.FilmInfo==null?null:cache.FilmInfo.View((int)Math.Round(f*cache.FilmInfo.fps/(double)Math.Max(1,state.fps))));cache.bulletWorld.Seek(frame,cache.Cancelled);}cache.Timing.Simulation+=PreviewTiming.Since(phase);cache.ThrowIfCancelled();
        if(camera!=null&&camera.Post){
            Bitmap processed;using(var scene=RenderScene(state,room,objectSprites,sprites,frame,grid,hitboxes,cache,null,room.width,room.height,null,null,nativeFilmPath,null,false))processed=Effects.CameraFrame(scene,camera);
            if(applyEffects)processed=Effects.Apply(processed,state,frame,cache);
            if(transform==null&&width<=0&&height<=0)return processed;
            var output=new Bitmap(width>0?width:room.width,height>0?height:room.height);
            using(processed)using(var g=Graphics.FromImage(output))using(var baseMatrix=transform==null?new Matrix():transform.Clone())using(var inverse=camera.WorldToView(room.width,room.height)){
                inverse.Invert();if(transform!=null)baseMatrix.Multiply(inverse,MatrixOrder.Prepend);Theme.Workspace(g,new Rectangle(0,0,output.Width,output.Height));if(displayClip.HasValue)g.SetClip(displayClip.Value);g.Transform=baseMatrix;g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;g.DrawImage(processed,new Rectangle(0,0,room.width,room.height),0,0,processed.Width,processed.Height,GraphicsUnit.Pixel);
            }return output;
        }
        var baked=film==null?null:cache.FilmNativeIds;
        var signature=new System.Text.StringBuilder().Append(room.width).Append(':').Append(room.height).Append(':').Append(room.bgColor).Append(':').Append(grid).Append(':').Append(hitboxes);
        string roomKey=signature.ToString();
        if(cache.roomLayer==null||cache.roomKey!=roomKey)
        {
            if(cache.roomLayer!=null)cache.roomLayer.Dispose();cache.roomLayer=new Bitmap(room.width,room.height);cache.roomKey=roomKey;
            using(var g=Graphics.FromImage(cache.roomLayer))
            {
                g.Clear(Color.FromArgb(room.bgColor&255,(room.bgColor>>8)&255,(room.bgColor>>16)&255));
                g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;

            }
        }
        var size=new Size(width>0?width:room.width,height>0?height:room.height);
        Action<Graphics> paintBase=g=>{
            Theme.Workspace(g,new Rectangle(0,0,size.Width,size.Height));if(displayClip.HasValue)g.SetClip(displayClip.Value);if(transform!=null)g.Transform=transform;
            g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
            var background=film??cache.roomLayer;
            using(var edge=new System.Drawing.Imaging.ImageAttributes()){edge.SetWrapMode(WrapMode.TileFlipXY);g.DrawImage(background,new Rectangle(0,0,room.width,room.height),0,0,background.Width,background.Height,GraphicsUnit.Pixel,edge);}
            if(film==null)RoomPresentation.DrawBackgrounds(g,room,sprites,frame,false);
            var nativeView=film==null||cache.FilmInfo==null?null:cache.FilmInfo.View((int)Math.Round(frame*cache.FilmInfo.fps/(double)Math.Max(1,state.fps)));
            if(nativeView!=null&&displayClip.HasValue)g.SetClip(nativeView.Viewport(room.width,room.height),CombineMode.Intersect);
            if(nativeView!=null)using(var matrix=nativeView.WorldToRoom(room.width,room.height))g.MultiplyTransform(matrix,MatrixOrder.Prepend);
            if(grid)using(var pen=new Pen(Color.FromArgb(35,104,127,160))){for(int x=0;x<room.width;x+=32)g.DrawLine(pen,x,0,x,room.height);for(int y=0;y<room.height;y+=32)g.DrawLine(pen,0,y,room.width,y);}
            g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
        };
        Action<Graphics> paintLayers=g=>{
            var layers=new DepthLayers();
            double trailDepth=Math.Max(-6999999,state.tracks.Where(t=>t.enabled&&Theme.Visual(t.kind)).Select(t=>(double)DrawingDepth.Track(t)+1).DefaultIfEmpty(-6999999).Max());
            if(advanced)trailDepth=Math.Max(trailDepth,cache.bulletWorld.bullets.Where(b=>!b.dead).Select(b=>DrawingDepth.Bullet(b)+1).DefaultIfEmpty(-6999999).Max());
            layers.Add(trailDepth,()=>{
                long trailsStart=PreviewTiming.Now();
                Action draw=()=>{
                    if(advanced)cache.bulletWorld.Draw(g,sprites,0,hitboxes,cache.Cancelled);
                    foreach(var track in state.tracks)DrawTrack(g,state,objectSprites,sprites,frame,hitboxes,cache,advanced,track,0);
                };
                if(cache.Gpu!=null)cache.Gpu.Batch(g,sprites,size.Width,size.Height,cache.Timing,draw);else draw();
                cache.Timing.Trails+=PreviewTiming.Since(trailsStart);
            });
            var roomArt=cache.RoomArt(room);
            foreach(double depth in roomArt.Depths){
                double at=depth;
                layers.Add(at,()=>{
                    Action draw=()=>roomArt.Draw(g,objectSprites,sprites,hitboxes,baked,cache,at);
                    if(!hitboxes&&roomArt.DenseSprites(at)&&cache.Gpu!=null)cache.Gpu.Batch(g,sprites,size.Width,size.Height,cache.Timing,draw);
                    else draw();
                });
            }
            if(advanced)foreach(var group in cache.bulletWorld.bullets.Where(b=>!b.dead&&frame>=b.birth).GroupBy(DrawingDepth.Bullet)){
                var members=group;
                layers.Add(group.Key,()=>DrawBodies(g,sprites,size,cache,!hitboxes,
                    ()=>cache.bulletWorld.Draw(g,sprites,3,hitboxes,cache.Cancelled,members)));
            }
            foreach(var track in state.tracks){
                var item=track;
                if(item.kind=="object"||item.kind=="rod"||item.kind=="native-instance"||item.kind=="emitter"&&!advanced)
                    layers.Add(DrawingDepth.Track(item),()=>DrawBodies(g,sprites,size,cache,!hitboxes&&item.kind!="native-instance",
                        ()=>DrawTrack(g,state,objectSprites,sprites,frame,hitboxes,cache,advanced,item,1)));
            }
            if(!roomOnly)layers.Add(room.playerDepth,()=>DrawPlayer(g,state,sprites,frame,hitboxes));
            layers.Draw();
        };
        Bitmap bitmap=null;bool finishedEffects=false;
        try{
            // The command-only pass requires every depth layer to draw through SpriteStore.
            // GDI markers, tiled subrectangles and moving backgrounds retain their original painter.
            bool spriteScene=cache.Gpu!=null&&!hitboxes&&film==null&&room.tiles.Length==0&&
                !room.backgrounds.Any(bg=>bg.foreground||bg.speedX!=0||bg.speedY!=0)&&
                !state.tracks.Exists(t=>t.enabled&&t.kind=="native-instance"&&BulletSystem.Active(t,frame)&&
                    (!objectSprites.ContainsKey(t.nativeObject??"")||sprites.Get(objectSprites[t.nativeObject??""])==null));
            if(spriteScene){
                bitmap=cache.Gpu.Compose(sprites,cache,state,frame,cache.roomLayer,size,transform,displayClip,
                    new Size(room.width,room.height),effectsFrame,applyEffects,paintBase,paintLayers,out finishedEffects);
            }
            if(bitmap==null){
                bitmap=new Bitmap(size.Width,size.Height);
                using(var g=Graphics.FromImage(bitmap)){
                    long tick=PreviewTiming.Now();paintBase(g);cache.Timing.Backdrop+=PreviewTiming.Since(tick);
                    paintLayers(g);if(film==null)RoomPresentation.DrawBackgrounds(g,room,sprites,frame,true);
                }
            }
            Bitmap result=bitmap;
            if(applyEffects&&!finishedEffects&&effectsFrame.HasValue)
            {
                bool active=state.tracks.Exists(t=>t.enabled&&t.kind=="effect"&&frame>=t.start&&frame<=t.end);
                if(active)
                {
                    long cropStart=PreviewTiming.Now();double effectsBefore=cache.Timing.Effects;
                    var area=new Bitmap(room.width,room.height);
                    using(var g=Graphics.FromImage(area))g.DrawImage(bitmap,new RectangleF(0,0,room.width,room.height),effectsFrame.Value,GraphicsUnit.Pixel);
                    area=Effects.Apply(area,state,frame,cache);
                    using(area)using(var g=Graphics.FromImage(bitmap))g.DrawImage(area,effectsFrame.Value);
                    cache.Timing.Crop+=PreviewTiming.Since(cropStart)-(cache.Timing.Effects-effectsBefore);
                }
                else cache.ClearEffects();
            }
            else if(applyEffects&&!finishedEffects)result=Effects.Apply(bitmap,state,frame,cache);
            if(displayClip.HasValue)using(var g=Graphics.FromImage(result)){g.SetClip(displayClip.Value,CombineMode.Exclude);Theme.Workspace(g,new Rectangle(0,0,result.Width,result.Height));}
            return result;
        }
        catch{if(bitmap!=null)bitmap.Dispose();throw;}
    }

    private static void DrawBodies(Graphics g,SpriteStore sprites,Size target,PreviewCache cache,bool spriteOnly,Action draw)
    {
        long start=PreviewTiming.Now();
        if(spriteOnly&&cache.Gpu!=null)cache.Gpu.Batch(g,sprites,target.Width,target.Height,cache.Timing,draw);
        else draw();
        cache.Timing.Bodies+=PreviewTiming.Since(start);
    }

    private static void DrawTrack(Graphics g,EditorState state,Dictionary<string,string> objectSprites,SpriteStore sprites,int frame,bool hitboxes,PreviewCache cache,bool advanced,Track track,int pass)
    {
        cache.ThrowIfCancelled();
        if(!track.enabled||track.templateOnly||pass==0&&Trails.Mode(track)=="off"||advanced&&!cache.bulletWorld.Templates.Visible(track))return;Color tint=track.rainbow?Color.White:Motion.ColorOf(track.color);
        if(track.kind=="native-instance"){
            if(cache.FilmInfo!=null&&cache.FilmInfo.nativeTrackIds.Contains(track.id))return;
            if(pass==1&&BulletSystem.Active(track,frame)){
                var spawn=NativeClip.SpawnPoint(track);string sprite;
                if(objectSprites.TryGetValue(track.nativeObject??"",out sprite)&&sprites.Get(sprite)!=null)
                    sprites.Draw(g,sprite,spawn.x,spawn.y,1,0,1,Color.White);
                else using(var pen=new Pen(Theme.Muted)){float x=(float)spawn.x,y=(float)spawn.y;g.DrawRectangle(pen,x-6,y-6,12,12);}
            }
            return;
        }
        if(BulletSystem.Active(track,frame)&&(track.kind=="object"||track.kind=="rod"&&(track.releaseFrame<0||frame<track.releaseFrame)))
        {
            if(pass==0){
                int step=Trails.Interval(track);
                for(int sample=(frame/step)*step;sample>=Math.Max(track.start,frame-Trails.Life(track)+1);sample-=step){
                    if(track.kind=="object"){
                        var q=Motion.At(track,sample);
                        Trails.Draw(g,sprites,track,sample,frame-sample,q.x,q.y,q.scale,q.angle,q.alpha,tint,Rainbow.Index(track,0,sample));
                    }else{
                        var s=cache.Formation(track,sample);
                        for(int k=0;k<s.points.Length;k++)if(!advanced||cache.bulletWorld.Templates.Visible(track,k))
                            Trails.Draw(g,sprites,track,sample*301+k,frame-sample,s.points[k].X,s.points[k].Y,s.pose.scale,s.angle,s.pose.alpha,tint,s.images[k]);
                    }
                }
            }else if(track.kind=="object"){
                var p=Motion.At(track,frame);sprites.Draw(g,track.sprite,p.x,p.y,p.scale,p.angle,p.alpha,tint,Rainbow.Index(track,0,frame));
            }else{
                var s=cache.Formation(track,frame);
                for(int k=0;k<s.points.Length;k++)if(!advanced||cache.bulletWorld.Templates.Visible(track,k)){
                    var look=advanced&&cache.bulletWorld.HasFormationProperties?cache.bulletWorld.FixedMember(track,k,frame):null;
                    sprites.Draw(g,track.sprite,s.points[k].X,s.points[k].Y,look==null?s.pose.scale:look.scale,s.angle,
                        look==null?s.pose.alpha:look.alpha,look==null?tint:look.color,s.images[k]);
                }
            }
        }
        if(track.kind!="emitter")return;
        if(advanced)return;
        // Interval is track-level; speed/spread/count/lifetime are sampled independently at birth.
        int interval=Math.Max(1,track.interval),maxLife=(int)Math.Max(track.life,1);
        foreach(var key in track.keyframes)if(key.property=="life")maxLife=Math.Max(maxLife,(int)key.value);
        if(track.expressions.ContainsKey("life")&&RandomExpression.IsDynamic(track.expressions["life"])||track.keyframes.Exists(k=>k.property=="life"&&!String.IsNullOrWhiteSpace(k.expression)&&RandomExpression.IsDynamic(k.expression)))maxLife=state.duration+1;
        int first=track.start+Math.Max(0,(int)Math.Ceiling((frame-maxLife+1-track.start)/(double)interval))*interval,drawn=0;
        double ordinal=0;
        if(track.rainbow)
        {if(Motion.HasKeys(track,"count")){for(int b=track.start;b<first;b+=interval)ordinal+=Math.Max(1,Math.Min(300,(int)Math.Round(Motion.Value(track,"count",b))));}else ordinal=(first-track.start)/interval*Math.Max(1,Math.Min(300,track.count));}
        for(int birth=first;birth<=Math.Min(frame,track.end);birth+=interval)
        {
            int count=Math.Max(1,Math.Min(300,(int)Math.Round(Motion.Value(track,"count",birth))));double birthOrdinal=ordinal;ordinal+=count;
            int age=frame-birth;if(age<0||age>=Motion.Value(track,"life",birth))continue;var p=Motion.At(track,birth);
            double speed=Motion.Value(track,"speed",birth),spread=Motion.Value(track,"spread",birth);
            for(int j=0;j<count;j++,drawn++)
            {
                if((drawn&127)==0)cache.ThrowIfCancelled();
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

    internal static void DrawPlayer(Graphics g,EditorState state,SpriteStore sprites,int frame,bool hitboxes)
    {
        if(state==null)return;
        bool hasTrace=state.usePlayerTrace&&state.playerTrace!=null&&state.playerTrace.Count>0;
        if(!hasTrace)return;
        var trace=state.playerTrace;
        int lo=0,hi=trace.Count-1;
        while(lo<hi){int mid=(lo+hi+1)/2;if(trace[mid].frame<=frame)lo=mid;else hi=mid-1;}
        var pt=trace[lo];
        if(pt.dead||pt.alpha<=0)return;
        double px=pt.x,py=pt.y;
        if(lo<trace.Count-1)
        {
            var next=trace[lo+1];
            if(next.frame>pt.frame&&frame>=pt.frame)
            {
                double u=Math.Max(0,Math.Min(1,(frame-pt.frame)/(double)(next.frame-pt.frame)));
                px=pt.x+(next.x-pt.x)*u;
                py=pt.y+(next.y-pt.y)*u;
            }
        }
        string spriteName=pt.sprite;
        int subimage=pt.subimage;
        double xscale=pt.xscale!=0?pt.xscale:(pt.facing<0?-1:1);
        double yscale=pt.yscale!=0?pt.yscale:1;
        double alpha=pt.alpha>0?pt.alpha:1;
        double angle=pt.angle;
        if(sprites!=null)
        {
            if(string.IsNullOrEmpty(spriteName)||sprites.Get(spriteName,subimage)==null)
            {
                if(pt.action=="jump")spriteName="sprPlayerJump";
                else if(pt.action=="fall")spriteName="sprPlayerFall";
                else if(pt.action=="run")spriteName="sprPlayerRunning";
                else spriteName="sprPlayerIdle";
                if(sprites.Get(spriteName,subimage)==null)
                {
                    spriteName="sprPlayerIdle";
                    subimage=0;
                }
            }
            if(sprites.Get(spriteName,subimage)!=null)
            {
                sprites.DrawExt(g,spriteName,px,py,xscale,yscale,angle,alpha,Color.White,subimage);
            }
            else
            {
                if(sprites.Batch!=null)sprites.Batch.RequireGdi();
                using(var pen=new Pen(Color.FromArgb(220,0,240,255),1.5f))
                {
                    g.DrawRectangle(pen,(float)px-6,(float)py-12,12,24);
                    g.FillEllipse(Brushes.White,(float)px+(xscale>0?1:-4),(float)py-8,3,3);
                }
            }
        }
        if(hitboxes)
        {
            using(var pen=new Pen(Color.FromArgb(200,255,60,60),1f))
            {
                g.DrawRectangle(pen,(float)px-5,(float)py-12,11,21);
            }
        }
    }
}
