import {actionValue} from './scalar-expressions.mjs';
import {groupIndex} from './timeline-groups.mjs';
import {gmlNumber as num} from './gml.mjs';
import {stateCode,fields} from './motion.mjs';
import {activeCode} from './bullet-schema.mjs';
import {copyPlacements,linkedCopyRules} from './copy-generation.mjs';

// A linked copy samples the original graph at its current clock. The saved
// independent template remains available when copyLinked is disabled.
export function linkedCopyScripts(s,poseCode=stateCode,activeFor=(t,time)=>`rf_copy_active=(${activeCode(t,time,s.tracks)});`){
  const rules=linkedCopyRules(s);if(!rules.length)return [['rf_copy_point','return 0;']];
  const create=(rule,source,cell)=>`rf_new=instance_create(0,0,objRufTemplate);rf_new.rf_scope_group=${groupIndex(s,s.tracks[source])};if instance_exists(argument1)rf_new.rf_scope_group=argument1.rf_scope_group;rf_new.rf_template_source=${source};rf_new.rf_birth=argument2;rf_new.rf_copy_rule=${rule};rf_new.rf_copy_owner=argument1;rf_new.rf_generation=1;if instance_exists(argument1) rf_new.rf_generation=argument1.rf_generation+1;${cell}global.rf_template_serial+=1;rf_new.rf_template_serial=global.rf_template_serial;global.rf_template_world=argument2;with(rf_new){if !rf_template_prepare() instance_destroy();else rf_template_instance_step(global.rf_template_world);}`;
  const spawn=`var rf_new,rf_nx,rf_ny,rf_c,rf_s,rf_ix,rf_iy;
if instance_exists(argument1){if argument1.rf_copy_started[argument0] return 0;argument1.rf_copy_started[argument0]=1;}else {if global.rf_copy_started[argument0] return 0;global.rf_copy_started[argument0]=1;}
switch(argument0){${rules.map(({sourceIndex,action:a},i)=>{
    if(a.copyMode!=='grid')return `case ${i}:${copyPlacements(a).map(p=>create(i,sourceIndex,`rf_new.rf_copy_dx=${num(p.x)};rf_new.rf_copy_dy=${num(p.y)};rf_new.rf_copy_pa=${num(p.angle)};rf_new.rf_copy_i=${p.index};rf_new.rf_copy_n=${p.count};`)).join('\n')}break;`;
    return `case ${i}:rf_c=abs(cos(${num(a.copyAngle)}*pi/180));rf_s=abs(sin(${num(a.copyAngle)}*pi/180));rf_nx=ceil(max(${num(a.copyWidth)},room_width*rf_c+room_height*rf_s)/${num(a.copySpacingX)})+1;rf_ny=ceil(max(${num(a.copyHeight)},room_width*rf_s+room_height*rf_c)/${num(a.copySpacingY)})+1;
if rf_nx*rf_ny>300 {show_error('Grid fill exceeds 300 copies; increase grid spacing.',0);return 0;}
for(rf_iy=0;rf_iy<rf_ny;rf_iy+=1)for(rf_ix=0;rf_ix<rf_nx;rf_ix+=1){${create(i,sourceIndex,`rf_new.rf_copy_dx=(rf_ix-floor(rf_nx/2))*${num(a.copySpacingX)};rf_new.rf_copy_dy=(rf_iy-floor(rf_ny/2))*${num(a.copySpacingY)};rf_new.rf_copy_pa=point_direction(0,0,rf_new.rf_copy_dx,rf_new.rf_copy_dy)+${num(a.copyAngle)};rf_new.rf_copy_i=rf_iy*rf_nx+rf_ix;rf_new.rf_copy_n=rf_nx*rf_ny;`)}}break;`;
  }).join('\n')}}return 1;`;
  const vars=fields.map(f=>'rf_'+f).concat(['rf_u','rf_heading','rf_cx','rf_cy']).join(',');
  const update=`var ${vars},rf_clock,rf_copy_active;if rf_copy_owner!=noone {if !instance_exists(rf_copy_owner) return 0;rf_shift=rf_copy_owner.rf_shift;}else rf_shift=0;rf_clock=argument0-rf_shift;
switch(rf_copy_rule){${rules.map(({source:t,sourceIndex,action:a},i)=>{
    const offset=actionValue(t,a,'copyFacingAngle',{frame:'rf_clock',i:'rf_copy_i',n:'rf_copy_n',direction:'rf_heading',x:'rf_x',y:'rf_y'});
    const facing=a.copyFacing==='keep'?`rf_heading+${offset}`:a.copyFacing==='fixed'?offset:`rf_copy_pa+${num(a.copyFacing==='inward'?180:a.copyFacing==='tangent'?90:0)}+${offset}`;
    let tint='rf_color_override=-1;rf_inherit_sprite=-1;';
    if(a.copyColorMode==='fixed'){const h=a.copyColor.slice(1);tint=`rf_color_override=make_color_rgb(${parseInt(h.slice(0,2),16)},${parseInt(h.slice(2,4),16)},${parseInt(h.slice(4,6),16)});`;}
    if(a.copyColorMode==='rainbow'){const hue=`(${num(a.copyHue)}+360*rf_copy_i/max(1,rf_copy_n)*${num(a.copyColorDensity)})`;tint=t.rainbow?`rf_inherit_sprite=${t.sprite};rf_inherit_color=c_white;rf_inherit_image=floor(((${hue} mod 360+360) mod 360)*32/360);rf_color_override=c_white;`:`rf_color_override=rf_color_hue(${hue});`;}
    return `case ${i}:${activeFor(t,'rf_clock')}if !rf_copy_active || !rf_template_visible(${sourceIndex},-1,rf_copy_owner) return 0;
${poseCode(t,'rf_clock')}rf_anchor_x=rf_x;rf_anchor_y=rf_y;rf_cx=rf_x;rf_cy=rf_y;rf_heading=rf_angle;rf_scale_multiplier=1;
if instance_exists(rf_copy_owner){rf_cx=rf_copy_owner.rf_origin_x+lengthdir_x(rf_x-rf_copy_owner.rf_anchor_x,rf_copy_owner.rf_rotation)+lengthdir_x(rf_y-rf_copy_owner.rf_anchor_y,rf_copy_owner.rf_rotation-90);rf_cy=rf_copy_owner.rf_origin_y+lengthdir_y(rf_x-rf_copy_owner.rf_anchor_x,rf_copy_owner.rf_rotation)+lengthdir_y(rf_y-rf_copy_owner.rf_anchor_y,rf_copy_owner.rf_rotation-90);rf_heading+=rf_copy_owner.rf_rotation;rf_scale_multiplier=rf_copy_owner.rf_scale_multiplier;}
rf_rotation=(${facing})-rf_angle;rf_origin_x=rf_cx+${a.copyMode==='grid'?'0':'rf_copy_dx'}+lengthdir_x(${num(a.spawnOffsetX)},rf_rotation)+lengthdir_x(${num(a.spawnOffsetY)},rf_rotation-90);rf_origin_y=rf_cy+${a.copyMode==='grid'?'0':'rf_copy_dy'}+lengthdir_y(${num(a.spawnOffsetX)},rf_rotation)+lengthdir_y(${num(a.spawnOffsetY)},rf_rotation-90);rf_source_x=rf_cx;rf_source_y=rf_cy;rf_end=${num(t.end)}+rf_shift;
rf_copy_grid=${a.copyMode==='grid'?1:0};rf_copy_sx=${num(a.copySpacingX)};rf_copy_sy=${num(a.copySpacingY)};rf_copy_angle=${num(a.copyAngle)};rf_copy_center=${a.copyCenter?1:0};${tint}return 1;`;
  }).join('\n')}}return 0;`;
  const point=`var rf_cp_x,rf_cp_y,rf_cp_u,rf_cp_v,rf_cp_c,rf_cp_s,rf_cp_ox,rf_cp_oy;rf_cp_c=cos(rf_copy_angle*pi/180);rf_cp_s=sin(rf_copy_angle*pi/180);rf_cp_x=argument0-room_width/2;rf_cp_y=argument1-room_height/2;rf_cp_u=rf_cp_x*rf_cp_c-rf_cp_y*rf_cp_s;rf_cp_v=rf_cp_x*rf_cp_s+rf_cp_y*rf_cp_c;
rf_cp_u=rf_cp_u-floor(rf_cp_u/rf_copy_sx)*rf_copy_sx+rf_copy_dx;rf_cp_v=rf_cp_v-floor(rf_cp_v/rf_copy_sy)*rf_copy_sy+rf_copy_dy;global.rf_copy_px=room_width/2+rf_cp_u*rf_cp_c+rf_cp_v*rf_cp_s;global.rf_copy_py=room_height/2-rf_cp_u*rf_cp_s+rf_cp_v*rf_cp_c;
rf_cp_ox=argument2;rf_cp_oy=argument3;if instance_exists(rf_copy_owner){rf_cp_ox=rf_copy_owner.rf_origin_x+lengthdir_x(argument2-rf_copy_owner.rf_anchor_x,rf_copy_owner.rf_rotation)+lengthdir_x(argument3-rf_copy_owner.rf_anchor_y,rf_copy_owner.rf_rotation-90);rf_cp_oy=rf_copy_owner.rf_origin_y+lengthdir_y(argument2-rf_copy_owner.rf_anchor_x,rf_copy_owner.rf_rotation)+lengthdir_y(argument3-rf_copy_owner.rf_anchor_y,rf_copy_owner.rf_rotation-90);}
global.rf_copy_omit=!rf_copy_center && abs(global.rf_copy_px-rf_cp_ox)<.01 && abs(global.rf_copy_py-rf_cp_oy)<.01;return 0;`;
  return [['rf_copy_link_spawn',spawn],['rf_copy_link_update',update],['rf_copy_point',point]];
}
