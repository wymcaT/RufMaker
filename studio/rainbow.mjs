import {gmlNumber as n} from './gml.mjs';
export const rainbowSprite='sprrainbowcherry';
export function normalizeRainbow(t) {
  const defaults={rainbow:false,rainbowReverse:false,rainbowDynamic:false,rainbowSourceSprite:'',rainbowSourceColor:'',rainbowSprite:'',rainbowMode:'sequence',rainbowDensity:1,rainbowOffset:0,rainbowSpeed:.25};
  for(const [key,value] of Object.entries(defaults))if(t[key]===undefined)t[key]=value;
  if(!['sequence','distance','angle'].includes(t.rainbowMode))t.rainbowMode='sequence';
  if(t.rainbow){t.sprite=t.rainbowSprite||rainbowSprite;t.color='#FFFFFF';}
}
export function rainbowCode(t,sequence,time,{sampled=true}={}) {
  if(!t.rainbow)return '0';
  return `rf_rainbow_index(${sequence},${time},${sampled?'rf_rainbowDensity':n(t.rainbowDensity,1)},${sampled?'rf_rainbowOffset':n(t.rainbowOffset)},${sampled?'rf_rainbowSpeed':n(t.rainbowSpeed,.25)},${n(t.start)},${t.rainbowDynamic?1:0},${t.rainbowReverse?1:0},32)`;
}
export function rodSequence(t,{rotation='0'}={}) {
  // Match the PointF preview at palette boundaries (one ten-thousandth of a color step).
  if(t.rainbowMode==='distance')return '(round(point_distance(rf_x,rf_y,rf_px,rf_py)*10000)/10000)';
  if(t.rainbowMode==='angle')return `(round((((point_direction(rf_x,rf_y,rf_px,rf_py)+${rotation}) mod 360+360) mod 360)*32/360*10000)/10000)`;
  return (t.shape||'rod')==='rod'&&t.rodMode==='center'?'abs(rf_fraction-.5)*(rf_count-1)':'rf_j';
}
export const spatialRainbow=t=>t.rainbow&&['distance','angle'].includes(t.rainbowMode);
export function releasedRainbow(t,time,target='rf_inst') {
  return spatialRainbow(t)?`if instance_exists(${target}) {${target}.rf_r_sequence=rf_color_sequence;${target}.image_index=${rainbowCode(t,'rf_color_sequence',time)};}`:'';
}
export const rainbowGml=`// sequence, timeline frame, density, phase, speed, start, dynamic, reverse, frame count
var p,n;p=argument0*argument2+argument3;
if argument6 p+=(argument1-argument5)*argument4;
if argument7 p=-p;
n=max(1,argument8);return ((floor(p) mod n)+n) mod n;
`;
