using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class LocalizationTests
{
    private static readonly BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static object Get(object obj,string name){return obj.GetType().GetField(name,flags).GetValue(obj);}
    private static void Set(object obj,string name,object value){obj.GetType().GetField(name,flags).SetValue(obj,value);}
    private static object Call(object obj,string name,params object[] args){return obj.GetType().GetMethod(name,flags).Invoke(obj,args);}
    private static IEnumerable<Control> All(Control c){yield return c;foreach(Control child in c.Controls)foreach(Control item in All(child))yield return item;}
    private static void Assets(string root)
    {
        string folder=Path.Combine(root,"migration");Directory.CreateDirectory(Path.Combine(folder,"desktop","assets"));Directory.CreateDirectory(Path.Combine(folder,"Ultimate Avoidance Sprite Pack","Cherry"));Directory.CreateDirectory(Path.Combine(folder,"projects"));
        File.WriteAllText(Path.Combine(folder,"icon.ico"),"updated icon");File.WriteAllText(Path.Combine(folder,"sprrainbowcherry.gmspr"),"sprite");File.WriteAllText(Path.Combine(folder,"desktop","assets","sprRufBlock.png"),"terrain");File.WriteAllText(Path.Combine(folder,"Ultimate Avoidance Sprite Pack","Cherry","a.png"),"pack");File.WriteAllText(Path.Combine(folder,"projects","keep.rufmaker"),"user project");
        string stagedGml=Path.Combine(folder,"templates","avoidance.gm82","rufmaker-assets","gml","formations");Directory.CreateDirectory(stagedGml);File.WriteAllText(Path.Combine(stagedGml,"heart.gml"),"original heart");
        AppPaths.ArrangeAssets(folder);AppPaths.ArrangeAssets(folder);Check(File.ReadAllText(Path.Combine(folder,"assets","gml","formations","heart.gml"))=="original heart","Legacy updater GML transport was not arranged");
        Check(File.ReadAllText(Path.Combine(folder,"assets","icons","icon.ico"))=="updated icon"&&!File.Exists(Path.Combine(folder,"icon.ico")),"Icon migration failed");Check(File.Exists(Path.Combine(folder,"assets","sprites","sprrainbowcherry.gmspr")),"Source sprite migration failed");Check(File.Exists(Path.Combine(folder,"assets","terrain","sprRufBlock.png")),"Terrain migration failed");Check(File.Exists(Path.Combine(folder,"assets","sprite-packs","Ultimate Avoidance Sprite Pack","Cherry","a.png")),"Pack migration failed");Check(File.ReadAllText(Path.Combine(folder,"projects","keep.rufmaker"))=="user project","Migration touched user data");
        File.WriteAllText(Path.Combine(folder,"icon.ico"),"next update");AppPaths.ArrangeAssets(folder);Check(File.ReadAllText(Path.Combine(folder,"assets","icons","icon.ico"))=="next update","Subsequent legacy update did not replace shipped assets");
    }
    private static void Interface(string root,string image)
    {
        using(var form=new MainForm(root)){
            var state=new EditorState{name="淡入",duration=500};var track=new Track{id="camera",kind="camera",name="柔和摇摆",start=0,end=200};state.tracks.Add(track);
            Set(form,"state",state);Set(form,"document",new LoadData{state=state,project=root,projectName="淡入",waveform=new WaveData(),inventory=new Inventory{objects=new string[0],sprites=new string[0],scripts=new string[0]}});
            ((TimelineCanvas)Get(form,"timeline")).State=state;Set(form,"resourceKind","camera-presets");Call(form,"SelectTrack",track,null);Language.Attach(form);
            string before=Json.Write(state);var add=All(form).OfType<Button>().Single(b=>b.Text=="添加特效");var snap=(CheckBox)Get(form,"keySnap");
            Language.Select(root,"en");Language.Apply(form);Call(form,"FillResources");Call(form,"ShowProperties");Call(form,"UpdateTitle");
            Check(add.Text=="Add effect"&&snap.Text=="Snap playhead to keyframes","Toolbar or checkbox did not switch");Check(All(form).OfType<Label>().Any(l=>l.Text=="Camera zoom"),"Inspector property did not switch");
            using(var choice=new WheelSafeComboBox{DropDownStyle=ComboBoxStyle.DropDownList}){choice.Items.Add("柔和摇摆");choice.SelectedIndex=0;int changed=0;choice.SelectedIndexChanged+=(s,e)=>changed++;choice.RefreshLanguage();Check(choice.GetItemText(choice.SelectedItem)=="Gentle sway"&&(string)choice.SelectedItem=="柔和摇摆"&&changed==0,"Localized combo changed its value or fired selection edits");}
            var list=(ListView)Get(form,"resources");Check(list.Items.Count==CameraPresets.Names.Length&&list.Items.Cast<ListViewItem>().Any(item=>item.Text=="Gentle sway"&&((PresetEntry)item.Tag).key=="柔和摇摆"),"Preset display changed its stored key");
            var search=(TextBox)Get(form,"search");search.Text="sway";Check(list.Items.Count==1,"English preset search failed");search.Clear();
            using(var menu=new ContextMenuStrip()){menu.Items.Add("添加特效");Language.Attach(menu);Check(menu.Items[0].Text=="Add effect","Menu not localized");menu.Items.Add("转转棒");Check(menu.Items[1].Text=="Rotating rod","Dynamic menu not localized");Language.Select(root,"zh-CN");Language.Attach(menu);Check(menu.Items[0].Text=="添加特效","Cached popup did not switch back");}
            Language.Select(root,"en");Language.Apply(form);Check(before==Json.Write(state),"Language switch modified project data");
            string code="// 淡入\r\nwith (player) { x=400; }";using(var editor=new CodeForm("test",code,s=>Task.FromResult(0))){Language.Attach(editor);Check(All(editor).OfType<TextBox>().Any(t=>t.Text==code),"GML source was translated");}
            string diagnostic=Language.Text("GameMaker 编译失败（7）。构建记录：D:\\项目\\新耐久\\compiler.log");Check(diagnostic=="GameMaker compilation failed (7). Build log: D:\\项目\\新耐久\\compiler.log","Diagnostic path or template changed: "+diagnostic);
            foreach(string value in MotionPresets.Names.Concat(CameraPresets.Names).Concat(EffectPresets.Names).Concat(Motion.EaseNames).Concat(Effects.Names).Concat(Trails.Names).Concat(FormationShapes.Names).Concat(FormationShapes.Values.SelectMany(FormationShapes.Fields).Select(Motion.Name)).Concat(BulletSystem.ActionNames).Concat(BulletSystem.ShapeNames).Concat(new[]{"仅用作生成模板","生成后销毁母体","重复间隔 / 帧（0 仅一次）","继承母体方向","物件生成行为…"}))Check(!System.Text.RegularExpressions.Regex.IsMatch(Language.Text(value),@"[\u3400-\u9fff]"),"Missing option translation: "+value);
            // Render the content in a parentless panel, so all children are drawn
            // without showing a Form or starting the project's Shown handler.
            ((ComboBox)Get(form,"languageChoice")).SelectedIndex=1;var content=form.Controls[0];form.Controls.Remove(content);using(var surface=new Panel{Size=form.ClientSize,Font=form.Font,BackColor=form.BackColor,ForeColor=form.ForeColor}){surface.Controls.Add(content);surface.PerformLayout();Call(form,"RestorePanelLayout");surface.PerformLayout();using(var bitmap=new Bitmap(surface.Width,surface.Height)){surface.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(image,System.Drawing.Imaging.ImageFormat.Png);}surface.Controls.Remove(content);}form.Controls.Add(content);
            Language.Initialize(root);Check(Language.English,"Language preference not persisted");Language.Select(root,"zh-CN");Language.Apply(form);Call(form,"FillResources");Call(form,"ShowProperties");Check(add.Text=="添加特效"&&list.Items.Cast<ListViewItem>().Any(item=>item.Text=="柔和摇摆")&&before==Json.Write(state),"Switch back did not restore Chinese or preserve state");
        }
    }
    [STAThread] public static int Main(string[] args)
    {try{if(args[0]=="--package"){
            string version=args.Length>4?args[4]:AppVersion.Current;var legacy=Assembly.LoadFile(Path.GetFullPath(args[2]));legacy.GetType("Updater").GetMethod("Extract",BindingFlags.Public|BindingFlags.Static).Invoke(null,new object[]{args[1],args[3],version});Updater.Validate(args[3],version);AppPaths.ArrangeAssets(args[3]);Check(File.Exists(Path.Combine(args[3],"assets","terrain","sprRufBlock.png"))&&Directory.Exists(Path.Combine(args[3],"assets","sprite-packs","Ultimate Avoidance Sprite Pack")),"Update layout incomplete");Console.WriteLine("PASS: previous updater ("+legacy.GetName().Version+") accepts and verifies "+version+"; new assets arrange successfully.");return 0;
        }Assets(args[0]);Interface(args[0],args[1]);Console.WriteLine("PASS: asset migration, live Chinese/English UI, parameter and preset translation, English search, stored option keys, unchanged GML/project data, diagnostic paths and persistent preference (no application window).");return 0;}catch(Exception error){Console.Error.WriteLine(error);return 1;}}
}
