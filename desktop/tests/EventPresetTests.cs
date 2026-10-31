using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class EventPresetTests
{
    static readonly BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static object Get(object o,string name){return o.GetType().GetField(name,flags).GetValue(o);}
    static void Set(object o,string name,object value){o.GetType().GetField(name,flags).SetValue(o,value);}
    static object Call(object o,string name,params object[] args){return o.GetType().GetMethods(flags).Single(m=>m.Name==name&&m.GetParameters().Length==args.Length).Invoke(o,args);}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Near(double a,double b,string message){Check(Math.Abs(a-b)<.00001,message+": "+a+"/"+b);}
    static IEnumerable<Control> All(Control c){yield return c;foreach(Control child in c.Controls)foreach(var item in All(child))yield return item;}
    static Track Event(string key,int frame){var t=new Track{id=key+"-"+frame,start=frame,lane=3};EventPresets.ApplyPreset(t,key);return t;}
    static Track Emitter(string id){return new Track{id=id,name=id,kind="emitter",start=0,end=100,count=2,spread=60,speed=3,interval=5,life=120,sprite="sprCherry",x=300,y=200,advancedBullets=true,lane=0};}
    static EditorState Scene(params Track[] tracks){var s=new EditorState{fps=50,duration=400,randomSeed=991,tracks=tracks.ToList()};Motion.Normalize(s);return s;}
    static object Rows(BulletWorld w){return w.bullets.Where(b=>b.birth<=w.Frame&&!b.dead).Select(b=>new{source=b.source.id,b.birth,b.index,b.generation,b.x,b.y,b.direction,b.speed,scale=b.scale*BulletLifetime.Scale(b),alpha=b.alpha*BulletLifetime.Alpha(b),trailMode=Trails.Mode(b.source),trailInterval=b.source.trailInterval,trailLife=b.source.trailLife,trailAlpha=b.source.trailStartAlpha,trailSpeed=b.source.trailSpeed,trailDirection=b.source.trailDirection,trailRandom=b.source.trailRandom,b.kinematic,b.clockShift,b.sourceX,b.sourceY,b.collision}).ToArray();}
    static EditorState Fixture(){
        var emitter=Emitter("emitter");emitter.indexDelay=2;emitter.bulletActions.Add(new BulletAction{type="acceleration",start=1,duration=90,from=.1,to=.1});emitter.bulletActions.Add(new BulletAction{type="speed",start=16,duration=90,from=8,to=8});emitter.flightMode="aim";
        var rod=new Track{id="rod",name="held formation",kind="rod",shape="ring",count=5,start=0,end=95,sprite="sprCherry",lane=1};
        var future=rod.Snapshot();future.id="future";future.name="future formation";future.start=30;future.lane=2;
        var template=rod.Snapshot();template.id="template";template.name="template formation";template.start=0;template.end=15;template.templateOnly=true;
        var parent=Emitter("generator");parent.count=1;parent.interval=5;parent.bulletActions.Add(new BulletAction{type="spawn",templateId=template.id,start=1,spawnRepeat=10,maxGeneration=2});
        var stop=Event("stop-bullets",10);var reset=Event("reset-motion",15);reset.eventRestoreBirth=false;reset.controlValue=4;reset.controlFrom=90;
        var continuous=Event("stop-bullets",25);continuous.end=28;
        return Scene(emitter,rod,future,template,parent,Event("clear-bullets",5),stop,reset,Event("clear-danmaku",20),continuous,Event("reset-motion",50),new Track{id="phase",kind="variable",start=0,end=0,variableName="phase",variableExpression="1"},new Track{id="counter",kind="variable",start=0,end=0,variableName="counter",variableExpression="2"},new Track{id="distance",kind="variable",start=0,end=0,variableName="distance",variableExpression="3"});
    }
    static void Simulation(string root){
        var e=Emitter("clear");e.deathEffect="fade";e.deathFrames=40;var s=Scene(e,Event("clear-bullets",5));var world=new BulletWorld(s);world.Seek(5);Check(!world.bullets.Any(b=>b.birth<=5&&!b.dead),"Clear must immediately remove old and same-frame bullets, bypassing death transitions");world.Seek(10);Check(world.bullets.Count==2&&world.bullets.All(b=>b.birth==10),"Clear cancelled future emission");world.Seek(4);Check(world.bullets.Count==2,"Rewind did not restore cleared bullets");
        var rod=new Track{id="held",kind="rod",start=0,end=90,count=5};s=Scene(rod,Emitter("flying"),Event("clear-bullets",5));world=new BulletWorld(s);world.Seek(5);Check(world.Templates.Visible(world.State.tracks.Single(t=>t.id=="held")),"Clear bullets removed held formations");
        var all=Event("clear-danmaku",5);s=Scene(rod,Emitter("flying"),all,new Track{id="next",kind="rod",start=10,end=80,count=3});world=new BulletWorld(s);world.Seek(6);Check(!world.Templates.Visible(world.State.tracks.Single(t=>t.id=="held")),"Cleared formation returned next frame");world.Seek(10);Check(world.Templates.Visible(world.State.tracks.Single(t=>t.id=="next")),"All-clear removed future formations");world.Seek(4);Check(world.Templates.Visible(world.State.tracks.Single(t=>t.id=="held")),"Rewind did not restore formation visibility");
        e=Emitter("reset");e.interval=100;e.flightMode="aim";e.bulletActions.Add(new BulletAction{type="speed",start=1,duration=80,from=8,to=8});e.bulletActions.Add(new BulletAction{type="acceleration",start=1,duration=80,from=1,to=1});var reset=Event("reset-motion",5);reset.eventRestoreBirth=false;reset.controlValue=4;reset.controlFrom=90;s=Scene(e,reset);world=new BulletWorld(s);world.Seek(6);Check(world.bullets.All(b=>b.speed==4&&b.direction==90&&b.acceleration==0&&b.motionReset),"Reset did not suppress future age movement and aiming");
        reset.eventRestoreBirth=true;s=Scene(e,reset);world=new BulletWorld(s);world.Seek(6);Check(world.bullets.All(b=>b.speed==b.baseSpeed&&b.direction==b.baseDirection),"Restore birth motion failed");
        var resetRod=new Track{id="rod-disintegrate",kind="rod",shape="polygon",sides=5,count=5,start=0,end=90,x=400,y=304,radius=100};
        var rodEvent=Event("reset-motion",10);rodEvent.eventRestoreBirth=false;rodEvent.controlValue=5;rodEvent.controlFrom=45;rodEvent.eventAcceleration=0.5;rodEvent.eventTurn=2;rodEvent.eventResetLife=true;rodEvent.eventLife=80;
        s=Scene(resetRod,rodEvent);world=new BulletWorld(s);world.Seek(9);
        Check(world.Templates.Visible(world.State.tracks.Single(t=>t.id=="rod-disintegrate")),"Rod should be visible before reset-motion");
        Check(world.bullets.Count==0,"No flying bullets before reset-motion");
        world.Seek(10);
        Check(!world.Templates.Visible(world.State.tracks.Single(t=>t.id=="rod-disintegrate")),"Rod should be hidden/destroyed on reset-motion");
        Check(world.bullets.Count==5,"Rod member bullets not spawned on reset-motion");
        Check(world.bullets.All(b=>b.speed==5&&b.direction==45&&b.acceleration==0.5&&b.turn==2&&b.life==80&&b.birth==10),"Rod bullets did not receive reset-motion properties and reset life");
        world.Seek(11);Check(world.bullets.Count==5&&world.bullets.All(b=>b.speed>5),"Rod member bullets did not fly after reset-motion");
        world.Seek(89);Check(world.bullets.Count==5,"Rod member bullets died before reset life expired");
        world.Seek(90);Check(world.bullets.Count==0,"Rod member bullets did not expire after reset life");
        world.Seek(9);Check(world.Templates.Visible(world.State.tracks.Single(t=>t.id=="rod-disintegrate")),"Rewind did not restore rod formation");
        Check(world.bullets.Count==0,"Rewind did not clean up disintegrated bullets");
        var grpA=new TimelineGroup{id="grpA",name="Group A"};var grpB=new TimelineGroup{id="grpB",name="Group B"};
        var rodA=new Track{id="rodA",kind="rod",shape="ring",count=4,start=0,end=90,scopeGroup="grpA"};
        var rodB=new Track{id="rodB",kind="rod",shape="ring",count=4,start=0,end=90,scopeGroup="grpB"};
        var grpEvent=Event("group-set",15);grpEvent.eventGroup="grpA";grpEvent.eventRestoreBirth=false;grpEvent.controlValue=8;grpEvent.controlFrom=180;grpEvent.eventAcceleration=0.2;grpEvent.eventTurn=3;grpEvent.eventResetLife=true;grpEvent.eventLife=50;
        var groupScene=new EditorState{fps=50,duration=200,randomSeed=123,timelineGroups=new List<TimelineGroup>{grpA,grpB},tracks=new List<Track>{rodA,rodB,grpEvent}};
        Motion.Normalize(groupScene);world=new BulletWorld(groupScene);world.Seek(14);
        Check(world.Templates.Visible(world.State.tracks.Single(t=>t.id=="rodA")),"Rod A should be visible before group-set");
        Check(world.Templates.Visible(world.State.tracks.Single(t=>t.id=="rodB")),"Rod B should be visible before group-set");
        world.Seek(15);
        Check(!world.Templates.Visible(world.State.tracks.Single(t=>t.id=="rodA")),"Rod A in target group should disintegrate on group-set");
        Check(world.Templates.Visible(world.State.tracks.Single(t=>t.id=="rodB")),"Rod B in non-target group must NOT disintegrate on group-set");
        Check(world.bullets.Count(b=>b.scopeGroup=="grpA")==4,"Rod A member bullets not detached");
        Check(world.bullets.Where(b=>b.scopeGroup=="grpA").All(b=>b.speed==8&&b.direction==180&&b.acceleration==0.2&&b.turn==3&&b.life==50&&!b.kinematic),"Rod A bullets did not receive group-set motion and life properties");
        var tmplRod=new Track{id="tmpl-rod",kind="rod",shape="polygon",sides=4,count=4,start=0,end=40,templateOnly=true};
        var spawner=new Track{id="spawner",kind="object",start=0,end=50,x=400,y=304};
        spawner.spawnActions.Add(new BulletAction{type="spawn",generationPreset="copy-object",templateId="tmpl-rod",enabled=true,start=0,spawnRepeat=0,count=1});
        var tmplReset=Event("reset-motion",10);tmplReset.eventRestoreBirth=false;tmplReset.controlValue=6;tmplReset.controlFrom=90;tmplReset.eventResetLife=true;tmplReset.eventLife=60;
        var tmplScene=Scene(tmplRod,spawner,tmplReset);world=new BulletWorld(tmplScene);world.Seek(5);
        Check(world.Templates.InstanceCount>0,"Template instance should exist before reset");
        world.Seek(10);
        Check(world.Templates.InstanceCount==0,"Template instance container must be ended/destroyed on reset-motion");
        Check(world.bullets.Count==4,"Template member bullets should be detached into free bullets");
        Check(world.bullets.All(b=>!b.kinematic&&b.speed==6&&b.direction==90&&b.life==60),"Template member bullets did not receive reset-motion properties");
        var copy=reset.Snapshot();Check(copy.eventPreset==reset.eventPreset&&copy.eventRestoreBirth,"Event snapshot lost flags");var preset=TrackPreset.Capture("events",new[]{reset},50).Instantiate(100,100).Single();Near(preset.controlValue,2,"Cross-FPS event speed");Check(preset.start==100,"Cross-FPS event trigger");
        var fixture=Fixture();File.WriteAllText(Path.Combine(root,"scene.json"),Json.Write(fixture));world=new BulletWorld(fixture);world.Seek(26);Check(world.bullets.Where(b=>!b.kinematic&&b.birth<=26).All(b=>b.speed==0),"Continuous stop missed newborn bullets");world.Seek(205);string snapshot=Json.Write(Rows(world));world.Seek(4);world.Seek(205);Check(Json.Write(Rows(world))==snapshot,"Checkpoint rewind changed events");
        Console.WriteLine("PASS: immediate clear, held/future formations, newborn/template bullets, reset/age behavior suppression, seeded rewind, snapshots and cross-FPS presets.");
    }
    static void Render(Control c,string file){c.CreateControl();c.PerformLayout();using(var image=new Bitmap(c.Width,c.Height)){c.DrawToBitmap(image,c.ClientRectangle);image.Save(file);}}
    static void UI(string root){
        using(var form=new MainForm(root)){
            var t=Emitter("UI");Motion.SetKey(t,"x",0,200);Motion.SetKey(t,"x",80,500);Motion.SetKey(t,"y",20,200);Motion.SetKey(t,"alpha",40,.4);var s=Scene(t);Set(form,"state",s);Set(form,"document",new LoadData{state=s,project=root,projectName="events",room=new RoomData(),inventory=new Inventory{objects=new[]{"objNativeBullet"},sprites=new[]{"sprCherry"},scripts=new string[0]},waveform=new WaveData()});var timeline=(TimelineCanvas)Get(form,"timeline");timeline.State=s;Call(form,"SelectTrack",t,null);
            var split=(SplitContainer)Get(form,"keySplit");var keyPane=(Panel)Get(form,"keyWorkspace");var properties=(ParameterInspector)Get(form,"properties");var before=All(properties).ToArray();
            var parent=split.Parent;parent.Parent.Controls.Remove(parent);using(var host=new Panel{Size=new Size(540,630),Font=Theme.Font,BackColor=Theme.Panel}){host.Controls.Add(parent);parent.Dock=DockStyle.Fill;host.CreateControl();host.PerformLayout();Call(form,"RefreshKeyWorkspace");
                Check(!split.IsSplitterFixed&&split.SplitterWidth>=5,"Keyframe editor has no draggable splitter");split.OnSplitterMoving(new SplitterCancelEventArgs(0,0,0,0));split.SplitterDistance=320;split.OnSplitterMoved(new SplitterEventArgs(0,0,0,320));Call(form,"RefreshKeyWorkspace");Check(split.SplitterDistance==320&&(int)Get(form,"keyWorkspaceHeight")==320,"Splitter drag was reset by inspector refresh");Check(All(properties).SequenceEqual(before),"Resizing recreated property controls");
                Call(form,"SaveWorkspaceView");var view=WorkspaceView.Load(root);Check(view.keyframesHeight==320,"Keyframe height not saved");Render(host,Path.Combine(root,"resizable-keys-zh.png"));
                var fonts=All(host).ToDictionary(c=>c,c=>c.Font);using(var font=new Font(Theme.Font.FontFamily,18)){foreach(var control in fonts.Keys)control.Font=font;Call(form,"RefreshKeyWorkspace");Render(host,Path.Combine(root,"resizable-keys-large.png"));var keys=(TimelineCanvas)Get(form,"keyTimeline");Check(keys.Height>keys.AudioBottom+keys.Font.Height+12+SystemInformation.HorizontalScrollBarHeight,"Large-font key viewport has no complete row");foreach(var entry in fonts)entry.Key.Font=entry.Value;}
                host.Controls.Remove(parent);
            }
            Call(form,"SetResourcePage",true);Set(form,"resourceKind","event-presets");Call(form,"FillResources");Check(((ListView)Get(form,"resources")).Items.Count==EventPresets.Keys.Length,"Event category missing");int count=s.tracks.Count;((Task)Call(form,"UsePreset","event-presets","clear-bullets",t,77)).GetAwaiter().GetResult();Check(s.tracks.Count==count+1&&s.tracks.Last().start==77&&s.tracks.Last().end==77&&s.tracks.Last().eventPreset=="clear-bullets","Preset must create a song-time event at drop position");Call(form,"Undo",false);Check(((EditorState)Get(form,"state")).tracks.Count==count,"Event insertion undo failed");
            var data=new DataObject(DataFormats.Text,"event-presets|stop-bullets");var drag=new DragEventArgs(data,0,0,0,DragDropEffects.Copy,DragDropEffects.None);Call(timeline,"OnDragEnter",drag);Check(drag.Effect==DragDropEffects.Copy,"Timeline rejects event presets");Language.Select(root,"en");Language.Apply(form);Set(form,"resourceKind","event-presets");Call(form,"FillResources");Check(((ListView)Get(form,"resources")).Items.Cast<ListViewItem>().All(i=>!System.Text.RegularExpressions.Regex.IsMatch(i.Text,@"[\u3400-\u9fff]")),"Events not translated");Language.Select(root,"zh-CN");
        }
        using(var font=new Font(Theme.Font.FontFamily,18))using(var time=new SelectionTimeControl{Font=font,Width=540}){time.Height=time.PreferredSize.Height;time.SetView(50,600,120,new Track{start=100,end=300});Render(time,Path.Combine(root,"time-large.png"));Check(All(time).OfType<Button>().All(b=>b.Height>=font.Height+4),"Start/end button text is clipped");Check(!((TrackBar)Get(time,"progress")).AutoSize,"Trackbar is forcing a tall native control over the buttons");}
        Console.WriteLine("PASS: real resizable/persistent keyframe splitter, high-font complete rows/start-end buttons, event category/drop/undo and localization.");
    }
    [STAThread]static int Main(string[] args){try{
        if(args[0]=="--samples"){var s=Json.Read<EditorState>(File.ReadAllText(args[1]));Motion.Normalize(s);var w=new BulletWorld(s);var result=new Dictionary<string,object>();foreach(int f in args[2].Split(',').Select(Int32.Parse)){w.Seek(f);result[f.ToString()]=new{bullets=Rows(w),globals=GlobalVariables.At(w.State,f),visible=w.State.tracks.Where(t=>!t.templateOnly&&(t.kind=="rod"||t.kind=="object")).ToDictionary(t=>t.id,t=>w.Templates.Visible(t))};}File.WriteAllText(args[3],Json.Write(result));return 0;}
        Application.EnableVisualStyles();Simulation(args[0]);UI(args[0]);return 0;
    }catch(Exception error){Console.Error.WriteLine(error);return 1;}}
}
