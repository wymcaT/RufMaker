using System;
using System.IO;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class ShellLayoutTests
{
    private sealed class FocusProbe : Form{protected override bool ShowWithoutActivation{get{return true;}}public FocusProbe(){StartPosition=FormStartPosition.Manual;Location=new Point(-20000,-20000);ShowInTaskbar=false;ClientSize=new Size(340,420);}}
    private static object Get(object o,string name){return o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);}
    private static void Set(object o,string name,object value){o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,value);}
    private static object Call(object o,string name,params object[] args){return o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);}
    private static void Check(bool test,string message){if(!test)throw new Exception(message);}
    private static IEnumerable<Control> All(Control root){yield return root;foreach(Control child in root.Controls)foreach(var item in All(child))yield return item;}
    private static IEnumerable<ToolStripItem> Items(ToolStrip strip){foreach(ToolStripItem item in strip.Items){yield return item;var drop=item as ToolStripDropDownItem;if(drop!=null)foreach(var child in Children(drop))yield return child;}}
    private static IEnumerable<ToolStripItem> Children(ToolStripDropDownItem drop){foreach(ToolStripItem child in drop.DropDownItems){yield return child;var sub=child as ToolStripDropDownItem;if(sub!=null)foreach(var leaf in Children(sub))yield return leaf;}}
    private static void Sections()
    {
        Func<NativeProperties> create=()=>{var bag=new NativeProperties();bag.Add("贴图","外观",typeof(string),()=>"sprCherry",null);bag.Add("X","变换",typeof(double),()=>400.0,v=>{});return bag;};
        using(var inspector=new ParameterInspector{Compact=true,Size=new Size(330,500)}){
            inspector.SelectedObject=create();Check(inspector.CaptureSections().Contains("外观"),"Appearance should initially fold");
            var headings=(Dictionary<string,Button>)Get(inspector,"headings");headings["外观"].PerformClick();Check(inspector.CaptureSections().Contains("!外观"),"Opening a section was not captured");
            var other=new NativeProperties();other.Add("X","变换",typeof(double),()=>400.0,v=>{});inspector.SelectedObject=other;
            Check(inspector.CaptureSections().Contains("!外观"),"Switching object type forgot an open section");
            inspector.SelectedObject=create();Check(inspector.CaptureSections().Contains("!外观"),"Returning to object type re-collapsed a section");
            var rows=(TableLayoutPanel)Get(inspector,"rows");var controls=All(rows).ToArray();headings=(Dictionary<string,Button>)Get(inspector,"headings");headings["外观"].PerformClick();headings["外观"].PerformClick();Check(controls.All(c=>!c.IsDisposed&&All(rows).Contains(c)),"Fold/unfold recreated existing controls");
            using(var restored=new ParameterInspector{Compact=true}){restored.RestoreSections(inspector.CaptureSections());restored.SelectedObject=create();Check(restored.CaptureSections().Contains("!外观"),"Saved fold preference did not reopen");}
            inspector.ResetDefaultSections();Check(inspector.CaptureSections().Contains("外观")&&inspector.CaptureSections().Contains("!变换"),"Reset did not restore compact defaults");Check(controls.All(c=>!c.IsDisposed&&All(rows).Contains(c)),"Reset recreated existing parameter controls");
        }
    }
    private static void SectionPosition()
    {
        using(var surface=new FocusProbe())using(var inspector=new ParameterInspector{Dock=DockStyle.Fill}){
            surface.Controls.Add(inspector);var bag=new NativeProperties();for(int i=0;i<12;i++)bag.Add("Before "+i,"Before",typeof(double),()=>1.0,v=>{});for(int i=0;i<4;i++)bag.Add("Target "+i,"Target",typeof(double),()=>2.0,v=>{});for(int i=0;i<25;i++)bag.Add("After "+i,"After",typeof(double),()=>3.0,v=>{});
            inspector.SelectedObject=bag;surface.Show();Application.DoEvents();surface.PerformLayout();inspector.PerformLayout();var scroll=(Panel)Get(inspector,"scroll");var rows=(TableLayoutPanel)Get(inspector,"rows");var heading=((Dictionary<string,Button>)Get(inspector,"headings"))["Target"];
            scroll.AutoScrollPosition=new Point(0,heading.Top-90);var offset=scroll.AutoScrollPosition;int top=heading.Top+rows.Top;var children=All(rows).ToArray();
            var focusPosition=(Point)Call(scroll,"ScrollToControl",heading);Check(focusPosition==offset,"Focusing a section header scrolls away from the user's location: "+offset+" -> "+focusPosition);
            heading.Focus();Application.DoEvents();Console.WriteLine("Section scroll before click: "+offset+"; after focus: "+scroll.AutoScrollPosition);heading.PerformClick();Application.DoEvents();Check(scroll.AutoScrollPosition==offset&&heading.Top+rows.Top==top,"Collapsing a section moved its visual anchor: "+offset+" -> "+scroll.AutoScrollPosition);heading.Focus();heading.PerformClick();Application.DoEvents();Check(scroll.AutoScrollPosition==offset&&heading.Top+rows.Top==top,"Expanding a section moved its visual anchor");Check(children.All(c=>!c.IsDisposed&&All(rows).Contains(c)),"Expanding rebuilt existing controls");var last=All(rows).OfType<ScrubNumber>().Last();last.Focus();Application.DoEvents();Check(scroll.AutoScrollPosition.Y<offset.Y,"Header fix disabled normal keyboard focus scrolling");surface.Hide();
        }
    }
    private static void StripTheme(MainForm form,string root)
    {
        foreach(var strip in All(form).OfType<ToolStrip>()){
            Check(strip.Renderer is DarkStripRenderer,"Strip is using a light system border: "+strip.Name);
            foreach(var item in Items(strip).Where(i=>!(i is ToolStripSeparator)&&!(i is ToolStripControlHost))){Check(item.ForeColor.GetBrightness()>.3,"Dark menu text: "+item.Text);var drop=item as ToolStripDropDownItem;if(drop!=null)Check(drop.DropDown.Renderer is DarkStripRenderer,"Submenu renderer did not inherit dark theme: "+item.Text);}
        }
        using(var surface=new Panel{Size=new Size(520,95),BackColor=Theme.Panel})using(var strip=new ToolStrip{Dock=DockStyle.Top,Height=40,AutoSize=false,GripStyle=ToolStripGripStyle.Hidden}){
            strip.Items.Add("Toolbar label");var menu=new ToolStripDropDownButton("Menu");menu.DropDownItems.Add("Nested menu");strip.Items.Add(menu);surface.Controls.Add(strip);Language.Attach(strip);menu.DropDownItems.Add("Added later");Check(menu.DropDownItems.Cast<ToolStripItem>().All(i=>i.ForeColor.GetBrightness()>.3),"Dynamic submenu text is dark");surface.CreateControl();surface.PerformLayout();
            foreach(bool classic in new[]{false,true}){Application.VisualStyleState=classic?System.Windows.Forms.VisualStyles.VisualStyleState.NoneEnabled:System.Windows.Forms.VisualStyles.VisualStyleState.ClientAndNonClientAreasEnabled;using(var image=new Bitmap(strip.Width,strip.Height)){strip.DrawToBitmap(image,strip.ClientRectangle);Check(Enumerable.Range(0,image.Width).All(x=>image.GetPixel(x,image.Height-1).GetBrightness()<.5),"White toolbar edge");image.Save(Path.Combine(root,classic?"dark-strip-classic.png":"dark-strip.png"),System.Drawing.Imaging.ImageFormat.Png);}}
            Application.VisualStyleState=System.Windows.Forms.VisualStyles.VisualStyleState.ClientAndNonClientAreasEnabled;
        }
        using(var number=new WheelSafeNumericUpDown{Width=120,Value=4}){
            number.CreateControl();using(var image=new Bitmap(number.Width,number.Height)){number.DrawToBitmap(image,number.ClientRectangle);image.Save(Path.Combine(root,"dark-number.png"),System.Drawing.Imaging.ImageFormat.Png);Check(image.GetPixel(number.Width-1,number.Height-1).GetBrightness()<.5,"Numeric input has a white frame");Check(image.GetPixel(number.Width-5,2).GetBrightness()<.5,"Numeric spin buttons use a white background");}
            number.UpButton();Check(number.Value==5,"Dark numeric input lost its increment");number.DownButton();Check(number.Value==4,"Dark numeric input lost its decrement");
        }
        using(var combo=new WheelSafeComboBox{Width=100,Text="0.75×",DropDownStyle=ComboBoxStyle.DropDown}){combo.CreateControl();using(var image=new Bitmap(combo.Width,combo.Height)){combo.DrawToBitmap(image,combo.ClientRectangle);Check(image.GetPixel(combo.Width-3,3).GetBrightness()<.5,"Editable combo retains a white arrow button");image.Save(Path.Combine(root,"dark-editable-combo.png"),System.Drawing.Imaging.ImageFormat.Png);}Check(combo.Text=="0.75×","Dark editable combo changed input text");}
    }
    private static void ExpectBehaviorEditor(Action open,Track source,bool template,int index)
    {
        int visited=0;Exception failure=null;EventHandler inspect=(s,e)=>{
            var dialog=Application.OpenForms.OfType<BulletBehaviorForm>().FirstOrDefault();if(dialog==null)return;
            try{dialog.Opacity=0;visited++;Check(ReferenceEquals(Get(dialog,"track"),source),"Template opened another source's editor");Check((bool)Get(dialog,"templateMode")==template,"Template opened the wrong editor mode");Check(((ListBox)Get(dialog,"list")).SelectedIndex==index,"Template editor selected another rule");}
            catch(Exception error){failure=error;}finally{dialog.DialogResult=DialogResult.Cancel;dialog.Close();}
        };
        Application.Idle+=inspect;try{open();}finally{Application.Idle-=inspect;}if(failure!=null)throw failure;Check(visited==1,"Double-click did not open exactly one full behavior editor");
    }
    private static void TemplateEditor(MainForm form,Track source,Track template)
    {
        var timeline=(TimelineCanvas)Get(form,"timeline");Call(form,"SelectTrack",source,null);Call(form,"SetFrame",500);timeline.Size=new Size(1100,420);timeline.RefreshLayout();
        var lanes=((System.Collections.IEnumerable)Get(timeline,"lanes")).Cast<object>();Func<object,string,object> field=(o,key)=>o.GetType().GetField(key).GetValue(o);var reference=lanes.First(l=>(string)field(l,"property")=="@template"&&field(l,"generationSource")==source);
        int header=(int)timeline.GetType().GetProperty("Header",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(timeline,null),y=header+(int)field(reference,"top")+20;var bounds=(Rectangle)Call(timeline,"TemplateCardBounds",reference,header+(int)field(reference,"top"),1083);
        ExpectBehaviorEditor(()=>Call(timeline,"OnMouseDoubleClick",new MouseEventArgs(MouseButtons.Left,2,bounds.Left+70,y,0)),source,true,0);Check((int)Get(form,"frame")==500&&Get(form,"templateEditing")==null,"Opening a rule unexpectedly entered local preview or moved song time");
        var button=Items(All(form).OfType<ToolStrip>().Single(s=>s.Name=="BehaviorTools")).Single(i=>i.Text=="展开编辑");ExpectBehaviorEditor(()=>button.PerformClick(),source,true,0);
        timeline.RefreshLayout();var bulletReference=((System.Collections.IEnumerable)Get(timeline,"lanes")).Cast<object>().First(l=>(string)field(l,"property")=="@template"&&field(l,"generationSource")==source&&(string)field(l,"generationRule")=="@bullet:1");int bulletY=header+(int)field(bulletReference,"top")+20;var bulletBounds=(Rectangle)Call(timeline,"TemplateCardBounds",bulletReference,header+(int)field(bulletReference,"top"),1083);
        ExpectBehaviorEditor(()=>Call(timeline,"OnMouseDoubleClick",new MouseEventArgs(MouseButtons.Left,2,bulletBounds.Left+70,bulletY,0)),source,false,1);Check((int)Get(form,"frame")==500,"Bullet-rule editor moved song time");
        Call(form,"SelectTrack",template,null);var menu=(ContextMenuStrip)Call(form,"CreateContext",false);using(menu){Call(menu,"OnOpening",new System.ComponentModel.CancelEventArgs());Check(Items(menu).Any(i=>i.Text=="预览模板局部动画"),"Local preview entry was removed");}
    }
    private static void SnapBounds(MainForm form,string root)
    {
        var snap=(ToolStripDropDownButton)Get(form,"snapTools");var host=(ToolStripControlHost)snap.DropDownItems[0];var panel=host.Control;var beats=(FlowLayoutPanel)Get(form,"beatBar");
        using(var surface=new Panel{Size=new Size(650,650),BackColor=Theme.Panel}){
            var owner=host.Owner;owner.Items.Remove(host);panel.Dock=DockStyle.None;panel.Size=host.Size;surface.Controls.Add(panel);surface.CreateControl();surface.PerformLayout();panel.PerformLayout();beats.PerformLayout();
            File.WriteAllLines(Path.Combine(root,"snap-bounds.txt"),beats.Controls.Cast<Control>().Select(c=>c.Text+" "+c.Bounds+" preferred="+c.PreferredSize));
            Check(beats.Controls.Cast<Control>().All(c=>c.Bottom+c.Margin.Bottom<=beats.ClientSize.Height-beats.Padding.Bottom),"Snap popup clips its controls");
            Check(beats.Controls.Cast<Control>().Where(c=>c is Label||c is CheckBox).All(c=>c.ForeColor.GetBrightness()>.45),"Snap popup uses dark text on its dark background");
            var fonts=All(panel).ToDictionary(c=>c,c=>c.Font);foreach(float factor in new[]{1.25f,1.5f})using(var font=new Font(Theme.Font.FontFamily,Theme.Font.Size*factor)){
                foreach(var c in fonts.Keys)c.Font=font;panel.PerformLayout();beats.PerformLayout();Check(beats.Controls.Cast<Control>().All(c=>c.Bottom+c.Margin.Bottom<=beats.ClientSize.Height-beats.Padding.Bottom),"Snap popup clips at font scale "+factor);var division=(ComboBox)Get(form,"beatDivision");Check(division.Width>=TextRenderer.MeasureText(Language.Text("1/128 音符"),division.Font).Width+26,"Note division is truncated at font scale "+factor);
                using(var image=new Bitmap(panel.Width,panel.Height)){panel.DrawToBitmap(image,panel.ClientRectangle);image.Save(Path.Combine(root,"snap-font-"+(factor*100).ToString("0")+(Language.English?"-en":"-zh")+".png"));}foreach(var entry in fonts)entry.Key.Font=entry.Value;panel.PerformLayout();beats.PerformLayout();
            }
            using(var image=new Bitmap(panel.Width,panel.Height)){panel.DrawToBitmap(image,panel.ClientRectangle);image.Save(Path.Combine(root,"snap-settings.png"),System.Drawing.Imaging.ImageFormat.Png);}surface.Controls.Remove(panel);owner.Items.Add(host);
        }
    }
    private static void Render(MainForm form,string path,int width,int height)
    {
        form.ClientSize=new Size(width,height);var content=form.Controls.OfType<TableLayoutPanel>().Single();form.Controls.Remove(content);
        using(var surface=new Panel{Size=form.ClientSize,Font=form.Font,BackColor=form.BackColor,ForeColor=form.ForeColor}){surface.Controls.Add(content);surface.CreateControl();surface.PerformLayout();Call(form,"RestorePanelLayout");surface.PerformLayout();var stage=(StageCanvas)Get(form,"stage");Call(stage,"Fit");using(var matrix=(System.Drawing.Drawing2D.Matrix)Call(stage,"DisplayMatrix")){var room=(LoadData)Get(form,"document");var image=PreviewRenderer.Render((EditorState)Get(form,"state"),room.room,room.inventory.objectSprites,stage.Sprites,(int)Get(form,"frame"),true,false,null,matrix,stage.Width,stage.Height);var old=Get(stage,"image") as Bitmap;Set(stage,"image",image);if(old!=null)old.Dispose();}File.WriteAllLines(path+".visible.txt",All((ParameterInspector)Get(form,"properties")).Select(c=>c.GetType().Name+" "+c.Text+" "+c.Bounds+" visible="+c.Visible));using(var bitmap=new Bitmap(width,height)){surface.DrawToBitmap(bitmap,new Rectangle(0,0,width,height));bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);}surface.Controls.Remove(content);}form.Controls.Add(content);
        var rows=(TableLayoutPanel)Get((ParameterInspector)Get(form,"properties"),"rows");File.WriteAllLines(path+".layout.txt",rows.Controls.Cast<Control>().Select(c=>c.GetType().Name+" "+c.Text+" "+c.Bounds+" visible="+c.Visible+" cell="+rows.GetRow(c)+","+rows.GetColumn(c)));
        Check(((ToolStrip)Get(form,"mainTools")).Height<65&&((ToolStrip)Get(form,"transportTools")).Height<65,"Toolbar wrapped");
    }
    [STAThread]public static int Main(string[] args)
    {
        try{Application.EnableVisualStyles();Sections();SectionPosition();string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);Language.Select(root,"zh-CN");using(var form=new MainForm(root)){
            var track=new Track{id="star",kind="rod",shape="star2",name="周期星星",start=0,end=800,lane=0,count=60,rainbow=true,sprite="sprrainbowcherry"};var template=new Track{id="echo",name="扩散副本",kind="emitter",templateOnly=true,start=0,end=90,lane=1};track.spawnActions.Add(new BulletAction{type="spawn",templateId=template.id,spawnRepeat=50});track.bulletActions.Add(new BulletAction{type="speed"});track.bulletActions.Add(new BulletAction{type="spawn",templateId=template.id,start=25});var state=new EditorState{name="界面验证",fps=50,duration=1200,laneCount=2,tracks=new List<Track>{track,template}};Motion.Normalize(state);
            var doc=new LoadData{state=state,project=root,projectName=state.name,waveform=new WaveData{duration=24,peaks=Enumerable.Range(0,400).Select(i=>Math.Abs(Math.Sin(i*.31))).ToArray()},inventory=new Inventory{objects=new string[0],sprites=new[]{"sprrainbowcherry"},scripts=new string[0]},room=new RoomData{width=800,height=608}};
            string folder=Path.Combine(root,"sprites","sprrainbowcherry");Directory.CreateDirectory(folder);string sprite=Path.GetFullPath(Path.Combine(root,"../../../assets/sprite-packs/Ultimate Avoidance Sprite Pack/Cherry Sprites/Cherry White.png"));File.Copy(sprite,Path.Combine(folder,"0.png"),true);var store=new SpriteStore{Project=root,Metadata=new Dictionary<string,SpriteMeta>{{"sprrainbowcherry",new SpriteMeta{originX=16,originY=16}}}};
            Set(form,"state",state);Set(form,"document",doc);Set(form,"sprites",store);var timeline=(TimelineCanvas)Get(form,"timeline");timeline.State=state;timeline.Wave=doc.waveform;var stage=(StageCanvas)Get(form,"stage");stage.Sprites=store;stage.State=state;stage.Room=doc.room;Call(form,"SelectTrack",track,null);Call(form,"SetFrame",500);Call(form,"UpdateTitle");Language.Attach(form);string original=Json.Write(state);
            StripTheme(form,root);var all=All(form).OfType<ToolStrip>().SelectMany(Items).ToArray();foreach(string name in new[]{"文件","编辑","视图","工程","工具","帮助","添加弹幕","添加整体对象","添加拉镜","添加对象","添加特效","添加 GML 事件","添加弹幕控制","检测更新","切换语言"})Check(all.Any(i=>i.Text==name),"Missing command: "+name);
            var bars=All(form).OfType<ToolStrip>().Where(t=>new[]{"MainTools","TransportTools"}.Contains(t.Name)).ToArray();Check(bars.Length==2,"Command surfaces missing");Check(((SelectionTimeControl)Get(form,"rightTime")).PreferredSize.Height<130,"Right time panel too tall");SnapBounds(form,root);
            var propertyPanel=(ParameterInspector)Get(form,"properties");propertyPanel.ResetSections();Check(propertyPanel.CaptureSections().Contains("!外观"),"Fixture did not open appearance");((ToolStripMenuItem)all.Single(i=>i.Text=="恢复默认布局")).PerformClick();Check(propertyPanel.CaptureSections().Contains("外观")&&propertyPanel.CaptureSections().Contains("!变换"),"View reset did not restore compact section defaults");Check(SceneComparison.Same(Json.Read<EditorState>(original),state),"View reset modified the project");
            Call(form,"FocusBehavior",true,0);Check(((InspectorTabs)Get(form,"inspectorTabs")).SelectedIndex==1,"Rule did not focus behavior page");Call(form,"EnterTemplateContext",template);Check(Get(form,"templateEditing")==template&&stage.TemplatePreviewId==template.id,"Template context absent");var render=(EditorState)Get(stage,"renderState");Check(render.tracks.Single(t=>t.id==template.id).templateOnly==false&&render.tracks.Single(t=>t.id==track.id).templateOnly,"Local preview did not isolate the template");Call(form,"ExitTemplateContext");Check((int)Get(form,"frame")==500&&Get(form,"selectedTrack")==track&&stage.TemplatePreviewId==null,"Template return lost source/time");
            TemplateEditor(form,track,template);Call(form,"SelectTrack",track,null);Call(form,"SetResourcePage",true);Set(form,"resourceKind","camera-presets");Call(form,"FillResources");Check(((ListView)Get(form,"resources")).Items.Count==CameraPresets.Names.Length,"Preset browser empty");
            ((InspectorTabs)Get(form,"inspectorTabs")).SelectedIndex=0;Render(form,Path.Combine(root,"shell-zh.png"),1500,900);Render(form,Path.Combine(root,"shell-narrow.png"),1100,740);
            Language.Select(root,"en");form.RefreshLanguageUI();SnapBounds(form,root);Render(form,Path.Combine(root,"shell-en.png"),1500,900);Check(SceneComparison.Same(Json.Read<EditorState>(original),state),"Layout or language mutated scene");
            Check(Items((MenuStrip)Get(form,"shellMenu")).Any(i=>i.Text=="File"),"Menu translation missing");Call(form,"SelectTrack",track,null);var inspector=(ParameterInspector)Get(form,"properties");var controls=All(inspector).ToArray();Call(form,"AddPropertyKey","alpha");Call(form,"Undo",false);Check(Get(form,"selectedTrack")!=null,"Undo lost selection");Check(controls.Count(c=>!c.IsDisposed)>controls.Length*.8,"Key undo rebuilt inspector");
            Console.WriteLine("PASS: header focus/fold keeps scroll and visual anchor; section preferences, control reuse, dark strips/menus/numeric inputs, direct template rule editors (object/bullet), local preview entry, bilingual layout and unchanged scene/undo. Screenshots: "+root);
        }return 0;}catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
