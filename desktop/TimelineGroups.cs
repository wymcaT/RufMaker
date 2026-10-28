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
        if(timeline.ActiveLane<0&&timeline.SelectedTracks.Count==0)return;var current=TimelineGroups.At(state,timeline.ActiveLane>=0?timeline.ActiveLane:selectedTrack.lane);var menu=new ToolStripMenuItem("轨道归属组");
        menu.DropDownItems.Add(new ToolStripMenuItem("不分组",null,(s,e)=>SetLaneGroup(null)){Checked=current==null});foreach(var g in state.timelineGroups){var target=g;var choice=new ToolStripMenuItem(g.name,null,(s,e)=>SetLaneGroup(target)){Checked=g==current,ForeColor=Motion.ColorOf(g.color)};Language.PreserveText(choice);menu.DropDownItems.Add(choice);}menu.DropDownItems.Add(new ToolStripSeparator());menu.DropDownItems.Add("新建组并加入…",null,(s,e)=>CreateLaneGroup());
        if(current!=null){menu.DropDownItems.Add("选择组内所有物件",null,(s,e)=>timeline.SetTrackSelection(state.tracks.Where(t=>current.lanes.Contains(t.lane))));menu.DropDownItems.Add("重命名组…",null,(s,e)=>{string name=AskGroupName(current.name);if(name==null)return;PushUndo();current.name=name;MarkChanged();});menu.DropDownItems.Add("设置组底色…",null,(s,e)=>{using(var picker=new ColorDialog{Color=Motion.ColorOf(current.color),FullOpen=true})if(picker.ShowDialog(this)==DialogResult.OK){PushUndo();current.color=ColorTranslator.ToHtml(picker.Color);MarkChanged();}});menu.DropDownItems.Add("解散组（保留物件）",null,(s,e)=>{PushUndo();state.timelineGroups.Remove(current);MarkChanged();ShowProperties();});}items.Add(menu);
    }
    private void CompactTrackContext(ContextMenuStrip menu)
    {
        var t=selectedTrack;LaneGroupMenu(menu.Items);
        if(t!=null){menu.Items.Add("在右栏编辑关键帧",null,(s,e)=>OpenKeyWorkspace(t));menu.Items.Add("复制    Ctrl+C",null,(s,e)=>{timeline.ClearKeySelection();CopySelection();});var paste=menu.Items.Add("粘贴    Ctrl+V",null,async(s,e)=>await PasteSelection());paste.Enabled=copiedKind!=null;menu.Items.Add("删除所选物件    Delete",null,(s,e)=>{selectedKey=null;timeline.ClearKeySelection();RemoveSelection();});
            var edit=new ToolStripMenuItem("更多物件操作");edit.DropDownItems.Add("存为自定义预设…",null,(s,e)=>SaveSelectionAsPreset());edit.DropDownItems.Add(t.enabled?"禁用物件":"启用物件",null,(s,e)=>{PushUndo();t.enabled=!t.enabled;MarkChanged();});if(TemplateRuntime.Eligible(t))edit.DropDownItems.Add("物件生成行为…",null,(s,e)=>OpenTemplateBehavior());if(t.kind=="rod"||t.kind=="emitter")edit.DropDownItems.Add("编辑弹幕年龄行为…",null,(s,e)=>OpenBulletBehavior());if(t.kind=="pattern")edit.DropDownItems.Add("拆开片段以编辑内部物件",null,(s,e)=>UnpackPattern());else edit.DropDownItems.Add("组合为可循环弹幕片段",null,(s,e)=>PackPattern());if(t.templateOnly)edit.DropDownItems.Add("预览模板局部动画",null,(s,e)=>EnterTemplateContext(t));var move=new ToolStripMenuItem("移动到轨道");for(int i=0;i<state.laneCount;i++){int lane=i;move.DropDownItems.Add(Language.Text("轨道")+" "+(i+1),null,(s,e)=>MoveSelectionToLane(lane));}move.DropDownItems.Add("新轨道",null,(s,e)=>MoveSelectionToLane(state.laneCount));edit.DropDownItems.Add(move);menu.Items.Add(edit);
        }else if(copiedKind!=null)menu.Items.Add("粘贴    Ctrl+V",null,async(s,e)=>await PasteSelection());
        var add=new ToolStripMenuItem("添加物件或事件");add.DropDownItems.Add("添加弹幕发射器",null,(s,e)=>AddTrack("emitter"));var shapes=new ToolStripMenuItem("添加整体对象");foreach(var value in FormationShapes.Values){string shape=value;shapes.DropDownItems.Add(FormationShapes.Name(shape),null,(s,e)=>AddShape(shape));}add.DropDownItems.Add(shapes);var events=new ToolStripMenuItem("添加事件");foreach(string value in EventPresets.Keys){string key=value;events.DropDownItems.Add(EventPresets.Names[Array.IndexOf(EventPresets.Keys,key)],null,(s,e)=>AddEventPreset(key,frame));}add.DropDownItems.Add(events);add.DropDownItems.Add("添加拉镜",null,(s,e)=>AddTrack("camera"));add.DropDownItems.Add("添加贴图对象",null,(s,e)=>AddTrack("object"));var fx=new ToolStripMenuItem("添加画面特效");FillEffectMenu(fx.DropDownItems);add.DropDownItems.Add(fx);add.DropDownItems.Add("添加全局变量",null,(s,e)=>AddGlobalVariable());add.DropDownItems.Add("添加 GML 事件",null,(s,e)=>AddTrack("script"));menu.Items.Add(add);
        var lanes=new ToolStripMenuItem("轨道操作");lanes.DropDownItems.Add("新建空轨道",null,(s,e)=>{PushUndo();timeline.ActiveLane=state.laneCount++;MarkChanged();});int active=timeline.ActiveLane;lanes.DropDownItems.Add("删除空轨道",null,(s,e)=>{if(active<0||state.laneCount<=1||state.tracks.Any(x=>x.lane==active&&x.kind!="native"))return;PushUndo();TimelineGroups.RemoveLane(state,active);foreach(var x in state.tracks.Where(x=>x.kind!="native"&&x.lane>active))x.lane--;state.laneCount--;MarkChanged();}).Enabled=active>=0&&state.laneCount>1&&!state.tracks.Any(x=>x.lane==active&&x.kind!="native");menu.Items.Add(lanes);menu.Items.Add(new ToolStripSeparator());menu.Items.Add("撤销    Ctrl+Z",null,(s,e)=>Undo(false)).Enabled=undo.Count>0;menu.Items.Add("重做    Ctrl+Y",null,(s,e)=>Undo(true)).Enabled=redo.Count>0;Language.Attach(menu);
    }
}
