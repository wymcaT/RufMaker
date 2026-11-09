using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed partial class MainForm
{
    private static bool GmAssetKind(string kind){return kind=="objects"||kind=="paths"||kind=="timelines";}
    private static bool CanEditGmAsset(string name){return !String.IsNullOrEmpty(name)&&!name.StartsWith("objRuf",StringComparison.OrdinalIgnoreCase);}
    private static bool AcceptsGmPath(Track track){return track!=null&&(track.kind=="emitter"||track.kind=="native-instance");}
    private static string GmNewLabel(string kind){return kind=="objects"?"新建 GM 对象…":kind=="paths"?"新建 GM 路径…":"新建原生 Timeline…";}

    private void AddGmResourceCommands(ContextMenuStrip menu)
    {
        string kind=resourceKind;
        string name=resources.SelectedItems.Count==0?null:resources.SelectedItems[0].Tag as string;
        if(CanEditGmAsset(name)){
            menu.Items.Add("打开编辑器…",null,async(s,e)=>await OpenGmAsset(kind,name));
            if(kind=="objects")menu.Items.Add("删除 GM 对象…",null,async(s,e)=>await DeleteGmObject(name));
            if(kind=="paths"&&AcceptsGmPath(selectedTrack))menu.Items.Add("应用到选中物件",null,(s,e)=>AttachGmPath(selectedTrack,name));
        }
        menu.Items.Add(GmNewLabel(kind),null,async(s,e)=>await CreateGmAsset(kind));
    }

    private async Task CreateGmAsset(string kind)
    {
        if(state==null||bridge==null||busy)return;
        string name=GmResourceUi.TextEntry(this,GmNewLabel(kind),kind=="objects"?"objNew":kind=="paths"?"pathNew":"tlNew");
        if(String.IsNullOrEmpty(name))return;
        try{
            await bridge.Call<GmAssetData>("gm-asset-edit",new{kind,name,operation="create"});
            await RefreshEditedGmResources();
            await OpenGmAsset(kind,name);
        }catch(Exception error){Error(error);}
    }

    private async Task OpenGmAsset(string kind,string name,string section=null)
    {
        if(state==null||bridge==null||busy||!CanEditGmAsset(name))return;
        StopPlaying();CommitInspectorEdits();
        try{
            var data=await bridge.Call<GmAssetData>("gm-asset-read",new{kind,name});
            bool changed;
            if(kind=="paths"){
                var room=stage.Room??document.room;
                var background=PreviewRenderer.Render(new EditorState(),room,document.inventory.objectSprites,sprites,0,false,false,roomOnly:true);
                using(var editor=new GmPathEditor(data,room,background,async path=>
                    await bridge.Call<GmAssetData>("gm-asset-edit",new{kind,name,data.revision,operation="settings",path})))
                    changed=Language.ShowDialog(editor,this)==DialogResult.OK;
            }else if(kind=="objects"){
                using(var editor=new GmObjectEditor(bridge,document.inventory,data)){Language.ShowDialog(editor,this);changed=editor.Changed;}
            }else{
                using(var editor=new GmAssetEditor(bridge,data,section)){Language.ShowDialog(editor,this);changed=editor.Changed;}
            }
            if(!changed)return;
            await RefreshEditedGmResources();
            if(state.nativeProject!=null&&kind!="paths")await RefreshGmTiming();
            SetStatus("修改已保存到 GameMaker 资源。");
        }catch(Exception error){Error(error);}
    }

    private async Task DeleteGmObject(string name)
    {
        if(state==null||bridge==null||busy||!CanEditGmAsset(name))return;
        StopPlaying();CommitInspectorEdits();saveTimer.Stop();
        try{
            if(dirty&&!await SaveNow(false))return;
            var asset=await bridge.Call<GmAssetData>("gm-asset-read",new{kind="objects",name});
            var refs=await bridge.Call<string[]>("gm-object-references",new{name});
            string title="删除 GM 对象…";
            if(refs.Length>0){
                var locations=String.Join("\n",refs.Take(8));
                if(refs.Length>8)locations+="\n"+String.Format(Language.Text("另有 {0} 处引用。"),refs.Length-8);
                MessageBox.Show(this,Language.Text("请先移除下列对该对象的引用。")+"\n\n"+locations,Language.Text(title),MessageBoxButtons.OK,MessageBoxIcon.Information);
                return;
            }
            var answer=MessageBox.Show(this,Language.Text("从 GameMaker 工程删除此对象？")+"\n\n"+name,
                Language.Text(title),MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2);
            if(answer!=DialogResult.Yes)return;
            await bridge.Call<object>("gm-asset-edit",new{kind="objects",name,asset.revision,operation="delete"});
            if(resourceName==name)resourceName=null;
            await RefreshEditedGmResources();
            SetStatus(Language.Text("已删除 GM 对象")+" · "+name);
        }catch(Exception error){Error(error);}
    }

    private async Task RefreshEditedGmResources()
    {
        var result=await bridge.Call<RoomResourceResult>("gm-resources-read",new{room=state.room});
        ApplyGmResourceData(result);
        NativeObjectPreviewChanged(true);stage.SceneChanged();FillResources();ShowPropertiesCore(false);
    }

    private void ApplyGmResourceData(RoomResourceResult result)
    {
        var old=sprites;
        sprites=new SpriteStore{Project=document.project,Metadata=result.spriteMeta};
        document.inventory=result.inventory;document.spriteMeta=result.spriteMeta;
        state.nativePaths=document.nativePaths=result.nativePaths;
        stage.Sprites=sprites;stage.ObjectSprites=document.inventory.objectSprites;
        if(old!=null)old.Dispose();
        ReplaceRoomFromDisk(result.room);
    }

    private async Task RefreshGmTiming()
    {
        if(state.nativeProject==null)return;
        var report=await bridge.Call<NativeScan>("gm-native-timing",new{});
        if(report==null)return;
        var native=state.nativeProject;
        native.events=report.events;native.unresolved=report.unresolved;native.warnings=report.warnings;
        native.timeline=report.timeline;native.timelineSpeed=report.timelineSpeed;native.clock=report.clock;
        native.duration=report.duration;native.confidence=report.confidence;
        state.duration=Math.Max(state.duration,report.events.Select(item=>item.frame).DefaultIfEmpty(0).Max());
        var track=state.tracks.FirstOrDefault(t=>t.kind=="native");
        if(track!=null)track.end=Math.Min(state.duration,native.duration);
        selectedNativeEvent=null;
        frameInput.Maximum=state.duration;
        timeline.RefreshLayout();UpdateTitle();ShowPropertiesCore(false);
    }

    private void AttachGmPath(Track track,string name)
    {
        if(!AcceptsGmPath(track)){SetStatus("请将 GM 路径拖到弹幕来源或原生对象片段上。");return;}
        if(!state.nativePaths.ContainsKey(name))return;
        StopPlaying();CommitInspectorEdits();
        if(track.nativePath!=name){PushUndo();track.nativePath=name;MarkChanged();}
        if(selectedTrack!=track||timeline.SelectedTracks.Count!=1)SelectTrack(track,null);else ShowPropertiesCore(false);
        inspectorTabs.SelectedIndex=0;inspectorDock.Activate("general");properties.OpenSection("沿 GM 路径");
        SetStatus("已绑定 GM 路径。");
    }

    private void GmPathFields(NativeProperties bag,Track track)
    {
        if(!AcceptsGmPath(track)||String.IsNullOrEmpty(track.nativePath))return;
        const string category="沿 GM 路径";
        var choices=new[]{""}.Concat(document.inventory.paths).Concat(new[]{track.nativePath}).Distinct().ToArray();
        Field(bag,"GM 路径",category,()=>track.nativePath,v=>{track.nativePath=v;QueueInspectorUpdate();},choices);
        Field(bag,"循环路径",category,()=>track.nativePathLoop,v=>track.nativePathLoop=v);
        if(track.kind=="native-instance"){
            Field(bag,"路径速度 / 像素每帧",category,()=>track.nativePathSpeed,v=>track.nativePathSpeed=Math.Max(-1000000,Math.Min(1000000,v)));
            Field(bag,"使用房间坐标",category,()=>track.nativePathAbsolute,v=>track.nativePathAbsolute=v);
        }else{
            Field(bag,"走完路径所需帧数",category,()=>track.nativePathFrames,v=>track.nativePathFrames=Math.Max(1,Math.Min(1000000,v)),
                null,"按路程比例移动，物件的 X/Y 是路径起点。");
        }
    }

    private async Task EditRoomInstanceCode(RoomInstance instance)
    {
        if(instance==null||bridge==null||busy||writingRoomInstance||!CanEditGmAsset(instance.name))return;
        StopPlaying();CommitInspectorEdits();
        string room=state.room,id=instance.id,revision=document.room.instanceRevision;
        try{
            var code=await bridge.Call<GmInstanceData>("gm-instance-code-read",new{room,id});
            if(code.fields.Length>0){
                var options=new[]{Language.Text("对象字段"),Language.Text("实例创建 GML")};
                string action=GmResourceUi.TextEntry(this,"编辑实例…","",options);
                if(action==null)return;
                if(action==options[0]){await EditObjectFields(instance,code,room,id,revision);return;}
            }
            using(var editor=new CodeForm(Language.Text("实例创建 GML")+" · "+instance.name,code.content,async content=>{
                var result=await bridge.Call<RoomData>("gm-instance-code-save",new{room,id,revision,code.hash,content});
                revision=result.instanceRevision;code.hash=result.codeHash;
                SaveRoomInstanceCode(result);
            }))
                Language.ShowDialog(editor,this);
        }catch(Exception error){Error(error);}
    }

    private async Task EditObjectFields(RoomInstance instance,GmInstanceData source,string room,string id,string revision)
    {
        var current=source.fields.ToDictionary(f=>f.name,f=>f.value);
        var bag=new NativeProperties();
        foreach(var item in source.fields){
            var field=item;
            string[] choices=new[]{"bool","boolean","true","false"}.Contains(field.type)?new[]{"","true","false","1","0",field.value}.Distinct().ToArray():null;
            string category=field.depends.Length==0?"对象字段":String.Join(" / ",field.depends);
            bag.Add(field.name,category,typeof(string),()=>current[field.name],v=>current[field.name]=(string)v,choices,
                field.spec+(String.IsNullOrEmpty(field.description)?"":" · "+field.description));
        }
        bag.Add("提示","对象字段",typeof(string),()=>Language.Text("输入 GML 值。字符串需要引号，XY 用逗号分隔，留空使用对象默认值。"),null);
        if(!GmResourceUi.EditFields(this,Language.Text("对象字段")+" · "+instance.name,bag,new Size(590,520)))return;
        var fields=source.fields.Where(f=>current[f.name]!=f.value).Select(f=>new{f.name,value=current[f.name]}).ToArray();
        if(fields.Length>0)SaveRoomInstanceCode(await bridge.Call<RoomData>("gm-instance-code-save",new{room,id,revision,source.hash,fields}));
    }
    private void SaveRoomInstanceCode(RoomData room)
    {ReplaceRoomFromDisk(room);stage.SceneChanged();ShowPropertiesCore(false);SetStatus("实例创建代码已写入 GameMaker 房间。");}
}
