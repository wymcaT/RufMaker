using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

internal static class Program
{
    [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern IntPtr FindWindow(string className,string title);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window,int command);

    [STAThread]
    private static void Main(string[] args)
    {
        if(args.Length==3&&args[0]=="--render-native-film"){Environment.ExitCode=NativeFilmBuilder.RunJobFile(args[1],args[2]);return;}
        SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        if(args.Length>0&&args[0]=="--apply-update"){Updater.Apply(args);return;}
        string root=AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        Directory.SetCurrentDirectory(root);
        AppWindow.RefreshFileIcon(Application.ExecutablePath);
        string identity;
        using(var hash=SHA256.Create())identity=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(root.ToUpperInvariant()))).Replace("-","");
        using(var instance=new Mutex(false,"Local\\RufMakerNative-Beta-"+identity))
        {
            bool owner;
            try{owner=instance.WaitOne(0);}catch(AbandonedMutexException){owner=true;}
            if(!owner)
            {
                IntPtr window=FindWindow(null,"RufMaker Beta · 弹幕控制测试版");
                if(window==IntPtr.Zero)window=FindWindow(null,"RufMaker Beta · Bullet Control Preview");
                if(window!=IntPtr.Zero){ShowWindow(window,9);SetForegroundWindow(window);}return;
            }
            try
            {
                AppPaths.ArrangeAssets(root);
                Application.ThreadException+=(s,e)=>{
                    string log=Path.Combine(root,"build","logs","native-ui.log");Directory.CreateDirectory(Path.GetDirectoryName(log));File.AppendAllText(log,e.Exception+Environment.NewLine);
                    Language.Message(e.Exception.Message,"RufMaker",MessageBoxButtons.OK,MessageBoxIcon.Error);
                };
                Application.Run(new MainForm(root));
            }
            catch(Exception error){Language.Message(error.Message,"RufMaker",MessageBoxButtons.OK,MessageBoxIcon.Error);}
            finally{instance.ReleaseMutex();}
        }
    }
}
