///n_star(n,number,speed,object,x,y,dir)
//锟节讹拷锟街凤拷锟斤拷锟斤拷锟轿碉拷幕锟侥脚憋拷
//n: 锟斤拷锟轿的讹拷锟斤拷锟斤拷锟斤拷锟斤拷锟斤拷小为3锟斤拷3锟斤拷锟斤拷锟斤拷锟轿ｏ拷4锟斤拷十锟街ｏ拷5锟斤拷锟斤拷锟斤拷牵锟?锟斤拷锟斤拷芒锟角ｏ拷8锟角八斤拷锟角ｏ拷
//number: 锟斤拷锟斤拷每锟斤拷锟竭碉拷幕锟斤拷锟斤拷
//speed: 锟斤拷幕锟劫讹拷
//object: 锟斤拷锟斤拷锟斤拷幕锟斤拷obj
//x: 锟斤拷锟斤拷锟絰锟斤拷锟斤拷
//y: 锟斤拷锟斤拷锟結锟斤拷锟斤拷
//dir: 锟斤拷锟轿的凤拷锟斤拷嵌锟?
var dirm, obj, i, k;
dirm = argument6;
obj = argument3;
for (k = 0; k < argument0; k += 1)
{
    COS[k] = cos(degtorad(k*360/argument0));
    SIN[k] = sin(degtorad(k*360/argument0));
    COS[k+2] = cos(degtorad((k+2)*360/argument0));
    SIN[k+2] = sin(degtorad((k+2)*360/argument0));
    for (i = 0; i < argument1; i += 1)
    {
        idx[k,i] = instance_create(argument4,argument5,obj);
        idx[k,i].direction = dirm + point_direction(argument4,argument5,argument4+COS[k]+(COS[k+2]-COS[k])*i/argument1,argument5-SIN[k]-(SIN[k+2]-SIN[k])*i/argument1);
        idx[k,i].speed = argument2*point_distance(argument4,argument5,argument4+COS[k]+(COS[k+2]-COS[k])*i/argument1,argument5-SIN[k]-(SIN[k+2]-SIN[k])*i/argument1);
        idx[k,i].i=k*26+i
    }
}
