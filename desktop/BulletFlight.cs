using System;
using System.Collections.Generic;

internal sealed class FlightPoint {public double x,y,angle;}
internal static class BulletFlight
{
    public static bool Curved(Track t){return t.flightMode!="straight";}
    public static FlightPoint[] Path(Track t,int birth,int age,double x,double y,double angle,double speed,int index)
    {
        double turn=Motion.Value(t,"turnRate",birth),delay=Motion.Value(t,"homingDelay",birth);var points=new FlightPoint[age+1];points[0]=new FlightPoint{x=x,y=y,angle=angle};
        for(int at=1;at<=age;at++){
            if(at>delay){double target=ExpressionContext.Direction(x,y,t.expressionPlayerX,t.expressionPlayerY);
                if(t.flightMode=="aim")angle=target;
                else if(t.flightMode=="turn")angle=ExpressionContext.Turn(angle,target,turn);
                else if(t.flightMode=="expression"){var context=ExpressionContext.For(t,birth+at);context.x=x;context.y=y;context.age=at;context.direction=angle;context.speed=speed;angle=RandomExpression.Evaluate("("+t.flightExpression+")+0*frame",t.expressionSeed,t.randomId+"|flight|base",context);}}
            x+=Math.Cos(angle*Math.PI/180)*speed;y-=Math.Sin(angle*Math.PI/180)*speed;points[at]=new FlightPoint{x=x,y=y,angle=angle};
        }return points;
    }
}
internal sealed partial class MainForm
{
    private void FlightFields(NativeProperties bag,Track track)
    {
        string[] names={"直线（发射后方向不变）","每帧朝向玩家","限制转弯速度的跟踪弹","逐帧方向表达式"},values={"straight","aim","turn","expression"};
        Field(bag,"飞行方式","弹幕飞行",()=>names[Math.Max(0,Array.IndexOf(values,track.flightMode))],value=>{track.flightMode=values[Array.IndexOf(names,value)];if(IsHandleCreated)BeginInvoke(new Action(ShowProperties));},names,"发射角度只影响新弹幕；这里控制已经发射的每颗弹幕。预览追踪参考玩家，游戏中追踪真实玩家。");
        if(track.flightMode=="straight")return;
        Field(bag,"开始跟踪前等待 / 帧","弹幕飞行",()=>track.homingDelay,value=>track.homingDelay=Math.Max(0,value));
        if(track.flightMode=="turn")Field(bag,"最大转弯 / 度每帧","弹幕飞行",()=>track.turnRate,value=>track.turnRate=Math.Max(0,value));
        if(track.flightMode=="expression")Field(bag,"逐帧方向公式","弹幕飞行",()=>track.flightExpression,value=>{RandomExpression.Evaluate(value,state.randomSeed,track.id,ExpressionContext.For(track,frame));track.flightExpression=value;},null,"每帧执行，x/y 为这颗弹幕的位置，direction 为上一帧方向。示例：turn_towards(direction,point_direction(x,y,player.x,player.y),3)");
    }
}
