using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

// Deliberately not an editor mode: no editor controls, commands, bridge or undo
// callbacks are passed to this window. Filtering never touches the render scene.
internal sealed class WalkthroughSnapshot
{
    public readonly EditorState Scene;
    public readonly RoomData Room;
    public readonly NativeFilmInfo Film;
    public readonly HashSet<string> FilmIds;
    private readonly HashSet<int> hidden=new HashSet<int>();
    public WalkthroughSnapshot(EditorState scene,RoomData room,NativeFilmInfo film=null,IEnumerable<string> filmIds=null){Film=film==null?null:Json.Read<NativeFilmInfo>(Json.Write(film));FilmIds=new HashSet<string>(filmIds??Enumerable.Empty<string>());Scene=scene.Snapshot();Scene.nativePaths=scene.nativePaths.ToDictionary(p=>p.Key,p=>p.Value.Copy());Room=Json.Read<RoomData>(Json.Write(room));}
    public int[] VisibleRows {get{return Enumerable.Range(0,Scene.tracks.Count).Where(i=>!hidden.Contains(i)).ToArray();}}
    public void Hide(IEnumerable<int> rows){foreach(int row in rows)if(row>=0&&row<Scene.tracks.Count)hidden.Add(row);}
    public void ShowAll(){hidden.Clear();}
}

internal sealed class WalkthroughView : Form
{
    private readonly WalkthroughSnapshot snapshot;
    private readonly StageCanvas preview;
    private readonly SpriteStore assets;
    private readonly ListBox rows=new ListBox{Dock=DockStyle.Fill,SelectionMode=SelectionMode.MultiExtended,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=28,BackColor=Theme.Panel,ForeColor=Theme.Text};
    private readonly TrackBar seek=new TrackBar{Dock=DockStyle.Bottom,TickStyle=TickStyle.None};
    private readonly Timer timer=new Timer{Interval=20};
    private readonly Stopwatch clock=new Stopwatch();
    private readonly Label position=new Label{AutoSize=true,ForeColor=Theme.Text};
    private int frame,origin;private double seconds;private bool updatingSeek;
    internal WalkthroughView(WalkthroughSnapshot value,SpriteStore sprites,Dictionary<string,string> objects,string film,int initialFrame,bool camera)
    {
        snapshot=value;Text=Language.Text("只读走查快照");Width=1100;Height=760;MinimumSize=new Size(700,450);StartPosition=FormStartPosition.CenterParent;BackColor=Theme.Back;Font=Theme.Font;
        assets=new SpriteStore{Project=sprites.Project,Metadata=Json.Read<Dictionary<string,SpriteMeta>>(Json.Write(sprites.Metadata))};
        // Disabled input plus private snapshots: no stage pick/drag/curve/native-test mutation.
        preview=new StageCanvas{Dock=DockStyle.Fill,Enabled=false,AllowDrop=false,State=value.Scene,Room=value.Room,Sprites=assets,ObjectSprites=new Dictionary<string,string>(objects),NativeFilmPath=film,NativeFilmInfo=value.Film,NativeFilmIds=new HashSet<string>(value.FilmIds),RenderCamera=camera,Grid=false};
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,ColumnCount=1};layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Percent,62));layout.RowStyles.Add(new RowStyle(SizeType.Percent,38));
        var bar=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true};
        AddButton(bar,"播放 / 暂停",Toggle);AddButton(bar,"隐藏所选轨道",()=>{snapshot.Hide(rows.SelectedItems.Cast<int>().ToArray());RefreshRows();});AddButton(bar,"显示全部轨道",()=>{snapshot.ShowAll();RefreshRows();});
        AddButton(bar,"放大时间轴",()=>{seconds=Math.Max(1,seconds/2);rows.Invalidate();});AddButton(bar,"适应全部",()=>{seconds=Math.Max(1,snapshot.Scene.duration/(double)Math.Max(1,snapshot.Scene.fps));rows.Invalidate();});
        AddButton(bar,"关闭",Close);bar.Controls.Add(position);
        // Do not inherit a green coverage badge without revalidating cache provenance.
        var coverage=NativePreviewCoverage.Evaluate(NativePreviewCoverage.Collect(value.Scene),value.Scene,value.Film,!String.IsNullOrEmpty(film),null,false);
        AddButton(bar,"预览覆盖说明",()=>MessageBox.Show(this,coverage.Description,Language.Text(coverage.Title)));
        if(coverage.Warning)bar.Controls.Add(new Label{AutoSize=true,ForeColor=Theme.Accent,Text=Language.Text(coverage.Title)});
        preview.RenderFailed=error=>{timer.Stop();clock.Stop();position.Text=Language.Text("预览失败")+": "+error.Message;};
        var note=new Label{AutoSize=true,ForeColor=Theme.Muted,Text=Language.Text("仅隐藏列表行；场景保留全部物件。只读快照，不保存；播放无音频，原生画面使用已有缓存。")};bar.Controls.Add(note);
        layout.Controls.Add(bar,0,0);layout.Controls.Add(preview,0,1);layout.Controls.Add(rows,0,2);Controls.Add(layout);Controls.Add(seek);
        seek.Maximum=Math.Max(0,value.Scene.duration);seek.ValueChanged+=(s,e)=>{if(updatingSeek)return;clock.Stop();timer.Stop();SetFrame(seek.Value);};
        rows.DrawItem+=DrawRow;rows.DoubleClick+=(s,e)=>{if(rows.SelectedItem!=null){clock.Stop();timer.Stop();SetFrame(snapshot.Scene.tracks[(int)rows.SelectedItem].start);}};
        seconds=Math.Max(1,value.Scene.duration/(double)Math.Max(1,value.Scene.fps));RefreshRows();SetFrame(initialFrame);
        timer.Tick+=(s,e)=>{SetFrame(origin+(int)(clock.Elapsed.TotalSeconds*Math.Max(1,snapshot.Scene.fps)));if(frame>=snapshot.Scene.duration){clock.Stop();timer.Stop();}};
        Shown+=(s,e)=>preview.SceneChanged();
    }
    private static void AddButton(Control bar,string text,Action action){var b=new Button{Text=Language.Text(text),AutoSize=true};b.Click+=(s,e)=>action();bar.Controls.Add(b);}
    private void RefreshRows(){rows.BeginUpdate();try{rows.Items.Clear();foreach(int row in snapshot.VisibleRows)rows.Items.Add(row);}finally{rows.EndUpdate();}rows.Invalidate();}
    private void SetFrame(int value){frame=Math.Max(0,Math.Min(snapshot.Scene.duration,value));updatingSeek=true;try{seek.Value=frame;}finally{updatingSeek=false;}preview.ShowFrame(frame);position.Text=frame+" / "+snapshot.Scene.duration;rows.Invalidate();}
    private void Toggle(){if(timer.Enabled){timer.Stop();clock.Stop();return;}if(frame>=snapshot.Scene.duration)SetFrame(0);origin=frame;clock.Restart();timer.Start();}
    private void DrawRow(object sender,DrawItemEventArgs e)
    {
        if(e.Index<0)return;e.DrawBackground();var t=snapshot.Scene.tracks[(int)rows.Items[e.Index]];int nameWidth=Math.Min(240,e.Bounds.Width/3),width=Math.Max(1,e.Bounds.Width-nameWidth-12),fps=Math.Max(1,snapshot.Scene.fps);
        double span=seconds*fps,start=Math.Max(0,Math.Min(frame-span/2,snapshot.Scene.duration-span));
        Theme.Label(e.Graphics,t.name,new Rectangle(e.Bounds.X+4,e.Bounds.Y,nameWidth-8,e.Bounds.Height),Theme.Text,localize:false);
        float left=(float)Math.Max(0,Math.Min(width,(t.start-start)/span*width)),right=(float)Math.Max(0,Math.Min(width,(t.end+1-start)/span*width));
        if(right>left)using(var brush=new SolidBrush(Theme.TrackColor(t.kind)))e.Graphics.FillRectangle(brush,nameWidth+left,e.Bounds.Y+6,right-left,e.Bounds.Height-12);
        float cursor=nameWidth+(float)((frame-start)/span*width);using(var pen=new Pen(Theme.Accent))e.Graphics.DrawLine(pen,cursor,e.Bounds.Top,cursor,e.Bounds.Bottom);e.DrawFocusRectangle();
    }
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData)
    {
        if(keyData==Keys.Space){Toggle();return true;}if(keyData==Keys.Escape){Close();return true;}
        // No editor accelerator may bubble to the owner, including native F5/F6.
        if((keyData&(Keys.Control|Keys.Alt))!=0||keyData==Keys.Delete||keyData>=Keys.F1&&keyData<=Keys.F12)return true;
        return base.ProcessCmdKey(ref msg,keyData);
    }
    protected override void Dispose(bool disposing){if(disposing){timer.Stop();timer.Dispose();clock.Stop();assets.Dispose();}base.Dispose(disposing);}
}

internal sealed partial class MainForm
{
    private void ShowWalkthrough()
    {
        if(state==null||stage.Room==null||sprites==null||busy||saving||writingRoomInstance)return;
        StopPlaying();bool resumeSave=saveTimer.Enabled;saveTimer.Stop();
        try{
            var snapshot=new WalkthroughSnapshot(state,stage.Room,stage.NativeFilmInfo,stage.NativeFilmIds);
            using(var viewer=new WalkthroughView(snapshot,sprites,stage.ObjectSprites,stage.NativeFilmPath,frame,stage.RenderCamera))Language.ShowDialog(viewer,this);
        }finally{if(resumeSave&&!IsDisposed)saveTimer.Start();}
    }
}
