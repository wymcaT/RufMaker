using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed class NativeFilmProgressForm:Form
{
    private readonly CancellationTokenSource cancellation=new CancellationTokenSource();private bool finished;
    public NativeFilmInfo Result;public string ErrorText;
    public NativeFilmProgressForm(NativeFilmJob job)
    {
        Text="生成原生底片";Size=new Size(540,190);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;Font=Theme.Font;BackColor=Theme.Panel;ForeColor=Theme.Text;
        var label=new Label{Text="正在完整运行原生耐久，player 固定在 (400,304)…",Dock=DockStyle.Top,Height=54,Padding=new Padding(15,15,10,0)};
        var bar=new ProgressBar{Dock=DockStyle.Top,Height=25,Minimum=0,Maximum=job.frameCount};
        var cancel=new Button{Text="取消",Dock=DockStyle.Bottom,Height=34};cancel.Click+=(s,e)=>{cancellation.Cancel();cancel.Enabled=false;label.Text="正在停止生成，已有底片会保留…";};Controls.Add(bar);Controls.Add(label);Controls.Add(cancel);
        FormClosing+=(s,e)=>{if(!finished){e.Cancel=true;cancellation.Cancel();cancel.Enabled=false;}};
        Shown+=async(s,e)=>{
            try{var progress=new Progress<NativeFilmProgress>(value=>{if(IsDisposed||finished)return;bar.Value=Math.Min(bar.Maximum,value.frame);label.Text="正在生成原生底片 · "+value.frame+" / "+value.total+" 帧（50 FPS）";});Result=await Task.Run(()=>NativeFilmBuilder.Generate(job,cancellation.Token,progress));DialogResult=DialogResult.OK;}
            catch(OperationCanceledException){DialogResult=DialogResult.Cancel;}catch(Exception error){ErrorText=error.Message;DialogResult=DialogResult.Abort;}finally{finished=true;Close();}
        };
    }
    protected override void Dispose(bool disposing){if(disposing)cancellation.Dispose();base.Dispose(disposing);}
}
internal sealed partial class MainForm
{
    private readonly CheckBox nativeRender=new CheckBox{Text="原生底片",AutoSize=true,Enabled=false,ForeColor=Theme.Muted,Margin=new Padding(8,9,5,0)};
    private readonly Button regenerateNativeFilm=new Button{Text="重新生成原生底片",AutoSize=true,Enabled=false,Margin=new Padding(8,3,5,0)};
    private NativeEvent selectedNativeEvent;private NativeFilmInfo nativeFilmInfo;private bool nativeFilmGenerating;
    private string NativeFilmPath {get{return document==null?null:Path.Combine(document.project,"cache","native-film","base.rufvideo");}}
    private void ToggleNativeFilm(){stage.NativeFilmPath=nativeRender.Checked&&File.Exists(NativeFilmPath)?NativeFilmPath:null;stage.NativeFilmIds=new System.Collections.Generic.HashSet<string>(nativeFilmInfo==null?new string[0]:nativeFilmInfo.nativeIds??new string[0]);stage.SceneChanged();}
    private async Task EnsureNativeFilm()
    {
        nativeRender.Enabled=state!=null&&state.nativeProject!=null;regenerateNativeFilm.Enabled=nativeRender.Enabled;nativeFilmInfo=null;
        if(!nativeRender.Enabled){stage.NativeFilmPath=null;return;}
        if(File.Exists(NativeFilmPath)){
            try{using(var film=new NativeFilmReader(NativeFilmPath))nativeFilmInfo=film.Info;nativeRender.Checked=true;ToggleNativeFilm();SetStatus("原生底片已载入 · 可直接拖动查看，RufMaker 物件与特效实时叠加。");}
            catch(Exception error){nativeRender.Checked=false;Error(error);}return;
        }
        await GenerateNativeFilm();
    }
    private async Task GenerateNativeFilm()
    {
        if(state==null||state.nativeProject==null||nativeFilmGenerating)return;bool wasBusy=busy;nativeFilmGenerating=true;StopPlaying();SetBusy(true);regenerateNativeFilm.Enabled=false;
        try{
            properties.CommitPending();if(!await SaveNow(false))return;SetStatus("正在编译原生底片录制副本…");var job=await bridge.Call<NativeFilmJob>("native-film-prepare",new{});
            using(var dialog=new NativeFilmProgressForm(job)){
                var result=dialog.ShowDialog(this);if(result==DialogResult.OK){nativeFilmInfo=dialog.Result;nativeRender.Checked=true;ToggleNativeFilm();ShowProperties();SetStatus("原生底片已保存 · "+nativeFilmInfo.frameCount+" 帧 · 后续打开直接使用缓存。");}
                else if(!String.IsNullOrEmpty(dialog.ErrorText))throw new IOException(dialog.ErrorText);else SetStatus("底片生成已取消，可稍后点击重新生成。");
            }
        }catch(Exception error){Error(error);}finally{nativeFilmGenerating=false;regenerateNativeFilm.Enabled=state!=null&&state.nativeProject!=null;SetBusy(wasBusy);}
    }
    private void NativeProperties(NativeProperties bag)
    {
        var native=state.nativeProject;selectionTitle.Text="GM 工程 · 原生";codeButton.Enabled=true;
        bag.Add("耐久房间","原生工程",typeof(string),()=>state.room,null);bag.Add("控制对象","原生工程",typeof(string),()=>native.controller,null);bag.Add("计时方式","原生工程",typeof(string),()=>String.IsNullOrEmpty(native.timeline)?native.clock:native.timeline+" × "+native.timelineSpeed,null);
        bag.Add("底片状态","原生底片",typeof(string),()=>nativeFilmInfo==null?"尚未生成":nativeFilmInfo.frameCount+" 帧 / "+nativeFilmInfo.fps+" FPS · player (400,304)",null);
        bag.Add("已定位事件","识别结果",typeof(int),()=>native.events.Length,null);bag.Add("未换算表达式","识别结果",typeof(int),()=>native.unresolved.Length,null);
        if(selectedNativeEvent!=null){var item=selectedNativeEvent;bag.Add("原表达式","所选事件",typeof(string),()=>item.expression,null);bag.Add("帧","所选事件",typeof(int),()=>item.frame,null);bag.Add("原始代码","所选事件",typeof(string),()=>item.file+":"+item.line,null);bag.Add("内容","所选事件",typeof(string),()=>item.snippet,null);}
        bag.Add("兼容提示与建议","识别结果",typeof(string),()=>String.Join("\r\n",native.warnings),null);bag.Add("编辑方式","原生工程",typeof(string),()=>"双击事件标记编辑该帧代码块。改动原生代码后点击“重新生成原生底片”；RufMaker 物件和特效实时叠加。F5 进入真实试玩。",null);
    }
    private async Task EditNativeEvent()
    {
        if(state.nativeProject==null||selectedNativeEvent==null)return;var item=selectedNativeEvent;
        try{StopPlaying();properties.CommitPending();if(!await SaveNow(false))return;bool saved=false;var source=await bridge.Call<SourceData>("native-event-read",new{ @event=item });using(var editor=new CodeForm("第 "+item.frame+" 帧 · "+item.expression+" · "+item.file,source.content,async text=>{await bridge.Call<object>("native-event-save",new{ @event=item,content=text,hash=source.hash });saved=true;}))editor.ShowDialog(this);if(saved){await LoadProject();SetStatus("原生代码已保存，点击“重新生成原生底片”更新画面。");}}catch(Exception error){Error(error);}
    }
    private async Task EditNativeSource()
    {
        if(state.nativeProject==null)return;string file=selectedNativeEvent==null?"objects/"+state.nativeProject.controller+".gml":selectedNativeEvent.file;if(String.IsNullOrEmpty(state.nativeProject.controller)&&selectedNativeEvent==null){SetStatus("未识别控制对象，请从左栏资源打开对应 GML。");return;}
        try{bool saved=false;var source=await bridge.Call<SourceData>("native-source-read",new{path=file});using(var editor=new CodeForm(file,source.content,async text=>{await bridge.Call<object>("native-source-save",new{path=file,content=text});await SaveNow(false);saved=true;},selectedNativeEvent==null?1:selectedNativeEvent.line))editor.ShowDialog(this);if(saved){await LoadProject();SetStatus("原生代码已保存，点击“重新生成原生底片”更新画面。");}}catch(Exception error){Error(error);}
    }
}
