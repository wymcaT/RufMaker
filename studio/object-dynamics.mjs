import {gmlNumber as num} from './gml.mjs';
export const physicsFields=['bulletAcceleration','bulletFriction','bulletGravity','bulletGravityDirection'];
export const animationFields=['imageIndex','imageSpeed','imageDelay'];
export const dynamicsDefaults={bulletAcceleration:0,bulletFriction:0,bulletGravity:0,bulletGravityDirection:270,imageIndex:0,imageSpeed:0,imageDelay:0};
export function hasAnimation(t){return animationFields.some(p=>(t[p]||0)!==0||t.keyframes?.some(k=>k.property===p)||t.expressions?.[p]);}
export function hasPhysics(t){return physicsFields.slice(0,3).some(p=>(t[p]||0)!==0||t.keyframes?.some(k=>k.property===p)||t.expressions?.[p]);}
export function normalizeDynamics(t){
  for(const [key,value] of Object.entries(dynamicsDefaults))if(t[key]===undefined)t[key]=value;
  if(Object.keys(dynamicsDefaults).some(p=>!Number.isFinite(t[p])||Math.abs(t[p])>1000000)||t.bulletFriction<0||!Number.isInteger(t.imageDelay)||t.imageDelay<0)throw Error('物理或动画参数无效。');
}
export function dynamicsSpawn(t){const get=field=>'rf_'+field;return `rf_result.rf_accel=${get('bulletAcceleration')};rf_result.rf_friction=max(0,${get('bulletFriction')});rf_result.rf_gravity=${get('bulletGravity')};rf_result.rf_gravity_dir=${get('bulletGravityDirection',270)};rf_result.rf_anim_enabled=${hasAnimation(t)?1:0};rf_result.rf_anim_birth=rf_born;rf_result.rf_anim_delay=max(0,round(rf_imageDelay));rf_result.rf_anim_speed=${get('imageSpeed')};rf_result.rf_anim_index=${get('imageIndex')}${t.kind==='rod'?`+max(0,rf_sample_time-(${num(t.clockStart??t.start)})-rf_result.rf_anim_delay)*rf_result.rf_anim_speed`:''};${t.kind==='rod'?`rf_result.rf_anim_delay=max(0,rf_result.rf_anim_delay-max(0,rf_sample_time-(${num(t.clockStart??t.start)})));`:''}rf_result.image_index=rf_result.rf_anim_index;`;}
export const gravityGml=`if !rf_stop_speed && rf_gravity!=0 {rf_velocity_x=lengthdir_x(rf_speed,rf_dir)+lengthdir_x(rf_gravity,rf_gravity_dir);rf_velocity_y=lengthdir_y(rf_speed,rf_dir)+lengthdir_y(rf_gravity,rf_gravity_dir);rf_speed=point_distance(0,0,rf_velocity_x,rf_velocity_y);if rf_speed>0.000000001 rf_dir=point_direction(0,0,rf_velocity_x,rf_velocity_y);}`;
export const animationTick=`if rf_anim_enabled && !rf_rainbow && rf_frozen_image<0 image_index=floor(rf_anim_index+max(0,rf_world-rf_anim_birth-rf_anim_delay)*rf_anim_speed);`;
export const spriteFrame=(sprite,index)=>`(((floor(${index}) mod max(1,sprite_get_number(${sprite})))+max(1,sprite_get_number(${sprite}))) mod max(1,sprite_get_number(${sprite})))`;
export function scaleDynamics(t,ratio){
  for(const p of ['bulletAcceleration','bulletFriction','bulletGravity','imageSpeed','imageDelay']){
    const factor=p==='imageDelay'?ratio:1/(p==='imageSpeed'?ratio:ratio**2);t[p]*=factor;if(p==='imageDelay')t[p]=Math.round(t[p]);
    if(t.expressions?.[p])t.expressions[p]='('+t.expressions[p]+')*'+num(factor);
    for(const k of [...t.keyframes,...t.keyframeCycle?.keys||[]].filter(k=>k.property===p)){k.value*=factor;if(k.expression)k.expression='('+k.expression+')*'+num(factor);}
  }
}
