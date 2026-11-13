import {blendModeCode,blendScope} from './sprite-blend.mjs';
import {parameterCode as parameter} from './random-parameters.mjs';
import {gmlNumber as num} from './gml.mjs';
// Trail samples are evaluated from their birth time, so seeking needs no simulation.
export const trailModes=['off','fade','shrink','shrink-fade','appear','custom'];
export const trailMode=t=>t.trailMode==='legacy'||t.trailMode===undefined?(t.trail?'fade':'off'):t.trailMode;
export const trailDirection=(t,seed)=>t.trailRandom?`((sin((${seed}+1)*12.9898)*43758.5453-floor(sin((${seed}+1)*12.9898)*43758.5453))*360)`:parameter(t,'trailDirection');
export function trailCall(t,{sprite=t.sprite,x='rf_x',y='rf_y',scale='rf_scale',axisX='rf_scaleX',axisY='rf_scaleY',angle='rf_angle',alpha='rf_alpha',color,age,seed,subimage='0'}) {
  return `rf_trail_draw_xy(${sprite},${x}+lengthdir_x((${age})*${parameter(t,'trailSpeed')},${trailDirection(t,seed)}),${y}+lengthdir_y((${age})*${parameter(t,'trailSpeed')},${trailDirection(t,seed)}),(${scale})*(${axisX}),(${scale})*(${axisY}),${angle},${alpha},${color},${age},'${trailMode(t)}',${parameter(t,'trailLife')},${parameter(t,'trailStartAlpha')},${parameter(t,'trailScaleDecay')},${parameter(t,'trailAlphaDecay')},${subimage});`;
}
export const trailGml=`// Arguments: sprite, x, y, scale, angle, alpha, color, age, mode,
// lifetime, initial opacity, scale decay/frame, alpha decay/frame, drift speed, drift angle.
var a,m,n,z,o,d,rf_images,rf_image;
a=argument7;m=argument8;n=max(1,argument9);z=1;o=argument10;
if m='off' || a<0 || a>=n return 0;
if m='shrink' || m='shrink-fade' z=1-a/n;
if m='fade' || m='shrink-fade' o*=1-a/n;
if m='appear' o*=a/n;
if m='custom' {z=max(0,1-argument11*a);o=argument10-argument12*a;}
if z<=0 || o<=0 return 0;
d=argument13*a;
if argument0<0 return 0;rf_images=max(1,sprite_get_number(argument0));rf_image=((floor(argument15) mod rf_images)+rf_images) mod rf_images;
draw_sprite_ext(argument0,rf_image,argument1+lengthdir_x(d,argument14),argument2+lengthdir_y(d,argument14),argument3*z,argument3*z,argument4,argument6,argument5*max(0,min(1,o)));
return 0;
`;

// Separate axes and pre-drifted positions use 15 arguments. Legacy rf_trail_draw remains unchanged.
export const trailXYGml=`// sprite, x, y, scaleX, scaleY, angle, alpha, color, age, mode, life, opacity, size decay, alpha decay, subimage
var a,m,n,z,o,rf_images,rf_image;a=argument8;m=argument9;n=max(1,argument10);z=1;o=argument11;
if m='off' || a<0 || a>=n return 0;
if m='shrink' || m='shrink-fade' z=1-a/n;
if m='fade' || m='shrink-fade' o*=1-a/n;
if m='appear' o*=a/n;
if m='custom' {z=max(0,1-argument12*a);o=argument11-argument13*a;}
if z<=0 || o<=0 return 0;
if argument0<0 return 0;rf_images=max(1,sprite_get_number(argument0));rf_image=((floor(argument14) mod rf_images)+rf_images) mod rf_images;
draw_sprite_ext(argument0,rf_image,argument1,argument2,argument3*z,argument4*z,argument5,argument7,max(0,min(1,argument6*max(0,min(1,o)))));
return 0;
`;

// Product draw sites use this scoped wrapper; legacy trailCall ABI is unchanged.
export function blendTrailCall(t,options){return blendScope(trailCall(t,options),blendModeCode(t));}
