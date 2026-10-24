using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal sealed partial class TimelineCanvas
{
    private const int Ruler=26;
    private int audioHeight=50;private string audioDisplay="waveform",resizeKey;private int resizeHeight;
    private readonly Dictionary<string,int> rowHeights=new Dictionary<string,int>();
    private WaveData wave;private SpectrogramData spectrum;private Bitmap spectrumStrip;private string stripKey;private ContextMenuStrip audioMenu;
    public Action SpectrumRequested;
    public string SpectrumStatus="";
    public WaveData Wave {get{return wave;}set{wave=value;Spectrum=null;Invalidate();if(audioDisplay=="spectrogram"&&SpectrumRequested!=null)SpectrumRequested();}}
    public SpectrogramData Spectrum {get{return spectrum;}set{spectrum=value;ClearSpectrumStrip();Invalidate();}}
    public string AudioDisplay {get{return audioDisplay;}set{audioDisplay=value=="spectrogram"?"spectrogram":"waveform";if(audioDisplay=="spectrogram"){if(audioHeight<100)audioHeight=180;if(SpectrumRequested!=null)SpectrumRequested();}RefreshLayout();}}
    private int Header {get{return Ruler+Math.Min(audioHeight,Math.Max(20,Height-horizontal.Height-Ruler-Row));}}
    public int AudioHeight {get{return audioHeight;}}
    public int AudioBottom {get{return Header;}}
    public void SetAudioHeight(int height){audioHeight=Math.Max(20,Math.Min(600,height));RefreshLayout();}
    public void SetTrackHeight(Track track,int height){SetRowHeight(track.kind=="native"?"native:"+track.id+"|":"lane:"+track.lane,height);}
    public int TrackHeight(Track track){return GetRowHeight(track.kind=="native"?"native:"+track.id+"|":"lane:"+track.lane);}
    private int GetRowHeight(string key){int value;return rowHeights.TryGetValue(key,out value)?value:Row;}
    private void SetRowHeight(string key,int height){rowHeights[key]=Math.Max(Row,Math.Min(600,height));RefreshLayout();}
    private string RowKey(Lane lane){return lane.clips!=null?"lane:"+lane.index:"native:"+lane.track.id+"|"+(lane.property??"");}
    private int RowIndex(int y)
    {int lo=0,hi=lanes.Count-1;while(lo<=hi){int mid=(lo+hi)/2;var row=lanes[mid];if(y<row.top)hi=mid-1;else if(y>=row.top+row.height)lo=mid+1;else return mid;}return -1;}
    private string HeightBoundary(Point p)
    {
        if(Math.Abs(p.Y-Header)<=3)return "@audio";
        if(p.X>=Names||p.Y<Header)return null;int y=p.Y-Header+vertical.Value;
        int row=RowIndex(y-3);if(row>=0&&Math.Abs(y-lanes[row].top-lanes[row].height)<=4)return RowKey(lanes[row]);return null;
    }
    private bool BeginHeight(Point p)
    {resizeKey=HeightBoundary(p);if(resizeKey==null)return false;resizeHeight=resizeKey=="@audio"?audioHeight:GetRowHeight(resizeKey);dragMode="height";Capture=true;Cursor=Cursors.HSplit;return true;}
    private int DragRow(int y)
    {
        y=Math.Max(0,y-Header+vertical.Value);
        for(int i=0;i<dragRowLanes.Length;i++)if(y<dragRowTops[i]+dragRowHeights[i])return i;
        int total=dragRowLanes.Length==0?0:dragRowTops.Last()+dragRowHeights.Last();return dragRowLanes.Length+(y-total)/Row;
    }
    internal ContextMenuStrip CreateAudioMenu()
    {
        var menu=new ContextMenuStrip{BackColor=Theme.Panel,ForeColor=Theme.Text};
        foreach(var mode in new[]{new[]{"waveform","波形图"},new[]{"spectrogram","频谱图"}}){string key=mode[0];var item=new ToolStripMenuItem(mode[1]){Checked=audioDisplay==key};item.Click+=(s,e)=>AudioDisplay=key;menu.Items.Add(item);}
        menu.Items.Add(new ToolStripSeparator());var heights=new ToolStripMenuItem("音频轨高度");foreach(int value in new[]{50,140,260,400}){int height=value;heights.DropDownItems.Add(height+" px",null,(s,e)=>SetAudioHeight(height));}menu.Items.Add(heights);menu.Items.Add("还原所有轨道高度",null,(s,e)=>{rowHeights.Clear();SetAudioHeight(audioDisplay=="spectrogram"?180:50);});Language.Attach(menu);return menu;
    }
    protected override void WndProc(ref Message m)
    {
        if(m.Msg==0x007b){long position=m.LParam.ToInt64();var screen=new Point((short)(position&65535),(short)((position>>16)&65535));var p=screen.X==-1&&screen.Y==-1?PointToClient(System.Windows.Forms.Cursor.Position):PointToClient(screen);
            if(p.Y>=Ruler&&p.Y<Header){if(audioMenu!=null)audioMenu.Dispose();audioMenu=CreateAudioMenu();audioMenu.Show(this,p);m.Result=IntPtr.Zero;return;}}
        base.WndProc(ref m);
    }
    private void ClearSpectrumStrip(){if(spectrumStrip!=null)spectrumStrip.Dispose();spectrumStrip=null;stripKey=null;}
    private void DrawAudio(Graphics g,int width)
    {
        int plotWidth=width-Names,plotHeight=Header-Ruler;using(var brush=new SolidBrush(Color.FromArgb(11,15,22)))g.FillRectangle(brush,Names,Ruler,plotWidth,plotHeight);
        Theme.Label(g,"♫ "+(String.IsNullOrEmpty(State.musicTitle)?System.IO.Path.GetFileName(State.music):State.musicTitle),new Rectangle(12,Ruler,Names-75,28),Theme.Muted,localize:false);
        if(plotHeight>55)Theme.Label(g,audioDisplay=="spectrogram"?"频谱 · Hz":"波形",new Rectangle(12,Header-27,Names-75,24),Theme.Muted,Theme.Small);
        var saved=g.Save();g.SetClip(new Rectangle(Names,Ruler,plotWidth,plotHeight),System.Drawing.Drawing2D.CombineMode.Intersect);
        if(audioDisplay=="spectrogram")
        {
            if(spectrum==null)Theme.Label(g,String.IsNullOrEmpty(SpectrumStatus)?"右键选择频谱图以分析歌曲":SpectrumStatus,new Rectangle(Names+10,Ruler+5,Math.Max(1,plotWidth-20),Math.Max(24,plotHeight-10)),Theme.Muted);
            else {BuildSpectrumStrip(plotWidth);g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;g.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.Half;g.DrawImage(spectrumStrip,new Rectangle(Names,Ruler,plotWidth,plotHeight),0,0,spectrumStrip.Width,spectrumStrip.Height,GraphicsUnit.Pixel);}
        }
        else if(Wave!=null&&Wave.peaks!=null&&Wave.duration>0)using(var pen=new Pen(Color.FromArgb(100,192,203)))
        {
            float center=Ruler+plotHeight/2f,scale=Math.Max(1,plotHeight/2f-5);
            for(int x=0;x<plotWidth;x+=2){double first=(x+horizontal.Value)/PixelsPerSecond,last=(x+2+horizontal.Value)/PixelsPerSecond;int a=Math.Max(0,(int)(first/Wave.duration*Wave.peaks.Length)),b=Math.Min(Wave.peaks.Length,(int)Math.Ceiling(last/Wave.duration*Wave.peaks.Length));double peak=0;for(int i=a;i<b;i++)peak=Math.Max(peak,Wave.peaks[i]);float h=(float)Math.Min(1,peak)*scale;g.DrawLine(pen,Names+x,center-h,Names+x,center+h);}
        }
        g.Restore(saved);
        if(audioDisplay=="spectrogram"&&spectrum!=null){int previous=-1000000;foreach(double hz in new[]{20000.0,10000,5000,2000,1000,500,200,100,50,20}){if(hz>spectrum.maxFrequency||hz<spectrum.minFrequency)continue;int y=Ruler+(int)((1-Math.Log(hz/spectrum.minFrequency)/Math.Log(spectrum.maxFrequency/spectrum.minFrequency))*plotHeight);if(y-previous<Theme.Small.Height+2)continue;previous=y;Theme.Label(g,hz>=1000?(hz/1000).ToString("0.#")+"k":hz.ToString("0"),new Rectangle(Names-55,Math.Max(Ruler,Math.Min(Header-Theme.Small.Height-3,y-Theme.Small.Height/2)),50,Theme.Small.Height+3),Theme.Muted,Theme.Small,TextFormatFlags.Right|TextFormatFlags.VerticalCenter,localize:false);}}
        using(var pen=new Pen(Theme.Muted))g.DrawLine(pen,Names/2-12,Header-3,Names/2+12,Header-3);
    }
    private void BuildSpectrumStrip(int width)
    {
        string key=width+"|"+horizontal.Value+"|"+PixelsPerSecond;if(spectrumStrip!=null&&stripKey==key)return;ClearSpectrumStrip();stripKey=key;
        spectrumStrip=new Bitmap(width,spectrum.bands,PixelFormat.Format8bppIndexed);var palette=spectrumStrip.Palette;for(int i=0;i<256;i++)palette.Entries[i]=Color.FromArgb(i,i,i);spectrumStrip.Palette=palette;
        var data=spectrumStrip.LockBits(new Rectangle(0,0,width,spectrum.bands),ImageLockMode.WriteOnly,PixelFormat.Format8bppIndexed);
        try{var bytes=new byte[data.Stride*spectrum.bands];double step=spectrum.hop/spectrum.rate;
            for(int x=0;x<width;x++){double first=(x+horizontal.Value)/PixelsPerSecond,last=(x+1+horizontal.Value)/PixelsPerSecond;int a=Math.Max(0,(int)Math.Floor(first/step)),b=Math.Min(spectrum.columns,Math.Max(a+1,(int)Math.Ceiling(last/step)));for(int row=0;row<spectrum.bands;row++){byte max=0;for(int c=a;c<b;c++)max=Math.Max(max,spectrum.levels[c*spectrum.bands+row]);bytes[(spectrum.bands-1-row)*data.Stride+x]=max;}}
            Marshal.Copy(bytes,0,data.Scan0,bytes.Length);
        }finally{spectrumStrip.UnlockBits(data);}
    }
    protected override void Dispose(bool disposing){if(disposing){ClearSpectrumStrip();if(audioMenu!=null)audioMenu.Dispose();tips.Dispose();}base.Dispose(disposing);}
}
