using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class ResourceBrowsingTests
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    private static object Get(object target,string name){return target.GetType().GetField(name,Flags).GetValue(target);}
    private static void Set(object target,string name,object value){target.GetType().GetField(name,Flags).SetValue(target,value);}
    private static void Call(object target,string name){target.GetType().GetMethod(name,Flags).Invoke(target,null);}
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void CheckTutorial(MainForm owner,string root)
    {
        File.WriteAllText(Path.Combine(root,"README.md"),"Alpha beta alpha");Exception failure=null;bool visited=false;
        using(var timer=new Timer{Interval=50})
        {
            timer.Tick+=(s,e)=>{
                var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f!=owner);if(dialog==null)return;
                timer.Stop();visited=true;
                try{
                    var content=dialog.Controls.OfType<TextBox>().Single();
                    var bar=dialog.Controls.OfType<ToolStrip>().Single();var input=(TextBox)bar.Items.OfType<ToolStripControlHost>().Single().Control;
                    input.Text="alpha";Check(content.ReadOnly&&content.SelectionStart==0&&content.SelectionLength==5,"Tutorial finds first match without editing content");
                    typeof(Control).GetMethod("OnKeyDown",Flags).Invoke(input,new object[]{new KeyEventArgs(Keys.Enter)});
                    Check(content.SelectionStart==11,"Enter finds next case-insensitive match");
                    typeof(Control).GetMethod("OnKeyDown",Flags).Invoke(input,new object[]{new KeyEventArgs(Keys.Enter)});
                    Check(content.SelectionStart==0,"Find next wraps to first match");
                    input.Text="missing";Check(content.Text=="Alpha beta alpha","Missing query preserves tutorial content");
                }catch(Exception error){failure=error;}finally{dialog.Close();}
            };
            timer.Start();Call(owner,"OpenLocalReadme");timer.Stop();
        }
        if(failure!=null)throw failure;Check(visited,"Tutorial search dialog opened");
    }
    [STAThread] private static void Main(string[] args)
    {
        string root=Path.Combine(Path.GetFullPath(args[0]),"fixture");Directory.CreateDirectory(root);
        // Do not Show MainForm: Shown starts the real bridge and project loader.
        using(var form=new MainForm(root))
        {
            var a=new Track{id="a",name="A",kind="object",sprite="old-a",end=40};
            var b=new Track{id="b",name="B",kind="object",sprite="old-b",end=40};
            var camera=new Track{id="c",name="C",kind="camera",sprite="camera-old",end=40};
            var state=new EditorState{name="Fixture",duration=50,timelineInitialized=true};state.tracks.AddRange(new[]{a,b,camera});Motion.Normalize(state);
            var room=new RoomData();Set(form,"state",state);Set(form,"document",new LoadData{state=state,room=room,project=root,projectName="Fixture",spriteMeta=new Dictionary<string,SpriteMeta>(),inventory=new Inventory{objects=new string[0],sprites=new[]{"new-sprite"},scripts=new string[0],rooms=new string[0],paths=new string[0],timelines=new string[0]}});
            var stage=(StageCanvas)Get(form,"stage");stage.State=state;stage.Room=room;
            var timeline=(TimelineCanvas)Get(form,"timeline");timeline.State=state;timeline.SelectedTracks.Clear();timeline.SelectedTracks.UnionWith(new[]{a,b,camera});Set(form,"selectedTrack",camera);
            var resources=(ListView)Get(form,"resources");Set(form,"resourceKind","sprites");var handle=resources.Handle;
            resources.Items.Clear();var item=new ListViewItem("new-sprite"){Tag="new-sprite"};resources.Items.Add(item);item.Selected=true;
            typeof(ListView).GetMethod("OnSelectedIndexChanged",Flags).Invoke(resources,new object[]{EventArgs.Empty});
            Check(a.sprite=="old-a"&&b.sprite=="old-b"&&camera.sprite=="camera-old","Browsing must not apply a sprite");
            var undo=(UndoStack)Get(form,"undo");Check(undo.Count==0,"Browsing must not create undo history");
            Set(form,"resourceName",null);Call(form,"RefreshResourceActions");var apply=(ToolStripButton)Get(form,"applyResourceSprite");
            Check(apply.Enabled&&apply.Text.Contains("2"),"Apply remains available for two visual targets after track focus changes");
            Call(form,"ApplyBrowsedSprite");
            Check(a.sprite=="new-sprite"&&b.sprite=="new-sprite"&&camera.sprite=="camera-old","Apply changes only selected visual tracks, even with a camera primary");
            Check(undo.Count==1,"Batch apply must create exactly one undo entry");
            Call(form,"ApplyBrowsedSprite");Check(undo.Count==1,"Applying the same sprite must not create another undo entry");
            ((Timer)Get(form,"saveTimer")).Stop();
            CheckTutorial(form,root);Console.WriteLine("Resource browsing and tutorial checks passed");
        }
    }
}
