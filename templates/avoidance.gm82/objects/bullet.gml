#define Create_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
alarm[0] = 40;
#define Alarm_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
instance_destroy();
#define Step_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
if(collision_rectangle(x-4,y-4,x+4+hspeed,y+4,block,0,0)){
    a=instance_place(x+hspeed,y,block);
    if(a!=noone && a.object_index=block){
        instance_destroy();
    }
    else if(a.visible=1){
        instance_destroy();
    }
}
if(collision_line(x,y,x+hspeed,y,obj_wall,0,0)){
    instance_destroy()
}
image_alpha=player.image_alpha
