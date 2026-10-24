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
    public int randomSeed=12345,laneCount;public bool randomizeOnPlay;
    public bool usePlayerTrace;public List<PlayerTracePoint> playerTrace=new List<PlayerTracePoint>();
    public string[] plainRooms=new string[0];
    public NativeProject nativeProject;
    public NativeTestPosition nativeTest=new NativeTestPosition();
    public List<Track> tracks=new List<Track>();
    public EditorState Snapshot()
    {
        var copy=(EditorState)MemberwiseClone();copy.tracks=tracks.Select(t=>t.Snapshot()).ToList();copy.playerTrace=playerTrace.Select(p=>new PlayerTracePoint{frame=p.frame,x=p.x,y=p.y}).ToList();copy.plainRooms=plainRooms==null?null:(string[])plainRooms.Clone();
        copy.nativeTest=nativeTest==null?null:new NativeTestPosition{x=nativeTest.x,y=nativeTest.y};copy.player=player==null?null:player.Snapshot();copy.nativeProject=nativeProject==null?null:nativeProject.Snapshot();copy.terrain=terrain==null?null:Json.Read<object>(Json.Write(terrain));return copy;
    }
}
internal sealed class NativeTestPosition {public double x=400,y=304;}
internal sealed class PlayerTracePoint {public int frame;public double x,y;}
internal sealed class PlayerSettings {public double gravity=.4,moveSpeed=3,jumpSpeed=8.5,doubleJumpSpeed=7;public int maxJumps=2;public bool infiniteJump;public PlayerSettings Snapshot(){return (PlayerSettings)MemberwiseClone();}}
internal sealed class Track
{
    public string tag="",parentId="",emissionShape="point",emissionFacing="fan",randomScope="bullet";
    public bool advancedBullets;
    public double emitWidth=240,emitHeight=160,emitArc=360,emitPhase,emitRotation,emitWaves=1,indexSpeed,indexAngle,indexScale;
    public int indexDelay,releaseFrame=-1,repeatCount=1,repeatGap;public double timeScale=1;
    public string releaseDirection="radial";public List<CurvePoint> curvePoints=new List<CurvePoint>();
    public List<BulletAction> bulletActions=new List<BulletAction>(),spawnActions=new List<BulletAction>();public List<Track> children=new List<Track>();
    public bool templateOnly;
    public string targetSource="",targetTag="",targetParity="all",controlAction="speed",controlExpression="",controlEasing="linear";
    public int targetVolley=-1,targetMinAge,targetMaxAge=1000000;public bool includeFuture;
    public double controlFrom,controlValue;public string controlColor="#FFFFFF";
    [ScriptIgnore] public Track parent;[ScriptIgnore] public EditorState owner;
    public int lane=-1;public string randomId="";public Dictionary<string,string> expressions=new Dictionary<string,string>();
    public string id, name, kind, sprite="sprBullet", easing="linear", color="#ff6d91", effect="flash", code="", rodMode="center", motion="keyframes";
    public bool enabled=true, trail, rolling, rotateSprite, trailRandom;
    [ScriptIgnore] public int expressionSeed=12345,expressionFps=50;
    [ScriptIgnore] public bool liveExpressions;
    [ScriptIgnore] public double expressionPlayerX=400,expressionPlayerY=304;
    public string flightMode="straight",flightExpression="point_direction(x,y,player.x,player.y)";
    public double turnRate=3;public int homingDelay;
    public string shape="rod";
    public double radius=120,orientation,sides=5,cameraZoom=1,shapeAspect=1,shapeInner=.45;
    public string cameraStyle="";public double cameraIntensity=1,cameraOriginX=400,cameraOriginY=304,cameraOriginZoom=1,cameraOriginAngle;
    public bool cameraMirror;public double cameraBlur,cameraBlurAngle;
    public double fxZoom=1.13,fxFlash=.18;public int fxSlices=11,fxStutter=2;
    public string trailMode="legacy";
    public int trailInterval=3,trailLife=18;
    public double spriteAngle,trailScaleDecay=.035,trailAlphaDecay=.035,trailStartAlpha=.55,trailSpeed,trailDirection;
    public bool rainbow,rainbowReverse,rainbowDynamic;
    public string rainbowSourceSprite="",rainbowSourceColor="",rainbowSprite="";
    public double rainbowDensity=1,rainbowOffset,rainbowSpeed=.25;
    public int start, end, interval=25, count=12, life=160;
    public double speed=3, spread=30, angle, strength=6;
    public double x=400,y=304,scale=1,alpha=1,x1=280,y1=304,x2=520,y2=304,length=240,rollSpeed=2,easePower=3,easePeriod=.3;
    public double velocityX,velocityY,accelerationX,accelerationY,angularSpeed,orbitRadius=100,orbitSpeed=1,orbitPhase;
    public double pathStartX,pathStartY,pathEndX,pathEndY,pathPeakX,pathPeakY,pathProgress;
    public double fxOpacity=1,fxAngle,fxRetention=.5,fxFrequency=1;
    public int fxSamples=6;
    public List<Keyframe> keyframes=new List<Keyframe>();
    public Track Snapshot(){var copy=(Track)MemberwiseClone();copy.keyframes=keyframes.Select(k=>k.Snapshot()).ToList();copy.expressions=new Dictionary<string,string>(expressions??new Dictionary<string,string>());copy.bulletActions=(bulletActions??new List<BulletAction>()).Select(a=>a.Snapshot()).ToList();copy.spawnActions=(spawnActions??new List<BulletAction>()).Select(a=>a.Snapshot()).ToList();copy.curvePoints=(curvePoints??new List<CurvePoint>()).Select(p=>new CurvePoint{x=p.x,y=p.y}).ToList();copy.children=(children??new List<Track>()).Select(t=>t.Snapshot()).ToList();copy.parent=null;copy.owner=null;return copy;}
}
internal sealed class CurvePoint {public double x,y;}
internal sealed class BulletAction
{
    public string type="speed",easing="linear",expression="",templateId="";public bool enabled=true,relative;
    public string spawnOrigin="center",spawnParity="all";public int spawnRepeat,spawnIndexFrom,spawnIndexTo=-1;
    public double spawnOffsetX,spawnOffsetY;public bool inheritAngle,destroyParent;
    public int start,duration=30,stagger,count=5,maxGeneration=1;public double from,to=3,spread=20,childSpeed=3;
    public BulletAction Snapshot(){return (BulletAction)MemberwiseClone();}
}
internal sealed class Keyframe
{
    public int frame;
    public double x=400,y=304,angle,scale=1,alpha=1;
    public string property,easing="";
    public double value;
    public string expression="",randomId="";
    public Keyframe Snapshot(){return (Keyframe)MemberwiseClone();}
}
internal sealed class RoomData
{
    public string name;
    public int width=800,height=608,fps=50,bgColor=0x202020;
    public List<RoomInstance> instances=new List<RoomInstance>();
    public RoomData Snapshot(){return new RoomData{name=name,width=width,height=height,fps=fps,bgColor=bgColor,instances=instances.Select(i=>i.Snapshot()).ToList()};}
    public bool SameContent(RoomData other)
    {
        if(other==null)return false;if(ReferenceEquals(this,other))return true;
        if(name!=other.name||width!=other.width||height!=other.height||fps!=other.fps||bgColor!=other.bgColor||instances.Count!=other.instances.Count)return false;
        for(int n=0;n<instances.Count;n++){var a=instances[n];var b=other.instances[n];if(a.id!=b.id||a.index!=b.index||a.name!=b.name||a.raw!=b.raw||a.x!=b.x||a.y!=b.y)return false;}return true;
    }
}
internal sealed class RoomInstance {public int index;public string id,name,raw;public double x,y;public RoomInstance Snapshot(){return (RoomInstance)MemberwiseClone();}}
internal sealed class UndoKey {public string trackId,property;public int frame;}
internal sealed class UndoSnapshot
{
    public EditorState state;
    public RoomData room;
    public string selectedTrack,selectedProperty,selectedInstance;
    public string[] selectedTracks=new string[0];
    public UndoKey selectedKey;
    public UndoKey[] selectedKeys=new UndoKey[0];
}
internal sealed class UndoEntry
{
    private readonly UndoSnapshot snapshot;
    public UndoEntry(UndoSnapshot source)
    {
        snapshot=Copy(source);
    }
    private static UndoSnapshot Copy(UndoSnapshot source)
    {
        Func<UndoKey,UndoKey> key=k=>k==null?null:new UndoKey{trackId=k.trackId,property=k.property,frame=k.frame};
        return new UndoSnapshot{state=source.state==null?null:source.state.Snapshot(),room=source.room==null?null:source.room.Snapshot(),selectedTrack=source.selectedTrack,selectedTracks=(string[])source.selectedTracks.Clone(),selectedProperty=source.selectedProperty,selectedKey=key(source.selectedKey),selectedKeys=source.selectedKeys.Select(key).ToArray(),selectedInstance=source.selectedInstance};
    }
    public UndoSnapshot Restore(){return Copy(snapshot);}
    public bool SameScene(UndoEntry other){return SceneComparison.Same(snapshot.state,other.snapshot.state)&&SceneComparison.Same(snapshot.room,other.snapshot.room);}
}
internal static class SceneComparison
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type,System.Reflection.FieldInfo[]> fields=new System.Collections.Concurrent.ConcurrentDictionary<Type,System.Reflection.FieldInfo[]>();
    // Only persisted model fields participate. Parent/owner links and preview caches are excluded.
    public static bool Same(object a,object b)
    {
        if(ReferenceEquals(a,b))return true;if(a==null||b==null||a.GetType()!=b.GetType())return false;Type type=a.GetType();if(type.IsValueType||a is string)return a.Equals(b);
        var map=a as System.Collections.IDictionary;if(map!=null){var other=(System.Collections.IDictionary)b;if(map.Count!=other.Count)return false;foreach(object key in map.Keys)if(!other.Contains(key)||!Same(map[key],other[key]))return false;return true;}
        var list=a as System.Collections.IList;if(list!=null){var other=(System.Collections.IList)b;if(list.Count!=other.Count)return false;for(int n=0;n<list.Count;n++)if(!Same(list[n],other[n]))return false;return true;}
        foreach(var field in fields.GetOrAdd(type,t=>t.GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public).Where(f=>!f.IsDefined(typeof(ScriptIgnoreAttribute),true)).ToArray()))if(!Same(field.GetValue(a),field.GetValue(b)))return false;return true;
    }
}
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
internal sealed class NativeRunProgress {public string phase,room,error;public int frame,target;}
internal sealed class NativeEvent {public string id,file,expression,kind,label,snippet,reason;public int frame,line;public double period;public NativeEvent Snapshot(){return (NativeEvent)MemberwiseClone();}}
internal sealed class NativeProject
{
    public int version=1,fps=50,width=800,height=608,duration;public bool imported,hasFmod;public double timelineSpeed=1,originalStart,bpm;
    public string entry,source,name,room,controller,clock,timeline,music,bootObject,musicInitializer,confidence,defaultSprite,killerParent;
    public NativeEvent[] events=new NativeEvent[0],unresolved=new NativeEvent[0];public string[] warnings=new string[0];
    public NativeProject Snapshot(){var copy=(NativeProject)MemberwiseClone();copy.events=events.Select(e=>e.Snapshot()).ToArray();copy.unresolved=unresolved.Select(e=>e.Snapshot()).ToArray();copy.warnings=(string[])warnings.Clone();return copy;}
}
internal sealed class NativeRoomCandidate {public string name;public int width,height,fps,score;public NativeControllerCandidate[] controllers;public override string ToString(){return name+Language.Text(score>0?" · 候选":"");}}
internal sealed class NativeControllerCandidate {public string name;public int score;public bool direct;}
internal sealed class NativeScan:NativeScanBase {}
internal class NativeScanBase {public string entry,name,room,controller,clock,timeline,music,confidence;public int fps,duration;public string[] entries,controllers,musicChoices,warnings;public NativeRoomCandidate[] rooms;public NativeEvent[] events,unresolved;}
internal sealed class NativeImportOptions {public string entry,name,parent,room,controller,music;public double duration;}
internal sealed class NativeImportResult {public string path;public string[] warnings;public int events;}
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
    public string GameMakerPath {get;private set;}
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
        var engine=EngineSetup.Find(root);GameMakerPath=engine.Ready?engine.Exe:"";if(engine.Ready)info.EnvironmentVariables["RUFMAKER_GAMEMAKER"]=engine.Exe;else info.EnvironmentVariables.Remove("RUFMAKER_GAMEMAKER");
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
    public void Add(string name,string category,Type type,Func<object> get,Action<object> set,string[] choices=null,string description="",ParameterActions actions=null,bool localizeChoices=true)
    {items.Add(new ValueProperty(name,category,type,get,set,choices,description){Actions=actions,LocalizeChoices=localizeChoices});}
    public override PropertyDescriptorCollection GetProperties(){return new PropertyDescriptorCollection(items.ToArray());}
    public override PropertyDescriptorCollection GetProperties(Attribute[] attributes){return GetProperties();}
    public override object GetPropertyOwner(PropertyDescriptor pd){return this;}
    public bool TryRebind(NativeProperties next)
    {
        if(next==null||items.Count!=next.items.Count)return false;
        for(int i=0;i<items.Count;i++)if(!((ValueProperty)items[i]).CanRebind((ValueProperty)next.items[i]))return false;
        for(int i=0;i<items.Count;i++)((ValueProperty)items[i]).Rebind((ValueProperty)next.items[i]);return true;
    }
}
internal sealed class ValueProperty : PropertyDescriptor
{
    public ParameterActions Actions;public bool LocalizeChoices=true;
    private readonly Type type;private Func<object> getter;private Action<object> setter;private readonly string[] choices;
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
    internal bool CanRebind(ValueProperty next)
    {
        return Name==next.Name&&Category==next.Category&&Description==next.Description&&type==next.type&&IsReadOnly==next.IsReadOnly&&LocalizeChoices==next.LocalizeChoices&&
            (choices==null?next.choices==null:next.choices!=null&&choices.SequenceEqual(next.choices))&&
            (Actions==null?next.Actions==null:next.Actions!=null&&(Actions.ToggleKey==null)==(next.Actions.ToggleKey==null)&&(Actions.EditExpression==null)==(next.Actions.EditExpression==null));
    }
    internal void Rebind(ValueProperty next)
    {
        getter=next.getter;setter=next.setter;
        if(Actions!=null){Actions.EditExpression=next.Actions.EditExpression;Actions.Keyed=next.Actions.Keyed;Actions.Random=next.Actions.Random;Actions.ToggleKey=next.Actions.ToggleKey;Actions.ToggleRandom=next.Actions.ToggleRandom;Actions.Expression=next.Actions.Expression;Actions.SetExpression=next.Actions.SetExpression;}
    }
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
        int alphaByte=(int)Math.Max(0,Math.Min(255,Math.Round(alpha*255)));int key=Color.FromArgb(alphaByte,tint).ToArgb();ImageAttributes attributes;
        if(!attributesCache.TryGetValue(key,out attributes))
        {attributes=new ImageAttributes();
            var matrix=new ColorMatrix();matrix.Matrix00=tint.R/255f;matrix.Matrix11=tint.G/255f;matrix.Matrix22=tint.B/255f;matrix.Matrix33=(float)Math.Max(0,Math.Min(1,alpha));
            attributes.SetColorMatrix(matrix);attributes.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
            if(attributesCache.Count>=1024){foreach(var entry in attributesCache.Values)entry.Dispose();attributesCache.Clear();}attributesCache[key]=attributes;
        }
        // Clamp sampling at image edges. Transparent samples outside a tile become cracks after rotation.
        if(scale==1&&Math.Abs(angle%360)<.0001&&x==Math.Floor(x)&&y==Math.Floor(y)){g.DrawImage(image,new Rectangle((int)x-ox,(int)y-oy,image.Width,image.Height),0,0,image.Width,image.Height,GraphicsUnit.Pixel,attributes);return;}
        var state=g.Save();g.TranslateTransform((float)x,(float)y);g.RotateTransform((float)-angle);g.ScaleTransform((float)scale,(float)scale);
        g.DrawImage(image,new Rectangle(-ox,-oy,image.Width,image.Height),0,0,image.Width,image.Height,GraphicsUnit.Pixel,attributes);
        g.Restore(state);
    }
    public void Dispose(){foreach(var image in images.Values)image.Dispose();images.Clear();foreach(var attributes in attributesCache.Values)attributes.Dispose();attributesCache.Clear();}
}
