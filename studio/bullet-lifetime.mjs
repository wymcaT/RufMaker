import {gmlNumber as num} from './gml.mjs';
export const lifetimeModes=['fixed','room','view'],deathEffects=['none','shrink','fade','shrink-fade'];
export function normalizeLifetime(t) {
  if(!lifetimeModes.includes(t.lifetimeMode))t.lifetimeMode='fixed';
  if(!deathEffects.includes(t.deathEffect))t.deathEffect='none';
  t.deathFrames=Number.isFinite(t.deathFrames)?Math.max(0,Math.round(t.deathFrames)):15;
  t.despawnMargin=Number.isFinite(t.despawnMargin)?t.despawnMargin:0;
  t.fadeInFrames=Number.isFinite(t.fadeInFrames)?Math.max(0,Math.round(t.fadeInFrames)):0;
}
export const lifetimeEnabled=t=>Boolean(t.fadeInFrames>0||t.lifetimeMode&&t.lifetimeMode!=='fixed'||t.deathEffect&&t.deathEffect!=='none'&&t.deathFrames>0);
export function lifetimeSpawn(t,target='rf_result') {
  return `${target}.rf_lifetime_enabled=${lifetimeEnabled(t)?1:0};${target}.rf_fade_in=${num(t.fadeInFrames)};${target}.rf_life_mode=${Math.max(0,lifetimeModes.indexOf(t.lifetimeMode))};${target}.rf_die_fx=${Math.max(0,deathEffects.indexOf(t.deathEffect))};${target}.rf_die_frames=${num(t.deathFrames,15)};${target}.rf_die_margin=${num(t.despawnMargin)};${target}.rf_die_start=-1;${target}.rf_raw_sx=${target}.image_xscale;${target}.rf_raw_sy=${target}.image_yscale;${target}.rf_raw_alpha=${target}.image_alpha;`;
}
export const lifetimeDefaults='rf_lifetime_enabled=0;rf_fade_in=0;rf_life_mode=0;rf_die_fx=0;rf_die_frames=0;rf_die_margin=0;rf_die_start=-1;rf_die_factor=1;rf_die_initial=1;rf_raw_sx=1;rf_raw_sy=1;rf_raw_alpha=1;';
export const lifetimeScripts=[['rf_lifetime_inside',`// mode, world x/y, outward margin in world pixels
var rf_cx,rf_cy,rf_hw,rf_hh,rf_a,rf_dx,rf_dy,rf_c,rf_s;
rf_cx=room_width/2;rf_cy=room_height/2;rf_hw=room_width/2;rf_hh=room_height/2;rf_a=0;
if argument0=2 {
if instance_exists(objRufController) && objRufController.rf_cam_enabled {rf_cx=objRufController.rf_cam_x;rf_cy=objRufController.rf_cam_y;rf_hw/=objRufController.rf_cam_zoom;rf_hh/=objRufController.rf_cam_zoom;rf_a=objRufController.rf_cam_angle;}
else if view_enabled && view_visible[0] {rf_cx=view_xview[0]+view_wview[0]/2;rf_cy=view_yview[0]+view_hview[0]/2;rf_hw=view_wview[0]/2;rf_hh=view_hview[0]/2;rf_a=view_angle[0];}
}
rf_dx=argument1-rf_cx;rf_dy=argument2-rf_cy;rf_c=cos(degtorad(rf_a));rf_s=sin(degtorad(rf_a));
return abs(rf_dx*rf_c-rf_dy*rf_s)<=rf_hw+argument3 && abs(rf_dx*rf_s+rf_dy*rf_c)<=rf_hh+argument3;
`],['rf_lifetime_request',`// Convert an explicit removal into a noninteractive dying instance.
if !rf_lifetime_enabled || rf_die_fx=0 || rf_die_frames<=0 return 0;
if rf_die_start<0 {rf_die_start=rf_age;rf_die_initial=1;if rf_life_mode=0 rf_die_initial=clamp((rf_life-rf_age)/min(rf_life,rf_die_frames),0,1);}rf_dead=0;rf_collision=0;return 1;
`],['rf_lifetime_tick',`// Called after actions and movement, before saving visual history.
if rf_die_start<0 && rf_life_mode!=0 {if !rf_lifetime_inside(rf_life_mode,x,y,rf_die_margin) {if !rf_lifetime_request() {instance_destroy();return 0;}}}
if rf_die_start>=0 {if rf_age-rf_die_start>=rf_die_frames {instance_destroy();return 0;}}
else if rf_life_mode=0 && rf_age>=rf_life {instance_destroy();return 0;}
rf_die_factor=1;
if rf_die_fx!=0 && rf_die_frames>0 {
if rf_die_start>=0 rf_die_factor=rf_die_initial*max(0,1-(rf_age-rf_die_start)/rf_die_frames);
else if rf_life_mode=0 rf_die_factor=clamp((rf_life-rf_age)/min(rf_life,rf_die_frames),0,1);
}
rf_raw_sx=image_xscale;rf_raw_sy=image_yscale;rf_raw_alpha=image_alpha;
if rf_die_fx=1 || rf_die_fx=3 {image_xscale*=rf_die_factor;image_yscale*=rf_die_factor;}
if rf_die_fx=2 || rf_die_fx=3 image_alpha*=rf_die_factor;
if rf_fade_in>0 && !rf_kinematic image_alpha*=clamp(rf_age/rf_fade_in,0,1);
return 1;
`]];
