import {expressionCall} from './random-parameters.mjs';
const hex=/["']?(#[0-9a-fA-F]{6})["']?/g;
export const packedColor=expression=>/#[0-9a-fA-F]{6}/.test(expression||'');
export function numericColor(expression){return String(expression||'').replace(hex,(_,s)=>String(parseInt(s.slice(1,3),16)+parseInt(s.slice(3,5),16)*256+parseInt(s.slice(5,7),16)*65536));}
export function colorCall(expression,t,slot,context){const call=expressionCall(numericColor(expression),t,slot,null,{...context,randomFrame:'0'});return packedColor(expression)?`clamp(round(${call}),0,16777215)`:`rf_color_hue(${call})`;}
export const colorHueGml=`var rf_h,rf_x,rf_r,rf_g,rf_b;rf_h=(((argument0 mod 360)+360) mod 360)/60;rf_x=1-abs((rf_h mod 2)-1);rf_r=0;rf_g=0;rf_b=0;
if rf_h<1 {rf_r=1;rf_g=rf_x;}else if rf_h<2 {rf_r=rf_x;rf_g=1;}else if rf_h<3 {rf_g=1;rf_b=rf_x;}else if rf_h<4 {rf_g=rf_x;rf_b=1;}else if rf_h<5 {rf_r=rf_x;rf_b=1;}else {rf_r=1;rf_b=rf_x;}
return make_color_rgb(floor(rf_r*255+.5),floor(rf_g*255+.5),floor(rf_b*255+.5));`;
export const spawnAppearanceGml=`var rf_owner;rf_owner=argument0;
if rf_generation>0 depth=objRufController.depth-rf_generation;
if !instance_exists(rf_owner) return 0;
image_xscale*=rf_owner.rf_scale_multiplier;image_yscale*=rf_owner.rf_scale_multiplier;rf_base_scale=image_xscale;
if rf_owner.rf_inherit_sprite>=0 {rf_sprite=rf_owner.rf_inherit_sprite;if rf_collision sprite_index=rf_sprite;rf_rainbow=0;rf_frozen_image=rf_owner.rf_inherit_image;image_index=rf_frozen_image;image_speed=0;image_blend=rf_owner.rf_inherit_color;}
if rf_owner.rf_color_override>=0 image_blend=rf_owner.rf_color_override;
rf_raw_sx=image_xscale;rf_raw_sy=image_yscale;rf_raw_alpha=image_alpha;return 0;`;
