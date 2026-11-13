import {gmlNumber as num} from './gml.mjs';
import {readSpriteMask} from './sprite-mask.mjs';
import {bulletImageCode} from './rainbow.mjs';
export function validateMaskState(s){
 if(!s.tracks.some(t=>t.despawnBounds==='sprite-mask'))return;
 for(const t of s.tracks)if(t.despawnBounds==='sprite-mask'&&t.kind&&!['emitter','rod'].includes(t.kind))throw Error('Sprite-mask lifetime supports flying emitter bullets and released/copied rod members only, not fixed objects or groups.');
 for(const t of s.tracks)if(t.despawnBounds==='sprite-mask'&&t.boundsSprite&&!/^[A-Za-z_][A-Za-z_0-9]*$/.test(t.boundsSprite))throw Error('Invalid boundary sprite resource name: '+t.boundsSprite);
}
export function spriteMaskScripts(project,s,names){
 validateMaskState(s);if(!s.tracks.some(t=>t.despawnBounds==='sprite-mask'))return [];
 const metadata=new Map();for(const name of names){try{metadata.set(name,readSpriteMask(project,name));}catch(e){metadata.set(name,{error:e.message});}}
 for(const t of s.tracks.filter(t=>t.despawnBounds==='sprite-mask')){const name=t.boundsSprite||t.sprite;if(name&&name!=='-1'&&(!metadata.has(name)||metadata.get(name).error))throw Error(metadata.get(name)?.error??`Missing sprite-mask metadata: ${name}`);}
 let lookup='var rf_f;switch(argument0){';
 for(const [name,m] of metadata){if(!/^[A-Za-z_][A-Za-z_0-9]*$/.test(name))continue;lookup+=`case ${name}:`;if(m.error){lookup+=`show_error('Sprite-mask metadata unavailable: ${name}. Reload resources or convert PNG.',true);return 0;`;continue;}const boxes=m.colliders.length?m.colliders:[{empty:true}];lookup+=`rf_f=((floor(argument1) mod ${boxes.length})+${boxes.length}) mod ${boxes.length};switch(rf_f){`;boxes.forEach((b,i)=>{const v=b.empty?[0,0,0,0]:[b.left-m.originX,b.top-m.originY,b.right-m.originX,b.bottom-m.originY];lookup+=`case ${i}:switch(argument2){${v.map((x,k)=>`case ${k}:return ${num(x)};`).join('')}}break;`;});lookup+='}return 0;';}
 lookup+="}if argument0<0 return 0;show_error('Missing sprite-mask metadata. Reload resources.',true);return 0;";
 return [['rf_sprite_mask_value',lookup],['rf_sprite_mask_tick',`// Continuous inclusive collision-box contract. Native bbox rounding NOT assumed.
var rf_mask,rf_frame,rf_l,rf_t,rf_r,rf_b,rf_c,rf_s,rf_cx,rf_cy,rf_hw,rf_hh,rf_vc,rf_vs,rf_va,rf_factor,rf_sx,rf_sy,rf_i,rf_lx,rf_ly,rf_wx,rf_wy,rf_dx,rf_dy,rf_px,rf_py,rf_minx,rf_maxx,rf_miny,rf_maxy;
if !rf_mask_bounds || rf_die_start>=0 return 1;
rf_mask=rf_mask_sprite;if rf_mask<0 rf_mask=rf_sprite;
rf_frame=${bulletImageCode('argument0')};
rf_l=rf_sprite_mask_value(rf_mask,rf_frame,0);rf_t=rf_sprite_mask_value(rf_mask,rf_frame,1);rf_r=rf_sprite_mask_value(rf_mask,rf_frame,2);rf_b=rf_sprite_mask_value(rf_mask,rf_frame,3);
rf_factor=1;if (rf_die_fx=1 || rf_die_fx=3) && rf_die_frames>0 && rf_life_mode=0 rf_factor=clamp((rf_life-rf_age)/min(rf_life,rf_die_frames),0,1);
rf_sx=image_xscale*rf_factor;rf_sy=image_yscale*rf_factor;rf_c=cos(degtorad(image_angle));rf_s=sin(degtorad(image_angle));
rf_cx=0;rf_cy=0;rf_hw=room_width;rf_hh=room_height;rf_va=0;
if rf_mask_view {rf_cx=room_width/2;rf_cy=room_height/2;rf_hw=room_width/2;rf_hh=room_height/2;
if instance_exists(objRufController) && objRufController.rf_cam_enabled {rf_cx=objRufController.rf_cam_x;rf_cy=objRufController.rf_cam_y;rf_hw=objRufController.rf_cam_width/2;rf_hh=objRufController.rf_cam_height/2;rf_va=objRufController.rf_cam_angle;}
else if view_enabled && view_visible[0] {rf_cx=view_xview[0]+view_wview[0]/2;rf_cy=view_yview[0]+view_hview[0]/2;rf_hw=view_wview[0]/2;rf_hh=view_hview[0]/2;rf_va=view_angle[0];}}
rf_vc=cos(degtorad(rf_va));rf_vs=sin(degtorad(rf_va));
for(rf_i=0;rf_i<4;rf_i+=1){rf_lx=rf_l;if rf_i>=2 rf_lx=rf_r;rf_ly=rf_t;if (rf_i mod 2)=1 rf_ly=rf_b;rf_lx*=rf_sx;rf_ly*=rf_sy;rf_wx=x+rf_lx*rf_c+rf_ly*rf_s;rf_wy=y-rf_lx*rf_s+rf_ly*rf_c;rf_dx=rf_wx-rf_cx;rf_dy=rf_wy-rf_cy;rf_px=rf_dx*rf_vc-rf_dy*rf_vs;rf_py=rf_dx*rf_vs+rf_dy*rf_vc;
if rf_i=0 {rf_minx=rf_px;rf_maxx=rf_px;rf_miny=rf_py;rf_maxy=rf_py;}else {rf_minx=min(rf_minx,rf_px);rf_maxx=max(rf_maxx,rf_px);rf_miny=min(rf_miny,rf_py);rf_maxy=max(rf_maxy,rf_py);}}
if rf_maxx<(-rf_mask_view*rf_hw-rf_die_margin) || rf_minx>rf_hw+rf_die_margin || rf_maxy<(-rf_mask_view*rf_hh-rf_die_margin) || rf_miny>rf_hh+rf_die_margin {if !rf_lifetime_request() {instance_destroy();return 0;}}return 1;
`]];
}
