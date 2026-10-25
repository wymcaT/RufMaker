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
{
    public bool LocalizeItems=true;
    private bool refreshingLanguage;
    public WheelSafeComboBox(){FlatStyle=FlatStyle.Flat;DrawMode=DrawMode.OwnerDrawFixed;BackColor=Theme.Raised;ForeColor=Theme.Text;ItemHeight=Math.Max(20,Font.Height+6);FormattingEnabled=true;Format+=(s,e)=>{if(LocalizeItems)e.Value=Language.Text(Convert.ToString(e.Value));};}
    protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);ItemHeight=Math.Max(20,Font.Height+6);}
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        bool highlighted=DroppedDown&&(e.State&DrawItemState.Selected)!=0;Color back=highlighted?Theme.Line:BackColor;
        using(var brush=new SolidBrush(back))e.Graphics.FillRectangle(brush,e.Bounds);
        string text=e.Index>=0&&e.Index<Items.Count?GetItemText(Items[e.Index]):Text;
        Theme.Label(e.Graphics,text,new Rectangle(e.Bounds.X+4,e.Bounds.Y,Math.Max(0,e.Bounds.Width-8),e.Bounds.Height),Enabled?ForeColor:Theme.Muted,Font,localize:false);
    }
    public void RefreshLanguage(){refreshingLanguage=true;try{RefreshItems();}finally{refreshingLanguage=false;}}
    protected override void OnSelectedIndexChanged(EventArgs e){if(!refreshingLanguage)base.OnSelectedIndexChanged(e);Invalidate();}
    protected override void OnMouseWheel(MouseEventArgs e){SettingsWheel.Scroll(this,e);}
    private void PaintClosed(Graphics g)
    {
        using(var brush=new SolidBrush(BackColor))g.FillRectangle(brush,ClientRectangle);
        int arrow=Math.Max(20,SystemInformation.VerticalScrollBarWidth+4),cx=Width-arrow/2,cy=Height/2;
        using(var pen=new Pen(Focused?Theme.Accent:Theme.Line))g.DrawRectangle(pen,0,0,Width-1,Height-1);
        using(var brush=new SolidBrush(Enabled?ForeColor:Theme.Muted))g.FillPolygon(brush,new[]{new Point(cx-4,cy-2),new Point(cx+4,cy-2),new Point(cx,cy+2)});
        string text=SelectedIndex>=0?GetItemText(Items[SelectedIndex]):Text;
        Theme.Label(g,text,new Rectangle(5,1,Math.Max(0,Width-arrow-7),Height-2),Enabled?ForeColor:Theme.Muted,Font,localize:false);
    }
    protected override void WndProc(ref Message m)
    {
        if(m.Msg==0x020A){SettingsWheel.Scroll(this,new MouseEventArgs(MouseButtons.None,0,0,0,(short)((long)m.WParam>>16)));return;}
        base.WndProc(ref m);
        // The native combo frame ignores BackColor on some Windows themes.
        // Paint the closed DropDownList too, including WM_PRINT used during resize/capture.
        if(DropDownStyle==ComboBoxStyle.DropDownList){
            if(m.Msg==0x000F){using(var g=Graphics.FromHwnd(Handle))PaintClosed(g);}
            else if((m.Msg==0x0317||m.Msg==0x0318)&&m.WParam!=IntPtr.Zero){using(var g=Graphics.FromHdc(m.WParam))PaintClosed(g);}
        }
    }
}
internal sealed class WheelSafeNumericUpDown : NumericUpDown
{protected override void OnMouseWheel(MouseEventArgs e){SettingsWheel.Scroll(this,e);}protected override void WndProc(ref Message m){if(m.Msg==0x020A){SettingsWheel.Scroll(this,new MouseEventArgs(MouseButtons.None,0,0,0,(short)((long)m.WParam>>16)));return;}base.WndProc(ref m);}}
internal sealed class WheelSafeTrackBar : TrackBar
{protected override void OnMouseWheel(MouseEventArgs e){SettingsWheel.Scroll(this,e);}protected override void WndProc(ref Message m){if(m.Msg==0x020A){SettingsWheel.Scroll(this,new MouseEventArgs(MouseButtons.None,0,0,0,(short)((long)m.WParam>>16)));return;}base.WndProc(ref m);}}
