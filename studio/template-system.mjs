import {gmlNumber as num} from './gml.mjs';
import {stateCode,fields} from './motion.mjs';
import {activeCode} from './bullet-schema.mjs';
import {shapeSetup,shapePoint} from './shapes.mjs';
import {rainbowCode,rodSequence,releasedRainbow} from './rainbow.mjs';
import {parameterCode as parameter} from './random-parameters.mjs';
import {trailMode} from './trails.mjs';
import {colorCall} from './spawn-appearance.mjs';
export const templateKinds=['object','rod','emitter','group'];
export const hasTemplates=s=>s.tracks.some(t=>t.spawnActions.length||t.bulletActions.some(a=>a.type==='spawn'));
export function templateBundles(s){
  const referenced=new Set(s.tracks.flatMap(t=>[...t.bulletActions,...t.spawnActions]).filter(a=>a.type==='spawn'&&a.enabled!==false).map(a=>a.templateId)),bundles=[];
  for(let rootIndex=0;rootIndex<s.tracks.length;rootIndex++){
    const root=s.tracks[rootIndex];if(!referenced.has(root.id)||!templateKinds.includes(root.kind))continue;
    const selected=new Set([root.id]);let more;do{more=false;for(const t of s.tracks)if(selected.has(t.parentId)&&!selected.has(t.id)){selected.add(t.id);more=true;}}while(more);
    const tracks=s.tracks.filter(t=>selected.has(t.id)).map(t=>{const copy={...t};Object.defineProperty(copy,'_parentIndex',{value:t.id===root.id?-1:s.tracks.findIndex(g=>g.id===t.parentId&&g.kind==='group')});if(t.id===root.id){copy.parentId='';copy.enabled=true;copy.templateOnly=false;}return copy;});
    bundles.push({rootIndex,root:tracks.find(t=>t.id===root.id),tracks});
  }return bundles;
}
const locals=fields.map(f=>'rf_'+f).concat(['rf_u','rf_px','rf_py','rf_len','rf_phase','rf_fraction','rf_seg','rf_mix','rf_j','rf_n','rf_inst','rf_dx','rf_dy','rf_heading','rf_now','rf_clock','rf_angle_outer','rf_color_sequence','rf_center_x','rf_center_y','rf_look_sprite','rf_look_color','rf_look_image','rf_look_scale']).join(',');
export function templateStateCode(t,time,context={},root='rf_template_source'){
  return stateCode(t,time,context).replaceAll('rf_group_pose(',`rf_template_group_pose(${root},`);
}
export function spawnCondition(a,age,index,generation){
  const at=`(${age}-${num(a.start)}-${index}*${num(a.stagger)})`;
  let value=`${a.enabled===false?'0':'1'} && ${generation}<${num(a.maxGeneration)} && ${index}>=${num(a.spawnIndexFrom)} && ${at}>=0`;
  if(a.spawnIndexTo>=0)value+=` && ${index}<=${num(a.spawnIndexTo)}`;
  if(a.spawnParity!=='all')value+=` && ${index} mod 2=${a.spawnParity==='even'?0:1}`;
  return value+(a.spawnRepeat>0?`${a.duration>0?` && ${at}<=${num(a.duration)}`:''} && ${at} mod ${num(a.spawnRepeat)}=0`:` && ${at}=0`);
}
function generateAction(a,s,{age,index,generation,x,y,heading,sourceX=x,sourceY=y,after='',source,lookSprite,lookColor,lookImage,lookScale}){
  const target=s.tracks.findIndex(t=>t.id===a.templateId&&templateKinds.includes(t.kind));if(target<0)return '';
  const angle=`(${a.inheritAngle?heading:'0'}+${num(a.spawnAngle)}+${index}*${num(a.spawnAngleStep)})`;
  const wave=a.spawnRepeat>0?`((${age}-${num(a.start)}-${index}*${num(a.stagger)}) div ${num(a.spawnRepeat)})`:'0';
  const slot='spawnColor'+[...source.spawnActions,...source.bulletActions].indexOf(a),color=a.spawnColorExpression?colorCall(a.spawnColorExpression,source,slot,{frame:'rf_now',randomOffset:`(${wave}*1009)`,volley:wave}):'-1';
  return `if (${spawnCondition(a,age,index,generation)}) {rf_inst=rf_template_spawn(${target},rf_now,${x}+lengthdir_x(${num(a.spawnOffsetX)},${angle})+lengthdir_x(${num(a.spawnOffsetY)},${angle}-90),${y}+lengthdir_y(${num(a.spawnOffsetX)},${angle})+lengthdir_y(${num(a.spawnOffsetY)},${angle}-90),${angle},${generation}+1,${sourceX},${sourceY},${a.inheritAppearance?lookSprite:'-1'},${lookColor},${lookImage},${a.inheritScale?1:0},${lookScale},${color});if instance_exists(rf_inst) {${after}}}`;
}
function warp(x='rf_px',y='rf_py'){return `rf_dx=${x}-rf_anchor_x;rf_dy=${y}-rf_anchor_y;${x}=rf_origin_x+lengthdir_x(rf_dx,rf_rotation)+lengthdir_x(rf_dy,rf_rotation-90);${y}=rf_origin_y+lengthdir_y(rf_dx,rf_rotation)+lengthdir_y(rf_dy,rf_rotation-90);`;}
function trackActions(t,s,owner='noone',activeTracks=s.tracks){
  const source=s.tracks.findIndex(x=>x.id===t.id),inside=owner!=='noone',time=inside?'rf_clock':'rf_now',generation=inside?'rf_generation':'0';
  return t.spawnActions.map(a=>{
    const each=a.spawnOrigin==='each'&&t.kind==='rod',member=each?'rf_j':'-1';
    return `if (${activeCode(t,time,activeTracks)}) && rf_template_visible(${source},-1,${owner}) {
${inside?templateStateCode(t,time):stateCode(t,time)}${each?shapeSetup(t,time):''}rf_center_x=rf_x;rf_center_y=rf_y;${inside?warp('rf_center_x','rf_center_y'):''}
${each?'rf_n=rf_count;':'rf_n=1;'}for(rf_j=0;rf_j<rf_n;rf_j+=1) {if !rf_template_visible(${source},${member},${owner}) continue;
${each?shapePoint(t):'rf_px=rf_x;rf_py=rf_y;'}rf_color_sequence=${each?rodSequence(t,{rotation:inside?'rf_rotation':'0'}):'0'};rf_heading=${each?'point_direction(rf_x,rf_y,rf_px,rf_py)':'rf_angle'};${inside?warp()+'rf_heading+=rf_rotation;':''}
rf_look_sprite=${t.sprite};rf_look_color=${t.rainbow?'c_white':tint(t.color)};rf_look_image=${rainbowCode(t,'rf_color_sequence',time)};rf_look_scale=rf_scale;
${inside?'rf_look_scale*=rf_scale_multiplier;if rf_inherit_sprite>=0 {rf_look_sprite=rf_inherit_sprite;rf_look_color=rf_inherit_color;rf_look_image=rf_inherit_image;}if rf_color_override>=0 rf_look_color=rf_color_override;':''}
${generateAction(a,s,{source:t,age:`(${time}-${num(t.start)})`,index:'rf_j',generation,x:'rf_px',y:'rf_py',heading:'rf_heading',sourceX:'rf_center_x',sourceY:'rf_center_y',lookSprite:'rf_look_sprite',lookColor:'rf_look_color',lookImage:'rf_look_image',lookScale:'rf_look_scale',after:a.destroyParent?`rf_template_hide(${source},${member},${owner});`:''})}}}`;
  }).join('\n');
}
function tint(hex){const c=/^#[0-9a-f]{6}$/i.test(hex||'')?hex.slice(1):'ffffff';return `make_color_rgb(${parseInt(c.slice(0,2),16)},${parseInt(c.slice(2,4),16)},${parseInt(c.slice(4,6),16)})`;}
function appearance(t){
  const color=/^#[0-9a-f]{6}$/i.test(t.color||'')?t.color.slice(1):'ffffff',tint=`make_color_rgb(${parseInt(color.slice(0,2),16)},${parseInt(color.slice(2,4),16)},${parseInt(color.slice(4,6),16)})`;
  return `rf_inst.rf_sprite=${t.sprite};rf_inst.sprite_index=${t.kind==='object'?'-1':t.sprite};rf_inst.rf_collision=${t.kind==='object'?0:1};rf_inst.image_blend=${t.rainbow?'c_white':tint};rf_inst.image_xscale=rf_scale;rf_inst.image_yscale=rf_scale;rf_inst.image_alpha=rf_alpha;rf_inst.image_angle=${t.kind==='object'?'rf_angle+rf_rotation':`rf_spriteAngle+${t.rotateSprite?'rf_angle+rf_rotation':'0'}`};rf_inst.image_speed=0;rf_inst.image_index=${rainbowCode(t,'rf_color_sequence','rf_clock')};
rf_inst.rf_t_mode='${trailMode(t)}';rf_inst.rf_t_interval=${parameter(t,'trailInterval',3)};rf_inst.rf_t_life=${parameter(t,'trailLife',18)};rf_inst.rf_t_alpha=${parameter(t,'trailStartAlpha',.55)};rf_inst.rf_t_scale_decay=${parameter(t,'trailScaleDecay',.035)};rf_inst.rf_t_alpha_decay=${parameter(t,'trailAlphaDecay',.035)};rf_inst.rf_t_speed=${parameter(t,'trailSpeed',0)};rf_inst.rf_t_direction=${parameter(t,'trailDirection',0)};rf_inst.rf_t_random=${t.trailRandom?1:0};`;
}
function instanceMember(t,s,bundle){
  const source=s.tracks.findIndex(x=>x.id===t.id),active=activeCode(t,'rf_clock',bundle.tracks),clear=`for(rf_j=0;rf_j<rf_tc[${source}];rf_j+=1) if instance_exists(rf_tf[${source},rf_j]) with(rf_tf[${source},rf_j]) instance_destroy();rf_tc[${source}]=0;`;
  if(t.kind==='emitter')return `if (${active}) && rf_template_visible(${source},-1,id) && (rf_clock-${num(t.start)}) mod ${parameter(t,'interval',1)}=0 {
${templateStateCode(t,'rf_clock')}rf_n=clamp(round(rf_count),1,300);for(rf_j=0;rf_j<rf_n;rf_j+=1){rf_inst=rf_pattern_spawn(${source},rf_now+rf_j*${num(t.indexDelay)},rf_j,rf_n,(rf_clock-${num(t.start)}) div ${parameter(t,'interval',1)},0,0,0,0,0,rf_generation,0,rf_shift,id,rf_source_x,rf_source_y);if instance_exists(rf_inst) with(rf_inst) rf_pattern_tick(global.rf_template_world);}}`;
  if(!['object','rod'].includes(t.kind))return '';
  const release=t.kind==='rod'&&t.releaseFrame>=0?`if rf_clock=${num(t.releaseFrame)} {${templateStateCode(t,'rf_clock')}${shapeSetup(t,'rf_clock')}rf_center_x=rf_x;rf_center_y=rf_y;${warp('rf_center_x','rf_center_y')}rf_n=rf_count;for(rf_j=0;rf_j<rf_n;rf_j+=1)if rf_template_visible(${source},rf_j,id){${shapePoint(t)}rf_color_sequence=${rodSequence(t,{rotation:'rf_rotation'})};rf_heading=${t.releaseDirection==='fixed'?'rf_angle':t.releaseDirection==='aim'?'0':'point_direction(rf_x,rf_y,rf_px,rf_py)'+(t.releaseDirection==='tangent'?'+90':'')};${warp()}rf_heading+=rf_rotation;${t.releaseDirection==='aim'?'rf_heading=point_direction(rf_px,rf_py,rf_expression_player(0),rf_expression_player(1));':''}rf_inst=rf_pattern_spawn(${source},rf_now,rf_j,rf_n,0,1,rf_px,rf_py,rf_heading,rf_speed,rf_generation,0,rf_shift,id,rf_center_x,rf_center_y);${releasedRainbow(t,'rf_clock')}if instance_exists(rf_inst) with(rf_inst) rf_pattern_tick(global.rf_template_world);}}`:'';
  return `${release}
if (${active}) && rf_template_visible(${source},-1,id)${t.kind==='rod'&&t.releaseFrame>=0?' && rf_clock<'+num(t.releaseFrame):''} {
${templateStateCode(t,'rf_clock')}${t.kind==='rod'?shapeSetup(t,'rf_clock'):'rf_count=1;'}rf_n=rf_count;
for(rf_j=0;rf_j<max(rf_n,rf_tc[${source}]);rf_j+=1){
if rf_j>=rf_n || !rf_template_visible(${source},${t.kind==='rod'?'rf_j':'-1'},id) {if instance_exists(rf_tf[${source},rf_j]) with(rf_tf[${source},rf_j]) instance_destroy();rf_tf[${source},rf_j]=noone;continue;}
${t.kind==='rod'?shapePoint(t):'rf_px=rf_x;rf_py=rf_y;'}rf_color_sequence=${t.kind==='rod'?rodSequence(t,{rotation:'rf_rotation'}):'0'};${warp()}
if !instance_exists(rf_tf[${source},rf_j]) {rf_tf[${source},rf_j]=instance_create(rf_px,rf_py,objRufBullet);rf_inst=rf_tf[${source},rf_j];rf_inst.rf_system=1;rf_inst.rf_kinematic=1;rf_inst.rf_template_owner=id;rf_inst.rf_source=${source};rf_inst.rf_behavior=-1;rf_inst.rf_birth=rf_shift+${num(t.start)};rf_inst.rf_index=rf_j;rf_inst.rf_generation=rf_generation;rf_inst.rf_clock_shift=rf_shift;rf_inst.rf_life=${num(t.end-t.start+1)};}
rf_inst=rf_tf[${source},rf_j];rf_inst.x=rf_px;rf_inst.y=rf_py;rf_inst.rf_age=rf_now-rf_inst.rf_birth;rf_inst.rf_last_update=rf_now;rf_inst.rf_n=rf_n;${appearance(t)}with(rf_inst) rf_spawn_appearance(other.id);
rf_inst.rf_hx[rf_inst.rf_age mod 301]=rf_px;rf_inst.rf_hy[rf_inst.rf_age mod 301]=rf_py;rf_inst.rf_ha[rf_inst.rf_age mod 301]=rf_inst.image_angle;rf_inst.rf_hs[rf_inst.rf_age mod 301]=rf_scale;rf_inst.rf_hal[rf_inst.rf_age mod 301]=rf_alpha;
rf_inst.rf_himage[rf_inst.rf_age mod 301]=rf_inst.image_index;
}rf_tc[${source}]=rf_n;
}else {${clear}}`;
}
export function templateScripts(s){
  const bundles=templateBundles(s);
  const init=`global.rf_template_serial=0;var rf_i,rf_j;for(rf_i=0;rf_i<${s.tracks.length};rf_i+=1){for(rf_j=0;rf_j<=300;rf_j+=1)global.rf_thidden[rf_i,rf_j]=0;global.rf_tparent[rf_i]=-1;}
${s.tracks.map((t,i)=>`global.rf_tparent[${i}]=${t._parentIndex??-1};`).join('\n')}return 0;`;
  const visible=`var rf_source_idx,rf_index_idx,rf_owner_obj,rf_stop;rf_source_idx=argument0;rf_index_idx=argument1;rf_owner_obj=argument2;rf_stop=-1;
if instance_exists(rf_owner_obj) rf_stop=rf_owner_obj.rf_template_source;
if rf_index_idx>=0 {if rf_owner_obj=noone {if global.rf_thidden[rf_source_idx,rf_index_idx+1] return 0;}else if rf_owner_obj.rf_thidden[rf_source_idx,rf_index_idx+1] return 0;}
while(rf_source_idx>=0){if rf_owner_obj=noone {if global.rf_thidden[rf_source_idx,0] return 0;}else if rf_owner_obj.rf_thidden[rf_source_idx,0] return 0;if rf_source_idx=rf_stop break;rf_source_idx=global.rf_tparent[rf_source_idx];}return 1;`;
  const hide=`var rf_source_idx,rf_index_idx,rf_owner_obj;rf_source_idx=argument0;rf_index_idx=argument1;rf_owner_obj=argument2;if rf_owner_obj=noone global.rf_thidden[rf_source_idx,rf_index_idx+1]=1;else rf_owner_obj.rf_thidden[rf_source_idx,rf_index_idx+1]=1;
with(objRufBullet){if rf_static_source>=0 {if !rf_template_visible(rf_static_source,rf_index,noone) instance_destroy();}else if rf_kinematic {if !rf_template_visible(rf_source,rf_index,rf_template_owner) instance_destroy();}}return 0;`;
  const groups=`var ${locals};switch(argument0){${bundles.map(b=>`case ${b.rootIndex}:switch(argument1){${b.tracks.filter(t=>t.kind==='group').map(t=>`case ${s.tracks.findIndex(x=>x.id===t.id)}:${templateStateCode(t,'argument2',{},'argument0')}global.rf_gx=rf_x;global.rf_gy=rf_y;global.rf_ga=rf_angle;global.rf_gscale=rf_scale;global.rf_galpha=rf_alpha;return 0;`).join('\n')}}break;`).join('\n')}}global.rf_gx=0;global.rf_gy=0;global.rf_ga=0;global.rf_gscale=1;global.rf_galpha=1;return 0;`;
  const prepare=`var ${locals},rf_i;switch(rf_template_source){${bundles.map(b=>`case ${b.rootIndex}:rf_shift=rf_birth-${num(b.root.start)};rf_end=${num(b.root.end)}+rf_shift;${templateStateCode(b.root,num(b.root.start))}rf_anchor_x=rf_x;rf_anchor_y=rf_y;rf_scale_multiplier=1;if rf_inherit_scale rf_scale_multiplier=rf_inherit_size/max(.000001,rf_scale);${b.tracks.map(t=>{const i=s.tracks.findIndex(x=>x.id===t.id);return `rf_tc[${i}]=0;for(rf_i=0;rf_i<=300;rf_i+=1){rf_thidden[${i},rf_i]=0;rf_tf[${i},rf_i]=noone;}`;}).join('\n')}return 1;`).join('\n')}}return 0;`;
  const instance=`var ${locals};rf_now=argument0;if rf_last_frame=rf_now return 0;rf_last_frame=rf_now;if rf_now>rf_end {instance_destroy();return 0;}rf_clock=rf_now-rf_shift;global.rf_template_world=rf_now;
switch(rf_template_source){${bundles.map(b=>`case ${b.rootIndex}:${b.tracks.filter(t=>templateKinds.includes(t.kind)).map(t=>trackActions(t,s,'id',b.tracks)).join('\n')}${b.tracks.filter(t=>templateKinds.includes(t.kind)).map(t=>instanceMember(t,s,b)).join('\n')}break;`).join('\n')}}return 0;`;
  const spawn=`var rf_created;rf_created=instance_create(0,0,objRufTemplate);rf_created.rf_template_source=argument0;rf_created.rf_birth=argument1;rf_created.rf_origin_x=argument2;rf_created.rf_origin_y=argument3;rf_created.rf_rotation=argument4;rf_created.rf_generation=argument5;rf_created.rf_source_x=argument6;rf_created.rf_source_y=argument7;rf_created.rf_inherit_sprite=argument8;rf_created.rf_inherit_color=argument9;rf_created.rf_inherit_image=argument10;rf_created.rf_inherit_scale=argument11;rf_created.rf_inherit_size=argument12;rf_created.rf_color_override=argument13;global.rf_template_serial+=1;rf_created.rf_template_serial=global.rf_template_serial;
global.rf_template_world=argument1;with(rf_created){if !rf_template_prepare(){instance_destroy();}else rf_template_instance_step(global.rf_template_world);}return rf_created;`;
  const step=`var ${locals};rf_now=argument0;global.rf_template_world=rf_now;
${s.tracks.filter(t=>templateKinds.includes(t.kind)&&t.enabled!==false).map(t=>trackActions(t,s)).join('\n')}
global.rf_template_world=rf_now;with(objRufTemplate) rf_template_instance_step(global.rf_template_world);return 0;`;
  const bullet=`var rf_inst,rf_now;rf_now=argument0;switch(rf_behavior){${s.tracks.map((t,i)=>`case ${i}:${t.bulletActions.filter(a=>a.type==='spawn').map(a=>generateAction(a,s,{source:t,age:'rf_age',index:'rf_index',generation:'rf_generation',x:'x',y:'y',heading:'rf_dir',lookSprite:'rf_sprite',lookColor:'image_blend',lookImage:`(rf_rainbow*rf_rainbow_index(rf_r_sequence,rf_now,rf_r_density,rf_r_offset,rf_r_speed,rf_r_start,rf_r_dynamic,rf_r_reverse,32)+(!rf_rainbow)*image_index)`,lookScale:'image_xscale',after:a.destroyParent?'rf_dead=1;':''})).join('\n')}break;`).join('\n')}}return 0;`;
  return [['rf_template_init',init],['rf_template_visible',visible],['rf_template_hide',hide],['rf_template_group_pose',groups],['rf_template_prepare',prepare],['rf_template_instance_step',instance],['rf_template_spawn',spawn],['rf_template_step',step],['rf_template_bullet_actions',bullet]];
}
export const templateObject=`#define Create_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
rf_template_source=-1;rf_birth=0;rf_shift=0;rf_origin_x=0;rf_origin_y=0;rf_source_x=0;rf_source_y=0;rf_rotation=0;rf_generation=0;rf_last_frame=-1;rf_end=0;rf_inherit_sprite=-1;rf_inherit_color=c_white;rf_inherit_image=0;rf_inherit_scale=0;rf_inherit_size=1;rf_scale_multiplier=1;rf_color_override=-1;
#define Destroy_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
with(objRufBullet) if rf_kinematic && rf_template_owner=other.id instance_destroy();
`;
