using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

#pragma warning disable 0649
internal sealed class TimelineView
{
    public float pixelsPerSecond=80;
    public int horizontal,vertical;
    public string[] expanded=new string[0];
    public string[] collapsedGenerationGroups=new string[0];
    public bool focusGenerationGroups=true;
    public string audioDisplay="waveform";public int audioHeight=50;
    public System.Collections.Generic.Dictionary<string,int> rowHeights=new System.Collections.Generic.Dictionary<string,int>();
}
internal sealed class WorkspaceView
{
    public int version=2,x,y,width=1500,height=940,leftWidth=220,rightWidth=340,timelineHeight=300,frame;
    public double cameraZoom=1,cameraX,cameraY,previewRate=1;
    public bool positioned,maximized,grid=true,hitboxes,renderCamera,keySnap,objectStatistics;
    public string projectPath="";
    public string[] recentProjects=new string[0],collapsedSections=new string[0];
    public bool leftHidden,rightHidden;public int inspectorPage;
    public string resourceKind="objects",search="",selectedTrack,selectedProperty="x";
    public TimelineView timeline=new TimelineView();
    public static WorkspaceView Load(string root)
    {string file=Path.Combine(root,".rufmaker-view.json"),beta=Path.Combine(root,".rufmaker-beta-view.json");if(File.Exists(beta)&&(!File.Exists(file)||File.GetLastWriteTimeUtc(beta)>File.GetLastWriteTimeUtc(file)))file=beta;try{return File.Exists(file)?Json.Read<WorkspaceView>(File.ReadAllText(file)):null;}catch{return null;}}
    public void Save(string root)
    {
        string file=Path.Combine(root,".rufmaker-view.json"),temp=file+".tmp";File.WriteAllText(temp,Json.Write(this),new System.Text.UTF8Encoding(false));
        if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);
    }
    public Rectangle VisibleBounds()
    {
        var desired=new Rectangle(x,y,Math.Max(1100,width),Math.Max(740,height));
        var screen=Screen.AllScreens.OrderByDescending(s=>{var r=Rectangle.Intersect(s.WorkingArea,desired);return (long)r.Width*r.Height;}).First().WorkingArea;
        desired.Width=Math.Min(desired.Width,screen.Width);desired.Height=Math.Min(desired.Height,screen.Height);
        desired.X=Math.Max(screen.Left,Math.Min(desired.X,screen.Right-desired.Width));desired.Y=Math.Max(screen.Top,Math.Min(desired.Y,screen.Bottom-desired.Height));return desired;
    }
}
#pragma warning restore 0649
