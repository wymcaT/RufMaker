using System;
using System.Globalization;
using System.Linq;
using System.Collections.Generic;

internal sealed partial class MainForm
{
    private void MoveSelectionToLane(int lane)
    {if(state==null||timeline.SelectedTracks.Count==0)return;PushUndo();foreach(var t in timeline.SelectedTracks.Where(t=>t.kind!="native"))t.lane=lane;state.laneCount=Math.Max(state.laneCount,lane+1);timeline.ActiveLane=lane;MarkChanged();}
    private void RerollRandom()
    {
        if(state==null||busy)return;StopPlaying();properties.CommitPending();PushUndo();
        using(var random=System.Security.Cryptography.RandomNumberGenerator.Create()){var bytes=new byte[4];random.GetBytes(bytes);state.randomSeed=(int)(BitConverter.ToUInt32(bytes,0)%2147483646);}
        RandomParameters.Resolve(state);MarkChanged();properties.Refresh();SetStatus("已重新随机 · 预览种子 "+state.randomSeed);
    }
    private ParameterActions ParameterControls(Track[] tracks,string property,bool animate=true,Keyframe explicitKey=null)
    {
        var t=tracks[0];Func<Keyframe> key=()=>explicitKey??(Motion.HasKeys(t,property)?t.keyframes.FirstOrDefault(k=>k.property==property&&k.frame==frame):null);
        Func<string> expression=()=>{var k=key();if(k!=null)return k.expression??"";if(Motion.HasKeys(t,property))return "";string value;return t.expressions.TryGetValue(property,out value)?value:"";};
        Action refresh=()=>{MarkChanged();if(IsHandleCreated)BeginInvoke(new Action(ShowProperties));};
        Action<string> assign=text=>{
            var slots=tracks.ToDictionary(track=>track,track=>{var k=explicitKey??track.keyframes.FirstOrDefault(item=>item.property==property&&item.frame==frame);return explicitKey!=null||Motion.HasKeys(track,property)?k!=null&&!String.IsNullOrEmpty(k.randomId)?k.randomId:Guid.NewGuid().ToString("N"):"base";});
            foreach(var track in tracks)RandomExpression.Evaluate(text,state.randomSeed,(String.IsNullOrEmpty(track.randomId)?track.id:track.randomId)+"|"+property+"|"+slots[track],ExpressionContext.For(track,frame));PushUndo();
            foreach(var track in tracks){
                if(String.IsNullOrEmpty(track.randomId))track.randomId=track.id;
                if(explicitKey!=null){explicitKey.expression=text;explicitKey.randomId=slots[track];}
                else if(Motion.HasKeys(track,property)){Motion.SetKey(track,property,frame,Motion.Value(track,property,frame));var k=track.keyframes.First(item=>item.property==property&&item.frame==frame);k.expression=text;k.randomId=slots[track];}
                else track.expressions[property]=text;
            }
            RandomParameters.Resolve(state);MarkChanged();
        };
        return new ParameterActions{EditExpression=()=>EditParameterExpression(tracks,property,explicitKey),Random=()=>!String.IsNullOrWhiteSpace(expression()),Expression=expression,SetExpression=assign,Keyed=()=>tracks.All(track=>Motion.HasKeys(track,property)),
            ToggleRandom=()=>{if(String.IsNullOrWhiteSpace(expression())){double current=explicitKey==null?Motion.Value(t,property,frame):explicitKey.value;string n=current.ToString("R",CultureInfo.InvariantCulture);assign("random_range("+n+", "+n+")");}else{PushUndo();foreach(var track in tracks){var k=explicitKey??track.keyframes.FirstOrDefault(item=>item.property==property&&item.frame==frame);double frozen=Motion.Value(track,property,frame);if(k!=null){k.expression="";k.value=frozen;}else{track.expressions.Remove(property);Motion.SetBase(track,property,frozen);}}}refresh();},
            ToggleKey=animate&&explicitKey==null&&tracks.All(track=>Motion.Fields(track).Contains(property))?(Action)(()=>{
                PushUndo();bool disable=tracks.All(track=>Motion.HasKeys(track,property));foreach(var track in tracks){double value=Motion.Value(track,property,frame);if(disable){track.keyframes.RemoveAll(item=>item.property==property);Motion.SetBase(track,property,value);track.expressions.Remove(property);}
                else if(!Motion.HasKeys(track,property)){string formula;track.expressions.TryGetValue(property,out formula);track.expressions.Remove(property);Motion.SetKey(track,property,frame,value);var k=track.keyframes.First(item=>item.property==property&&item.frame==frame);k.expression=formula??"";k.randomId="base";}timeline.Expand(track,true);}timeline.ClearKeySelection();selectedKey=null;timeline.SelectedProperty=property;RandomParameters.Resolve(state);refresh();}):null};
    }
    private string NumericProperty(string name)
    {
        foreach(string p in new[]{"x","y","scale","alpha"})if(name=="统一"+Motion.Name(p)||name=="整组 "+p.ToUpper()+" · 主物件")return p;
        var extra=new Dictionary<string,string>{{"最大转弯 / 度每帧","turnRate"},{"开始跟踪前等待 / 帧","homingDelay"},{"发射间隔","interval"},{"Power 指数","easePower"},{"ExpSin 周期","easePeriod"},{"震动频率","fxFrequency"},{"模糊采样数","fxSamples"},{"X 速度","velocityX"},{"Y 速度","velocityY"},{"X 加速度","accelerationX"},{"Y 加速度","accelerationY"},{"镜头旋转速度 °/帧","angularSpeed"},{"自转速度 °/帧","angularSpeed"},{"公转速度 °/帧","orbitSpeed"},{"生成间隔 / 帧","trailInterval"},{"拖影寿命 / 帧","trailLife"},{"初始不透明度","trailStartAlpha"},{"大小衰减 / 帧","trailScaleDecay"},{"不透明度衰减 / 帧","trailAlphaDecay"},{"移动速度 / 像素每帧","trailSpeed"},{"移动方向 / 度","trailDirection"}};
        string property;return extra.TryGetValue(name,out property)?property:null;
    }
}
