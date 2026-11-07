using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class UiSimplificationTests
{
    private static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    private static object Get(object o,string name){return o.GetType().GetField(name,Flags).GetValue(o);}
    private static void Set(object o,string name,object value){o.GetType().GetField(name,Flags).SetValue(o,value);}
    private static object Call(object o,string name,params object[] args){return o.GetType().GetMethod(name,Flags).Invoke(o,args);}
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    private static IEnumerable<ToolStripItem> Items(ToolStripItemCollection items){foreach(ToolStripItem item in items){yield return item;var drop=item as ToolStripDropDownItem;if(drop!=null)foreach(var child in Items(drop.DropDownItems))yield return child;}}
    private static PropertyDescriptorCollection Bag(MainForm form){return TypeDescriptor.GetProperties(((ParameterInspector)Get(form,"properties")).SelectedObject);}
    private static void Select(MainForm form,Track track){Call(form,"SelectTrack",track,null);}
    private static MainForm Editor(string root,params Track[] tracks)
    {
        var state=new EditorState{duration=600,laneCount=4,music="",tracks=tracks.ToList()};Motion.Normalize(state);
        var form=new MainForm(root);Set(form,"state",state);Set(form,"document",new LoadData{project=root,state=state,room=new RoomData(),inventory=new Inventory{sprites=new[]{"sprCherry",Rainbow.Sprite},objects=new string[0],scripts=new string[0]}});
        ((TimelineCanvas)Get(form,"timeline")).State=state;return form;
    }
    private static void Menus(string root)
    {
        var item=new Track{id="menu",kind="object",name="Menu object",end=200,keyframes=new List<Keyframe>{new Keyframe{property="x",frame=0,value=300},new Keyframe{property="x",frame=100,value=500},new Keyframe{property="y",frame=0,value=250}}};
        using(var form=Editor(root,item)){
            Select(form,item);var timeline=(TimelineCanvas)Get(form,"timeline");timeline.ActiveLane=0;
            using(var menu=new ContextMenuStrip()){
                Call(form,"CompactTrackContext",menu);Check(menu.Items.Count<=7,"Object menu exceeds seven entries");
                Check(!menu.Items.Cast<ToolStripItem>().Any(i=>i.Text.Contains("撤销")||i.Text.Contains("添加物件")),"Object menu retains global command duplicates");
                Check(Items(menu.Items).Any(i=>i.Text=="存为自定义预设…")&&Items(menu.Items).Any(i=>i.Text=="轨道归属组"),"Advanced object/group actions became inaccessible");
                var more=(ToolStripMenuItem)menu.Items.Cast<ToolStripItem>().Single(i=>i.Text=="更多物件操作");var hide=more.DropDownItems.Cast<ToolStripItem>().Single(i=>i.Text=="仅在预览中隐藏此物件");hide.PerformClick();Check(item.previewHidden,"Preview visibility command did not run");Call(form,"Undo",false);
            }
            item=((EditorState)Get(form,"state")).tracks[0];Select(form,item);
            using(var menu=(ContextMenuStrip)Call(form,"CreateContext",true)){
                Call(menu,"OnOpening",new CancelEventArgs());Check(menu.Items.Count<=4,"Property context retains old long menu");Check(!Items(menu.Items).Any(i=>i.Text.Contains("删除所选物件")||i.Text=="轨道与组"),"Property menu mixes object management with parameter editing");
                Call(menu,"OnOpening",new CancelEventArgs());Check(menu.Items.Count<=4,"Opening menu twice duplicates commands");
            }
            timeline.SelectedProperty="x";timeline.SelectedKeys.Add(item.keyframes[0]);
            using(var menu=new ContextMenuStrip()){
                Call(form,"ParameterContext",menu,true);Check(Items(menu.Items).Any(i=>i.Text.Contains("复制关键帧")),"Keyframe copy missing");
                menu.Items.Cast<ToolStripItem>().Single(i=>i.Text=="清除此属性的全部关键帧").PerformClick();Check(!item.keyframes.Any(k=>k.property=="x")&&item.keyframes.Any(k=>k.property=="y")&&item.x==300,"Clear property keys changed other properties or lost current value");
            }
            ((Timer)Get(form,"saveTimer")).Stop();
        }
        Console.WriteLine("PASS: contextual menus stay compact; advanced commands remain reachable; visibility/Undo and property-only key clearing work.");
    }
    private static void Properties(string root)
    {
        var rod=new Track{id="rod",kind="rod",shape="rod",end=200};var emitter=new Track{id="emit",kind="emitter",end=200,emissionShape="line"};var effect=new Track{id="fx",kind="effect",effect="rgb",end=200};var preset=new Track{id="preset",kind="object",end=200};
        using(var form=Editor(root,rod,emitter,effect,preset)){
            Select(form,rod);var fields=Bag(form);Check(fields["端点1 X"]==null&&fields["端点2 X"]==null&&fields["滚动速度"]==null&&fields["棒长"]!=null,"Center rod exposes unused endpoints/rolling");Check(fields["飞行方向"]==null&&fields["散开后的弹速"]==null,"Unconfigured formations expose scatter fields");
            Check(fields["Power 指数"]==null&&fields["ExpSin 周期"]==null,"Unrelated easing controls remain visible");
            rod.rodMode="endpoints";rod.rolling=true;rod.releaseFrame=100;Select(form,rod);fields=Bag(form);Check(fields["端点1 X"]!=null&&fields["端点2 X"]!=null&&fields["棒长"]==null&&fields["滚动速度"]!=null&&fields["飞行方向"]!=null&&fields["保持原形状"]!=null,"Active rod controls missing");
            rod.rodMode="center";rod.rolling=false;rod.keyframes.Add(new Keyframe{property="x2",frame=0,value=100});rod.expressions["rollSpeed"]="choose(1,2)";Select(form,rod);Check(Bag(form)["端点2 X"]!=null&&Bag(form)["滚动速度"]!=null,"Existing keys/expressions were hidden");
            rod.easing="power-in";Select(form,rod);Check(Bag(form)["Power 指数"]!=null&&Bag(form)["ExpSin 周期"]==null,"Power easing dependency incorrect");rod.keyframes.Add(new Keyframe{property="alpha",frame=100,value=.5,easing="expsin"});Select(form,rod);Check(Bag(form)["ExpSin 周期"]!=null,"Per-key easing parameter inaccessible");
            Select(form,emitter);fields=Bag(form);Check(fields["阵列宽度"]!=null&&fields["阵列高度"]==null&&fields["圆弧角度"]==null&&fields["波形周期数"]==null,"Line array shows unrelated shape parameters");Check(fields["弹幕标签"].Category=="高级设置"&&fields["每颗弹速递增"].Category=="高级设置","Rare configuration not grouped");
            emitter.emissionShape="sine";Select(form,emitter);Check(Bag(form)["阵列高度"]!=null&&Bag(form)["波形周期数"]!=null,"Sine configuration missing");
            Select(form,effect);Check(Bag(form)["遮罩 / 闪屏颜色"]==null&&Bag(form)["帧混合比例"]==null&&Bag(form)["效果方向"]!=null,"RGB exposes irrelevant color/history settings");effect.effect="fade-out";Select(form,effect);Check(Bag(form)["遮罩 / 闪屏颜色"]!=null&&Bag(form)["效果方向"]==null,"Fade settings not contextual");
            MotionPresets.Apply(preset,"圆周运动",0);Select(form,preset);Check(Bag(form)["运动速度 / 圈每秒"]!=null&&Bag(form)["运动圈数"]==null,"Preset keeps duplicate speed/cycle inputs");
            var inspector=(ParameterInspector)Get(form,"properties");Check(inspector.CaptureSections().Contains("高级设置"),"Advanced configuration should default to folded");
        }
        Console.WriteLine("PASS: inactive settings hide conditionally; existing keys/formulas remain editable; shape, release, easing and preset dependencies are preserved.");
    }
    private static void OptionalProperties(string root)
    {
        var rod=new Track{id="formation",kind="rod",shape="ring",sprite="sprCherry",start=20,end=150};
        using(var form=Editor(root,rod)){
            Select(form,rod);
            foreach(string group in ObjectProperties.Names)Check(!Bag(form).Cast<PropertyDescriptor>().Any(p=>p.Category==group),"An optional group is shown initially: "+group);
            Set(form,"resourceKind",ObjectProperties.Kind);
            Call(form,"FillResources");
            Check(((ListView)Get(form,"resources")).Items.Count==5,"Object property library does not list all presets");
            foreach(string key in new[]{"rainbow","trail","lifetime","bounce","scatter"})
                ((System.Threading.Tasks.Task)Call(form,"UsePreset",ObjectProperties.Kind,key,rod,71)).GetAwaiter().GetResult();
            var fields=Bag(form);
            Check(rod.rainbow&&rod.sprite==Rainbow.Sprite&&Trails.Mode(rod)=="fade"&&rod.lifetimeMode=="room"&&rod.boundaryMode=="reflect"&&rod.releaseFrame==71,"Preset application did not configure formation");
            Check(ObjectProperties.Names.All(group=>fields.Cast<PropertyDescriptor>().Any(p=>p.Category==group)),"Applied group is missing from inspector");
            Call(form,"Undo",false);
            var state=(EditorState)Get(form,"state");
            Check(state.tracks[0].releaseFrame<0&&state.tracks[0].boundaryMode=="reflect","Undo removed a previous property or did not remove scatter");
            rod=state.tracks[0];
            var resources=(ListView)Get(form,"resources");
            Func<ListViewItem> trailItem=()=>resources.Items.Cast<ListViewItem>().Single(i=>((PresetEntry)i.Tag).key=="trail");
            Check(trailItem().Text.StartsWith("✓ "),"Visible module is not marked in the resource list");
            rod.trailSpeed=7.5;
            using(var menu=new ContextMenuStrip()){
                Call(form,"ParameterContext",menu,false);
                Items(menu.Items).Single(i=>i.OwnerItem!=null&&i.OwnerItem.Text=="收回物件属性"&&i.Text=="拖影").PerformClick();
            }
            Check(!ObjectProperties.Shows(rod,1)&&!Bag(form).Cast<PropertyDescriptor>().Any(p=>p.Category=="拖影")&&!trailItem().Text.StartsWith("✓ "),"Hiding a module did not update both panels");
            Check(Trails.Mode(rod)=="fade"&&rod.trailSpeed==7.5&&!ObjectProperties.Shows(Json.Read<Track>(Json.Write(rod)),1),"Hiding changed the effect or was lost in JSON");
            Call(form,"Undo",false);
            rod=((EditorState)Get(form,"state")).tracks[0];
            Check(ObjectProperties.Shows(rod,1)&&trailItem().Text.StartsWith("✓ "),"Undo did not restore the module");
            Call(form,"HideObjectProperty",rod,1);
            ((System.Threading.Tasks.Task)Call(form,"UsePreset",ObjectProperties.Kind,"trail",rod,95)).GetAwaiter().GetResult();
            Check(ObjectProperties.Shows(rod,1)&&rod.trailSpeed==7.5&&Trails.Mode(rod)=="fade","Reapplying a hidden property reset its settings");
            Trails.SetMode(rod,"off");
            int version=(int)Get(form,"revision");
            int undoCount=((System.Collections.ICollection)Get(form,"undo")).Count;
            ((System.Threading.Tasks.Task)Call(form,"UsePreset",ObjectProperties.Kind,"trail",rod,100)).GetAwaiter().GetResult();
            Check(version==(int)Get(form,"revision")&&undoCount==((System.Collections.ICollection)Get(form,"undo")).Count&&Trails.Mode(rod)=="off","Reapplying a visible property changed values, saved or added an undo entry");
            Call(form,"AddTrack","emitter",null);
            Check(((EditorState)Get(form,"state")).tracks.Last().lifetimeMode=="room","New emitter did not default to room lifetime");
            ((Timer)Get(form,"saveTimer")).Stop();
        }
        Console.WriteLine("PASS: optional property markers, hide/restore/undo and persisted effect settings, no save on repeated apply, room lifetime");
    }
    private static void Images(string root)
    {
        foreach(string language in new[]{"zh-CN","en"}){
            Language.Select(root,language);var track=new Track{id="image",kind="emitter",name="Preview",emissionShape="line",end=200};
            using(var form=Editor(root,track)){
                Select(form,track);var inspector=(ParameterInspector)Get(form,"properties");inspector.HelpVisible=false;
                var original=inspector.Parent;original.Controls.Remove(inspector);
                using(var host=new Panel{Size=new Size(language=="en"?340:440,760),BackColor=Theme.Panel}){
                    host.Controls.Add(inspector);inspector.Dock=DockStyle.Fill;Language.Attach(inspector);host.CreateControl();host.PerformLayout();inspector.PerformLayout();
                    using(var image=new Bitmap(host.Width,host.Height)){host.DrawToBitmap(image,host.ClientRectangle);image.Save(Path.Combine(root,"properties-"+language+".png"));}
                    host.Controls.Remove(inspector);
                }
                original.Controls.Add(inspector);
            }
            foreach(string key in new[]{"高级设置","轨道与组","编辑行为","编辑参数公式…"})Check(language!="en"||Language.Text(key)!=key,"Missing English text: "+key);
        }
        Language.Select(root,"zh-CN");
    }
    [STAThread]public static int Main(string[] args)
    {
        try{Directory.CreateDirectory(args[0]);Language.Select(args[0],"zh-CN");Menus(args[0]);Properties(args[0]);OptionalProperties(args[0]);Images(args[0]);return 0;}
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
