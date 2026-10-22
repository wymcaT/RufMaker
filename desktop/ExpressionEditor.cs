using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

internal sealed class ExpressionEditor : Form
{
    private readonly TextBox formula=new TextBox{Dock=DockStyle.Fill,Multiline=true,ScrollBars=ScrollBars.Vertical,BackColor=Theme.Raised,ForeColor=Theme.Text};
    private readonly NumericUpDown first=new WheelSafeNumericUpDown{BackColor=Theme.Raised,ForeColor=Theme.Text},last=new WheelSafeNumericUpDown{BackColor=Theme.Raised,ForeColor=Theme.Text};
    private readonly CheckBox fill=new CheckBox{Text="填充范围内每一帧的关键帧",AutoSize=true};
    private readonly Label result=new Label{AutoSize=true,ForeColor=Theme.Accent,MaximumSize=new Size(650,0)};
    private readonly Func<string,int,double> evaluate;
    public string Expression {get{return formula.Text.Trim();}}
    public bool Fill {get{return fill.Checked;}}
    public int First {get{return (int)first.Value;}}public int Last {get{return (int)last.Value;}}
    internal static readonly string[] TemplateNames={"玩家 X 坐标","玩家 Y 坐标","朝向玩家","两点之间的方向","两点之间的距离","端点1 指向端点2","沿方向移动的 X 分量","沿方向移动的 Y 分量","限制转角（弹幕飞行公式）","正弦摆动","随机范围","随机选择"};
    internal static readonly string[] Templates={"player.x","player.y","point_direction(x,y,player.x,player.y)","point_direction(100,100,400,304)","point_distance(x,y,player.x,player.y)","point_direction(x1,y1,x2,y2)","lengthdir_x(100,45)","lengthdir_y(100,45)","turn_towards(direction,point_direction(x,y,player.x,player.y),3)","sin(time*2*pi)*30","random_range(0,360)","choose(0,90,180,270)"};
    public ExpressionEditor(string property,string expression,int frame,int start,int end,int duration,bool canFill,Func<string,int,double> evaluate)
    {
        this.evaluate=evaluate;Text="参数表达式 · "+Motion.Name(property);Font=Theme.Font;BackColor=Theme.Back;ForeColor=Theme.Text;Icon=AppWindow.LoadIcon();StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;MinimizeBox=false;MaximizeBox=false;Size=new Size(740,615);MinimumSize=new Size(580,560);AutoScaleMode=AutoScaleMode.None;
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(16),ColumnCount=1,RowCount=8};
        foreach(float size in new float[]{36,105,58,38,42,80,60,48})layout.RowStyles.Add(new RowStyle(SizeType.Absolute,size));layout.RowStyles[1]=new RowStyle(SizeType.Percent,100);
        var picker=new WheelSafeComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,BackColor=Theme.Raised,ForeColor=Theme.Text,FlatStyle=FlatStyle.Flat};picker.Items.Add(Language.Text("选择公式模板…"));foreach(var name in TemplateNames)picker.Items.Add(Language.Text(name));picker.SelectedIndex=0;picker.SelectionChangeCommitted+=(s,e)=>{if(picker.SelectedIndex>0)formula.Text=Templates[picker.SelectedIndex-1];};layout.Controls.Add(picker,0,0);
        formula.Font=new Font(FontFamily.GenericMonospace,12);formula.Text=expression;layout.Controls.Add(formula,0,1);
        layout.Controls.Add(new Label{Text="含玩家、坐标或时间变量的表达式会逐帧读取。只有随机数的表达式沿用固定取样；“重新随机”可换一组结果。",Dock=DockStyle.Fill},0,2);
        fill.Enabled=canFill;layout.Controls.Add(fill,0,3);
        var range=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};range.Controls.Add(new Label{Text="起始帧",AutoSize=true,Padding=new Padding(0,6,4,0)});first.Maximum=last.Maximum=duration;first.Minimum=last.Minimum=0;first.Width=last.Width=130;first.Value=start;last.Value=end;range.Controls.Add(first);range.Controls.Add(new Label{Text="结束帧（包含）",AutoSize=true,Padding=new Padding(14,6,4,0)});range.Controls.Add(last);layout.Controls.Add(range,0,4);range.Enabled=false;fill.CheckedChanged+=(s,e)=>range.Enabled=fill.Checked;
        layout.Controls.Add(new Label{Text="变量：player.x / player.y、x / y（当前物件）、x1 / y1 / x2 / y2（端点当前值）、frame（绝对帧）、time（秒）、age（已存在帧）、direction、speed、fps、pi。方向单位为度，sin / cos 使用弧度。",Dock=DockStyle.Fill},0,5);
        layout.Controls.Add(result,0,6);
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var apply=new Button{Text="应用",AutoSize=true,Height=36,BackColor=Theme.Accent,ForeColor=Color.Black,FlatStyle=FlatStyle.Flat};var cancel=new Button{Text="取消",AutoSize=true,Height=36,DialogResult=DialogResult.Cancel};buttons.Controls.Add(apply);buttons.Controls.Add(cancel);layout.Controls.Add(buttons,0,7);Controls.Add(layout);CancelButton=cancel;
        Action preview=()=>{try{result.Text=Language.Text("当前参考结果：")+evaluate(Expression,frame).ToString("0.######",CultureInfo.CurrentCulture);result.ForeColor=Theme.Accent;}catch(FormatException error){result.Text=Language.Text(error.Message);result.ForeColor=Color.Salmon;}};formula.TextChanged+=(s,e)=>preview();preview();
        apply.Click+=(s,e)=>{try{if(Fill&&First>Last)throw new FormatException("起始帧不能晚于结束帧。");evaluate(Expression,Fill?First:frame);DialogResult=DialogResult.OK;}catch(FormatException error){Language.Message(this,error.Message,"参数表达式");}};
    }
}

internal sealed partial class MainForm
{
    private void EditParameterExpression(Track[] tracks,string property,Keyframe key=null)
    {
        if(state==null||tracks.Length==0)return;StopPlaying();var t=tracks[0];var actions=ParameterControls(tracks,property,true,key);string text=actions.Expression();if(String.IsNullOrWhiteSpace(text))text=Motion.Value(t,property,frame).ToString("R",CultureInfo.InvariantCulture);
        using(var dialog=new ExpressionEditor(property,text,frame,t.start,t.end,state.duration,key==null&&tracks.All(track=>Motion.Fields(track).Contains(property)),(expression,at)=>{var context=ExpressionContext.For(t,at);var p=Motion.At(t,at);context.x=p.x;context.y=p.y;return RandomExpression.Evaluate(expression,state.randomSeed,t.randomId+"|"+property+"|base",context);}))
        {
            if(Language.ShowDialog(dialog,this)!=DialogResult.OK)return;
            try{if(dialog.Fill)FillExpressionKeys(tracks,property,dialog.Expression,dialog.First,dialog.Last);else actions.SetExpression(dialog.Expression);ShowProperties();}catch(FormatException error){Language.Message(this,error.Message,"参数表达式");}
        }
    }
    private void FillExpressionKeys(Track[] tracks,string property,string expression,int first,int last)
    {
        // Validate the complete operation before altering any selected object.
        if(first<0||last>state.duration||first>last)throw new FormatException("关键帧范围无效。");
        foreach(var t in tracks){if(t.keyframes.Count(k=>k.property!=property||k.frame<first||k.frame>last)+last-first+1+(!Motion.HasKeys(t,property)?(first>t.start?1:0)+(last<t.end?1:0):0)>10000)throw new FormatException("每个物件最多 10000 个关键帧，请缩短填充范围。");for(int at=first;at<=last;at++)RandomExpression.Evaluate(expression,state.randomSeed,t.randomId+"|"+property+"|fill:"+at,ExpressionContext.For(t,at));}
        PushUndo();foreach(var t in tracks){double before=Motion.Value(t,property,Math.Max(0,first-1)),after=Motion.Value(t,property,last+1);bool keyed=Motion.HasKeys(t,property);t.keyframes.RemoveAll(k=>k.property==property&&k.frame>=first&&k.frame<=last);
            if(!keyed){t.expressions.Remove(property);if(first>t.start)Motion.SetKey(t,property,first-1,before);if(last<t.end)Motion.SetKey(t,property,last+1,after);}
            for(int at=first;at<=last;at++)t.keyframes.Add(new Keyframe{frame=at,property=property,value=0,expression=expression,randomId="fill:"+at,easing="step"});t.start=Math.Min(t.start,first);t.end=Math.Max(t.end,last);timeline.Expand(t,true);}
        RandomParameters.Resolve(state);selectedKey=null;timeline.ClearKeySelection();timeline.SelectedProperty=property;MarkChanged();
    }
}
