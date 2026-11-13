using System;
using System.Linq;
using System.Threading.Tasks;

internal static class ObjectProperties
{
    public const string Kind="object-properties";
    public const string Category="物件属性";
    public const string Help="拖到物件上添加属性，或双击应用到当前物件。";
    public const string Scatter="散开飞行";
    public const string Boundaries="反弹设置";
    public static readonly string[] Keys={"rainbow","trail","lifetime","scatter","bounce","physics","animation"};
    public static readonly string[] Names={"彩虹 Cherry","拖影","弹幕寿命",Scatter,Boundaries,"弹幕物理","贴图动画"};
    private static readonly string[] BoundaryNames={"按寿命设置","出屏幕反弹","出屏幕从另一边穿回来"};
    private static readonly string[] BoundaryModes={"none","reflect","wrap"};

    public static bool Supports(Track track,string key)
    {
        if(track==null)return false;
        if(key=="rainbow"||key=="trail")return Theme.Visual(track.kind);
        if(key=="scatter")return track.kind=="rod";
        if(key=="physics")return track.kind=="emitter"||track.kind=="rod";
        if(key=="animation")return Theme.Visual(track.kind);
        return (key=="lifetime"||key=="bounce")&&(track.kind=="emitter"||track.kind=="rod");
    }

    public static bool Shows(Track track,int index)
    {return (track.hiddenObjectProperties&(1<<index))==0&&Configured(track,index);}

    public static bool Configured(Track track,int index)
    {
        if((track.objectProperties&(1<<index))!=0)return true;
        switch(index){
            case 0:return track.rainbow;
            case 1:return Trails.Mode(track)!="off";
            case 2:return BulletLifetime.Mode(track)=="view"||track.despawnMargin!=0||track.fadeInFrames>0||BulletLifetime.Effect(track)!="none"||
                (BulletLifetime.Mode(track)=="fixed"&&(track.life!=160||Motion.HasKeys(track,"life")||track.expressions.ContainsKey("life")));
            case 3:return track.releaseFrame>=0;
            case 4:return track.bounceBricks||Mode(track)!="none";
            case 5:return ObjectDynamics.HasPhysics(track);
            case 6:return ObjectDynamics.HasAnimation(track);
            default:return false;
        }
    }

    public static string Mode(Track track){return track.boundaryMode=="reflect"||track.boundaryMode=="wrap"?track.boundaryMode:"none";}
    public static string BoundaryLabel(Track track){return BoundaryNames[Array.IndexOf(BoundaryModes,Mode(track))];}
    public static void SetBoundary(Track track,string label){track.boundaryMode=BoundaryModes[Array.IndexOf(BoundaryNames,label)];}
    public static string[] BoundaryChoices {get{return BoundaryNames;}}

    public static void Attach(Track track,int index,int at)
    {
        bool configured=Configured(track,index);
        track.objectProperties|=1<<index;
        track.hiddenObjectProperties&=~(1<<index);
        if(configured)return;
        if(index==0)Rainbow.Enable(track,true);
        if(index==1&&Trails.Mode(track)=="off")Trails.SetMode(track,"fade");
        if(index==2)track.lifetimeMode="room";
        if(index==3&&track.releaseFrame<0)track.releaseFrame=Math.Max(track.start,Math.Min(track.end,at));
        if(index==4)track.boundaryMode="reflect";
    }

    public static void Hide(Track track,int index)
    {
        track.objectProperties|=1<<index;
        track.hiddenObjectProperties|=1<<index;
    }
}

internal sealed partial class MainForm
{
    private async Task ApplyObjectProperty(string key,Track target,int at)
    {
        int index=Array.IndexOf(ObjectProperties.Keys,key);
        if(index<0||!ObjectProperties.Supports(target,key)){
            SetStatus(key=="scatter"?"请把散开飞行拖到整体对象上。":"请把属性拖到支持它的物件上。");
            return;
        }
        StopPlaying();CommitInspectorEdits();
        if(ObjectProperties.Shows(target,index)){RevealObjectProperty(target,index,false);return;}
        bool configured=ObjectProperties.Configured(target,index);
        if(!configured&&key=="rainbow"&&!document.inventory.sprites.Contains(String.IsNullOrEmpty(target.rainbowSprite)?Rainbow.Sprite:target.rainbowSprite)){
            if(bridge==null)throw new InvalidOperationException("工程缺少彩虹 Cherry 贴图。");
            var current=document;
            var assets=await bridge.Call<ImportResult>("ensure-rainbow",new{});
            if(document!=current||!state.tracks.Contains(target))return;
            document.inventory=assets.inventory;
            document.spriteMeta=assets.spriteMeta;
            sprites.Metadata=assets.spriteMeta;
        }
        PushUndo();
        if(!configured&&key=="rainbow"&&!document.inventory.sprites.Contains(target.rainbowSprite))target.rainbowSprite="";
        ObjectProperties.Attach(target,index,at);
        if(configured){QueueProjectSave();UpdateTitle();}else MarkChanged();
        RevealObjectProperty(target,index,true);
        SetStatus(Language.Text(configured?"已展开物件属性":"已添加物件属性")+" · "+Language.Text(ObjectProperties.Names[index]));
    }

    private Track ObjectPropertyTarget()
    {return timeline.SelectedTracks.Count==1?selectedTrack:null;}

    private void RevealObjectProperty(Track target,int index,bool changed)
    {
        if(ObjectPropertyTarget()!=target)SelectTrack(target,null);
        else if(changed)ShowPropertiesCore(false);
        inspectorTabs.SelectedIndex=0;
        inspectorDock.Activate("general");
        properties.OpenSection(ObjectProperties.Names[index]);
    }

    private void HideObjectProperty(Track target,int index)
    {
        if(target!=ObjectPropertyTarget()||!ObjectProperties.Supports(target,ObjectProperties.Keys[index])||!ObjectProperties.Shows(target,index))return;
        CommitInspectorEdits();
        PushUndo();
        ObjectProperties.Hide(target,index);
        QueueProjectSave();
        ShowPropertiesCore(false);
        UpdateTitle();
        SetStatus(Language.Text("已收回物件属性，效果仍生效")+" · "+Language.Text(ObjectProperties.Names[index]));
    }

    private void BoundaryFields(NativeProperties bag,Track track)
    {
        const string category=ObjectProperties.Boundaries;
        Field(bag,"碰到砖块反弹",category,()=>track.bounceBricks,value=>track.bounceBricks=value,null,"作用于飞行中的单颗弹幕。砖块使用房间内实心实例的边界框。");
        Field(bag,"房间边界",category,()=>ObjectProperties.BoundaryLabel(track),value=>ObjectProperties.SetBoundary(track,value),ObjectProperties.BoundaryChoices,"屏幕边界按整个房间计算。穿屏时从对边出现，保留方向和剩余位移。");
    }
}
