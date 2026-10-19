#define Step_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
switch(form){
    case 1:{
        FollowDirection=1
        image_xscale-=0.05;image_yscale-=0.05
        if image_yscale<=0 instance_destroy()
    }break;
    case 2:{
        image_xscale+=0.1
        image_yscale+=0.1
        image_alpha-=0.1
        if image_alpha<=0 instance_destroy()
    }break;
    case 3:{
        repeat(ceil(vsp/3+0.01)){
            with(inst_xyscaang_form(x,y,objBullet2,0.5-0.1/ceil(vsp/3+0.01),0.5,0,4))mask_index=maskNothing
            vsp+=0.2
            y+=vsp
            if y>628{
                instance_destroy()
                break;
            }
        }
    }break;
    case 4:{
        image_xscale-=0.1
        if image_xscale<=0 instance_destroy()
    }break;
    case 5:{
        if Vint=0{Vint=1;direction=random(60)+60;speed=6}
        speed+=(1-speed)/10
        image_xscale-=(1.51-image_xscale)/3
        if image_xscale<0.5 image_xscale=0.5
        image_yscale=image_xscale
        if Time mod 25<5 with(inst_sca_form(x,y,objBullet2,0.5,6))mask_index=maskNothing
        Time+=1
        if x<-20 or x>820 or y<-20 instance_destroy()
    }break;
    case 6:{
        image_xscale+=(-0.02-image_xscale)/15
        image_yscale+=(-0.02-image_yscale)/15
        if image_xscale<=0 instance_destroy()
    }break;
    case 7:{
        FollowDirection=1
        image_xscale+=(1-image_xscale)/10
        image_yscale+=(0.2-image_yscale)/10
        speed+=(4-speed)/10
        with(inst_sca_form(x,y,objBullet2,image_yscale,6))mask_index=maskNothing
        if x<-20 or x>820 or y<-20 instance_destroy()
    }break;
    case 8:{
        image_xscale-=0.06
        image_yscale-=0.06
        if image_xscale<=4/30 instance_destroy()
        if x<-10 or x>810 or y<-10 or y>618 instance_destroy()
    }break;
    case 9:{
        if x<-10 or x>810 or y<-10 or y>618 instance_destroy()
    }break
    case 10:{
        if !Vint{
            Vint=1
            xto1=xstart+random_range(-50,50)
            xto2=xstart+random_range(-80,80)
            xto3=xstart+random_range(-50,50)
            yto=random_range(50,150)
        }
        if T<30{
            T+=T<30
            x=Ease_bezier4Point(T/50,xstart,xto1,xto2,xto3)
            y=Merge_Number(-20,yto,T/30)
            if T=29{
                xp=x
                yp=y
            }
            if T=30{
                direction=point_direction(xp,yp,x,y)
                speed=point_distance(xp,yp,x,y)
            }
            image_xscale=Merge_Number(1,0.6,Ease_sinwave_in(T/30))
            image_yscale=Merge_Number(1,0.6,Ease_sinwave_in(T/30))
            inst=inst_form(x,y,objBullet2,11)
            inst.scD=T/100
            inst.image_xscale=image_xscale
            inst.image_yscale=image_yscale
        }
    }break
    case 11:{
        image_xscale-=scD
        image_yscale-=scD
        if image_xscale<=4/30 instance_destroy()
    }break;
}

if FollowDirection image_angle=direction
