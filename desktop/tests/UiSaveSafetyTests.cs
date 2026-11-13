using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class UiSaveSafetyTests
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    private static object Get(object o,string n){return o.GetType().GetField(n,Flags).GetValue(o);}
    private static void Set(object o,string n,object v){o.GetType().GetField(n,Flags).SetValue(o,v);}
    private static object Call(object o,string n,params object[] args){return o.GetType().GetMethod(n,Flags).Invoke(o,args);}
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private static void Wait(Task task){var until=DateTime.UtcNow.AddSeconds(10);while(!task.IsCompleted){if(DateTime.UtcNow>until)throw new TimeoutException();Application.DoEvents();Thread.Sleep(1);}task.GetAwaiter().GetResult();}
    [STAThread] private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        using(var main=new MainForm(args[0])){
            Check((string)Call(main,"SaveStateText")=="已保存","Clean status");
            Set(main,"dirty",true);Check((string)Call(main,"SaveStateText")=="修改待保存","Pending is not saving");
            Set(main,"saving",true);Check((string)Call(main,"SaveStateText")=="正在保存…","In-flight status");
            Set(main,"saving",false);Set(main,"saveFailure","disk full");Call(main,"SetStatus","other action");
            Check((string)Call(main,"SaveStateText")=="保存失败 · 修改未保存","Other status must not erase save failure");
            var retry=(ToolStripButton)Get(main,"saveRetry");
            Check(retry.Available&&retry.Enabled,"Retry remains available");
            // The same busy -> save completion -> unbusy transition used by
            // LoadProject must refresh retry without a later status message.
            Call(main,"SetBusy",true);Set(main,"saving",true);Call(main,"SetStatus","saving during load");
            Check(!retry.Enabled,"Retry is disabled during load/save");
            Set(main,"saving",false);Call(main,"FinishSaveAttempt",false);Call(main,"RefreshShell");
            Check(retry.Available&&!retry.Enabled,"Failed save cannot retry until load ends");
            Call(main,"SetBusy",false);
            Check(retry.Enabled&&(bool)Get(main,"dirty"),"Load completion must re-enable retry and retain dirty state");
            Set(main,"saving",true);Call(main,"SetBusy",true);Call(main,"SetBusy",false);
            Check(!retry.Enabled,"Finishing another busy action must not enable retry during a save");
            Set(main,"saving",false);Set(main,"saveFailure",null);Set(main,"dirty",false);Call(main,"RefreshShell");
            Check(!retry.Available,"Successful save hides retry");
        }
        int writes=0;string received=null;var pending=new TaskCompletionSource<bool>();
        using(var editor=new CodeForm("test","old\n",text=>{writes++;received=text;return pending.Task;})){
            var code=(TextBox)Get(editor,"code");code.Text="new\r\n";
            var saving=(Task<bool>)Call(editor,"Save");Check(writes==1,"One save started");
            var repeated=(Task<bool>)Call(editor,"Save");Wait(repeated);Check(!repeated.Result&&writes==1,"Repeated Ctrl+S coalesces");
            var close=new FormClosingEventArgs(CloseReason.UserClosing,false);Call(editor,"OnFormClosing",close);Check(close.Cancel,"Cannot close while writing");
            code.Text="newer\r\n";pending.SetResult(true);Wait(saving);
            Check(saving.Result&&!editor.IsDisposed&&editor.DialogResult==DialogResult.None,"Save must keep editor open");
            Check(received=="new\n"&&(string)Get(editor,"savedText")=="new\r\n","Saved baseline is submitted snapshot");
            Check(code.Text!=(string)Get(editor,"savedText"),"Typing during save stays dirty");
            code.Text="new\r\n";close=new FormClosingEventArgs(CloseReason.UserClosing,false);Call(editor,"OnFormClosing",close);Check(!close.Cancel,"Clean editor can close");
        }
        Console.WriteLine("UI save safety checks passed");
    }
}
