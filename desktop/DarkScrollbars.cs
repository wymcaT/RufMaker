using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Opt native scrollbar-bearing windows into the system's dark Explorer theme.
// Scrolling, keyboard navigation and accessibility stay with the native controls.
internal static class DarkScrollbars
{
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct OSVersion
    {public int size,major,minor,build,platform;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string servicePack;}
    [StructLayout(LayoutKind.Sequential)]private struct Rect {public int left,top,right,bottom;}
    [StructLayout(LayoutKind.Sequential)]private struct ComboInfo {public int size;public Rect item,button;public int state;public IntPtr combo,edit,list;}
    [DllImport("ntdll.dll",CharSet=CharSet.Unicode)]private static extern int RtlGetVersion(ref OSVersion version);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr LoadLibrary(string file);
    [DllImport("kernel32.dll")]private static extern IntPtr GetProcAddress(IntPtr module,IntPtr name);
    [DllImport("uxtheme.dll",CharSet=CharSet.Unicode)]private static extern int SetWindowTheme(IntPtr window,string app,string classes);
    [DllImport("user32.dll")]private static extern bool GetComboBoxInfo(IntPtr combo,ref ComboInfo info);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]private delegate int PreferredMode(int mode);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] [return:MarshalAs(UnmanagedType.I1)]private delegate bool AllowWindow(IntPtr window,[MarshalAs(UnmanagedType.I1)]bool allow);
    private sealed class Binding{public IntPtr themedHandle;}
    private static readonly ConditionalWeakTable<Control,Binding> attached=new ConditionalWeakTable<Control,Binding>();
    private static readonly AllowWindow allowWindow;
    public static readonly bool Available;
    static DarkScrollbars()
    {
        var version=new OSVersion{size=Marshal.SizeOf(typeof(OSVersion))};if(RtlGetVersion(ref version)!=0||version.major<10||version.build<18362)return;
        IntPtr library=LoadLibrary(Path.Combine(Environment.SystemDirectory,"uxtheme.dll"));if(library==IntPtr.Zero)return;
        IntPtr preferred=GetProcAddress(library,(IntPtr)135),window=GetProcAddress(library,(IntPtr)133);if(preferred==IntPtr.Zero||window==IntPtr.Zero)return;
        allowWindow=(AllowWindow)Marshal.GetDelegateForFunctionPointer(window,typeof(AllowWindow));var mode=(PreferredMode)Marshal.GetDelegateForFunctionPointer(preferred,typeof(PreferredMode));mode(2);Available=true;
    }
    public static void Initialize(){}
    public static void Attach(Control control)
    {
        if(control==null||control.IsDisposed)return;Binding existing;if(attached.TryGetValue(control,out existing))return;attached.Add(control,new Binding());
        if(control is ScrollableControl||control is ScrollBar||control is ListBox||control is ListView||control is TreeView||control is TextBoxBase||control is ComboBox){control.HandleCreated+=(s,e)=>ApplyWindow(control);control.HandleDestroyed+=(s,e)=>{Binding b;if(attached.TryGetValue(control,out b))b.themedHandle=IntPtr.Zero;};if(control.IsHandleCreated)ApplyWindow(control);}
        var scroll=control as ScrollableControl;if(scroll!=null)scroll.Layout+=(s,e)=>{if(scroll.AutoScroll)ApplyWindow(scroll);};
        var combo=control as ComboBox;if(combo!=null)combo.DropDown+=(s,e)=>ApplyCombo(combo);
        control.ControlAdded+=(s,e)=>Attach(e.Control);foreach(Control child in control.Controls)Attach(child);
    }
    private static void ApplyWindow(Control control)
    {if(!Available||!control.IsHandleCreated)return;var scroll=control as ScrollableControl;if(scroll!=null&&!scroll.AutoScroll)return;Binding binding;if(!attached.TryGetValue(control,out binding)||binding.themedHandle==control.Handle)return;binding.themedHandle=control.Handle;ThemeWindow(control.Handle);var combo=control as ComboBox;if(combo!=null)ApplyCombo(combo);control.Invalidate();}
    private static void ThemeWindow(IntPtr window){if(window==IntPtr.Zero)return;allowWindow(window,true);SetWindowTheme(window,"DarkMode_Explorer",null);}
    private static void ApplyCombo(ComboBox combo)
    {if(!Available||!combo.IsHandleCreated)return;var info=new ComboInfo{size=Marshal.SizeOf(typeof(ComboInfo))};if(GetComboBoxInfo(combo.Handle,ref info)){ThemeWindow(info.list);ThemeWindow(info.edit);}}
}
