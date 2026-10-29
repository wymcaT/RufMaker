using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using System.Windows.Forms;

internal static class RegionGenerationTests
{
    sealed class QuietForm:Form {protected override bool ShowWithoutActivation{get{return true;}}}
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Near(double a,double b,string message){Check(Math.Abs(a-b)<.0001,message+": "+a+" / "+b);}
    static object Rows(BulletWorld w){return w.bullets.Where(b=>b.birth<=w.Frame&&!b.dead).Select(b=>new{source=b.source.id,b.birth,b.index,b.generation,b.x,b.y,b.direction,b.speed,scale=b.scale*BulletLifetime.Scale(b),alpha=b.alpha*BulletLifetime.Alpha(b),b.kinematic,b.clockShift,b.sourceX,b.sourceY,b.collision,b.deathStart,sprite=b.sprite??b.source.sprite,color=b.color.R+b.color.G*256+b.color.B*65536,depth=b.generation>0?-7000000-b.generation:-100,image=b.frozenImage>=0?b.frozenImage:Rainbow.Index(b.source,b.sequence,w.Frame-b.clockShift,(b.kinematic?w.Frame:b.birth)-b.clockShift)}).ToArray();}
    static Track Host(string id){return new Track{id=id,name=id,kind="object",start=0,end=28,x=-600,y=900};}
    static Track AddRegion(EditorState s,string id,string mode,bool follow=false)
    {
        var host=Host(id);s.tracks.Add(host);var a=GenerationPresets.Apply(s,host,GenerationPresets.Region);var t=s.tracks.Single(row=>row.id==a.templateId);t.id=id+"-shot";a.templateId=t.id;a.spawnRepeat=7;a.spawnAtWorld=!follow;a.spawnOffsetX=follow?100:400;a.spawnOffsetY=follow?60:304;t.emitWidth=mode=="fixed"?200:680;t.emitHeight=mode=="fixed"?100:500;t.count=12;t.fadeInFrames=8;t.life=35;t.lifetimeMode=mode;t.deathEffect="fade";t.deathFrames=6;t.rainbow=true;t.rainbowRandom=true;t.expressions["speed"]="random_range(1,5)";t.expressions["angle"]="choose(20,90,210)";Trails.SetMode(t,"fade");
        if(follow){host.x=400;host.y=304;host.keyframes.Add(new Keyframe{frame=0,property="x",value=400});host.keyframes.Add(new Keyframe{frame=28,property="x",value=700});}return t;
    }
    static EditorState Fixture()
    {
        var s=new EditorState{fps=50,duration=110,randomSeed=7261};AddRegion(s,"world","fixed");AddRegion(s,"room","room",true);AddRegion(s,"view","view");
        s.tracks.Add(new Track{id="palette",kind="emitter",start=2,end=36,count=17,interval=11,x=100,y=100,rainbow=true,rainbowRandom=true,speed=1,life=80});
        s.tracks.Add(new Track{id="released",kind="rod",start=0,end=55,x=420,y=310,shape="star2",radius=50,count=20,releaseFrame=20,speed=2,life=45,rainbow=true,rainbowRandom=true,rainbowMode="distance"});
        var host=Host("shape-host");host.x=300;host.y=220;host.spawnActions.Add(new BulletAction{type="spawn",templateId="shape-template",spawnRepeat=15});s.tracks.Add(host);
        s.tracks.Add(new Track{id="shape-template",kind="rod",start=0,end=21,templateOnly=true,shape="heart",radius=60,count=25,rainbow=true,rainbowRandom=true,rainbowDynamic=true,rainbowMode="angle"});
        s.tracks.Add(new Track{id="camera",kind="camera",start=0,end=110,x=400,y=304,cameraZoom=1.7});Motion.Normalize(s);return s;
    }
    static void Ui(EditorState s,Track source,string folder,string language)
    {
        Language.Select(folder,language);var action=source.spawnActions[0];var target=s.tracks.Single(t=>t.id==action.templateId);var bag=new NativeProperties();GenerationPresets.Fields(bag,s,source,action,change=>change());
        foreach(string name in new[]{"区域中心 X","区域中心 Y","区域宽度","区域高度","新弹幕速度","新弹幕方向 / 度或表达式","寿命模式","出生淡入","淡入帧数","随机彩虹颜色"})Check(bag.GetProperties()[name]!=null,"Missing field "+name);
        if(language=="zh-CN"){
            bag.GetProperties()["新弹幕方向 / 度或表达式"].SetValue(bag,"random_range(30,150)");Check(target.expressions["angle"]=="random_range(30,150)","Direction formula lost");target.expressions["angle"]="choose(20,90,210)";
            bag.GetProperties()["区域中心 X"].SetValue(bag,420.0);Near(action.spawnOffsetX,420,"Region field not editable");action.spawnOffsetX=400;
        }
        using(var form=new QuietForm{ShowInTaskbar=false,Size=new Size(560,780),StartPosition=FormStartPosition.Manual,Location=new Point(-20000,-20000),BackColor=Theme.Panel})using(var inspector=new ParameterInspector{Dock=DockStyle.Fill,SelectedObject=bag}){form.Controls.Add(inspector);form.Show();form.PerformLayout();Application.DoEvents();inspector.PerformLayout();using(var image=new Bitmap(form.ClientSize.Width,form.ClientSize.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(folder,"region-"+language+".png"));}}
    }
    [STAThread] public static int Main(string[] args)
    {
        try{
            if(args.Length>0&&args[0]=="--samples"){var sampleState=Json.Read<EditorState>(File.ReadAllText(args[1]));Motion.Normalize(sampleState);var w=new BulletWorld(sampleState);var rows=new Dictionary<string,object>();foreach(int f in args[2].Split(',').Select(Int32.Parse)){w.Seek(f);rows[f.ToString()]=Rows(w);}File.WriteAllText(args[3],Json.Write(rows));return 0;}
            var folder=args[0];Directory.CreateDirectory(folder);Check(new Track().color=="#FFFFFF","New track must be white");var s=Fixture();var world=new BulletWorld(s);world.Seek(0);var first=world.bullets.Where(b=>b.source.id=="world-shot").ToArray();Check(first.Length==12,"Region birth count");var firstPositions=first.Select(b=>b.x).ToArray();
            Check(first.All(b=>b.x>=300&&b.x<=500&&b.y>=254&&b.y<=354),"World rectangle followed offscreen source");Check(first.Select(b=>b.x).Distinct().Count()==12&&first.Max(b=>b.x)-first.Min(b=>b.x)>100,"Positions not independently distributed");Check(first.All(b=>b.speed>=1&&b.speed<=5&&new[]{20.0,90.0,210.0}.Contains(b.direction)),"Birth speed/direction expression");Check(first.All(b=>BulletLifetime.Alpha(b)==0&&b.color.ToArgb()==Color.White.ToArgb()),"Fade/default white");
            var snapshot=s.Snapshot();Check(snapshot.tracks.Single(t=>t.id=="world-shot").fadeInFrames==8&&snapshot.tracks.Single(t=>t.id=="world-shot").rainbowRandom&&snapshot.tracks[0].spawnActions[0].spawnAtWorld,"New fields lost in snapshot");
            world.Seek(4);foreach(var b in world.bullets.Where(b=>b.source.id=="world-shot"&&b.birth==0))Near(BulletLifetime.Alpha(b),.5,"Fade midpoint");world.Seek(8);Check(world.bullets.Where(b=>b.source.id=="world-shot"&&b.birth==7).Count()==12,"Interval failed");
            var palette=world.bullets.Where(b=>b.source.id=="palette").Select(b=>Rainbow.Index(b.source,b.sequence,world.Frame,b.birth)).ToArray();Check(palette.Distinct().Count()>=8,"Random rainbow colors did not vary");var a=Json.Write(Rows(world));world.Seek(2);world.Seek(8);Check(a==Json.Write(Rows(world)),"Reverse seek changed random results");
            var reseed=Json.Read<EditorState>(Json.Write(s));reseed.randomSeed++;Motion.Normalize(reseed);var other=new BulletWorld(reseed);other.Seek(0);Check(other.bullets.Where(b=>b.source.id=="world-shot").Select(b=>b.x).SequenceEqual(firstPositions)==false,"Reroll did not change region positions");
            var scaled=new TrackPreset{fps=50,tracks=new List<Track>{s.tracks.Single(t=>t.id=="world-shot").Snapshot()}}.Instantiate(0,100).Single();Check(scaled.fadeInFrames==16&&scaled.rainbowRandom,"Preset FPS conversion lost birth fields");
            var emitter=Host("emitter-source");emitter.kind="emitter";var check=new EditorState{tracks=new List<Track>{emitter}};GenerationPresets.Apply(check,emitter,GenerationPresets.Region);Check(emitter.spawnActions.Count==1&&emitter.bulletActions.Count==0,"Region should default to source lifecycle, not every emitted bullet");
            emitter.color="#123456";var white=GenerationPresets.Apply(check,emitter,"random-360");Check(!white.inheritAppearance&&check.tracks.Single(t=>t.id==white.templateId).color=="#FFFFFF","Colored source changed new bullet default");
            using(var stage=new StageCanvas{State=s,SelectedTrack=s.tracks.Single(t=>t.id=="world"),Frame=0})using(var bitmap=new Bitmap(800,608))using(var graphics=Graphics.FromImage(bitmap)){typeof(StageCanvas).GetMethod("PaintGenerationRegions",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(stage,new object[]{graphics});bitmap.Save(Path.Combine(folder,"region-outline.png"));Check(Enumerable.Range(254,100).Any(y=>bitmap.GetPixel(300,y).A>0),"Region outline missing");}
            Ui(s,s.tracks.Single(t=>t.id=="world"),folder,"zh-CN");Ui(s,s.tracks.Single(t=>t.id=="world"),folder,"en");Language.Select(folder,"zh-CN");File.WriteAllText(Path.Combine(folder,"scene.json"),Json.Write(s));
            Console.WriteLine("PASS: world/follow regions, independent positions/directions/speeds, fade, three lifetimes, stable/random palette, reverse seek, snapshot/preset FPS, source lifecycle and bilingual fields; GM fixture exported.");return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
