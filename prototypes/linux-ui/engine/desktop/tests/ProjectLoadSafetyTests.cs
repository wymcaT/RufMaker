using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class ProjectLoadSafetyTests
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    private static object Get(object value,string name){return value.GetType().GetField(name,Flags).GetValue(value);}
    private static void Set(object value,string name,object item){value.GetType().GetField(name,Flags).SetValue(value,item);}
    private static object Call(object value,string name,params object[] args){return value.GetType().GetMethod(name,Flags).Invoke(value,args);}
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Wait(Task task)
    {
        var until=DateTime.UtcNow.AddSeconds(15);
        while(!task.IsCompleted){if(DateTime.UtcNow>until)throw new TimeoutException("Project preparation did not finish");Application.DoEvents();Thread.Sleep(1);}
        task.GetAwaiter().GetResult();
    }
    private static LoadData Document(string folder,string name,bool native=false)
    {
        Directory.CreateDirectory(folder);
        return new LoadData{project=folder,projectName=name,descriptor=Path.Combine(folder,name+".rufmaker"),
            state=new EditorState{name=name,music="missing.ogg",duration=50,timelineInitialized=true,nativeProject=native?new NativeProject{imported=true,duration=50}:null},
            room=new RoomData(),spriteMeta=new Dictionary<string,SpriteMeta>(),
            inventory=new Inventory{objects=new string[0],sprites=new string[0],scripts=new string[0],rooms=new string[0],paths=new string[0],timelines=new string[0]}};
    }
    private static void ProjectPreparation(string folder)
    {
        using(var form=new MainForm(folder)){
            var original=Document(Path.Combine(folder,"A"),"A");var track=new Track{id="old",name="old",kind="object",start=0,end=40};original.state.tracks.Add(track);Motion.Normalize(original.state);
            Set(form,"state",original.state);Set(form,"document",original);Set(form,"selectedTrack",track);
            var timeline=(TimelineCanvas)Get(form,"timeline");timeline.State=original.state;
            var stage=(StageCanvas)Get(form,"stage");stage.State=original.state;stage.Room=original.room;
            var undo=(UndoStack)Get(form,"undo");undo.Push(new UndoEntry(new UndoSnapshot{state=original.state,room=original.room,selectedTrack=track.id}));
            var oldAudio=Get(form,"audio");var missing=Document(Path.Combine(folder,"B"),"B");
            var failed=(Task)Call(form,"PrepareProjectLoad",missing,null);
            try{Wait(failed);throw new Exception("Missing ordinary-project music must fail preparation");}catch(FileNotFoundException){}
            Check(ReferenceEquals(Get(form,"document"),original)&&ReferenceEquals(Get(form,"state"),original.state),"Failed preparation changed the active project");
            Check(ReferenceEquals(Get(form,"audio"),oldAudio)&&ReferenceEquals(Get(form,"selectedTrack"),track),"Failed preparation replaced current audio or selection");
            Check(undo.Count==1&&undo.Peek().Restore().state.name=="A"&&ReferenceEquals(stage.State,original.state),"Failed preparation changed old history or canvas");

            // Imported projects intentionally support missing audio. Use that path
            // to exercise a successful transfer without loading an FMOD DLL.
            var incoming=Document(Path.Combine(folder,"C"),"C",true);var success=(Task)Call(form,"PrepareProjectLoad",incoming,null);Wait(success);
            var prepared=success.GetType().GetProperty("Result").GetValue(success,null);
            try{
                Check(ReferenceEquals(Get(form,"document"),original)&&undo.Count==1,"Preparing a valid replacement committed it too soon");
                // The preparation already finished; omit native recording in this
                // UI-only test so it never launches GM or accesses user projects.
                incoming.state.nativeProject=null;
                Wait((Task)Call(form,"LoadProject",incoming,prepared,null));
                Check(ReferenceEquals(Get(form,"document"),incoming)&&ReferenceEquals(Get(form,"state"),incoming.state),"Successful load did not commit the incoming project");
                Check(undo.Count==0&&((UndoStack)Get(form,"redo")).Count==0,"Old undo/redo survived the project commit");
                Check(ReferenceEquals(stage.State,incoming.state)&&ReferenceEquals(timeline.State,incoming.state),"Canvas/timeline still use the old document");
                Check(!ReferenceEquals(Get(form,"audio"),oldAudio)&&prepared.GetType().GetField("Audio").GetValue(prepared)==null,"Prepared audio ownership was not transferred");
            }finally{((IDisposable)prepared).Dispose();}
            ((System.Windows.Forms.Timer)Get(form,"saveTimer")).Stop();
        }
        Console.WriteLine("PASS: failed audio preparation preserves project/selection/history; successful load clears old history and transfers audio.");
    }
    private static void SaveRetry(string folder)
    {
        using(var form=new MainForm(folder)){
            var timer=(System.Windows.Forms.Timer)Get(form,"saveTimer");Set(form,"dirty",true);timer.Start();
            Call(form,"FinishSaveAttempt",false);Check(!timer.Enabled&&(bool)Get(form,"dirty"),"Failed save must stay dirty without retrying automatically");
            Call(form,"FinishSaveAttempt",true);Check(timer.Enabled,"Edits made during a successful save need a follow-up save");
            Set(form,"closeInProgress",true);Call(form,"FinishSaveAttempt",true);Check(!timer.Enabled,"Closing must not restart autosave");
            Set(form,"closeInProgress",false);Set(form,"parameterScrubbing",true);Call(form,"FinishSaveAttempt",true);Check(!timer.Enabled,"Scrubbing must not restart autosave");
            Set(form,"parameterScrubbing",false);Set(form,"dirty",false);Call(form,"FinishSaveAttempt",true);Check(!timer.Enabled,"A clean save must not queue another save");
        }
        Console.WriteLine("PASS: failure stops the autosave loop; successful in-flight edits still reschedule safely.");
    }
    [STAThread]
    public static int Main(string[] args)
    {
        try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);ProjectPreparation(args[0]);SaveRetry(args[0]);return 0;}
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
