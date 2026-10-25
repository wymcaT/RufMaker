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
    private static object Rows(BulletWorld w){return w.bullets.Where(b=>b.birth<=w.Frame&&!b.dead).OrderBy(b=>b.source.id).ThenBy(b=>b.birth).ThenBy(b=>b.index).ThenBy(b=>b.x).Select(b=>new{source=b.source.id,birth=b.birth,index=b.index,generation=b.generation,x=b.x,y=b.y,direction=b.direction,speed=b.speed,scale=b.scale,alpha=b.alpha,kinematic=b.kinematic,clockShift=b.clockShift,sourceX=b.sourceX,sourceY=b.sourceY,image=Rainbow.Index(b.source,b.sequence,w.Frame-b.clockShift,(b.kinematic?w.Frame:b.birth)-b.clockShift)}).ToArray();}
    private static EditorState Read(string file){var state=Json.Read<EditorState>(File.ReadAllText(file));Motion.Normalize(state);return state;}
    [STAThread] public static int Main(string[] args)
    {
        try
        {
            if(args[0]=="--sample"){var w=new BulletWorld(Read(args[1]));w.Seek(Int32.Parse(args[2]));File.WriteAllText(args[3],Json.Write(Rows(w)));return 0;}
            if(args[0]=="--render"){var s=Read(args[1]);var metadata=Directory.GetDirectories(Path.Combine(args[3],"sprites")).ToDictionary(Path.GetFileName,spriteFolder=>{var file=Path.Combine(spriteFolder,"sprite.txt");var data=(File.Exists(file)?File.ReadAllLines(file):new string[0]).Where(line=>line.Contains("=")).Select(line=>line.Split(new[]{'='},2)).ToDictionary(pair=>pair[0],pair=>pair[1]);return new SpriteMeta{frames=data.ContainsKey("frames")?Int32.Parse(data["frames"]):1,originX=data.ContainsKey("origin_x")?Int32.Parse(data["origin_x"]):0,originY=data.ContainsKey("origin_y")?Int32.Parse(data["origin_y"]):0};});using(var sprites=new SpriteStore{Project=args[3],Metadata=metadata})using(var image=PreviewRenderer.Render(s,new RoomData(),new Dictionary<string,string>(),sprites,Int32.Parse(args[2]),false,false)){image.Save(args[4]);}return 0;}
            string folder=args[0];var state=Read(Path.Combine(folder,"scene.json"));var world=new BulletWorld(state);
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
            using(var form=new BulletBehaviorForm(state.tracks.First(t=>t.id=="host-rod"),state,true)){var f=typeof(BulletBehaviorForm).GetField("list",BindingFlags.NonPublic|BindingFlags.Instance);var list=(ListBox)f.GetValue(form);list.SelectedIndex=0;typeof(BulletBehaviorForm).GetMethod("ShowAction",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,null);var inspector=(ParameterInspector)typeof(BulletBehaviorForm).GetField("inspector",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(form);var p=TypeDescriptor.GetProperties(inspector.SelectedObject);Check(p["生成模板"]!=null&&p["生成后销毁母体"]!=null&&p["最大编号（-1 不限）"]!=null,"Generation editor is missing required controls");}
            Console.WriteLine("PASS: independent template clocks, mother replacement, member filtering, repeating spawn, group/emitter templates, release, recursive depth, checkpoint rewind, copy/preset references and native UI.");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
    private static HashSet<T> ToHashSetCompat<T>(this IEnumerable<T> source){return new HashSet<T>(source);}
}
