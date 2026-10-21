using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

internal sealed class ParameterActions
{
    public Func<bool> Keyed,Random;public Action ToggleKey,ToggleRandom;public Func<string> Expression;public Action<string> SetExpression;
}
internal sealed class RandomExpression
{
    internal sealed class Node {public string op;public double number;public readonly List<Node> args=new List<Node>();}
    private readonly string source;private int at;
    private RandomExpression(string text){source=(text??"").Replace('（','(').Replace('）',')').Replace('，',',');}
    private void Space(){while(at<source.Length&&Char.IsWhiteSpace(source[at]))at++;}
    private bool Take(char c){Space();if(at>=source.Length||source[at]!=c)return false;at++;return true;}
    private FormatException Error(){return new FormatException("随机表达式格式错误（位置 "+(at+1)+"）；支持 random、random_range、choose 和四则运算。");}
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
        while(at<source.Length&&(Char.IsLetter(source[at])||source[at]=='_'))at++;
        string name=source.Substring(start,at-start).ToLowerInvariant();if(!new[]{"random","random_range","choose","irandom","irandom_range"}.Contains(name)||!Take('('))throw Error();
        var call=new Node{op=name};if(!Take(')')){do{call.args.Add(Sum());}while(Take(','));if(!Take(')'))throw Error();}
        if((name=="random"||name=="irandom")&&call.args.Count!=1||(name=="random_range"||name=="irandom_range")&&call.args.Count!=2||name=="choose"&&call.args.Count<1)throw Error();return call;
    }
    internal static Node Parse(string text){var parser=new RandomExpression(text);var result=parser.Sum();parser.Space();if(parser.at!=parser.source.Length)throw parser.Error();return result;}
    public static int Hash(string text){long value=0;foreach(char c in text??"")value=(value*31+c)%2147483647;return (int)value;}
    private sealed class Generator {public long state;public double Next(){state=state*16807%2147483647;return (state-1)/2147483646.0;}}
    private static double Run(Node node,Generator generator)
    {double value=Calculate(node,generator);if(Double.IsNaN(value)||Double.IsInfinity(value))throw new FormatException("随机表达式的结果必须是有限数字，请检查除数和范围。");return value;}
    private static double Calculate(Node node,Generator generator)
    {
        if(node.op=="number")return node.number;var a=node.args.Select(n=>Run(n,generator)).ToArray();double u;
        switch(node.op){case "neg":return -a[0];case "+":return a[0]+a[1];case "-":return a[0]-a[1];case "*":return a[0]*a[1];case "/":return a[0]/a[1];case "%":return a[0]%a[1];case "random":return generator.Next()*a[0];case "irandom":return Math.Floor(generator.Next()*(Math.Floor(Math.Abs(a[0]))+1));case "choose":return a[(int)Math.Floor(generator.Next()*a.Length)];default:u=generator.Next();double lo=Math.Min(a[0],a[1]),hi=Math.Max(a[0],a[1]);return node.op=="irandom_range"?Math.Ceiling(lo)+Math.Floor(u*(Math.Floor(hi)-Math.Ceiling(lo)+1)):lo+(hi-lo)*u;}
    }
    public static double Evaluate(string expression,int seed,string slot)
    {var generator=new Generator{state=1+((long)Math.Max(0,seed)+Hash(slot))%2147483646};double result=Run(Parse(expression),generator);if(Double.IsNaN(result)||Double.IsInfinity(result))throw new FormatException("随机表达式的结果必须是有限数字，请检查除数和范围。");return result;}
}
internal static class RandomParameters
{
    public static double Clamp(string field,double value)
    {
        switch(field){case "alpha":case "fxOpacity":case "fxRetention":case "trailStartAlpha":return Math.Max(0,Math.Min(1,value));case "count":return Math.Max(1,Math.Min(300,Math.Floor(value+.5)));case "sides":return Math.Max(3,Math.Min(64,Math.Floor(value+.5)));case "fxSamples":return Math.Max(2,Math.Min(16,Math.Floor(value+.5)));case "interval":case "life":case "trailInterval":case "trailLife":return Math.Max(1,Math.Floor(value+.5));case "cameraZoom":return Math.Max(.05,Math.Min(20,value));case "radius":case "length":case "strength":case "rainbowDensity":return Math.Max(0,value);case "easePower":return Math.Max(.1,value);case "easePeriod":case "fxFrequency":return Math.Max(.01,value);default:return value;}
    }
    public static void Resolve(EditorState state)
    {
        state.randomSeed=Math.Max(0,state.randomSeed);foreach(var t in state.tracks){if(t.expressions==null)t.expressions=new Dictionary<string,string>();if(String.IsNullOrEmpty(t.randomId))t.randomId=t.id;
            foreach(var pair in t.expressions){var field=typeof(Track).GetField(pair.Key);if(field==null||(field.FieldType!=typeof(double)&&field.FieldType!=typeof(int)))throw new FormatException("不支持随机的属性："+pair.Key);Motion.SetBase(t,pair.Key,Clamp(pair.Key,RandomExpression.Evaluate(pair.Value,state.randomSeed,t.randomId+"|"+pair.Key+"|base")));}
            foreach(var key in t.keyframes.Where(k=>!String.IsNullOrWhiteSpace(k.expression))){if(String.IsNullOrEmpty(key.randomId))key.randomId=key.property+":"+key.frame;key.value=Clamp(key.property,RandomExpression.Evaluate(key.expression,state.randomSeed,t.randomId+"|"+key.property+"|"+key.randomId));}
        }
    }
}
