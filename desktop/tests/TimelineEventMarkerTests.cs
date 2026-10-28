using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class TimelineEventMarkerTests
{
    private static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static object Get(object target,string name){return target.GetType().GetField(name,Flags).GetValue(target);}
    private static object Call(object target,string name,params object[] args){return target.GetType().GetMethod(name,Flags).Invoke(target,args);}
    private static void Mouse(Control target,string method,int x,int y){typeof(Control).GetMethod(method,Flags).Invoke(target,new object[]{new MouseEventArgs(MouseButtons.Left,1,x,y,0)});}
    private static Bitmap Draw(TimelineCanvas timeline,string file=null){var bitmap=new Bitmap(timeline.Width,timeline.Height);timeline.Invalidate();timeline.DrawToBitmap(bitmap,timeline.ClientRectangle);if(file!=null)bitmap.Save(file);return bitmap;}
    private static int X(TimelineCanvas timeline,int frame){return (int)Math.Round((float)Call(timeline,"X",frame));}
    private static int Center(TimelineCanvas timeline,Track track){var rect=timeline.ClipBounds(track);return (int)Math.Round(rect.Y+rect.Height/2);}
    private static int Differences(Bitmap a,Bitmap b,Rectangle area,out int minX,out int maxX)
    {int count=0;minX=Int32.MaxValue;maxX=Int32.MinValue;area.Intersect(new Rectangle(0,0,a.Width,a.Height));for(int y=area.Top;y<area.Bottom;y++)for(int x=area.Left;x<area.Right;x++)if(a.GetPixel(x,y)!=b.GetPixel(x,y)){count++;minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);}return count;}
    private static Track Hit(TimelineCanvas timeline,int x,int y){var lane=Call(timeline,"LaneAt",new Point(x,y));return lane==null?null:(Track)lane.GetType().GetField("track").GetValue(lane);}
    private static EditorState Fixture(out Track point,out Track clip,out Track script,out Track next)
    {
        point=new Track{id="point",name="Clear the screen",kind="control",eventPreset="clear-bullets",lane=0,start=50,end=50};
        next=new Track{id="next",name="Next event",kind="control",eventPreset="stop-bullets",lane=0,start=100,end=100};
        clip=new Track{id="clip",name="Object name follows the viewport",kind="object",lane=1,start=0,end=400};
        script=new Track{id="script",name="Run the GML event",kind="script",lane=2,start=70,end=70};
        var state=new EditorState{duration=1200,fps=50,laneCount=3,tracks=new List<Track>{point,next,clip,script}};Motion.Normalize(state);return state;
    }
    private static void PaintAndScroll(string folder)
    {
        Track point,clip,script,next;var state=Fixture(out point,out clip,out script,out next);
        using(var timeline=new TimelineCanvas{State=state,Size=new Size(800,300),PixelsPerSecond=200,SelectionKeyframesOnly=true,ClipSnap=false}){
            timeline.CreateControl();timeline.RefreshLayout();int x=X(timeline,point.start),y=Center(timeline,point),scriptX=X(timeline,script.start),scriptY=Center(timeline,script);
            using(var named=Draw(timeline,Path.Combine(folder,"events-and-clips.png"))){
                var left=named.GetPixel(x-5,y);var right=named.GetPixel(x+5,y);Check(left.B>left.R+35&&right.B>right.R+35,"Event is not circular around its exact frame");
                var leftScript=named.GetPixel(scriptX-5,scriptY);Check(leftScript.G>leftScript.R+25,"GML event is not a centered circle");
                string name=point.name;point.name="";using(var unnamed=Draw(timeline)){int min,max;Check(Differences(named,unnamed,new Rectangle(x+13,y-10,160,20),out min,out max)>5,"Event name not visible to the right of its marker");Check(Differences(named,unnamed,new Rectangle(0,timeline.AudioBottom,190,40),out min,out max)==0,"Event label escaped into track headers");}point.name=name;
            }
            var horizontal=(HScrollBar)Get(timeline,"horizontal");horizontal.Value=400;int row=Center(timeline,clip);string title=clip.name;
            using(var named=Draw(timeline,Path.Combine(folder,"scrolled-object-name.png"))){clip.name="";using(var unnamed=Draw(timeline)){
                int min,max,count=Differences(named,unnamed,new Rectangle(190,row-12,590,24),out min,out max);Check(count>10&&min>=200&&min<225,"Offscreen object start did not bring its name to the visible left edge: "+count+"/"+min);
                Check(Differences(named,unnamed,new Rectangle(0,timeline.AudioBottom,190,200),out min,out max)==0,"Object name painted outside the timeline viewport");}clip.name=title;}
            clip.end=210;horizontal.Value=800;timeline.RefreshLayout();int edge=X(timeline,clip.end+1);row=Center(timeline,clip);
            using(var named=Draw(timeline,Path.Combine(folder,"clipped-object-name.png"))){clip.name="";using(var unnamed=Draw(timeline)){int min,max;Check(Differences(named,unnamed,new Rectangle(190,row-12,590,24),out min,out max)>0,"Short visible object did not show any title");Check(max<edge,"Object name escaped past the end of its clip: "+max+" / "+edge);}clip.name=title;}
            horizontal.Value=1000;using(var named=Draw(timeline)){clip.name="";using(var unnamed=Draw(timeline)){int min,max;Check(Differences(named,unnamed,new Rectangle(190,timeline.AudioBottom,590,180),out min,out max)==0,"Offscreen object left a floating title");}clip.name=title;}
            point.start=0;point.end=400;next.start=next.end=500;horizontal.Value=400;int eventY=Center(timeline,point);timeline.RefreshLayout();
            using(var named=Draw(timeline,Path.Combine(folder,"scrolled-duration-event.png"))){string eventName=point.name;point.name="";using(var unnamed=Draw(timeline)){int min,max;Check(Differences(named,unnamed,new Rectangle(190,eventY-10,220,20),out min,out max)>5&&min>=194&&min<220,"Long event name did not follow its visible duration");}point.name=eventName;}
            point.start=point.end=50;next.start=next.end=100;horizontal.Value=0;timeline.Size=new Size(800,125);timeline.RefreshLayout();var vertical=(VScrollBar)Get(timeline,"vertical");vertical.Value=10;int header=timeline.AudioBottom;
            using(var named=Draw(timeline)){string eventName=point.name;point.name="";using(var unnamed=Draw(timeline)){int min,max;Check(Differences(named,unnamed,new Rectangle(190,header-18,590,18),out min,out max)==0,"Event title escaped vertically into the audio area");}point.name=eventName;}
        }
        Console.WriteLine("PASS: centered event/GML circles, readable labels, viewport-pinned clip names and clipping at both edges.");
    }
    private static void InputAndDuration()
    {
        Track point,clip,script,next;var state=Fixture(out point,out clip,out script,out next);
        using(var timeline=new TimelineCanvas{State=state,Size=new Size(800,300),PixelsPerSecond=200,SelectionKeyframesOnly=true,ClipSnap=false}){
            timeline.ReadModifiers=()=>Keys.None;timeline.CreateControl();timeline.RefreshLayout();int edits=0;timeline.BeginEdit=()=>edits++;int x=X(timeline,point.start),y=Center(timeline,point);
            Check(Hit(timeline,x-5,y)==point&&Hit(timeline,x+5,y)==point,"Visible sides of marker are not selectable");Mouse(timeline,"OnMouseDown",x,y);Check((string)Get(timeline,"dragMode")=="bar","Circle center must move the event");Mouse(timeline,"OnMouseMove",x+40,y);Mouse(timeline,"OnMouseUp",x+40,y);Check(point.start==60&&point.end==60&&edits==1,"Dragging a point event did not move exactly 10 frames");
            x=X(timeline,point.start);Mouse(timeline,"OnMouseDown",x+9,y);Check((string)Get(timeline,"dragMode")=="right","Point event cannot be extended from its right handle");Mouse(timeline,"OnMouseMove",x+49,y);Mouse(timeline,"OnMouseUp",x+49,y);Check(point.start==60&&point.end==70,"Extending event changed the start or missed its end");
            x=X(timeline,point.start);Mouse(timeline,"OnMouseDown",x-7,y);Check((string)Get(timeline,"dragMode")=="left","Long event's left edge cannot be resized");Mouse(timeline,"OnMouseMove",x+1,y);Mouse(timeline,"OnMouseUp",x+1,y);Check(point.start==62&&point.end==70,"Resizing event start changed its end");
            int scriptX=X(timeline,script.start),scriptY=Center(timeline,script);Check(Hit(timeline,scriptX+18,scriptY)==script,"Event name cannot be clicked to select the event");Mouse(timeline,"OnMouseDown",scriptX+18,scriptY);Check((string)Get(timeline,"dragMode")=="bar"&&timeline.SelectedTrack==script,"Clicking the event name did not select it");Mouse(timeline,"OnMouseUp",scriptX+18,scriptY);
            var adjacent=new Track{id="adjacent",name="Adjacent",kind="control",lane=0,start=101,end=101,eventPreset="stop-bullets"};state.tracks.Add(adjacent);timeline.RefreshLayout();timeline.SetTrackSelection(new[]{next},next);Check(Hit(timeline,X(timeline,adjacent.start),Center(timeline,adjacent))==adjacent,"Overlapping marker hit boxes did not select the nearest frame");
            timeline.SetTrackSelection(new Track[0]);x=X(timeline,point.start);y=Center(timeline,point);Mouse(timeline,"OnMouseDown",x-12,y-10);Mouse(timeline,"OnMouseMove",x-5,y-4);Mouse(timeline,"OnMouseUp",x-5,y-4);Check(timeline.SelectedTracks.Contains(point),"Box touching event circle did not select it");
            script.end=110;timeline.RefreshLayout();Check(Hit(timeline,X(timeline,105),Center(timeline,script))==null,"Invisible one-shot GML duration is still selectable as a bar");
        }
        Console.WriteLine("PASS: circle and title selection, precise event move, duration handles, adjacent events and box selection.");
    }
    [STAThread]private static int Main(string[] args){try{Application.EnableVisualStyles();PaintAndScroll(args[0]);InputAndDuration();return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
