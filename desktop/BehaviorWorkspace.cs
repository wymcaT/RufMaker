using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

internal sealed partial class MainForm
{
    private readonly ListBox behaviorList=new ListBox{Dock=DockStyle.Fill,BackColor=Theme.Panel,ForeColor=Theme.Text,BorderStyle=BorderStyle.None,HorizontalScrollbar=true};
    private readonly ParameterInspector behaviorProperties=new ParameterInspector{Dock=DockStyle.Fill,Compact=true};
    private readonly Label behaviorSummary=new Label{Dock=DockStyle.Fill,ForeColor=Theme.Muted,Padding=new Padding(8),AutoEllipsis=true};
    private bool refreshingBehavior;private string behaviorOwner;private readonly List<bool> behaviorModes=new List<bool>();private readonly List<int> behaviorIndices=new List<int>();
    private ToolStripButton generatedBehaviorButton;
    private Control BuildBehaviorWorkspace()
    {
        var table=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Margin=Padding.Empty};table.RowStyles.Add(new RowStyle(SizeType.Absolute,Font.Height+26));table.RowStyles.Add(new RowStyle(SizeType.Absolute,Font.Height*4+12));table.RowStyles.Add(new RowStyle(SizeType.Absolute,Font.Height*3+16));table.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var tools=NewStrip("BehaviorTools");var add=new ToolStripDropDownButton("添加行为"){ForeColor=Theme.Text};add.DropDownItems.Add(Command("物件生成",()=>AddInlineBehavior(true,"spawn")));foreach(string type in BulletSystem.ActionTypes){string value=type;add.DropDownItems.Add(Command(BulletSystem.ActionName(value),()=>AddInlineBehavior(false,value)));}tools.Items.Add(add);tools.Items.Add(Tool("删除",RemoveInlineBehavior));tools.Items.Add(Tool("展开编辑",ExpandBehaviorEditor));generatedBehaviorButton=Tool("新弹幕行为…",OpenGeneratedBulletBehavior);generatedBehaviorButton.Visible=false;tools.Items.Add(generatedBehaviorButton);table.Controls.Add(tools,0,0);
        behaviorList.SelectedIndexChanged+=(s,e)=>{if(!refreshingBehavior)ShowInlineBehavior();};table.Controls.Add(behaviorList,0,1);table.Controls.Add(behaviorSummary,0,2);table.Controls.Add(behaviorProperties,0,3);behaviorProperties.BeginScrub=()=>{StopPlaying();PushUndo();parameterScrubbing=true;saveTimer.Stop();};behaviorProperties.EndScrub=()=>{parameterScrubbing=false;MarkChanged();RefreshBehavior();};return table;
    }
    private bool GetBehaviorSelection(out bool template,out int index){int row=behaviorList.SelectedIndex;template=row>=0&&row<behaviorModes.Count&&behaviorModes[row];index=row>=0&&row<behaviorIndices.Count?behaviorIndices[row]:-1;return selectedTrack!=null&&index>=0&&(template?selectedTrack.spawnActions:selectedTrack.bulletActions).Count>index;}
    private void ExpandBehaviorEditor()
    {bool template;int index;if(GetBehaviorSelection(out template,out index)){if(template)OpenTemplateBehavior(index);else OpenBulletBehavior(index);}else if(TemplateRuntime.Eligible(selectedTrack))OpenTemplateBehavior();}
    private void FocusBehavior(bool template,int index){RefreshBehavior();int row=-1;for(int i=0;i<behaviorIndices.Count;i++)if(behaviorModes[i]==template&&behaviorIndices[i]==index){row=i;break;}if(row>=0)behaviorList.SelectedIndex=row;inspectorTabs.SelectedIndex=1;ShowInlineBehavior();}
    private void AddInlineBehavior(bool template,string type)
    {
        if(selectedTrack==null||!TemplateRuntime.Eligible(selectedTrack)||!template&&!new[]{"emitter","rod"}.Contains(selectedTrack.kind)){SetStatus("请先选中可编辑的物件或弹幕来源。");return;}if(type=="spawn"&&!state.tracks.Any(t=>t!=selectedTrack&&TemplateRuntime.Eligible(t))){SetStatus("请先创建一个生成模板。");return;}properties.CommitPending();behaviorProperties.CommitPending();PushUndo();var action=new BulletAction{type=type,inheritScale=true,duration=new[]{"spawn","split","clear","aim","collision"}.Contains(type)?0:state.fps};
        if(type=="spawn"){var target=state.tracks.FirstOrDefault(t=>t!=selectedTrack&&TemplateRuntime.Eligible(t)&&t.templateOnly)??state.tracks.FirstOrDefault(t=>t!=selectedTrack&&TemplateRuntime.Eligible(t));action.templateId=target==null?"":target.id;}
        var actions=template?selectedTrack.spawnActions:selectedTrack.bulletActions;actions.Add(action);if(!template)selectedTrack.advancedBullets=true;MarkChanged();FocusBehavior(template,actions.Count-1);
    }
    private void RemoveInlineBehavior(){bool template;int index;if(!GetBehaviorSelection(out template,out index))return;behaviorProperties.CommitPending();PushUndo();(template?selectedTrack.spawnActions:selectedTrack.bulletActions).RemoveAt(index);MarkChanged();RefreshBehavior();}
    private void RefreshBehavior()
    {
        if(refreshingBehavior||behaviorList.Parent==null)return;refreshingBehavior=true;try{
            bool same=selectedTrack!=null&&behaviorOwner==selectedTrack.id;int selected=same?behaviorList.SelectedIndex:-1;var labels=new List<string>();behaviorModes.Clear();behaviorIndices.Clear();if(selectedTrack!=null){foreach(bool template in new[]{true,false}){var actions=template?selectedTrack.spawnActions:selectedTrack.bulletActions;for(int i=0;i<actions.Count;i++){var a=actions[i];labels.Add((a.enabled?"":"× ")+Language.Text(template?"生成":"年龄")+" · "+a.start+"f · "+Language.Text(String.IsNullOrEmpty(a.generationPreset)?BulletSystem.ActionName(a.type):GenerationPresets.Name(a.generationPreset))+(a.type=="spawn"&&a.spawnRepeat>0?" / "+a.spawnRepeat+"f":""));behaviorModes.Add(template);behaviorIndices.Add(i);}}}
            if(!behaviorList.Items.Cast<string>().SequenceEqual(labels)){behaviorList.BeginUpdate();behaviorList.Items.Clear();behaviorList.Items.AddRange(labels.Cast<object>().ToArray());behaviorList.EndUpdate();}behaviorOwner=selectedTrack==null?null:selectedTrack.id;if(behaviorList.Items.Count>0)behaviorList.SelectedIndex=Math.Max(0,Math.Min(behaviorList.Items.Count-1,selected));
        }finally{refreshingBehavior=false;}ShowInlineBehavior();
    }
    private void ShowInlineBehavior()
    {
        if(generatedBehaviorButton!=null)generatedBehaviorButton.Visible=false;
        if(refreshingBehavior)return;bool template;int index;var bag=new NativeProperties();if(!GetBehaviorSelection(out template,out index)){behaviorSummary.Text=Language.Text("选中物件后添加生成规则；弹幕来源还可设置出生后的年龄行为。");behaviorProperties.SelectedObject=bag;return;}
        var track=selectedTrack;var a=(template?track.spawnActions:track.bulletActions)[index];
        behaviorSummary.Text=Language.Text(template?"生成时间从来源物件开始计算；生成模板使用自己的局部时间。":"按每颗弹幕出生后的年龄执行。复杂筛选、公式和外观继承可在“展开编辑”中调整。");
        Action<Action> edit=change=>{PushUndo();change();if(!template)track.advancedBullets=true;MarkChanged();RefreshBehavior();};
        if(a.type=="spawn"&&!String.IsNullOrEmpty(a.generationPreset)){
            if(generatedBehaviorButton!=null)generatedBehaviorButton.Visible=state.tracks.Any(t=>t.id==a.templateId&&(t.kind=="emitter"||t.kind=="rod"));
            if(track.kind=="emitter")bag.Add("生成来源","循环生成",typeof(string),()=>template?"发射器本体":"发出的每颗弹幕",value=>SetGeneratorSource(track,a,(string)value=="发出的每颗弹幕"),new[]{"发射器本体","发出的每颗弹幕"});
            bag.Add("启用","循环生成",typeof(bool),()=>a.enabled,value=>edit(()=>a.enabled=(bool)value));
            bag.Add(a.generationPreset==CopyGeneration.Key?"复制间隔 / 帧（0仅一次）":"发射间隔 / 帧","循环生成",typeof(int),()=>a.spawnRepeat,value=>edit(()=>a.spawnRepeat=Math.Max(a.generationPreset==CopyGeneration.Key?0:1,(int)value)));
            GenerationPresets.Fields(bag,state,track,a,edit);
            bag.Add("首次发射延迟 / 帧","循环生成",typeof(int),()=>a.start,value=>edit(()=>a.start=Math.Max(0,(int)value)));
            bag.Add("继承出生贴图与颜色","生成外观",typeof(bool),()=>a.inheritAppearance,value=>edit(()=>a.inheritAppearance=(bool)value));
            behaviorSummary.Text=Language.Text("在来源生命周期内循环生成；复杂参数可点“展开编辑”。");behaviorProperties.SelectedObject=bag;return;
        }
        bag.Add("启用","规则",typeof(bool),()=>a.enabled,v=>edit(()=>a.enabled=(bool)v));bag.Add("种类","规则",typeof(string),()=>Language.Text(BulletSystem.ActionName(a.type)),null);
        bag.Add("开始年龄 / 帧","时间",typeof(int),()=>a.start,v=>edit(()=>a.start=Math.Max(0,(int)v)));bag.Add("持续帧数","时间",typeof(int),()=>a.duration,v=>edit(()=>a.duration=Math.Max(0,(int)v)));bag.Add("每颗延迟 / 帧","时间",typeof(int),()=>a.stagger,v=>edit(()=>a.stagger=Math.Max(0,(int)v)));
        if(template)bag.Add("首次触发歌曲帧","时间",typeof(int),()=>track.start+a.start,v=>edit(()=>a.start=Math.Max(0,(int)v-track.start)));
        if(a.type=="spawn"){
            var candidates=state.tracks.Where(TemplateRuntime.Eligible).ToArray();var names=new[]{Language.Text("请选择生成模板…")}.Concat(candidates.Select(t=>t.name+" · "+t.id.Substring(0,Math.Min(6,t.id.Length)))).ToArray();
            bag.Add("生成模板","生成",typeof(string),()=>names[Math.Max(0,Array.FindIndex(candidates,t=>t.id==a.templateId)+1)],v=>edit(()=>{int n=Array.IndexOf(names,(string)v)-1;a.templateId=n<0?"":candidates[n].id;}),names,localizeChoices:false);
            bag.Add("重复间隔 / 帧（0 仅一次）","生成",typeof(int),()=>a.spawnRepeat,v=>edit(()=>a.spawnRepeat=Math.Max(0,(int)v)));
            if(template&&track.kind=="rod")bag.Add("生成位置","生成",typeof(string),()=>a.spawnOrigin=="each"?"各颗弹幕":"整体中心",v=>edit(()=>a.spawnOrigin=(string)v=="各颗弹幕"?"each":"center"),new[]{"整体中心","各颗弹幕"});
            bag.Add("生成后销毁母体","生成",typeof(bool),()=>a.destroyParent,v=>edit(()=>a.destroyParent=(bool)v));bag.Add("继承出生贴图与颜色","生成外观",typeof(bool),()=>a.inheritAppearance,v=>edit(()=>a.inheritAppearance=(bool)v));bag.Add("继承母体大小","生成外观",typeof(bool),()=>a.inheritScale,v=>edit(()=>a.inheritScale=(bool)v));
        }else if(!new[]{"clear","split"}.Contains(a.type)){
            bag.Add("起始值","参数",typeof(double),()=>a.from,v=>edit(()=>a.from=(double)v));bag.Add("结束值","参数",typeof(double),()=>a.to,v=>edit(()=>a.to=(double)v));bag.Add("缓动","参数",typeof(string),()=>Motion.EaseNames[Math.Max(0,Array.IndexOf(Motion.EaseValues,a.easing))],v=>edit(()=>a.easing=Motion.EaseValues[Array.IndexOf(Motion.EaseNames,(string)v)]),Motion.EaseNames);
        }
        GenerationPresets.Fields(bag,state,track,a,edit);
        behaviorProperties.SelectedObject=bag;
    }
}
