using System;

// Preview samples the player at song time (recorded trace or the edited test position).
// Exported GML reads the live player instance.
internal sealed class ExpressionContext
{
    public System.Collections.Generic.IDictionary<string,double> globals;
    public double playerX=400,playerY=304,x,y,x1,y1,x2,y2,sourceX,sourceY,frame,age,direction,speed,fps=50,i,n=1,volley;
    public bool sampleRandom,randomMix;public double randomFrame=Double.NaN,randomOffset;
    public ExpressionContext Clone(){return (ExpressionContext)MemberwiseClone();}
    public static NativeTestPosition PlayerAt(Track t,double frame,bool worldTime=false)
    {return t.owner==null?new NativeTestPosition{x=t.expressionPlayerX,y=t.expressionPlayerY}:BulletSystem.PlayerAt(t.owner,(int)(frame+(worldTime?0:t.expressionWorldShift)));}
    public static ExpressionContext For(Track t,double frame,bool worldTime=false)
    {var player=PlayerAt(t,frame,worldTime);return new ExpressionContext{globals=GlobalVariables.At(t.owner,frame+(worldTime?0:t.expressionWorldShift)),playerX=player.x,playerY=player.y,x=t.x,y=t.y,x1=t.x1,y1=t.y1,x2=t.x2,y2=t.y2,sourceX=t.x,sourceY=t.y,frame=frame,age=Math.Max(0,frame-TimelineLayout.ClockStart(t)),direction=t.angle,speed=t.speed,fps=t.expressionFps};}
    public double Get(string name)
    {if(name.StartsWith("global.",StringComparison.Ordinal)){double value;if(globals!=null&&globals.TryGetValue(name.Substring(7),out value))return value;throw new FormatException("未定义全局变量："+name);}switch(name){case "source.x":return sourceX;case "source.y":return sourceY;case "i":return i;case "n":return n;case "u":return n<=1?0:i/(n-1);case "volley":return volley;case "player.x":return playerX;case "player.y":return playerY;case "x":return x;case "y":return y;case "x1":return x1;case "y1":return y1;case "x2":return x2;case "y2":return y2;case "frame":return frame;case "time":return frame/Math.Max(1,fps);case "age":return age;case "direction":return direction;case "speed":return speed;case "fps":return fps;case "pi":return Math.PI;default:throw new FormatException("未知变量："+name);}}
    public static double Direction(double x1,double y1,double x2,double y2)
    {if(x1==x2&&y1==y2)return 0;return (Math.Atan2(y1-y2,x2-x1)*180/Math.PI+360)%360;}
    public static double Turn(double current,double target,double limit)
    {double delta=((target-current)%360+540)%360-180;return current+Math.Max(-Math.Abs(limit),Math.Min(Math.Abs(limit),delta));}
}
