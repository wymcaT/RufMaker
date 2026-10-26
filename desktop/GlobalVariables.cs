using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

internal static class GlobalVariables
{
    public static bool IsReference(string name){return name!=null&&name.StartsWith("global.",StringComparison.Ordinal)&&Regex.IsMatch(name,@"^global\.[a-z_][a-z0-9_]*$");}
    public static string Name(string name){name=(name??"").Trim().ToLowerInvariant();if(!Regex.IsMatch(name,@"^[a-z_][a-z0-9_]*$")||name.StartsWith("rf_"))throw new FormatException("变量名请使用英文字母、数字和下划线，不能以数字或 rf_ 开头。");return name;}
    public static void Attach(EditorState state)
    {
        if(state.globalSource!=null){state.variableTimeline=null;return;}
        if(!Declarations(state.tracks).Any()){state.variableTimeline=null;return;}
        var flattened=BulletSystem.Expand(state,false);BulletSystem.Attach(flattened);foreach(var t in flattened.tracks.Where(t=>t.kind=="variable")){t.variableName=Name(t.variableName);t.variableExpression=String.IsNullOrWhiteSpace(t.variableExpression)?"0":t.variableExpression.Trim();if(t.variableRepeat<0||Double.IsNaN(t.variableDefault)||Double.IsInfinity(t.variableDefault))throw new FormatException("全局变量参数无效。");RandomExpression.Parse(t.variableExpression);}
        state.variableTimeline=flattened.tracks.Any(t=>t.kind=="variable")?new GlobalVariableTimeline(flattened):null;
    }
    public static IDictionary<string,double> At(EditorState state,double frame)
    {if(state==null)return null;if(state.globalSource!=null)return At(state.globalSource,frame);return state.variableTimeline==null?null:state.variableTimeline.At(Math.Max(0,(int)Math.Floor(frame)));}
    public static IEnumerable<string> References(Track t)
    {
        var texts=t.expressions.Values.Concat(t.keyframes.Select(k=>k.expression)).Concat(new[]{t.variableExpression,t.flightExpression,t.controlExpression,t.colorExpression}).Concat(TemplateRuntime.Actions(t).SelectMany(a=>new[]{a.expression,a.spawnColorExpression}));
        return texts.SelectMany(text=>Regex.Matches(text??"",@"\bglobal\.([a-z_][a-z0-9_]*)\b",RegexOptions.IgnoreCase).Cast<Match>().Select(m=>m.Groups[1].Value.ToLowerInvariant())).Concat(t.children.SelectMany(References)).Distinct();
    }
    public static IEnumerable<Track> Declarations(IEnumerable<Track> tracks){foreach(var t in tracks){if(t.kind=="variable")yield return t;foreach(var child in Declarations(t.children))yield return child;}}
    public static void ReplaceReference(Track t,string oldName,string newName)
    {
        Func<string,string> replace=text=>Regex.Replace(text??"",@"\bglobal\."+Regex.Escape(oldName)+@"\b","global."+newName,RegexOptions.IgnoreCase);
        if(t.kind=="variable"&&t.variableName==oldName){t.variableName=newName;if(t.name=="全局变量 · "+oldName)t.name="全局变量 · "+newName;}t.variableExpression=replace(t.variableExpression);foreach(string key in t.expressions.Keys.ToArray())t.expressions[key]=replace(t.expressions[key]);foreach(var k in t.keyframes)k.expression=replace(k.expression);t.flightExpression=replace(t.flightExpression);t.controlExpression=replace(t.controlExpression);t.colorExpression=replace(t.colorExpression);foreach(var a in TemplateRuntime.Actions(t)){a.expression=replace(a.expression);a.spawnColorExpression=replace(a.spawnColorExpression);}foreach(var child in t.children)ReplaceReference(child,oldName,newName);
    }
    public static void RemapCopies(List<Track> copies,EditorState target)
    {
        var used=new HashSet<string>(Declarations(target.tracks).Select(t=>t.variableName));foreach(string name in Declarations(copies).Select(t=>t.variableName).Distinct().ToArray()){string next=name;int n=2;while(used.Contains(next))next=name+"_"+(n++);used.Add(next);if(next!=name)foreach(var t in copies)ReplaceReference(t,name,next);}
    }
}

// Event history is built once per immutable scene, in song-frame / timeline order.
// Reads at old frames use stored samples; they never consume new random numbers.
internal sealed class GlobalVariableTimeline
{
    private sealed class Sample {public int frame;public double value;}
    private readonly EditorState state;private readonly List<Track> tracks;
    private readonly Dictionary<string,double> current=new Dictionary<string,double>();
    private readonly Dictionary<string,List<Sample>> history=new Dictionary<string,List<Sample>>();
    private readonly SortedSet<Tuple<int,int>> events=new SortedSet<Tuple<int,int>>();
    private int cachedFrame=-1;private IDictionary<string,double> cached;private readonly object gate=new object();
    public GlobalVariableTimeline(EditorState state)
    {
        this.state=state;tracks=state.tracks.Where(t=>t.kind=="variable").ToList();
        for(int i=0;i<tracks.Count;i++){var t=tracks[i];if(!current.ContainsKey(t.variableName)){current[t.variableName]=t.variableDefault;history[t.variableName]=new List<Sample>{new Sample{frame=-1,value=t.variableDefault}};}int first=TimelineLayout.ClockStart(t);if(t.variableRepeat>0)first+=Math.Max(0,(int)Math.Ceiling((t.start-first)/(double)t.variableRepeat))*t.variableRepeat;if(t.enabled&&!t.templateOnly&&first>=t.start&&first<=t.end)events.Add(Tuple.Create(first,i));}
        foreach(var t in tracks)Validate(RandomExpression.Parse(t.variableExpression));
    }
    private void Validate(RandomExpression.Node node)
    {if(GlobalVariables.IsReference(node.op)&&!current.ContainsKey(node.op.Substring(7)))throw new FormatException("未定义全局变量："+node.op);foreach(var a in node.args)Validate(a);}
    public IDictionary<string,double> At(int frame)
    {
        lock(gate){if(frame==cachedFrame)return cached;
            while(events.Count>0&&events.Min.Item1<=frame){var e=events.Min;events.Remove(e);var t=tracks[e.Item2];int f=e.Item1;
                if(t.parent==null||BulletSystem.Active(t.parent,f)){
                    var player=BulletSystem.PlayerAt(state,f);var c=new ExpressionContext{globals=current,playerX=player.x,playerY=player.y,frame=f,age=f-TimelineLayout.ClockStart(t),fps=state.fps,x=t.x,y=t.y,sourceX=t.x,sourceY=t.y,x1=t.x1,y1=t.y1,x2=t.x2,y2=t.y2,direction=t.angle,speed=t.speed,randomFrame=f};
                    double value=RandomExpression.Evaluate(t.variableExpression,state.randomSeed,(String.IsNullOrEmpty(t.randomId)?t.id:t.randomId)+"|variable|base",c);
                    current[t.variableName]=value;history[t.variableName].Add(new Sample{frame=f,value=value});
                }
                if(t.variableRepeat>0&&(long)f+t.variableRepeat<=Math.Min(t.end,state.duration))events.Add(Tuple.Create(f+t.variableRepeat,e.Item2));
            }
            var values=new Dictionary<string,double>();foreach(var pair in history){var list=pair.Value;int lo=0,hi=list.Count-1;while(lo<hi){int mid=(lo+hi+1)/2;if(list[mid].frame<=frame)lo=mid;else hi=mid-1;}values[pair.Key]=list[lo].value;}cachedFrame=frame;cached=values;return values;
        }
    }
}

internal sealed partial class MainForm
{
    private void AddGlobalVariable()
    {
        if(state==null)return;string name="phase";int n=2;while(state.tracks.Any(t=>t.kind=="variable"&&t.variableName==name))name="phase"+(n++);
        AddTrack("variable",t=>{t.name="全局变量 · "+name;t.variableName=name;t.end=state.duration;});
    }
    private void VariableFields(NativeProperties bag,Track t)
    {
        if(t.kind!="variable")return;
        Field(bag,"变量名","全局变量",()=>t.variableName,value=>RenameGlobal(t.variableName,GlobalVariables.Name(value)));
        Field(bag,"赋值表达式","全局变量",()=>t.variableExpression,value=>{
            var probe=state.Snapshot();probe.tracks.First(row=>row.id==t.id).variableExpression=value;Motion.Normalize(probe);GlobalVariables.At(probe,Math.Max(frame,t.start));t.variableExpression=value;
        },null,"支持 global.变量名、random、choose、floor、point_distance 等。每次触发计算一次，结果保持到下次赋值。");
        Field(bag,"赋值前默认值","全局变量",()=>t.variableDefault,value=>t.variableDefault=value);
        Field(bag,"重复赋值间隔 / 帧（0 仅一次）","全局变量",()=>t.variableRepeat,value=>t.variableRepeat=Math.Max(0,value));
        string[] formulas={"random(360)","random_range(1,5)","choose(0,90,180,270)","floor(random(10))","point_distance(0,0,player.x,player.y)"};
        bag.Add("公式示例","全局变量",typeof(string),()=>"选择公式模板…",value=>{if(!formulas.Contains((string)value))return;PushUndo();t.variableExpression=(string)value;MarkChanged();},new[]{"选择公式模板…"}.Concat(formulas).ToArray(),localizeChoices:false);
        bag.Add("引用方式","全局变量",typeof(string),()=>"global."+t.variableName,null);
        bag.Add("当前游标值","全局变量",typeof(double),()=>GlobalVariables.At(state,frame)[t.variableName],null);
        bag.Add("说明","全局变量",typeof(string),()=>"开始帧触发赋值；重复时在结束帧内循环。结束后保留结果。同帧按轨道顺序赋值。",null);
    }
    private void RenameGlobal(string oldName,string newName)
    {
        if(oldName==newName)return;if(GlobalVariables.Declarations(state.tracks).Any(t=>t.variableName==newName))throw new FormatException("已有同名变量。");foreach(var t in state.tracks)GlobalVariables.ReplaceReference(t,oldName,newName);
    }
}
