import {fields} from './motion.mjs';
import {fxPassesGml} from './effects.mjs';
import {codeOnly} from './native-import.mjs';

// Keep native view objects and projections in the native pass. RufMaker's preview
// uses the recorded native view for its layers, then applies camera and effects.
export function nativeComposeGml(state) {
  return `if !instance_exists(objRufController) {draw_surface(application_surface,0,0);return 0;}
with(objRufController) {
var rf_src,rf_dst,rf_swap,rf_u,rf_dx,rf_dy,rf_age,rf_i,rf_offset,rf_samples,rf_band,rf_inset,rf_coverage,${fields.map(f=>'rf_'+f).join(',')};
if rf_fx_w!=room_width || rf_fx_h!=room_height {if surface_exists(rf_fx_scene)surface_free(rf_fx_scene);if surface_exists(rf_fx_a)surface_free(rf_fx_a);if surface_exists(rf_fx_b)surface_free(rf_fx_b);rf_fx_w=room_width;rf_fx_h=room_height;}
${['rf_fx_scene','rf_fx_a','rf_fx_b'].map(v=>`if !surface_exists(${v}) ${v}=surface_create(room_width,room_height);`).join('\n')}
if !surface_exists(rf_fx_scene) || !surface_exists(rf_fx_a) || !surface_exists(rf_fx_b) {draw_surface(application_surface,0,0);return 0;}
surface_set_target(rf_fx_scene);d3d_set_projection_ortho(0,0,room_width,room_height,0);
draw_set_blend_mode(bm_normal);draw_set_color(c_white);draw_set_alpha(1);draw_clear(c_black);
draw_surface_stretched(application_surface,0,0,room_width,room_height);
rf_src=rf_fx_scene;rf_dst=rf_fx_a;
if rf_cam_enabled {
 surface_set_target(rf_fx_a);d3d_set_projection_ortho(0,0,room_width,room_height,0);draw_clear(c_black);
 rf_dx=room_width/2-cos(degtorad(rf_cam_angle))*rf_cam_zoom*rf_cam_x+sin(degtorad(rf_cam_angle))*rf_cam_zoom*rf_cam_y;
 rf_dy=room_height/2-sin(degtorad(rf_cam_angle))*rf_cam_zoom*rf_cam_x-cos(degtorad(rf_cam_angle))*rf_cam_zoom*rf_cam_y;
 draw_surface_ext(rf_fx_scene,rf_dx,rf_dy,rf_cam_zoom,rf_cam_zoom,-rf_cam_angle,c_white,1);
 rf_src=rf_fx_a;rf_dst=rf_fx_b;
}
surface_reset_target();d3d_set_projection_ortho(0,0,room_width,room_height,0);
${fxPassesGml(state)}
surface_reset_target();d3d_set_projection_ortho(0,0,room_width,room_height,0);
draw_set_blend_mode(bm_normal);draw_set_color(c_white);draw_set_alpha(1);draw_surface(rf_src,0,0);
}
return 0;`;
}

// Native engines often render their own intermediate surfaces. Return those
// draws to the application surface, preserving the engine's room/view projection.
export const nativeSurfaceResetGml=`if application_surface_is_enabled() {surface_reset();} else {surface_reset_target();}return 0;`;
export function patchNativeSurfaceReset(source) {
  const edits=[...codeOnly(source).replace(/(['"])[\s\S]*?\1/g,m=>' '.repeat(m.length)).matchAll(/\bsurface_reset_target\b(?=\s*\(\s*\))/g)];
  for(const m of edits.reverse())source=source.slice(0,m.index)+'rf_native_surface_reset'+source.slice(m.index+m[0].length);
  return source;
}
