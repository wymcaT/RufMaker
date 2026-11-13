using System;
using System.Collections.Generic;
using System.Linq;

// Cross-branch runtime regression checks. This is C# simulation, not GM execution.
internal static class EventRuntimeUnionTests
{
    static int checks;
    static void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
    static void Near(double actual,double expected,string name){Check(Math.Abs(actual-expected)<.00001,name+": "+actual+" != "+expected);}
    static Track E(string id){return new Track{id=id,kind="emitter",start=0,end=0,count=1,x=100,y=100,speed=0,life=100,advancedBullets=true};}
    static BulletWorld W(params Track[] tracks){var s=new EditorState{duration=100,tracks=tracks.ToList()};Motion.Normalize(s);return new BulletWorld(s);}
    public static void Main()
    {
        var raw=E("raw");raw.advancedBullets=false;raw.rawAlpha=true;Check(BulletSystem.Advanced(new EditorState{tracks=new List<Track>{raw}}),"raw alpha selects advanced pipeline like export");
        var p=E("p");p.speed=2;p.bulletGravity=1;p.moveOnBirth=true;p.imageIndex=2;p.imageSpeed=.5;
        var w=W(p);w.Seek(0);var b=w.bullets.Single();Near(b.x,102,"birth movement x");Near(b.y,101,"one gravity composition at birth");Check(ObjectDynamics.Image(b,0)==2,"birth animation index");
        w.Seek(2);Near(w.bullets.Single().y,106,"one gravity composition each subsequent frame");Check(ObjectDynamics.Image(w.bullets.Single(),2)==3,"animation frame");w.Seek(0);Near(w.bullets.Single().y,101,"reverse rebuild birth movement");
        p=E("p");p.imageSpeed=1;p.bulletGravity=1;
        var reset=new Track{id="reset",kind="control",start=2,end=2,eventPreset="reset-motion",eventRestoreBirth=false,controlValue=0,eventResetLife=true,eventLife=50,eventDisableMotion=true};
        w=W(p,reset);w.Seek(3);b=w.bullets.Single();Check(b.birth==2,"lifetime clock reset");Check(b.animationBirth==0&&ObjectDynamics.Image(b,3)==3,"animation clock survives lifetime reset");Near(b.gravity,0,"reset clears gravity");double y=b.y;w.Seek(5);Near(w.bullets.Single().y,y,"reset stopped position persists");
        p=E("p");p.imageSpeed=1;p.scaleX=-2;p.scaleY=3;
        p.bulletActions.Add(new BulletAction{type="spawn",templateId="child",spawnTiming="action-time",start=2,duration=0,inheritAppearance=true});
        var child=E("child");child.templateOnly=true;w=W(p,child);w.Seek(2);var newborn=w.bullets.Single(x=>x.source.id=="child");Check(ObjectDynamics.Image(newborn,2)==2,"action-time child captures current animation frame");w.Seek(4);Check(ObjectDynamics.Image(w.bullets.Single(x=>x.source.id=="child"),4)==2,"inherited image remains frozen");
        Console.WriteLine("PASS EventRuntimeUnion "+checks+" checks");
    }
}
