using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed partial class MainForm
{
    private string currentProjectPath="";
    private FlowLayoutPanel beatBar;private bool beatLoading;
    private readonly CheckBox beatSnap=new CheckBox{Text="按 BPM 和 x 分音吸附",AutoSize=true,ForeColor=Theme.Text,Margin=new Padding(8,8,8,2)};
    private readonly NumericUpDown beatBpm=new WheelSafeNumericUpDown{Minimum=1,Maximum=1000,DecimalPlaces=2,Increment=.1m,Width=90},beatDelay=new WheelSafeNumericUpDown{Minimum=-3600000,Maximum=3600000,DecimalPlaces=1,Increment=1,Width=105};
    private readonly ComboBox beatDivision=new WheelSafeComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=85};
    private readonly int[] divisions={1,2,3,4,6,8,12,16,24,32,48,64,96,128};
    private void BuildProjectActions(FlowLayoutPanel bar)
    {
        bar.Controls.Add(Button("新建工程",async()=>await NewProject(),false,90));bar.Controls.Add(Button("打开工程",async()=>await OpenProject(),false,90));bar.Controls.Add(Button("导入歌曲",async()=>await ImportMusic(),false,90));
    }
    private FlowLayoutPanel BuildBeatControls()
    {
        beatBar=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true,Margin=Padding.Empty};beatBar.Controls.Add(beatSnap);
        foreach(var n in new[]{beatBpm,beatDelay}){n.BackColor=Theme.Raised;n.ForeColor=Theme.Text;n.Margin=new Padding(2,4,5,3);}
        beatDivision.BackColor=Theme.Raised;beatDivision.ForeColor=Theme.Text;beatDivision.Margin=new Padding(2,4,8,3);foreach(int n in divisions)beatDivision.Items.Add("1/"+n+" 音符");
        beatBar.Controls.Add(new Label{Text="BPM",AutoSize=true,Margin=new Padding(2,9,2,0)});beatBar.Controls.Add(beatBpm);beatBar.Controls.Add(beatDivision);beatBar.Controls.Add(new Label{Text="延迟 / 毫秒",AutoSize=true,Margin=new Padding(2,9,2,0)});beatBar.Controls.Add(beatDelay);
        beatBar.Controls.Add(Button("自动检测 BPM / 延迟",async()=>await DetectMusicBeat(),false,150));beatBar.Controls.Add(Button("游标设为拍点",()=>{if(state==null)return;PushUndo();state.beatOffsetMs=frame*1000.0/state.fps;MarkChanged();},false,108));
        tips.SetToolTip(beatSnap,"拖动时间轴播放头时按节拍吸附；Alt 临时关闭吸附。帧数输入和逐帧定位保持精确。");tips.SetToolTip(beatDelay,"节拍网格相对歌曲起点的延迟，允许负数。4 分音符是一拍，8 分是半拍，16 分是四分之一拍。");
        Action change=()=>{if(beatLoading||state==null)return;PushUndo();state.beatSnap=beatSnap.Checked;state.bpm=(double)beatBpm.Value;state.beatOffsetMs=(double)beatDelay.Value;if(beatDivision.SelectedIndex>=0)state.beatDivision=divisions[beatDivision.SelectedIndex];MarkChanged();timeline.Invalidate();};
        beatSnap.CheckedChanged+=(s,e)=>change();beatBpm.ValueChanged+=(s,e)=>change();beatDelay.ValueChanged+=(s,e)=>change();beatDivision.SelectionChangeCommitted+=(s,e)=>change();RefreshBeatControls();return beatBar;
    }
    private void RefreshBeatControls()
    {
        if(beatBar==null)return;beatLoading=true;try{beatSnap.Checked=state!=null&&state.beatSnap;beatBpm.Value=(decimal)Math.Max(1,Math.Min(1000,state==null?120:state.bpm));beatDelay.Value=(decimal)Math.Max(-3600000,Math.Min(3600000,state==null?0:state.beatOffsetMs));int index=Array.IndexOf(divisions,state==null?4:state.beatDivision);beatDivision.SelectedIndex=index<0?3:index;}finally{beatLoading=false;}
    }
    private async Task NewProject()
    {
        if(busy||bridge==null)return;StopPlaying();
        using(var dialog=new NewProjectForm(Path.Combine(root,"projects"),o=>bridge.Call<TerrainLayout>("terrain-preview",o)))
        {
            if(dialog.ShowDialog(this)!=DialogResult.OK)return;StopPlaying();properties.CommitPending();if(!await SaveNow(false))return;
            SetBusy(true);Enabled=false;SetStatus("正在创建独立 RufMaker 工程…");ProjectPathResult result=null;
            try{result=await bridge.Call<ProjectPathResult>("new-project",dialog.Options);}catch(Exception error){Error(error);}finally{Enabled=true;SetBusy(false);}
            if(result!=null)await SwitchProject(result.path);
        }
    }
    private async Task OpenProject()
    {
        if(busy||bridge==null)return;StopPlaying();using(var dialog=new OpenFileDialog{Title="打开 RufMaker 工程",Filter="RufMaker 工程|*.rufmaker|现有工程时间轴|rufmaker.json|GameMaker 8.2 工程|*.gm82",InitialDirectory=String.IsNullOrEmpty(currentProjectPath)?Path.Combine(root,"projects"):Path.GetDirectoryName(currentProjectPath)})
        {if(dialog.ShowDialog(this)==DialogResult.OK)await SwitchProject(dialog.FileName);}
    }
    private async Task SwitchProject(string path)
    {
        if(busy)return;StopPlaying();properties.CommitPending();stage.Focus();if(!await SaveNow(false))return;
        saveTimer.Stop();SetBusy(true);Enabled=false;NativeBridge candidate=null;
        try
        {
            candidate=new NativeBridge(root,path);var loaded=await candidate.Call<LoadData>("load",new{});
            var previous=bridge;bridge=candidate;candidate=null;restoredDocumentView=true;frame=0;stage.ResetView();
            try{await LoadProject(loaded);currentProjectPath=loaded.descriptor;SaveWorkspaceView();}finally{previous.Dispose();}
        }
        catch(Exception error){Error(error);}finally{if(candidate!=null)candidate.Dispose();Enabled=true;SetBusy(false);}
    }
    private async Task LoadCurrentMusic()
    {
        string file=Path.Combine(document.project,state.music.Replace('/',Path.DirectorySeparatorChar));await Task.Run(()=>audio.Load(file));
        if(document.waveform==null||document.waveform.duration<=0||document.waveform.source!=state.music)
        {
            SetStatus("正在分析歌曲波形与节拍…");document.waveform=await Task.Run(()=>AudioAnalyzer.Analyze(file));document.waveform.source=state.music;
            await bridge.Call<object>("audio-cache",document.waveform);
        }
        timeline.Wave=document.waveform;
    }
    private async Task ImportMusic()
    {
        if(state==null||busy)return;StopPlaying();using(var dialog=new OpenFileDialog{Title="选择并导入耐久歌曲",Filter="音频文件|*.ogg;*.mp3;*.wav;*.flac"})
        {
            if(dialog.ShowDialog(this)!=DialogResult.OK)return;StopPlaying();properties.CommitPending();saveTimer.Stop();SetBusy(true);Enabled=false;
            try
            {
                SetStatus("正在导入歌曲并生成波形…");var result=await bridge.Call<ProjectPathResult>("import-song",new{path=dialog.FileName});string file=Path.Combine(document.project,result.music.Replace('/',Path.DirectorySeparatorChar));
                var wave=await Task.Run(()=>AudioAnalyzer.Analyze(file));await Task.Run(()=>audio.Load(file));PushUndo();state.music=result.music;state.musicTitle=result.title;wave.source=state.music;document.waveform=wave;timeline.Wave=wave;
                state.duration=Math.Max((int)Math.Ceiling(wave.duration*state.fps),state.tracks.Count==0?1:state.tracks.Max(t=>Math.Max(t.end,t.keyframes.Count==0?0:t.keyframes.Max(k=>k.frame))));
                await bridge.Call<object>("audio-cache",wave);MarkChanged();SetFrame(Math.Min(frame,state.duration));timeline.FitAll();await SaveNow(false);SetStatus("已导入 "+result.title+"；可点击自动检测 BPM / 延迟");
            }
            catch(Exception error){Error(error);}finally{Enabled=true;SetBusy(false);}
        }
    }
    private async Task DetectMusicBeat()
    {
        if(state==null||busy)return;StopPlaying();SetBusy(true);
        try
        {
            SetStatus("正在检测歌曲节拍…");var wave=document.waveform;
            if(wave==null||wave.bpm<=0){string file=Path.Combine(document.project,state.music.Replace('/',Path.DirectorySeparatorChar));wave=await Task.Run(()=>AudioAnalyzer.Analyze(file));wave.source=state.music;document.waveform=wave;timeline.Wave=wave;await bridge.Call<object>("audio-cache",wave);}
            if(wave.bpm<=0){SetStatus("未检测到稳定节拍，请手动输入 BPM 与延迟");return;}
            PushUndo();state.bpm=wave.bpm;state.beatOffsetMs=wave.offsetMs;MarkChanged();SetStatus(String.Format("节拍估算：{0:0.##} BPM · 延迟 {1:0.#} ms · 可手动校正；检测到的是拍点相位",wave.bpm,wave.offsetMs));
        }
        catch(Exception error){Error(error);}finally{SetBusy(false);}
    }
    private async void NewFromKeyboard(){await NewProject();}private async void OpenFromKeyboard(){await OpenProject();}
}
