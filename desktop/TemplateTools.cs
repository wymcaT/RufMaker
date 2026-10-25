using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;

internal sealed partial class MainForm
{
    private void OpenTemplateBehavior(){OpenTemplateBehavior(-1);}
    private void OpenTemplateBehavior(int actionIndex)
    {
        if(!TemplateRuntime.Eligible(selectedTrack)){SetStatus("请先选中物件、整体对象、发射器或分组。");return;}
        StopPlaying();properties.CommitPending();var track=selectedTrack;
        using(var dialog=new BulletBehaviorForm(track,state,true,actionIndex))if(Language.ShowDialog(dialog,this)==DialogResult.OK){PushUndo();track.spawnActions=dialog.Actions;MarkChanged();timeline.RefreshLayout();ShowProperties();SetStatus("已应用物件生成行为；模板从自己的起点独立运行。");}
    }
    private void TemplateFields(NativeProperties bag,Track track)
    {
        if(!TemplateRuntime.Eligible(track))return;
        Field(bag,"仅用作生成模板","生成模板",()=>track.templateOnly,value=>track.templateOnly=value,null,"开启后不在歌曲时间轴自行出现，仍可被其他物件生成。模板的关键帧与运动保留。");
        bag.Add("物件生成行为","生成模板",typeof(string),()=>track.spawnActions.Count+" 项 · 右键“物件生成行为…”编辑",null);
    }
    private Track[] PresetDependencies(IEnumerable<Track> selected)
    {
        var original=new HashSet<Track>(WithGroupChildren(selected));var result=new HashSet<Track>(original);bool added;
        do{added=false;foreach(var track in result.ToArray())foreach(var action in TemplateRuntime.Actions(track).Where(a=>a.type=="spawn"||a.type=="split")){
            var target=state.tracks.FirstOrDefault(t=>t.id==action.templateId);if(target!=null)foreach(var dependency in WithGroupChildren(new[]{target}))added|=result.Add(dependency);
        }}while(added);
        return state.tracks.Where(result.Contains).Select(t=>{var copy=t.Snapshot();if(!original.Contains(t)&&!result.Any(p=>!original.Contains(p)&&p.id==t.parentId))copy.templateOnly=true;return copy;}).ToArray();
    }
}
