using System;
using System.Drawing;
using System.Windows.Forms;

internal sealed class SelectionTimeControl : UserControl
{
    private readonly TrackBar progress=new WheelSafeTrackBar{Dock=DockStyle.Fill,AutoSize=false,TickStyle=TickStyle.None,Minimum=0,Maximum=1,SmallChange=1,Margin=Padding.Empty};
    private TableLayoutPanel table;
    private readonly NumericUpDown startInput=new WheelSafeNumericUpDown(),endInput=new WheelSafeNumericUpDown();
    private readonly Label range=new Label{Dock=DockStyle.Fill,ForeColor=Theme.Muted,TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true};
    private readonly CheckBox selectedRange=new CheckBox{Text="物件时段",Checked=true,AutoSize=true,ForeColor=Theme.Muted,Margin=Padding.Empty,Dock=DockStyle.Right};
    private bool updating;private int current,duration=1,start,finish=1;private Track track;
    public Action<int> Seek;public Func<int,int> SnapFrame;public Action<int,int> EditRange;
    public SelectionTimeControl()
    {
        AutoScaleMode=AutoScaleMode.None;BackColor=Theme.Panel;ForeColor=Theme.Text;Font=Theme.Font;
        AutoSize=true;AutoSizeMode=AutoSizeMode.GrowAndShrink;
        table=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(7,0,7,3),Margin=Padding.Empty};for(int i=0;i<3;i++)table.RowStyles.Add(new RowStyle(SizeType.Absolute,Font.Height+12));
        var header=new Panel{Dock=DockStyle.Fill};header.Controls.Add(range);header.Controls.Add(selectedRange);table.Controls.Add(header,0,0);
        var navigation=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=Padding.Empty,Padding=Padding.Empty};navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,22));navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,56));navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,22));
        var begin=MakeButton("起点");var end=MakeButton("终点");begin.Click+=(s,e)=>Jump(track==null?0:track.start);end.Click+=(s,e)=>Jump(track==null?duration:track.end);navigation.Controls.Add(begin,0,0);navigation.Controls.Add(progress,1,0);navigation.Controls.Add(end,2,0);table.Controls.Add(navigation,0,1);
        var timing=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=1,Margin=Padding.Empty};timing.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));timing.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));timing.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));timing.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        foreach(var input in new[]{startInput,endInput}){input.Dock=DockStyle.Fill;input.BackColor=Theme.Raised;input.ForeColor=Theme.Text;input.Margin=new Padding(2,2,2,0);input.Maximum=1000000;}
        timing.Controls.Add(new Label{Text="开始帧",AutoSize=true,Anchor=AnchorStyles.Left},0,0);timing.Controls.Add(startInput,1,0);timing.Controls.Add(new Label{Text="结束帧",AutoSize=true,Anchor=AnchorStyles.Left},2,0);timing.Controls.Add(endInput,3,0);table.Controls.Add(timing,0,2);Controls.Add(table);
        startInput.ValueChanged+=(s,e)=>{if(!updating&&EditRange!=null)EditRange((int)startInput.Value,(int)endInput.Value);};endInput.ValueChanged+=(s,e)=>{if(!updating&&EditRange!=null)EditRange((int)startInput.Value,(int)endInput.Value);};
        progress.ValueChanged+=(s,e)=>{if(!updating)Jump(Math.Max(start,Math.Min(finish,SnapFrame==null?progress.Value:SnapFrame(progress.Value))));};selectedRange.CheckedChanged+=(s,e)=>UpdateRange();UpdateMetrics();
    }
    private Button MakeButton(string text){var button=new Button{Text=text,Dock=DockStyle.Fill,FlatStyle=FlatStyle.Flat,BackColor=Theme.Raised,ForeColor=Theme.Text,Margin=new Padding(1),TabStop=false,Padding=Padding.Empty};button.FlatAppearance.BorderSize=0;return button;}
    private int TimingHeight {get{return Math.Max(Font.Height+10,startInput.PreferredSize.Height+6);}}
    private void UpdateMetrics(){if(table==null)return;table.RowStyles[0].Height=Font.Height+12;table.RowStyles[1].Height=Math.Max(32,Font.Height+16);table.RowStyles[2].Height=TimingHeight;}
    protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);if(table!=null){UpdateMetrics();PerformLayout();if(Parent!=null)Parent.PerformLayout();}}
    public override Size GetPreferredSize(Size proposedSize){return new Size(proposedSize.Width>0?proposedSize.Width:300,Font.Height+12+Math.Max(32,Font.Height+16)+TimingHeight+4);}
    private void Jump(int frame){if(Seek!=null)Seek(Math.Max(0,Math.Min(duration,frame)));}
    public void SetView(int fps,int duration,int frame,Track selection){this.duration=duration;track=selection;current=frame;UpdateRange();SetFrame(frame);}
    private void UpdateRange()
    {
        updating=true;start=selectedRange.Checked&&track!=null?track.start:0;finish=selectedRange.Checked&&track!=null?track.end:duration;
        if(progress.Minimum>start)progress.Minimum=start;progress.Maximum=Math.Max(start,finish);progress.Minimum=start;progress.SmallChange=1;progress.LargeChange=1;progress.Value=Math.Max(start,Math.Min(finish,current));range.Text=Language.Text(track==null?"共享播放头":"共享时间")+" · "+current;
        startInput.Enabled=endInput.Enabled=track!=null&&track.kind!="native"&&track.kind!="pattern";startInput.Maximum=endInput.Maximum=duration;startInput.Value=track==null?0:Math.Max(0,Math.Min(duration,track.start));endInput.Value=track==null?duration:Math.Max(0,Math.Min(duration,track.end));updating=false;
    }
    public void SetFrame(int frame){current=frame;updating=true;progress.Value=Math.Max(start,Math.Min(finish,frame));range.Text=Language.Text("共享时间")+" · "+frame;updating=false;}
}
