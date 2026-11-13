import {gmlNumber as num} from './gml.mjs';
export const frozenImageCheck='(rf_image_frozen || rf_frozen_image>=0)';
export const physicsFields=['bulletAcceleration','bulletFriction','bulletGravity','bulletGravityDirection'];
export const animationFields=['imageIndex','imageSpeed','imageDelay'];
export const dynamicsDefaults={bulletAcceleration:0,bulletFriction:0,bulletGravity:0,bulletGravityDirection:270,imageIndex:0,imageSpeed:0,imageDelay:0};
export function hasAnimation(t){return animationFields.some(p=>(t[p]||0)!==0||t.keyframes?.some(k=>k.property===p)||t.expressions?.[p]);}
export function hasPhysics(t){return physicsFields.slice(0,3).some(p=>(t[p]||0)!==0||t.keyframes?.some(k=>k.property===p)||t.expressions?.[p]);}
export function normalizeDynamics(t){
  for(const [key,value] of Object.entries(dynamicsDefaults))if(t[key]===undefined)t[key]=value;else if(t[key]===null)t[key]=0;
  if(Object.keys(dynamicsDefaults).some(p=>!Number.isFinite(t[p])||Math.abs(t[p])>1000000)||t.bulletFriction<0||!Number.isInteger(t.imageDelay)||t.imageDelay<0)throw Error('物理或动画参数无效。');
}
export function dynamicsSpawn(t){const get=field=>'rf_'+field;return `rf_result.rf_accel=${get('bulletAcceleration')};rf_result.rf_friction=max(0,${get('bulletFriction')});rf_result.rf_gravity=${get('bulletGravity')};rf_result.rf_gravity_dir=${get('bulletGravityDirection',270)};rf_result.rf_anim_enabled=${hasAnimation(t)?1:0};rf_result.rf_anim_birth=rf_born;rf_result.rf_anim_delay=max(0,round(rf_imageDelay));rf_result.rf_anim_speed=${get('imageSpeed')};rf_result.rf_anim_index=${get('imageIndex')}${t.kind==='rod'?`+max(0,rf_sample_time-(${num(t.clockStart??t.start)})-rf_result.rf_anim_delay)*rf_result.rf_anim_speed`:''};${t.kind==='rod'?`rf_result.rf_anim_delay=max(0,rf_result.rf_anim_delay-max(0,rf_sample_time-(${num(t.clockStart??t.start)})));`:''}rf_result.image_index=rf_result.rf_anim_index;if rf_spriteFrame>=0 {rf_result.rf_frozen_image=floor(rf_spriteFrame);rf_result.rf_image_frozen=1;rf_result.image_index=floor(rf_spriteFrame);}`;}
export const gravityGml=`if !rf_stop_speed && rf_gravity!=0 {rf_velocity_x=lengthdir_x(rf_speed,rf_dir)+lengthdir_x(rf_gravity,rf_gravity_dir);rf_velocity_y=lengthdir_y(rf_speed,rf_dir)+lengthdir_y(rf_gravity,rf_gravity_dir);rf_speed=point_distance(0,0,rf_velocity_x,rf_velocity_y);if rf_speed>0.000000001 rf_dir=point_direction(0,0,rf_velocity_x,rf_velocity_y);}`;
export const animationTick=`if rf_anim_enabled && !rf_rainbow && !${frozenImageCheck} image_index=floor(rf_anim_index+max(0,rf_world-rf_anim_birth-rf_anim_delay)*rf_anim_speed);`;
export const spriteFrame=(sprite,index)=>`(((floor(${index}) mod max(1,sprite_get_number(${sprite})))+max(1,sprite_get_number(${sprite}))) mod max(1,sprite_get_number(${sprite})))`;
export function scaleDynamics(t,ratio){
  if(t.controlAction==='gravity'){t.controlFrom/=ratio**2;t.controlValue/=ratio**2;for(const p of ['controlFrom','controlValue'])if(t.expressions?.[p])t.expressions[p]='('+t.expressions[p]+')*'+num(1/ratio**2);if(t.controlExpression&&ratio!==1)t.controlExpression='('+t.controlExpression+')*'+num(1/ratio**2);}
  for(const p of ['bulletAcceleration','bulletFriction','bulletGravity','imageSpeed','imageDelay']){
    const factor=p==='imageDelay'?ratio:1/(p==='imageSpeed'?ratio:ratio**2);t[p]*=factor;if(p==='imageDelay')t[p]=Math.round(t[p]);
    if(t.expressions?.[p])t.expressions[p]='('+t.expressions[p]+')*'+num(factor);
    for(const k of [...t.keyframes,...t.keyframeCycle?.keys||[]].filter(k=>k.property===p)){k.value*=factor;if(k.expression)k.expression='('+k.expression+')*'+num(factor);}
  }
}

export const dynamicsFields=physicsFields;
export const dynamicsRates=dynamicsFields.slice(0,3);
export function usesDynamics(t){return dynamicsFields.some(p=>t[p]!==undefined&&t[p]!==dynamicsDefaults[p]||t.expressions?.[p]||t.keyframes?.some(k=>k.property===p));}
export function hasDynamics(s){return s.tracks.some(t=>usesDynamics(t)||['gravity','gravityDirection'].includes(t.controlAction)||t.bulletActions?.some(a=>['gravity','gravityDirection'].includes(a.type))||t.children?.length&&hasDynamics({tracks:t.children}));}
export const clearGravity='rf_gravity=0;rf_gravity_dir=270;';
export function scaleDynamicsKey(k,ratio){if(!dynamicsRates.includes(k.property))return;k.value/=ratio**2;if(k.expression&&ratio!==1)k.expression='('+k.expression+')*'+num(1/ratio**2);}
