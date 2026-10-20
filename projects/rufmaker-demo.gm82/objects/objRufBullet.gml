#define Create_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
image_speed=0;image_index=0;rf_rainbow=0;rf_r_sequence=0;rf_r_density=1;rf_r_offset=0;rf_r_speed=.25;rf_r_start=0;rf_r_dynamic=0;rf_r_reverse=0;
rf_age=0;rf_life=120;rf_speed=0;rf_dir=0;rf_trail=0;rf_fixed=0;rf_rotate=0;rf_sprite_angle=0;rf_birth=0;rf_index=0;
rf_t_mode='off';rf_t_interval=3;rf_t_life=18;rf_t_alpha=.55;rf_t_scale_decay=.035;rf_t_alpha_decay=.035;rf_t_speed=0;rf_t_direction=0;rf_t_random=0;
#define Step_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
rf_custom_bullet();if rf_fixed exit;
rf_age+=1;x+=lengthdir_x(rf_speed,rf_dir);y+=lengthdir_y(rf_speed,rf_dir);if rf_rotate image_angle=rf_sprite_angle+rf_dir;
if rf_age>=rf_life instance_destroy();
#define Draw_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
var rf_sample,rf_tail,rf_drift,rf_seed,rf_world,rf_subimage;
rf_world=rf_birth+rf_age;if instance_exists(objRufController) rf_world=objRufController.rf_frame;
if rf_rainbow {image_speed=0;image_blend=c_white;image_index=rf_rainbow_index(rf_r_sequence,rf_world,rf_r_density,rf_r_offset,rf_r_speed,rf_r_start,rf_r_dynamic,rf_r_reverse,32);}
draw_sprite_ext(sprite_index,image_index,x,y,image_xscale,image_yscale,image_angle,image_blend,image_alpha);
