using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

internal sealed partial class MainForm
{
    private readonly InspectorDockWorkspace inspectorDock=new InspectorDockWorkspace();
    private readonly TimelineCanvas keyTimeline=new TimelineCanvas{InspectorKeyframes=true,Dock=DockStyle.Fill};
    private readonly Panel keyWorkspace=new Panel{Dock=DockStyle.Fill,BackColor=Theme.Panel};
    private readonly SplitContainer keySplit=new SplitContainer{Dock=DockStyle.Fill,Orientation=Orientation.Horizontal,BackColor=Theme.Line,SplitterWidth=7,Size=new Size(340,450)};
    private readonly ParameterInspector keyProperties=new ParameterInspector{Dock=DockStyle.Fill,Compact=true};
    private readonly Label keyEmpty=new Label{Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Theme.Muted,Text="选择单个物件以编辑关键帧"};
    private ToolStrip keyframeTools;private bool layingOutKeys,resizingKeys,refreshingKeyDetails;private int keyWorkspaceHeight=220;
    private double keyDetailsRatio=.65;
    private bool keyWorkspaceOpen=true;
    private Track keyWorkspaceTrack;
    private int keyWorkspaceStart,keyWorkspaceEnd;

    private Control BuildKeyframeProperties()
    {
        var page=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,BackColor=Theme.Panel,Margin=Padding.Empty};
        var tools=keyframeTools=NewStrip("KeyframeTools");page.RowStyles.Add(new RowStyle(SizeType.AutoSize));page.RowStyles.Add(new RowStyle(SizeType.Absolute,tools.Height));page.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        tools.Items.Add(Tool("◀",()=>StepPropertyKey(-1)));tools.Items.Add(Tool("◆",AddKey));tools.Items.Add(Tool("▶",()=>StepPropertyKey(1)));
        tools.Items.Add(Tool("−",()=>{if(timeline.SelectedKeys.Count>0)RemoveSelection();}));tools.Items.Add(Tool("适应",()=>keyTimeline.FitSelection()));
        keyTimeline.Seek=value=>{StopPlaying();SetFrame(value);};
        keyTimeline.SnapCursor=value=>SnapCursor(value,state==null?1:Math.Max(1,8.0*state.fps/keyTimeline.PixelsPerSecond));keyTimeline.PrepareCursorTargets=()=>CaptureCursorTargets(state==null?1:Math.Max(1,8.0*state.fps/keyTimeline.PixelsPerSecond));keyTimeline.CursorGuideChanged=timeline.ShowSnapGuide;
        keyTimeline.SelectTrack=(track,key)=>{timeline.SelectedProperty=keyTimeline.SelectedProperty;timeline.SetSelection(keyTimeline.SelectedKeys.ToArray(),track,key);};
        keyTimeline.BeginEdit=PushUndo;keyTimeline.Changed=()=>{MarkChanged();keyProperties.Refresh();};
        keyTimeline.PreviewChanged=()=>{stage.SceneChanged();inspectorTimer.Stop();inspectorTimer.Start();};
        keyTimeline.InsertKey=(track,property,at)=>{timeline.SelectedProperty=property;SelectTrack(track,null);SetFrame(Math.Max(track.start,Math.Min(track.end,at)));AddKey();};
        keyTimeline.ContextMenuStrip=CreateContext(false);keyProperties.ContextMenuStrip=CreateContext(true);keyProperties.HelpVisible=false;
        keyProperties.BeginScrub=properties.BeginScrub;keyProperties.EndScrub=properties.EndScrub;
        keyWorkspace.Controls.Add(keyTimeline);keySplit.Panel1.Controls.Add(keyWorkspace);keySplit.Panel1.Controls.Add(keyEmpty);keySplit.Panel2.Controls.Add(keyProperties);page.Controls.Add(rightTime,0,0);page.Controls.Add(tools,0,1);page.Controls.Add(keySplit,0,2);
        keyDetailsRatio=restoredView!=null&&restoredView.inspectorDock!=null?Math.Max(.05,Math.Min(.95,restoredView.inspectorDock.details)):.65;
        keySplit.Resize+=(s,e)=>LayoutKeyWorkspace();keySplit.SplitterMoving+=(s,e)=>resizingKeys=true;
        keySplit.SplitterMoved+=(s,e)=>{if(resizingKeys&&!layingOutKeys){keyWorkspaceHeight=keySplit.SplitterDistance;int total=keySplit.ClientSize.Height-keySplit.SplitterWidth;if(total>0)keyDetailsRatio=keySplit.SplitterDistance/(double)total;}resizingKeys=false;};
        Language.ToolTip(tips,keySplit,"上下拖动分隔条，调整关键帧列表与详情的高度");
        LayoutKeyWorkspace();return page;
    }
    private void LayoutKeyWorkspace()
    {
        if(layingOutKeys||resizingKeys||keyframeTools==null)return;layingOutKeys=true;
        try{
            bool available=keyWorkspaceTrack!=null&&Motion.Fields(keyWorkspaceTrack).Length>0;keyframeTools.Enabled=available;keyWorkspace.Visible=available;keyEmpty.Visible=!available;
            if(keySplit.ClientSize.Height>keySplit.SplitterWidth){int total=keySplit.ClientSize.Height-keySplit.SplitterWidth,minimum=Math.Min(total/2,70);
                keySplit.Panel1MinSize=0;keySplit.Panel2MinSize=0;keySplit.SplitterDistance=Math.Max(minimum,Math.Min(total-minimum,(int)Math.Round(total*keyDetailsRatio)));keySplit.Panel1MinSize=minimum;keySplit.Panel2MinSize=minimum;
            }
        }finally{layingOutKeys=false;}
    }
    private void RefreshKeyWorkspace()
    {
        if(keyframeTools==null)return;
        var track=selectedTrack!=null&&selectedTrack.kind!="native"&&timeline.SelectedTracks.Count==1?selectedTrack:null;
        bool fit=keyWorkspaceTrack!=track||track!=null&&(keyWorkspaceStart!=track.start||keyWorkspaceEnd!=track.end);
        keyWorkspaceTrack=track;
        keyTimeline.State=state;keyTimeline.SelectedTrack=track;keyTimeline.SelectedTracks.Clear();if(track!=null)keyTimeline.SelectedTracks.Add(track);
        keyTimeline.SelectedKeys.Clear();if(track!=null)keyTimeline.SelectedKeys.UnionWith(timeline.SelectedKeys.Where(track.keyframes.Contains));
        keyTimeline.SelectedKey=selectedKey;keyTimeline.SelectedProperty=timeline.SelectedProperty;keyTimeline.Frame=frame;
        if(track!=null){keyWorkspaceStart=track.start;keyWorkspaceEnd=track.end;}
        keyTimeline.RefreshLayout();if(fit)keyTimeline.FitSelection();LayoutKeyWorkspace();
        if(refreshingKeyDetails)return;refreshingKeyDetails=true;try{var bag=new NativeProperties();if(track!=null&&selectedKey!=null&&track.keyframes.Contains(selectedKey)&&!MotionPresets.Generated(track,selectedKey.property))SelectedKeyFields(bag,track);else bag.Add("操作","关键帧",typeof(string),()=>track==null?"选择单个物件以编辑关键帧":"在关键帧列表中选中一帧，在此修改帧数、数值与缓动。",null);keyProperties.SelectedObject=bag;}finally{refreshingKeyDetails=false;}
    }
    private void OpenKeyWorkspace(Track track)
    {
        if(track==null||track.kind=="native")return;
        if(selectedTrack!=track)SelectTrack(track,null);
        inspectorDock.Activate("keys");RefreshKeyWorkspace();keyTimeline.Focus();
    }
    private InspectorDockView CaptureInspectorDock(){var view=inspectorDock.CaptureView();view.details=keyDetailsRatio;return view;}
    private void CommitInspectorEdits(){properties.CommitPending();keyProperties.CommitPending();}
    private void StepPropertyKey(int direction)
    {
        if(selectedTrack==null)return;var keys=selectedTrack.keyframes.Where(k=>k.property==timeline.SelectedProperty&&k.frame>=selectedTrack.start&&k.frame<=selectedTrack.end);
        var key=direction<0?keys.Where(k=>k.frame<frame).OrderByDescending(k=>k.frame).FirstOrDefault():keys.Where(k=>k.frame>frame).OrderBy(k=>k.frame).FirstOrDefault();
        if(key==null)return;timeline.SetSelection(new[]{key},selectedTrack,key);StopPlaying();SetFrame(key.frame);keyTimeline.EnsureFrameVisible();
    }
    private void SelectedKeyFields(NativeProperties bag,Track track)
    {
        var key=selectedKey;
        bag.Add("属性","所选关键帧",typeof(string),()=>Language.Text(Motion.Name(key.property)),null);
        if(timeline.SelectedKeys.Count>1)bag.Add("选中数量","所选关键帧",typeof(int),()=>timeline.SelectedKeys.Count,null);
        Field(bag,timeline.SelectedKeys.Count>1?"整组移动 · 主帧":"帧","所选关键帧",()=>key.frame,value=>{timeline.MoveSelectedTo(Math.Max(0,Math.Min(state.duration,value)));SetFrame(key.frame);});
        bag.Add("数值","所选关键帧",typeof(double),()=>key.value,value=>{PushUndo();key.expression="";key.value=ClampValue(key.property,(double)value);MarkChanged();},null,"",ParameterControls(new[]{track},key.property,false,key));EaseField(bag,track,key);
    }
}
