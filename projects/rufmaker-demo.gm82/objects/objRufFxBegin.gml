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
 rf_fx_active=(rf_frame>=407 && rf_frame<=907);rf_fx_rendering=0;if !rf_fx_active exit;
 if rf_fx_w!=room_width || rf_fx_h!=room_height {if surface_exists(rf_fx_scene)surface_free(rf_fx_scene);if surface_exists(rf_fx_a)surface_free(rf_fx_a);if surface_exists(rf_fx_b)surface_free(rf_fx_b);rf_fx_w=room_width;rf_fx_h=room_height;}
 if !surface_exists(rf_fx_scene) rf_fx_scene=surface_create(room_width,room_height);
if !surface_exists(rf_fx_a) rf_fx_a=surface_create(room_width,room_height);
if !surface_exists(rf_fx_b) rf_fx_b=surface_create(room_width,room_height);
 if !surface_exists(rf_fx_scene) || !surface_exists(rf_fx_a) || !surface_exists(rf_fx_b) exit;
 rf_fx_rendering=1;surface_set_target(rf_fx_scene);draw_set_blend_mode(bm_normal);draw_set_alpha(1);draw_clear(c_black);
 if background_showcolor draw_clear(background_color);
 // The supplied engine draws its room to global.surface1 before compositing objBackground.
 // Preserve that pipeline; rf_fx_reset_target redirects its final composite into our scene.
 if instance_exists(objBackground) && variable_global_exists('surface1') {if surface_exists(global.surface1) surface_set_target(global.surface1);}
 rf_camera_apply();
}
