using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

#pragma warning disable 0649 // JSON fields are populated by JavaScriptSerializer.
internal sealed class EditorState
{
    public int version=2, fps=50, duration=1500;
    public double bpm=120,beatOffsetMs;public int beatDivision=4;public bool beatSnap,directStart;
    public string name="",musicTitle="";public PlayerSettings player;public object terrain;
    public string room="rAvoidance", music="Data/Music/BGM.ogg";
    public bool timelineInitialized;
    public string[] plainRooms=new string[0];
    public NativeProject nativeProject;
    public List<Track> tracks=new List<Track>();
}
internal sealed class PlayerSettings {public double gravity=.4,moveSpeed=3,jumpSpeed=8.5,doubleJumpSpeed=7;public int maxJumps=2;public bool infiniteJump;}
internal sealed class Track
{
    public string id, name, kind, sprite="sprBullet", easing="linear", color="#ff6d91", effect="flash", code="", rodMode="center", motion="keyframes";
    public bool enabled=true, trail, rolling, rotateSprite, trailRandom;
    public string shape="rod";
    public double radius=120,orientation,sides=5,cameraZoom=1;
    public string trailMode="legacy";
    public int trailInterval=3,trailLife=18;
    public double spriteAngle,trailScaleDecay=.035,trailAlphaDecay=.035,trailStartAlpha=.55,trailSpeed,trailDirection;
    public bool rainbow,rainbowReverse,rainbowDynamic;
    public string rainbowSourceSprite="",rainbowSourceColor="";
    public double rainbowDensity=1,rainbowOffset,rainbowSpeed=.25;
    public int start, end, interval=25, count=12, life=160;
    public double speed=3, spread=30, angle, strength=6;
    public double x=400,y=304,scale=1,alpha=1,x1=280,y1=304,x2=520,y2=304,length=240,rollSpeed=2,easePower=3,easePeriod=.3;
    public double velocityX,velocityY,accelerationX,accelerationY,angularSpeed,orbitRadius=100,orbitSpeed=1,orbitPhase;
    public double pathStartX,pathStartY,pathEndX,pathEndY,pathPeakX,pathPeakY,pathProgress;
    public double fxOpacity=1,fxAngle,fxRetention=.5,fxFrequency=1;
    public int fxSamples=6;
    public List<Keyframe> keyframes=new List<Keyframe>();
    public Track Snapshot(){var copy=(Track)MemberwiseClone();copy.keyframes=keyframes.Select(k=>k.Snapshot()).ToList();return copy;}
}
internal sealed class Keyframe
{
    public int frame;
    public double x=400,y=304,angle,scale=1,alpha=1;
    public string property,easing="";
    public double value;
    public Keyframe Snapshot(){return (Keyframe)MemberwiseClone();}
}
internal sealed class RoomData
{
    public string name;
    public int width=800,height=608,fps=50,bgColor=0x202020;
    public List<RoomInstance> instances=new List<RoomInstance>();
    public RoomData Snapshot(){return new RoomData{name=name,width=width,height=height,fps=fps,bgColor=bgColor,instances=instances.Select(i=>i.Snapshot()).ToList()};}
}
internal sealed class RoomInstance {public int index;public string id,name,raw;public double x,y;public RoomInstance Snapshot(){return (RoomInstance)MemberwiseClone();}}
internal sealed class UndoSnapshot {public EditorState state;public RoomData room;}
internal sealed class KeyClipboardItem {public string trackId;public Keyframe key;public int offset;}
internal sealed class Inventory
{
    public string[] scripts,objects,sprites,rooms,pack;
    public Dictionary<string,string> objectSprites=new Dictionary<string,string>();
}
internal sealed class SpriteMeta {public int originX,originY,frames=1;}
internal sealed class WaveData {public double duration,bpm,offsetMs,confidence;public double[] peaks;public string source;}
internal sealed class ProjectPathResult {public string path,music,title;}
internal sealed class LoadData
{
    public EditorState state;
    public Inventory inventory;
    public RoomData room;
    public string project,projectFile,descriptor,projectName,pack;
    public Dictionary<string,SpriteMeta> spriteMeta;
    public WaveData waveform;
}
internal sealed class SourceData {public string content,hash;}
internal sealed class SaveResult {public RoomData room;}
internal sealed class BuildResult {public string exe,dir;public long bytes;public bool ready,cached;public int pid;public Dictionary<string,string> status;}
internal sealed class NativeEvent {public string id,file,expression,kind,label,snippet,reason;public int frame,line;public double period;}
internal sealed class NativeProject
{
    public int version=1,fps=50,width=800,height=608,duration;public bool imported,hasFmod;public double timelineSpeed=1,originalStart,bpm;
    public string entry,source,name,room,controller,clock,timeline,music,bootObject,musicInitializer,confidence,defaultSprite,killerParent;
    public NativeEvent[] events=new NativeEvent[0],unresolved=new NativeEvent[0];public string[] warnings=new string[0];
}
internal sealed class NativeRoomCandidate {public string name;public int width,height,fps,score;public NativeControllerCandidate[] controllers;public override string ToString(){return name+(score>0?" · 候选":"");}}
internal sealed class NativeControllerCandidate {public string name;public int score;public bool direct;}
internal sealed class NativeScan:NativeScanBase {}
internal class NativeScanBase {public string entry,name,room,controller,clock,timeline,music,confidence;public int fps,duration;public string[] entries,controllers,musicChoices,warnings;public NativeRoomCandidate[] rooms;public NativeEvent[] events,unresolved;}
internal sealed class NativeImportOptions {public string entry,name,parent,room,controller,music;public double duration;}
internal sealed class NativeImportResult {public string path;public string[] warnings;public int events;}
internal sealed class NativeSession {public string exe,dir,commandFile,statusFile,frameFile;public bool cached;public int width,height;}
internal sealed class ImportResult {public string name;public Inventory inventory;public Dictionary<string,SpriteMeta> spriteMeta;}
internal sealed class RpcResponse {public int id;public bool ok;public object result;public string error;}
#pragma warning restore 0649

internal static class Json
{
    public static JavaScriptSerializer Serializer(){return new JavaScriptSerializer{MaxJsonLength=20000000,RecursionLimit=150};}
    public static string Write(object value){return Serializer().Serialize(value);}
    public static T Read<T>(string value){return Serializer().Deserialize<T>(value);}
}

internal sealed class NativeBridge : IDisposable
{
    private readonly Process process;
    private readonly object sync=new object();
    private readonly Dictionary<int,TaskCompletionSource<string>> pending=new Dictionary<int,TaskCompletionSource<string>>();
    private int nextId;
    private bool disposed;
    private readonly string logPath;
    private readonly StreamWriter input;
    public NativeBridge(string root,string projectPath="")
    {
        Directory.CreateDirectory(Path.Combine(root,"build","logs"));
        logPath=Path.Combine(root,"build","logs","native-bridge.log");
        var info=new ProcessStartInfo(Path.Combine(root,"runtime","node.exe"),"\""+Path.Combine(root,"studio","bridge.mjs")+"\"");
        info.WorkingDirectory=root;info.UseShellExecute=false;info.CreateNoWindow=true;
        info.RedirectStandardInput=true;info.RedirectStandardOutput=true;info.RedirectStandardError=true;
        info.StandardOutputEncoding=System.Text.Encoding.UTF8;info.StandardErrorEncoding=System.Text.Encoding.UTF8;
        info.EnvironmentVariables["RUFMAKER_ROOT"]=root;
        if(!String.IsNullOrEmpty(projectPath))info.EnvironmentVariables["RUFMAKER_PROJECT"]=projectPath;else info.EnvironmentVariables.Remove("RUFMAKER_PROJECT");
        process=new Process{StartInfo=info,EnableRaisingEvents=true};
        process.OutputDataReceived+=Receive;
        process.ErrorDataReceived+=(s,e)=>{if(e.Data!=null)lock(sync)File.AppendAllText(logPath,e.Data+Environment.NewLine);};
        process.Exited+=(s,e)=>FailPending(new IOException("工程进程已退出，请重开制作器。记录："+logPath));
        process.Start();input=new StreamWriter(process.StandardInput.BaseStream,new System.Text.UTF8Encoding(false)){AutoFlush=true};process.BeginOutputReadLine();process.BeginErrorReadLine();
    }
    public async Task<T> Call<T>(string command,object payload)
    {
        var completion=new TaskCompletionSource<string>();
        lock(sync)
        {
            if(disposed||process.HasExited)throw new IOException("工程进程未运行");
            int id=++nextId;pending.Add(id,completion);
            input.WriteLine(Json.Write(new{id=id,command=command,payload=payload}));
        }
        string result=await completion.Task.ConfigureAwait(false);
        return Json.Read<T>(result);
    }
    private void Receive(object sender,DataReceivedEventArgs e)
    {
        if(e.Data==null)return;
        try
        {
            var message=Json.Read<RpcResponse>(e.Data);
            TaskCompletionSource<string> completion;
            lock(sync){if(!pending.TryGetValue(message.id,out completion))return;pending.Remove(message.id);}
            if(message.ok)completion.TrySetResult(Json.Write(message.result));
            else completion.TrySetException(new InvalidOperationException(message.error));
        }
        catch(Exception error){FailPending(error);}
    }
    private void FailPending(Exception error)
    {
        lock(sync){foreach(var completion in pending.Values)completion.TrySetException(error);pending.Clear();}
    }
    public void Dispose()
    {
        lock(sync)
        {
            if(disposed)return;disposed=true;
            if(!process.HasExited){input.WriteLine("{\"id\":0,\"command\":\"shutdown\"}");input.Close();}
        }
        if(!process.WaitForExit(1500))process.Kill();
        process.Dispose();FailPending(new ObjectDisposedException("NativeBridge"));
    }
}

internal sealed class NativeProperties : CustomTypeDescriptor
{
    private readonly List<PropertyDescriptor> items=new List<PropertyDescriptor>();
    public void Add(string name,string category,Type type,Func<object> get,Action<object> set,string[] choices=null,string description="")
    {items.Add(new ValueProperty(name,category,type,get,set,choices,description));}
    public override PropertyDescriptorCollection GetProperties(){return new PropertyDescriptorCollection(items.ToArray());}
    public override PropertyDescriptorCollection GetProperties(Attribute[] attributes){return GetProperties();}
    public override object GetPropertyOwner(PropertyDescriptor pd){return this;}
}
internal sealed class ValueProperty : PropertyDescriptor
{
    private readonly Type type;private readonly Func<object> getter;private readonly Action<object> setter;private readonly string[] choices;
    public ValueProperty(string name,string category,Type type,Func<object> get,Action<object> set,string[] choices,string description)
        :base(name,new Attribute[]{new CategoryAttribute(category),new DescriptionAttribute(description)})
    {this.type=type;getter=get;setter=set;this.choices=choices;}
    public override Type ComponentType{get{return typeof(NativeProperties);}}
    public override Type PropertyType{get{return type;}}
    public override bool IsReadOnly{get{return setter==null;}}
    public override bool CanResetValue(object component){return false;}
    public override object GetValue(object component){return getter();}
    public override void SetValue(object component,object value){setter(value);OnValueChanged(component,EventArgs.Empty);}
    public override void ResetValue(object component){}
    public override bool ShouldSerializeValue(object component){return false;}
    public override TypeConverter Converter{get{return choices==null?base.Converter:new ChoiceConverter(choices);}}
}
internal sealed class ChoiceConverter : StringConverter
{
    private readonly string[] values;public ChoiceConverter(string[] values){this.values=values;}
    public override bool GetStandardValuesSupported(ITypeDescriptorContext context){return true;}
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext context){return true;}
    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context){return new StandardValuesCollection(values);}
}

internal sealed class SpriteStore : IDisposable
{
    private readonly Dictionary<string,Bitmap> images=new Dictionary<string,Bitmap>();
    private readonly Dictionary<int,ImageAttributes> attributesCache=new Dictionary<int,ImageAttributes>();
    public string Project;
    public Dictionary<string,SpriteMeta> Metadata=new Dictionary<string,SpriteMeta>();
    public Bitmap Get(string name,int frame=0)
    {
        if(String.IsNullOrEmpty(name))return null;
        SpriteMeta meta;Metadata.TryGetValue(name,out meta);int count=meta==null?1:Math.Max(1,meta.frames);frame=(frame%count+count)%count;
        string key=name+"|"+frame;Bitmap image;if(images.TryGetValue(key,out image))return image;
        string file=Path.Combine(Project,"sprites",name,frame+".png");
        if(!File.Exists(file))return null;
        using(var stream=new MemoryStream(File.ReadAllBytes(file)))using(var source=Image.FromStream(stream))image=new Bitmap(source);
        images[key]=image;return image;
    }
    public RectangleF Bounds(string name,double x,double y)
    {
        var image=Get(name);SpriteMeta meta;Metadata.TryGetValue(name??"",out meta);
        return image==null?new RectangleF((float)x,(float)y,16,16):new RectangleF((float)x-(meta==null?0:meta.originX),(float)y-(meta==null?0:meta.originY),image.Width,image.Height);
    }
    public void Draw(Graphics g,string name,double x,double y,double scale,double angle,double alpha,Color tint,int frame=0)
    {
        if(scale==0||alpha<=0)return;var image=Get(name,frame);if(image==null)return;
        SpriteMeta meta;Metadata.TryGetValue(name,out meta);
        int ox=meta==null?0:meta.originX,oy=meta==null?0:meta.originY;
        if(scale==1&&Math.Abs(angle%360)<.0001&&alpha>=.999&&tint.ToArgb()==Color.White.ToArgb()){g.DrawImageUnscaled(image,(int)Math.Round(x)-ox,(int)Math.Round(y)-oy);return;}
        int alphaByte=(int)Math.Max(0,Math.Min(255,Math.Round(alpha*255)));int key=Color.FromArgb(alphaByte,tint).ToArgb();ImageAttributes attributes;
        if(!attributesCache.TryGetValue(key,out attributes))
        {attributes=new ImageAttributes();
            var matrix=new ColorMatrix();matrix.Matrix00=tint.R/255f;matrix.Matrix11=tint.G/255f;matrix.Matrix22=tint.B/255f;matrix.Matrix33=(float)Math.Max(0,Math.Min(1,alpha));
            attributes.SetColorMatrix(matrix);
            if(attributesCache.Count>=1024){foreach(var entry in attributesCache.Values)entry.Dispose();attributesCache.Clear();}attributesCache[key]=attributes;
        }
        var state=g.Save();g.TranslateTransform((float)x,(float)y);g.RotateTransform((float)-angle);g.ScaleTransform((float)scale,(float)scale);
        g.DrawImage(image,new Rectangle(-ox,-oy,image.Width,image.Height),0,0,image.Width,image.Height,GraphicsUnit.Pixel,attributes);
        g.Restore(state);
    }
    public void Dispose(){foreach(var image in images.Values)image.Dispose();images.Clear();foreach(var attributes in attributesCache.Values)attributes.Dispose();attributesCache.Clear();}
}
