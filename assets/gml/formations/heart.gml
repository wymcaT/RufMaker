///heart(number,speed,object,x,y,dir)
//锟斤拷锟斤拷锟斤拷锟轿碉拷幕锟侥脚憋拷
//number: 锟斤拷锟戒弹幕时锟斤拷幕锟斤拷锟斤拷锟斤拷
//speed: 锟斤拷幕锟斤拷锟劫讹拷
//object: 锟斤拷锟斤拷锟斤拷锟戒弹幕锟斤拷obj
//x: 锟斤拷锟斤拷锟絏锟斤拷锟斤拷
//y: 锟斤拷锟斤拷锟結锟斤拷锟斤拷
//dir: 锟斤拷锟轿的凤拷锟斤拷嵌锟?
var i, dirm;
dirm = argument5;
for (i = 1; i <= argument0; i += 1)
{
    idx[i] = instance_create(argument3,argument4,argument2);
    idx[i].speed = 1/3*argument1*point_distance(argument3,argument4,argument3+16*sin(degtorad(360/argument0*i))*sin(degtorad(360/argument0*i))*sin(degtorad(360/argument0*i)),argument4-13*cos(degtorad(360/argument0*i))+5*cos(degtorad(360/argument0*i*2))+2*cos(degtorad(360/argument0*i*3))+cos(degtorad(360/argument0*i*4)))/point_distance(argument3,argument4,argument3,argument4-5);
    idx[i].direction = dirm + point_direction(argument3,argument4,argument3,argument4)+90+point_direction(argument3,argument4,argument3+16*sin(degtorad(360/argument0*i))*sin(degtorad(360/argument0*i))*sin(degtorad(360/argument0*i)),argument4-13*cos(degtorad(360/argument0*i))+5*cos(degtorad(360/argument0*i*2))+2*cos(degtorad(360/argument0*i*3))+cos(degtorad(360/argument0*i*4)));
    idx[i].i=i
}
