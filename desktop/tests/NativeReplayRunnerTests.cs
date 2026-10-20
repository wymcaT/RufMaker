using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

internal static class NativeReplayRunnerTests
{
    private static void Check(bool valid,string message){if(!valid)throw new Exception(message);}
    private static async Task<NativeRunnerStatus> Wait(NativePreviewRunner runner,Func<NativeRunnerStatus,bool> ready)
    {
        var deadline=DateTime.UtcNow.AddSeconds(8);
        while(DateTime.UtcNow<deadline){var status=runner.ReadStatus();if(status!=null&&ready(status))return status;await Task.Delay(20);}
        throw new Exception("Timed out waiting for the headless runner protocol");
    }
    private static bool Alive(int id){try{using(var process=Process.GetProcessById(id))return !process.HasExited;}catch(ArgumentException){return false;}}
    private static async Task Run(string folder)
    {
        var session=new NativeSession{exe=Path.Combine(folder,"fake-native.exe"),dir=folder,commandFile=Path.Combine(folder,"command.ini"),statusFile=Path.Combine(folder,"status.ini"),frameFile=Path.Combine(folder,"frame.bmp")};
        using(var runner=new NativePreviewRunner(session)){
            await runner.Restart(100,false);await Wait(runner,s=>s.frame==100&&!s.rewind);int first=runner.Process.Id;string original=File.ReadAllText(Path.Combine(folder,"snapshot.txt"));
            runner.WriteControl(160,false,true);await Wait(runner,s=>s.frame==160&&!s.rewind);Check(runner.Process.Id==first,"Forward seek should keep its process");
            runner.WriteControl(40,false,true);await Wait(runner,s=>s.rewind);await runner.Restart(40,false);await Wait(runner,s=>s.frame==40&&!s.rewind);int second=runner.Process.Id;
            Check(second!=first&&!Alive(first),"Backward seek must replace and exit the previous process");Check(File.ReadAllText(Path.Combine(folder,"snapshot.txt")).EndsWith(",0"),"Future state must be cleared by a fresh process");
            runner.WriteControl(100,false,true);await Wait(runner,s=>s.frame==100&&!s.rewind);Check(File.ReadAllText(Path.Combine(folder,"snapshot.txt"))==original,"Earlier random state must match a fresh replay");
            runner.WriteControl(0,true,false);await Wait(runner,s=>s.playing&&s.frame>100&&!s.rewind);
            runner.WriteControl(0,false,false);var paused=await Wait(runner,s=>!s.playing&&!s.rewind);await Task.Delay(50);var still=runner.ReadStatus();Check(still!=null&&still.frame==paused.frame&&runner.Process.Id==second,"Pause must preserve the current frame and process despite an old command frame");
            var pending=runner.Restart(10,false);runner.Stop();await pending;Check(!runner.Alive,"Stop must cancel an in-flight process restart");
        }
        Console.WriteLine("PASS: process replacement, forward replay, random state reset, pause/resume and restart cancellation");
    }
    public static int Main(string[] args){try{Run(args[0]).GetAwaiter().GetResult();return 0;}catch(Exception error){Console.Error.WriteLine(error);return 1;}}
}
