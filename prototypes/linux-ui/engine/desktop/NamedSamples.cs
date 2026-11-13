using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Drawing;
using System.Windows.Forms;

internal sealed class SampleTable
{
    public double[] values=new double[0];public string mode;
    public SampleTable Snapshot(){return new SampleTable{values=values==null?null:(double[])values.Clone(),mode=mode};}
}
internal static class NamedSamples
{
    private static readonly Regex Name=new Regex("^[a-z_][a-z0-9_]*$",RegexOptions.CultureInvariant);
    public static bool IsReference(string name){return name!=null&&name.StartsWith("sample.",StringComparison.Ordinal)&&Name.IsMatch(name.Substring(7));}
    public static Dictionary<string,SampleTable> Clone(IDictionary<string,SampleTable> tables){return tables==null?new Dictionary<string,SampleTable>():tables.ToDictionary(p=>p.Key,p=>p.Value==null?null:p.Value.Snapshot());}
    public static void Validate(IDictionary<string,SampleTable> tables)
    {
        if(tables==null)return;if(tables.Count>64)throw new FormatException("命名取样表最多64张。");
        foreach(var p in tables){if(!Name.IsMatch(p.Key)||p.Value==null)throw new FormatException("取样表名称必须使用小写字母、数字和下划线，且不能以数字开头。");var t=p.Value;if(t.values==null||t.values.Length<1||t.values.Length>4096||t.values.Any(v=>Double.IsNaN(v)||Double.IsInfinity(v)))throw new FormatException("取样表必须包含1至4096个有限数字："+p.Key);if(t.mode!="clamp"&&t.mode!="wrap"&&t.mode!="error")throw new FormatException("取样表越界策略必须为clamp、wrap或error："+p.Key);}
    }
    public static double Value(IDictionary<string,SampleTable> tables,string name,double index)
    {
        SampleTable t;if(tables==null||!tables.TryGetValue(name,out t)||t==null)throw new FormatException("未定义取样表："+name);
        if(Double.IsNaN(index)||Double.IsInfinity(index))throw new FormatException("取样索引必须为有限数字。");
        int n=t.values==null?0:t.values.Length;if(n==0)throw new FormatException("取样表不能为空："+name);double i=Math.Floor(index);
        if(t.mode=="wrap")i=((i%n)+n)%n;else if(t.mode=="error"){if(i<0||i>=n)throw new FormatException("取样索引超出范围："+name);}else if(t.mode=="clamp")i=Math.Max(0,Math.Min(n-1,i));else throw new FormatException("取样表越界策略无效："+name);
        double v=t.values[(int)i];if(Double.IsNaN(v)||Double.IsInfinity(v))throw new FormatException("取样值必须为有限数字："+name);return v;
    }
    private static void References(RandomExpression.Node n,IDictionary<string,SampleTable> tables){if(IsReference(n.op)&&(tables==null||!tables.ContainsKey(n.op.Substring(7))))throw new FormatException("未定义取样表："+n.op.Substring(7));foreach(var a in n.args)References(a,tables);}
    public static void ValidateReferences(Track t,IDictionary<string,SampleTable> tables)
    {
        var texts=new List<string>();texts.AddRange((t.expressions??new Dictionary<string,string>()).Values);texts.AddRange(t.keyframes.Where(k=>!String.IsNullOrWhiteSpace(k.expression)).Select(k=>k.expression));
        foreach(var a in (t.bulletActions??new List<BulletAction>()).Concat(t.spawnActions??new List<BulletAction>())){texts.Add(a.expression);texts.Add(a.condition);if(a.expressions!=null)texts.AddRange(a.expressions.Values);texts.Add(a.spawnColorExpression);}
        if(t.flightMode=="expression")texts.Add(t.flightExpression);if(t.kind=="variable")texts.Add(t.variableExpression);if(t.kind=="control")texts.Add(t.controlExpression);if(t.colorMode=="random")texts.Add(t.colorExpression);
        foreach(string text in texts.Where(x=>!String.IsNullOrWhiteSpace(x)&&x.IndexOf("sample.",StringComparison.OrdinalIgnoreCase)>=0))References(RandomExpression.Parse(text),tables);
    }
    public static void Normalize(Track t){if(t.sampleTables==null)t.sampleTables=new Dictionary<string,SampleTable>();Validate(t.sampleTables);ValidateReferences(t,t.sampleTables);}
    // Resolve against an isolated whole-scene snapshot before touching history or live values.
    // Static expression/keyframe caches must be refreshed without making sample() time-dynamic.
    public static void ApplyChange(EditorState state,Track track,IDictionary<string,SampleTable> tables,Action beforeCommit)
    {
        int index=state.tracks.IndexOf(track);if(index<0)throw new FormatException("取样表物件不属于当前工程。");
        var next=Clone(tables);Validate(next);ValidateReferences(track,next);
        var trial=state.Snapshot();trial.tracks[index].sampleTables=Clone(next);RandomParameters.Resolve(trial);
        if(beforeCommit!=null)beforeCommit();track.sampleTables=next;RandomParameters.Resolve(state);
    }
    public static string Format(IDictionary<string,SampleTable> tables){return String.Join(Environment.NewLine,(tables??new Dictionary<string,SampleTable>()).OrderBy(p=>p.Key).Select(p=>p.Key+" | "+p.Value.mode+" | "+String.Join(", ",p.Value.values.Select(v=>v.ToString("R",CultureInfo.InvariantCulture)))));}
    public static Dictionary<string,SampleTable> Parse(string text)
    {
        var result=new Dictionary<string,SampleTable>();foreach(string raw in (text??"").Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)){if(String.IsNullOrWhiteSpace(raw))continue;var p=raw.Split('|');if(p.Length!=3)throw new FormatException("每行格式：名称 | clamp/wrap/error | 数值,数值");string name=p[0].Trim();if(result.ContainsKey(name))throw new FormatException("取样表名称重复："+name);double v;var cells=p[2].Split(',');var values=new List<double>();foreach(string cell in cells){if(!Double.TryParse(cell.Trim(),NumberStyles.Float,CultureInfo.InvariantCulture,out v))throw new FormatException("无法读取取样数值："+cell);values.Add(v);}result.Add(name,new SampleTable{mode=p[1].Trim(),values=values.ToArray()});}Validate(result);return result;
    }
    public static string Summary(IDictionary<string,SampleTable> tables){return Language.Text("编辑命名取样表")+" ("+(tables==null?0:tables.Count)+")";}
    public static Dictionary<string,SampleTable> Edit(IWin32Window owner,Track track)
    {
        using(var form=new Form{Text=Language.Text("命名取样表"),Size=new Size(780,520),MinimumSize=new Size(580,380),StartPosition=FormStartPosition.CenterParent,BackColor=Theme.Back,ForeColor=Theme.Text,Font=Theme.Font,ShowInTaskbar=false}){
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12),ColumnCount=1,RowCount=3};layout.RowStyles.Add(new RowStyle(SizeType.Absolute,92));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,44));
            var help=new Label{Dock=DockStyle.Fill,Text=Language.Text("每行：名称 | clamp/wrap/error | 数值,数值\n例如 delay | clamp | 1, 15, 30\n引用 sample.delay(i)；索引向下取整。clamp取首尾，wrap循环，error越界报错。修改一次，当前物件所有引用同步更新。")};layout.Controls.Add(help,0,0);
            var input=new TextBox{Dock=DockStyle.Fill,Multiline=true,ScrollBars=ScrollBars.Both,WordWrap=false,AcceptsReturn=true,Text=Format(track.sampleTables),Font=new Font(FontFamily.GenericMonospace,11),BackColor=Theme.Raised,ForeColor=Theme.Text};layout.Controls.Add(input,0,1);
            var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var apply=new Button{Text=Language.Text("应用"),AutoSize=true};var cancel=new Button{Text=Language.Text("取消"),AutoSize=true,DialogResult=DialogResult.Cancel};buttons.Controls.Add(apply);buttons.Controls.Add(cancel);layout.Controls.Add(buttons,0,2);form.Controls.Add(layout);form.CancelButton=cancel;Dictionary<string,SampleTable> result=null;
            apply.Click+=(s,e)=>{try{var next=Parse(input.Text);ValidateReferences(track,next);result=next;form.DialogResult=DialogResult.OK;}catch(FormatException ex){Language.Message(form,ex.Message,"命名取样表");}};
            return Language.ShowDialog(form,owner)==DialogResult.OK?result:null;
        }
    }
}
