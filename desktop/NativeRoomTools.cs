using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed partial class MainForm
{
    private RoomData browsedRoom;
    private int roomBrowseRequest,roomUndoEpoch;
    private ToolStripButton returnToRoom;
    private bool writingRoomInstance;

    private async Task BrowseRoom(string name)
    {
        if(state==null||bridge==null||busy)return;
        if(name==state.room){ReturnToTimelineRoom();return;}
        int request=++roomBrowseRequest;
        try{
            var room=await bridge.Call<RoomData>("room-read",new{room=name});
            if(request!=roomBrowseRequest||IsDisposed)return;
            StopPlaying();
            SelectProject();
            browsedRoom=room;
            stage.InspectRoomOnly=true;
            stage.Room=room;
            stage.ResetView();
            stage.SceneChanged();
            inspectorTabs.SelectedIndex=0;inspectorDock.Activate("general");
            ShowPropertiesCore(false);
            SetStatus("正在查看其他房间，耐久时间轴仍属于原房间。");
        }catch(Exception error){Error(error);}
    }

    private void ReturnToTimelineRoom(bool updateInspector=true)
    {
        roomBrowseRequest++;
        if(browsedRoom==null)return;
        browsedRoom=null;
        stage.InspectRoomOnly=false;
        stage.Room=document.room;
        stage.ResetView();
        stage.SceneChanged();
        if(updateInspector)ShowPropertiesCore(false);
    }

    private void RoomSummary(NativeProperties bag,RoomData room)
    {
        selectionTitle.Text=Language.Text("房间查看")+" · "+room.name;
        bag.Add("房间大小","房间",typeof(string),()=>room.width+" × "+room.height,null);
        bag.Add("房间实例数","房间",typeof(int),()=>room.instances.Count,null);
        bag.Add("瓦片数","房间",typeof(int),()=>room.tiles.Length,null);
        bag.Add("原生视野","房间",typeof(int),()=>room.views.Length,null);
    }

    private async Task ReadGameMakerChanges()
    {
        if(state==null||bridge==null||busy||writingRoomInstance)return;
        StopPlaying();CommitInspectorEdits();SetBusy(true);
        try{
            // The resource read leaves GM files alone and keeps the editor's undo and playhead.
            var result=await bridge.Call<RoomResourceResult>("gm-resources-read",new{room=state.room});
            RoomData other=null;
            if(browsedRoom!=null&&result.inventory.rooms.Contains(browsedRoom.name))
                other=await bridge.Call<RoomData>("room-read",new{room=browsedRoom.name});
            if(other==null)ReturnToTimelineRoom(false);
            else browsedRoom=other;
            ApplyGmResourceData(result);
            NativeObjectPreviewChanged(true);
            if(state.nativeProject!=null&&nativeRender.Checked)nativeRender.Checked=false;
            if(browsedRoom!=null)stage.Room=browsedRoom;
            stage.SceneChanged();
            FillResources();ShowPropertiesCore(false);
            if(state.nativeProject!=null)await RefreshGmTiming();
            SetStatus(state.nativeProject==null?"已读取 GameMaker 的房间和资源修改。":"已读取 GameMaker 修改，已切换到房间预览。原生底片可按需重新生成。");
        }catch(Exception error){Error(error);}
        finally{SetBusy(false);}
    }

    private void ReplaceRoomFromDisk(RoomData room)
    {
        if(document.room!=null&&document.room.instanceRevision!=room.instanceRevision&&
            !EditableRoomRows(document.room).SequenceEqual(EditableRoomRows(room)))roomUndoEpoch++;
        ReplaceRoom(room);
    }

    private static System.Collections.Generic.IEnumerable<string> EditableRoomRows(RoomData room)
    {return room.instances.Where(i=>i.name!="objRufController").Select(i=>i.raw);}

    private async Task RefreshRoomAfterConflict(Exception error)
    {
        try{ReplaceRoomFromDisk(await bridge.Call<RoomData>("room-read",new{room=state.room}));ShowPropertiesCore(false);}
        catch{ }
        Error(error);
    }

    private void InstanceTransformFields(NativeProperties bag,RoomInstance instance)
    {
        const string section="原生实例变换";
        Action<Action> edit=change=>{
            if(writingRoomInstance||busy)return;
            PushUndo();change();stage.SceneChanged();if(!parameterScrubbing)SaveInstanceFromProperty(instance);
        };
        bag.Add("水平缩放",section,typeof(double),()=>instance.scaleX,v=>edit(()=>instance.scaleX=(double)v));
        bag.Add("垂直缩放",section,typeof(double),()=>instance.scaleY,v=>edit(()=>instance.scaleY=(double)v));
        bag.Add("角度",section,typeof(double),()=>instance.angle,v=>edit(()=>instance.angle=(double)v));
        bag.Add("颜色",section,typeof(Color),()=>RoomPresentation.Blend(instance.blend),v=>edit(()=>{
            var color=(Color)v;
            instance.blend=RoomPresentation.Pack(Color.FromArgb(RoomPresentation.Blend(instance.blend).A,color));
        }));
        bag.Add("透明度",section,typeof(double),()=>RoomPresentation.Blend(instance.blend).A/255.0,v=>edit(()=>{
            var color=RoomPresentation.Blend(instance.blend);
            int alpha=(int)Math.Round(Math.Max(0,Math.Min(1,(double)v))*255);
            instance.blend=RoomPresentation.Pack(Color.FromArgb(alpha,color));
        }));
        bag.Add("对象深度",section,typeof(double),()=>instance.depth,null);
    }

    private void AddNativeObject(string name,int at)
    {
        if(state==null||busy)return;
        if(!CanScheduleObject(name)){SetStatus("该对象不能从时间轴生成。");return;}
        ReturnToTimelineRoom(false);
        StopPlaying();CommitInspectorEdits();PushUndo();
        int start=Math.Max(0,Math.Min(state.duration,at));
        int lane=timeline.ActiveLane>=0?timeline.ActiveLane:Math.Max(0,state.laneCount-1);
        var track=new Track{id=Guid.NewGuid().ToString("N"),name=name,kind="native-instance",nativeObject=name,
            start=start,end=Math.Min(state.duration,start+state.fps-1),lane=lane,x=document.room.width/2.0,y=document.room.height/2.0};
        state.tracks.Add(track);state.laneCount=Math.Max(state.laneCount,lane+1);
        SelectTrack(track,null);inspectorTabs.SelectedIndex=0;inspectorDock.Activate("general");SetFrame(start);MarkChanged();
        SetStatus("已添加原生对象片段。拖动预览中的位置，或在右栏设置生成参数。");
    }

    private bool CanScheduleObject(string name)
    {return !String.IsNullOrEmpty(name)&&!name.StartsWith("objRuf",StringComparison.OrdinalIgnoreCase)&&document!=null&&document.inventory.objects.Contains(name);}

    private void NativeObjectFields(NativeProperties bag,Track track)
    {
        var choices=document.inventory.objects.Where(CanScheduleObject).Concat(new[]{track.nativeObject}).Distinct().ToArray();
        const string section="生成实例";
        Field(bag,"GM 对象",section,()=>track.nativeObject,value=>track.nativeObject=value,choices);
        Field(bag,"生成 X",section,()=>track.x,value=>track.x=value);
        Field(bag,"生成 Y",section,()=>track.y,value=>track.y=value);
        Field(bag,"片段结束后保留",section,()=>track.nativeKeepAlive,value=>track.nativeKeepAlive=value);
        bag.Add("预览",section,typeof(string),()=>Language.Text("按空格生成原生事件动态预览，随后可拖动时间轴查看。修改参数后再次播放会更新预览。F5 进入真实试玩。"),null);
        codeButton.Enabled=true;
    }

    private void EditNativeObjectInit(Track track)
    {
        using(var editor=new CodeForm(Language.Text("实例初始化 GML · ")+track.nativeObject,track.code,async text=>{
            PushUndo();track.code=text;MarkChanged();if(!await SaveNow(false))throw new System.IO.IOException(saveFailure??Language.Text("保存失败"));
        }))Language.ShowDialog(editor,this);
    }
}

internal static class NativeClip
{
    public static bool HasClips(System.Collections.Generic.IEnumerable<Track> tracks)
    {return tracks.Any(t=>t.enabled&&!t.templateOnly&&(t.kind=="native-instance"||t.kind=="pattern"&&HasClips(t.children)));}
    public static string PreviewKey(EditorState state)
    {
        if(state==null||!HasClips(state.tracks))return "";
        return Json.Write(new{state.room,state.duration,state.fps,tracks=PreviewRows(state.tracks)});
    }
    private static object[] PreviewRows(System.Collections.Generic.IEnumerable<Track> tracks)
    {
        var rows=new System.Collections.Generic.List<object>();
        foreach(var t in tracks){
            if(t.kind=="native-instance")rows.Add(new{t.id,t.nativeObject,t.enabled,t.templateOnly,t.start,t.end,t.clockStart,t.clockEnd,t.parentId,t.x,t.y,t.code,t.nativeKeepAlive,t.nativePath,t.nativePathSpeed,t.nativePathLoop,t.nativePathAbsolute});
            else if(t.kind=="group"||t.kind=="variable")rows.Add(t);
            else if(t.kind=="pattern"&&HasClips(t.children))rows.Add(new{t.id,t.enabled,t.start,t.end,t.clockStart,t.clockEnd,t.repeatCount,t.repeatGap,t.timeScale,children=PreviewRows(t.children)});
        }
        return rows.ToArray();
    }
    public static Keyframe SpawnPoint(Track track)
    {
        int start=Math.Max(0,track.start);
        for(var parent=track.parent;parent!=null;parent=parent.parent)start=Math.Max(start,parent.start);
        return BulletSystem.Transform(track.parent,start,new Keyframe{x=track.x,y=track.y,scale=1,alpha=1});
    }
}
