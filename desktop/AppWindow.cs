using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;

internal static class AppWindow
{
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window,int command);
    [DllImport("user32.dll")] private static extern bool AllowSetForegroundWindow(int process);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode,ExactSpelling=true)] private static extern void SHChangeNotify(uint change,uint flags,string item,IntPtr extra);
    public static Icon LoadIcon()
    {
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("RufMaker.App.ico"))
        {if(stream==null)return (Icon)SystemIcons.Application.Clone();using(var icon=new Icon(stream,32,32))return (Icon)icon.Clone();}
    }
    public static void RefreshFileIcon(string executable){SHChangeNotify(0x2000,0x0005,executable,IntPtr.Zero);}
    public static void FocusGame(int pid)
    {
        if(pid<=0)return;
        try{using(var process=Process.GetProcessById(pid)){if(process.HasExited)return;IntPtr window=process.MainWindowHandle;if(window==IntPtr.Zero)return;AllowSetForegroundWindow(pid);ShowWindow(window,9);SetForegroundWindow(window);}}
        catch(ArgumentException){}catch(InvalidOperationException){}
    }
}
