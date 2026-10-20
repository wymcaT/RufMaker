// The separate trail renderer stays behind every source, including custom GML depth changes.
var rf_sample,rf_tail,rf_drift,rf_seed,rf_world,rf_subimage;
rf_world=rf_birth+rf_age;if instance_exists(objRufController) rf_world=objRufController.rf_frame;
if !rf_fixed && rf_t_mode!='off' {
for(rf_sample=floor(rf_age/max(1,rf_t_interval))*max(1,rf_t_interval);rf_sample>=max(0,rf_age-rf_t_life+1);rf_sample-=max(1,rf_t_interval)) {
rf_tail=rf_age-rf_sample;rf_seed=(rf_birth+rf_sample)*301+rf_index;
rf_drift=rf_t_direction;if rf_t_random rf_drift=(sin((rf_seed+1)*12.9898)*43758.5453-floor(sin((rf_seed+1)*12.9898)*43758.5453))*360;
rf_subimage=image_index;if rf_rainbow rf_subimage=rf_rainbow_index(rf_r_sequence,rf_world-rf_tail,rf_r_density,rf_r_offset,rf_r_speed,rf_r_start,rf_r_dynamic,rf_r_reverse,32);
rf_trail_draw(sprite_index,x-lengthdir_x(rf_speed*rf_tail,rf_dir),y-lengthdir_y(rf_speed*rf_tail,rf_dir),image_xscale,image_angle,image_alpha,image_blend,rf_tail,rf_t_mode,rf_t_life,rf_t_alpha,rf_t_scale_decay,rf_t_alpha_decay,rf_t_speed,rf_drift,rf_subimage);
}}

return 0;
