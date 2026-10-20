#define Step_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
switch(form){
    case 1:{
        Time+=1
        if Time mod 4=0{
            inst_sca_form(random(800),558+random_range(-25,25),objBullet2,1.5,5)
        }
    }break;
    case 2:{
        Time+=1
        if Time mod 6=0{
            len=sqrt(random(1))*100
            dir=random(40)+70
            with(inst_sca_form(400+lengthdir_x(len,dir),608+lengthdir_y(len,dir),objBullet2,1.5,7)){
                speed=20;direction=other.dir+random_range(-30,30)
                if other.maskNt=1 mask_index=maskNothing
            }
        }
    }break;
    case 3:{
        sc+=(sc<1)/20//(1-sc)/10
        while(Num<sc*20){
            Num+=1
            for(i=0;i<6;i+=1){
                inst=inst_form(x+lengthdir_x((Num-0.5)*15,i*60),y+lengthdir_y((Num-0.5)*15,i*60),objBullet1,11)
                inst.FollowDirection=1
                inst.image_blend=make_color_rgb(22,179,144)
                inst.direction=i*60
                inst.image_angle=inst.direction
                inst.speed=2
                inst.image_yscale=2-Num*0.1
            }
        }
        if !Vint{
            Vint=1
            for(i=0;i<18;i+=1){
                inst2[i,1]=inst_form(-100,-100,objBullet2,0)
                inst2[i,1].image_alpha=0
                inst2[i,1].image_xscale=0.5
                inst2[i,1].image_yscale=0.5
                inst2[i,2]=inst_form(-100,-100,objBullet2,0)
                inst2[i,2].image_alpha=0
                inst2[i,2].image_xscale=0.5
                inst2[i,2].image_yscale=0.5
            }
            dirxy=irandom(19)*20
            dirtxy=(irandom_range(20,39)*20+10)*lr
            repeat(20){
                inst=inst_form(x,y,objBullet2,9)
                inst.direction=random(360)
                inst.speed=random_range(4,8)
                    inst.image_xscale=0.75
                    inst.image_yscale=0.75
            }
        }
        if spread=0{
            if stt=0{
                lencir+=(100-lencir)/10
                dirz+=(180-dirz)/40
                dirdxy+=(dirtxy-dirdxy)/40
            }
            else{
                lencir+=(80-lencir)/20
                dirz+=(180-dirz)/8
                dirdxy+=(dirtxy-dirdxy)/8
            }
            if out>=1{
                To1+=To1<15
                lent1=Ease_expsin(To1/15)*75
            }
            if out=2{
                To2+=To2<15
                lent2=Ease_expsin(To2/15)*45
            }
            lex1=lengthdir_zx(lencir+lent1+lent2,dirxy+dirdxy+dirdxy2,dirz)
            ley1=lengthdir_zy(lencir+lent1+lent2,dirxy+dirdxy+dirdxy2,dirz)
            lez=lengthdir_zz(lencir/400,dirxy+dirdxy+dirdxy2,dirz)
            lex2=lengthdir_x(lencir+lent1+lent2,dirxy+dirdxy+90+dirdxy2)
            ley2=lengthdir_y(lencir+lent1+lent2,dirxy+dirdxy+90+dirdxy2)
            for(i=0;i<18;i+=1){
                inst2[i,1].x=x+lex1*lengthdir_x(1,i*20)+lex2*lengthdir_y(1,i*20)
                inst2[i,1].y=y+ley1*lengthdir_x(1,i*20)+ley2*lengthdir_y(1,i*20)
                inst2[i,1].image_xscale=0.5+lez*lengthdir_x(1,i*20)
                inst2[i,1].image_yscale=inst2[i,1].image_xscale
                inst2[i,2].x=x+lex1*lengthdir_x(1,i*20)+lex2*lengthdir_y(1,i*20)
                inst2[i,2].y=y+ley1*lengthdir_x(1,i*20)+ley2*lengthdir_y(1,i*20)
                inst2[i,2].image_xscale=0.5+lez*lengthdir_x(1,i*20)
                inst2[i,2].image_yscale=inst2[i,2].image_xscale
            }
        }
        else{
            if !Vint2{
                spe=-1
                Vint2=1
            }
            spe+=0.05
            len+=spe/sqrt(1+sqr(spe))*3
            lenn=200+len
            dird+=spe/sqrt(1+sqr(spe))*0.3
            TNum+=1
            for(i=0;i<18;i+=1){
                if instance_exists(inst2[i,1]){
                    inst2[i,1].x=x+lengthdir_x(lenn,i*20+dird+10)
                    inst2[i,1].y=y+lengthdir_y(lenn,i*20+dird+10)
                    if inst2[i,1].x<-50 or inst2[i,1].x>850 or inst2[i,1].y<-50 or inst2[i,1].y>658{
                        instance_destroy_id(inst2[i,1])
                    }
                    if TNum mod 2=0{
                        inst=inst_form(inst2[i,1].x,inst2[i,1].y,objBullet2,8)
                        inst.image_xscale=0.5
                        inst.image_yscale=0.5
                        inst.mask_index=maskNothing
                    }
                }
                if instance_exists(inst2[i,2]){
                    inst2[i,2].x=x+lengthdir_x(lenn,i*20-dird-10)
                    inst2[i,2].y=y+lengthdir_y(lenn,i*20-dird-10)
                    if inst2[i,2].x<-50 or inst2[i,2].x>850 or inst2[i,2].y<-50 or inst2[i,2].y>658{
                        instance_destroy_id(inst2[i,2])
                    }
                    if TNum mod 2=0{
                        inst=inst_form(inst2[i,2].x,inst2[i,2].y,objBullet2,8)
                        inst.image_xscale=0.5
                        inst.image_yscale=0.5
                        inst.mask_index=maskNothing
                    }
                }
            }
        }
    }break;
    case 4:{
        sc+=(sc<1)/15//(1-sc)/10
        while(Num<sc*30){
            Num+=1
            for(i=0;i<12;i+=1){
                inst=inst_form(x+lengthdir_x((Num-0.5)*18,i*30),y+lengthdir_y((Num-0.5)*18,i*30),objBullet1,11)
                inst.FollowDirection=1
                inst.image_blend=make_color_rgb(22,179,144)
                inst.direction=i*30
                inst.image_angle=inst.direction
                inst.speed=3
                inst.image_yscale=3-Num*0.1
                inst.image_xscale=1.5
            }
        }
    }break;
    case 5:{
        T+=1
        if T mod 4=0{
            inst_form(random(800),-50,objBullet2,10)
        }
    }break
    case 6:{
        if !Vint{
            Vint=1
            for(i=0;i<=30;i+=1){
                inst[i,0]=instance_create(-50,i*608/30,objBullet2)
                inst[i,0].image_xscale=0.75
                inst[i,0].image_yscale=0.75

                inst[i,1]=instance_create(850,i*608/30,objBullet2)
                inst[i,1].image_xscale=0.75
                inst[i,1].image_yscale=0.75
            }
        }
        T+=1
        for(i=0;i<30;i+=1){
            if T>i{
                Time[i]+=(Time[i]<50)
            }
            sc=Time[i]/50
            inst[i,0].x=Merge_Number(-25,250,Ease_sinwave_in(sc))
            inst[i,1].x=Merge_Number(825,550,Ease_sinwave_in(sc))
        }
    }break
}
