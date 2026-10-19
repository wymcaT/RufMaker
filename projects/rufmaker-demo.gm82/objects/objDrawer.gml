#define Draw_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
switch(form){
    case 1:{
        draw_set_color(merge_color(c_white,c_red,0.4+sc*0.6))
        draw_rectangle(x-50+sc*50,0,x+49-sc*50,607,0)
        sc+=(1.1-sc)/10
        if sc>=1 instance_destroy()
    }break;
    case 2:{
        draw_set_color(merge_color(c_white,c_red,0.4+sc*0.6))
        draw_rectangle(0,y-50+sc*50,799,y+49-sc*50,0)
        if sc<0.5{
            if collision_rectangle(0,y-50+sc*50,799,y+49-sc*50,player,1,1)>0{
                with(player)event_user(0)
            }
        }
        sc+=(1.1-sc)/10
        if sc>=1 instance_destroy()
    }break;
}
