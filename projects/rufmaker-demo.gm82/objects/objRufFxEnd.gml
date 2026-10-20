#define Create_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
rf_owner=noone;
#define Draw_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
if !instance_exists(rf_owner) {instance_destroy();exit;}
with(rf_owner) {
if !rf_fx_rendering exit;surface_reset_target();rf_fx_rendering=0;
var rf_src,rf_dst,rf_swap,rf_u,rf_dx,rf_dy,rf_age,rf_i,rf_offset,rf_samples,rf_band,rf_inset,rf_coverage,rf_x,rf_y,rf_angle,rf_scale,rf_alpha,rf_speed,rf_spread,rf_count,rf_life,rf_x1,rf_y1,rf_x2,rf_y2,rf_length,rf_rollSpeed,rf_rainbowDensity,rf_rainbowOffset,rf_rainbowSpeed,rf_spriteAngle,rf_strength,rf_fxOpacity,rf_fxAngle,rf_fxRetention;
rf_src=rf_fx_scene;rf_dst=rf_fx_a;

draw_set_blend_mode(bm_normal);draw_set_alpha(1);draw_set_color(c_white);draw_surface(rf_src,view_xview[0],view_yview[0]);
}
