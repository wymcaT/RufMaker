using System;
using System.ComponentModel;
using System.Drawing;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class NativeIntegrationTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static object Get(object target,string name){return target.GetType().GetField(name,Private).GetValue(target);}
    private static void Set(object target,string name,object value){target.GetType().GetField(name,Private).SetValue(target,value);}
    private static object Call(object target,string name,params object[] arguments){return target.GetType().GetMethod(name,Private).Invoke(target,arguments);}
    private static void Check(bool result,string message){if(!result)throw new Exception(message);}
    private static void Image(string folder,string relative,Color color,int width=6,int height=4)
    {
        string file=Path.Combine(folder,relative);Directory.CreateDirectory(Path.GetDirectoryName(file));
        using(var bitmap=new Bitmap(width,height))using(var g=Graphics.FromImage(bitmap)){g.Clear(color);bitmap.Save(file);}
    }
    private static bool ColorAt(Bitmap bitmap,int x,int y,Color color)
    {
        var actual=bitmap.GetPixel(x,y);
        return Math.Abs(actual.R-color.R)<=2&&Math.Abs(actual.G-color.G)<=2&&Math.Abs(actual.B-color.B)<=2;
    }
    private static void RoomGraphics(string folder)
    {
        Image(folder,"backgrounds/under.png",Color.Blue);
        Image(folder,"backgrounds/tile.png",Color.Red);
        Image(folder,"backgrounds/front.png",Color.Yellow);
        Image(folder,"sprites/door/0.png",Color.White,6,4);
        var room=new RoomData{name="room1",width=48,height=36,instanceRevision="test",
            backgrounds=new[]{new RoomBackground{source="under",tileX=true,tileY=true,speedX=1},
                new RoomBackground{source="front",foreground=true,x=1,y=2}},
            tiles=new[]{new RoomTile{source="tile",x=13,y=10,width=4,height=4,depth=10,scaleX=2,scaleY=2}},
            views=new[]{new RoomView{width=20,height=12}}};
        var door=new RoomInstance{id="abcd",name="objDoor",x=18,y=15,angle=90,scaleX=2,scaleY=1,blend=RoomPresentation.Pack(Color.Lime)};
        room.instances.Add(door);
        var spritesByObject=new Dictionary<string,string>{{"objDoor","door"}};
        using(var sprites=new SpriteStore{Project=folder})using(var bitmap=PreviewRenderer.Render(new EditorState(),room,spritesByObject,sprites,5,false,false,roomOnly:true)){
            Check(ColorAt(bitmap,45,33,Color.Blue),"Tiled room background missing");
            Check(ColorAt(bitmap,16,17,Color.Red),"Scaled room tile missing");
            Check(ColorAt(bitmap,20,13,Color.Lime),"Rotated and tinted instance missing or drawn below tile");
            Check(ColorAt(bitmap,2,3,Color.Yellow),"Room foreground missing");
            Check(ColorAt(bitmap,30,13,Color.Blue),"Room instance ignored its rotation");
            bitmap.Save(Path.Combine(folder,"static-room.png"));
        }
        var copy=Json.Read<RoomData>(Json.Write(room));
        Check(room.SameContent(copy)&&copy.Snapshot().instances[0]!=copy.instances[0],"Room metadata was not preserved across JSON and snapshot");
    }
    private static void ObjectClip(string folder)
    {
        using(var form=new MainForm(folder)){
            var state=new EditorState{duration=200,room="room1"};
            var room=new RoomData{name=state.room,width=48,height=36};
            var inventory=new Inventory{objects=new[]{"objDoor","objRufController"},sprites=new string[0],scripts=new string[0],rooms=new[]{state.room,"room2"}};
            Set(form,"state",state);Set(form,"document",new LoadData{state=state,room=room,project=folder,inventory=inventory});
            var timeline=(TimelineCanvas)Get(form,"timeline");timeline.State=state;timeline.Frame=17;
            var data=new DataObject(DataFormats.Text,"objects|objDoor");var p=timeline.PointToScreen(new Point(10,60));
            var drop=new DragEventArgs(data,0,p.X,p.Y,DragDropEffects.Copy,DragDropEffects.None);
            Call(timeline,"OnDragEnter",drop);Check(drop.Effect==DragDropEffects.Copy,"Main timeline refuses GM object drag");
            Call(timeline,"OnDragDrop",drop);
            var track=state.tracks.Single();Check(track.kind=="native-instance"&&track.nativeObject=="objDoor"&&track.start==17&&track.x==24&&track.y==18,"Drop did not create a real-object clip at the playhead");
            Check(!StageCanvas.AimsAtPlayer(track),"An ordinary native object acquired a homing helper line");
            Check(!StageCanvas.AimsAtPlayer(new Track{kind="emitter"}),"The disabled default flight formula acquired a homing helper line");
            Check(StageCanvas.AimsAtPlayer(new Track{kind="emitter",flightMode="aim"}),"An enabled homing emitter lost its helper line");
            string previewKey=NativeClip.PreviewKey(state);
            track.name="Renamed";Check(NativeClip.PreviewKey(state)==previewKey,"Renaming a clip invalidates the native recording");
            track.x+=1;Check(NativeClip.PreviewKey(state)!=previewKey,"Moving a native clip does not invalidate its recording");track.x-=1;
            var inspector=(ParameterInspector)Get(form,"properties");var fields=TypeDescriptor.GetProperties(inspector.SelectedObject);
            Check(fields["生成 X"]!=null&&fields["片段结束后保留"]!=null&&fields["弹速"]==null&&fields["贴图"]==null,"Native object clip exposes unrelated controls");
            var copy=Json.Read<Track>(Json.Write(track.Snapshot()));Check(copy.nativeObject=="objDoor"&&!copy.nativeKeepAlive,"Native object fields were not persisted");
            var roomBack=(ToolStripButton)Get(form,"returnToRoom");Check(!roomBack.Available,"Room return button consumes space in the normal editor");
            var keyTimeline=(TimelineCanvas)Get(form,"keyTimeline");Call(keyTimeline,"OnDragEnter",drop);Check(drop.Effect==DragDropEffects.None,"Object drop leaked into the keyframe panel");
            var route=new GmPath{points=new List<GmPathPoint>{new GmPathPoint{x=10,y=15},new GmPathPoint{x=40,y=15}}};
            state.nativePaths["pathDemo"]=route;inventory.paths=new[]{"pathDemo"};
            Check(fields["路径速度 / 像素每帧"]==null,"Unassigned object exposes path controls");
            Call(form,"AttachGmPath",track,"pathDemo");
            Check(track.nativePath=="pathDemo"&&TypeDescriptor.GetProperties(inspector.SelectedObject)["路径速度 / 像素每帧"]!=null,"Path binding does not appear in the selected object inspector");
            using(var canvas=new GmPathCanvas(route,room,null){Size=new Size(520,400)}){
                Call(canvas,"OnMouseDown",new MouseEventArgs(MouseButtons.Left,2,285,290,0));
                Check(route.points.Count==3&&canvas.SelectedPoint!=null,"Double click did not add a point in the native Path editor");
                canvas.Remove();Check(route.points.Count==2,"Removing a selected path point failed");
            }
            ((Timer)Get(form,"saveTimer")).Stop();
        }
    }
    private static void ObjectEditor(string folder)
    {
        var data=new GmAssetData{name="objExample",kind="objects",@object=new GmObjectData{sprite="sprExample",depth=-7500000},
            events=new[]{new GmAssetSection{id="Create_0",actions=new[]{
                new GmAssetAction{index=0,id=201,lib=1,visual=new GmVisualAction{kind="create",target="objChild",x="24",y="32"}},
                new GmAssetAction{index=1,id=603,lib=1,editable=true}}},
                new GmAssetSection{id="Step_1"}}};
        var inventory=new Inventory{objects=new[]{"objExample","objChild"},sprites=new[]{"sprExample"},scripts=new[]{"scrExample"}};
        using(var form=new GmObjectEditor(null,inventory,data)){
            var layout=(TableLayoutPanel)form.Controls[0];
            Check(layout.ColumnCount==3&&layout.Controls.Count==3,"Object editor is missing its three panes");
            var events=(ListBox)Get(form,"eventsList");var actions=(ListBox)Get(form,"actionsList");
            Check(events.SelectedIndex==0&&actions.Items.Count==2&&actions.Items[0].ToString().Contains("objChild"),"Selected event actions are missing");
            actions.SelectedIndex=0;
            Check(((Button)Get(form,"editAction")).Enabled&&((Button)Get(form,"down")).Enabled&&!((Button)Get(form,"up")).Enabled,"Selected visual action cannot be edited or moved");
            events.SelectedIndex=1;
            Check(actions.Items.Count==0&&((Button)Get(form,"addAction")).Enabled&&!((Button)Get(form,"editAction")).Enabled,"Empty event shows the wrong action state");
            var draft=(GmObjectData)Call(form,"ReadProperties");
            Check(draft.sprite=="sprExample"&&draft.depth==-7500000,"Object fields did not populate");
            var motion=new GmVisualAction{kind="motion",speed="choose(2,4)",direction="random(360)",appliesTo="other"};
            var fields=TypeDescriptor.GetProperties(Call(form,"ActionFields",motion));
            Check((string)fields["作用对象"].GetValue(null)=="碰撞另一方（other）","Action target does not display its friendly name");
            fields["速度 / 像素每帧"].SetValue(null,"6");
            Check(motion.speed=="6"&&motion.direction=="random(360)","Editing speed overwrote the direction expression");
            fields["作用对象"].SetValue(null,"objChild");
            Check(motion.appliesTo=="objChild","The target label was stored in place of the GM identifier");
            var script=new GmVisualAction{kind="script",target="scrExample"};
            fields=TypeDescriptor.GetProperties(Call(form,"ActionFields",script));
            fields["argument4"].SetValue(null,"choose(1,3)");
            var clone=script.Snapshot();clone.arguments[4]="7";
            Check(script.arguments[4]=="choose(1,3)","Editing a script draft mutated the original action arguments");
            var sprite=new GmVisualAction{kind="sprite",target="sprExample"};
            fields=TypeDescriptor.GetProperties(Call(form,"ActionFields",sprite));
            Check((string)fields["贴图"].GetValue(null)=="sprExample","Sprite selection did not retain the GM resource identifier");
            events.SelectedIndex=0;
            form.Size=form.MinimumSize;form.StartPosition=FormStartPosition.Manual;
            form.Location=new Point(SystemInformation.VirtualScreen.Right+100,0);form.Opacity=0;
            form.Show();form.PerformLayout();
            Check(events.Height>150&&actions.Height>150&&((Button)Get(form,"finish")).Height>=32,"Object controls are clipped at minimum size");
            using(var bitmap=new Bitmap(form.Width,form.Height)){
                form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));
                bitmap.Save(Path.Combine(folder,"gm-object-editor.png"));
            }
        }
    }
    private sealed class PathProbe {public Dictionary<string,GmPath> paths=new Dictionary<string,GmPath>();public int[] frames=new int[0];}
    private static void NativeFilmProbe(string input,string movie,string output)
    {
        var data=Json.Read<LoadData>(File.ReadAllText(input));Motion.Normalize(data.state);
        var result=new List<object>();
        using(var film=new NativeFilmReader(movie)){
            Check(film.Info.nativeTrackIds.Contains("clip"),"Cached movie lost its native clip identifiers");
            foreach(int frame in new[]{1,5,8,20}){
                var bitmap=film.Frame(frame);int red=0,blue=0,left=Int32.MaxValue;
                for(int y=60;y<150;y++)for(int x=50;x<220;x++){
                    var color=bitmap.GetPixel(x,y);
                    if(color.R>180&&color.G<90&&color.B<90){red++;left=Math.Min(left,x);}
                    if(color.B>180&&color.R<90&&color.G<90)blue++;
                }
                result.Add(new{frame,red,blue,left});
            }
            using(var sprites=new SpriteStore{Project=data.project})using(var cache=new PreviewCache()){
                using(var composite=PreviewRenderer.Render(data.state,data.room,new Dictionary<string,string>(),sprites,8,false,false,cache,nativeFilmPath:movie))
                    for(int y=91;y<109;y++)for(int x=91;x<109;x++)
                        Check(composite.GetPixel(x,y).ToArgb()==film.Frame(8).GetPixel(x,y).ToArgb(),"A static native placeholder was drawn over the recorded actor");
                using(var staticView=PreviewRenderer.Render(data.state,data.room,new Dictionary<string,string>(),sprites,8,false,false,cache)){
                    staticView.Save(Path.Combine(Path.GetDirectoryName(output),"static-marker.png"));
                    int marker=0;for(int y=91;y<109;y++)for(int x=91;x<109;x++)if(ColorAt(staticView,x,y,Theme.Muted))marker++;
                    Check(marker>0,"Disabling a cached native film did not restore static editing markers");
                }
            }
            int saved=film.Frame(8).GetPixel(110,100).ToArgb();film.Frame(1);
            Check(film.Frame(8).GetPixel(110,100).ToArgb()==saved,"Seeking native history changed a recorded frame");
        }
        File.WriteAllText(output,Json.Write(result));
    }
    private static void DepthAndPlayer(string folder)
    {
        foreach(var item in new[]{Tuple.Create("sprDepthRed",Color.Red),Tuple.Create("sprDepthBlue",Color.Blue),Tuple.Create("sprDepthGreen",Color.Lime)}){
            Image(folder,"sprites/"+item.Item1+"/0.png",item.Item2,32,32);
            File.WriteAllText(Path.Combine(folder,"sprites/"+item.Item1+"/sprite.txt"),"frames=1\norigin_x=16\norigin_y=16\n");
        }
        var state=new EditorState{duration=100,room="rAvoidance"};
        for(int i=0;i<2;i++){
            state.tracks.Add(new Track{id="red"+i,name="Red",kind="object",sprite="sprDepthRed",x=100+i*100,y=100,start=0,end=90,depth=i==0?100:400});
            state.tracks.Add(new Track{id="blue"+i,name="Blue",kind="rod",sprite="sprDepthBlue",x=100+i*100,y=100,start=0,end=90,count=1,length=0,depth=i==0?200:300});
        }
        for(int i=0;i<2;i++)state.tracks.Add(new Track{id="room"+i,name="Room layer",kind="object",sprite="sprDepthRed",x=300+i*100,y=100,start=0,end=90,depth=i==0?200:100});
        for(int i=0;i<2;i++){
            state.tracks.Add(new Track{id="shot-red"+i,name="Red object",kind="object",sprite="sprDepthRed",x=500+i*100,y=100,start=0,end=90,depth=150});
            state.tracks.Add(new Track{id="shot"+i,name="Blue bullet",kind="emitter",sprite="sprDepthBlue",x=500+i*100,y=100,start=0,end=90,depth=i==0?200:100,count=1,speed=0,interval=100,life=60});
        }
        var room=new RoomData{name=state.room,width=800,height=608};
        room.instances.Add(new RoomInstance{id="a1",name="objDepthGreen",x=300,y=100,depth=150});
        room.instances.Add(new RoomInstance{id="a2",name="objDepthGreen",x=400,y=100,depth=150});
        Motion.Normalize(state);
        using(var sprites=new SpriteStore{Project=folder})using(var bitmap=PreviewRenderer.Render(state,room,new Dictionary<string,string>{{"objDepthGreen","sprDepthGreen"}},sprites,5,false,false)){
            Check(ColorAt(bitmap,100,100,Color.Red)&&ColorAt(bitmap,200,100,Color.Blue),"Object/formation depth order is incorrect");
            Check(ColorAt(bitmap,300,100,Color.Lime)&&ColorAt(bitmap,400,100,Color.Red),"Object depth does not interleave with room instances");
            Check(ColorAt(bitmap,500,100,Color.Red)&&ColorAt(bitmap,600,100,Color.Blue),"Bullet depth does not interleave with objects");
            bitmap.Save(Path.Combine(folder,"depth-preview.png"));
        }
        File.WriteAllText(Path.Combine(folder,"depth-scene.json"),Json.Write(state));
        var restored=Json.Read<EditorState>(Json.Write(state.Snapshot()));
        Check(restored.tracks[0].depth==100&&TrackPreset.Capture("Depth",state.tracks,50).tracks[1].depth==200,"Depth was lost in snapshot/JSON/preset capture");
        var generated=new SimBullet{source=state.tracks[0],generation=2};
        Check(DrawingDepth.Bullet(generated)==98,"Generated members do not inherit custom depth");
        Check(DrawingDepth.Track(new Track{kind="object"})==-7000000&&DrawingDepth.Track(new Track{kind="rod"})==-100,"Older projects lost their default depths");
        var move=new Track{id="move",name="Teleport",kind="control",eventPreset="player-move",start=20,end=20,playerTargetX=315,playerTargetY=240};
        state.tracks.Add(move);Motion.Normalize(state);
        using(var canvas=new StageCanvas{State=state,Room=room,SelectedTrack=move})using(var bitmap=new Bitmap(800,608))using(var g=Graphics.FromImage(bitmap)){
            g.Clear(Color.Black);Call(canvas,"PaintPlayerMove",g);
            Check(ColorAt(bitmap,332,240,Color.Cyan),"Selected player-move event does not preview its target before its start");
            move.playerTargetX=410;g.Clear(Color.Black);Call(canvas,"PaintPlayerMove",g);
            Check(ColorAt(bitmap,427,240,Color.Cyan)&&ColorAt(bitmap,332,240,Color.Black),"Teleport preview did not follow edited coordinates");
            bitmap.Save(Path.Combine(folder,"teleport-preview.png"));
        }
        move.expressions["playerTargetX"]="player.x+25";
        var target=EventPresets.PlayerTarget(state,move,0);
        Check(target.x==state.nativeTest.x+25,"Teleport preview ignored its coordinate expression");
    }
    private static void SamplePaths(string input,string output)
    {
        var data=Json.Read<PathProbe>(File.ReadAllText(input));
        var result=new Dictionary<string,double[][]>();
        foreach(var pair in data.paths){
            var state=new EditorState{nativePaths=data.paths};
            var track=new Track{id="probe",kind="emitter",x=240,y=160,nativePath=pair.Key,nativePathFrames=30,nativePathLoop=true};
            state.tracks.Add(track);Motion.Normalize(state);
            result[pair.Key]=data.frames.Select(frame=>{var p=Motion.At(track,frame);return new[]{p.x,p.y};}).ToArray();
        }
        File.WriteAllText(output,Json.Write(result));
    }
    [STAThread]public static int Main(string[] args)
    {
        try{
            if(args[0]=="--path-probe"){SamplePaths(args[1],args[2]);return 0;}
            if(args[0]=="--native-film-probe"){NativeFilmProbe(args[1],args[2],args[3]);return 0;}
            Application.EnableVisualStyles();RoomGraphics(args[0]);ObjectClip(args[0]);ObjectEditor(args[0]);DepthAndPlayer(args[0]);Console.WriteLine("PASS: room art, native resources, depth ordering and editable teleport preview.");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
