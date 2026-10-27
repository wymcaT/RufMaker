using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class InspectorDockingTests
{
    private sealed class ImageForm : Form{protected override bool ShowWithoutActivation{get{return true;}}public ImageForm(){StartPosition=FormStartPosition.Manual;Location=new Point(-20000,-20000);ShowInTaskbar=false;}}
    [DllImport("user32.dll")]static extern bool PrintWindow(IntPtr window,IntPtr dc,uint flags);
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static object Get(object target,string name){return target.GetType().GetField(name,Flags).GetValue(target);}
    static void Set(object target,string name,object value){target.GetType().GetField(name,Flags).SetValue(target,value);}
    static object Call(object target,string name,params object[] args){return target.GetType().GetMethods(Flags).Single(m=>m.Name==name&&m.GetParameters().Length==args.Length).Invoke(target,args);}
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static IEnumerable<Control> All(Control root){yield return root;foreach(Control child in root.Controls)foreach(var item in All(child))yield return item;}
    static void Mouse(Control c,string method,Point p){Call(c,method,new MouseEventArgs(MouseButtons.Left,1,p.X,p.Y,0));}
    static void Drag(InspectorDockWorkspace dock,string page,Point destination,bool tab=false)
    {var title=All(dock).Single(c=>c.Name==(tab?"DockTab:":"DockTitle:")+page);var target=title.PointToClient(dock.PointToScreen(destination));Mouse(title,"OnMouseDown",new Point(12,12));Check(title.Capture,"Title did not capture docking drag");Mouse(title,"OnMouseMove",target);Mouse(title,"OnMouseUp",target);}
    static void Image(Control c,string path){var parent=c.Parent;var table=parent as TableLayoutPanel;var cell=table==null?new TableLayoutPanelCellPosition():table.GetCellPosition(c);var dock=c.Dock;var bounds=c.Bounds;if(parent!=null)parent.Controls.Remove(c);using(var window=new ImageForm{ClientSize=c.Size}){window.Controls.Add(c);c.Dock=DockStyle.Fill;window.Show();window.PerformLayout();c.PerformLayout();Application.DoEvents();using(var image=new Bitmap(c.Width,c.Height))using(var graphics=Graphics.FromImage(image)){var dc=graphics.GetHdc();try{Check(PrintWindow(window.Handle,dc,1),"Offscreen native window capture failed");}finally{graphics.ReleaseHdc(dc);}image.Save(path);}window.Controls.Remove(c);window.Close();}c.Dock=dock;c.Bounds=bounds;if(table!=null)table.Controls.Add(c,cell.Column,cell.Row);else if(parent!=null)parent.Controls.Add(c);}
    static void Layouts(string root)
    {
        using(var host=new Panel{Size=new Size(1000,600),BackColor=Theme.Panel})using(var dock=new InspectorDockWorkspace()){
            var general=new Panel();var editor=new TextBox{Text="keep editor",Dock=DockStyle.Top};general.Controls.Add(editor);var keys=new Panel();keys.Controls.Add(new Label{Text="keyframes"});dock.AddPage("general","常规设置",general);dock.AddPage("keys","关键帧",keys);host.Controls.Add(dock);host.CreateControl();host.PerformLayout();var controls=All(general).ToArray();
            Drag(dock,"keys",new Point(990,300));Check(dock.Mode=="columns"&&dock.First=="general"&&keys.Parent.Parent==dock.Divider.Panel2,"Dragging to right failed");
            Drag(dock,"keys",new Point(10,300));Check(dock.Mode=="columns"&&dock.First=="keys","Dragging to left failed");
            Drag(dock,"general",new Point(500,10));Check(dock.Mode=="rows"&&dock.First=="general","Dragging above failed");
            Drag(dock,"general",new Point(500,590));Check(dock.Mode=="rows"&&dock.First=="keys","Dragging below failed");
            Drag(dock,"keys",new Point(500,450));Check(dock.Mode=="tabs"&&dock.ActivePage=="keys","Center drop did not combine pages");
            Drag(dock,"general",new Point(10,300),true);Check(dock.Mode=="columns"&&dock.First=="general","Dragging a tab did not split pages");
            var split=dock.Divider;split.OnSplitterMoving(new SplitterCancelEventArgs(0,0,0,0));split.SplitterDistance=320;split.OnSplitterMoved(new SplitterEventArgs(0,0,320,0));double ratio=dock.CaptureView().columns;host.Width=720;host.PerformLayout();host.Width=1000;host.PerformLayout();Check(Math.Abs(split.SplitterDistance-320)<=1&&Math.Abs(dock.CaptureView().columns-ratio)<.0001,"Column divider changed after resize");
            var saved=dock.CaptureView();new WorkspaceView{inspectorDock=saved}.Save(root);dock.DockPage("keys","top");dock.Restore(WorkspaceView.Load(root).inspectorDock);Check(dock.Mode=="columns"&&dock.First=="general"&&Math.Abs(dock.CaptureView().columns-ratio)<.0001,"Dock layout did not restore");
            Drag(dock,"keys",new Point(-40,300));Check(dock.Mode=="columns","Out-of-bounds drag changed layout");host.Size=new Size(0,0);host.PerformLayout();host.Size=new Size(1000,600);host.PerformLayout();Check(All(general).SequenceEqual(controls)&&!editor.IsDisposed&&editor.Text=="keep editor","Docking rebuilt or disposed editor controls");
            dock.Restore(null,true,270);Check(dock.Mode=="rows"&&dock.Divider.SplitterDistance==270,"Old keyframe height was not retained");dock.Restore(null,false);Check(dock.Mode=="tabs","Old collapsed view did not migrate");dock.DockPage("keys","left");Image(host,Path.Combine(root,"dock-components.png"));
            var title=All(dock).Single(c=>c.Name=="DockTitle:keys");Mouse(title,"OnMouseDown",new Point(12,12));Mouse(title,"OnMouseMove",title.PointToClient(dock.PointToScreen(new Point(990,300))));Check((bool)Call(dock,"ProcessCmdKey",Message.Create(IntPtr.Zero,0,IntPtr.Zero,IntPtr.Zero),Keys.Escape)&&!title.Capture&&dock.First=="keys","Escape did not cancel docking drag");
        }
    }
    static void Editor(string root)
    {
        using(var form=new MainForm(root)){
            var track=new Track{id="selected",name="拖动布局示例",kind="object",sprite="sprCherry",start=0,end=100};Motion.SetKey(track,"x",0,100);Motion.SetKey(track,"x",50,400);Motion.SetKey(track,"alpha",20,.5);var state=new EditorState{fps=50,duration=200,tracks=new List<Track>{track}};Motion.Normalize(state);
            Set(form,"state",state);Set(form,"document",new LoadData{state=state,project=root,projectName="Docking",room=new RoomData(),waveform=new WaveData(),inventory=new Inventory{objects=new string[0],sprites=new[]{"sprCherry"},scripts=new string[0]}});((TimelineCanvas)Get(form,"timeline")).State=state;
            Call(form,"SelectTrack",track,track.keyframes.First(k=>k.property=="x"));var general=(ParameterInspector)Get(form,"properties");var details=(ParameterInspector)Get(form,"keyProperties");Check(TypeDescriptor.GetProperties(general.SelectedObject)["数值"]==null&&TypeDescriptor.GetProperties(details.SelectedObject)["数值"]!=null,"Key details did not move to their own page");
            var dock=(InspectorDockWorkspace)Get(form,"inspectorDock");var time=(SelectionTimeControl)Get(form,"rightTime");Control owner=time;while(owner!=null&&owner.Name!="DockPage:keys")owner=owner.Parent;Check(owner!=null,"Object time strip is outside the keyframe page");var parent=(TableLayoutPanel)dock.Parent;var cell=parent.GetCellPosition(dock);parent.Controls.Remove(dock);using(var host=new Panel{Size=new Size(1100,700),BackColor=Theme.Panel}){host.Controls.Add(dock);host.CreateControl();host.PerformLayout();string before=Json.Write(state);int undo=((Stack<UndoEntry>)Get(form,"undo")).Count;var controls=All(general).ToArray();
                dock.DockPage("keys","left");Image(host,Path.Combine(root,"inspector-columns-zh.png"));dock.DockPage("keys","top");Image(host,Path.Combine(root,"inspector-rows-zh.png"));dock.DockPage("keys","tabs");Image(host,Path.Combine(root,"inspector-tabs-zh.png"));File.WriteAllText(Path.Combine(root,"layout-before.json"),before);File.WriteAllText(Path.Combine(root,"layout-after.json"),Json.Write(state));Check(SceneComparison.Same(Json.Read<EditorState>(before),state),"Layout changed project data");Check(((Stack<UndoEntry>)Get(form,"undo")).Count==undo,"Layout changed undo history");Check(All(general).SequenceEqual(controls),"Layout recreated regular parameter controls");
                TypeDescriptor.GetProperties(details.SelectedObject)["数值"].SetValue(details.SelectedObject,250.0);Check(track.keyframes.First(k=>k.property=="x").value==250,"Independent key value editor failed");Call(form,"Undo",false);state=(EditorState)Get(form,"state");track=state.tracks.Single();Check(track.keyframes.First(k=>k.property=="x").value==100&&Get(form,"selectedTrack")==track,"Key undo lost value or selection");
                var retained=All(details).ToArray();Call(form,"SelectTrack",track,track.keyframes.First(k=>k.property=="alpha"));Check(retained.All(c=>!c.IsDisposed)&&All(details).Intersect(retained).Count()>5,"Changing selected key destroyed detail controls");
                Language.Select(root,"en");form.RefreshLanguageUI();Language.Apply(dock);dock.DockPage("keys","left");Image(host,Path.Combine(root,"inspector-columns-en.png"));host.Width=360;Image(host,Path.Combine(root,"inspector-narrow-en.png"));host.Width=1100;Language.Select(root,"zh-CN");form.RefreshLanguageUI();Language.Apply(dock);dock.DockPage("keys","right");Call(form,"SaveWorkspaceView");Check(WorkspaceView.Load(root).inspectorDock.first=="general"&&WorkspaceView.Load(root).inspectorDock.mode=="columns","Editor did not save docking layout");host.Controls.Remove(dock);
            }parent.Controls.Add(dock,cell.Column,cell.Row);
        }
    }
    [STAThread]static int Main(string[] args){try{Application.EnableVisualStyles();Layouts(args[0]);Editor(args[0]);Console.WriteLine("PASS: captured title drags dock both pages on four sides or as tabs; independent ratios, old/new persistence, resize/cancel, separate key details, undo/rebind, bilingual layouts and retained controls.");return 0;}catch(Exception error){Console.Error.WriteLine(error);return 1;}}
}
