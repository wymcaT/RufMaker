using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed class GmAssetData
{
    public string name="",kind="",revision="";
    public GmObjectData @object=null;
    public GmAssetSection[] events=new GmAssetSection[0];
    public GmPath path=null;
}
internal sealed class GmObjectData
{public string sprite="",parent="",mask="";public int depth;public bool visible=true,solid,persistent;}
internal sealed class GmAssetSection
{public string id="";public GmAssetAction[] actions=new GmAssetAction[0];}
internal sealed class GmAssetAction
{public int index=0,id=0,lib=0;public bool editable=false;public GmVisualAction visual=null;}
internal sealed class GmVisualAction
{
    public string kind="",x="0",y="0",target="",name="speed",value="0",appliesTo="self";
    public string speed="0",direction="0",frames="50",alarm="0",subimage="0",animationSpeed="1",precise="0",collisionAll="0";
    public string[] arguments=new[]{"0","0","0","0","0"};
    public bool relative;
    public GmVisualAction Snapshot(){var copy=(GmVisualAction)MemberwiseClone();copy.arguments=(string[])arguments.Clone();return copy;}
}
internal sealed class GmInstanceData
{public string content="",hash="";public GmInstanceField[] fields=new GmInstanceField[0];}
internal sealed class GmInstanceField
{public string name="",type="",spec="",description="",value="";public string[] depends=new string[0];}

internal static class GmResourceUi
{
    private sealed class ResourceButton : Button
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            if(Enabled){base.OnPaint(e);return;}
            e.Graphics.Clear(BackColor);
            Theme.Label(e.Graphics,Text,ClientRectangle,Theme.Muted,Font,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter,localize:false);
        }
    }
    public static Button Button(string title,Action click)
    {
        var button=new ResourceButton{Text=title,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,MinimumSize=new Size(78,32),Padding=new Padding(7,0,7,0),
            FlatStyle=FlatStyle.Flat,BackColor=Theme.Raised,ForeColor=Theme.Text,UseVisualStyleBackColor=false,Margin=new Padding(4)};
        button.FlatAppearance.BorderSize=0;button.Click+=(s,e)=>click();return button;
    }
    public static Form Dialog(string title,Size size)
    {
        return new LocalizedForm{Text=title,Size=size,MinimumSize=size,StartPosition=FormStartPosition.CenterParent,ShowInTaskbar=false,
            Font=Theme.Font,BackColor=Theme.Panel,ForeColor=Theme.Text,Icon=AppWindow.LoadIcon()};
    }
    public static FlowLayoutPanel Footer()
    {return new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=48,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(3)};}
    public static string TextEntry(IWin32Window owner,string title,string initial,string[] choices=null)
    {
        using(var dialog=Dialog(title,new Size(420,146))){
            dialog.FormBorderStyle=FormBorderStyle.FixedDialog;dialog.MinimizeBox=false;dialog.MaximizeBox=false;
            var panel=new Panel{Dock=DockStyle.Top,Height=47,Padding=new Padding(12,11,12,0)};
            Func<string> value;
            if(choices==null){
                var input=new TextBox{Dock=DockStyle.Top,BackColor=Theme.Raised,ForeColor=Theme.Text,Text=initial};
                panel.Controls.Add(input);value=()=>input.Text.Trim();dialog.Shown+=(s,e)=>{input.Focus();input.SelectAll();};
            }else{
                var input=new WheelSafeComboBox{Dock=DockStyle.Top,DropDownStyle=ComboBoxStyle.DropDownList,BackColor=Theme.Raised,ForeColor=Theme.Text,LocalizeItems=false};
                input.Items.AddRange(choices);if(choices.Length>0)input.SelectedIndex=0;
                panel.Controls.Add(input);value=()=>input.SelectedItem as string;
            }
            var footer=Footer();var cancel=Button("取消",()=>dialog.DialogResult=DialogResult.Cancel);
            var ok=Button("确定",()=>{if(!String.IsNullOrWhiteSpace(value()))dialog.DialogResult=DialogResult.OK;});
            footer.Controls.Add(cancel);footer.Controls.Add(ok);dialog.Controls.Add(panel);dialog.Controls.Add(footer);dialog.AcceptButton=ok;dialog.CancelButton=cancel;
            return Language.ShowDialog(dialog,owner)==DialogResult.OK?value():null;
        }
    }
    public static bool EditFields(IWin32Window owner,string title,NativeProperties bag,Size size)
    {
        using(var dialog=Dialog(title,size)){
            var inspector=new ParameterInspector{Dock=DockStyle.Fill,HelpVisible=false,SelectedObject=bag};
            var footer=Footer();var cancel=Button("取消",()=>dialog.DialogResult=DialogResult.Cancel);
            var ok=Button("保存",()=>{inspector.CommitPending();dialog.DialogResult=DialogResult.OK;});
            footer.Controls.Add(cancel);footer.Controls.Add(ok);dialog.Controls.Add(inspector);dialog.Controls.Add(footer);
            dialog.AcceptButton=ok;dialog.CancelButton=cancel;
            return Language.ShowDialog(dialog,owner)==DialogResult.OK;
        }
    }
    public static string EventName(string id)
    {
        switch(id){
            case "Create_0":return Language.Text("创建（Create）");
            case "Destroy_0":return Language.Text("销毁（Destroy）");
            case "Step_0":return "Step";
            case "Step_1":return "Begin Step";
            case "Step_2":return "End Step";
            case "Draw_0":return "Draw";
            case "Other_0":return Language.Text("离开房间");
            case "Other_1":return Language.Text("碰到房间边缘");
            case "Other_2":return Language.Text("游戏开始");
            case "Other_3":return Language.Text("游戏结束");
            case "Other_4":return Language.Text("房间开始（Room Start）");
            case "Other_5":return Language.Text("房间结束（Room End）");
            case "Other_6":return Language.Text("生命耗尽");
            case "Other_7":return Language.Text("动画结束");
        }
        if(id.StartsWith("Alarm_",StringComparison.Ordinal))return "Alarm "+id.Substring(6);
        if(id.StartsWith("Collision_",StringComparison.Ordinal))return Language.Text("碰撞 · ")+id.Substring(10);
        int n;
        if(id.StartsWith("Other_",StringComparison.Ordinal)&&Int32.TryParse(id.Substring(6),out n)&&n>=10&&n<=25)
            return Language.Text("用户事件")+" "+(n-10);
        foreach(var key in new[]{new[]{"KeyPress_","按下按键"},new[]{"KeyRelease_","松开按键"},new[]{"Keyboard_","按住按键"}})
            if(id.StartsWith(key[0],StringComparison.Ordinal)&&Int32.TryParse(id.Substring(key[0].Length),out n))
                return Language.Text(key[1])+" · "+KeyName(n);
        return id;
    }
    public static string KeyName(int value)
    {
        if(value==0)return Language.Text("无按键");
        if(value==1)return Language.Text("任意按键");
        if(value==32)return Language.Text("空格");
        if(value>=48&&value<=57)return ((char)value).ToString();
        return Enum.GetName(typeof(Keys),value)??value.ToString(CultureInfo.InvariantCulture);
    }
}

internal sealed class GmAssetEditor : LocalizedForm
{
    private readonly NativeBridge bridge;
    private readonly ListView sections=new ListView{View=View.Details,FullRowSelect=true,MultiSelect=false,HideSelection=false,Dock=DockStyle.Fill,BackColor=Theme.Panel,ForeColor=Theme.Text,BorderStyle=BorderStyle.None};
    private readonly Button edit,move;
    private GmAssetData data;
    private bool working;
    public bool Changed {get;private set;}
    private GmAssetSection Selected {get{return sections.SelectedItems.Count==0?null:sections.SelectedItems[0].Tag as GmAssetSection;}}

    public GmAssetEditor(NativeBridge bridge,GmAssetData asset,string section=null)
    {
        this.bridge=bridge;data=asset;
        Text=Language.Text("原生 Timeline")+" · "+data.name;Size=new Size(620,485);MinimumSize=new Size(430,320);
        StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;Font=Theme.Font;Icon=AppWindow.LoadIcon();BackColor=Theme.Panel;ForeColor=Theme.Text;
        sections.Columns.Add(Language.Text("原生帧"),320);sections.Columns.Add(Language.Text("动作"),110);
        sections.Resize+=(s,e)=>sections.Columns[0].Width=Math.Max(110,sections.ClientSize.Width-145);
        sections.SelectedIndexChanged+=(s,e)=>EnableCommands();
        sections.DoubleClick+=async(s,e)=>await EditAction();
        sections.KeyDown+=async(s,e)=>{if(e.KeyCode==Keys.Enter){e.Handled=true;await EditAction();}};
        var context=new ContextMenuStrip{BackColor=Theme.Panel,ForeColor=Theme.Text,ShowImageMargin=false};
        var raw=context.Items.Add("编辑整个源文件",null,async(s,e)=>await EditWholeSource());
        context.Opening+=(s,e)=>{raw.Enabled=!working;Language.Attach(context);};
        sections.ContextMenuStrip=context;Disposed+=(s,e)=>context.Dispose();
        var footer=GmResourceUi.Footer();
        edit=GmResourceUi.Button("编辑 GML",async()=>await EditAction());footer.Controls.Add(edit);
        move=GmResourceUi.Button("移动到帧…",async()=>await ChangeSection(true));footer.Controls.Add(move);
        footer.Controls.Add(GmResourceUi.Button("添加原生帧…",async()=>await ChangeSection(false)));
        Controls.Add(sections);Controls.Add(footer);
        FormClosing+=(s,e)=>{if(working)e.Cancel=true;};
        Rebuild(section);
        if(section!=null)Shown+=async(s,e)=>await EditAction();
    }
    private void EnableCommands()
    {if(edit==null||move==null)return;edit.Enabled=!working&&Selected!=null;move.Enabled=edit.Enabled;}
    private void Rebuild(string select)
    {
        sections.BeginUpdate();sections.Items.Clear();
        foreach(var section in data.events){
            int code=section.actions.Count(action=>action.editable);
            var item=new ListViewItem(section.id){Tag=section};
            item.SubItems.Add(code+" GML / "+section.actions.Length);sections.Items.Add(item);
            if(section.id==select){item.Selected=true;item.Focused=true;item.EnsureVisible();}
        }
        sections.EndUpdate();EnableCommands();
    }
    private async Task Update(object payload,string select)
    {
        data=await bridge.Call<GmAssetData>("gm-asset-edit",payload);
        Changed=true;Rebuild(select);
    }
    private async Task ChangeSection(bool shift)
    {
        if(working||shift&&Selected==null)return;
        string current=Selected==null?null:Selected.id;
        string title=shift?"移动到帧…":"添加原生帧…";
        string value=GmResourceUi.TextEntry(this,title,current??"0");
        if(value==null)return;
        int at;
        if(!Int32.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out at)||at<0||at>1000000){Language.Message(this,"请输入有效的原生帧编号。",title);return;}
        string id=at.ToString(CultureInfo.InvariantCulture);
        try{working=true;await Update(new{data.kind,data.name,data.revision,operation=shift?"move":"add",section=shift?current:id,frame=at},id);}
        catch(Exception error){Language.Message(this,error.Message,Text);}
        finally{working=false;EnableCommands();}
    }
    private async Task EditAction()
    {
        var selected=Selected;if(working||selected==null)return;
        try{
            working=true;
            var codes=selected.actions.Where(action=>action.editable).ToArray();
            int index;
            if(codes.Length==0){
                if(Language.Message(this,"此事件没有 GML 动作。要追加一个吗？",Text,MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
                await Update(new{data.kind,data.name,data.revision,operation="add-action",section=selected.id},selected.id);
                index=data.events.Single(row=>row.id==selected.id).actions.Last().index;
            }else if(codes.Length==1)index=codes[0].index;
            else{
                var labels=codes.Select(item=>Language.Text("GML 动作")+" "+(item.index+1)).ToArray();
                string label=GmResourceUi.TextEntry(this,"选择 GML 动作","",labels);if(label==null)return;index=codes[Array.IndexOf(labels,label)].index;
            }
            var source=await bridge.Call<SourceData>("gm-asset-read",new{data.kind,data.name,section=selected.id,action=index});
            using(var editor=new CodeForm(data.name+" · "+selected.id,source.content,
                async content=>{await Update(new{data.kind,data.name,revision=source.hash,operation="code",section=selected.id,action=index,content},selected.id);source.hash=data.revision;}))
                Language.ShowDialog(editor,this);
        }catch(Exception error){Language.Message(this,error.Message,Text);}
        finally{working=false;EnableCommands();}
    }
    private async Task EditWholeSource()
    {
        if(working)return;
        try{
            working=true;
            var source=await bridge.Call<SourceData>("gm-asset-read",new{data.kind,data.name,whole=true});
            string selected=Selected==null?null:Selected.id;
            using(var editor=new CodeForm(data.name+".gml",source.content,
                async content=>{await Update(new{data.kind,data.name,revision=source.hash,operation="source",content},selected);source.hash=data.revision;}))
                Language.ShowDialog(editor,this);
        }catch(Exception error){Language.Message(this,error.Message,Text);}
        finally{working=false;EnableCommands();}
    }
}
