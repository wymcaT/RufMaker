using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

internal sealed class TimelineGroup
{
    public string id="",name="",color="#5887B8";public List<int> lanes=new List<int>();
    public TimelineGroup Snapshot(){return new TimelineGroup{id=id,name=name,color=color,lanes=new List<int>(lanes)};}
}
internal static class TimelineGroups
{
    public static readonly string[] Palette={"#5887B8","#9B73B0","#4D9D89","#BA8459","#A16B82","#8A9957"};
    public static void Normalize(EditorState s)
    {
        s.timelineGroups=s.timelineGroups??new List<TimelineGroup>();var used=new HashSet<int>();
        foreach(var g in s.timelineGroups){if(String.IsNullOrEmpty(g.id))g.id=Guid.NewGuid().ToString("N");if(String.IsNullOrWhiteSpace(g.name))g.name="组";g.lanes=(g.lanes??new List<int>()).Where(n=>n>=0&&n<Int32.MaxValue&&used.Add(n)).Distinct().ToList();}
    }
    public static TimelineGroup At(EditorState s,int lane){return s==null?null:s.timelineGroups.FirstOrDefault(g=>g.lanes.Contains(lane));}
    public static string Id(Track t){return t==null?"":t.scopeGroup??(At(t.owner,t.lane)==null?"":At(t.owner,t.lane).id);}
    public static void Assign(EditorState s,IEnumerable<int> lanes,TimelineGroup group)
    {var set=new HashSet<int>(lanes.Where(n=>n>=0&&n<Int32.MaxValue));foreach(var g in s.timelineGroups)g.lanes.RemoveAll(set.Contains);if(group!=null)group.lanes.AddRange(set);}
    public static TimelineGroup Create(EditorState s,IEnumerable<int> lanes,string name)
    {var g=new TimelineGroup{id=Guid.NewGuid().ToString("N"),name=name,color=Palette[s.timelineGroups.Count%Palette.Length]};s.timelineGroups.Add(g);Assign(s,lanes,g);return g;}
    public static void RemoveLane(EditorState s,int lane){foreach(var g in s.timelineGroups)g.lanes=g.lanes.Where(n=>n!=lane).Select(n=>n>lane?n-1:n).ToList();}
    public static Color Background(TimelineGroup g,bool selected)
    {var c=Motion.ColorOf(g.color);double a=selected?.40:.23;return Color.FromArgb((int)(Theme.Panel.R*(1-a)+c.R*a),(int)(Theme.Panel.G*(1-a)+c.G*a),(int)(Theme.Panel.B*(1-a)+c.B*a));}
}
internal sealed partial class MainForm
{
    private int[] ContextGroupLanes(){return timeline.SelectedTracks.Count>0?timeline.SelectedTracks.Select(t=>t.lane).Distinct().ToArray():new[]{timeline.ActiveLane};}
    private string AskGroupName(string value)
    {
        using(var dialog=new Form{Text=Language.Text("组名称"),BackColor=Theme.Panel,ForeColor=Theme.Text,Font=Theme.Font,Size=new Size(420,160),FormBorderStyle=FormBorderStyle.FixedDialog,StartPosition=FormStartPosition.CenterParent,ShowInTaskbar=false,MinimizeBox=false,MaximizeBox=false}){
            var input=new TextBox{Text=value,Dock=DockStyle.Top,BackColor=Theme.Raised,ForeColor=Theme.Text};var buttons=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=42,FlowDirection=FlowDirection.RightToLeft};var ok=new Button{Text=Language.Text("确定"),DialogResult=DialogResult.OK,AutoSize=true,BackColor=Theme.Raised,ForeColor=Theme.Text,FlatStyle=FlatStyle.Flat,UseVisualStyleBackColor=false};var cancel=new Button{Text=Language.Text("取消"),DialogResult=DialogResult.Cancel,AutoSize=true,BackColor=Theme.Raised,ForeColor=Theme.Text,FlatStyle=FlatStyle.Flat,UseVisualStyleBackColor=false};buttons.Controls.Add(ok);buttons.Controls.Add(cancel);dialog.Padding=new Padding(12);dialog.Controls.Add(input);dialog.Controls.Add(buttons);dialog.AcceptButton=ok;dialog.CancelButton=cancel;return Language.ShowDialog(dialog,this)==DialogResult.OK&&!String.IsNullOrWhiteSpace(input.Text)?input.Text.Trim():null;}
    }
    private void SetLaneGroup(TimelineGroup group){PushUndo();TimelineGroups.Assign(state,ContextGroupLanes(),group);MarkChanged();ShowProperties();}
    private void CreateLaneGroup(){string name=AskGroupName(Language.Text("组")+" "+(state.timelineGroups.Count+1));if(name==null)return;PushUndo();TimelineGroups.Create(state,ContextGroupLanes(),name);MarkChanged();ShowProperties();}
    private void LaneGroupMenu(ToolStripItemCollection items)
    {
        LaneFoldingMenu(items);if(timeline.ActiveLane<0&&timeline.SelectedTracks.Count==0)return;var current=TimelineGroups.At(state,timeline.ActiveLane>=0?timeline.ActiveLane:selectedTrack.lane);var menu=new ToolStripMenuItem("轨道归属组");
        menu.DropDownItems.Add(new ToolStripMenuItem("不分组",null,(s,e)=>SetLaneGroup(null)){Checked=current==null});foreach(var g in state.timelineGroups){var target=g;var choice=new ToolStripMenuItem(g.name,null,(s,e)=>SetLaneGroup(target)){Checked=g==current,ForeColor=Motion.ColorOf(g.color)};Language.PreserveText(choice);menu.DropDownItems.Add(choice);}menu.DropDownItems.Add(new ToolStripSeparator());menu.DropDownItems.Add("新建组并加入…",null,(s,e)=>CreateLaneGroup());
        if(current!=null){menu.DropDownItems.Add("选择组内所有物件",null,(s,e)=>timeline.SelectLaneGroup(current));menu.DropDownItems.Add("重命名组…",null,(s,e)=>{string name=AskGroupName(current.name);if(name==null)return;PushUndo();current.name=name;MarkChanged();});menu.DropDownItems.Add("设置组底色…",null,(s,e)=>{using(var picker=new ColorDialog{Color=Motion.ColorOf(current.color),FullOpen=true})if(picker.ShowDialog(this)==DialogResult.OK){PushUndo();current.color=ColorTranslator.ToHtml(picker.Color);MarkChanged();}});menu.DropDownItems.Add("解散组（保留物件）",null,(s,e)=>{PushUndo();state.timelineGroups.Remove(current);MarkChanged();ShowProperties();});}items.Add(menu);
    }
    private void CompactTrackContext(ContextMenuStrip menu)
    {
        if(timeline.SummaryGroup!=null){LaneFoldingMenu(menu.Items);Language.Attach(menu);return;}
        var t=selectedTrack;
        if(timeline.SelectedKeys.Count>0){SelectedKeyContext(menu.Items);menu.Items.Add(new ToolStripSeparator());}
        if(t!=null){
            menu.Items.Add("来源与事件信息…",null,(s,e)=>ShowTimelineRelations());
            menu.Items.Add("编辑属性",null,(s,e)=>{inspectorTabs.SelectedIndex=0;inspectorDock.Activate("general");});
            if(t.kind=="native-instance")menu.Items.Add("编辑实例初始化 GML",null,(s,e)=>EditNativeObjectInit(t));
            else if(Motion.Fields(t).Length>0)menu.Items.Add("在右栏编辑关键帧",null,(s,e)=>OpenKeyWorkspace(t));
            menu.Items.Add(timeline.SelectedKeys.Count>0?"复制物件":"复制    Ctrl+C",null,(s,e)=>{timeline.ClearKeySelection();CopySelection();});
            var paste=menu.Items.Add("粘贴    Ctrl+V",null,async(s,e)=>await PasteSelection());paste.Enabled=copiedKind!=null;
            menu.Items.Add(timeline.SelectedKeys.Count>0?"删除所选物件":"删除所选物件    Delete",null,(s,e)=>{selectedKey=null;timeline.ClearKeySelection();RemoveSelection();});
            var edit=new ToolStripMenuItem("更多物件操作");
            if(TemplateRuntime.Eligible(t))edit.DropDownItems.Add("编辑行为",null,(s,e)=>FocusBehavior(true,-1));
            edit.DropDownItems.Add("存为自定义预设…",null,(s,e)=>SaveSelectionAsPreset());
            edit.DropDownItems.Add(t.enabled?"禁用物件":"启用物件",null,(s,e)=>{PushUndo();bool enabled=!t.enabled;foreach(var item in timeline.SelectedTracks)item.enabled=enabled;MarkChanged();ShowProperties();});
            edit.DropDownItems.Add(t.previewHidden?"恢复物件预览显示":"仅在预览中隐藏此物件",null,(s,e)=>{PushUndo();bool hidden=!t.previewHidden;foreach(var item in timeline.SelectedTracks)item.previewHidden=hidden;MarkChanged();ShowProperties();});
            if(t.kind=="pattern")edit.DropDownItems.Add("拆开片段以编辑内部物件",null,(s,e)=>UnpackPattern());
            else if(timeline.SelectedTracks.All(item=>item.kind!="native"&&item.kind!="script"))edit.DropDownItems.Add("组合为可循环弹幕片段",null,(s,e)=>PackPattern());
            if(t.templateOnly)edit.DropDownItems.Add("预览模板局部动画",null,(s,e)=>EnterTemplateContext(t));
            var move=new ToolStripMenuItem("移动到轨道");for(int i=0;i<state.laneCount;i++){int lane=i;move.DropDownItems.Add(Language.Text("轨道")+" "+(i+1),null,(s,e)=>MoveSelectionToLane(lane));}move.DropDownItems.Add("新轨道",null,(s,e)=>MoveSelectionToLane(state.laneCount));edit.DropDownItems.Add(move);menu.Items.Add(edit);
        }else{
            if(copiedKind!=null)menu.Items.Add("粘贴    Ctrl+V",null,async(s,e)=>await PasteSelection());
            var add=new ToolStripMenuItem("添加物件或事件");FillAddMenu(add);menu.Items.Add(add);
        }
        var lanes=new ToolStripMenuItem("轨道与组");LaneGroupMenu(lanes.DropDownItems);int active=timeline.ActiveLane;
        if(active>=0){var laneTracks=state.tracks.Where(x=>x.lane==active&&x.kind!="native").ToList();bool allHidden=laneTracks.Count>0&&laneTracks.All(x=>x.previewHidden);lanes.DropDownItems.Add(allHidden?"恢复本轨道物件预览显示":"仅预览隐藏本轨道所有物件",null,(s,e)=>{PushUndo();foreach(var x in laneTracks)x.previewHidden=!allHidden;MarkChanged();ShowProperties();}).Enabled=laneTracks.Count>0;}
        lanes.DropDownItems.Add("新建空轨道",null,(s,e)=>{PushUndo();timeline.ActiveLane=state.laneCount++;MarkChanged();});
        lanes.DropDownItems.Add("删除空轨道",null,(s,e)=>{if(active<0||state.laneCount<=1||state.tracks.Any(x=>x.lane==active&&x.kind!="native"))return;PushUndo();TimelineGroups.RemoveLane(state,active);foreach(var x in state.tracks.Where(x=>x.kind!="native"&&x.lane>active))x.lane--;state.laneCount--;MarkChanged();}).Enabled=active>=0&&state.laneCount>1&&!state.tracks.Any(x=>x.lane==active&&x.kind!="native");
        menu.Items.Add(lanes);Language.Attach(menu);
    }


}
