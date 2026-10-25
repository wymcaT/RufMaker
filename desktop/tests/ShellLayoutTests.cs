using System;
using System.IO;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class ShellLayoutTests
{
    private static object Get(object o,string name){return o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);}
    private static void Set(object o,string name,object value){o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,value);}
    private static object Call(object o,string name,params object[] args){return o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);}
    private static void Check(bool test,string message){if(!test)throw new Exception(message);}
    private static IEnumerable<Control> All(Control root){yield return root;foreach(Control child in root.Controls)foreach(var item in All(child))yield return item;}
    private static IEnumerable<ToolStripItem> Items(ToolStrip strip){foreach(ToolStripItem item in strip.Items){yield return item;var drop=item as ToolStripDropDownItem;if(drop!=null)foreach(var child in Children(drop))yield return child;}}
    private static IEnumerable<ToolStripItem> Children(ToolStripDropDownItem drop){foreach(ToolStripItem child in drop.DropDownItems){yield return child;var sub=child as ToolStripDropDownItem;if(sub!=null)foreach(var leaf in Children(sub))yield return leaf;}}
    private static void Render(MainForm form,string path,int width,int height)
    {
        form.ClientSize=new Size(width,height);var content=form.Controls.OfType<TableLayoutPanel>().Single();form.Controls.Remove(content);
        using(var surface=new Panel{Size=form.ClientSize,Font=form.Font,BackColor=form.BackColor,ForeColor=form.ForeColor}){surface.Controls.Add(content);surface.CreateControl();surface.PerformLayout();Call(form,"RestorePanelLayout");surface.PerformLayout();var stage=(StageCanvas)Get(form,"stage");Call(stage,"Fit");using(var matrix=(System.Drawing.Drawing2D.Matrix)Call(stage,"DisplayMatrix")){var room=(LoadData)Get(form,"document");var image=PreviewRenderer.Render((EditorState)Get(form,"state"),room.room,room.inventory.objectSprites,stage.Sprites,(int)Get(form,"frame"),true,false,null,matrix,stage.Width,stage.Height);var old=Get(stage,"image") as Bitmap;Set(stage,"image",image);if(old!=null)old.Dispose();}File.WriteAllLines(path+".visible.txt",All((ParameterInspector)Get(form,"properties")).Select(c=>c.GetType().Name+" "+c.Text+" "+c.Bounds+" visible="+c.Visible));using(var bitmap=new Bitmap(width,height)){surface.DrawToBitmap(bitmap,new Rectangle(0,0,width,height));bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);}surface.Controls.Remove(content);}form.Controls.Add(content);
        var rows=(TableLayoutPanel)Get((ParameterInspector)Get(form,"properties"),"rows");File.WriteAllLines(path+".layout.txt",rows.Controls.Cast<Control>().Select(c=>c.GetType().Name+" "+c.Text+" "+c.Bounds+" visible="+c.Visible+" cell="+rows.GetRow(c)+","+rows.GetColumn(c)));
        Check(((ToolStrip)Get(form,"mainTools")).Height<65&&((ToolStrip)Get(form,"transportTools")).Height<65,"Toolbar wrapped");
    }
    [STAThread]public static int Main(string[] args)
    {
        try{Application.EnableVisualStyles();string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);Language.Select(root,"zh-CN");using(var form=new MainForm(root)){
            var track=new Track{id="star",kind="rod",shape="star2",name="周期星星",start=0,end=800,lane=0,count=60,rainbow=true,sprite="sprrainbowcherry"};var template=new Track{id="echo",name="扩散副本",kind="emitter",templateOnly=true,start=0,end=90,lane=1};track.spawnActions.Add(new BulletAction{type="spawn",templateId=template.id,spawnRepeat=50});var state=new EditorState{name="界面验证",fps=50,duration=1200,laneCount=2,tracks=new List<Track>{track,template}};Motion.Normalize(state);
            var doc=new LoadData{state=state,project=root,projectName=state.name,waveform=new WaveData{duration=24,peaks=Enumerable.Range(0,400).Select(i=>Math.Abs(Math.Sin(i*.31))).ToArray()},inventory=new Inventory{objects=new string[0],sprites=new[]{"sprrainbowcherry"},scripts=new string[0]},room=new RoomData{width=800,height=608}};
            string folder=Path.Combine(root,"sprites","sprrainbowcherry");Directory.CreateDirectory(folder);string sprite=Path.GetFullPath(Path.Combine(root,"../../../assets/sprite-packs/Ultimate Avoidance Sprite Pack/Cherry Sprites/Cherry White.png"));File.Copy(sprite,Path.Combine(folder,"0.png"),true);var store=new SpriteStore{Project=root,Metadata=new Dictionary<string,SpriteMeta>{{"sprrainbowcherry",new SpriteMeta{originX=16,originY=16}}}};
            Set(form,"state",state);Set(form,"document",doc);Set(form,"sprites",store);var timeline=(TimelineCanvas)Get(form,"timeline");timeline.State=state;timeline.Wave=doc.waveform;var stage=(StageCanvas)Get(form,"stage");stage.Sprites=store;stage.State=state;stage.Room=doc.room;Call(form,"SelectTrack",track,null);Call(form,"SetFrame",500);Call(form,"UpdateTitle");Language.Attach(form);string original=Json.Write(state);
            var all=All(form).OfType<ToolStrip>().SelectMany(Items).ToArray();foreach(string name in new[]{"文件","编辑","视图","工程","工具","帮助","添加弹幕","添加整体对象","添加拉镜","添加对象","添加特效","添加 GML 事件","添加弹幕控制","检测更新","切换语言"})Check(all.Any(i=>i.Text==name),"Missing command: "+name);
            var bars=All(form).OfType<ToolStrip>().Where(t=>new[]{"MainTools","TransportTools"}.Contains(t.Name)).ToArray();Check(bars.Length==2,"Command surfaces missing");Check(((SelectionTimeControl)Get(form,"rightTime")).PreferredSize.Height<130,"Right time panel too tall");
            Call(form,"FocusBehavior",true,0);Check(((InspectorTabs)Get(form,"inspectorTabs")).SelectedIndex==1,"Rule did not focus behavior page");Call(form,"EnterTemplateContext",template);Check(Get(form,"templateEditing")==template&&stage.TemplatePreviewId==template.id,"Template context absent");var render=(EditorState)Get(stage,"renderState");Check(render.tracks.Single(t=>t.id==template.id).templateOnly==false&&render.tracks.Single(t=>t.id==track.id).templateOnly,"Local preview did not isolate the template");Call(form,"ExitTemplateContext");Check((int)Get(form,"frame")==500&&Get(form,"selectedTrack")==track&&stage.TemplatePreviewId==null,"Template return lost source/time");
            Call(form,"SetResourcePage",true);Set(form,"resourceKind","camera-presets");Call(form,"FillResources");Check(((ListView)Get(form,"resources")).Items.Count==CameraPresets.Names.Length,"Preset browser empty");
            ((InspectorTabs)Get(form,"inspectorTabs")).SelectedIndex=0;Render(form,Path.Combine(root,"shell-zh.png"),1500,900);Render(form,Path.Combine(root,"shell-narrow.png"),1100,740);
            Language.Select(root,"en");form.RefreshLanguageUI();Render(form,Path.Combine(root,"shell-en.png"),1500,900);Check(SceneComparison.Same(Json.Read<EditorState>(original),state),"Layout or language mutated scene");
            Check(Items((MenuStrip)Get(form,"shellMenu")).Any(i=>i.Text=="File"),"Menu translation missing");Call(form,"SelectTrack",track,null);var inspector=(ParameterInspector)Get(form,"properties");var controls=All(inspector).ToArray();Call(form,"AddPropertyKey","alpha");Call(form,"Undo",false);Check(Get(form,"selectedTrack")!=null,"Undo lost selection");Check(controls.Count(c=>!c.IsDisposed)>controls.Length*.8,"Key undo rebuilt inspector");
            Console.WriteLine("PASS: command routing, fixed bars, presets, behavior page, shared time, template return, language/scene preservation and undo. Screenshots: "+root);
        }return 0;}catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
