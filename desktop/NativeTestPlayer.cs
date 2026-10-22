using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed partial class MainForm
{
    private readonly CheckBox editNativeTest=new CheckBox{Text="编辑试玩玩家",AutoSize=true,Enabled=false,Margin=new Padding(8,9,5,0)};
    private readonly System.Windows.Forms.Timer nativeWatch=new System.Windows.Forms.Timer{Interval=500};private bool nativePolling;private string nativePhase="";
    private void BuildNativeTestTools(Control tools)
    {
        tools.Controls.Add(editNativeTest);Language.ToolTip(tips,editNativeTest,"左键点击或拖动设置表达式预览的参考玩家位置，Shift 按 16 像素吸附；工程设置可输入 X/Y。原生工程中同时用作 F5 试玩位置；底片保持录制时的玩家位置。");
        editNativeTest.CheckedChanged+=(s,e)=>{stage.EditNativeTest=editNativeTest.Checked;if(editNativeTest.Checked&&state!=null){var track=state.tracks.FirstOrDefault(t=>t.kind=="native");if(track!=null)SelectTrack(track,null);}stage.Invalidate();};
        stage.NativeTestPreview=()=>{RandomParameters.Attach(state);inspectorTimer.Stop();inspectorTimer.Start();};stage.NativeTestChanged=()=>{MarkChanged();ShowProperties();};
        nativeWatch.Tick+=async(s,e)=>await WatchNativePlay();
    }
    private void RefreshNativeTestPlayer()
    {
        editNativeTest.Enabled=state!=null;if(!editNativeTest.Enabled)editNativeTest.Checked=false;stage.EditNativeTest=editNativeTest.Checked;
        string player=document.inventory.objects.FirstOrDefault(n=>System.Text.RegularExpressions.Regex.IsMatch(n,@"^(?:(?:obj_?|o)?player[12]?)$",System.Text.RegularExpressions.RegexOptions.IgnoreCase));
        string sprite;stage.NativeTestSprite=player!=null&&document.inventory.objectSprites.TryGetValue(player,out sprite)?sprite:null;
    }
    private void NativeTestFields(NativeProperties bag)
    {
        Field(bag,"玩家 X","试玩起点",()=>state.nativeTest.x,value=>state.nativeTest.x=Math.Max(-100000,Math.Min(100000,value)),null,"F5 进入测试时的位置；游戏内按 R 重新回到同一测试时间和位置。只影响试玩。");
        Field(bag,"玩家 Y","试玩起点",()=>state.nativeTest.y,value=>state.nativeTest.y=Math.Max(-100000,Math.Min(100000,value)));
        bag.Add("测试时间","试玩起点",typeof(string),()=>"F5 时保存状态；R 在当前游戏内直接恢复该快照。",null);
    }
    private async Task WatchNativePlay()
    {
        if(nativePolling||busy||bridge==null||closing)return;nativePolling=true;
        try{var progress=await bridge.Call<NativeRunProgress>("run-progress",new{});if(IsDisposed||busy)return;string phase=progress==null?"ended":progress.phase;
            if(phase!=nativePhase){if(phase=="ready")SetStatus("原生试玩已就绪 · R 直接恢复第 "+progress.target+" 帧的测试快照。");else if(phase=="error")Error(new InvalidOperationException(progress.error));}
            nativePhase=phase;if(phase=="ended"||phase=="error")nativeWatch.Stop();
        }catch{nativeWatch.Stop();}finally{nativePolling=false;}
    }
}
