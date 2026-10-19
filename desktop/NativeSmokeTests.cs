using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

internal static class NativeSmokeTests
{
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Main(){Run().GetAwaiter().GetResult();}
    private static async Task Run()
    {
        const string root="D:\\rufmaker";
        using(var bridge=new NativeBridge(root))
        {
            var loaded=await bridge.Call<LoadData>("load",new{});
            Check(loaded.state.version==1&&loaded.state.tracks.Count>=2,"Project DTO failed");
            Check(loaded.room.name==loaded.state.room,"Room DTO failed");
            Check(loaded.inventory.sprites.Contains("sprBullet"),"Sprite inventory failed");
            Check(loaded.waveform.peaks.Length>100,"Waveform DTO failed");
            var track=loaded.state.tracks.First(t=>t.kind=="emitter");
            string originalName=track.name;
            var middle=Motion.At(track,(track.keyframes.First().frame+track.keyframes.Last().frame)/2.0);
            Check(Math.Abs(middle.angle-90)<.001,"Native easing interpolation failed");
            using(var sprites=new SpriteStore{Project=loaded.project,Metadata=loaded.spriteMeta})Check(sprites.Get("sprBullet").Width>0,"Native PNG decode failed");
            await bridge.Call<SaveResult>("save",loaded.state);
            var reloaded=await bridge.Call<LoadData>("load",new{});
            Check(reloaded.state.tracks.First(t=>t.id==track.id).name==originalName,"Chinese JSON round trip failed");
            var played=await bridge.Call<BuildResult>("run",new{state=reloaded.state,frame=350});
            Check(played.ready&&played.status["frame"]=="350"&&played.status["players"]=="1","Native IPC preview failed");
            var exported=await bridge.Call<BuildResult>("export",reloaded.state);
            Check(File.Exists(exported.exe)&&File.Exists(Path.Combine(exported.dir,"Data","Music","BGM.ogg")),"Native export failed");
            string report=Json.Write(new{result="passed",tracks=reloaded.state.tracks.Count,room=reloaded.room.name,preview=played.status,release=exported.exe,bytes=exported.bytes,transport="stdin/stdout JSON",interfaceType="Windows Forms / GDI+"});
            File.WriteAllText(Path.Combine(root,"build","native-verification.json"),report);
            Console.WriteLine(report);
        }
    }
}
