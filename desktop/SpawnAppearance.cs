using System;
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;

internal sealed class BirthAppearance
{
    public string sprite;public int image;public Color color;public double scale=1;
    public BirthAppearance Clone(){return (BirthAppearance)MemberwiseClone();}
    public static BirthAppearance TrackAt(Track t,int frame,int index,PointF point,Keyframe pose,double rotation=0,int count=1)
    {return new BirthAppearance{sprite=t.sprite,color=t.rainbow?Color.White:Motion.ColorOf(t.color),image=Rainbow.Index(t,t.kind=="rod"?Rainbow.RodSequence(t,index,count,frame,point,pose,rotation):0,frame),scale=pose.scale};}
    public static BirthAppearance BulletAt(SimBullet b,int frame)
    {return new BirthAppearance{sprite=b.sprite??b.source.sprite,color=b.color,image=b.frozenImage>=0?b.frozenImage:Rainbow.Index(b.source,b.sequence,frame-b.clockShift,b.birth-b.clockShift),scale=b.scale*BulletLifetime.Scale(b)};}
}
internal static class SpawnColors
{
    private static readonly Regex hex=new Regex("[\"']?(#[0-9a-fA-F]{6})[\"']?");
    public static bool Packed(string expression){return hex.IsMatch(expression??"");}
    public static string Numeric(string expression)
    {return hex.Replace(expression??"",m=>{string s=m.Groups[1].Value;return (Int32.Parse(s.Substring(1,2),NumberStyles.HexNumber)+Int32.Parse(s.Substring(3,2),NumberStyles.HexNumber)*256+Int32.Parse(s.Substring(5,2),NumberStyles.HexNumber)*65536).ToString(CultureInfo.InvariantCulture);});}
    public static Color Hue(double value)
    {
        double h=((value%360+360)%360)/60,x=1-Math.Abs(h%2-1);double r=0,g=0,b=0;
        if(h<1){r=1;g=x;}else if(h<2){r=x;g=1;}else if(h<3){g=1;b=x;}else if(h<4){g=x;b=1;}else if(h<5){r=x;b=1;}else{r=1;b=x;}
        return Color.FromArgb((int)Math.Floor(r*255+.5),(int)Math.Floor(g*255+.5),(int)Math.Floor(b*255+.5));
    }
    public static Color Evaluate(string expression,Track t,string slot,int frame,int wave,int index=0,bool perBullet=false)
    {
        var c=ExpressionContext.For(t,frame);c.randomFrame=0;c.randomOffset=wave*1009+(perBullet?index*9176:0);c.i=index;c.volley=wave;
        double value=RandomExpression.Evaluate(Numeric(expression),t.expressionSeed,(t.randomId??t.id)+"|"+slot+"|base",c);
        if(!Packed(expression))return Hue(value);int n=(int)Math.Max(0,Math.Min(16777215,Math.Floor(value+.5)));return Color.FromArgb(n&255,(n>>8)&255,(n>>16)&255);
    }
}
internal sealed partial class MainForm
{
    private void ColorFields(NativeProperties bag,Track t)
    {
        if(t.kind!="emitter")return;
        Field(bag,"波次随机颜色","发射颜色",()=>t.colorMode=="random",v=>{t.colorMode=v?"random":"fixed";QueueInspectorUpdate();});
        if(t.colorMode!="random")return;
        Field(bag,"颜色公式","发射颜色",()=>t.colorExpression,v=>{SpawnColors.Evaluate(v,t,"color",frame,0);t.colorExpression=v;},null,"random(360) 随机色相；choose(0,120,240) 选择色相；choose(\"#FF75AD\",\"#6DE8FF\") 选择指定颜色。出生时取样，之后固定。请使用白色底图；彩虹贴图会保留自身颜色。");
        Field(bag,"每颗独立配色","发射颜色",()=>t.colorScope=="bullet",v=>t.colorScope=v?"bullet":"wave",null,"关闭时同一波弹幕使用相同颜色；开启时逐颗取样。支持固定种子和重新随机。");
    }
}
