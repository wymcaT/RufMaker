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
}
internal sealed class WorkspaceView
{
    public int version=1,x,y,width=1500,height=940,leftWidth=220,rightWidth=300,timelineHeight=285,frame;
    public double cameraZoom=1,cameraX,cameraY;
    public bool positioned,maximized,grid=true,hitboxes,renderCamera,keySnap;
    public string projectPath="";
    public string resourceKind="objects",search="",selectedTrack,selectedProperty="x";
    public TimelineView timeline=new TimelineView();
    public static WorkspaceView Load(string root)
    {string file=Path.Combine(root,".rufmaker-view.json");try{return File.Exists(file)?Json.Read<WorkspaceView>(File.ReadAllText(file)):null;}catch{return null;}}
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
