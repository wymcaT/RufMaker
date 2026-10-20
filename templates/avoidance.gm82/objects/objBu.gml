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
    case 1:
        x+=(500-x)/20;y+=(304-y)/20
        image_xscale+=(5-image_xscale)/20;image_yscale+=(5-image_yscale)/20
    break;
    case 2:
        speed-=speed/10
        if de=1{image_xscale-=0.1;image_yscale-=0.1;if image_xscale<=0 instance_destroy()}
    break;
    case 3:
        if de=0 image_alpha+=(0.3-image_alpha)/10
        else{image_xscale-=0.1;image_yscale-=0.1;if image_xscale<=0 instance_destroy()}
    break;
    case 4:
        repeat(3){
            y+=(yto-y)/8
            if place_meeting(x,y,player){
                with(player)event_user(0)
            }
        }
        Time+=1;if Time>30{S+=1;if x<S*20-20 instance_destroy()}
    break;
    case 5:
        if Vint=0{dir=random(360);Vint=1}
        speed-=speed/30
        Time+=1;if Time>100{
            image_xscale-=0.05;image_yscale-=0.05
            if image_xscale<=0 instance_destroy()}
    break;
    case 6:
        if de=1{
            image_xscale-=0.1;image_yscale-=0.1
            image_alpha-=0.1
            if image_alpha<=0 instance_destroy()
        }
    break;
    case 7:
        x+=(xto-x)/10
        Time+=1;if Time>50{mask_index=maskNothing;image_alpha-=0.1;if image_alpha<=0 instance_destroy()}
    break
    case 8:
        dird+=(-60-dird)/10
        x=576+lengthdir_x(len,dir+dird)
        y=352+lengthdir_y(len,dir+dird)
        Time+=1;if Time>50{mask_index=maskNothing;image_alpha-=0.1;if image_alpha<=0 instance_destroy()}
        else{if Time*32>len image_alpha=1
        Time2+=1;if Time2=5 image_index=3}
    break
}
