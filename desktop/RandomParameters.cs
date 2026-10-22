using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Collections.Concurrent;

internal sealed class ParameterActions
{
    public Action EditExpression;public Func<bool> Keyed,Random;public Action ToggleKey,ToggleRandom;public Func<string> Expression;public Action<string> SetExpression;
}
internal sealed class RandomExpression
{
    internal sealed class Node {public string op;public double number;public readonly List<Node> args=new List<Node>();}
    private static readonly ConcurrentDictionary<string,Node> cache=new ConcurrentDictionary<string,Node>();
    internal static readonly string[] Variables={"player.x","player.y","x","y","x1","y1","x2","y2","frame","time","age","direction","speed","fps","pi"};
    private static readonly Dictionary<string,int> Functions=new Dictionary<string,int>{{"random",1},{"random_range",2},{"choose",-1},{"irandom",1},{"irandom_range",2},{"point_direction",4},{"point_distance",4},{"lengthdir_x",2},{"lengthdir_y",2},{"sin",1},{"cos",1},{"abs",1},{"sqrt",1},{"floor",1},{"ceil",1},{"min",-1},{"max",-1},{"clamp",3},{"lerp",3},{"turn_towards",3}};
    private readonly string source;private int at;
    private RandomExpression(string text){source=(text??"").Replace('（','(').Replace('）',')').Replace('，',',');}
    private void Space(){while(at<source.Length&&Char.IsWhiteSpace(source[at]))at++;}
    private bool Take(char c){Space();if(at>=source.Length||source[at]!=c)return false;at++;return true;}
    private FormatException Error(){return new FormatException("参数表达式格式错误（位置 "+(at+1)+"）；支持 player.x/y、point_direction、point_distance、random、choose 和四则运算。");}
    private Node Binary(Func<Node> next,string operators)
    {var left=next();while(true){Space();if(at>=source.Length||operators.IndexOf(source[at])<0)return left;string op=source[at++].ToString();var node=new Node{op=op};node.args.Add(left);node.args.Add(next());left=node;}}
    private Node Sum(){return Binary(Product,"+-");}private Node Product(){return Binary(Atom,"*/%");}
    private Node Atom()
    {
        Space();if(Take('+'))return Atom();if(Take('-')){var negative=new Node{op="neg"};negative.args.Add(Atom());return negative;}
        if(Take('(')){var value=Sum();if(!Take(')'))throw Error();return value;}
        Space();int start=at;
        if(at<source.Length&&(Char.IsDigit(source[at])||source[at]=='.')){
            while(at<source.Length&&(Char.IsDigit(source[at])||source[at]=='.'))at++;
            if(at<source.Length&&(source[at]=='e'||source[at]=='E')){at++;if(at<source.Length&&(source[at]=='+'||source[at]=='-'))at++;while(at<source.Length&&Char.IsDigit(source[at]))at++;}
            double number;if(!Double.TryParse(source.Substring(start,at-start),NumberStyles.Float,CultureInfo.InvariantCulture,out number)||Double.IsInfinity(number))throw Error();return new Node{op="number",number=number};
        }
        while(at<source.Length&&(Char.IsLetterOrDigit(source[at])||source[at]=='_'||source[at]=='.'))at++;
        string name=source.Substring(start,at-start).ToLowerInvariant();
        if(Variables.Contains(name))return new Node{op=name};
        int arity;if(!Functions.TryGetValue(name,out arity)||!Take('('))throw Error();
        var call=new Node{op=name};if(!Take(')')){do{call.args.Add(Sum());}while(Take(','));if(!Take(')'))throw Error();}
        if(arity<0?call.args.Count<1:call.args.Count!=arity)throw Error();return call;
    }
    internal static Node Parse(string text){return cache.GetOrAdd(text??"",value=>{var parser=new RandomExpression(value);var result=parser.Sum();parser.Space();if(parser.at!=parser.source.Length)throw parser.Error();return result;});}
    internal static bool Dynamic(Node node){return Variables.Contains(node.op)&&node.op!="pi"||node.args.Any(Dynamic);}
    public static bool IsDynamic(string expression){return Dynamic(Parse(expression));}
    public static int Hash(string text){long value=0;foreach(char c in text??"")value=(value*31+c)%2147483647;return (int)value;}
    private sealed class Generator {public long state;public double Next(){state=state*16807%2147483647;return (state-1)/2147483646.0;}}
    private static double Run(Node node,Generator generator,ExpressionContext context)
    {double value=Calculate(node,generator,context);if(Double.IsNaN(value)||Double.IsInfinity(value))throw new FormatException("随机表达式的结果必须是有限数字，请检查除数和范围。");return value;}
    private static double Calculate(Node node,Generator generator,ExpressionContext context)
    {
        if(node.op=="number")return node.number;if(Variables.Contains(node.op))return context.Get(node.op);var a=node.args.Select(n=>Run(n,generator,context)).ToArray();double u;
        switch(node.op){case "point_direction":return ExpressionContext.Direction(a[0],a[1],a[2],a[3]);case "point_distance":return Math.Sqrt(Math.Pow(a[2]-a[0],2)+Math.Pow(a[3]-a[1],2));
        case "lengthdir_x":return a[0]*Math.Cos(a[1]*Math.PI/180);case "lengthdir_y":return -a[0]*Math.Sin(a[1]*Math.PI/180);
        case "sin":return Math.Sin(a[0]);case "cos":return Math.Cos(a[0]);case "abs":return Math.Abs(a[0]);case "sqrt":return Math.Sqrt(a[0]);case "floor":return Math.Floor(a[0]);case "ceil":return Math.Ceiling(a[0]);case "min":return a.Min();case "max":return a.Max();case "clamp":return Math.Max(a[1],Math.Min(a[2],a[0]));case "lerp":return a[0]+(a[1]-a[0])*a[2];case "turn_towards":return ExpressionContext.Turn(a[0],a[1],a[2]);case "neg":return -a[0];case "+":return a[0]+a[1];case "-":return a[0]-a[1];case "*":return a[0]*a[1];case "/":return a[0]/a[1];case "%":return a[0]%a[1];case "random":return generator.Next()*a[0];case "irandom":return Math.Floor(generator.Next()*(Math.Floor(Math.Abs(a[0]))+1));case "choose":return a[(int)Math.Floor(generator.Next()*a.Length)];default:u=generator.Next();double lo=Math.Min(a[0],a[1]),hi=Math.Max(a[0],a[1]);return node.op=="irandom_range"?Math.Ceiling(lo)+Math.Floor(u*(Math.Floor(hi)-Math.Ceiling(lo)+1)):lo+(hi-lo)*u;}
    }
    public static double Evaluate(string expression,int seed,string slot,ExpressionContext context=null)
    {context=context??new ExpressionContext();var generator=new Generator{state=(long)(1+((long)Math.Max(0,seed)+Hash(slot)+(IsDynamic(expression)?Math.Floor(context.frame)*104729:0))%2147483646)};double result=Run(Parse(expression),generator,context);if(Double.IsNaN(result)||Double.IsInfinity(result))throw new FormatException("随机表达式的结果必须是有限数字，请检查除数和范围。");return result;}
}
internal static class RandomParameters
{
    public static void Attach(EditorState state){foreach(var t in state.tracks){t.expressionSeed=state.randomSeed;t.expressionFps=Math.Max(1,state.fps);t.expressionPlayerX=state.nativeTest==null?400:state.nativeTest.x;t.expressionPlayerY=state.nativeTest==null?304:state.nativeTest.y;t.liveExpressions=(t.expressions!=null&&t.expressions.Values.Any(RandomExpression.IsDynamic))||t.keyframes.Any(k=>!String.IsNullOrWhiteSpace(k.expression)&&RandomExpression.IsDynamic(k.expression));}}
    public static double Clamp(string field,double value)
    {
        switch(field){case "homingDelay":return Math.Max(0,Math.Floor(value+.5));case "turnRate":return Math.Max(0,value);case "alpha":case "fxOpacity":case "fxRetention":case "trailStartAlpha":return Math.Max(0,Math.Min(1,value));case "count":return Math.Max(1,Math.Min(300,Math.Floor(value+.5)));case "sides":return Math.Max(3,Math.Min(64,Math.Floor(value+.5)));case "fxSamples":return Math.Max(2,Math.Min(16,Math.Floor(value+.5)));case "interval":case "life":case "trailInterval":case "trailLife":return Math.Max(1,Math.Floor(value+.5));case "cameraZoom":return Math.Max(.05,Math.Min(20,value));case "radius":case "length":case "strength":case "rainbowDensity":return Math.Max(0,value);case "easePower":return Math.Max(.1,value);case "easePeriod":case "fxFrequency":return Math.Max(.01,value);default:return value;}
    }
    public static void Resolve(EditorState state)
    {
        state.randomSeed=Math.Max(0,state.randomSeed);Attach(state);foreach(var t in state.tracks){if(t.expressions==null)t.expressions=new Dictionary<string,string>();if(String.IsNullOrEmpty(t.randomId))t.randomId=t.id;
            foreach(var pair in t.expressions){var field=typeof(Track).GetField(pair.Key);if(field==null||(field.FieldType!=typeof(double)&&field.FieldType!=typeof(int)))throw new FormatException("不支持随机的属性："+pair.Key);double value=Clamp(pair.Key,RandomExpression.Evaluate(pair.Value,state.randomSeed,t.randomId+"|"+pair.Key+"|base",ExpressionContext.For(t,t.start)));if(!RandomExpression.IsDynamic(pair.Value))Motion.SetBase(t,pair.Key,value);}
            foreach(var key in t.keyframes.Where(k=>!String.IsNullOrWhiteSpace(k.expression))){if(String.IsNullOrEmpty(key.randomId))key.randomId=key.property+":"+key.frame;key.value=Clamp(key.property,RandomExpression.Evaluate(key.expression,state.randomSeed,t.randomId+"|"+key.property+"|"+key.randomId,ExpressionContext.For(t,key.frame)));}
        }
    }
}
