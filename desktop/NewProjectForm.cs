using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

#pragma warning disable 0649 // Fields are populated through reflection or JSON.
internal sealed class NewProjectOptions
{
    public string name="新耐久",parent,song="",preset="floor";
    public int width=800,height=608,fps=50,platformTiles=5,leftMarginTiles=3,gapTiles=2,topTiles=3,spanTiles=12,leftLevels=5,rightLevels=4,maxJumps=2;
    public bool floor,sideWalls,ceiling,oneWay,autoSpawn=true,infiniteJump;
    public double spawnX=400,spawnY=564,gravity=.4,moveSpeed=3,jumpSpeed=8.5,doubleJumpSpeed=7;
}
internal sealed class TerrainCell {public int x,y,width,height;public string kind;}
internal sealed class TerrainLayout {public int width,height;public string preset;public double spawnX,spawnY;public TerrainCell[] cells;}

internal sealed class NewProjectForm : Form
{
    readonly TableLayoutPanel fields=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,Padding=new Padding(8)};
    readonly TextBox projectName=new TextBox{Text="新耐久"},parent=new TextBox(),song=new TextBox();
    readonly WheelSafeComboBox preset=new WheelSafeComboBox{DropDownStyle=ComboBoxStyle.DropDownList};
    readonly Dictionary<string,NumericUpDown> numbers=new Dictionary<string,NumericUpDown>();
    readonly Dictionary<string,CheckBox> checks=new Dictionary<string,CheckBox>();
    readonly TerrainPreview preview=new TerrainPreview();readonly Label info=new Label{Dock=DockStyle.Bottom,Height=62,ForeColor=Theme.Muted,Padding=new Padding(8)};
    readonly Button create=new Button{Text="创建工程",AutoSize=true};readonly Timer timer=new Timer{Interval=160};
    readonly Func<NewProjectOptions,Task<TerrainLayout>> render;int request;bool valid;
    public NewProjectOptions Options {get;private set;}
    public NewProjectForm(string defaultParent,Func<NewProjectOptions,Task<TerrainLayout>> render)
    {
        this.render=render;Text="新建 RufMaker 工程";Font=Theme.Font;ForeColor=Theme.Text;BackColor=Theme.Panel;Size=new Size(1000,760);MinimumSize=new Size(900,650);StartPosition=FormStartPosition.CenterParent;Icon=AppWindow.LoadIcon();ShowInTaskbar=false;
        foreach(var input in new[]{projectName,parent,song}){input.BackColor=Theme.Raised;input.ForeColor=Theme.Text;input.BorderStyle=BorderStyle.FixedSingle;}preset.BackColor=Theme.Raised;preset.ForeColor=Theme.Text;create.BackColor=Theme.Accent;create.ForeColor=Theme.Back;create.FlatStyle=FlatStyle.Flat;
        var body=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,Padding=new Padding(8)};body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,53));body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,47));
        var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true};fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,48));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,52));scroll.Controls.Add(fields);body.Controls.Add(scroll,0,0);
        var right=new Panel{Dock=DockStyle.Fill,Padding=new Padding(8)};right.Controls.Add(preview);right.Controls.Add(info);body.Controls.Add(right,1,0);Controls.Add(body);
        parent.Text=defaultParent;Row("工程名称",projectName);Row("保存到文件夹",Browse(parent,false));Row("歌曲（可稍后导入）",Browse(song,true));
        Heading("房间与初始地形");preset.Items.AddRange(new object[]{"标准底板","视频式双列错层平台","中央平台","空房间"});preset.SelectedIndex=0;Row("地形预设",preset);
        Number("width","房间宽 / 像素",800,320,3840,32);Number("height","房间高 / 像素",608,240,2160,32);Number("fps","游戏 FPS",50,15,240,1);
        Check("floor","附加底板",false);Check("sideWalls","左右边墙",false);Check("ceiling","顶部封闭",false);Check("oneWay","悬空台使用单向平台",false);
        Heading("平台排列 · 每格 32 像素");Number("platformTiles","平台宽度 / 格",5,1,40,1);Number("leftLevels","左列层数",5,1,20,1);Number("rightLevels","右列层数",4,1,20,1);Number("leftMarginTiles","左侧留空 / 格",3,0,100,1);Number("gapTiles","两列间留空 / 格",2,0,60,1);Number("topTiles","顶部位置 / 格",3,0,60,1);Number("spanTiles","最高到最低的跨度 / 格",12,1,60,1);
        Heading("玩家出生与物理");Check("autoSpawn","自动站在最低平台上",true);Number("spawnX","出生 X",400,0,3840,1);Number("spawnY","出生 Y",564,0,2160,1);
        Number("gravity","重力 / 每帧",.4,0,3,.05,2);Number("moveSpeed","水平速度",3,0,30,.1,2);Number("jumpSpeed","一段跳速度",8.5,0,40,.1,2);Number("doubleJumpSpeed","后续跳速度",7,0,40,.1,2);Number("maxJumps","最大跳跃次数",2,1,8,1);Check("infiniteJump","无限跳跃",false);
        foreach(Control field in new Control[]{projectName,parent,song})field.TextChanged+=(s,e)=>Schedule();preset.SelectedIndexChanged+=(s,e)=>Schedule();
        timer.Tick+=async(s,e)=>{timer.Stop();await RefreshPreview();};Shown+=(s,e)=>Schedule();
        var footer=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(8)};var cancel=new Button{Text="取消",DialogResult=DialogResult.Cancel,AutoSize=true};footer.Controls.Add(cancel);footer.Controls.Add(create);Controls.Add(footer);CancelButton=cancel;AcceptButton=create;
        create.Click+=(s,e)=>{if(!valid)return;var options=ReadOptions();if(String.IsNullOrWhiteSpace(options.name)||!Directory.Exists(options.parent)){info.Text="请输入工程名称并选择存在的父文件夹。";return;}Options=options;DialogResult=DialogResult.OK;Close();};
    }
    Control Browse(TextBox input,bool music)
    {
        var p=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,AutoSize=true,Margin=Padding.Empty};p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,54));input.Dock=DockStyle.Fill;var button=new Button{Text="选择",Dock=DockStyle.Fill};p.Controls.Add(input,0,0);p.Controls.Add(button,1,0);
        button.Click+=(s,e)=>{if(music){using(var d=new OpenFileDialog{Title="选择耐久歌曲",Filter="音频|*.ogg;*.mp3;*.wav;*.flac"})if(d.ShowDialog(this)==DialogResult.OK)input.Text=d.FileName;}else using(var d=new FolderBrowserDialog{Description="选择新工程的父文件夹",SelectedPath=input.Text})if(d.ShowDialog(this)==DialogResult.OK)input.Text=d.SelectedPath;};return p;
    }
    void Heading(string text){var label=new Label{Text=text,ForeColor=Theme.Accent,Font=Theme.Bold,AutoSize=true,Margin=new Padding(0,16,0,8)};int row=fields.RowCount++;fields.Controls.Add(label,0,row);fields.SetColumnSpan(label,2);}
    void Row(string label,Control editor){int row=fields.RowCount++;fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));fields.Controls.Add(new Label{Text=label,AutoSize=true,Margin=new Padding(0,7,4,5)},0,row);editor.Dock=DockStyle.Fill;editor.Margin=new Padding(1,3,1,3);fields.Controls.Add(editor,1,row);}
    void Number(string key,string label,double value,double min,double max,double increment,int decimals=0)
    {var n=new WheelSafeNumericUpDown{Minimum=(decimal)min,Maximum=(decimal)max,Value=(decimal)value,Increment=(decimal)increment,DecimalPlaces=decimals,BackColor=Theme.Raised,ForeColor=Theme.Text};numbers[key]=n;n.ValueChanged+=(s,e)=>Schedule();Row(label,n);}
    void Check(string key,string label,bool value){var c=new CheckBox{Checked=value,AutoSize=true};checks[key]=c;c.CheckedChanged+=(s,e)=>Schedule();Row(label,c);}
    NewProjectOptions ReadOptions()
    {
        var o=new NewProjectOptions{name=projectName.Text.Trim(),parent=parent.Text,song=song.Text,preset=new[]{"floor","staggered","center","empty"}[Math.Max(0,preset.SelectedIndex)]};
        foreach(var pair in numbers){var f=typeof(NewProjectOptions).GetField(pair.Key);f.SetValue(o,f.FieldType==typeof(int)?(object)(int)pair.Value.Value:(object)(double)pair.Value.Value);}
        foreach(var pair in checks)typeof(NewProjectOptions).GetField(pair.Key).SetValue(o,pair.Value.Checked);return o;
    }
    void Schedule()
    {
        request++;
        bool staggered=preset.SelectedIndex==1;foreach(string key in new[]{"leftLevels","rightLevels","leftMarginTiles","gapTiles","topTiles","spanTiles"})if(numbers.ContainsKey(key))numbers[key].Enabled=staggered;
        if(numbers.ContainsKey("platformTiles"))numbers["platformTiles"].Enabled=staggered||preset.SelectedIndex==2;
        if(checks.ContainsKey("autoSpawn")){numbers["spawnX"].Enabled=numbers["spawnY"].Enabled=!checks["autoSpawn"].Checked;}valid=false;create.Enabled=false;timer.Stop();timer.Start();
    }
    async Task RefreshPreview()
    {
        int id=++request;try{var layout=await render(ReadOptions());if(IsDisposed||id!=request)return;preview.Data=layout;preview.Invalidate();info.Text=String.Format("出生点：{0:0}, {1:0}\n地形会写入原生 GameMaker 房间，可继续在房间编辑器内修改。",layout.spawnX,layout.spawnY);valid=true;create.Enabled=true;}catch(Exception e){if(!IsDisposed&&id==request){valid=false;create.Enabled=false;info.Text=e.Message;}}
    }
    protected override void OnFormClosed(FormClosedEventArgs e){timer.Stop();request++;base.OnFormClosed(e);}
    protected override void Dispose(bool disposing){if(disposing){request++;timer.Dispose();}base.Dispose(disposing);}
    sealed class TerrainPreview : Control
    {
        public TerrainLayout Data;
        public TerrainPreview(){Dock=DockStyle.Fill;DoubleBuffered=true;ResizeRedraw=true;}
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);var g=e.Graphics;g.Clear(Theme.Back);if(Data==null)return;float scale=Math.Min((Width-24)/(float)Data.width,(Height-24)/(float)Data.height),x=(Width-Data.width*scale)/2,y=(Height-Data.height*scale)/2;
            g.TranslateTransform(x,y);g.ScaleTransform(scale,scale);using(var room=new SolidBrush(Color.FromArgb(230,234,240)))g.FillRectangle(room,0,0,Data.width,Data.height);
            foreach(var c in Data.cells)using(var b=new SolidBrush(c.kind=="platform"?Color.FromArgb(80,145,180):Color.FromArgb(65,76,94)))g.FillRectangle(b,c.x,c.y,c.width,c.height);
            using(var b=new SolidBrush(Theme.Accent))g.FillEllipse(b,(float)Data.spawnX-8,(float)Data.spawnY-12,16,24);
        }
    }
}
