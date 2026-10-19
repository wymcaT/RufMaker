import {stateCode,fields} from './motion.mjs';

const action=`/*"/*'/**//* YYD ACTION\nlib_id=1\naction_id=603\napplies_to=self\n*/`;
const n=(v,d=0)=>Number.isFinite(Number(v))?String(Number(v)):String(d);
const color=t=>{const h=/^#[\da-f]{6}$/i.test(t.color||'')?t.color.slice(1):'000000';return `make_color_rgb(${parseInt(h.slice(0,2),16)},${parseInt(h.slice(2,4),16)},${parseInt(h.slice(4,6),16)})`;};
export const effectTypes=['flash','shake','color','fade-in','fade-out','rgb','motion-blur','blur','invert','vignette'];
export const fxResetGml=`// Restore the timeline's full-frame target after the engine composites its own surfaces.
if instance_exists(objRufController) {
 if objRufController.rf_fx_active && surface_exists(objRufController.rf_fx_scene) {surface_set_target(objRufController.rf_fx_scene);return 0;}
}
surface_reset_target();return 0;
`;
export function fxInitialization(state) {
  const count=state.tracks.filter(t=>t.enabled!==false&&t.kind==='effect').length;
  return `rf_fx_scene=-1;rf_fx_a=-1;rf_fx_b=-1;rf_fx_active=0;rf_fx_rendering=0;rf_fx_w=0;rf_fx_h=0;
for(rf_j=0;rf_j<${Math.max(1,count)};rf_j+=1){rf_fx_history[rf_j]=-1;rf_fx_history_frame[rf_j]=-1000000;}
${count?'rf_inst=instance_create(0,0,objRufFxBegin);rf_inst.rf_owner=id;rf_inst=instance_create(0,0,objRufFxEnd);rf_inst.rf_owner=id;':''}`;
}
export function fxCleanup(state) {
  const count=state.tracks.filter(t=>t.enabled!==false&&t.kind==='effect').length;
  return `if surface_exists(rf_fx_scene) surface_free(rf_fx_scene);if surface_exists(rf_fx_a) surface_free(rf_fx_a);if surface_exists(rf_fx_b) surface_free(rf_fx_b);var rf_k;for(rf_k=0;rf_k<${count};rf_k+=1)if surface_exists(rf_fx_history[rf_k]) surface_free(rf_fx_history[rf_k]);`;
}
export function makeFxObjects(state) {
  const effects=state.tracks.filter(t=>t.enabled!==false&&t.kind==='effect');
  const enabled=effects.map(t=>`(rf_frame>=${n(t.start)} && rf_frame<=${n(t.end)})`).join(' || ')||'0';
  const allocate=['rf_fx_scene','rf_fx_a','rf_fx_b'].map(v=>`if !surface_exists(${v}) ${v}=surface_create(room_width,room_height);`).join('\n');
  const begin=`#define Create_0\n${action}\nrf_owner=noone;
#define Draw_0
${action}
if !instance_exists(rf_owner) {instance_destroy();exit;}
with(rf_owner) {
 rf_fx_active=${enabled};rf_fx_rendering=0;if !rf_fx_active exit;
 if rf_fx_w!=room_width || rf_fx_h!=room_height {if surface_exists(rf_fx_scene)surface_free(rf_fx_scene);if surface_exists(rf_fx_a)surface_free(rf_fx_a);if surface_exists(rf_fx_b)surface_free(rf_fx_b);rf_fx_w=room_width;rf_fx_h=room_height;}
 ${allocate}
 if !surface_exists(rf_fx_scene) || !surface_exists(rf_fx_a) || !surface_exists(rf_fx_b) exit;
 surface_set_target(rf_fx_scene);draw_set_blend_mode(bm_normal);draw_set_alpha(1);draw_clear(c_black);
 if background_showcolor draw_clear(background_color);
 // The supplied engine draws its room to global.surface1 before compositing objBackground.
 // Preserve that pipeline; rf_fx_reset_target redirects its final composite into our scene.
 if instance_exists(objBackground) && variable_global_exists('surface1') {if surface_exists(global.surface1) surface_set_target(global.surface1);}
 rf_fx_rendering=1;
}
`;
  const parts=[`#define Create_0\n${action}\nrf_owner=noone;`, `#define Draw_0\n${action}
if !instance_exists(rf_owner) {instance_destroy();exit;}
with(rf_owner) {
if !rf_fx_rendering exit;surface_reset_target();rf_fx_rendering=0;
var rf_src,rf_dst,rf_swap,rf_u,rf_dx,rf_dy,rf_age,rf_i,rf_offset,rf_samples,rf_band,rf_inset,rf_coverage,${fields.map(f=>'rf_'+f).join(',')};
rf_src=rf_fx_scene;rf_dst=rf_fx_a;
`];
  for(let index=0;index<effects.length;index++) {
    const t=effects[index];
    parts.push(`if (rf_frame>=${n(t.start)} && rf_frame<=${n(t.end)}) {\n${stateCode(t,'rf_frame')}\nrf_strength=max(0,rf_strength);rf_fxOpacity=max(0,min(1,rf_fxOpacity));
rf_u=rf_ease('${t.easing||'linear'}',(rf_frame-${n(t.start)})/${n(Math.max(1,t.end-t.start))},${n(t.easePower,3)},${n(t.easePeriod,.3)});
surface_set_target(rf_dst);draw_set_blend_mode(bm_normal);draw_set_alpha(1);draw_clear(c_black);`);
    const draw=(c='c_white',a='1',x='0',y='0')=>`draw_surface_ext(rf_src,${x},${y},1,1,0,${c},${a});`;
    if(t.effect==='shake')parts.push(`rf_age=rf_frame-${n(t.start)};rf_dx=round(sin(rf_age*${n(t.fxFrequency,1)}*2.17)*rf_strength*rf_fxOpacity);rf_dy=round(cos(rf_age*${n(t.fxFrequency,1)}*1.61)*rf_strength*rf_fxOpacity);${draw('c_white','1','rf_dx','rf_dy')}`);
    else if(t.effect==='rgb')parts.push(`rf_dx=round(lengthdir_x(rf_strength,rf_fxAngle));rf_dy=round(lengthdir_y(rf_strength,rf_fxAngle));draw_set_blend_mode(bm_add);${draw('c_white','1-rf_fxOpacity')}${draw('c_red','rf_fxOpacity','rf_dx','rf_dy')}${draw('c_lime','rf_fxOpacity')}${draw('c_blue','rf_fxOpacity','-rf_dx','-rf_dy')}draw_set_blend_mode(bm_normal);`);
    else if(t.effect==='blur'||t.effect==='motion-blur') {
      parts.push(`rf_samples=${n(Math.max(2,Math.min(16,t.fxSamples||6)))};draw_set_blend_mode(bm_add);${draw('c_white','1-rf_fxOpacity')}
for(rf_i=0;rf_i<rf_samples;rf_i+=1){rf_offset=(rf_i/(rf_samples-1)-.5)*rf_strength;rf_dx=lengthdir_x(rf_offset,rf_fxAngle);rf_dy=lengthdir_y(rf_offset,rf_fxAngle);${draw('c_white','rf_fxOpacity/rf_samples','rf_dx','rf_dy')}}draw_set_blend_mode(bm_normal);`);
      if(t.effect==='motion-blur')parts.push(`if !surface_exists(rf_fx_history[${index}]) rf_fx_history[${index}]=surface_create(room_width,room_height);
if surface_exists(rf_fx_history[${index}]) {
if rf_frame>rf_fx_history_frame[${index}] && rf_frame-rf_fx_history_frame[${index}]<=max(1,${n(state.fps)}/5) draw_surface_ext(rf_fx_history[${index}],0,0,1,1,0,c_white,max(0,min(1,rf_fxRetention))*.95*rf_fxOpacity);
if rf_frame!=rf_fx_history_frame[${index}] {surface_reset_target();surface_copy_part(rf_fx_history[${index}],0,0,rf_dst,0,0,room_width,room_height);surface_set_target(rf_dst);rf_fx_history_frame[${index}]=rf_frame;}
}`);
    } else if(t.effect==='invert')parts.push(`${draw()}draw_set_blend_mode_ext(bm_inv_dest_color,bm_inv_src_alpha);draw_set_alpha(rf_fxOpacity);draw_set_color(c_white);draw_rectangle(0,0,room_width,room_height,0);draw_set_blend_mode(bm_normal);draw_set_alpha(1);`);
    else if(t.effect==='vignette')parts.push(`${draw()}draw_set_color(c_black);for(rf_band=23;rf_band>=0;rf_band-=1){rf_inset=min(room_width,room_height)*.4*rf_band/24;draw_set_alpha(rf_fxOpacity*max(0,min(1,rf_strength/100))*sqr(1-rf_band/24));draw_rectangle(rf_inset,rf_inset,room_width-rf_inset,rf_inset+min(room_width,room_height)*.4/24+1,0);draw_rectangle(rf_inset,room_height-rf_inset-min(room_width,room_height)*.4/24-1,room_width-rf_inset,room_height-rf_inset,0);draw_rectangle(rf_inset,rf_inset,rf_inset+min(room_width,room_height)*.4/24+1,room_height-rf_inset,0);draw_rectangle(room_width-rf_inset-min(room_width,room_height)*.4/24-1,rf_inset,room_width-rf_inset,room_height-rf_inset,0);}draw_set_alpha(1);`);
    else {
      const coverage=t.effect==='fade-in'||t.effect==='flash'?'1-rf_u':t.effect==='fade-out'?'rf_u':'1';
      parts.push(`${draw()}rf_coverage=max(0,min(1,${coverage}))*rf_fxOpacity;draw_set_color(${color(t)});draw_set_alpha(rf_coverage);draw_rectangle(0,0,room_width,room_height,0);draw_set_alpha(1);`);
    }
    parts.push(`draw_set_blend_mode(bm_normal);draw_set_color(c_white);draw_set_alpha(1);surface_reset_target();rf_src=rf_dst;if rf_dst=rf_fx_a rf_dst=rf_fx_b;else rf_dst=rf_fx_a;}`);
  }
  parts.push(`draw_set_blend_mode(bm_normal);draw_set_alpha(1);draw_set_color(c_white);draw_surface(rf_src,view_xview[0],view_yview[0]);\n}\n`);
  return [
    ['objRufFxBegin','sprite=\nvisible=1\nsolid=0\npersistent=0\ndepth=1000000000\nparent=\nmask=\n',begin],
    ['objRufFxEnd','sprite=\nvisible=1\nsolid=0\npersistent=0\ndepth=-1000000000\nparent=\nmask=\n',parts.join('\n')]
  ];
}
