using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

#pragma warning disable 0649
internal sealed class TrackPreset
{
    public int version=1,fps=50;public string name;public List<Track> tracks=new List<Track>();
    public static TrackPreset Capture(string name,IEnumerable<Track> selected,int fps)
    {
        var result=new TrackPreset{name=name,fps=fps,tracks=selected.Select(t=>t.Snapshot()).ToList()};Validate(result);
        int origin=result.tracks.Min(t=>Math.Min(t.start,t.keyframes.Select(k=>k.frame).DefaultIfEmpty(t.start).Min()));
        int firstLane=result.tracks.Min(t=>Math.Max(0,t.lane));foreach(var t in result.tracks){t.lane=Math.Max(0,t.lane-firstLane);t.start-=origin;t.end-=origin;TimelineLayout.ShiftClock(t,-origin);if(t.releaseFrame>=0)t.releaseFrame-=origin;foreach(var k in t.keyframes)k.frame-=origin;}return result;
    }
    public List<Track> Instantiate(int frame,int fps)
    {
        Validate(this);double ratio=fps/(double)this.fps;var copies=BulletSystem.CloneTracks(tracks);
        foreach(var t in copies){EventPresets.Scale(t,ratio);t.start=frame+(int)BulletSystem.Round(t.start*ratio);t.end=frame+(int)BulletSystem.Round(t.end*ratio);
            foreach(var k in t.keyframes){k.frame=frame+(int)BulletSystem.Round(k.frame*ratio);if(k.property=="speed"||k.property=="rollSpeed"||k.property=="rainbowSpeed")k.value/=ratio;if(k.property=="life")k.value*=ratio;}
            t.velocityX/=ratio;t.velocityY/=ratio;t.accelerationX/=ratio*ratio;t.accelerationY/=ratio*ratio;t.angularSpeed/=ratio;t.orbitSpeed/=ratio;t.speed/=ratio;t.rollSpeed/=ratio;t.rainbowSpeed/=ratio;t.trailSpeed/=ratio;
            t.fxStutter=Math.Max(1,Math.Min(12,(int)BulletSystem.Round(t.fxStutter*ratio)));t.fxFrequency/=ratio;t.trailScaleDecay/=ratio;t.trailAlphaDecay/=ratio;t.interval=Math.Max(1,(int)BulletSystem.Round(t.interval*ratio));t.life=Math.Max(1,(int)BulletSystem.Round(t.life*ratio));t.deathFrames=Math.Max(0,BulletSystem.Round(t.deathFrames*ratio));t.trailInterval=Math.Max(1,(int)BulletSystem.Round(t.trailInterval*ratio));t.trailLife=Math.Max(1,(int)BulletSystem.Round(t.trailLife*ratio));
            if(t.variableRepeat>0)t.variableRepeat=Math.Max(1,(int)BulletSystem.Round(t.variableRepeat*ratio));t.indexDelay=(int)BulletSystem.Round(t.indexDelay*ratio);t.indexSpeed/=ratio;t.repeatGap=(int)BulletSystem.Round(t.repeatGap*ratio);if(t.releaseFrame>=0)t.releaseFrame=frame+(int)BulletSystem.Round(t.releaseFrame*ratio);foreach(var a in TemplateRuntime.Actions(t)){ScalarExpressions.ScaleAction(a,ratio);a.spawnRepeat=(int)BulletSystem.Round(a.spawnRepeat*ratio);a.start=(int)BulletSystem.Round(a.start*ratio);a.duration=(int)BulletSystem.Round(a.duration*ratio);a.stagger=(int)BulletSystem.Round(a.stagger*ratio);a.childSpeed/=ratio;if(a.type=="speed"||a.type=="turn"){a.from/=ratio;a.to/=ratio;}if(a.type=="acceleration"){a.from/=ratio*ratio;a.to/=ratio*ratio;}}if(t.children.Count>0)t.children=new TrackPreset{fps=this.fps,tracks=t.children}.Instantiate(0,fps);
            t.keyframes=t.keyframes.GroupBy(k=>k.property+":"+k.frame).Select(g=>g.Last()).OrderBy(k=>k.frame).ToList();
            if(t.clockStart.HasValue)t.clockStart=frame+BulletSystem.Round(t.clockStart.Value*ratio);if(t.clockEnd.HasValue)t.clockEnd=frame+BulletSystem.Round(t.clockEnd.Value*ratio);
            TimelineEditing.ScaleCycle(t,ratio);if(MotionPresets.Bound(t)){t.motionPreset.start=frame+BulletSystem.Round(t.motionPreset.start*ratio);t.motionPreset.end=frame+BulletSystem.Round(t.motionPreset.end*ratio);MotionPresets.Bake(t);}
            if(CameraPresets.Bound(t)){CameraPresets.Scale(t,frame,ratio);CameraPresets.Refresh(t);}
        }return copies;
    }
    public static void Validate(TrackPreset p)
    {
        if(p==null||p.version!=1||p.fps<15||p.fps>240||p.tracks==null||p.tracks.Count==0)throw new InvalidDataException("不是受支持的 RufMaker 轨道预设。");
        foreach(var t in p.tracks){
            if(t==null||!new[]{"emitter","rod","object","camera","effect","script","group","control","pattern","variable"}.Contains(t.kind)||t.start<0||t.end<t.start||t.end>1000000||t.keyframes==null||t.keyframes.Count>(MotionPresets.Bound(t)||t.keyframeCycle!=null?2010000:10000))throw new InvalidDataException("预设轨道数据不完整。");
            if(t.kind=="pattern")Validate(new TrackPreset{fps=p.fps,tracks=t.children});
            foreach(var k in t.keyframes){var f=k==null||k.property==null?null:typeof(Track).GetField(k.property);if(f==null||(f.FieldType!=typeof(double)&&f.FieldType!=typeof(int))||k.frame<(t.clockStart.HasValue?-1000000:0)||k.frame>1000000||Double.IsNaN(k.value)||Double.IsInfinity(k.value))throw new InvalidDataException("预设含有无效关键帧。");}
        }
    }
}
#pragma warning restore 0649

internal sealed class PresetLibrary
{
    public readonly string Root;
    public PresetLibrary(string root){Root=Path.GetFullPath(root);Directory.CreateDirectory(Root);}
    public string Resolve(string relative)
    {
        if(Path.IsPathRooted(relative??""))throw new InvalidDataException("预设分类必须在公共预设库中。");
        string file=Path.GetFullPath(Path.Combine(Root,relative??""));if(file!=Root&&!file.StartsWith(Root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("预设路径不在公共预设库中。");return file;
    }
    public string Relative(string file){file=Path.GetFullPath(file);if(file==Root)return "";if(!file.StartsWith(Root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("无效预设路径。");return file.Substring(Root.Length+1);}
    public static string ValidName(string name)
    {name=(name??"").Trim();if(name.Length==0||name=="."||name==".."||name.IndexOfAny(Path.GetInvalidFileNameChars())>=0||name.EndsWith(".")||Regex.IsMatch(name,@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)",RegexOptions.IgnoreCase))throw new ArgumentException("请输入有效名称，不能包含 / \\ : * ? 等文件名符号。");return name;}
    public string[] Folders(){return new[]{""}.Concat(Directory.GetDirectories(Root,"*",SearchOption.AllDirectories).Select(Relative).OrderBy(s=>s)).ToArray();}
    public string CreateFolder(string category,string name){string relative=Path.Combine(category,ValidName(name)),dir=Resolve(relative);if(Directory.Exists(dir))throw new IOException("该分类文件夹已存在。");Directory.CreateDirectory(dir);return relative;}
    public string Move(string relative,string category,string name)
    {
        string source=Resolve(relative);bool folder=Directory.Exists(source);string target=Resolve(Path.Combine(category,ValidName(name)+(folder?"":".rufpreset")));
        if(String.Equals(source,target,StringComparison.OrdinalIgnoreCase)){if(source!=target){if(folder)Directory.Move(source,target);else File.Move(source,target);}return Relative(target);}
        if(folder&&target.StartsWith(source+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("不能把文件夹放到它自己里面。");
        if(File.Exists(target)||Directory.Exists(target))throw new IOException("此分类中已有同名项目，请换一个名称。");Directory.CreateDirectory(Path.GetDirectoryName(target));if(folder)Directory.Move(source,target);else File.Move(source,target);return Relative(target);
    }
    public string Save(string category,string name,TrackPreset data,string project)
    {
        string relative=Path.Combine(category,ValidName(name)+".rufpreset"),target=Resolve(relative);if(File.Exists(target))throw new IOException("已有同名预设，请换一个名称。");data.name=name;WritePackage(target,data,project);return relative;
    }
    private string Unique(string relative)
    {string file=Resolve(relative),directory=Path.GetDirectoryName(relative)??"",stem=Path.GetFileNameWithoutExtension(file);int n=2;while(File.Exists(file)||Directory.Exists(file))file=Resolve(Path.Combine(directory,stem+" ("+(n++)+").rufpreset"));return file;}
    public int Import(string source,string category)
    {
        if(String.Equals(Path.GetExtension(source),".rufpreset",StringComparison.OrdinalIgnoreCase)){Read(source);string target=Unique(Path.Combine(category,ValidName(Path.GetFileNameWithoutExtension(source))+".rufpreset"));Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(source,target);return 1;}
        int count=0,folders=0;using(var archive=ZipFile.OpenRead(source)){
            foreach(var entry in archive.Entries){if(entry.FullName.EndsWith("/")){Directory.CreateDirectory(Resolve(Path.Combine(category,entry.FullName.TrimEnd('/').Replace('/',Path.DirectorySeparatorChar))));folders++;continue;}if(!entry.FullName.EndsWith(".rufpreset",StringComparison.OrdinalIgnoreCase))continue;
                string relative=entry.FullName.Replace('/',Path.DirectorySeparatorChar);Resolve(Path.Combine(category,relative));
                using(var memory=new MemoryStream()){using(var input=entry.Open())input.CopyTo(memory);memory.Position=0;using(var preset=new ZipArchive(memory,ZipArchiveMode.Read,true))Read(preset);string target=Unique(Path.Combine(category,relative));Directory.CreateDirectory(Path.GetDirectoryName(target));File.WriteAllBytes(target,memory.ToArray());count++;}
            }
        }if(count==0&&folders==0)throw new InvalidDataException("预设包中没有 .rufpreset 文件或分类。");return count;
    }
    public void Export(string relative,string output)
    {
        string source=Resolve(relative);if(File.Exists(source)){File.Copy(source,output,true);return;}
        if(Path.GetFullPath(output).StartsWith(source+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("请把导出包保存到该分类文件夹以外。");
        string prefix=String.IsNullOrEmpty(relative)?"":Path.GetFileName(source)+"/";
        using(var archive=new ZipArchive(File.Create(output),ZipArchiveMode.Create)){
            if(prefix!="")archive.CreateEntry(prefix);foreach(var folder in Directory.GetDirectories(source,"*",SearchOption.AllDirectories))archive.CreateEntry(prefix+folder.Substring(source.Length+1).Replace('\\','/')+"/");
            foreach(var file in Directory.GetFiles(source,"*.rufpreset",SearchOption.AllDirectories))archive.CreateEntryFromFile(file,prefix+file.Substring(source.Length+1).Replace('\\','/'),CompressionLevel.Optimal);
        }
    }
    public static TrackPreset Read(string file){using(var archive=ZipFile.OpenRead(file))return Read(archive);}
    private static TrackPreset Read(ZipArchive archive)
    {var entry=archive.GetEntry("preset.json");if(entry==null)throw new InvalidDataException("预设缺少 preset.json。");using(var reader=new StreamReader(entry.Open())){var result=Json.Read<TrackPreset>(reader.ReadToEnd());TrackPreset.Validate(result);return result;}}
    private static IEnumerable<string> SpriteNames(TrackPreset data)
    {return Flatten(data.tracks).Where(t=>Theme.Visual(t.kind)).SelectMany(t=>new[]{t.sprite,t.rainbow?t.rainbowSourceSprite:null}).Where(s=>!String.IsNullOrEmpty(s)).Distinct();}
    private static IEnumerable<Track> Flatten(IEnumerable<Track> tracks){foreach(var t in tracks){yield return t;foreach(var c in Flatten(t.children))yield return c;}}
    private static bool AssetName(string name){return Regex.IsMatch(name??"",@"^[A-Za-z_][A-Za-z0-9_]*$");}
    private static bool AssetFile(string name){return name=="sprite.txt"||Regex.IsMatch(name,@"^\d+\.png$");}
    private static void WritePackage(string target,TrackPreset data,string project)
    {
        TrackPreset.Validate(data);Directory.CreateDirectory(Path.GetDirectoryName(target));string temporary=target+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{
            using(var archive=new ZipArchive(File.Create(temporary),ZipArchiveMode.Create)){
                using(var writer=new StreamWriter(archive.CreateEntry("preset.json").Open(),new UTF8Encoding(false)))writer.Write(Json.Write(data));
                foreach(string sprite in SpriteNames(data)){
                    if(!AssetName(sprite))throw new InvalidDataException("无效贴图资源名："+sprite);string folder=Path.Combine(project,"sprites",sprite);
                    if(!File.Exists(Path.Combine(folder,"sprite.txt"))||!File.Exists(Path.Combine(folder,"0.png")))throw new FileNotFoundException("预设使用的贴图不存在："+sprite);
                    foreach(string file in Directory.GetFiles(folder).Where(f=>AssetFile(Path.GetFileName(f))))archive.CreateEntryFromFile(file,"sprites/"+sprite+"/"+Path.GetFileName(file),CompressionLevel.Optimal);
                }
            }File.Move(temporary,target);
        }finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    private static byte[] Bytes(ZipArchiveEntry entry){using(var memory=new MemoryStream()){using(var input=entry.Open())input.CopyTo(memory);return memory.ToArray();}}
    private static bool Matches(string folder,Dictionary<string,byte[]> files)
    {return Directory.Exists(folder)&&Directory.GetFiles(folder).Count(f=>AssetFile(Path.GetFileName(f)))==files.Count&&files.All(p=>File.Exists(Path.Combine(folder,p.Key))&&File.ReadAllBytes(Path.Combine(folder,p.Key)).SequenceEqual(p.Value));}
    private static void Index(string file,string line){string text=File.Exists(file)?File.ReadAllText(file):"";if(!text.Split('\n').Any(l=>l.TrimEnd('\r')==line))File.AppendAllText(file,(text.EndsWith("\n")||text.Length==0?"":"\n")+line+"\n",new UTF8Encoding(false));}
    public List<Track> Instantiate(string relative,string project,int frame,int fps)
    {
        using(var archive=ZipFile.OpenRead(Resolve(relative))){var data=Read(archive);var map=new Dictionary<string,string>();
            foreach(string sprite in SpriteNames(data)){
                if(!AssetName(sprite))throw new InvalidDataException("预设含有无效贴图名。");string prefix="sprites/"+sprite+"/";var entries=archive.Entries.Where(e=>e.FullName.StartsWith(prefix,StringComparison.Ordinal)).ToArray();
                if(entries.Any(e=>!AssetFile(e.FullName.Substring(prefix.Length))))throw new InvalidDataException("预设贴图文件无效。");var files=entries.ToDictionary(e=>e.FullName.Substring(prefix.Length),Bytes);
                if(!files.ContainsKey("sprite.txt")||!files.ContainsKey("0.png"))throw new InvalidDataException("预设缺少贴图："+sprite);
                string name=sprite,folder=Path.Combine(project,"sprites",name);
                if(Directory.Exists(folder)&&!Matches(folder,files)){
                    string hash;using(var sha=SHA256.Create()){using(var memory=new MemoryStream()){foreach(var item in files.OrderBy(p=>p.Key)){byte[] key=Encoding.UTF8.GetBytes(item.Key);memory.Write(key,0,key.Length);memory.Write(item.Value,0,item.Value.Length);}hash=BitConverter.ToString(sha.ComputeHash(memory.ToArray())).Replace("-","").Substring(0,8).ToLowerInvariant();}}
                    name="rfp_"+sprite.Substring(0,Math.Min(35,sprite.Length))+"_"+hash;folder=Path.Combine(project,"sprites",name);int n=2;string stem=name;while(Directory.Exists(folder)&&!Matches(folder,files)){name=stem+"_"+(n++);folder=Path.Combine(project,"sprites",name);}
                }
                if(!Directory.Exists(folder)){Directory.CreateDirectory(folder);foreach(var file in files)File.WriteAllBytes(Path.Combine(folder,file.Key),file.Value);}
                Index(Path.Combine(project,"sprites","index.yyd"),name);Index(Path.Combine(project,"sprites","tree.yyd"),"\t|"+name);map[sprite]=name;
            }
            foreach(var t in Flatten(data.tracks)){string mapped;if(t.sprite!=null&&map.TryGetValue(t.sprite,out mapped))t.sprite=mapped;if(t.rainbow){t.rainbowSprite=t.sprite;t.color="#FFFFFF";}if(t.rainbowSourceSprite!=null&&map.TryGetValue(t.rainbowSourceSprite,out mapped))t.rainbowSourceSprite=mapped;}
            return data.Instantiate(frame,fps);
        }
    }
}
