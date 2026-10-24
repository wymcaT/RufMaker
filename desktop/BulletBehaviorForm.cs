using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

internal sealed class BulletBehaviorForm : Form
{
    public List<BulletAction> Actions;
    private readonly ListBox list=new ListBox{Dock=DockStyle.Fill,BackColor=Theme.Panel,ForeColor=Theme.Text,BorderStyle=BorderStyle.None};
    private readonly ParameterInspector inspector=new ParameterInspector{Dock=DockStyle.Fill};
    private readonly AgeTimeline age=new AgeTimeline();private readonly Track track;private readonly EditorState state;
    private readonly Label summary=new Label{Dock=DockStyle.Fill,ForeColor=Theme.Muted,Padding=new Padding(10),AutoSize=true};
    public BulletBehaviorForm(Track track,EditorState state)
    {
        this.track=track;this.state=state;Actions=track.bulletActions.Select(a=>a.Snapshot()).ToList();Text="弹幕行为 · "+track.name;Font=Theme.Font;BackColor=Theme.Back;ForeColor=Theme.Text;Icon=AppWindow.LoadIcon();StartPosition=FormStartPosition.CenterParent;Size=new Size(1040,760);MinimumSize=new Size(850,620);AutoScaleMode=AutoScaleMode.None;
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=5,ColumnCount=1,Padding=new Padding(10)};layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,185));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,48));Controls.Add(layout);
        var tools=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true};var types=new WheelSafeComboBox{Width=165,DropDownStyle=ComboBoxStyle.DropDownList,BackColor=Theme.Raised,ForeColor=Theme.Text};types.Items.AddRange(BulletSystem.ActionNames.Select(Language.Text).Cast<object>().ToArray());types.SelectedIndex=0;tools.Controls.Add(types);
        AddButton(tools,"添加行为",()=>{var a=new BulletAction{type=BulletSystem.ActionTypes[types.SelectedIndex]};if(new[]{"aim","clear","split","collision"}.Contains(a.type))a.duration=0;if(a.type=="alpha"||a.type=="scale"||a.type=="collision")a.to=1;Actions.Add(a);RefreshList(Actions.Count-1);});
        AddButton(tools,"复制行为",()=>{if(list.SelectedIndex<0)return;Actions.Insert(list.SelectedIndex+1,Actions[list.SelectedIndex].Snapshot());RefreshList(list.SelectedIndex+1);});AddButton(tools,"删除行为",()=>{if(list.SelectedIndex<0)return;int i=list.SelectedIndex;Actions.RemoveAt(i);RefreshList(Math.Min(i,Actions.Count-1));});
        AddButton(tools,"上移",()=>MoveAction(-1));AddButton(tools,"下移",()=>MoveAction(1));var presets=new WheelSafeComboBox{Width=180,DropDownStyle=ComboBoxStyle.DropDownList,BackColor=Theme.Raised,ForeColor=Theme.Text};presets.Items.AddRange(new object[]{Language.Text("选择行为预设…"),Language.Text("减速 → 瞄准 → 冲刺"),Language.Text("旋转加速"),Language.Text("淡入淡出"),Language.Text("延迟分裂")});presets.SelectedIndex=0;presets.SelectionChangeCommitted+=(s,e)=>{if(presets.SelectedIndex>0){ApplyPreset(presets.SelectedIndex);presets.SelectedIndex=0;}};tools.Controls.Add(presets);layout.Controls.Add(tools,0,0);
        age.Dock=DockStyle.Fill;age.Actions=Actions;age.Changed=()=>RefreshList(age.Selected);age.SelectionChanged=i=>{if(i>=0&&i<list.Items.Count)list.SelectedIndex=i;};layout.Controls.Add(age,0,1);
        summary.Text="时间从每颗弹幕出生的第 0 帧开始。拖动条块移动，拖右边界改时长；同属性重叠时，列表靠后的行为优先。";layout.Controls.Add(summary,0,2);
        var split=new SplitContainer{Dock=DockStyle.Fill,SplitterWidth=6,BackColor=Theme.Back};split.Panel1.Controls.Add(list);split.Panel2.Controls.Add(inspector);layout.Controls.Add(split,0,3);split.SplitterDistance=290;list.SelectedIndexChanged+=(s,e)=>ShowAction();
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var save=new Button{Text="应用行为",AutoSize=true,Height=35,BackColor=Theme.Accent,ForeColor=Color.Black,FlatStyle=FlatStyle.Flat};save.Click+=(s,e)=>{try{inspector.CommitPending();ValidateActions();DialogResult=DialogResult.OK;}catch(Exception error){Language.Message(this,error.Message,"弹幕行为");}};buttons.Controls.Add(save);buttons.Controls.Add(new Button{Text="取消",AutoSize=true,Height=35,DialogResult=DialogResult.Cancel});layout.Controls.Add(buttons,0,4);RefreshList(Actions.Count>0?0:-1);
    }
    private static void AddButton(Control host,string text,Action action){var b=new Button{Text=text,AutoSize=true,Height=32,FlatStyle=FlatStyle.Flat,BackColor=Theme.Raised,ForeColor=Theme.Text,Margin=new Padding(3)};b.Click+=(s,e)=>action();host.Controls.Add(b);}
    private void MoveAction(int delta){int i=list.SelectedIndex,j=i+delta;if(i<0||j<0||j>=Actions.Count)return;var a=Actions[i];Actions.RemoveAt(i);Actions.Insert(j,a);RefreshList(j);}
    private void RefreshList(int selected){list.BeginUpdate();list.Items.Clear();foreach(var a in Actions)list.Items.Add((a.enabled?"":"× ")+a.start+"–"+(a.start+a.duration)+"f   "+Language.Text(BulletSystem.ActionName(a.type)));list.EndUpdate();if(selected>=0&&selected<list.Items.Count)list.SelectedIndex=selected;else ShowAction();age.Selected=list.SelectedIndex;age.Invalidate();}
    private void ShowAction()
    {
        age.Selected=list.SelectedIndex;age.Invalidate();var bag=new NativeProperties();inspector.SelectedObject=bag;if(list.SelectedIndex<0)return;var a=Actions[list.SelectedIndex];
        Action refresh=()=>{age.Invalidate();};Action rebuild=()=>BeginInvoke(new Action(()=>RefreshList(list.SelectedIndex)));
        bag.Add("启用","行为",typeof(bool),()=>a.enabled,v=>{a.enabled=(bool)v;rebuild();});bag.Add("种类","行为",typeof(string),()=>BulletSystem.ActionName(a.type),v=>{a.type=BulletSystem.ActionTypes[Array.IndexOf(BulletSystem.ActionNames,(string)v)];if(new[]{"aim","clear","split","collision"}.Contains(a.type))a.duration=0;rebuild();},BulletSystem.ActionNames);
        bag.Add("开始年龄 / 帧","时间",typeof(int),()=>a.start,v=>{a.start=Math.Max(0,(int)v);refresh();});bag.Add("持续帧数","时间",typeof(int),()=>a.duration,v=>{a.duration=Math.Max(0,(int)v);refresh();});bag.Add("每颗延迟 / 帧","时间",typeof(int),()=>a.stagger,v=>{a.stagger=Math.Max(0,(int)v);refresh();},null,"实际开始年龄 = 开始年龄 + i × 每颗延迟。");
        if(a.type=="split"){
            var templates=state.tracks.Where(t=>t.kind=="emitter").ToArray();var names=new[]{"继承本体外观（无后续行为）"}.Concat(templates.Select(t=>t.name+" · "+t.id.Substring(0,Math.Min(8,t.id.Length)))).ToArray();
            bag.Add("子弹模板","分裂",typeof(string),()=>names[Math.Max(0,Array.FindIndex(templates,t=>t.id==a.templateId)+1)],v=>{int i=Array.IndexOf(names,(string)v)-1;a.templateId=i<0?"":templates[i].id;},names);
            bag.Add("子弹数量","分裂",typeof(int),()=>a.count,v=>a.count=Math.Max(1,Math.Min(300,(int)v)));bag.Add("子弹速度","分裂",typeof(double),()=>a.childSpeed,v=>a.childSpeed=(double)v);bag.Add("子弹角度间隔","分裂",typeof(double),()=>a.spread,v=>a.spread=(double)v);bag.Add("最多分裂代数","分裂",typeof(int),()=>a.maxGeneration,v=>a.maxGeneration=Math.Max(1,Math.Min(4,(int)v)));bag.Add("说明","分裂",typeof(string),()=>"母弹保留。子弹从母弹当时的位置发射，以母弹方向为中心；模板可禁用以仅供子发射器使用。",null);
        }else if(a.type!="clear"){
            if(a.type!="aim"){bag.Add("起始值","参数",typeof(double),()=>a.from,v=>a.from=(double)v);bag.Add("结束值","参数",typeof(double),()=>a.to,v=>a.to=(double)v);}else bag.Add("瞄准方向偏移 / 度","参数",typeof(double),()=>a.to,v=>{a.to=(double)v;a.from=a.to;});
            bag.Add("相对出生值","参数",typeof(bool),()=>a.relative,v=>a.relative=(bool)v,null,"方向、弹速为相加；大小、不透明度为相乘。");bag.Add("缓动","参数",typeof(string),()=>Motion.EaseNames[Math.Max(0,Array.IndexOf(Motion.EaseValues,a.easing))],v=>a.easing=Motion.EaseValues[Array.IndexOf(Motion.EaseNames,(string)v)],Motion.EaseNames);
            bag.Add("公式（留空使用数值）","参数",typeof(string),()=>a.expression,v=>{string text=((string)v??"").Trim();if(text.Length>0)RandomExpression.Evaluate(text,state.randomSeed,track.id,ExpressionContext.For(track,track.start));a.expression=text;},null,"公式每帧执行，可用 i、n、u、volley、age、player.x/y、x/y、speed、direction。sin/cos 使用弧度。");
            if(a.type=="collision")bag.Add("说明","参数",typeof(string),()=>"0 关闭碰撞；1 开启碰撞。外观与碰撞独立。",null);
        }
        inspector.SelectedObject=bag;
    }
    private void ValidateActions(){foreach(var a in Actions)if(!String.IsNullOrWhiteSpace(a.expression))RandomExpression.Evaluate(a.expression,state.randomSeed,track.id,ExpressionContext.For(track,track.start));}
    private void ApplyPreset(int index)
    {
        int fps=state.fps;if(index==1){Actions.Add(new BulletAction{type="speed",start=0,duration=fps/3,from=track.speed,to=0,easing="sin-out"});Actions.Add(new BulletAction{type="aim",start=fps,duration=0,to=0});Actions.Add(new BulletAction{type="speed",start=fps,duration=fps/2,from=0,to=7,easing="power-in"});}
        if(index==2){Actions.Add(new BulletAction{type="turn",start=0,duration=0,to=2});Actions.Add(new BulletAction{type="speed",start=0,duration=fps*2,from=1,to=6,easing="smooth"});}
        if(index==3){Actions.Add(new BulletAction{type="alpha",start=0,duration=fps/3,from=0,to=1});Actions.Add(new BulletAction{type="alpha",start=Math.Max(fps/3,track.life-fps/2-1),duration=fps/2,from=1,to=0});}
        if(index==4)Actions.Add(new BulletAction{type="split",start=fps,duration=0});RefreshList(Actions.Count-1);
    }
    private sealed class AgeTimeline : Control
    {
        public List<BulletAction> Actions;public int Selected=-1;public Action Changed;public Action<int> SelectionChanged;private bool dragging,resizing;private int mouse,start,duration;private float scale;
        public AgeTimeline(){DoubleBuffered=true;BackColor=Theme.Panel;}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(Actions==null)return;int max=Math.Max(150,Actions.Select(a=>a.start+a.duration).DefaultIfEmpty(0).Max()+30);scale=Math.Max(1,Width-130)/(float)max;var g=e.Graphics;for(int f=0;f<=max;f+=Math.Max(10,max/10/10*10)){int x=120+(int)(f*scale);g.DrawLine(Pens.DimGray,x,22,x,Height);Theme.Label(g,f+"f",new Rectangle(x,0,50,22),Theme.Muted,Theme.Small,localize:false);}int first=Math.Max(0,Selected-3);for(int i=first;i<Math.Min(Actions.Count,first+5);i++){var a=Actions[i];int y=28+(i-first)*28;Theme.Label(g,BulletSystem.ActionName(a.type),new Rectangle(4,y,112,25),Theme.Text,Theme.Small);using(var b=new SolidBrush(i==Selected?Theme.Accent:Color.SlateBlue))g.FillRectangle(b,120+a.start*scale,y+3,Math.Max(8,a.duration*scale),20);}}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;int first=Math.Max(0,Selected-3),i=first+(e.Y-28)/28;if(e.Y<28||i<0||i>=Actions.Count)return;Selected=i;if(SelectionChanged!=null)SelectionChanged(i);var a=Actions[i];float right=120+(a.start+a.duration)*scale;dragging=e.X>=120+a.start*scale-4&&e.X<=right+8;resizing=Math.Abs(e.X-right)<8;mouse=e.X;start=a.start;duration=a.duration;Capture=dragging;Invalidate();}
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(!dragging)return;int delta=(int)Math.Round((e.X-mouse)/scale);if(resizing)Actions[Selected].duration=Math.Max(0,duration+delta);else Actions[Selected].start=Math.Max(0,start+delta);Invalidate();}
        protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(dragging){dragging=false;Capture=false;if(Changed!=null)Changed();}}
    }
}
