using System;
using System.IO;

// Older updaters accept only the original asset paths. Their payload is moved
// into the organized layout at startup; project and user files are not touched.
internal static class AppPaths
{
    private static readonly string[][] AssetMoves={
        new[]{"icon.ico","assets/icons/icon.ico"},new[]{"icon.png","assets/icons/icon.png"},
        new[]{"sprrainbowcherry.gmspr","assets/sprites/sprrainbowcherry.gmspr"},
        new[]{"desktop/assets","assets/terrain"},
        new[]{"Ultimate Avoidance Sprite Pack","assets/sprite-packs/Ultimate Avoidance Sprite Pack"}
    };
    public static void ArrangeAssets(string root)
    {foreach(var item in AssetMoves)Move(Path.Combine(root,item[0]),Path.Combine(root,item[1]));}
    private static void Move(string source,string target)
    {
        if(File.Exists(source)){Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(source,target,true);File.Delete(source);return;}
        if(!Directory.Exists(source))return;Directory.CreateDirectory(target);
        foreach(string file in Directory.GetFiles(source))Move(file,Path.Combine(target,Path.GetFileName(file)));
        foreach(string folder in Directory.GetDirectories(source))Move(folder,Path.Combine(target,Path.GetFileName(folder)));
        Directory.Delete(source,false);
    }
}
