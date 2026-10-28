using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

// All numerical controls use the same expression editor; strings stay in the model.
internal static class ScalarExpressions
{
    public static double ActionValue(Track source,BulletAction action,string property,ExpressionContext context)
    {string text;double value=Convert.ToDouble(typeof(BulletAction).GetField(property).GetValue(action));return action.expressions!=null&&action.expressions.TryGetValue(property,out text)&&!String.IsNullOrWhiteSpace(text)?RandomExpression.Evaluate(text,source.expressionSeed,(source.randomId??source.id)+"|actionField"+source.spawnActions.Concat(source.bulletActions).ToList().IndexOf(action)+":"+property+"|base",context):value;}
    public static void ScaleBinding(Dictionary<string,string> values,string field,double factor){if(factor==1||values==null||!values.ContainsKey(field))return;values[field]="("+values[field]+")*"+factor.ToString("R",CultureInfo.InvariantCulture);}
    public static void ScaleAction(BulletAction a,double ratio){if(new[]{"speed","turn","home","acceleration"}.Contains(a.type)){double factor=1/(a.type=="acceleration"?ratio*ratio:ratio);ScaleBinding(a.expressions,"from",factor);ScaleBinding(a.expressions,"to",factor);}ScaleBinding(a.expressions,"childSpeed",1/ratio);}
    private static ParameterActions Controls(Dictionary<string,string> values,string property,Func<double> get,Action<double> set,Func<string,double> evaluate,Action<Action> edit)
    {
        Func<string> expression=()=>values.ContainsKey(property)?values[property]:"";Action<string> assign=text=>{evaluate(text);edit(()=>values[property]=text);};
        return new ParameterActions{Property=property,Expression=expression,Random=()=>!String.IsNullOrWhiteSpace(expression()),SetExpression=assign,Keyed=()=>false,ToggleRandom=()=>{if(String.IsNullOrWhiteSpace(expression()))assign("random_range("+get().ToString("R",CultureInfo.InvariantCulture)+", "+get().ToString("R",CultureInfo.InvariantCulture)+")");else {double frozen=evaluate(expression());edit(()=>{values.Remove(property);set(frozen);});}},EditExpression=()=>{using(var dialog=new ExpressionEditor(property,String.IsNullOrWhiteSpace(expression())?get().ToString(CultureInfo.InvariantCulture):expression(),0,0,0,0,false,(text,at)=>evaluate(text)))if(Language.ShowDialog(dialog,null)==System.Windows.Forms.DialogResult.OK)assign(dialog.Expression);}};
    }
    public static void TrackField(NativeProperties bag,EditorState state,Track t,string property,string name,string category,Action<Action> edit)
    {
        var f=typeof(Track).GetField(property);t.expressions=t.expressions??new Dictionary<string,string>();Func<double> get=()=>Motion.Value(t,property,t.start);Action<double> set=v=>{t.expressions.Remove(property);Motion.SetBase(t,property,RandomParameters.Clamp(property,v));};
        var controls=Controls(t.expressions,property,get,set,text=>RandomExpression.Evaluate(text,state.randomSeed,(t.randomId??t.id)+"|"+property+"|base",ExpressionContext.For(t,t.start)),edit);
        bag.Add(name,category,f.FieldType,()=>Convert.ChangeType(get(),f.FieldType),v=>edit(()=>set(Convert.ToDouble(v))),null,"支持数值、random_range、random、choose 及表达式。",controls);
    }
    public static void ActionField(NativeProperties bag,EditorState state,Track t,BulletAction a,string property,string name,string category,Action<Action> edit)
    {
        var f=typeof(BulletAction).GetField(property);a.expressions=a.expressions??new Dictionary<string,string>();Func<double> get=()=>Convert.ToDouble(f.GetValue(a));Action<double> set=v=>{a.expressions.Remove(property);f.SetValue(a,f.FieldType==typeof(int)?(object)BulletSystem.Round(v):v);};var controls=Controls(a.expressions,property,get,set,text=>RandomExpression.Evaluate(text,state.randomSeed,(t.randomId??t.id)+"|actionField:"+property+"|base",ExpressionContext.For(t,t.start)),edit);
        controls.Property=property=="from"||property=="to"?a.type:property=="childSpeed"?"speed":property;
        bag.Add(name,category,f.FieldType,()=>Convert.ChangeType(ScalarExpressions.ActionValue(t,a,property,ExpressionContext.For(t,t.start)),f.FieldType),v=>edit(()=>set(Convert.ToDouble(v))),null,"支持数值、random_range、random、choose 及表达式。",controls);
    }
}
