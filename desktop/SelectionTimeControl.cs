using System;
using System.Drawing;
using System.Windows.Forms;

internal sealed class SelectionTimeControl : UserControl
{
    private readonly TrackBar progress=new TrackBar{Dock=DockStyle.Fill,TickStyle=TickStyle.None,Minimum=0,Maximum=1,SmallChange=1,Margin=Padding.Empty};
    private readonly NumericUpDown frames=new NumericUpDown(),seconds=new NumericUpDown();
    private readonly Label range=new Label{Dock=DockStyle.Fill,ForeColor=Theme.Muted,TextAlign=ContentAlignment.MiddleLeft};
    private readonly Button begin=new Button(),end=new Button();
    private readonly CheckBox selectedRange=new CheckBox{Text="物件时段",Checked=true,AutoSize=true,ForeColor=Theme.Muted,Margin=new Padding(4,5,0,0)};
    private readonly FlowLayoutPanel header=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true,Margin=Padding.Empty};
    private bool updating;private int current,fps=50,duration=1,start,finish=1;private Track track;
    public Action<int> Seek;public Action RangeChanged;
    public SelectionTimeControl()
    {
        AutoScaleMode=AutoScaleMode.None;BackColor=Theme.Panel;Font=Theme.Font;
        var table=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(7,0,7,0),Margin=Padding.Empty};
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));table.RowStyles.Add(new RowStyle(SizeType.Absolute,32));table.RowStyles.Add(new RowStyle(SizeType.Absolute,Font.Height+18));table.RowStyles.Add(new RowStyle(SizeType.Absolute,(Font.Height+18)*2));
        range.AutoSize=true;range.Dock=DockStyle.None;range.Margin=new Padding(0,4,4,4);header.Controls.Add(range);header.Controls.Add(selectedRange);table.Controls.Add(header,0,0);table.Controls.Add(progress,0,1);
        var inputs=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,Margin=Padding.Empty};inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,22));inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,22));inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        foreach(var input in new[]{frames,seconds}){input.Dock=DockStyle.Fill;input.BackColor=Theme.Raised;input.ForeColor=Theme.Text;input.Margin=new Padding(0,3,2,2);input.Maximum=1000000;}
        seconds.DecimalPlaces=3;seconds.Increment=.02m;frames.ValueChanged+=(s,e)=>{if(!updating)Jump((int)frames.Value);};seconds.ValueChanged+=(s,e)=>{if(!updating)Jump((int)Math.Round((double)seconds.Value*fps));};
        inputs.Controls.Add(new Label{Text="帧",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,0);inputs.Controls.Add(frames,1,0);inputs.Controls.Add(new Label{Text="秒",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},2,0);inputs.Controls.Add(seconds,3,0);table.Controls.Add(inputs,0,2);
        var actions=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Margin=Padding.Empty};for(int i=0;i<2;i++){actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));actions.RowStyles.Add(new RowStyle(SizeType.Percent,50));}
        Button before=new Button(),after=new Button();var buttons=new[]{begin,before,after,end};string[] names={"到开始","前一帧","后一帧","到结束"};
        for(int i=0;i<4;i++){buttons[i].Text=names[i];buttons[i].Dock=DockStyle.Fill;buttons[i].FlatStyle=FlatStyle.Flat;buttons[i].BackColor=Theme.Raised;buttons[i].ForeColor=Theme.Text;buttons[i].FlatAppearance.BorderSize=0;buttons[i].Margin=new Padding(1,2,1,3);actions.Controls.Add(buttons[i],i%2,i/2);}
        begin.Click+=(s,e)=>Jump(track==null?0:track.start);end.Click+=(s,e)=>Jump(track==null?duration:track.end);before.Click+=(s,e)=>Jump(current-1);after.Click+=(s,e)=>Jump(current+1);table.Controls.Add(actions,0,3);
        progress.ValueChanged+=(s,e)=>{if(!updating)Jump(progress.Value);};selectedRange.CheckedChanged+=(s,e)=>UpdateRange();
        Controls.Add(table);
    }
    public override Size GetPreferredSize(Size proposedSize)
    {int width=proposedSize.Width>0?proposedSize.Width:280;return new Size(width,header.GetPreferredSize(new Size(Math.Max(160,width-14),0)).Height+32+(Font.Height+18)*3+4);}
    private void Jump(int frame){if(Seek!=null)Seek(Math.Max(0,Math.Min(duration,frame)));}
    public void SetView(int fps,int duration,int frame,Track selection)
    {this.fps=fps;this.duration=duration;track=selection;current=frame;UpdateRange();SetFrame(frame);}
    private void UpdateRange()
    {
        updating=true;start=selectedRange.Checked&&track!=null?track.start:0;finish=selectedRange.Checked&&track!=null?track.end:duration;
        if(progress.Minimum>start)progress.Minimum=start;progress.Maximum=Math.Max(start,finish);progress.Minimum=start;progress.SmallChange=1;progress.LargeChange=Math.Max(1,fps);
        progress.Value=Math.Max(start,Math.Min(finish,current));range.Text=track==null?"共享播放头":"物件 "+track.start+" — "+track.end+" 帧";updating=false;if(RangeChanged!=null)RangeChanged();
    }
    public void SetFrame(int frame)
    {current=frame;updating=true;frames.Maximum=duration;frames.Value=Math.Max(0,Math.Min(duration,frame));seconds.Maximum=(decimal)duration/fps;seconds.Value=Math.Max(0,Math.Min(seconds.Maximum,(decimal)frame/fps));seconds.Increment=1m/fps;progress.Value=Math.Max(start,Math.Min(finish,frame));updating=false;}
}
