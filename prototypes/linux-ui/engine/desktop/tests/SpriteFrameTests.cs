using System;
using System.Linq;
internal static class SpriteFrameTests
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Main()
    {
        var t=new Track{kind="object",sprite="sprAnimated",color="#80A0C0"};Check(t.spriteFrame==-1&&Rainbow.Index(t,0,0)==0,"Legacy default");
        t.spriteFrame=2.7;Check(Rainbow.Index(t,0,0)==2,"Explicit floor");Check(Motion.Fields(t).Contains("spriteFrame"),"Editable property");
        t.rainbow=true;t.rainbowSprite="sprAnimated";var s=new EditorState();s.tracks.Add(t);Motion.Normalize(s);Check(t.color=="#80A0C0"&&Rainbow.Index(t,19,50)==2,"Tint/frame override");
        Motion.EditValue(t,"spriteFrame",0,3);Check(t.spriteFrame==3&&t.keyframes.Count==0,"Base editing");
        t.keyframes.Add(new Keyframe{property="spriteFrame",frame=0,value=2,easing="step"});t.keyframes.Add(new Keyframe{property="spriteFrame",frame=10,value=5});Check(Rainbow.Index(t,0,9)==2&&Rainbow.Index(t,0,10)==5,"Step frame animation");
        var copy=t.Snapshot();Check(copy.spriteFrame==3&&copy.keyframes.Count==2,"Snapshot");var restored=Json.Read<Track>(Json.Write(t));Check(Rainbow.Index(restored,0,10)==5&&restored.color=="#80A0C0","JSON");
        Console.WriteLine("PASS: sprite frame legacy, tint, keys, editing, snapshot and JSON");
    }
}
