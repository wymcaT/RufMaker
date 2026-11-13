using System;
using System.Collections.Generic;
using System.Linq;

// Reports the existing renderer/recorder contract. This is not a GML interpreter.
internal sealed class NativePreviewRequirements
{
    public readonly List<string> nativeIds=new List<string>(),unexecuted=new List<string>();
    public int live;public bool nativeProject,playerMoves;public int nativeDuration;
    public bool HasNative {get{return nativeProject||nativeIds.Count>0||unexecuted.Count>0;}}
}
internal sealed class NativePreviewCoverageReport
{
    public string cacheState="unused";public int live,cached,missing;public bool referenceMismatch;
    public readonly List<string> details=new List<string>();
    public bool Warning {get{return missing>0||referenceMismatch;}}
    public string Title {get{return missing>0?"预览不完整":referenceMismatch?"底片与玩家参考不同":cached>0?"缓存参考 + 实时叠加":"实时预览";}}
    public string Description {get{return String.Format(Language.Text("实时计算：{0} 项；缓存参考：{1} 项；未覆盖：{2} 项。"),live,cached,missing)+"\r\n"+String.Join("\r\n",details);}}
}
internal static class NativePreviewCoverage
{
    public static NativePreviewRequirements Collect(EditorState state)
    {
        var result=new NativePreviewRequirements();if(state==null)return result;
        result.nativeProject=state.nativeProject!=null;
        result.nativeDuration=state.nativeProject==null?state.duration:Math.Min(state.duration,state.nativeProject.duration);
        // Expansion uses the same IDs as the native schedule and manifest. Never mutate the document.
        var expanded=BulletSystem.Expand(state.Snapshot());
        foreach(var t in expanded.tracks){
            int start=Math.Max(0,t.start),end=Math.Min(state.duration,t.end);bool enabled=true;
            for(var p=t;p!=null;p=p.parent){if(!p.enabled||p.templateOnly){enabled=false;break;}start=Math.Max(start,p.start);end=Math.Min(end,p.end);}
            if(!enabled||start>end)continue;
            string label=String.IsNullOrEmpty(t.name)?t.id:t.name+" ["+t.id+"]";
            if(t.kind=="native-instance")result.nativeIds.Add(t.id);
            else if(t.kind=="script"){
                // The product emits scripts only at clockStart; a clipped-away trigger never runs.
                int trigger=TimelineLayout.ClockStart(t);
                if(trigger>=start&&trigger<=end&&!String.IsNullOrWhiteSpace(t.code))result.unexecuted.Add(label);
            }else if(new[]{"object","emitter","rod","effect","camera","control","variable"}.Contains(t.kind)){
                if(t.eventPreset=="player-set"){result.unexecuted.Add(label);continue;}
                result.live++;
                if(t.eventPreset=="player-move")result.playerMoves=true;
                if(t.kind=="control"&&!String.IsNullOrEmpty(t.eventPreset)&&!String.IsNullOrWhiteSpace(t.eventNativeObjects))result.unexecuted.Add(label);
            }
        }
        return result;
    }
    public static string CacheState(NativeFilmInfo info,bool displayed,string signature,bool changed,EditorState state,int duration)
    {
        if(info==null)return "missing";
        if(changed)return "stale";
        if(!displayed)return "disabled";
        if(info.room!=state.room||info.fps!=50||info.frameCount<=Math.Ceiling(duration*50.0/Math.Max(1,state.fps)))return "incompatible";
        if(String.IsNullOrEmpty(signature)||String.IsNullOrEmpty(info.sourceSignature)||info.coverageVersion!=1||info.playerReference!="fixed")return "unverified";
        return signature==info.sourceSignature?"matching":"stale";
    }
    public static NativePreviewCoverageReport Evaluate(NativePreviewRequirements required,EditorState state,NativeFilmInfo info,bool displayed,string signature,bool changed)
    {
        var result=new NativePreviewCoverageReport{live=required.live};if(state==null)return result;
        int duration=required.nativeIds.Count>0?state.duration:required.nativeDuration;
        result.cacheState=CacheState(info,displayed,signature,changed,state,duration);
        var ids=new HashSet<string>(info==null?new string[0]:info.nativeTrackIds??new string[0]);
        bool matching=result.cacheState=="matching";
        if(required.nativeProject){if(matching)result.cached++;else result.missing++;}
        foreach(string id in required.nativeIds){if(matching&&ids.Contains(id))result.cached++;else{result.missing++;result.details.Add(Language.Text("原生对象尚未覆盖：")+id);}}
        foreach(string label in required.unexecuted){result.missing++;result.details.Add(Language.Text("时间轴 GML / 原生目标未执行：")+label);}
        if(required.nativeProject||required.nativeIds.Count>0){
            string message=result.cacheState=="missing"?"原生底片缺失；静态贴图只是编辑占位。":result.cacheState=="disabled"?"原生底片未启用；静态贴图只是编辑占位。":result.cacheState=="stale"?"原生底片已过期；请重新生成。":result.cacheState=="incompatible"?"底片房间、帧率或时长不匹配；请重新生成。":result.cacheState=="unverified"?"底片来源尚未验证；已有画面不能证明当前工程已执行。":"原生 GML 来自 GM 录制缓存，不是 C# 实时执行。";
            result.details.Insert(0,Language.Text(message));
            result.details.Add(Language.Text("来源校验在打开、播放或管理底片时进行；外部文件未持续监视，媒体和插件按大小与修改时间校验。"));
        }
        if(displayed&&info!=null){
            result.details.Add(String.Format(Language.Text("底片玩家参考：固定 ({0},{1})；不会跟随当前试玩轨迹。"),info.playerX,info.playerY));
            var position=state.nativeTest??new NativeTestPosition();
            result.referenceMismatch=position.x!=info.playerX||position.y!=info.playerY||state.usePlayerTrace&&state.playerTrace!=null&&state.playerTrace.Count>0||required.playerMoves;
            if(result.referenceMismatch)result.details.Add(Language.Text("当前玩家位置、轨迹或传送事件与固定底片参考不同；重新生成仍使用固定参考。"));
        }
        if(required.unexecuted.Count>0)result.details.Add(Language.Text("重新生成底片不会执行这些时间轴 GML；请用 F5 验证真实游戏。"));
        return result;
    }
}
