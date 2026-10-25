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
    private readonly InspectorScrollPanel scroll=new InspectorScrollPanel{Dock=DockStyle.Fill,AutoScroll=true};
    private readonly TableLayoutPanel rows=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,Padding=new Padding(8,0,8,8)};
    private readonly Label help=new Label{Dock=DockStyle.Bottom,Height=52,ForeColor=Theme.Muted,BackColor=Theme.Back,Padding=new Padding(8),Text="数字：按住上下拖动；单击输入。Shift 加速，Ctrl 微调。"};
    private readonly List<Action> refreshers=new List<Action>();
    private readonly Dictionary<TextBox,Action> textReverters=new Dictionary<TextBox,Action>();
    private object selectedObject;private bool rebuilding,scrubbing;private bool stacked=true;
    public Action BeginScrub,EndScrub;
    private readonly HashSet<string> collapsed=new HashSet<string>();
    private readonly HashSet<string> openSections=new HashSet<string>();
    private readonly HashSet<string> knownSections=new HashSet<string>();
    public bool Compact {get;set;}
    private bool DefaultFolded(string category){return Compact&&new[]{"物件","形状","发射","弹幕寿命","分组与作用范围","外观","彩虹 Cherry","拖影","运动","整体对象","转转棒","发射器","发射阵列","生成模板","释放队形","生命周期"}.Contains(category);}
    private void RememberSection(string category,bool folded){knownSections.Add(category);if(folded){collapsed.Add(category);openSections.Remove(category);}else{collapsed.Remove(category);openSections.Add(category);}}
    public string[] CaptureSections(){return collapsed.Concat(openSections.Select(k=>"!"+k)).ToArray();}
    public void RestoreSections(string[] values){collapsed.Clear();openSections.Clear();knownSections.Clear();foreach(string value in values??new string[0])RememberSection(value.StartsWith("!")?value.Substring(1):value,!value.StartsWith("!"));UpdateRows();}
    public void ResetSections(){foreach(string category in knownSections.ToArray())RememberSection(category,false);UpdateRows();}
    public void ResetDefaultSections(){foreach(string category in knownSections.ToArray())RememberSection(category,DefaultFolded(category));UpdateRows();}
    public string SelectedLabel {get;private set;}
    public object SelectedObject {get{return selectedObject;}set{CommitPending();var current=selectedObject as NativeProperties;if(current!=null&&current.TryRebind(value as NativeProperties)){Refresh();return;}if(current!=null&&value is NativeProperties){current.Reconcile((NativeProperties)value);}else selectedObject=value;UpdateRows();}}
    public bool EditingText {get{return Descendants(this).Any(c=>(c is TextBox&&!((TextBox)c).ReadOnly&&c.Visible&&c.ContainsFocus)||(c is ComboBox&&((ComboBox)c).DroppedDown));}}
    public ParameterInspector()
    {
        DoubleBuffered=true;AutoScaleMode=AutoScaleMode.None;BackColor=Theme.Panel;ForeColor=Theme.Text;Font=Theme.Font;help.Height=Font.Height*3+16;
        rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,48));rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,52));
        scroll.HeaderFocused=()=>headings.Values.Any(h=>h.Focused);
        scroll.Controls.Add(rows);Controls.Add(scroll);Controls.Add(help);
        Action defocus=()=>{CommitPending();ActiveControl=null;};
        scroll.MouseDown+=(s,e)=>defocus();rows.MouseDown+=(s,e)=>defocus();help.MouseDown+=(s,e)=>defocus();
        scroll.Resize+=(s,e)=>{rows.Width=Math.Max(50,scroll.ClientSize.Width-SystemInformation.VerticalScrollBarWidth);bool next=rows.Width<(Compact?260:520);if(next!=stacked){stacked=next;UpdateRows();}};
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
    private sealed class PropertyIdentity : IEqualityComparer<PropertyDescriptor>
    {public bool Equals(PropertyDescriptor a,PropertyDescriptor b){return ReferenceEquals(a,b);}public int GetHashCode(PropertyDescriptor p){return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(p);}}
    private sealed class PropertyRow
    {public PropertyDescriptor property;public Label label;public Control editor;public readonly List<Action> updates=new List<Action>();public readonly Dictionary<TextBox,Action> reverts=new Dictionary<TextBox,Action>();public void Dispose(){label.Dispose();editor.Dispose();}}
    private readonly Dictionary<PropertyDescriptor,PropertyRow> boundRows=new Dictionary<PropertyDescriptor,PropertyRow>(new PropertyIdentity());
    private readonly Dictionary<string,Button> headings=new Dictionary<string,Button>();private bool layoutPending;
    private PropertyRow CreateRow(PropertyDescriptor p)
    {
        object component=selectedObject;var binding=new PropertyRow{property=p};var label=new Label{Text=p.Name,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Theme.Text,Margin=new Padding(2,1,4,1),AutoEllipsis=true};
        label.MouseDown+=(s,e)=>{CommitPending();ActiveControl=null;Select(p);};Control editor;
        Func<object> get=()=>p.GetValue(component);
        Action<object> set=value=>{if(!rebuilding&&!p.IsReadOnly){p.SetValue(component,value);if(!scrubbing)Refresh();}};
        if(p.PropertyType==typeof(double)||p.PropertyType==typeof(int)||p.PropertyType==typeof(decimal)||p.PropertyType==typeof(float))
        {
            var actions=(p as ValueProperty)==null?null:((ValueProperty)p).Actions;
            var number=new ScrubNumber(p.Name,p.PropertyType,get,set,actions){ReadOnly=p.IsReadOnly,Dock=DockStyle.Fill,Margin=new Padding(0,3,1,3)};
            number.Enter+=(s,e)=>Select(p);number.MouseDown+=(s,e)=>Select(p);number.BeginScrub=()=>{scrubbing=true;if(BeginScrub!=null)BeginScrub();};number.EndScrub=()=>{scrubbing=false;if(EndScrub!=null)EndScrub();Refresh();};editor=number;binding.updates.Add(number.RefreshValue);
            if(actions!=null){
                var wrap=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=1,Margin=Padding.Empty};wrap.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,Compact?30:Math.Max(58,TextRenderer.MeasureText(Language.Text("表达式"),Font).Width+14)));wrap.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));wrap.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,actions.ToggleKey==null?0:34));
                var random=new CheckBox{Text=Compact?"ƒ":"表达式",Name="random:"+p.Name,AccessibleName=Language.Text("表达式开关"),Appearance=Appearance.Button,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,FlatStyle=FlatStyle.Flat,BackColor=Theme.Raised,ForeColor=Theme.Text,Margin=new Padding(0,3,4,3)};random.Checked=actions.Random();random.ForeColor=random.Checked?Theme.Accent:Theme.Muted;random.Click+=(s,e)=>{Select(p);CommitPending();actions.ToggleRandom();Refresh();};wrap.Controls.Add(random,0,0);wrap.Controls.Add(number,1,0);binding.updates.Add(()=>{random.Checked=actions.Random();random.ForeColor=random.Checked?Theme.Accent:Theme.Muted;});
                wrap.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,36));var fx=new Button{Text="ƒx",Name="expression:"+p.Name,Dock=DockStyle.Fill,FlatStyle=FlatStyle.Flat,BackColor=Theme.Raised,ForeColor=Theme.Accent,Margin=new Padding(3,3,0,3)};fx.Click+=(s,e)=>{Select(p);CommitPending();if(actions.EditExpression!=null)actions.EditExpression();};wrap.Controls.Add(fx,3,0);
                if(actions.ToggleKey!=null){var key=new CheckBox{Text="◇",Name="key:"+p.Name,Appearance=Appearance.Button,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,FlatStyle=FlatStyle.Flat,BackColor=Theme.Raised,ForeColor=Theme.Muted,Margin=new Padding(4,3,0,3)};key.Checked=actions.Keyed();key.Text=key.Checked?"◆":"◇";key.ForeColor=key.Checked?Theme.Accent:Theme.Muted;key.Click+=(s,e)=>{Select(p);CommitPending();actions.ToggleKey();Refresh();};wrap.Controls.Add(key,2,0);binding.updates.Add(()=>{key.Checked=actions.Keyed();key.Text=key.Checked?"◆":"◇";key.ForeColor=key.Checked?Theme.Accent:Theme.Muted;});}
                editor=wrap;
            }
        }
        else if(p.PropertyType==typeof(bool))
        {var check=new CheckBox{AutoSize=false,Dock=DockStyle.Fill,Enabled=!p.IsReadOnly,Margin=new Padding(4,4,0,2)};check.Checked=(bool)get();check.CheckedChanged+=(s,e)=>set(check.Checked);check.Enter+=(s,e)=>Select(p);editor=check;binding.updates.Add(()=>{check.Checked=(bool)get();});}
        else if(p.PropertyType==typeof(Color))
        {var button=new ColorParameterButton{Enabled=!p.IsReadOnly,Dock=DockStyle.Fill,Margin=new Padding(0,3,1,3)};Action update=()=>{Color c=(Color)get();button.Value=c;button.Text=String.Format("#{0:X2}{1:X2}{2:X2}",c.R,c.G,c.B);};button.Click+=(s,e)=>{Select(p);using(var dialog=new ColorDialog{Color=(Color)get(),FullOpen=true})if(Language.ShowDialog(dialog,this)==DialogResult.OK)set(dialog.Color);};editor=button;binding.updates.Add(update);update();}
        else if(p.Converter.GetStandardValuesSupported())
        {
            var combo=new WheelSafeComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,BackColor=Theme.Raised,ForeColor=Theme.Text,Enabled=!p.IsReadOnly,TabStop=false,LocalizeItems=!(p is ValueProperty)||((ValueProperty)p).LocalizeChoices,Margin=new Padding(0,4,1,2)};
            foreach(object item in p.Converter.GetStandardValues())combo.Items.Add(item);combo.DropDownWidth=Math.Max(200,combo.Items.Cast<object>().Max(item=>TextRenderer.MeasureText(Language.Text(Convert.ToString(item)),Font).Width)+32);combo.SelectedItem=get();combo.SelectionChangeCommitted+=(s,e)=>{Select(p);set(combo.SelectedItem);};combo.Enter+=(s,e)=>Select(p);editor=combo;binding.updates.Add(()=>{if(!combo.DroppedDown)combo.SelectedItem=get();});
        }
        else
        {
            var text=new TextBox{Dock=DockStyle.Fill,ReadOnly=p.IsReadOnly,TabStop=false,BorderStyle=BorderStyle.FixedSingle,BackColor=Theme.Raised,ForeColor=p.IsReadOnly?Theme.Muted:Theme.Text,Margin=new Padding(0,4,1,3),Text=Convert.ToString(get())};if(p.IsReadOnly&&new[]{"说明","提示","操作","操作提示","编辑方式","原生设置","导入方式","底片状态","兼容提示与建议","路径说明","排列方式"}.Contains(p.Name))Language.WatchText(text);
            binding.reverts[text]=()=>{text.Text=Convert.ToString(get());text.Modified=false;};
            text.Enter+=(s,e)=>Select(p);text.Validated+=(s,e)=>{if(!text.Modified)return;string value=text.Text;text.Modified=false;if(value!=Convert.ToString(get()))set(value);};text.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Enter){if(text.Modified){string value=text.Text;text.Modified=false;if(value!=Convert.ToString(get()))set(value);}ActiveControl=null;e.Handled=true;e.SuppressKeyPress=true;}else if(e.KeyCode==Keys.Escape){text.Text=Convert.ToString(get());text.Modified=false;ActiveControl=null;e.Handled=true;e.SuppressKeyPress=true;}};editor=text;binding.updates.Add(()=>{if(!text.ContainsFocus){text.Text=Convert.ToString(get());text.Modified=false;}});
        }
        binding.label=label;binding.editor=editor;return binding;
    }
    private void Place(Control control,int column,int row,int span)
    {
        if(control.Parent!=rows)rows.Controls.Add(control,column,row);
        else if(rows.GetRow(control)!=row||rows.GetColumn(control)!=column)rows.SetCellPosition(control,new TableLayoutPanelCellPosition(column,row));
        if(rows.GetColumnSpan(control)!=span)rows.SetColumnSpan(control,span);
        if(control.TabIndex!=row*2+column)control.TabIndex=row*2+column;
    }
    private void ToggleSection(string category)
    {
        Point position=scroll.AutoScrollPosition;scroll.PreservePosition=true;
        try{CommitPending();RememberSection(category,!collapsed.Contains(category));UpdateRows();scroll.AutoScrollPosition=new Point(-position.X,-position.Y);}
        finally{scroll.PreservePosition=false;}
    }
    private void UpdateRows()
    {
        if(rebuilding){layoutPending=true;return;}rebuilding=true;Point position=scroll.AutoScrollPosition;rows.SuspendLayout();scroll.SuspendLayout();
        try
        {
            var descriptors=selectedObject==null?new PropertyDescriptor[0]:TypeDescriptor.GetProperties(selectedObject).Cast<PropertyDescriptor>().ToArray();var present=new HashSet<PropertyDescriptor>(descriptors,new PropertyIdentity());
            foreach(var property in boundRows.Keys.Where(p=>!present.Contains(p)).ToArray()){var old=boundRows[property];if(old.editor.ContainsFocus||old.label.ContainsFocus)ActiveControl=null;old.Dispose();boundRows.Remove(property);}
            var categories=new HashSet<string>(descriptors.Select(p=>p.Category));foreach(string category in headings.Keys.Where(c=>!categories.Contains(c)).ToArray()){headings[category].Dispose();headings.Remove(category);}
            var styles=new List<RowStyle>();int row=0;refreshers.Clear();textReverters.Clear();
            foreach(var group in descriptors.GroupBy(p=>p.Category).OrderBy(group=>!Compact?0:group.Key=="变换"||group.Key=="位置"?0:group.Key=="物件"?1:group.Key=="整体对象"||group.Key=="形状"?2:group.Key=="外观"?3:4))
            {
                string category=group.Key;if(!knownSections.Contains(category))RememberSection(category,DefaultFolded(category));
                Button heading;if(!headings.TryGetValue(category,out heading)){heading=new Button{Text=category,Height=30,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Theme.Text,BackColor=Theme.Raised,FlatStyle=FlatStyle.Flat,Font=Theme.Bold,Margin=new Padding(0,6,0,1),AutoEllipsis=true};heading.FlatAppearance.BorderSize=0;heading.Click+=(sender,args)=>ToggleSection(category);headings[category]=heading;}
                bool folded=collapsed.Contains(category);string summary="";if(folded){var first=group.FirstOrDefault(p=>p.Name=="贴图"||p.Name=="种类"||p.Name=="拖影样式"||p.Name=="运动模式");if(first!=null)summary=" · "+Convert.ToString(first.GetValue(selectedObject));}heading.Text=(folded?"▸ ":"▾ ")+Language.Text(category)+summary;
                Place(heading,0,row++,2);styles.Add(new RowStyle(SizeType.Absolute,Font.Height+22));
                foreach(var property in group)
                {
                    PropertyRow binding;if(!boundRows.TryGetValue(property,out binding)){if(folded)continue;binding=CreateRow(property);boundRows[property]=binding;}
                    binding.label.Visible=binding.editor.Visible=!folded;if(folded){refreshers.AddRange(binding.updates);foreach(var revert in binding.reverts)textReverters.Add(revert.Key,revert.Value);continue;}
                    if(binding.label.AutoSize!=stacked)binding.label.AutoSize=stacked;
                    if(stacked){Place(binding.label,0,row++,2);styles.Add(new RowStyle(SizeType.AutoSize));Place(binding.editor,0,row++,2);styles.Add(new RowStyle(SizeType.Absolute,Math.Max(34,Font.Height+16)));}
                    else {Place(binding.label,0,row,1);Place(binding.editor,1,row++,1);styles.Add(new RowStyle(SizeType.Absolute,Math.Max(34,Font.Height+16)));}
                    refreshers.AddRange(binding.updates);foreach(var revert in binding.reverts)textReverters.Add(revert.Key,revert.Value);
                }
            }
            rows.RowCount=row;while(rows.RowStyles.Count>styles.Count)rows.RowStyles.RemoveAt(rows.RowStyles.Count-1);
            for(int i=0;i<styles.Count;i++){if(i>=rows.RowStyles.Count)rows.RowStyles.Add(styles[i]);else {var style=rows.RowStyles[i];if(style.SizeType!=styles[i].SizeType)style.SizeType=styles[i].SizeType;if(style.Height!=styles[i].Height)style.Height=styles[i].Height;}}
        }
        finally{rows.ResumeLayout(true);scroll.ResumeLayout(true);scroll.AutoScrollPosition=new Point(-position.X,-position.Y);rebuilding=false;}
        if(layoutPending){layoutPending=false;UpdateRows();}else Refresh();
    }
    public override void Refresh(){if(!rebuilding){rebuilding=true;try{foreach(var update in refreshers)update();}finally{rebuilding=false;}}}
}

internal sealed class InspectorScrollPanel : Panel
{
    public bool PreservePosition;public Func<bool> HeaderFocused;
    protected override Point ScrollToControl(Control activeControl)
    {return PreservePosition||HeaderFocused!=null&&HeaderFocused()?DisplayRectangle.Location:base.ScrollToControl(activeControl);}
}

internal sealed class ColorParameterButton : Button
{
    private Color value;
    public Color Value {get{return value;}set{if(this.value==value)return;this.value=value;Invalidate();}}
    public ColorParameterButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);BackColor=Theme.Raised;ForeColor=Theme.Text;FlatStyle=FlatStyle.Flat;UseVisualStyleBackColor=false;}
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);int edge=Math.Max(8,Math.Min(22,Height-10));var swatch=new Rectangle(7,(Height-edge)/2,edge,edge);
        using(var brush=new SolidBrush(value))e.Graphics.FillRectangle(brush,swatch);
        using(var pen=new Pen(Focused?Theme.Accent:Theme.Line)){e.Graphics.DrawRectangle(pen,0,0,Width-1,Height-1);e.Graphics.DrawRectangle(pen,swatch);}
        Theme.Label(e.Graphics,Text,new Rectangle(swatch.Right+8,0,Math.Max(0,Width-swatch.Right-12),Height),Enabled?ForeColor:Theme.Muted,Font,localize:false);
    }
}

internal sealed class ScrubNumber : Control
{
    private readonly Func<object> get;private readonly Action<object> set;private readonly Type numberType;private readonly string label;
    private readonly TextBox input=new TextBox{BorderStyle=BorderStyle.FixedSingle,Dock=DockStyle.Fill,BackColor=Theme.Raised,ForeColor=Theme.Text,Visible=false};
    private readonly ParameterActions actions;private readonly ToolTip errorTip=new ToolTip();
    private Point start;private double original,step;private bool down,dragging,committing;
    private object displayedValue;private string displayedExpression;private bool displayedRandom,hasDisplay;
    public bool ReadOnly;public Action BeginScrub,EndScrub;
    public ScrubNumber(string label,Type numberType,Func<object> get,Action<object> set,ParameterActions actions=null)
    {
        this.actions=actions;this.label=label;this.numberType=numberType;this.get=get;this.set=set;DoubleBuffered=true;TabStop=true;Cursor=Cursors.SizeNS;BackColor=Theme.Raised;Font=Theme.Font;
        Controls.Add(input);input.Leave+=(s,e)=>CommitText();input.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Enter){CommitText();if(!input.Visible)Focus();e.SuppressKeyPress=true;}else if(e.KeyCode==Keys.Escape){input.Visible=false;RefreshValue();Focus();e.SuppressKeyPress=true;}};
    }
    public static double StepFor(double value,bool integer,string label,string property=null)
    {
        if(integer)return 1;
        switch(property??""){
            case "trailScaleDecay":case "trailAlphaDecay":return .001;
            case "scale":case "alpha":case "cameraZoom":case "shapeAspect":case "shapeInner":case "pathProgress":case "fxOpacity":case "fxRetention":case "fxFlash":case "fxZoom":case "cameraIntensity":case "trailStartAlpha":case "rainbowDensity":case "easePeriod":case "fxFrequency":return .01;
            case "accelerationX":case "accelerationY":return .01;
            case "speed":case "velocityX":case "velocityY":case "rollSpeed":case "trailSpeed":case "angularSpeed":case "orbitSpeed":case "turnRate":case "rainbowSpeed":case "easePower":return .1;
        }
        if(!String.IsNullOrEmpty(property))return 1;
        if(label.Contains("衰减"))return .001;
        if(label.Contains("缩放")||label.Contains("透明")||label.Contains("比例")||label.Contains("周期")||label.Contains("频率")||label.Contains("重力")||label.Contains("加速度"))return .01;
        if(label.Contains("速度")||label.Contains("指数")||label.Contains("秒"))return .1;
        return 1;
    }
    public void RefreshValue()
    {
        if(input.Visible)return;object value=get();bool random=actions!=null&&actions.Random();string expression=random?actions.Expression():null;
        if(!hasDisplay||!Equals(value,displayedValue)||random!=displayedRandom||expression!=displayedExpression){displayedValue=value;displayedRandom=random;displayedExpression=expression;hasDisplay=true;Invalidate();}
    }
    public bool CancelUncommitted()
    {
        if(!input.Visible)return false;
        bool wasModified=input.Modified;committing=true;
        try{input.Modified=false;input.Visible=false;errorTip.Hide(input);Focus();}
        finally{committing=false;RefreshValue();Invalidate();}
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
        finally{committing=false;RefreshValue();Invalidate();}
    }
    private void SetNumber(double value)
    {
        object converted=numberType==typeof(int)?(object)(int)Math.Max(Int32.MinValue,Math.Min(Int32.MaxValue,Math.Round(value))):numberType==typeof(float)?(object)(float)value:numberType==typeof(decimal)?(object)(decimal)value:(object)value;
        if(!Equals(converted,get()))set(converted);RefreshValue();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);e.Graphics.Clear(dragging?Color.FromArgb(68,48,68):Theme.Raised);
        if(!hasDisplay)RefreshValue();string value=Convert.ToDouble(displayedValue).ToString("0.######",CultureInfo.CurrentCulture);bool random=displayedRandom;
        if(random){Theme.Label(e.Graphics,displayedExpression,new Rectangle(7,0,Math.Max(1,Width-107),Height),Theme.Text,localize:false);Theme.Label(e.Graphics,"= "+value,new Rectangle(Math.Max(7,Width-95),0,90,Height),Theme.Accent,Theme.Small,localize:false);}
        else Theme.Label(e.Graphics,value,new Rectangle(7,0,Math.Max(1,Width-23),Height),ReadOnly?Theme.Muted:Theme.Text,localize:false);
        if(!ReadOnly&&!random)Theme.Label(e.Graphics,"↕",new Rectangle(Width-17,0,15,Height),Theme.Muted,Theme.Small);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {base.OnMouseDown(e);if(ReadOnly||e.Button!=MouseButtons.Left)return;CommitText();Focus();start=e.Location;original=Convert.ToDouble(get());step=StepFor(original,numberType==typeof(int),label,actions==null?null:actions.Property);down=true;dragging=false;Capture=true;}
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
