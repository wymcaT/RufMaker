#define Create_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
//Below is particle code:
Sname = part_system_create();
part_system_depth(Sname,-99999999)

particle1 = part_type_create();
part_type_shape(particle1,pt_shape_sphere);
part_type_size(particle1,0.05,0.10,0,0);
part_type_scale(particle1,1,1);
part_type_color1(particle1,c_red);
part_type_alpha2(particle1,1,0);
part_type_speed(particle1,1,5,0,0);
part_type_direction(particle1,0,359,0,0);
part_type_gravity(particle1,0.20*player.yflag,270);
part_type_orientation(particle1,0,0,0,0,0);
part_type_blend(particle1,0);
part_type_life(particle1,50,50);

emitter1 = part_emitter_create(Sname);
part_emitter_region(Sname,emitter1,x,x,y,y,0,0);
part_emitter_stream(Sname,emitter1,particle1,15);

alarm[0]=20;
#define Alarm_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
part_emitter_destroy(Sname,emitter1)
#define Step_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
if keyboard_check_pressed(global.restartbutton){
    part_system_destroy(Sname);
    instance_destroy();
}
#define Other_5
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
part_system_destroy(Sname);
instance_destroy();
#define KeyPress_27
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
part_system_destroy(Sname);
instance_destroy();
#define KeyPress_113
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
part_system_destroy(Sname);
instance_destroy();
