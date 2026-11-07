using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed class GmObjectEditor : LocalizedForm
{
    private static readonly KeyValuePair<string,string>[] ActionKinds={
        new KeyValuePair<string,string>("create","创建实例"),
        new KeyValuePair<string,string>("position","移动到位置"),
        new KeyValuePair<string,string>("motion","设置速度和方向"),
        new KeyValuePair<string,string>("alarm","设置闹钟"),
        new KeyValuePair<string,string>("sprite","更换贴图"),
        new KeyValuePair<string,string>("bounce","反弹"),
        new KeyValuePair<string,string>("script","执行脚本"),
        new KeyValuePair<string,string>("variable","设置变量"),
        new KeyValuePair<string,string>("destroy","销毁实例"),
        new KeyValuePair<string,string>("inherited","调用父事件"),
        new KeyValuePair<string,string>("code","执行 GML")
    };
    private readonly NativeBridge bridge;
    private readonly Inventory inventory;
    private GmAssetData data;
    private bool working,closing,rebuilding;
    private readonly ListBox eventsList=ItemList("gmObjectEvents"),actionsList=ItemList("gmObjectActions");
    private readonly Label actionHint=new Label{Dock=DockStyle.Fill,AutoSize=true,Padding=new Padding(4,7,4,5),ForeColor=Theme.Muted};
    private readonly Button addEvent,changeEvent,deleteEvent,addAction,editAction,deleteAction,up,down,finish;
    private readonly WheelSafeComboBox sprite,parent,mask;
    private readonly WheelSafeNumericUpDown depth;
    private readonly CheckBox visible,solid,persistent;
    public bool Changed {get;private set;}
    private GmAssetSection SelectedEvent {get{return eventsList.SelectedIndex<0?null:data.events[eventsList.SelectedIndex];}}
    private GmAssetAction SelectedAction {get{var selected=SelectedEvent;return actionsList.SelectedIndex<0||selected==null?null:selected.actions.ElementAtOrDefault(actionsList.SelectedIndex);}}

    public GmObjectEditor(NativeBridge bridge,Inventory inventory,GmAssetData asset)
    {
        this.bridge=bridge;this.inventory=inventory;data=asset;
        Text=Language.Text("GM 对象")+" · "+asset.name;Language.PreserveText(this);
        Size=new Size(1060,620);MinimumSize=new Size(900,470);StartPosition=FormStartPosition.CenterParent;
        Font=Theme.Font;BackColor=Theme.Back;ForeColor=Theme.Text;Icon=AppWindow.LoadIcon();ShowInTaskbar=false;
        var columns=new TableLayoutPanel{ColumnCount=3,RowCount=1,Dock=DockStyle.Fill,Padding=new Padding(8)};
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,218));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,260));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        columns.RowStyles.Add(new RowStyle(SizeType.Percent,100));

        var info=asset.@object;
        sprite=ResourceChoice(inventory.sprites,info.sprite,"（无贴图）");
        parent=ResourceChoice(inventory.objects.Where(name=>name!=asset.name),info.parent,"（无父对象）");
        mask=ResourceChoice(inventory.sprites,info.mask,"（与贴图相同）");
        depth=new WheelSafeNumericUpDown{Dock=DockStyle.Fill,Minimum=Int32.MinValue,Maximum=Int32.MaxValue,Value=info.depth};
        visible=Check("可见",info.visible);solid=Check("实心",info.solid);persistent=Check("跨房间保留",info.persistent);
        finish=GmResourceUi.Button("完成",async()=>await Finish());
        columns.Controls.Add(BuildProperties(),0,0);

        addEvent=GmResourceUi.Button("添加事件…",async()=>await PickEvent(false));
        changeEvent=GmResourceUi.Button("更换…",async()=>await PickEvent(true));
        deleteEvent=GmResourceUi.Button("删除",async()=>await DeleteEvent());
        var eventPane=Pane("对象事件",eventsList);
        Append(eventPane,Buttons(addEvent));
        Append(eventPane,Buttons(deleteEvent,changeEvent));
        columns.Controls.Add(eventPane,1,0);

        addAction=GmResourceUi.Button("添加动作…",async()=>await AddAction());
        editAction=GmResourceUi.Button("编辑…",async()=>await EditAction());
        deleteAction=GmResourceUi.Button("删除",async()=>await ChangeAction("delete-action",0));
        up=GmResourceUi.Button("↑",async()=>await ChangeAction("move-action",-1));
        down=GmResourceUi.Button("↓",async()=>await ChangeAction("move-action",1));
        var tips=new ToolTip();
        Language.ToolTip(tips,up,"上移");up.AccessibleName=Language.Text("上移");
        Language.ToolTip(tips,down,"下移");down.AccessibleName=Language.Text("下移");
        Disposed+=(s,e)=>tips.Dispose();
        var actionPane=Pane("动作",actionsList);
        Append(actionPane,actionHint);
        Append(actionPane,Buttons(addAction,editAction,deleteAction,up,down));
        up.MinimumSize=down.MinimumSize=new Size(38,32);
        columns.Controls.Add(actionPane,2,0);
        Controls.Add(columns);
        eventsList.SelectedIndexChanged+=(s,e)=>{if(!rebuilding)RefreshActions();};
        actionsList.SelectedIndexChanged+=(s,e)=>EnableCommands();
        actionsList.DoubleClick+=async(s,e)=>await EditAction();
        actionsList.KeyDown+=async(s,e)=>{
            if(e.KeyCode!=Keys.Enter)return;
            e.Handled=e.SuppressKeyPress=true;await EditAction();
        };
        FormClosing+=OnClosing;
        RefreshEvents(asset.events.Length>0?asset.events[0].id:null);
    }

    private static ListBox ItemList(string name)
    {
        var list=new ListBox{Name=name,Dock=DockStyle.Fill,BackColor=Theme.Back,ForeColor=Theme.Text,BorderStyle=BorderStyle.None,
            IntegralHeight=false,DrawMode=DrawMode.OwnerDrawFixed};
        list.FontChanged+=(s,e)=>list.ItemHeight=Math.Max(26,list.Font.Height+10);
        list.DrawItem+=(s,e)=>{
            if(e.Index<0||e.Index>=list.Items.Count)return;
            bool selected=(e.State&DrawItemState.Selected)!=0;
            using(var brush=new SolidBrush(selected?Theme.Line:Theme.Back))e.Graphics.FillRectangle(brush,e.Bounds);
            var bounds=Rectangle.Inflate(e.Bounds,-8,0);
            Theme.Label(e.Graphics,list.GetItemText(list.Items[e.Index]),bounds,Theme.Text,list.Font,localize:false);
            e.DrawFocusRectangle();
        };
        return list;
    }
    private static TableLayoutPanel Pane(string title,Control body)
    {
        var pane=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,BackColor=Theme.Panel,
            Padding=new Padding(9),Margin=new Padding(3)};
        pane.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        pane.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        pane.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        pane.Controls.Add(new Label{Text=title,AutoSize=true,Font=Theme.Bold,Margin=new Padding(2,3,0,9)},0,0);
        pane.Controls.Add(body,0,1);
        return pane;
    }
    private static void Append(TableLayoutPanel panel,Control control)
    {
        int row=panel.RowCount++;
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(control,0,row);
    }
    private static FlowLayoutPanel Buttons(params Button[] buttons)
    {
        var row=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Margin=new Padding(0,3,0,0)};
        foreach(var button in buttons){button.MinimumSize=new Size(63,32);row.Controls.Add(button);}
        return row;
    }
    private static WheelSafeComboBox ResourceChoice(IEnumerable<string> resources,string current,string empty)
    {
        var box=new WheelSafeComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,LocalizeItems=false,DropDownWidth=240};
        box.Items.Add(Language.Text(empty));
        box.Items.AddRange(resources.Concat(new[]{current}).Where(name=>!String.IsNullOrEmpty(name)).Distinct().ToArray());
        box.SelectedIndex=String.IsNullOrEmpty(current)?0:box.Items.IndexOf(current);
        return box;
    }
    private static string ResourceValue(ComboBox box){return box.SelectedIndex<=0?"":(string)box.SelectedItem;}
    private static CheckBox Check(string label,bool value)
    {return new CheckBox{Text=label,Checked=value,AutoSize=true,Margin=new Padding(4,5,0,5)};}
    private TableLayoutPanel BuildProperties()
    {
        var fields=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=1,RowCount=0};
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        var name=new Label{Text=data.name,AutoEllipsis=true,Height=26,Dock=DockStyle.Fill,ForeColor=Theme.Accent,Margin=new Padding(4,0,4,5)};
        Language.PreserveText(name);
        Append(fields,name);
        PropertyField(fields,"贴图",sprite);
        PropertyField(fields,"父对象",parent);
        PropertyField(fields,"碰撞遮罩",mask);
        PropertyField(fields,"对象深度",depth);
        var flags=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Margin=Padding.Empty};
        flags.Controls.Add(visible);flags.Controls.Add(solid);
        Append(fields,flags);
        Append(fields,persistent);
        var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true};
        scroll.Controls.Add(fields);
        var pane=Pane("对象属性",scroll);
        Append(pane,Buttons(finish));
        return pane;
    }
    private static void PropertyField(TableLayoutPanel panel,string label,Control input)
    {
        Append(panel,new Label{Text=label,AutoSize=true,ForeColor=Theme.Muted,Margin=new Padding(4,5,0,1)});
        Append(panel,input);
    }
    private void RefreshEvents(string select,int action=-1)
    {
        int top=eventsList.TopIndex;
        rebuilding=true;
        eventsList.BeginUpdate();
        try{
            eventsList.Items.Clear();
            foreach(var item in data.events)eventsList.Items.Add(GmResourceUi.EventName(item.id));
            if(data.events.Length>0)eventsList.TopIndex=Math.Max(0,Math.Min(top,data.events.Length-1));
            eventsList.SelectedIndex=Array.FindIndex(data.events,item=>item.id==select);
        }finally{eventsList.EndUpdate();rebuilding=false;}
        RefreshActions(action);
    }
    private void RefreshActions(int select=-1)
    {
        var selected=SelectedEvent;
        actionsList.BeginUpdate();
        try{
            actionsList.Items.Clear();
            if(selected!=null)foreach(var action in selected.actions)
                actionsList.Items.Add((action.index+1)+". "+ActionLabel(action));
            if(select>=0&&select<actionsList.Items.Count)actionsList.SelectedIndex=select;
        }finally{actionsList.EndUpdate();}
        EnableCommands();
    }
    private static string ActionLabel(GmAssetAction action)
    {
        var a=action.visual;
        if(a==null)return NativeActionLabel(action);
        string name=Language.Text(ActionKinds.First(kind=>kind.Key==a.kind).Value);
        switch(a.kind){
            case "create":name+=" · "+a.target+" ("+a.x+", "+a.y+")";break;
            case "position":name+=" · ("+a.x+", "+a.y+")";break;
            case "variable":name+=" · "+a.name+(a.relative?" += ":" = ")+a.value;break;
            case "motion":name+=" · "+a.speed+" / "+a.direction+"°";break;
            case "alarm":name+=" · Alarm "+a.alarm+" / "+a.frames;break;
            case "sprite":name+=" · "+(String.IsNullOrEmpty(a.target)?Language.Text("（请选择贴图）"):a.target);break;
            case "script":name+=" · "+(String.IsNullOrEmpty(a.target)?Language.Text("（请选择脚本）"):a.target);break;
        }
        if(a.appliesTo!="self"&&a.kind!="inherited")name+=" · "+(a.appliesTo=="other"?Language.Text("碰撞另一方（other）"):a.appliesTo);
        return name;
    }
    private static string NativeActionLabel(GmAssetAction action)
    {
        if(action.lib==1)switch(action.id){
            case 101:return Language.Text("按固定方向移动");
            case 102:return Language.Text("设置速度和方向");
            case 109:return Language.Text("移动到位置");
            case 115:return Language.Text("反弹");
            case 201:return Language.Text("创建实例");
            case 203:return Language.Text("销毁实例");
            case 301:return Language.Text("设置闹钟");
            case 321:return Language.Text("显示消息");
            case 501:return Language.Text("绘制贴图");
            case 601:return Language.Text("执行脚本");
            case 603:return Language.Text("执行 GML");
            case 604:return Language.Text("调用父事件");
            case 541:return Language.Text("更换贴图");
            case 611:return Language.Text("设置变量");
            case 612:return Language.Text("检查变量");
        }
        return Language.Text("原生动作")+" · "+action.lib+"/"+action.id;
    }
    private void EnableCommands()
    {
        var selected=SelectedEvent;
        var action=SelectedAction;
        addEvent.Enabled=finish.Enabled=!working;
        changeEvent.Enabled=deleteEvent.Enabled=addAction.Enabled=!working&&selected!=null;
        editAction.Enabled=!working&&action!=null&&(action.visual!=null?action.visual.kind!="inherited":action.editable);
        deleteAction.Enabled=!working&&action!=null;
        up.Enabled=!working&&action!=null&&action.index>0;
        down.Enabled=!working&&action!=null&&action.index+1<selected.actions.Length;
        eventsList.Enabled=actionsList.Enabled=!working;
        actionHint.Text=selected==null?"选择或添加一个事件。":selected.actions.Length==0?"此事件还没有动作。":
            action!=null&&action.visual!=null&&action.visual.kind=="inherited"?"调用当前事件在父对象中的实现。":
            action!=null&&action.visual==null&&!action.editable?"这个动作的参数可在 GameMaker 中编辑。":"双击动作编辑。动作按列表顺序执行。";
    }
    private async Task Update(object payload,string select,int action=-1)
    {
        data=await bridge.Call<GmAssetData>("gm-asset-edit",payload);
        Changed=true;RefreshEvents(select,action);
    }
    private async Task Run(Func<Task> work)
    {
        if(working)return;
        working=true;EnableCommands();
        try{await work();}
        catch(Exception error){Language.Message(this,error.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Error);}
        finally{working=false;EnableCommands();}
    }

    private async Task PickEvent(bool change)
    {
        var current=SelectedEvent;
        if(working||(change&&current==null))return;
        string title=change?"更换事件…":"添加事件…";
        var ids=new[]{"Create_0","Destroy_0","Step_0","Step_1","Step_2","Draw_0","Other_4","Other_5","Other_2","Other_3","Other_0","Other_1","Other_6","Other_7"}
            .Where(id=>!data.events.Any(e=>e.id==id)).ToList();
        ids.AddRange(new[]{"Alarm","Collision","Keyboard","KeyPress","KeyRelease","User"});
        var labels=ids.Select(id=>id=="Alarm"?"闹钟…":id=="Collision"?"碰撞…":id=="Keyboard"?"按住按键…":
            id=="KeyPress"?"按下按键…":id=="KeyRelease"?"松开按键…":id=="User"?"用户事件…":GmResourceUi.EventName(id))
            .Select(Language.Text).ToArray();
        string label=GmResourceUi.TextEntry(this,title,"",labels);
        if(label==null)return;
        string next=ids[Array.IndexOf(labels,label)];
        if(next=="Alarm"||next=="User"||next=="Collision"){
            var variants=next=="Alarm"?Enumerable.Range(0,12).Select(i=>"Alarm_"+i):
                next=="User"?Enumerable.Range(10,16).Select(i=>"Other_"+i):inventory.objects.Select(n=>"Collision_"+n);
            var available=variants.Except(data.events.Select(e=>e.id)).ToArray();
            if(available.Length==0){Language.Message(this,"没有可添加的同类事件。",title);return;}
            label=GmResourceUi.TextEntry(this,title,"",available.Select(GmResourceUi.EventName).ToArray());
            if(label==null)return;
            next=available.First(id=>GmResourceUi.EventName(id)==label);
        }else if(next.StartsWith("Key",StringComparison.Ordinal)){
            var keys=new[]{32,37,38,39,40,13,27,16,17,18,8,9,0,1}
                .Concat(Enumerable.Range(65,26)).Concat(Enumerable.Range(48,10)).Concat(Enumerable.Range(112,12)).ToArray();
            var options=keys.Select(GmResourceUi.KeyName).Concat(new[]{Language.Text("其他按键…")}).ToArray();
            string choice=GmResourceUi.TextEntry(this,title,"",options);
            if(choice==null)return;
            int index=Array.IndexOf(options,choice),key;
            if(index==keys.Length){
                string value=GmResourceUi.TextEntry(this,"输入按键码（0–255）","32");
                if(value==null)return;
                if(!Int32.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out key)||key>255){
                    Language.Message(this,"请输入 0 到 255 的按键码。",title);return;
                }
            }else{
                key=keys[index];
            }
            next+="_"+key;
        }
        await Run(async()=>{
            if(change)await Update(new{data.kind,data.name,data.revision,operation="change-section",section=current.id,nextSection=next},next);
            else await Update(new{data.kind,data.name,data.revision,operation="add",section=next,empty=true},next);
        });
    }
    private bool Confirm(string message)
    {return Language.Message(this,message,Text,MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes;}
    private async Task DeleteEvent()
    {
        var current=SelectedEvent;
        if(working||current==null||!Confirm("删除所选事件和其中的动作？"))return;
        var neighbor=data.events.ElementAtOrDefault(eventsList.SelectedIndex+1)??data.events.ElementAtOrDefault(eventsList.SelectedIndex-1);
        await Run(()=>Update(new{data.kind,data.name,data.revision,operation="delete-section",section=current.id},neighbor==null?null:neighbor.id));
    }
    private async Task AddAction()
    {
        var current=SelectedEvent;
        if(working||current==null)return;
        var labels=ActionKinds.Select(item=>Language.Text(item.Value)).ToArray();
        string label=GmResourceUi.TextEntry(this,"添加动作…","",labels);
        if(label==null)return;
        string kind=ActionKinds[Array.IndexOf(labels,label)].Key;
        await Run(async()=>{
            if(kind=="code"){
                using(var editor=new CodeForm(data.name+" · "+GmResourceUi.EventName(current.id),"",content=>
                    Update(new{data.kind,data.name,data.revision,operation="add-action",section=current.id,content},current.id,current.actions.Length)))
                    Language.ShowDialog(editor,this);
            }else{
                var choices=kind=="sprite"?inventory.sprites:kind=="script"?inventory.scripts:inventory.objects;
                var visual=new GmVisualAction{kind=kind,target=(choices??new string[0]).FirstOrDefault()??""};
                if(kind!="inherited"&&!VisualFields(visual))return;
                await Update(new{data.kind,data.name,data.revision,operation="add-action",section=current.id,visual},current.id,current.actions.Length);
            }
        });
    }
    private bool VisualFields(GmVisualAction action)
    {
        string category=ActionKinds.First(kind=>kind.Key==action.kind).Value;
        return GmResourceUi.EditFields(this,category,ActionFields(action),action.kind=="script"?new Size(560,520):new Size(520,370));
    }
    private NativeProperties ActionFields(GmVisualAction action)
    {
        string category=ActionKinds.First(kind=>kind.Key==action.kind).Value;
        var fields=new NativeProperties();
        if(action.kind=="inherited")return fields;
        var labels=new[]{"自身（self）","碰撞另一方（other）"}.Concat(inventory.objects??new string[0]).ToArray();
        var targets=new[]{"self","other"}.Concat(inventory.objects??new string[0]).ToArray();
        if(!targets.Contains(action.appliesTo)){
            labels=labels.Concat(new[]{String.IsNullOrEmpty(action.appliesTo)?"（无对象）":action.appliesTo}).ToArray();
            targets=targets.Concat(new[]{action.appliesTo}).ToArray();
        }
        fields.Add("作用对象",category,typeof(string),()=>labels[Array.IndexOf(targets,action.appliesTo)],
            v=>action.appliesTo=targets[Array.IndexOf(labels,(string)v)],labels);
        if(action.kind=="create")fields.Add("GM 对象",category,typeof(string),()=>action.target,v=>action.target=(string)v,inventory.objects);
        if(action.kind=="create"||action.kind=="position"){
            fields.Add("X",category,typeof(string),()=>action.x,v=>action.x=(string)v);
            fields.Add("Y",category,typeof(string),()=>action.y,v=>action.y=(string)v);
            fields.Add("相对于当前位置",category,typeof(bool),()=>action.relative,v=>action.relative=(bool)v);
        }else if(action.kind=="motion"){
            fields.Add("方向 / 度",category,typeof(string),()=>action.direction,v=>action.direction=(string)v);
            fields.Add("速度 / 像素每帧",category,typeof(string),()=>action.speed,v=>action.speed=(string)v);
            fields.Add("相对设置",category,typeof(bool),()=>action.relative,v=>action.relative=(bool)v);
        }else if(action.kind=="alarm"){
            fields.Add("闹钟编号",category,typeof(string),()=>action.alarm,v=>action.alarm=(string)v,Enumerable.Range(0,12).Select(i=>i.ToString(CultureInfo.InvariantCulture)).ToArray());
            fields.Add("等待帧数",category,typeof(string),()=>action.frames,v=>action.frames=(string)v);
            fields.Add("加到当前值",category,typeof(bool),()=>action.relative,v=>action.relative=(bool)v);
        }else if(action.kind=="sprite"||action.kind=="script"){
            bool image=action.kind=="sprite";
            string empty=image?"（请选择贴图）":"（请选择脚本）";
            var resources=image?inventory.sprites:inventory.scripts;
            var choices=(resources??new string[0]).Concat(new[]{action.target}).Where(v=>!String.IsNullOrEmpty(v)).Distinct();
            if(String.IsNullOrEmpty(action.target))choices=new[]{empty}.Concat(choices);
            fields.Add(image?"贴图":"脚本",category,typeof(string),()=>String.IsNullOrEmpty(action.target)?empty:action.target,
                v=>action.target=(string)v==empty?"":(string)v,choices.ToArray());
            if(image){
                fields.Add("动画子图",category,typeof(string),()=>action.subimage,v=>action.subimage=(string)v);
                fields.Add("动画速度",category,typeof(string),()=>action.animationSpeed,v=>action.animationSpeed=(string)v);
            }else{
                for(int i=0;i<action.arguments.Length;i++){
                    int index=i;
                    fields.Add("argument"+i,category,typeof(string),()=>action.arguments[index],v=>action.arguments[index]=(string)v);
                }
            }
        }else if(action.kind=="bounce"){
            fields.Add("精确反弹",category,typeof(bool),()=>action.precise=="1",v=>action.precise=(bool)v?"1":"0");
            fields.Add("仅碰实心对象",category,typeof(bool),()=>action.collisionAll!="1",v=>action.collisionAll=(bool)v?"0":"1");
        }else if(action.kind=="variable"){
            fields.Add("变量名称",category,typeof(string),()=>action.name,v=>action.name=(string)v);
            fields.Add("数值",category,typeof(string),()=>action.value,v=>action.value=(string)v);
            fields.Add("加到当前值",category,typeof(bool),()=>action.relative,v=>action.relative=(bool)v);
        }
        return fields;
    }
    private async Task EditAction()
    {
        var current=SelectedEvent;var action=SelectedAction;
        if(!editAction.Enabled||current==null||action==null)return;
        await Run(async()=>{
            if(action.visual!=null){
                var visual=action.visual.Snapshot();
                if(VisualFields(visual))
                    await Update(new{data.kind,data.name,data.revision,operation="visual-action",section=current.id,action=action.index,visual},current.id,action.index);
            }else{
                var source=await bridge.Call<SourceData>("gm-asset-read",new{data.kind,data.name,section=current.id,action=action.index});
                if(source.hash!=data.revision)throw new InvalidOperationException(Language.Text("GM 资源已被修改，请重新打开后再编辑。"));
                using(var editor=new CodeForm(data.name+" · "+GmResourceUi.EventName(current.id),source.content,content=>
                    Update(new{data.kind,data.name,revision=source.hash,operation="code",section=current.id,action=action.index,content},current.id,action.index)))
                    Language.ShowDialog(editor,this);
            }
        });
    }
    private async Task ChangeAction(string operation,int direction)
    {
        var current=SelectedEvent;var action=SelectedAction;
        if(working||current==null||action==null)return;
        if(direction==0&&!Confirm("删除所选动作？"))return;
        int select=direction==0?Math.Min(action.index,current.actions.Length-2):action.index+direction;
        await Run(()=>Update(new{data.kind,data.name,data.revision,operation,section=current.id,action=action.index,direction},current.id,select));
    }
    private GmObjectData ReadProperties()
    {return new GmObjectData{sprite=ResourceValue(sprite),parent=ResourceValue(parent),mask=ResourceValue(mask),
        visible=visible.Checked,solid=solid.Checked,persistent=persistent.Checked,depth=(int)depth.Value};}
    private bool HasEdits(GmObjectData draft)
    {
        var source=data.@object;
        return draft.sprite!=source.sprite||draft.parent!=source.parent||draft.mask!=source.mask||draft.depth!=source.depth||
            draft.visible!=source.visible||draft.solid!=source.solid||draft.persistent!=source.persistent;
    }
    private async Task Finish()
    {
        if(working)return;
        var draft=ReadProperties();
        var selected=SelectedEvent;
        if(HasEdits(draft))await Run(()=>Update(new{data.kind,data.name,data.revision,operation="settings",@object=draft},selected==null?null:selected.id));
        if(HasEdits(draft))return;
        closing=true;Close();
    }
    private async void OnClosing(object sender,FormClosingEventArgs e)
    {
        if(closing)return;
        if(working){e.Cancel=true;return;}
        if(!HasEdits(ReadProperties()))return;
        var result=Language.Message(this,"保存左侧的对象属性？",Text,MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question);
        e.Cancel=result!=DialogResult.No;
        if(result==DialogResult.Yes){
            await Task.Yield();
            await Finish();
        }
    }
}
