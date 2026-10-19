#define Create_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
image_speed=0
#define Step_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
switch(form){
    case 1:{
        if !Vint{
            Vint=1
            switch(direction){
                case 0:
                    dirto=random_range(-15,15)+270
                break;
                case 90:
                    dirto=random_range(-30,30)+270
                break;
                case 180:
                    dirto=random_range(-15,15)+270
                break;
            }
        }
        xp=x;yp=y
        speed-=speed/20
        hsp+=lengthdir_x(0.2,dirto);vsp+=lengthdir_y(0.2,dirto)
        x+=hsp;y+=vsp
        image_angle=point_direction(xp,yp,x+hspeed,y+vspeed)
        inst=inst_sca_form(x,y,objBullet3,0.4,2)
        inst.image_blend=merge_color(image_blend,c_black,0.25)
        inst.image_angle=image_angle
        inst.mask_index=maskNothing
        inst.depth=1
        if y>658 instance_destroy()
        if on=1{
            dird=random_range(-30,30)
            dir=point_direction(0,0,hsp,vsp)
            len=point_distance(0,0,hsp,vsp)/5
            hsp=lengthdir_x(len,dir+dird)
            vsp=lengthdir_y(len,dir+dird)
            on=2
        }
        if on=2{
            sc2+=(1-sc2)/10
            xdel=random_range(-15,15)*(1-sc2)
            ydel=random_range(-15,15)*(1-sc2)
            x+=xdel-xnow
            y+=ydel-ynow
            xnow=xdel
            ynow=ydel
        }
    }break;
    case 2:{
        if !Vint{
            Vint=1
            image_speed=1
        }
        time+=1
        if time=12 instance_destroy()
    }break;
}

if FollowDirection image_angle=direction
