using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ComponentModel;
using System.Windows.Forms;
internal static class TemplateSystemTests
{
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static object Rows(BulletWorld w){return w.bullets.Where(b=>b.birth<=w.Frame&&!b.dead).OrderBy(b=>b.source.id).ThenBy(b=>b.birth).ThenBy(b=>b.index).ThenBy(b=>b.x).Select(b=>new{source=b.source.id,birth=b.birth,index=b.index,generation=b.generation,x=b.x,y=b.y,direction=b.direction,speed=b.speed,scale=b.scale*BulletLifetime.Scale(b),alpha=b.alpha*BulletLifetime.Alpha(b),kinematic=b.kinematic,clockShift=b.clockShift,sourceX=b.sourceX,sourceY=b.sourceY,collision=b.collision,deathStart=b.deathStart,sprite=b.sprite??b.source.sprite,color=b.color.R+b.color.G*256+b.color.B*65536,depth=b.generation>0?-7000000-b.generation:-100,image=b.frozenImage>=0?b.frozenImage:Rainbow.Index(b.source,b.sequence,w.Frame-b.clockShift,(b.kinematic?w.Frame:b.birth)-b.clockShift)}).ToArray();}
    private static EditorState Read(string file){var state=Json.Read<EditorState>(File.ReadAllText(file));Motion.Normalize(state);return state;}
    private static void RenderGallery(string stateFile,string project,string folder)
    {
        Directory.CreateDirectory(folder);var s=Read(stateFile);var metadata=Directory.GetDirectories(Path.Combine(project,"sprites")).ToDictionary(Path.GetFileName,dir=>{
            var values=File.ReadAllLines(Path.Combine(dir,"sprite.txt")).Where(line=>line.Contains("=")).Select(line=>line.Split(new[]{'='},2)).ToDictionary(pair=>pair[0],pair=>pair[1]);return new SpriteMeta{frames=Int32.Parse(values["frames"]),originX=Int32.Parse(values["origin_x"]),originY=Int32.Parse(values["origin_y"])};});
        var objects=Directory.GetFiles(Path.Combine(project,"objects"),"*.txt").ToDictionary(Path.GetFileNameWithoutExtension,file=>File.ReadAllLines(file).Where(line=>line.StartsWith("sprite=")).Select(line=>line.Substring(7)).FirstOrDefault()??"");
        var room=new RoomData();var roomFolder=Path.Combine(project,"rooms",s.room);foreach(var line in File.ReadAllLines(Path.Combine(roomFolder,"instances.txt"))){var c=line.Split(',');if(c.Length>=3)room.instances.Add(new RoomInstance{name=c[0],x=Double.Parse(c[1],System.Globalization.CultureInfo.InvariantCulture),y=Double.Parse(c[2],System.Globalization.CultureInfo.InvariantCulture)});}
        var chapters=Json.Read<List<Dictionary<string,object>>>(File.ReadAllText(Path.Combine(Path.GetDirectoryName(project),"章节索引.json")));
        using(var sprites=new SpriteStore{Project=project,Metadata=metadata})using(var cache=new PreviewCache())using(var sheet=new Bitmap(900,1040))using(var g=Graphics.FromImage(sheet)){
            g.Clear(Theme.Back);for(int i=0;i<chapters.Count;i++){
                int frame=Convert.ToInt32(chapters[i]["preview"]),column=i%3,row=i/3;string name=Convert.ToString(chapters[i]["name"]);
                using(var image=PreviewRenderer.Render(s,room,objects,sprites,frame,false,false,cache)){image.Save(Path.Combine(folder,(i+1).ToString("00")+".png"));g.DrawImage(image,new Rectangle(column*300,row*260,300,228));}
                Theme.Label(g,(i+1).ToString("00")+" · "+name+"  "+(i*8)+"–"+(i*8+8)+"s",new Rectangle(column*300+5,row*260+228,290,32),Theme.Text,Theme.Small,localize:false);
            }
            Theme.Label(g,"11 章 · 88 秒 · 50 FPS\n参数、关键帧、行为均可编辑",new Rectangle(610,850,280,100),Theme.Muted,Theme.Font,TextFormatFlags.Left|TextFormatFlags.WordBreak,localize:false);sheet.Save(Path.Combine(folder,"gallery-overview.png"));
        }
    }
    private static void Lifetimes(BulletWorld w)
    {
        Func<string,SimBullet> one=id=>w.bullets.Single(b=>b.source.id==id);Func<string,bool> alive=id=>w.bullets.Any(b=>b.source.id==id);
        w.Seek(0);Check(!alive("life-born-outside")&&one("life-inset").deathStart==0,"Birth-outside or negative margin did not trigger removal");
        w.Seek(2);Check(one("life-room").deathStart==2&&!one("life-room").collision&&!alive("life-room-none")&&alive("life-room-vs-view"),"Room exit timing, collision or nonfixed life differs");
        w.Seek(3);Check(one("life-view").deathStart==3&&!alive("life-inset"),"Rotated/zoomed camera exit or pending duration differs");
        w.Seek(4);var room=one("life-room");Check(Math.Abs(BulletLifetime.Scale(room)-.5)<1e-9&&Math.Abs(BulletLifetime.Alpha(room)-.5)<1e-9,"Room death transition compounds or misses halfway");
        w.Seek(5);Check(Math.Abs(BulletLifetime.Alpha(one("life-view"))-.5)<1e-9&&Math.Abs(BulletLifetime.Alpha(one("life-clear"))-.5)<1e-9&&alive("life-room-vs-view"),"View/explicit fade or room-vs-camera distinction differs");
        w.Seek(6);Check(!alive("life-room"),"Boundary transition did not end");w.Seek(7);Check(!alive("life-view")&&!alive("life-clear"),"Explicit/view transition did not end");
        w.Seek(8);var fixedBullet=one("life-fixed");Check(Math.Abs(fixedBullet.scale*BulletLifetime.Scale(fixedBullet)-1)<1e-9&&Math.Abs(fixedBullet.alpha*BulletLifetime.Alpha(fixedBullet)-.4)<1e-9,"Fixed-life transition should fade before expiry without accumulating");
        w.Seek(10);Check(!alive("life-fixed")&&!alive("life-clear-overlap"),"Fixed/overlapping destruction did not complete");
        w.Seek(34);var shots=w.bullets.Where(b=>b.source.id=="life-stagger-shot").OrderBy(b=>b.birth).ToArray();Check(shots.Length==8,"Periodic per-member generation must create two rounds");
        for(int i=0;i<4;i++)foreach(int round in new[]{0,1}){var b=shots.Single(x=>x.birth==5+i*3+round*20);Check(Math.Abs(b.speed-3)<1e-9&&Math.Abs(b.direction-(10+i*15))<1e-9,"Staggered members lost uniform speed or increasing direction");}
        var smallTrack=new Track{id="small",kind="emitter",start=0,end=0,count=1,x=315,y=100,speed=5,lifetimeMode="room"};var s=new EditorState{tracks=new System.Collections.Generic.List<Track>{smallTrack}};Motion.Normalize(s);var small=new BulletWorld(s,new RoomData{width=320,height=240});small.Seek(1);Check(small.bullets.Count==1,"Room edge should be inclusive");small.Seek(2);Check(small.bullets.Count==0,"Lifetime ignored actual room dimensions");
        smallTrack.x=60;smallTrack.y=50;smallTrack.speed=10;smallTrack.lifetimeMode="view";var native=new BulletWorld(s,new RoomData(),f=>new NativeViewPose{enabled=true,x=0,y=0,w=100,h=100});native.Seek(4);Check(native.bullets.Count==1,"Native view edge should be inclusive");native.Seek(5);Check(native.bullets.Count==0,"Native view fallback did not cull at the actual viewport");
        Console.WriteLine("PASS: fixed/room/view life, camera rotation/zoom, room dimensions/native view, shrink/fade, explicit clear, margins, periodic staggered uniform-speed generation and direction increments.");
    }
    private static void Appearance(BulletWorld w)
    {
        w.Seek(5);var source=w.State.tracks.Single(t=>t.id=="look-host");var pose=Motion.At(source,5);var points=Motion.RodPoints(source,5);var shots=w.bullets.Where(b=>b.source.id=="look-shot"&&Math.Abs(b.sourceX-300)<.001).OrderBy(b=>b.x).ToArray();Check(shots.Length==5,"Appearance inheritance lost members");
        foreach(var b in shots){int member=Array.FindIndex(points,p=>Math.Abs(p.X-b.x)<.001&&Math.Abs(p.Y-b.y)<.001);Check(b.sprite==source.sprite&&b.frozenImage==Rainbow.Index(source,Rainbow.RodSequence(source,member,5,5,points[member],pose),5)&&Math.Abs(b.scale-1.3)<.00001,"Source sprite/rainbow frame/size was not captured at birth");}
        int[] colors=shots.Select(b=>b.frozenImage).ToArray();w.Seek(20);Check(shots.Select(b=>b.frozenImage).SequenceEqual(colors),"Inherited rainbow kept animating");var animated=w.bullets.Single(b=>b.source.id=="look-fixed");Check(Math.Abs(animated.scale-2.4)<.00001,"Inherited size discarded the template scale animation");
        foreach(int frame in new[]{11,31}){w.Seek(frame);foreach(var group in w.bullets.Where(b=>b.source.id=="color-wave").GroupBy(b=>b.volley))Check(group.Select(b=>b.color).Distinct().Count()==1,"A wave sampled colors per bullet");var children=w.bullets.Where(b=>b.source.id=="look-shot"&&Math.Abs(b.sourceX-200)<.001).GroupBy(b=>b.birth<20);foreach(var group in children)Check(group.Select(b=>b.color).Distinct().Count()==1,"Staggered members did not share one wave color");}
        var host=w.State.tracks.Single(t=>t.id=="life-stagger-host");var marks=TimelineCanvas.SpawnFrames(host,host.spawnActions[0],0,50).OrderBy(f=>f).ToArray();Check(marks.SequenceEqual(new[]{5,8,11,14,25,28,31,34}),"Song timeline spawn markers missed repeat/stagger times");
        var copy=host.Snapshot();copy.start+=100;copy.end+=100;Check(TimelineCanvas.SpawnFrames(copy,copy.spawnActions[0],0,200).OrderBy(f=>f).SequenceEqual(marks.Select(f=>f+100)),"Moving a clip did not move its spawn markers");
        Console.WriteLine("PASS: source sprite/frozen rainbow/color/scale inheritance, keyed size, shared wave colors and song-time spawn markers");
    }
    private static void AppearancePixels(string folder)
    {
        string directory=Path.Combine(folder,"sprites","white-test");Directory.CreateDirectory(directory);using(var sprite=new Bitmap(12,12)){using(var g=Graphics.FromImage(sprite))g.Clear(Color.White);sprite.Save(Path.Combine(directory,"0.png"));}
        var target=new Track{id="front-child",kind="emitter",templateOnly=true,start=0,end=0,x=0,y=0,count=1,speed=0,life=20,sprite="white-test",color="#0000FF"};
        var source=new Track{id="back-source",kind="rod",start=0,end=20,count=1,x=50,y=50,length=0,sprite="white-test",color="#FF0000"};source.spawnActions.Add(new BulletAction{type="spawn",templateId=target.id,start=0,duration=0});var s=new EditorState{tracks=new List<Track>{source,target}};Motion.Normalize(s);
        using(var sprites=new SpriteStore{Project=folder})using(var image=PreviewRenderer.Render(s,new RoomData{width=100,height=100},new Dictionary<string,string>(),sprites,0,false,false)){Color pixel=image.GetPixel(55,55);Check(pixel.B>240&&pixel.R<10,"Newly spawned bullets were drawn behind the source formation");image.Save(Path.Combine(folder,"spawn-layer.png"));}
        Console.WriteLine("PASS: generated bullet pixels render above the original formation");
    }
    private static void SpawnTimelineUi(string folder,EditorState state)
    {
        var host=state.tracks.Single(t=>t.id=="life-stagger-host");using(var timeline=new TimelineCanvas{State=state,Size=new Size(1000,420)}){
            timeline.State=new EditorState{fps=50,duration=100,tracks=new List<Track>{host.Snapshot()}};timeline.State.tracks[0].lane=0;timeline.State.laneCount=1;timeline.Expand(timeline.State.tracks[0],true);timeline.CreateControl();
            var lanes=(System.Collections.IEnumerable)typeof(TimelineCanvas).GetField("lanes",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(timeline);object row=lanes.Cast<object>().Single(l=>(string)l.GetType().GetField("property").GetValue(l)=="@spawn:0");int y=Convert.ToInt32(row.GetType().GetField("top").GetValue(row))+Convert.ToInt32(typeof(TimelineCanvas).GetProperty("Header",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(timeline,null))+15;
            bool edited=false;timeline.EditSpawnRule=(t,i)=>{edited=t==timeline.State.tracks[0]&&i==0;};typeof(TimelineCanvas).GetMethod("OnMouseDoubleClick",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(timeline,new object[]{new MouseEventArgs(MouseButtons.Left,2,210,y,0)});Check(edited,"Double-clicking a spawn row did not edit its own rule");
            using(var image=new Bitmap(timeline.Width,timeline.Height)){timeline.DrawToBitmap(image,timeline.ClientRectangle);Check(image.GetPixel(198,y).G>150,"Spawn timeline row did not paint its trigger markers");image.Save(Path.Combine(folder,"spawn-timeline.png"));}
        }
    }
    private static void GenerationGroupsUi(string folder)
    {
        var template=new Track{id="linked-template",name="彩虹星星扩散弹模板 · 共享的完整名称",kind="emitter",templateOnly=true,lane=3,start=0,end=20,count=1};Motion.SetKey(template,"scale",0,1);Motion.SetKey(template,"scale",20,2);
        template.spawnActions.Add(new BulletAction{type="spawn",templateId=template.id,start=10,duration=0,maxGeneration=3});
        var source=new Track{id="source-a",name="周期星星 · 物件与生成行为组",kind="rod",lane=0,start=400,end=700};source.spawnActions.Add(new BulletAction{type="spawn",templateId=template.id,start=50,duration=100,spawnRepeat=50});
        var other=new Track{id="source-b",name="移动母弹 · 共用模板",kind="emitter",lane=1,start=800,end=1000};other.bulletActions.Add(new BulletAction{type="spawn",templateId=template.id,start=10,duration=0});
        var unused=new Track{id="unused",name="尚未引用的单发模板也有完整宽度",kind="emitter",templateOnly=true,lane=5,start=0,end=0};
        var scene=new EditorState{duration=1500,fps=50,tracks=new List<Track>{source,other,template,unused}};Motion.Normalize(scene);TimelineLayout.Normalize(scene);string before=Json.Write(scene);
        using(var timeline=new TimelineCanvas{State=scene,Size=new Size(1200,720),PixelsPerSecond=20}){
            timeline.RefreshLayout();timeline.CreateControl();var flags=BindingFlags.NonPublic|BindingFlags.Instance;
            Func<object[]> rows=()=>((System.Collections.IEnumerable)typeof(TimelineCanvas).GetField("lanes",flags).GetValue(timeline)).Cast<object>().ToArray();
            Func<object,string,object> field=(row,name)=>row.GetType().GetField(name).GetValue(row);
            Func<object,bool> card=row=>(string)field(row,"property")=="@template";var initial=rows();Check(initial.Length<20,"Recursive template references expanded without a stopping point");
            Check(!initial.Any(r=>Convert.ToInt32(field(r,"index"))==3),"Referenced template remained on a separate track");
            Check(initial.Any(r=>card(r)&&field(r,"track")==template&&Convert.ToInt32(field(r,"index"))==0)&&initial.Any(r=>card(r)&&field(r,"track")==template&&Convert.ToInt32(field(r,"index"))==1),"Shared template was not bound to both source tracks");
            Check(TimelineLayout.TemplateDisplayLanes(scene,template).SequenceEqual(new[]{0,1}),"Inspector template lanes do not match source groups");
            timeline.Expand(source,false);Check(!rows().Any(r=>card(r)&&field(r,"track")==template&&Convert.ToInt32(field(r,"index"))==0),"Collapsing the source left its template cards behind");var view=timeline.CaptureView();timeline.Expand(source,true);timeline.RestoreView(view);Check(!rows().Any(r=>card(r)&&field(r,"track")==template&&Convert.ToInt32(field(r,"index"))==0),"Group collapse state was not restored");timeline.Expand(source,true);
            var reference=rows().First(r=>card(r)&&field(r,"track")==template&&field(r,"generationSource")==source);int header=Convert.ToInt32(typeof(TimelineCanvas).GetProperty("Header",flags).GetValue(timeline,null)),y=header+Convert.ToInt32(field(reference,"top"))+20;
            var bounds=(Rectangle)typeof(TimelineCanvas).GetMethod("TemplateCardBounds",flags).Invoke(timeline,new object[]{reference,header+Convert.ToInt32(field(reference,"top")),1183});Check(bounds.Width>=360,"Short template definition still renders as an unreadable sliver");
            typeof(TimelineCanvas).GetMethod("OnMouseDown",flags).Invoke(timeline,new object[]{new MouseEventArgs(MouseButtons.Left,1,bounds.Left+70,y,0)});Check(timeline.SelectedTrack==template&&timeline.ActiveLane==0,"Template card did not select the real template in the source track");
            bool edited=false;timeline.EditTemplateDefinition=t=>{edited=t==template;timeline.Frame=t.start;};typeof(TimelineCanvas).GetMethod("OnMouseDoubleClick",flags).Invoke(timeline,new object[]{new MouseEventArgs(MouseButtons.Left,2,bounds.Left+70,y,0)});Check(edited&&rows().Any(r=>field(r,"track")==template&&(string)field(r,"property")=="scale"),"Double-click did not expose the template's original keyframes");
            File.WriteAllText(Path.Combine(folder,"generation-before.json"),before);File.WriteAllText(Path.Combine(folder,"generation-after.json"),Json.Write(scene));Check(SceneComparison.Same(Json.Read<EditorState>(before),scene),"Visual grouping changed clip times, lanes, references or keyframes");timeline.Expand(template,false);timeline.ResetSelection(source,null);var groupHeader=rows().First(r=>field(r,"track")==source&&(string)field(r,"property")=="@item");int groupY=header+Convert.ToInt32(field(groupHeader,"top"))+15;foreach(var gesture in new[]{new{method="OnMouseDown",x=410},new{method="OnMouseMove",x=430},new{method="OnMouseUp",x=430}})typeof(TimelineCanvas).GetMethod(gesture.method,flags).Invoke(timeline,new object[]{new MouseEventArgs(MouseButtons.Left,1,gesture.x,groupY,0)});timeline.RefreshLayout();Check(source.start==450&&template.start==0&&template.end==20&&template.keyframes[1].frame==20,"Moving the source changed the shared template clock");
            using(var image=new Bitmap(timeline.Width,timeline.Height)){timeline.DrawToBitmap(image,timeline.ClientRectangle);image.Save(Path.Combine(folder,"generation-groups.png"));}
        }
        Console.WriteLine("PASS: same-track generation groups, readable templates, shared/cyclic references, selection/local keys, source movement, saved folding and unchanged project data");
    }
    [STAThread] public static int Main(string[] args)
    {
        try
        {
            if(args[0]=="--render-gallery"){RenderGallery(args[1],args[2],args[3]);return 0;}
            if(args[0]=="--sample"){var w=new BulletWorld(Read(args[1]));w.Seek(Int32.Parse(args[2]));File.WriteAllText(args[3],Json.Write(Rows(w)));return 0;}
            if(args[0]=="--samples"){var w=new BulletWorld(Read(args[1]));var results=new Dictionary<string,object>();foreach(int at in args[2].Split(',').Select(Int32.Parse).Distinct().OrderBy(f=>f)){w.Seek(at);results[at.ToString()]=Rows(w);}File.WriteAllText(args[3],Json.Write(results));return 0;}
            if(args[0]=="--render"){var s=Read(args[1]);var metadata=Directory.GetDirectories(Path.Combine(args[3],"sprites")).ToDictionary(Path.GetFileName,spriteFolder=>{var file=Path.Combine(spriteFolder,"sprite.txt");var data=(File.Exists(file)?File.ReadAllLines(file):new string[0]).Where(line=>line.Contains("=")).Select(line=>line.Split(new[]{'='},2)).ToDictionary(pair=>pair[0],pair=>pair[1]);return new SpriteMeta{frames=data.ContainsKey("frames")?Int32.Parse(data["frames"]):1,originX=data.ContainsKey("origin_x")?Int32.Parse(data["origin_x"]):0,originY=data.ContainsKey("origin_y")?Int32.Parse(data["origin_y"]):0};});using(var sprites=new SpriteStore{Project=args[3],Metadata=metadata})using(var image=PreviewRenderer.Render(s,new RoomData(),new Dictionary<string,string>(),sprites,Int32.Parse(args[2]),false,false)){image.Save(args[4]);}return 0;}
            string folder=args[0];var state=Read(Path.Combine(folder,"scene.json"));var world=new BulletWorld(state);
            Lifetimes(world);
            Appearance(world);
            AppearancePixels(folder);
            SpawnTimelineUi(folder,state);
            GenerationGroupsUi(folder);
            world.Seek(5);Check(world.bullets.Count(b=>b.source.id=="flying-mother")==2,"Spawn/kill should remove only the odd mother");Check(world.bullets.Count(b=>b.source.id=="moving-heart")==10,"Odd mother should create one full heart");
            var rod=state.tracks.First(t=>t.id=="host-rod");Check(!world.Templates.Visible(rod,0)&&world.Templates.Visible(rod,1)&&!world.Templates.Visible(rod,2)&&world.Templates.Visible(rod,3),"Per-member generation/kill ignored index filters");
            var child=state.tracks.First(t=>t.id=="sacrifice-child");Check(!world.Templates.Visible(child),"Destroying a mother group must hide its descendants");
            Check(world.bullets.Count(b=>b.source.id=="recursive-template")==1&&world.bullets.First(b=>b.source.id=="recursive-template").generation==3,"Recursive replacement should stop at its chosen generation");
            world.Seek(15);var heart=world.bullets.First(b=>b.source.id=="moving-heart"&&b.index==0);var expected=Motion.RodPoints(state.tracks.First(t=>t.id=="moving-heart"),110)[0];Check(Math.Abs(heart.x-(40+expected.X-200))<.0001&&Math.Abs(heart.y-(30+expected.Y-200))<.0001,"Child motion should use local template time and outlive its mother");
            Check(world.bullets.Count(b=>b.source.id=="moving-object"&&b.clockShift== -998&&b.x<100)==1,"First periodic bullet generation disappeared or repeated twice");
            world.Seek(50);Check(world.bullets.Count(b=>b.source.id=="release-template"&&!b.kinematic)==5,"Generated formation release did not produce independent bullets");
            world.Seek(100);var star=world.State.tracks.Single(t=>t.id=="echo-star");var center=Motion.At(star,100);var starPoints=Motion.RodPoints(star,100);var echoes=world.bullets.Where(b=>b.source.id=="echo-shot"&&b.birth==100&&Math.Abs(b.sourceX-center.x)<.001&&Math.Abs(b.sourceY-center.y)<.001).ToArray();Check(echoes.Length==60&&world.Templates.Visible(star),"Star did not generate exactly one extra copy or destroyed its mother");
            for(int i=0;i<echoes.Length;i++){double dx=starPoints[i].X-center.x,dy=starPoints[i].Y-center.y;Check(Math.Abs(echoes[i].speed-Math.Sqrt(dx*dx+dy*dy)*.02)<.00001&&Math.Abs(echoes[i].x-starPoints[i].X)<.0001&&Math.Abs(echoes[i].y-starPoints[i].Y)<.0001,"Source-relative birth speed/position differs from original star");}
            world.Seek(150);Check(world.Templates.Visible(star)&&Motion.At(star,150).x!=center.x,"Original star must continue moving after generating its copy");
            for(int i=0;i<echoes.Length;i++)Check(Math.Abs(echoes[i].sourceX-center.x)<.0001&&Math.Abs(echoes[i].sourceY-center.y)<.0001&&Math.Abs(echoes[i].x-(center.x+2*(starPoints[i].X-center.x)))<.0005&&Math.Abs(echoes[i].y-(center.y+2*(starPoints[i].Y-center.y)))<.0005,"Expansion lost the frozen source center or original star proportions");
            world.Seek(450);world.Seek(230);string checkpoint=Json.Write(Rows(world));var fresh=new BulletWorld(Read(Path.Combine(folder,"scene.json")));fresh.Seek(230);Check(checkpoint==Json.Write(Rows(fresh)),"Checkpoint restore lost generated instances, hidden members or child clocks");
            world.Seek(3);world.Seek(230);Check(checkpoint==Json.Write(Rows(world)),"Replaying generation after rewind changed the result");
            var clones=BulletSystem.CloneTracks(state.tracks);var ids=clones.Select(t=>t.id).ToHashSetCompat();Check(clones.SelectMany(TemplateRuntime.Actions).Where(a=>a.type=="spawn").All(a=>ids.Contains(a.templateId)),"Copying lost template references");Check(!Object.ReferenceEquals(clones[0].spawnActions,state.tracks[0].spawnActions),"Copies share spawn actions");
            var preset=TrackPreset.Capture("generation",state.tracks,50);var imported=preset.Instantiate(20,100);Check(imported.First(t=>t.name=="repeat-host").spawnActions[0].spawnRepeat==140,"Preset FPS conversion missed generation intervals");
            Check(clones.First(t=>t.name=="moving-heart").rainbowMode=="angle"&&imported.First(t=>t.name=="release-template").rainbowMode=="distance","Copy/preset lost rainbow distribution mode");
            using(var form=new BulletBehaviorForm(state.tracks.First(t=>t.id=="host-rod"),state,true)){var f=typeof(BulletBehaviorForm).GetField("list",BindingFlags.NonPublic|BindingFlags.Instance);var list=(ListBox)f.GetValue(form);list.SelectedIndex=0;typeof(BulletBehaviorForm).GetMethod("ShowAction",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,null);var inspector=(ParameterInspector)typeof(BulletBehaviorForm).GetField("inspector",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(form);var p=TypeDescriptor.GetProperties(inspector.SelectedObject);Check(p["生成模板"]!=null&&p["生成后销毁母体"]!=null&&p["最大编号（-1 不限）"]!=null&&p["初始生成方向 / 度"]!=null&&p["每颗生成方向递增 / 度"]!=null,"Generation editor is missing required controls");p["初始生成方向 / 度"].SetValue(inspector.SelectedObject,30.0);p["每颗生成方向递增 / 度"].SetValue(inspector.SelectedObject,12.0);Check(form.Actions[0].spawnAngle==30&&form.Actions[0].spawnAngleStep==12,"Generation angle fields did not edit the selected action");}
            Console.WriteLine("PASS: independent template clocks, mother replacement, member filtering, repeating spawn, group/emitter templates, release, recursive depth, checkpoint rewind, copy/preset references and native UI.");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
    private static HashSet<T> ToHashSetCompat<T>(this IEnumerable<T> source){return new HashSet<T>(source);}
}
