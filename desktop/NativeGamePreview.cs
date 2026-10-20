using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed class NativeGamePreview:Panel
{
    [DllImport("user32.dll")] private static extern IntPtr SetParent(IntPtr child,IntPtr parent);
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr window,int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr window,int index,int value);
    [DllImport("user32.dll")] private static extern bool MoveWindow(IntPtr window,int x,int y,int width,int height,bool repaint);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window,int command);
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback,IntPtr parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window,out uint processId);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window,out NativeRect rect);
    private delegate bool WindowCallback(IntPtr window,IntPtr parameter);
    private struct NativeRect {public int left,top,right,bottom;}
    private readonly Timer poll=new Timer{Interval=40};private readonly Label message=new Label{Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Theme.Muted};
    private readonly PictureBox pausedFrame=new PictureBox{Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,Visible=false,BackColor=Theme.Back};private int imageSequence=-1,imageFrame=-1;
    private NativeSession session;private NativePreviewRunner runner;private IntPtr gameWindow;private int requested,lastStatus=-1,actualFrame=-1,lastTest;private bool commandDirty,pendingSeek,playing,stopping,launching;private DateTime lastWrite=DateTime.MinValue;private int generation;
    public Action<int,bool,bool> Progress;public Action<string> Failure;public Action RequestTest;
    public bool Running {get{return session!=null&&runner!=null&&!stopping&&(launching||runner.Alive);}}
    public bool Playing {get{return playing;}}
    public NativeGamePreview(){DoubleBuffered=true;BackColor=Theme.Back;Controls.Add(message);Controls.Add(pausedFrame);poll.Tick+=(s,e)=>Tick();Resize+=(s,e)=>Fit();}
    public async Task Start(NativeSession next,int frame)
    {
        Stop();session=next;runner=new NativePreviewRunner(next);requested=Math.Max(0,frame);Visible=true;await Launch(++generation);
    }
    private async Task Launch(int current)
    {
        var owned=runner;var next=session;launching=true;poll.Stop();gameWindow=IntPtr.Zero;lastStatus=-1;actualFrame=-1;lastTest=0;imageSequence=-1;imageFrame=-1;commandDirty=false;pendingSeek=false;lastWrite=DateTime.MinValue;
        message.Text="正在初始化并逐帧重放原生 GML…";pausedFrame.Visible=pausedFrame.Image!=null;message.Visible=!pausedFrame.Visible;if(pausedFrame.Visible)pausedFrame.BringToFront();else message.BringToFront();if(Progress!=null)Progress(0,false,true);
        try{
            await owned.Restart(requested,playing);if(current!=generation||runner!=owned)return;var launched=owned.Process;
            for(int i=0;i<300;i++){
                if(current!=generation)return;if(launched==null||launched.HasExited)throw new InvalidOperationException("原生渲染副本在启动时退出，请查看 GameMaker 的运行报错。");
                IntPtr found=IntPtr.Zero;long area=0;EnumWindows((window,p)=>{uint owner;GetWindowThreadProcessId(window,out owner);NativeRect rect;if(owner==(uint)launched.Id&&GetClientRect(window,out rect)){long size=(long)(rect.right-rect.left)*(rect.bottom-rect.top);if(size>area){area=size;found=window;}}return true;},IntPtr.Zero);
                if(found!=IntPtr.Zero&&area>10000&&File.Exists(next.statusFile)){gameWindow=found;break;}await Task.Delay(50);
            }
            if(current!=generation)return;if(gameWindow==IntPtr.Zero)throw new InvalidOperationException("没有找到原生游戏绘图窗口。请用 F5 查看 GameMaker 的运行报错。");
            SetParent(gameWindow,Handle);int style=GetWindowLong(gameWindow,-16);SetWindowLong(gameWindow,-16,(style&~unchecked((int)0x80C40000))|0x50000000);SetWindowLong(gameWindow,-20,GetWindowLong(gameWindow,-20)&~0x00040000);Fit();ShowWindow(gameWindow,5);message.Visible=false;poll.Start();
        }finally{if(current==generation)launching=false;}
    }
    public void Seek(int frame){if(session==null)return;requested=Math.Max(0,frame);playing=false;pendingSeek=true;commandDirty=true;}
    public void SetPlaying(bool value){if(session==null)return;if(playing==value)return;playing=value;commandDirty=true;}
    private void Fit(){if(gameWindow==IntPtr.Zero||session==null)return;double scale=Math.Min(Width/(double)Math.Max(1,session.width),Height/(double)Math.Max(1,session.height));int w=Math.Max(1,(int)(session.width*scale)),h=Math.Max(1,(int)(session.height*scale));MoveWindow(gameWindow,(Width-w)/2,(Height-h)/2,w,h,true);commandDirty=true;}
    private async void Tick()
    {
        if(stopping||launching||session==null||runner==null)return;int current=generation;var owned=runner;
        try{
            if(!owned.Alive){Stop();if(Failure!=null)Failure("原生渲染副本已退出；请用 F5 查看运行报错，或重新打开原生渲染。");return;}
            var status=owned.ReadStatus();if(status!=null)actualFrame=status.frame;
            if(status!=null&&status.rewind){await Launch(current);return;}
            if(commandDirty&&(DateTime.UtcNow-lastWrite).TotalMilliseconds>=75){
                if(pendingSeek&&requested<actualFrame){await Launch(current);return;}
                owned.WriteControl(requested,playing,pendingSeek);commandDirty=false;pendingSeek=false;lastWrite=DateTime.UtcNow;return;
            }
            if(status==null)return;int frame=status.frame,target=status.target,sequence=status.sequence;
            bool active=status.playing,seeking=frame<target,changed=frame!=lastStatus||active!=playing;if(!commandDirty)playing=active;if(!commandDirty&&playing&&!seeking)requested=frame;
            if(!active&&!seeking&&!commandDirty&&File.Exists(session.frameFile)&&(imageSequence!=sequence||imageFrame!=frame)){
                using(var stream=new MemoryStream(File.ReadAllBytes(session.frameFile)))using(var original=Image.FromStream(stream)){var old=pausedFrame.Image;pausedFrame.Image=new Bitmap(original);if(old!=null)old.Dispose();}
                imageSequence=sequence;imageFrame=frame;pausedFrame.Visible=true;pausedFrame.BringToFront();
            }
            if(active&&!seeking)pausedFrame.Visible=false;
            if(changed){lastStatus=frame;if(Progress!=null)Progress(frame,active&&!commandDirty,seeking);}
            if(status.test>lastTest){lastTest=status.test;if(RequestTest!=null)BeginInvoke(RequestTest);}
        }catch(IOException error){if(!poll.Enabled&&current==generation&&runner==owned){Stop();if(Failure!=null)Failure(error.Message);}}catch(Exception error){if(current==generation&&runner==owned){Stop();if(Failure!=null)Failure(error.Message);}}
    }
    public void Stop()
    {
        generation++;stopping=true;poll.Stop();gameWindow=IntPtr.Zero;var owned=runner;runner=null;session=null;commandDirty=false;pendingSeek=false;playing=false;launching=false;pausedFrame.Visible=false;var oldImage=pausedFrame.Image;pausedFrame.Image=null;if(oldImage!=null)oldImage.Dispose();imageSequence=-1;imageFrame=-1;
        if(owned!=null)owned.Dispose();
        stopping=false;message.Visible=true;message.Text="原生游戏渲染已停止";
    }
    protected override void Dispose(bool disposing){if(disposing){Stop();poll.Dispose();}base.Dispose(disposing);}
}
internal sealed partial class MainForm
{
    private readonly NativeGamePreview nativeView=new NativeGamePreview{Dock=DockStyle.Fill,Visible=false};private readonly CheckBox nativeRender=new CheckBox{Text="原生游戏渲染",AutoSize=true,Enabled=false,ForeColor=Theme.Muted,Margin=new Padding(8,9,5,0)};
    private NativeEvent selectedNativeEvent;private bool nativeSeekSuppressed,nativeRenderLoading;
    private async Task ToggleNativeRender()
    {
        if(nativeRenderLoading)return;if(!nativeRender.Checked){nativeView.Stop();nativeView.Visible=false;stage.Visible=true;audio.Pause();return;}if(state==null||state.nativeProject==null){nativeRender.Checked=false;return;}
        nativeRenderLoading=true;StopPlaying();nativeView.Stop();SetBusy(true);SetStatus("正在构建原工程的原生渲染预览…");
        try{properties.CommitPending();if(!await SaveNow(false)){nativeRender.Checked=false;return;}var session=await bridge.Call<NativeSession>("native-preview",new{frame=frame});if(!nativeRender.Checked)return;stage.Visible=false;nativeView.Visible=true;nativeView.BringToFront();await nativeView.Start(session,frame);if(!nativeRender.Checked){nativeView.Stop();nativeView.Visible=false;stage.Visible=true;return;}SetStatus("原生 GML 渲染已接入；向前定位执行原事件，向后定位从头重放。关闭再打开此开关可重建代码。");}
        catch(Exception error){nativeView.Stop();nativeView.Visible=false;stage.Visible=true;nativeRender.Checked=false;Error(error);}finally{nativeRenderLoading=false;SetBusy(false);}
    }
    private void NativeProgress(int current,bool playing,bool seeking)
    {
        if(state==null||state.nativeProject==null)return;
        if(seeking){audio.Pause();SetStatus("正在重放原生事件 · "+current+" 帧 → "+frame+" 帧");return;}
        if(playing){if(!audio.Playing&&audio.Duration>0)audio.Play(current/(double)state.fps);audio.Update();nativeSeekSuppressed=true;try{SetFrame(current);timeline.EnsureFrameVisible();}finally{nativeSeekSuppressed=false;}playButton.Text="Ⅱ";if(current>=state.duration){nativeView.SetPlaying(false);audio.Pause();}}
        else{audio.Pause();playButton.Text="▶";}
    }
    private void NativeProperties(NativeProperties bag)
    {
        var native=state.nativeProject;selectionTitle.Text="GM 工程 · 原生";codeButton.Enabled=true;
        bag.Add("耐久房间","原生工程",typeof(string),()=>state.room,null);bag.Add("控制对象","原生工程",typeof(string),()=>native.controller,null);bag.Add("计时方式","原生工程",typeof(string),()=>String.IsNullOrEmpty(native.timeline)?native.clock:native.timeline+" × "+native.timelineSpeed,null);
        bag.Add("已定位事件","识别结果",typeof(int),()=>native.events.Length,null);bag.Add("未换算表达式","识别结果",typeof(int),()=>native.unresolved.Length,null);
        if(selectedNativeEvent!=null){var item=selectedNativeEvent;bag.Add("原表达式","所选事件",typeof(string),()=>item.expression,null);bag.Add("帧","所选事件",typeof(int),()=>item.frame,null);bag.Add("原始代码","所选事件",typeof(string),()=>item.file+":"+item.line,null);bag.Add("内容","所选事件",typeof(string),()=>item.snippet,null);}
        bag.Add("兼容提示与建议","识别结果",typeof(string),()=>String.Join("\r\n",native.warnings),null);bag.Add("编辑方式","原生工程",typeof(string),()=>"双击事件标记编辑该帧代码块；右键可编辑整个源文件。原生游戏渲染执行原 GML，F5 从游标进入试玩。",null);
    }
    private async Task EditNativeEvent()
    {
        if(state.nativeProject==null||selectedNativeEvent==null)return;var item=selectedNativeEvent;
        try{StopPlaying();properties.CommitPending();if(!await SaveNow(false))return;nativeView.Stop();bool saved=false;var source=await bridge.Call<SourceData>("native-event-read",new{ @event=item });using(var editor=new CodeForm("第 "+item.frame+" 帧 · "+item.expression+" · "+item.file,source.content,async text=>{await bridge.Call<object>("native-event-save",new{ @event=item,content=text,hash=source.hash });saved=true;}))editor.ShowDialog(this);if(saved)await LoadProject();else if(nativeRender.Checked)await ToggleNativeRender();}catch(Exception error){Error(error);}
    }
    private async Task EditNativeSource()
    {
        if(state.nativeProject==null)return;string file=selectedNativeEvent==null?"objects/"+state.nativeProject.controller+".gml":selectedNativeEvent.file;if(String.IsNullOrEmpty(state.nativeProject.controller)&&selectedNativeEvent==null){SetStatus("未识别控制对象，请从左栏资源打开对应 GML。");return;}
        try{bool saved=false;var source=await bridge.Call<SourceData>("native-source-read",new{path=file});using(var editor=new CodeForm(file,source.content,async text=>{await bridge.Call<object>("native-source-save",new{path=file,content=text});await SaveNow(false);saved=true;},selectedNativeEvent==null?1:selectedNativeEvent.line))editor.ShowDialog(this);if(saved)await LoadProject();}catch(Exception error){Error(error);}
    }
}
