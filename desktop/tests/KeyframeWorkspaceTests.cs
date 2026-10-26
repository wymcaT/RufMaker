using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class KeyframeWorkspaceTests
{
    static readonly BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static object Get(object o,string name){return o.GetType().GetField(name,flags).GetValue(o);}
    static object Field(object o,string name){return o.GetType().GetField(name).GetValue(o);}
    static void Set(object o,string name,object value){o.GetType().GetField(name,flags).SetValue(o,value);}
    static object Call(object o,string name,params object[] args){return o.GetType().GetMethod(name,flags).Invoke(o,args);}
    static object[] Rows(TimelineCanvas t){return ((IEnumerable)Get(t,"lanes")).Cast<object>().ToArray();}
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    static void Mouse(TimelineCanvas t,string method,int x,int y){Call(t,method,new MouseEventArgs(MouseButtons.Left,1,x,y,0));}
    static IEnumerable<Control> All(Control c){yield return c;foreach(Control child in c.Controls)foreach(var item in All(child))yield return item;}
    static Point KeyPoint(TimelineCanvas t,Keyframe key){var row=Rows(t).Single(r=>(string)Field(r,"property")==key.property);return new Point((int)(float)Call(t,"X",key.frame),t.AudioBottom+(int)Field(row,"top")+(int)Field(row,"height")/2);}
    static Track Object(string id,int lane){var t=new Track{id=id,name=id,kind="object",lane=lane,start=100,end=300,sprite="sprCherry"};Motion.SetKey(t,"x",120,200);Motion.SetKey(t,"x",200,400);Motion.SetKey(t,"y",140,300);return t;}
    static EditorState State(params Track[] tracks){var s=new EditorState{duration=600,fps=50,laneCount=4,tracks=tracks.ToList()};Motion.Normalize(s);return s;}
    static MainForm Editor(string root,EditorState state){var form=new MainForm(root);Set(form,"state",state);Set(form,"document",new LoadData{state=state,project=root,projectName="key workspace fixture",room=new RoomData(),inventory=new Inventory{sprites=new[]{"sprCherry"},objects=new string[0],scripts=new string[0]},waveform=new WaveData{duration=12,peaks=new double[100]}});((TimelineCanvas)Get(form,"timeline")).State=state;return form;}
    static void Image(Control c,string path){c.CreateControl();c.PerformLayout();using(var b=new Bitmap(c.Width,c.Height)){c.DrawToBitmap(b,c.ClientRectangle);b.Save(path);}}
    static void FullImage(MainForm form,string path){var content=form.Controls.OfType<TableLayoutPanel>().Single();form.Controls.Remove(content);using(var host=new Panel{Size=new Size(1500,940),Font=form.Font,BackColor=Theme.Back}){host.Controls.Add(content);host.CreateControl();host.PerformLayout();Call(form,"RestorePanelLayout");host.PerformLayout();Image(host,path);host.Controls.Remove(content);}form.Controls.Add(content);}
    [STAThread] static int Main(string[] args)
    {
        Application.EnableVisualStyles();var root=args[0];try{
            var a=Object("selected",0);var b=Object("other",1);var s=State(a,b);string initial=Json.Write(s);
            using(var form=Editor(root,s)){
                var bottom=(TimelineCanvas)Get(form,"timeline");var right=(TimelineCanvas)Get(form,"keyTimeline");var properties=(ParameterInspector)Get(form,"properties");
                Call(form,"SelectTrack",a,null);bottom.Size=new Size(1200,400);bottom.PixelsPerSecond=100;bottom.RefreshLayout();right.Size=new Size(360,170);right.FitSelection();
                Check(bottom.SelectionKeyframesOnly&&right.InspectorKeyframes,"Product did not wire the two keyframe modes");
                bottom.RestoreView(new TimelineView{expanded=new[]{a.id,b.id},pixelsPerSecond=100,focusGenerationGroups=false});
                Check(Rows(bottom).All(r=>Field(r,"property")==null),"Old expanded preferences exposed bottom property rows");
                Check(Rows(right).Length==2&&Rows(right).All(r=>Field(r,"track")==a),"Inspector must only show current object's keyed properties");
                var bar=bottom.ClipBounds(b);var hiddenPoint=new Point((int)(float)Call(bottom,"X",120),(int)bar.Bottom-3);var lane=Call(bottom,"LaneAt",hiddenPoint);Check(Call(bottom,"KeyAt",lane,hiddenPoint)==null,"Unselected object's keys still hit on bottom");
                Call(form,"SelectTrack",b,null);Check(Rows(right).All(r=>Field(r,"track")==b),"Selection switch retained previous keyframe rows");
                Check(Call(bottom,"KeyAt",Call(bottom,"LaneAt",hiddenPoint),hiddenPoint)==b.keyframes[0],"Selected keys absent from clip summary");
                File.WriteAllText(Path.Combine(root,"before.json"),initial);File.WriteAllText(Path.Combine(root,"after.json"),Json.Write(s));Check(SceneComparison.Same(Json.Read<EditorState>(initial),s),"Selection/layout changed project data");
                Call(form,"SelectTrack",a,null);var key=a.keyframes.First(k=>k.property=="x");var p=KeyPoint(right,key);int undo=((Stack<UndoEntry>)Get(form,"undo")).Count;
                Mouse(right,"OnMouseDown",p.X,p.Y);Check(Get(form,"selectedKey")==key&&bottom.SelectedKeys.Contains(key),"Inspector key selection was not shared");
                var controls=All(properties).ToArray();Mouse(right,"OnMouseMove",p.X+32,p.Y);Mouse(right,"OnMouseUp",p.X+32,p.Y);
                Check(key.frame>120&&key.frame<=300,"Inspector drag failed");Check(((Stack<UndoEntry>)Get(form,"undo")).Count==undo+1,"Drag should create a single undo");Check(All(properties).SequenceEqual(controls),"Dragging a key recreated parameter controls");
                Call(form,"Undo",false);s=(EditorState)Get(form,"state");a=s.tracks.Single(t=>t.id=="selected");b=s.tracks.Single(t=>t.id=="other");key=a.keyframes.First(k=>k.property=="x");Check(key.frame==120&&Get(form,"selectedTrack")==a&&right.SelectedTrack==a,"Undo lost selection or keyframe binding");
                right.Size=new Size(360,170);right.FitSelection();p=KeyPoint(right,key);Mouse(right,"OnMouseDown",p.X,p.Y);Mouse(right,"OnMouseMove",p.X-2000,p.Y);Check(key.frame==a.start,"Right key crossed the clip start");Mouse(right,"OnMouseMove",p.X+2000,p.Y);Check(key.frame==a.end,"Right key crossed the clip end");Mouse(right,"OnMouseUp",p.X+2000,p.Y);Call(form,"Undo",false);
                s=(EditorState)Get(form,"state");a=s.tracks.Single(t=>t.id=="selected");right.Size=new Size(360,170);right.FitSelection();var first=KeyPoint(right,a.keyframes.First(k=>k.property=="x"));var last=KeyPoint(right,a.keyframes.First(k=>k.property=="y"));
                Mouse(right,"OnMouseDown",112,Math.Min(first.Y,last.Y)-12);Mouse(right,"OnMouseMove",Math.Max(first.X,last.X)+12,Math.Max(first.Y,last.Y)+12);Mouse(right,"OnMouseUp",Math.Max(first.X,last.X)+12,Math.Max(first.Y,last.Y)+12);Check(bottom.SelectedKeys.Count==2,"Inspector box selection missed cross-property keys");
                Mouse(right,"OnMouseDown",350,150);Mouse(right,"OnMouseUp",350,150);Check(Get(form,"selectedTrack")==a&&bottom.SelectedKeys.Count==0,"Empty keyframe area cleared object selection");
                Call(form,"SetFrame",180);Check(right.Frame==180&&bottom.Frame==180,"Playheads diverged");
                Call(form,"SelectTrack",a,null);bottom.SelectedProperty="alpha";Call(form,"AddKey");Check(Rows(right).Any(r=>(string)Field(r,"property")=="alpha")&&Rows(bottom).All(r=>Field(r,"property")==null),"Adding key did not update inspector-only rows");
                var bag=TypeDescriptor.GetProperties(properties.SelectedObject);Check(bag["帧"]!=null&&bag["数值"]!=null,"Selected key cannot be edited in inspector");bag["数值"].SetValue(properties.SelectedObject,.5);Check(a.keyframes.Single(k=>k.property=="alpha").value==.5,"Key value editor did not bind");
                Call(form,"RemoveSelection");Check(!a.keyframes.Any(k=>k.property=="alpha"),"Delete did not remove key");Call(form,"Undo",false);Check(((EditorState)Get(form,"state")).tracks.Single(t=>t.id==a.id).keyframes.Any(k=>k.property=="alpha"),"Undo did not restore removed key");
                var current=((EditorState)Get(form,"state")).tracks.Single(t=>t.id==a.id);Call(form,"SelectTrack",current,null);var point=new Point((int)(float)Call(bottom,"X",current.keyframes[0].frame),(int)bottom.ClipBounds(current).Bottom-3);Mouse(bottom,"OnMouseDown",point.X,point.Y);Check((string)Get(bottom,"dragMode")==null&&Get(form,"selectedKey")==current.keyframes[0],"Bottom marker should open inspector, not start key editing");
                bottom.SetTrackSelection(new[]{current,((EditorState)Get(form,"state")).tracks.Single(t=>t.id==b.id)},current);Check(!((Panel)Get(form,"keyWorkspace")).Visible,"Multi-object selection showed misleading inspector keys");Call(form,"SelectProject");Check(Rows(right).Length==0,"Deselection retained keyframe rows");
                Call(form,"SelectTrack",current,null);FullImage(form,Path.Combine(root,"workspace-zh.png"));var panel=(Panel)Get(form,"keyWorkspace");panel.Parent.Controls.Remove(panel);using(var host=new Panel{Size=new Size(360,240),BackColor=Theme.Panel}){panel.Dock=DockStyle.Fill;host.Controls.Add(panel);Image(host,Path.Combine(root,"keys-zh.png"));Language.Select(root,"en");Language.Apply(form);Call(form,"RefreshKeyWorkspace");Image(host,Path.Combine(root,"keys-en.png"));host.Width=250;Image(host,Path.Combine(root,"keys-narrow-en.png"));host.Controls.Remove(panel);}
                bottom.Parent.Controls.Remove(bottom);using(var host=new Panel{Size=new Size(1200,400),BackColor=Theme.Panel}){bottom.Dock=DockStyle.Fill;host.Controls.Add(bottom);Image(host,Path.Combine(root,"bottom-compact.png"));host.Controls.Remove(bottom);}Language.Select(root,"zh-CN");
            }
            var source=Object("source",0);var other=Object("unselected source",1);var template=Object("template",2);template.templateOnly=true;foreach(var t in new[]{source,other})t.spawnActions.Add(new BulletAction{type="spawn",templateId=template.id});var gen=State(source,other,template);
            using(var form=Editor(root,gen)){var bottom=(TimelineCanvas)Get(form,"timeline");Call(form,"SelectTrack",source,null);Check(Rows(bottom).Where(r=>Field(r,"property")!=null).All(r=>((string)Field(r,"property")).StartsWith("@")),"Generation groups exposed key properties");Check(!Rows(bottom).Any(r=>Field(r,"track")==other&&Field(r,"property")!=null),"Other source's generation group remained expanded");Call(form,"SelectTrack",template,null);Check(Rows(bottom).Any(r=>(string)Field(r,"property")=="@template"),"Selected template lost its card");MotionPresets.Apply(template,"圆周运动",100);Call(form,"ShowProperties");Check(!Rows((TimelineCanvas)Get(form,"keyTimeline")).Any(r=>new[]{"x","y"}.Contains((string)Field(r,"property"))),"Baked path keys flooded inspector");}
            Console.WriteLine("PASS: compact selected-only bottom markers; inspector-only property rows, shared selection/time, bounded drag/box selection, single undo, value/add/delete, old layouts, generated paths, template groups and bilingual narrow UI.");return 0;
        }catch(Exception error){File.WriteAllText(Path.Combine(root,"failure.log"),error.ToString());Console.Error.WriteLine(error);return 1;}
    }
}
