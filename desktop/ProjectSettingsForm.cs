using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

internal sealed partial class MainForm
{
    private void OpenProjectSettings()
    {
        if(state==null||busy)return;StopPlaying();properties.CommitPending();
        var draft=Json.Read<EditorState>(Json.Write(state));if(draft.player==null)draft.player=new PlayerSettings();
        var bag=new NativeProperties();
        bag.Add("工程名称","工程",typeof(string),()=>draft.name,v=>draft.name=(string)v);
        bag.Add("房间","工程",typeof(string),()=>draft.room,null);bag.Add("工程目录","工程",typeof(string),()=>document.project,null);
        if(state.nativeProject==null){bag.Add("直接进入耐久房间","工程",typeof(bool),()=>draft.directStart,v=>draft.directStart=(bool)v);bag.Add("FPS","时间",typeof(int),()=>draft.fps,v=>draft.fps=Math.Max(15,Math.Min(240,(int)v)),null,"更改 FPS 保留帧编号，会改变对应秒数。");}
        else {bag.Add("原引擎 FPS","时间",typeof(int),()=>state.fps,null);bag.Add("原生设置","工程",typeof(string),()=>"玩家逻辑、启动流程和 FPS 请在原 GML / 房间设置中修改后重新读取。",null);}
        bag.Add("总帧数","时间",typeof(int),()=>draft.duration,v=>draft.duration=Math.Max(1,Math.Min(1000000,(int)v)));
        bag.Add("BPM","节拍",typeof(double),()=>draft.bpm,v=>draft.bpm=Math.Max(1,Math.Min(1000,(double)v)));
        bag.Add("拍点延迟 / 毫秒","节拍",typeof(double),()=>draft.beatOffsetMs,v=>draft.beatOffsetMs=(double)v);
        bag.Add("歌曲","音乐",typeof(string),()=>String.IsNullOrEmpty(draft.musicTitle)?Path.GetFileName(draft.music):draft.musicTitle,null);
        bag.Add("音乐时长 / 秒","音乐",typeof(double),()=>document.waveform.duration,null);
        bag.Add("导入方式","音乐",typeof(string),()=>"使用顶栏“导入歌曲”选择音乐。",null);
        var p=draft.player;
        if(state.nativeProject==null){
        bag.Add("重力","玩家",typeof(double),()=>p.gravity,v=>p.gravity=Math.Max(0,(double)v));bag.Add("水平速度","玩家",typeof(double),()=>p.moveSpeed,v=>p.moveSpeed=Math.Max(0,(double)v));
        bag.Add("一段跳速度","玩家",typeof(double),()=>p.jumpSpeed,v=>p.jumpSpeed=Math.Max(0,(double)v));bag.Add("后续跳速度","玩家",typeof(double),()=>p.doubleJumpSpeed,v=>p.doubleJumpSpeed=Math.Max(0,(double)v));
        bag.Add("最大跳跃次数","玩家",typeof(int),()=>p.maxJumps,v=>p.maxJumps=Math.Max(1,Math.Min(8,(int)v)));bag.Add("无限跳跃","玩家",typeof(bool),()=>p.infiniteJump,v=>p.infiniteJump=(bool)v);
        }
        using(var dialog=new Form{Text="工程设置",Size=new Size(610,720),MinimumSize=new Size(440,440),StartPosition=FormStartPosition.CenterParent,BackColor=Theme.Back,ForeColor=Theme.Text,Font=Theme.Font,Icon=Icon,ShowInTaskbar=false,MinimizeBox=false,MaximizeBox=false})
        {
            var editor=new ParameterInspector{Dock=DockStyle.Fill,SelectedObject=bag};var foot=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(8)};
            var apply=Button("应用并关闭",()=>{},true,120);var cancel=Button("取消",()=>dialog.DialogResult=DialogResult.Cancel,false,80);
            apply.Click+=(s,e)=>{editor.CommitPending();int required=ContentEnd();if(draft.duration<required){Language.Message(dialog,"总帧数不能短于已有内容（第 "+required+" 帧）。","工程设置");return;}dialog.DialogResult=DialogResult.OK;};
            foot.Controls.Add(apply);foot.Controls.Add(cancel);dialog.Controls.Add(editor);dialog.Controls.Add(foot);dialog.AcceptButton=apply;dialog.CancelButton=cancel;
            if(Language.ShowDialog(dialog,this)!=DialogResult.OK)return;
        }
        PushUndo();state.name=draft.name;state.fps=draft.fps;state.duration=draft.duration;state.bpm=draft.bpm;state.beatOffsetMs=draft.beatOffsetMs;if(state.nativeProject==null){state.directStart=draft.directStart;state.player=draft.player;}
        MarkChanged();SetFrame(frame);SetStatus("工程设置已应用并自动保存。");
    }
}
