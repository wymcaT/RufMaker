using System;
internal static class AlphaExpressionTests
{
    static void Equal(double actual,double expected,string label){if(actual!=expected)throw new Exception(label+": "+actual+" != "+expected);}
    static double Value(string expression,ExpressionContext context=null){return RandomExpression.Evaluate(expression,12345,"object|x|base",context);}
    static void Reject(Action action,string label){try{action();}catch(FormatException){return;}throw new Exception("Accepted invalid "+label);}
    static void Main()
    {
        string[] expressions={"2+3*4>13","2+3*4<14","2+3*4<=14","14>=2+3*4","14==2+3*4","14!=2+3*4","(1<2)+2*(3>=4)","max(1>2,3<=3)","3>2>0","-2<-1"};
        double[] expected={1,0,1,1,1,0,1,1,1,1};for(int i=0;i<expressions.Length;i++)Equal(Value(expressions[i]),expected[i],expressions[i]);
        foreach(string expression in new[]{"1=1","1===1","1!==1","1<>2","1&&2","!1","1<","<1","1< =2","1;2","alpha=0"})Reject(()=>Value(expression),expression);
        Reject(()=>Value("alpha"),"unbound alpha");Reject(()=>Value("alpha",new ExpressionContext{alpha=Double.NaN}),"NaN alpha");Reject(()=>Value("alpha",new ExpressionContext{alpha=Double.PositiveInfinity}),"infinite alpha");
        Equal(Value("alpha<=0",new ExpressionContext{alpha=0}),1,"zero alpha");Equal(Value("alpha<=0",new ExpressionContext{alpha=.5}),0,"visible alpha");Equal(Value("alpha+.1",new ExpressionContext{alpha=.5}),.6,"alpha arithmetic");
        if(!RandomExpression.UsesAlpha("max(alpha,0)")||RandomExpression.UsesAlpha("global.alpha")||!RandomExpression.IsDynamic("alpha"))throw new Exception("Alpha AST detection");
        Equal(Value("random(100)"),2.424714716546903,"legacy random");Equal(Value("choose(random(100),random_range(200,300),-2)"),2.424714716546903,"legacy choose");Equal(Value("random(10)+random(20)*random(30)"),19.482822761351525,"legacy draw order");
        Equal(Value("(random(10)<0)+random(20)*random(30)"),19.240351289696835,"comparison draw order");
        foreach(bool raw in new[]{false,true})
        {
            double expectedAlpha=raw?1.04:1;
            var initial=new Track{id="initial",kind="emitter",start=0,end=100,rawAlpha=raw,alpha=1.04};
            var initialState=new EditorState();initialState.tracks.Add(initial);Motion.Normalize(initialState);Equal(Motion.Value(initial,"alpha",0),1.04,"literal initial alpha preserved");
            foreach(string expression in new[]{"1.04","random_range(1.04,1.04)","1.04+frame*.01"})
            {
                var t=new Track{id="raw",kind="emitter",start=0,end=100,rawAlpha=raw};t.expressions["alpha"]=expression;
                var state=new EditorState();state.tracks.Add(t);Motion.Normalize(state);
                Equal(Motion.Value(t,"alpha",0),expectedAlpha,"base expression alpha");Equal(Motion.Value(t,"alpha",2),raw&&expression.Contains("frame")?1.06:expectedAlpha,"dynamic alpha");
                var context=ExpressionContext.For(t,2);context.sampleRandom=true;Equal(Motion.SampleValue(t,"alpha",2,context),raw&&expression.Contains("frame")?1.06:expectedAlpha,"birth alpha");
                t.expressions.Clear();t.keyframes.Add(new Keyframe{property="alpha",frame=0,expression=expression,randomId="alpha0"});Motion.Normalize(state);
                Equal(t.keyframes[0].value,expectedAlpha,"resolved alpha key");Equal(Motion.Value(t,"alpha",2),raw&&expression.Contains("frame")?1.06:expectedAlpha,"dynamic alpha key");
            }
            Equal(RandomParameters.Clamp("alpha",-.04,raw),raw?-.04:0,"negative raw alpha");Equal(RandomParameters.Clamp("fxOpacity",1.04,raw),1,"other opacity remains clamped");
        }
        Console.WriteLine("PASS: comparisons, precedence, alpha scope, and unchanged random streams");
    }
}
