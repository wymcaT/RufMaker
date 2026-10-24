using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

// Native parameter rows: click to type, or hold a number and drag vertically.
internal sealed class ParameterInspector : UserControl
{
    private readonly Panel scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true};
    private readonly TableLayoutPanel rows=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,Padding=new Padding(8,0,8,8)};
    private readonly Label help=new Label{Dock=DockStyle.Bottom,Height=52,ForeColor=Theme.Muted,BackColor=Theme.Back,Padding=new Padding(8),Text="数字：按住上下拖动；单击输入。Shift 加速，Ctrl 微调。"};
    private readonly List<Action> refreshers=new List<Action>();
    private readonly Dictionary<TextBox,Action> textReverters=new Dictionary<TextBox,Action>();
    private object selectedObject;private bool rebuilding,scrubbing;private bool stacked=true;
    public Action BeginScrub,EndScrub;
    public string SelectedLabel {get;private set;}
    public object SelectedObject {get{return selectedObject;}set{CommitPending();selectedObject=value;Rebuild();}}
    public bool EditingText {get{return Descendants(this).Any(c=>(c is TextBox&&!((TextBox)c).ReadOnly&&c.Visible&&c.ContainsFocus)||(c is ComboBox&&((ComboBox)c).DroppedDown));}}
    public ParameterInspector()
    {
        AutoScaleMode=AutoScaleMode.None;BackColor=Theme.Panel;ForeColor=Theme.Text;Font=Theme.Font;help.Height=Font.Height*3+16;
        rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,48));rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,52));
        scroll.Controls.Add(rows);Controls.Add(scroll);Controls.Add(help);
        Action defocus=()=>{CommitPending();ActiveControl=null;};
        scroll.MouseDown+=(s,e)=>defocus();rows.MouseDown+=(s,e)=>defocus();help.MouseDown+=(s,e)=>defocus();
        scroll.Resize+=(s,e)=>{rows.Width=Math.Max(50,scroll.ClientSize.Width-SystemInformation.VerticalScrollBarWidth);bool next=rows.Width<520;if(next!=stacked){stacked=next;Rebuild();}};
    }
    private static IEnumerable<Control> Descendants(Control root)
    {foreach(Control child in root.Controls){yield return child;foreach(var item in Descendants(child))yield return item;}}
    public void CommitPending()
    {foreach(var number in Descendants(this).OfType<ScrubNumber>().ToArray())number.CommitText();ValidateChildren();}
    public bool CancelUncommittedEdit()
    {
        foreach(var number in Descendants(this).OfType<ScrubNumber>().ToArray())if(number.CancelUncommitted())return true;
        foreach(var text in Descendants(this).OfType<TextBox>().Where(t=>!t.ReadOnly&&t.Visible&&t.ContainsFocus).ToArray())
        {
            if(text.Modified){Action revert;if(textReverters.TryGetValue(text,out revert))revert();text.Modified=false;ActiveControl=null;return true;}
            ActiveControl=null;
        }
        return false;
    }
    private void Select(PropertyDescriptor p){SelectedLabel=p.Name;help.Text=String.IsNullOrEmpty(p.Description)?"数字：按住上下拖动；单击输入。Shift 加速，Ctrl 微调。":p.Description;}
    private void Rebuild()
    {
        if(rebuilding)return;rebuilding=true;if(ContainsFocus)ActiveControl=null;Point position=scroll.AutoScrollPosition;rows.SuspendLayout();
        try
        {
            foreach(Control child in rows.Controls.Cast<Control>().ToArray())child.Dispose();rows.Controls.Clear();rows.RowStyles.Clear();rows.RowCount=0;refreshers.Clear();textReverters.Clear();
            if(selectedObject==null)return;
            foreach(var group in TypeDescriptor.GetProperties(selectedObject).Cast<PropertyDescriptor>().GroupBy(p=>p.Category))
            {
                var heading=new Label{Text=group.Key,Height=30,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Theme.Accent,Font=Theme.Bold,Margin=new Padding(0,7,0,1)};
                heading.MouseDown+=(s,e)=>{CommitPending();ActiveControl=null;};
                int header=rows.RowCount++;rows.RowStyles.Add(new RowStyle(SizeType.Absolute,Font.Height+22));rows.Controls.Add(heading,0,header);rows.SetColumnSpan(heading,2);
                foreach(var property in group)
                {
                    var p=property;object component=selectedObject;var label=new Label{Text=p.Name,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Theme.Text,Margin=new Padding(2,1,4,1),AutoEllipsis=true};
                    label.MouseDown+=(s,e)=>{CommitPending();ActiveControl=null;Select(p);};Control editor;
                    Func<object> get=()=>p.GetValue(component);
                    Action<object> set=value=>{if(!rebuilding&&!p.IsReadOnly){p.SetValue(component,value);if(!scrubbing)Refresh();}};
                    if(p.PropertyType==typeof(double)||p.PropertyType==typeof(int)||p.PropertyType==typeof(decimal)||p.PropertyType==typeof(float))
                    {
                        var actions=(p as ValueProperty)==null?null:((ValueProperty)p).Actions;
                        var number=new ScrubNumber(p.Name,p.PropertyType,get,set,actions){ReadOnly=p.IsReadOnly,Dock=DockStyle.Fill,Margin=new Padding(0,3,1,3)};
                        number.Enter+=(s,e)=>Select(p);number.MouseDown+=(s,e)=>Select(p);number.BeginScrub=()=>{scrubbing=true;if(BeginScrub!=null)BeginScrub();};number.EndScrub=()=>{scrubbing=false;if(EndScrub!=null)EndScrub();Refresh();};editor=number;refreshers.Add(number.RefreshValue);
                        if(actions!=null){
                            var wrap=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=1,Margin=Padding.Empty};wrap.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,Math.Max(58,TextRenderer.MeasureText(Language.Text("表达式"),Font).Width+14)));wrap.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));wrap.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,actions.ToggleKey==null?0:34));
                            var random=new CheckBox{Text="表达式",Name="random:"+p.Name,Appearance=Appearance.Button,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,FlatStyle=FlatStyle.Flat,BackColor=Theme.Raised,ForeColor=Theme.Text,Margin=new Padding(0,3,4,3)};random.Checked=actions.Random();random.ForeColor=random.Checked?Theme.Accent:Theme.Muted;random.Click+=(s,e)=>{Select(p);CommitPending();actions.ToggleRandom();Refresh();};wrap.Controls.Add(random,0,0);wrap.Controls.Add(number,1,0);refreshers.Add(()=>{random.Checked=actions.Random();random.ForeColor=random.Checked?Theme.Accent:Theme.Muted;});
                            wrap.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,36));var fx=new Button{Text="ƒx",Name="expression:"+p.Name,Dock=DockStyle.Fill,FlatStyle=FlatStyle.Flat,BackColor=Theme.Raised,ForeColor=Theme.Accent,Margin=new Padding(3,3,0,3)};fx.Click+=(s,e)=>{Select(p);CommitPending();if(actions.EditExpression!=null)actions.EditExpression();};wrap.Controls.Add(fx,3,0);
                            if(actions.ToggleKey!=null){var key=new CheckBox{Text="◇",Name="key:"+p.Name,Appearance=Appearance.Button,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,FlatStyle=FlatStyle.Flat,BackColor=Theme.Raised,ForeColor=Theme.Muted,Margin=new Padding(4,3,0,3)};key.Checked=actions.Keyed();key.Text=key.Checked?"◆":"◇";key.ForeColor=key.Checked?Theme.Accent:Theme.Muted;key.Click+=(s,e)=>{Select(p);CommitPending();actions.ToggleKey();Refresh();};wrap.Controls.Add(key,2,0);refreshers.Add(()=>{key.Checked=actions.Keyed();key.Text=key.Checked?"◆":"◇";key.ForeColor=key.Checked?Theme.Accent:Theme.Muted;});}
                            editor=wrap;
                        }
                    }
                    else if(p.PropertyType==typeof(bool))
                    {var check=new CheckBox{AutoSize=false,Dock=DockStyle.Fill,Enabled=!p.IsReadOnly,Margin=new Padding(4,4,0,2)};check.Checked=(bool)get();check.CheckedChanged+=(s,e)=>set(check.Checked);check.Enter+=(s,e)=>Select(p);editor=check;refreshers.Add(()=>{check.Checked=(bool)get();});}
                    else if(p.PropertyType==typeof(Color))
                    {var button=new Button{Enabled=!p.IsReadOnly,FlatStyle=FlatStyle.Flat,Dock=DockStyle.Fill,Margin=new Padding(0,3,1,3)};Action update=()=>{Color c=(Color)get();button.BackColor=c;button.ForeColor=c.GetBrightness()>.5?Color.Black:Color.White;button.Text=String.Format("#{0:X2}{1:X2}{2:X2}",c.R,c.G,c.B);};button.Click+=(s,e)=>{Select(p);using(var dialog=new ColorDialog{Color=(Color)get(),FullOpen=true})if(Language.ShowDialog(dialog,this)==DialogResult.OK)set(dialog.Color);};editor=button;refreshers.Add(update);update();}
                    else if(p.Converter.GetStandardValuesSupported())
                    {
                        var combo=new WheelSafeComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,BackColor=Theme.Raised,ForeColor=Theme.Text,Enabled=!p.IsReadOnly,TabStop=false,LocalizeItems=!(p is ValueProperty)||((ValueProperty)p).LocalizeChoices,Margin=new Padding(0,4,1,2)};
                        foreach(object item in p.Converter.GetStandardValues())combo.Items.Add(item);combo.DropDownWidth=Math.Max(200,combo.Items.Cast<object>().Max(item=>TextRenderer.MeasureText(Language.Text(Convert.ToString(item)),Font).Width)+32);combo.SelectedItem=get();combo.SelectionChangeCommitted+=(s,e)=>{Select(p);set(combo.SelectedItem);};combo.Enter+=(s,e)=>Select(p);editor=combo;refreshers.Add(()=>{if(!combo.DroppedDown)combo.SelectedItem=get();});
                    }
                    else
                    {
                        var text=new TextBox{Dock=DockStyle.Fill,ReadOnly=p.IsReadOnly,TabStop=false,BorderStyle=BorderStyle.FixedSingle,BackColor=Theme.Raised,ForeColor=p.IsReadOnly?Theme.Muted:Theme.Text,Margin=new Padding(0,4,1,3),Text=Convert.ToString(get())};if(p.IsReadOnly&&new[]{"说明","提示","操作","操作提示","编辑方式","原生设置","导入方式","底片状态","兼容提示与建议","路径说明","排列方式"}.Contains(p.Name))Language.WatchText(text);
                        textReverters[text]=()=>{text.Text=Convert.ToString(get());text.Modified=false;};
                        text.Enter+=(s,e)=>Select(p);text.Validated+=(s,e)=>{if(!text.Modified)return;string value=text.Text;text.Modified=false;if(value!=Convert.ToString(get()))set(value);};text.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Enter){if(text.Modified){string value=text.Text;text.Modified=false;if(value!=Convert.ToString(get()))set(value);}ActiveControl=null;e.Handled=true;e.SuppressKeyPress=true;}else if(e.KeyCode==Keys.Escape){text.Text=Convert.ToString(get());text.Modified=false;ActiveControl=null;e.Handled=true;e.SuppressKeyPress=true;}};editor=text;refreshers.Add(()=>{if(!text.ContainsFocus){text.Text=Convert.ToString(get());text.Modified=false;}});
                    }
                    int row=rows.RowCount++;
                    if(stacked){label.AutoSize=true;rows.RowStyles.Add(new RowStyle(SizeType.AutoSize));rows.Controls.Add(label,0,row);rows.SetColumnSpan(label,2);int valueRow=rows.RowCount++;rows.RowStyles.Add(new RowStyle(SizeType.Absolute,Math.Max(34,Font.Height+16)));rows.Controls.Add(editor,0,valueRow);rows.SetColumnSpan(editor,2);}
                    else {rows.RowStyles.Add(new RowStyle(SizeType.Absolute,Math.Max(34,Font.Height+16)));rows.Controls.Add(label,0,row);rows.Controls.Add(editor,1,row);}
                }
            }
        }
        finally{rows.ResumeLayout(true);if(ContainsFocus)ActiveControl=null;rebuilding=false;scroll.AutoScrollPosition=new Point(-position.X,-position.Y);}
    }
    public override void Refresh(){if(!rebuilding){rebuilding=true;try{foreach(var update in refreshers.ToArray())update();}finally{rebuilding=false;}}base.Refresh();}
}

internal sealed class ScrubNumber : Control
{
    private readonly Func<object> get;private readonly Action<object> set;private readonly Type numberType;private readonly string label;
    private readonly TextBox input=new TextBox{BorderStyle=BorderStyle.FixedSingle,Dock=DockStyle.Fill,BackColor=Theme.Raised,ForeColor=Theme.Text,Visible=false};
    private readonly ParameterActions actions;private readonly ToolTip errorTip=new ToolTip();
    private Point start;private double original,step;private bool down,dragging,committing;
    public bool ReadOnly;public Action BeginScrub,EndScrub;
    public ScrubNumber(string label,Type numberType,Func<object> get,Action<object> set,ParameterActions actions=null)
    {
        this.actions=actions;this.label=label;this.numberType=numberType;this.get=get;this.set=set;DoubleBuffered=true;TabStop=true;Cursor=Cursors.SizeNS;BackColor=Theme.Raised;Font=Theme.Font;
        Controls.Add(input);input.Leave+=(s,e)=>CommitText();input.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Enter){CommitText();if(!input.Visible)Focus();e.SuppressKeyPress=true;}else if(e.KeyCode==Keys.Escape){input.Visible=false;Focus();e.SuppressKeyPress=true;}};
    }
    public static double StepFor(double value,bool integer,string label)
    {
        double magnitude=Math.Abs(value);double basis=magnitude<1?(label.Contains("透明")||label.Contains("缩放")||label.Contains("周期")?.01:label.Contains("速度")||label.Contains("加速度")?.1:1):Math.Pow(10,Math.Floor(Math.Log10(magnitude)))/50;
        return integer?Math.Max(1,basis):Math.Max(.001,basis);
    }
    public void RefreshValue(){if(!input.Visible)Invalidate();}
    public bool CancelUncommitted()
    {
        if(!input.Visible)return false;
        bool wasModified=input.Modified;committing=true;
        try{input.Modified=false;input.Visible=false;errorTip.Hide(input);Focus();}
        finally{committing=false;Invalidate();}
        return wasModified;
    }
    public void CommitText()
    {
        if(!input.Visible||committing)return;committing=true;
        try
        {
            double numeric;bool typedFormula=!Double.TryParse(input.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out numeric)&&!Double.TryParse(input.Text,NumberStyles.Float,CultureInfo.CurrentCulture,out numeric);
            if(actions!=null&&(actions.Random()||typedFormula)){try{actions.SetExpression(input.Text);input.BackColor=Theme.Raised;input.Visible=false;errorTip.Hide(input);}catch(FormatException error){input.BackColor=Color.FromArgb(88,37,49);Language.ToolTip(errorTip,input,error.Message);errorTip.Show(Language.Text(error.Message),input,0,input.Height,5000);}return;}
            double value;if(!Double.TryParse(input.Text,NumberStyles.Float,CultureInfo.CurrentCulture,out value)&&!Double.TryParse(input.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out value)){input.Text=Convert.ToString(get());return;}
            if(Double.IsNaN(value)||Double.IsInfinity(value))return;input.Visible=false;SetNumber(value);
        }
        finally{committing=false;Invalidate();}
    }
    private void SetNumber(double value)
    {
        object converted=numberType==typeof(int)?(object)(int)Math.Max(Int32.MinValue,Math.Min(Int32.MaxValue,Math.Round(value))):numberType==typeof(float)?(object)(float)value:numberType==typeof(decimal)?(object)(decimal)value:(object)value;
        if(!Equals(converted,get()))set(converted);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);e.Graphics.Clear(dragging?Color.FromArgb(68,48,68):Theme.Raised);
        string value=Convert.ToDouble(get()).ToString("0.######",CultureInfo.CurrentCulture);bool random=actions!=null&&actions.Random();
        if(random){Theme.Label(e.Graphics,actions.Expression(),new Rectangle(7,0,Math.Max(1,Width-107),Height),Theme.Text,localize:false);Theme.Label(e.Graphics,"= "+value,new Rectangle(Math.Max(7,Width-95),0,90,Height),Theme.Accent,Theme.Small,localize:false);}
        else Theme.Label(e.Graphics,value,new Rectangle(7,0,Math.Max(1,Width-23),Height),ReadOnly?Theme.Muted:Theme.Text,localize:false);
        if(!ReadOnly&&!random)Theme.Label(e.Graphics,"↕",new Rectangle(Width-17,0,15,Height),Theme.Muted,Theme.Small);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {base.OnMouseDown(e);if(ReadOnly||e.Button!=MouseButtons.Left)return;CommitText();Focus();start=e.Location;original=Convert.ToDouble(get());step=StepFor(original,numberType==typeof(int),label);down=true;dragging=false;Capture=true;}
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);if(!down||actions!=null&&actions.Random())return;int delta=start.Y-e.Y;if(!dragging&&Math.Abs(delta)<4)return;
        if(!dragging){dragging=true;if(BeginScrub!=null)BeginScrub();}double factor=(ModifierKeys&Keys.Control)!=0?.1:(ModifierKeys&Keys.Shift)!=0?10:1;
        double value=original+delta*step*factor;int precision=numberType==typeof(int)?0:Math.Max(0,Math.Min(6,(int)Math.Ceiling(-Math.Log10(step*factor))));SetNumber(Math.Round(value,precision));Invalidate();
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);if(!down)return;bool wasDragging=dragging;FinishDrag();Capture=false;
        if(!wasDragging&&!ReadOnly&&e.Button==MouseButtons.Left)EditText();
    }
    private void FinishDrag(){down=false;if(dragging){dragging=false;if(EndScrub!=null)EndScrub();}Invalidate();}
    protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture&&down)FinishDrag();}
    private void EditText(){input.Text=actions!=null&&actions.Random()?actions.Expression():Convert.ToString(get(),CultureInfo.CurrentCulture);input.Modified=false;input.BackColor=Theme.Raised;input.Visible=true;input.Focus();input.SelectAll();}
    protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(!ReadOnly&&(e.KeyCode==Keys.Enter||e.KeyCode==Keys.F2)){EditText();e.Handled=true;e.SuppressKeyPress=true;}}
    protected override void Dispose(bool disposing){if(disposing)errorTip.Dispose();base.Dispose(disposing);}
}
