using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

internal class LocalizedForm : Form
{
    protected override void OnLoad(EventArgs e){Language.Attach(this);base.OnLoad(e);}
}

internal static class Language
{
    private sealed class Binding {public string Source;public bool Writing;public Action Apply;}
    private sealed class Tip {public ToolTip Owner;public string Text;}
    private sealed class Tips {public readonly List<Tip> Items=new List<Tip>();}
    private sealed class Template {public Regex Pattern;public string Value;public string[] Tokens;}
    private static readonly ConditionalWeakTable<object,Binding> bindings=new ConditionalWeakTable<object,Binding>();
    private static readonly ConditionalWeakTable<Control,Tips> tips=new ConditionalWeakTable<Control,Tips>();
    private static readonly Dictionary<string,string> english=Load();
    private static readonly Regex token=new Regex(@"\$\{[^}]+\}");
    private static readonly Template[] templates=english.Where(p=>token.IsMatch(p.Key)).Select(p=>{
        var names=token.Matches(p.Key).Cast<Match>().Select(m=>m.Value).ToArray();
        string pattern=Regex.Escape(p.Key);foreach(string name in names)pattern=pattern.Replace(Regex.Escape(name),"(.+?)");
        return new Template{Pattern=new Regex(pattern),Value=p.Value,Tokens=names};}).ToArray();
    private static readonly Regex phrases=new Regex(String.Join("|",english.Keys.Where(k=>k.Length>0).OrderByDescending(k=>k.Length).Select(Regex.Escape)),RegexOptions.Compiled);
    // Keep diagnostic paths readable even when a project or folder has a Chinese name.
    private static readonly Regex paths=new Regex(@"(?:[A-Za-z]:[\\/]|\\\\)[^\r\n<>\""|]*",RegexOptions.Compiled);
    public static string Id {get;private set;}
    static Language(){Id="zh-CN";}
    public static bool English {get{return Id=="en";}}
    private static Dictionary<string,string> Load()
    {using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("RufMaker.en.json")){if(stream==null)return new Dictionary<string,string>();using(var reader=new StreamReader(stream))return Json.Read<Dictionary<string,string>>(reader.ReadToEnd());}}
    public static void Initialize(string root)
    {
        string file=Path.Combine(root,"user-data","settings.json");Id="zh-CN";
        if(File.Exists(file)){var settings=Json.Read<Dictionary<string,string>>(File.ReadAllText(file));string value;if(settings.TryGetValue("language",out value)&&value=="en")Id="en";}
    }
    public static void Select(string root,string id)
    {
        string file=Path.Combine(root,"user-data","settings.json");Directory.CreateDirectory(Path.GetDirectoryName(file));
        var settings=File.Exists(file)?Json.Read<Dictionary<string,string>>(File.ReadAllText(file)):new Dictionary<string,string>();settings["language"]=id=="en"?"en":"zh-CN";
        File.WriteAllText(file,Json.Write(settings),new System.Text.UTF8Encoding(false));Id=settings["language"];
        foreach(Form form in Application.OpenForms.Cast<Form>().ToArray())Apply(form);
    }
    public static string Text(string source)
    {
        if(!English||String.IsNullOrEmpty(source))return source;string result;if(english.TryGetValue(source,out result))return result;
        foreach(var template in templates)source=template.Pattern.Replace(source,m=>token.Replace(template.Value,t=>m.Groups[Array.IndexOf(template.Tokens,t.Value)+1].Value));
        int start=0;var value=new System.Text.StringBuilder();foreach(Match path in paths.Matches(source)){value.Append(phrases.Replace(source.Substring(start,path.Index-start),m=>english[m.Value]));value.Append(path.Value);start=path.Index+path.Length;}
        value.Append(phrases.Replace(source.Substring(start),m=>english[m.Value]));return value.ToString();
    }
    private static void Bind(object key,Func<string> get,Action<string> set,Action<EventHandler> listen)
    {
        Binding previous;if(bindings.TryGetValue(key,out previous)){previous.Apply();return;}
        var b=new Binding{Source=get()};b.Apply=()=>{b.Writing=true;try{set(Text(b.Source));}finally{b.Writing=false;}};bindings.Add(key,b);
        listen((s,e)=>{if(b.Writing)return;b.Source=get();b.Apply();});b.Apply();
    }
    public static void WatchText(Control control)
    {Bind(control,()=>control.Text,s=>control.Text=s,h=>control.TextChanged+=h);}
    public static void PreserveText(Control control)
    {bindings.Add(control,new Binding{Apply=()=>{}});}
    public static void Attach(Control control)
    {
        if(control==null)return;
        Binding existing;if(bindings.TryGetValue(control,out existing)){Apply(control);return;}
        if(control is Form||control is Label||control is ButtonBase||control is GroupBox)WatchText(control);
        else bindings.Add(control,new Binding{Apply=()=>{}});
        control.ControlAdded+=(s,e)=>Attach(e.Control);
        foreach(Control child in control.Controls)Attach(child);
        var strip=control as ToolStrip;if(strip!=null){foreach(ToolStripItem item in strip.Items)Attach(item);strip.ItemAdded+=(s,e)=>Attach(e.Item);}
        if(control.ContextMenuStrip!=null)Attach(control.ContextMenuStrip);
    }
    private static void Attach(ToolStripItem item)
    {
        Binding existing;if(bindings.TryGetValue(item,out existing))return;
        Bind(item,()=>item.Text,s=>item.Text=s,h=>item.TextChanged+=h);
        var drop=item as ToolStripDropDownItem;if(drop!=null){foreach(ToolStripItem child in drop.DropDownItems)Attach(child);drop.DropDown.ItemAdded+=(s,e)=>Attach(e.Item);}
    }
    private static void ApplyItem(ToolStripItem item)
    {Binding b;if(bindings.TryGetValue(item,out b))b.Apply();var drop=item as ToolStripDropDownItem;if(drop!=null)foreach(ToolStripItem child in drop.DropDownItems)ApplyItem(child);}
    public static void Apply(Control control)
    {
        control.SuspendLayout();Binding b;if(bindings.TryGetValue(control,out b))b.Apply();else Attach(control);
        Tips list;if(tips.TryGetValue(control,out list))foreach(var tip in list.Items)tip.Owner.SetToolTip(control,Text(tip.Text));
        foreach(Control child in control.Controls)Apply(child);
        var strip=control as ToolStrip;if(strip!=null)foreach(ToolStripItem item in strip.Items)ApplyItem(item);
        var combo=control as WheelSafeComboBox;if(combo!=null)combo.RefreshLanguage();
        if(control.ContextMenuStrip!=null)Apply(control.ContextMenuStrip);control.ResumeLayout(true);control.Invalidate(true);
    }
    public static void ToolTip(ToolTip owner,Control control,string text)
    {var list=tips.GetOrCreateValue(control);var previous=list.Items.FirstOrDefault(t=>t.Owner==owner);if(previous==null)list.Items.Add(new Tip{Owner=owner,Text=text});else previous.Text=text;owner.SetToolTip(control,Text(text));}
    public static DialogResult ShowDialog(Form form,IWin32Window owner){Attach(form);return form.ShowDialog(owner);}
    public static DialogResult ShowDialog(CommonDialog dialog,IWin32Window owner)
    {
        var file=dialog as FileDialog;if(file!=null){file.Title=Text(file.Title);file.Filter=Text(file.Filter);}
        var folder=dialog as FolderBrowserDialog;if(folder!=null)folder.Description=Text(folder.Description);return dialog.ShowDialog(owner);
    }
    public static DialogResult Message(IWin32Window owner,string message,string caption,MessageBoxButtons buttons=MessageBoxButtons.OK,MessageBoxIcon icon=MessageBoxIcon.None)
    {return MessageBox.Show(owner,Text(message),Text(caption),buttons,icon);}
    public static DialogResult Message(string message,string caption,MessageBoxButtons buttons=MessageBoxButtons.OK,MessageBoxIcon icon=MessageBoxIcon.None)
    {return MessageBox.Show(Text(message),Text(caption),buttons,icon);}
}

internal sealed partial class MainForm
{
    private readonly ComboBox languageChoice=new WheelSafeComboBox();
    private void BuildLanguageChoice(FlowLayoutPanel bar)
    {
        bar.Controls.Add(new Label{Text="切换语言",AutoSize=true,ForeColor=Theme.Muted,Margin=new Padding(10,10,3,0)});
        languageChoice.DropDownStyle=ComboBoxStyle.DropDownList;languageChoice.Width=125;languageChoice.Margin=new Padding(3,6,5,0);languageChoice.BackColor=Theme.Raised;languageChoice.ForeColor=Theme.Text;
        languageChoice.Items.AddRange(new object[]{"简体中文","English"});languageChoice.SelectedIndex=Language.English?1:0;
        languageChoice.SelectionChangeCommitted+=(s,e)=>{try{properties.CommitPending();Language.Select(root,languageChoice.SelectedIndex==1?"en":"zh-CN");Language.Apply(this);FillResources();ShowProperties();UpdateTitle();stage.Invalidate();timeline.Invalidate();}catch(Exception error){Error(error);}};bar.Controls.Add(languageChoice);
    }
}
