using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

internal sealed partial class MainForm
{
    private readonly TimelineCanvas keyTimeline=new TimelineCanvas{InspectorKeyframes=true,Dock=DockStyle.Fill};
    private readonly Panel keyWorkspace=new Panel{Dock=DockStyle.Fill,BackColor=Theme.Panel};
    private readonly SplitContainer keySplit=new SplitContainer{Dock=DockStyle.Fill,Orientation=Orientation.Horizontal,BackColor=Theme.Line,SplitterWidth=7,FixedPanel=FixedPanel.Panel1,Size=new Size(340,450)};
    private ToolStrip keyframeTools;private bool layingOutKeys,resizingKeys;private int keyWorkspaceHeight=220;
    private Button keyWorkspaceToggle;
    private bool keyWorkspaceOpen=true;
    private Track keyWorkspaceTrack;
    private int keyWorkspaceStart,keyWorkspaceEnd;

    private Control BuildKeyframeProperties()
    {
        var page=new Panel{Dock=DockStyle.Fill,BackColor=Theme.Panel};
        var tools=keyframeTools=NewStrip("KeyframeTools");tools.Dock=DockStyle.Top;
        keyWorkspaceToggle=Button("▾ 关键帧",()=>{keyWorkspaceOpen=!keyWorkspaceOpen;LayoutKeyWorkspace();},false,75);
        keyWorkspaceToggle.AutoSize=false;keyWorkspaceToggle.MinimumSize=Size.Empty;keyWorkspaceToggle.Size=new Size(110,Font.Height+12);tools.Items.Add(Host(keyWorkspaceToggle,110));
        Language.PreserveText(keyWorkspaceToggle);
        tools.Items.Add(Tool("◀",()=>StepPropertyKey(-1)));tools.Items.Add(Tool("◆",AddKey));tools.Items.Add(Tool("▶",()=>StepPropertyKey(1)));
        tools.Items.Add(Tool("−",()=>{if(timeline.SelectedKeys.Count>0)RemoveSelection();}));tools.Items.Add(Tool("适应",()=>keyTimeline.FitSelection()));
        Language.ToolTip(tips,keyWorkspaceToggle,"展开或收起右栏关键帧编辑区");
        keyTimeline.Seek=value=>{StopPlaying();SetFrame(value);};
        keyTimeline.SnapCursor=value=>SnapCursor(value,state==null?1:Math.Max(1,8.0*state.fps/keyTimeline.PixelsPerSecond));
        keyTimeline.SelectTrack=(track,key)=>{timeline.SelectedProperty=keyTimeline.SelectedProperty;timeline.SetSelection(keyTimeline.SelectedKeys.ToArray(),track,key);};
        keyTimeline.BeginEdit=PushUndo;keyTimeline.Changed=()=>{MarkChanged();properties.Refresh();};
        keyTimeline.PreviewChanged=()=>{stage.SceneChanged();inspectorTimer.Stop();inspectorTimer.Start();};
        keyTimeline.InsertKey=(track,property,at)=>{timeline.SelectedProperty=property;SelectTrack(track,null);SetFrame(Math.Max(track.start,Math.Min(track.end,at)));AddKey();};
        keyTimeline.ContextMenuStrip=CreateContext(false);
        keyWorkspace.Controls.Add(keyTimeline);keySplit.Panel1.Controls.Add(keyWorkspace);keySplit.Panel2.Controls.Add(properties);page.Controls.Add(keySplit);page.Controls.Add(tools);
        keyWorkspaceOpen=restoredView==null||restoredView.keyframesOpen;
        keyWorkspaceHeight=restoredView==null?220:Math.Max(60,restoredView.keyframesHeight);
        keySplit.Resize+=(s,e)=>LayoutKeyWorkspace();keySplit.SplitterMoving+=(s,e)=>resizingKeys=true;
        keySplit.SplitterMoved+=(s,e)=>{if(resizingKeys&&!layingOutKeys)keyWorkspaceHeight=keySplit.SplitterDistance;resizingKeys=false;};
        Language.ToolTip(tips,keySplit,"上下拖动分隔条，调整关键帧区与参数区的高度");
        LayoutKeyWorkspace();return page;
    }
    private void LayoutKeyWorkspace()
    {
        if(layingOutKeys||resizingKeys||keyframeTools==null)return;layingOutKeys=true;
        try{
        bool available=keyWorkspaceTrack!=null&&Motion.Fields(keyWorkspaceTrack).Length>0;keyframeTools.Visible=available;
        keySplit.Panel1Collapsed=!available||!keyWorkspaceOpen;keyWorkspace.Visible=available&&keyWorkspaceOpen;
        if(!keySplit.Panel1Collapsed&&keySplit.ClientSize.Height>keySplit.SplitterWidth){int total=keySplit.ClientSize.Height-keySplit.SplitterWidth,minimum=Math.Min(total,Math.Max(80,keyTimeline.Font.Height*2+32)),parameters=Math.Min(Math.Max(30,Font.Height*3),Math.Max(0,total-minimum));
            keySplit.Panel1MinSize=0;keySplit.Panel2MinSize=0;keySplit.SplitterDistance=Math.Max(minimum,Math.Min(Math.Max(minimum,total-parameters),keyWorkspaceHeight));keySplit.Panel1MinSize=minimum;keySplit.Panel2MinSize=parameters;
        }
        if(keyWorkspaceToggle!=null)keyWorkspaceToggle.Text=(keyWorkspaceOpen?"▾ ":"▸ ")+Language.Text("关键帧");
        int labelWidth=Math.Max(90,TextRenderer.MeasureText(keyWorkspaceToggle.Text,keyWorkspaceToggle.Font).Width+24);
        foreach(ToolStripItem item in keyframeTools.Items)if(item is ToolStripControlHost&&((ToolStripControlHost)item).Control==keyWorkspaceToggle){item.Width=labelWidth;keyWorkspaceToggle.Width=labelWidth;}
        }finally{layingOutKeys=false;}
    }
    private void RefreshKeyWorkspace()
    {
        if(keyWorkspaceToggle==null)return;
        var track=selectedTrack!=null&&selectedTrack.kind!="native"&&timeline.SelectedTracks.Count==1?selectedTrack:null;
        bool fit=keyWorkspaceTrack!=track||track!=null&&(keyWorkspaceStart!=track.start||keyWorkspaceEnd!=track.end);
        keyWorkspaceTrack=track;keyWorkspace.Visible=track!=null&&Motion.Fields(track).Length>0;
        keyTimeline.State=state;keyTimeline.SelectedTrack=track;keyTimeline.SelectedTracks.Clear();if(track!=null)keyTimeline.SelectedTracks.Add(track);
        keyTimeline.SelectedKeys.Clear();if(track!=null)keyTimeline.SelectedKeys.UnionWith(timeline.SelectedKeys.Where(track.keyframes.Contains));
        keyTimeline.SelectedKey=selectedKey;keyTimeline.SelectedProperty=timeline.SelectedProperty;keyTimeline.Frame=frame;
        if(track!=null){keyWorkspaceStart=track.start;keyWorkspaceEnd=track.end;}
        keyTimeline.RefreshLayout();if(fit)keyTimeline.FitSelection();LayoutKeyWorkspace();
    }
    private void OpenKeyWorkspace(Track track)
    {
        if(track==null||track.kind=="native")return;
        if(selectedTrack!=track)SelectTrack(track,null);
        keyWorkspaceOpen=true;inspectorTabs.SelectedIndex=0;RefreshKeyWorkspace();keyTimeline.Focus();
    }
    private void StepPropertyKey(int direction)
    {
        if(selectedTrack==null)return;var keys=selectedTrack.keyframes.Where(k=>k.property==timeline.SelectedProperty&&k.frame>=selectedTrack.start&&k.frame<=selectedTrack.end);
        var key=direction<0?keys.Where(k=>k.frame<frame).OrderByDescending(k=>k.frame).FirstOrDefault():keys.Where(k=>k.frame>frame).OrderBy(k=>k.frame).FirstOrDefault();
        if(key==null)return;timeline.SetSelection(new[]{key},selectedTrack,key);StopPlaying();SetFrame(key.frame);keyTimeline.EnsureFrameVisible();
    }
    private void SelectedKeyFields(NativeProperties bag,Track track)
    {
        var key=selectedKey;
        bag.Add("属性","所选关键帧",typeof(string),()=>Motion.Name(key.property),null);
        if(timeline.SelectedKeys.Count>1)bag.Add("选中数量","所选关键帧",typeof(int),()=>timeline.SelectedKeys.Count,null);
        Field(bag,timeline.SelectedKeys.Count>1?"整组移动 · 主帧":"帧","所选关键帧",()=>key.frame,value=>{timeline.MoveSelectedTo(Math.Max(0,Math.Min(state.duration,value)));SetFrame(key.frame);});
        bag.Add("数值","所选关键帧",typeof(double),()=>key.value,value=>{PushUndo();key.expression="";key.value=ClampValue(key.property,(double)value);MarkChanged();},null,"",ParameterControls(new[]{track},key.property,false,key));EaseField(bag,track,key);
    }
}
