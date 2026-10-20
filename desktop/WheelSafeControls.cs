using System;
using System.Drawing;
using System.Windows.Forms;

internal static class SettingsWheel
{
    public static void Scroll(Control control,MouseEventArgs e)
    {
        var handled=e as HandledMouseEventArgs;if(handled!=null)handled.Handled=true;
        for(Control p=control.Parent;p!=null;p=p.Parent)
        {
            var scroll=p as ScrollableControl;if(scroll==null||!scroll.AutoScroll||!scroll.VerticalScroll.Visible)continue;
            int y=Math.Max(0,Math.Min(scroll.VerticalScroll.Maximum-scroll.VerticalScroll.LargeChange+1,-scroll.AutoScrollPosition.Y-e.Delta/120*Math.Max(1,SystemInformation.MouseWheelScrollLines)*20));
            scroll.AutoScrollPosition=new Point(-scroll.AutoScrollPosition.X,y);break;
        }
    }
}
internal sealed class WheelSafeComboBox : ComboBox
{protected override void OnMouseWheel(MouseEventArgs e){SettingsWheel.Scroll(this,e);}protected override void WndProc(ref Message m){if(m.Msg==0x020A){SettingsWheel.Scroll(this,new MouseEventArgs(MouseButtons.None,0,0,0,(short)((long)m.WParam>>16)));return;}base.WndProc(ref m);}}
internal sealed class WheelSafeNumericUpDown : NumericUpDown
{protected override void OnMouseWheel(MouseEventArgs e){SettingsWheel.Scroll(this,e);}protected override void WndProc(ref Message m){if(m.Msg==0x020A){SettingsWheel.Scroll(this,new MouseEventArgs(MouseButtons.None,0,0,0,(short)((long)m.WParam>>16)));return;}base.WndProc(ref m);}}
internal sealed class WheelSafeTrackBar : TrackBar
{protected override void OnMouseWheel(MouseEventArgs e){SettingsWheel.Scroll(this,e);}protected override void WndProc(ref Message m){if(m.Msg==0x020A){SettingsWheel.Scroll(this,new MouseEventArgs(MouseButtons.None,0,0,0,(short)((long)m.WParam>>16)));return;}base.WndProc(ref m);}}
