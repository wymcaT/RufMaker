using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

internal static class UnionEditorContractsTests
{
    private static int checks;
    private static void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
    private static void Near(double value,double expected,string name){Check(Math.Abs(value-expected)<.000001,name+": "+value);}
    private static double Clamp(string property,double value,bool raw){return (double)typeof(MainForm).GetMethod("ClampValue",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{property,value,raw});}
    public static void Main(string[] args)
    {
        Near(Clamp("alpha",1.5,false),1,"legacy alpha constrained");Near(Clamp("alpha",1.5,true),1.5,"raw alpha preserved");
        Near(Clamp("imageDelay",-2,false),0,"Windows animation delay constrained");Near(Clamp("imageSpeed",.25,false),.25,"Windows animation speed retained");
        foreach(string field in new[]{"scaleX","scaleY","imageSpeed","bulletGravity"})Near(ScrubNumber.StepFor(5,false,"",field),.01,field+" stable scrub precision");
        var track=new Track{id="visual",kind="object",start=0,end=20,imageIndex=3,imageSpeed=2,imageDelay=4,spriteFrame=0,scaleX=-2,scaleY=.5,blendMode="add",rawAlpha=true,alpha=1.5,bulletGravity=8};
        track.keyframes.Add(new Keyframe{property="imageSpeed",frame=10,value=4});track.keyframes.Add(new Keyframe{property="imageDelay",frame=10,value=5});track.keyframes.Add(new Keyframe{property="bulletGravity",frame=10,value=12});
        var preset=new TrackPreset{fps=25,tracks=new List<Track>{track}};var result=preset.Instantiate(10,50).Single();
        Near(result.imageSpeed,1,"preset animation speed scaled once");Near(result.imageDelay,8,"preset animation delay scaled once");Near(result.bulletGravity,2,"preset gravity scaled once");
        Near(result.keyframes.Single(k=>k.property=="imageSpeed").value,2,"animation speed key scaled once");Near(result.keyframes.Single(k=>k.property=="imageDelay").value,10,"animation delay key scaled once");Near(result.keyframes.Single(k=>k.property=="bulletGravity").value,3,"gravity key scaled once");
        Check(result.spriteFrame==0&&result.imageIndex==3&&result.scaleX==-2&&result.scaleY==.5&&result.blendMode=="add"&&result.rawAlpha,"visual and animation fields coexist in presets");
        Check(track.imageSpeed==2&&track.imageDelay==4&&track.keyframes[0].value==4,"preset source stays isolated");
        var category=typeof(MainForm).GetMethod("ParameterCategory",BindingFlags.NonPublic|BindingFlags.Static);
        Check((string)category.Invoke(null,new object[]{track,"imageSpeed"})=="贴图动画","Windows animation category retained");Check((string)category.Invoke(null,new object[]{track,"spriteFrame"})=="外观","static override remains separate");
        Console.WriteLine("PASS: "+checks+" merged editor contracts (legacy/raw alpha, animation and static frame, scrub precision, preset scaling, isolation).");
    }
}
