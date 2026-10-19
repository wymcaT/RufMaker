#define Create_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
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
if rf_age>=rf_life || x<-80 || x>room_width+80 || y<-80 || y>room_height+80 instance_destroy();
#define Draw_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
var rf_sample,rf_tail,rf_drift,rf_seed;
if !rf_fixed && rf_t_mode!='off' {
for(rf_sample=floor(rf_age/max(1,rf_t_interval))*max(1,rf_t_interval);rf_sample>=max(0,rf_age-rf_t_life+1);rf_sample-=max(1,rf_t_interval)) {
rf_tail=rf_age-rf_sample;rf_seed=(rf_birth+rf_sample)*301+rf_index;
rf_drift=rf_t_direction;if rf_t_random rf_drift=(sin((rf_seed+1)*12.9898)*43758.5453-floor(sin((rf_seed+1)*12.9898)*43758.5453))*360;
rf_trail_draw(sprite_index,x-lengthdir_x(rf_speed*rf_tail,rf_dir),y-lengthdir_y(rf_speed*rf_tail,rf_dir),image_xscale,image_angle,image_alpha,image_blend,rf_tail,rf_t_mode,rf_t_life,rf_t_alpha,rf_t_scale_decay,rf_t_alpha_decay,rf_t_speed,rf_drift);
}}
draw_sprite_ext(sprite_index,image_index,x,y,image_xscale,image_yscale,image_angle,image_blend,image_alpha);
