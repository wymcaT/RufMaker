#define Step_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
switch(form){
    case 1:{
        if Vint=0{Vint=1;spe=30;FollowDirection=1}
        for(i=0;i<3;i+=1){
            if spe>1{
                with(inst_xyscaang_form(x,y,objBullet2,1-i*0.05/3,0.4-i*0.05/3,direction,1)){mask_index=maskNothing}}
            x+=lengthdir_x(spe,direction);y+=lengthdir_y(spe,direction)
            spe-=spe/10}
        if x<-20 or x>820 or y<-20 or y>628 instance_destroy()
    }break;
    case 2:{
        if on=0{
            if Vint=0{Vint=1;spe=15;FollowDirection=1}
            for(i=0;i<3;i+=1){
                if spe>1{with(inst_xyscaang_form(x,y,objBullet2,1-i*0.05/3,0.4-i*0.05/3,direction,1)){mask_index=maskNothing}}
                x+=lengthdir_x(spe,direction);y+=lengthdir_y(spe,direction);len+=spe;spe-=spe/10
            }
        }
        else{
            if Vint=1{Vint=2;dir=direction}
            spe2+=0.4;lenn+=spe2;sc+=(0.003+sc)*(1-sc)/3
            if spe2>0.6 with(inst_xyscaang_form(x,y,objBullet2,other.spe2/8,0.4,direction,1)){mask_index=maskNothing}
            xp=x;yp=y;x=400+lengthdir_x(len+lenn+sc*100,dir+sc*lr*15);y=304+lengthdir_y(len+lenn+sc*100,dir+sc*lr*15)
            direction+=Directionapproach(direction,point_direction(xp,yp,x,y),3)
        }
        if x<-20 or x>820 or y<-20 or y>628 instance_destroy()
    }break;
    case 3:{
        if Vint=0{Vint=1;spe=20;FollowDirection=1}
        for(i=0;i<3;i+=1){
            if spe>1{
                with(inst_xyscaang_form(x,y,objBullet2,1-i*0.05/3,0.4-i*0.05/3,direction,1)){mask_index=maskNothing}}
            x+=lengthdir_x(spe,direction);y+=lengthdir_y(spe,direction)
            spe+=(0.2-spe)/10}
        if x<-20 or x>820 or y<-20 or y>628 instance_destroy()
    }break;
    case 4:{
        if Vint=0{Vint=1;spe=40;FollowDirection=1}
        for(i=0;i<3;i+=1){
            if spe>1{
                with(inst_xyscaang_form(x,y,objBullet2,1-i*0.05/3,0.4-i*0.05/3,direction,1)){mask_index=maskNothing}}
            x+=lengthdir_x(spe,direction);y+=lengthdir_y(spe,direction)
            spe+=(0.2-spe)/10}
        if x<-20 or x>820 or y<-20 or y>628 instance_destroy()
    }break;
    case 5:{
        if on=0{
            if Vint=0{Vint=1;spe=30;dird=6;FollowDirection=1}
            spe+=(4-spe)/10;dird-=dird/20
            direction+=lr*dird
            if spe>1 with(inst_xyscaang_form(x,y,objBullet2,1,0.4,direction,1)){mask_index=maskNothing}
            x+=lengthdir_x(spe,direction);y+=lengthdir_y(spe,direction)
        }
        else{
            Time+=(Time<100)*2
            xp=(Time-50)*8/50
            C=3
            spe+=0.25
            image_xscale=1+8*(1/(1+exp(-xp-C))-1/(1+exp(-xp+C)))
            len=30*16/25*(exp(xp-3)/power(1+exp(xp-3),2)+exp(xp+3)/power(1+exp(xp+3),2))
            x+=lengthdir_x(len+spe,direction);y+=lengthdir_y(len+spe,direction)
        }
    }break;
    case 6:{
        if Vint=0{Vint=1;spe=40;FollowDirection=1}
        for(i=0;i<3;i+=1){
            if spe>1{
                with(inst_xyscaang_form(x,y,objBullet2,1-i*0.05/3,0.4-i*0.05/3,direction,1)){mask_index=maskNothing}}
            x+=lengthdir_x(spe,direction);y+=lengthdir_y(spe,direction)
            spe+=(0.5-spe)/10}
        Time+=1
        if Time>denum*2+20{
            image_yscale-=0.1
            if image_yscale<=0 instance_destroy()
        }
        if x<-20 or x>820 or y<-20 or y>628 instance_destroy()
    }break;
    case 7:{
        if Vint=0{Vint=1;spe=speed;speed=0;FollowDirection=1}
        for(i=0;i<3;i+=1){
            if spe>1{
                with(inst_xyscaang_form(x,y,objBullet2,1-i*0.05/3,0.4-i*0.05/3,direction,1)){mask_index=maskNothing}}
            x+=lengthdir_x(spe,direction);y+=lengthdir_y(spe,direction)
            spe-=spe/35}
        if x<-20 or x>820 or y<-20 or y>628 instance_destroy()
    }break;
    case 8:{
        if Vint=0{
            spe1=20
            spe2=5
            Vint=1
        }
        spe1-=spe1/20
        spe2-=spe2/10
        xp=x
        yp=y
        if point_distance(0,0,spe1,spe2)>1{
            with(inst_xyscaang_form(x,y,objBullet2,1,0.4,image_angle,1)){mask_index=maskNothing}}
        x+=lengthdir_x(spe1,direction)+lengthdir_x(spe2,direction+90*lr)
        y+=lengthdir_y(spe1,direction)+lengthdir_y(spe2,direction+90*lr)
        image_angle=point_direction(xp,yp,x,y)
    }break;
    case 9:{
        if Vint=0{Vint=1;spe=25;FollowDirection=1}
        for(i=0;i<3;i+=1){
            if spe>1{
                with(inst_xyscaang_form(x,y,objBullet2,1-i*0.05/3,0.4-i*0.05/3,direction,1)){mask_index=maskNothing}}
            x+=lengthdir_x(spe,direction);y+=lengthdir_y(spe,direction)
            spe+=(0.2-spe)/10}
        if x<-20 or x>820 or y<-20 or y>628 instance_destroy()
    }break;
    case 10:{
        if Vint=0{Vint=1;spe=speed;speto=speed/20;speed=0;FollowDirection=1}
        image_xscale+=(2-image_xscale)/10
        if spe>1{
            with(inst_xyscaang_form(x,y,objBullet2,1-i*0.05/3,0.4-i*0.05/3,direction,1)){mask_index=maskNothing}}
        x+=lengthdir_x(spe,direction);y+=lengthdir_y(spe,direction)
        spe+=(speto-spe)/6
        if x<-20 or x>820 or y<-20 or y>628 instance_destroy()
    }break;
    case 11:{
        image_xscale-=0.04
        image_yscale-=0.04
        if image_xscale<0 or image_yscale<0{
            instance_destroy()
        }
    }break;
}

if FollowDirection image_angle=direction
/*


            for(i=0;i<25;i+=1){
                with(inst_spdir_form(200+lengthdir_x(i*45,dir),608+lengthdir_y(i*45,dir),objBullet1,0,dir+180,8)){lr=1}
                with(inst_spdir_form(200+lengthdir_x(i*45,dir),608+lengthdir_y(i*45,dir),objBullet1,0,dir+180,8)){lr=-1}
            }
