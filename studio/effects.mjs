import {stylePass,postCameraPass} from './style-effects.mjs';
import {parameterCode as parameter} from './random-parameters.mjs';
import {stateCode,fields} from './motion.mjs';
import {gmlNumber as n} from './gml.mjs';

const action=`/*"/*'/**//* YYD ACTION\nlib_id=1\naction_id=603\napplies_to=self\n*/`;
const color=t=>{const h=/^#[\da-f]{6}$/i.test(t.color||'')?t.color.slice(1):'000000';return `make_color_rgb(${parseInt(h.slice(0,2),16)},${parseInt(h.slice(2,4),16)},${parseInt(h.slice(4,6),16)})`;};
export const effectTypes=['flash','shake','color','fade-in','fade-out','rgb','motion-blur','blur','invert','vignette','rgb-impact','glitch'];
// A completed scene already contains the RGB blended over its background. Keep
// those colors and fill alpha to one before sampling it as a full-screen image.
// Black * (1 - destination alpha) + destination RGB leaves every RGB unchanged.
export const fxOpaqueGml=`d3d_set_projection_ortho(0,0,room_width,room_height,0);draw_set_blend_mode_ext(bm_inv_dest_alpha,bm_one);draw_set_color(c_black);draw_set_alpha(1);draw_rectangle(0,0,room_width,room_height,0);draw_set_blend_mode(bm_normal);draw_set_color(c_white);return 0;`;
export const fxResetGml=`// Restore the timeline's full-frame target after the engine composites its own surfaces.
if instance_exists(objRufController) {
 if objRufController.rf_fx_active && surface_exists(objRufController.rf_fx_scene) {surface_set_target(objRufController.rf_fx_scene);rf_camera_apply();return 0;}
}
surface_reset_target();return 0;
`;
// Save games serialize surface handles, while the surfaces themselves belong to the live renderer.
export function fxResetBuffers() {
  return `rf_fx_scene=-1;rf_fx_a=-1;rf_fx_b=-1;rf_fx_active=0;rf_fx_rendering=0;rf_fx_w=room_width;rf_fx_h=room_height;
var rf_h;for(rf_h=0;rf_h<rf_fx_slots;rf_h+=1){rf_fx_history[rf_h]=-1;rf_fx_prior[rf_h]=-1;rf_fx_history_frame[rf_h]=-1000000;}`;
}
export function fxInitialization(state) {
  const active=state.tracks.some(t=>t.enabled!==false&&(t.kind==='effect'||t.kind==='camera'));
  const count=state.tracks.filter(t=>t.enabled!==false&&t.kind==='effect').length;
  let setup=`rf_fx_slots=${count};`+fxResetBuffers();
  if(state.nativeProject?.imported)return setup+'\napplication_surface_enable(rf_native_compose);';
  if(active)setup+='\n'+allocateTargets+'rf_inst=instance_create(0,0,objRufFxBegin);rf_inst.rf_owner=id;rf_inst=instance_create(0,0,objRufFxEnd);rf_inst.rf_owner=id;';
  return setup;
}
const releaseSurface=handle=>`if surface_exists(${handle}) surface_free(${handle});${handle}=-1;`;
const targets=['rf_fx_scene','rf_fx_a','rf_fx_b'];
const releaseTargets=targets.map(releaseSurface).join('\n');
const allocateTargets=targets.map(v=>`if !surface_exists(${v}) ${v}=surface_create(room_width,room_height);`).join('\n');
export const prepareFxTargets=`if rf_fx_w!=room_width || rf_fx_h!=room_height {${releaseTargets}rf_fx_w=room_width;rf_fx_h=room_height;}
${allocateTargets}`;
function releaseHistory(index) {
  return releaseSurface(`rf_fx_history[${index}]`)+releaseSurface(`rf_fx_prior[${index}]`)+`rf_fx_history_frame[${index}]=-1000000;`;
}
export function trimFxHistory(state) {
  const effects=state.tracks.filter(t=>t.enabled!==false&&t.kind==='effect');
  return effects.map((t,i)=>t.effect==='motion-blur'||t.effect==='glitch'
    ? `if (rf_fx_history[${i}]!=-1 || rf_fx_prior[${i}]!=-1) && (rf_frame<${n(t.start)} || rf_frame>${n(t.end)} || rf_fx_w!=room_width || rf_fx_h!=room_height) {${releaseHistory(i)}}`
    : '').filter(Boolean).join('\n');
}
export function fxCleanup() {
  return releaseTargets+`var rf_k;for(rf_k=0;rf_k<rf_fx_slots;rf_k+=1){${releaseHistory('rf_k')}}`;
}
export function makeFxObjects(state) {
  const enabled=state.tracks.filter(t=>t.enabled!==false&&(t.kind==='effect'||t.kind==='camera')).map(t=>`(rf_frame>=${n(t.start)} && rf_frame<=${n(t.end)})`).join(' || ')||'0';
  const begin=`#define Create_0\n${action}\nrf_owner=noone;
#define Draw_0
${action}
if !instance_exists(rf_owner) {instance_destroy();exit;}
with(rf_owner) {
 ${trimFxHistory(state)}
 rf_fx_active=${enabled};rf_fx_rendering=0;if !rf_fx_active exit;
 ${prepareFxTargets}
 if !surface_exists(rf_fx_scene) || !surface_exists(rf_fx_a) || !surface_exists(rf_fx_b) exit;
 rf_fx_rendering=1;surface_set_target(rf_fx_scene);draw_set_blend_mode(bm_normal);draw_set_alpha(1);draw_clear(c_black);
 if background_showcolor draw_clear(background_color);
 rf_camera_apply();
}
`;
  const parts=[`#define Create_0\n${action}\nrf_owner=noone;`, `#define Draw_0\n${action}
if !instance_exists(rf_owner) {instance_destroy();exit;}
with(rf_owner) {
if !rf_fx_rendering exit;rf_fx_opaque();surface_reset_target();rf_fx_rendering=0;rf_camera_apply();
var rf_src,rf_dst,rf_swap,rf_u,rf_dx,rf_dy,rf_age,rf_i,rf_offset,rf_samples,rf_band,rf_inset,rf_coverage,${fields.map(f=>'rf_'+f).join(',')};
rf_src=rf_fx_scene;rf_dst=rf_fx_a;
`];
  parts.push(postCameraPass);
  parts.push(fxPassesGml(state));
  parts.push(`draw_set_blend_mode(bm_normal);draw_set_alpha(1);draw_set_color(c_white);draw_surface(rf_src,view_xview[0],view_yview[0]);\n}\n`);
  return [
    ['objRufFxBegin','sprite=\nvisible=1\nsolid=0\npersistent=0\ndepth=1000000000\nparent=\nmask=\n',begin],
    ['objRufFxEnd','sprite=\nvisible=1\nsolid=0\npersistent=0\ndepth=-1000000000\nparent=\nmask=\n',parts.join('\n')]
  ];
}
export function fxPassesGml(state) {
  const effects=state.tracks.filter(t=>t.enabled!==false&&t.kind==='effect'),parts=[];
  for(let index=0;index<effects.length;index++) {
    const t=effects[index];
    parts.push(`if (rf_frame>=${n(t.start)} && rf_frame<=${n(t.end)}) {\n${stateCode(t,'rf_frame')}\nrf_strength=max(0,rf_strength);rf_fxOpacity=max(0,min(1,rf_fxOpacity));
rf_u=rf_ease('${t.easing||'linear'}',(rf_frame-(${n(t.clockStart??t.start)}))/${n(Math.max(1,(t.clockEnd??t.end)-(t.clockStart??t.start)))},${parameter(t,'easePower',3)},${parameter(t,'easePeriod',.3)});
surface_set_target(rf_dst);draw_set_blend_mode(bm_normal);draw_set_alpha(1);draw_clear(c_black);`);
    const draw=(c='c_white',a='1',x='0',y='0')=>`draw_surface_ext(rf_src,${x},${y},1,1,0,${c},${a});`;
    if(t.effect==='rgb-impact'||t.effect==='glitch')parts.push(stylePass(t,index));
    else if(t.effect==='shake')parts.push(`rf_age=rf_frame-(${n(t.clockStart??t.start)});rf_dx=round(sin(rf_age*${parameter(t,'fxFrequency',1)}*2.17)*rf_strength*rf_fxOpacity);rf_dy=round(cos(rf_age*${parameter(t,'fxFrequency',1)}*1.61)*rf_strength*rf_fxOpacity);${draw('c_white','1','rf_dx','rf_dy')}`);
    else if(t.effect==='rgb')parts.push(`rf_dx=round(lengthdir_x(rf_strength,rf_fxAngle));rf_dy=round(lengthdir_y(rf_strength,rf_fxAngle));draw_set_blend_mode(bm_add);${draw('c_white','1-rf_fxOpacity')}${draw('c_red','rf_fxOpacity','rf_dx','rf_dy')}${draw('c_lime','rf_fxOpacity')}${draw('c_blue','rf_fxOpacity','-rf_dx','-rf_dy')}draw_set_blend_mode(bm_normal);`);
    else if(t.effect==='blur'||t.effect==='motion-blur') {
      parts.push(`rf_samples=${parameter(t,'fxSamples',6)};draw_set_blend_mode(bm_add);${draw('c_white','1-rf_fxOpacity')}
for(rf_i=0;rf_i<rf_samples;rf_i+=1){rf_offset=(rf_i/(rf_samples-1)-.5)*rf_strength;rf_dx=lengthdir_x(rf_offset,rf_fxAngle);rf_dy=lengthdir_y(rf_offset,rf_fxAngle);${draw('c_white','rf_fxOpacity/rf_samples','rf_dx','rf_dy')}}draw_set_blend_mode(bm_normal);`);
      if(t.effect==='motion-blur')parts.push(`if rf_frame!=rf_fx_history_frame[${index}] {
 if surface_exists(rf_fx_history[${index}]) && rf_frame>rf_fx_history_frame[${index}] && rf_frame-rf_fx_history_frame[${index}]<=max(1,floor(${n(state.fps)}/5)) {
  rf_swap=rf_fx_prior[${index}];rf_fx_prior[${index}]=rf_fx_history[${index}];rf_fx_history[${index}]=rf_swap;
 }else{${releaseSurface(`rf_fx_prior[${index}]`)}}
}
if surface_exists(rf_fx_prior[${index}]) draw_surface_ext(rf_fx_prior[${index}],0,0,1,1,0,c_white,max(0,min(1,rf_fxRetention))*.95*rf_fxOpacity);
if !surface_exists(rf_fx_history[${index}]) rf_fx_history[${index}]=surface_create(room_width,room_height);
if surface_exists(rf_fx_history[${index}]) {
rf_fx_opaque();surface_reset_target();surface_copy_part(rf_fx_history[${index}],0,0,rf_dst,0,0,room_width,room_height);surface_set_target(rf_dst);
}rf_fx_history_frame[${index}]=rf_frame;`);
    } else if(t.effect==='invert')parts.push(`${draw()}draw_set_blend_mode_ext(bm_inv_dest_color,bm_inv_src_alpha);draw_set_alpha(rf_fxOpacity);draw_set_color(c_white);draw_rectangle(0,0,room_width,room_height,0);draw_set_blend_mode(bm_normal);draw_set_alpha(1);`);
    else if(t.effect==='vignette')parts.push(`${draw()}draw_set_color(c_black);for(rf_band=23;rf_band>=0;rf_band-=1){rf_inset=min(room_width,room_height)*.4*rf_band/24;draw_set_alpha(rf_fxOpacity*max(0,min(1,rf_strength/100))*sqr(1-rf_band/24));draw_rectangle(rf_inset,rf_inset,room_width-rf_inset,rf_inset+min(room_width,room_height)*.4/24+1,0);draw_rectangle(rf_inset,room_height-rf_inset-min(room_width,room_height)*.4/24-1,room_width-rf_inset,room_height-rf_inset,0);draw_rectangle(rf_inset,rf_inset,rf_inset+min(room_width,room_height)*.4/24+1,room_height-rf_inset,0);draw_rectangle(room_width-rf_inset-min(room_width,room_height)*.4/24-1,rf_inset,room_width-rf_inset,room_height-rf_inset,0);}draw_set_alpha(1);`);
    else {
      const coverage=t.effect==='fade-in'||t.effect==='flash'?'1-rf_u':t.effect==='fade-out'?'rf_u':'1';
      parts.push(`${draw()}rf_coverage=max(0,min(1,${coverage}))*rf_fxOpacity;draw_set_color(${color(t)});draw_set_alpha(rf_coverage);draw_rectangle(0,0,room_width,room_height,0);draw_set_alpha(1);`);
    }
    parts.push(`rf_fx_opaque();surface_reset_target();rf_src=rf_dst;if rf_dst=rf_fx_a rf_dst=rf_fx_b;else rf_dst=rf_fx_a;}`);
  }
  return parts.join('\n');
}
