import {gmlNumber as num} from './gml.mjs';
import {stateCode,fields} from './motion.mjs';
import {parameterCode,expressionCall} from './random-parameters.mjs';
import {activeCode} from './bullet-schema.mjs';
import {trailMode} from './trails.mjs';
import {shapeSetup,shapePoint} from './shapes.mjs';
import {rainbowCode,rodSequence,releasedRainbow} from './rainbow.mjs';
import {templateBundles,templateStateCode} from './template-system.mjs';

const color=hex=>{const v=/^#[0-9a-f]{6}$/i.test(hex||'')?hex.slice(1):'ffffff';return `make_color_rgb(${parseInt(v.slice(0,2),16)},${parseInt(v.slice(2,4),16)},${parseInt(v.slice(4,6),16)})`;};
const locals=`rf_u,rf_px,rf_py,rf_len,rf_phase,rf_fraction,rf_seg,rf_mix,rf_color_sequence,${fields.map(f=>'rf_'+f).join(',')}`;
export function groupPoseGml(s){return `var ${locals};switch(argument0){
${s.tracks.map((t,i)=>t.kind==='group'?`case ${i}:${stateCode(t,'argument1')}global.rf_gx=rf_x;global.rf_gy=rf_y;global.rf_ga=rf_angle;global.rf_gscale=rf_scale;global.rf_galpha=rf_alpha;return 0;`:'').join('\n')}
}global.rf_gx=0;global.rf_gy=0;global.rf_ga=0;global.rf_gscale=1;global.rf_galpha=1;return 0;`;
}
export function emissionPointGml(t){
  const u='rf_epu',prefix=`rf_epu=.5;if rf_n>1 rf_epu=rf_idx/(rf_n-1);rf_epx=0;rf_epy=0;`;
  let code='';
  switch(t.emissionShape){
    case 'line':code=`rf_epx=(${u}-.5)*${'rf_emitWidth'};`;break;
    case 'sine':code=`rf_epx=(${u}-.5)*${'rf_emitWidth'};rf_epy=sin(${u}*${'rf_emitWaves'}*2*pi+${'rf_emitPhase'}*pi/180)*${'rf_emitHeight'}/2;`;break;
    case 'bezier':{const p=t.curvePoints.length===4?t.curvePoints:[{x:-120,y:0},{x:-60,y:-120},{x:60,y:120},{x:120,y:0}];code=['x','y'].map(axis=>`rf_ep${axis}=power(1-${u},3)*${num(p[0][axis])}+3*sqr(1-${u})*${u}*${num(p[1][axis])}+3*(1-${u})*sqr(${u})*${num(p[2][axis])}+power(${u},3)*${num(p[3][axis])};`).join('');break;}
    case 'ring':case 'ellipse':case 'arc':code=`rf_epa=${'rf_emitPhase'}+${t.emissionShape==='arc'?`${u}*${'rf_emitArc'}`:'rf_idx/rf_n*360'};rf_epx=lengthdir_x(${'rf_emitWidth'}/2,rf_epa);rf_epy=lengthdir_y(${(t.emissionShape==='ellipse'?'rf_emitHeight':'rf_emitWidth')}/2,rf_epa);`;break;
  }
  code+=`rf_epa=${'rf_emitRotation'};${t._parentIndex>=0?'rf_epa+=global.rf_ga;':''}rf_oldx=rf_epx;rf_epx=lengthdir_x(rf_oldx,rf_epa)+lengthdir_x(rf_epy,rf_epa-90);rf_epy=lengthdir_y(rf_oldx,rf_epa)+lengthdir_y(rf_epy,rf_epa-90);${t._parentIndex>=0?'rf_epx*=global.rf_gscale;rf_epy*=global.rf_gscale;':''}`;
  return prefix+code;
}
function tangentGml(t){
  let code='rf_tdx=1;rf_tdy=0;';const u='rf_epu';
  if(t.emissionShape==='line')code='rf_tdx=rf_emitWidth;rf_tdy=0;';
  else if(t.emissionShape==='sine')code=`rf_tdx=rf_emitWidth;rf_tdy=cos(${u}*rf_emitWaves*2*pi+rf_emitPhase*pi/180)*rf_emitHeight/2*rf_emitWaves*2*pi;`;
  else if(t.emissionShape==='bezier'){const p=t.curvePoints.length===4?t.curvePoints:[{x:-120,y:0},{x:-60,y:-120},{x:60,y:120},{x:120,y:0}];code=['x','y'].map(axis=>`rf_td${axis}=3*sqr(1-${u})*(${num(p[1][axis])}-${num(p[0][axis])})+6*(1-${u})*${u}*(${num(p[2][axis])}-${num(p[1][axis])})+3*sqr(${u})*(${num(p[3][axis])}-${num(p[2][axis])});`).join('');}
  else if(t.emissionShape!=='point')code=`rf_epa=(rf_emitPhase+${t.emissionShape==='arc'?`${u}*rf_emitArc`:'rf_idx/rf_n*360'})*pi/180;rf_tdx=-rf_emitWidth/2*sin(rf_epa);rf_tdy=-${t.emissionShape==='ellipse'?'rf_emitHeight':'rf_emitWidth'}/2*cos(rf_epa);`;
  return code+'rf_heading=rf_angle+point_direction(0,0,rf_tdx,rf_tdy)+rf_emitRotation;';
}
function spawnCase(t,index,eventCount,s){
  const context={birthSampling:true,i:'rf_idx',n:'rf_n',volley:'rf_wave',randomFrame:'0',randomOffset:t.randomScope==='group'?'0':`(rf_wave*1009${t.randomScope==='bullet'?'+rf_idx*9176':''})`};
  const get=(field,fallback=0)=>parameterCode(t,field,fallback,null,{...context,frame:'rf_sample_time'});
  const pose=(track,template=false)=>`${template?templateStateCode(track,'rf_sample_time',context,'rf_spawn_owner.rf_template_source'):stateCode(track,'rf_sample_time',context)}${emissionPointGml(track)}
rf_heading=rf_angle+(rf_idx-(rf_n-1)/2)*rf_spread+rf_idx*${num(t.indexAngle)};
${track.emissionFacing==='radial'||track.emissionFacing==='tangent'?`rf_heading=rf_angle+point_direction(0,0,rf_epx,rf_epy)${track._parentIndex>=0?'-global.rf_ga':''}+${track.emissionFacing==='tangent'?90:0};`:track.emissionFacing==='fixed'?`rf_heading=rf_angle+rf_idx*${num(track.indexAngle)};`:''}
${track.emissionFacing==='tangent'?tangentGml(track):''}
rf_x+=rf_epx;rf_y+=rf_epy;
${template?`if !argument5 {rf_oldx=rf_x-rf_spawn_owner.rf_anchor_x;rf_oldy=rf_y-rf_spawn_owner.rf_anchor_y;rf_x=rf_spawn_owner.rf_origin_x+lengthdir_x(rf_oldx,rf_spawn_owner.rf_rotation)+lengthdir_x(rf_oldy,rf_spawn_owner.rf_rotation-90);rf_y=rf_spawn_owner.rf_origin_y+lengthdir_y(rf_oldx,rf_spawn_owner.rf_rotation)+lengthdir_y(rf_oldy,rf_spawn_owner.rf_rotation-90);rf_heading+=rf_spawn_owner.rf_rotation;}`:''}
${track.emissionFacing==='aim'?`rf_heading=point_direction(rf_x,rf_y,rf_expression_player(0),rf_expression_player(1))+rf_angle${track._parentIndex>=0?'-global.rf_ga':''}${template?'+rf_spawn_owner.rf_rotation':''};`:''}`;
  const variants=templateBundles(s).filter(b=>b.tracks.some(x=>x.id===t.id)).map(b=>`case ${b.rootIndex}:${pose(b.tracks.find(x=>x.id===t.id),true)}break;`).join('\n');
  return `case ${index}:if instance_exists(rf_spawn_owner) {switch(rf_spawn_owner.rf_template_source){${variants}}}else {${pose(t)}}
if argument5 {rf_x=argument6;rf_y=argument7;rf_heading=argument8;rf_speed=argument9;}else rf_speed+=rf_idx*${num(t.indexSpeed)};
rf_result=instance_create(rf_x,rf_y,objRufBullet);rf_result.rf_system=1;rf_result.rf_source=${index};rf_result.rf_behavior=${index};if argument11 rf_result.rf_behavior=-1;
rf_result.rf_birth=rf_born;rf_result.rf_clock_shift=argument12;rf_result.rf_index=rf_idx;rf_result.rf_n=rf_n;rf_result.rf_volley=rf_wave;rf_result.rf_generation=argument10;rf_result.rf_life=rf_life;rf_result.rf_age=0;
rf_result.rf_dir=rf_heading;rf_result.rf_speed=rf_speed;rf_result.rf_base_dir=rf_heading;rf_result.rf_base_speed=rf_speed;rf_result.rf_base_scale=rf_scale*(1+rf_idx*${num(t.indexScale)});rf_result.rf_base_alpha=rf_alpha;
rf_result.rf_sprite=${t.sprite};rf_result.sprite_index=${t.sprite};rf_result.image_xscale=rf_result.rf_base_scale;rf_result.image_yscale=rf_result.rf_base_scale;rf_result.image_alpha=rf_alpha;rf_result.image_blend=${t.rainbow?'c_white':color(t.color)};
rf_result.rf_rotate=${t.rotateSprite?1:0};rf_result.rf_sprite_angle=rf_spriteAngle;rf_result.image_angle=rf_spriteAngle+${t.rotateSprite?'rf_heading':'0'};rf_result.rf_turn=${get('turnRate',3)};rf_result.rf_delay=${get('homingDelay')};
rf_result.rf_t_mode='${trailMode(t)}';rf_result.rf_t_interval=${get('trailInterval',3)};rf_result.rf_t_life=${get('trailLife',18)};rf_result.rf_t_alpha=${get('trailStartAlpha',.55)};rf_result.rf_t_scale_decay=${get('trailScaleDecay',.035)};rf_result.rf_t_alpha_decay=${get('trailAlphaDecay',.035)};rf_result.rf_t_speed=${get('trailSpeed')};rf_result.rf_t_direction=${get('trailDirection')};rf_result.rf_t_random=${t.trailRandom?1:0};
${t.rainbow?`rf_result.rf_rainbow=1;rf_result.rf_r_sequence=rf_wave*rf_n+rf_idx;rf_result.rf_r_density=rf_rainbowDensity;rf_result.rf_r_offset=rf_rainbowOffset;rf_result.rf_r_speed=rf_rainbowSpeed;rf_result.rf_r_start=${num(t.start)}+argument12;rf_result.rf_r_dynamic=${t.rainbowDynamic?1:0};rf_result.rf_r_reverse=${t.rainbowReverse?1:0};rf_result.image_index=${rainbowCode(t,'rf_wave*rf_n+rf_idx','rf_sample_time')};`:''}
for(rf_e=0;rf_e<${eventCount};rf_e+=1)rf_result.rf_cmd[rf_e]=0;
rf_result.rf_hx[0]=rf_x;rf_result.rf_hy[0]=rf_y;rf_result.rf_ha[0]=rf_result.image_angle;rf_result.rf_hs[0]=rf_result.image_xscale;rf_result.rf_hal[0]=rf_alpha;
return rf_result;`;
}
const expressionContext={x:'x',y:'y',frame:'(rf_world-rf_clock_shift)',age:'rf_age',direction:'rf_dir',speed:'rf_speed',i:'rf_index',n:'rf_n',volley:'rf_volley'};
function applyCode(type,value){switch(type){
  case 'speed':return `rf_speed=${value};`;case 'direction':return `rf_dir=${value};`;case 'scale':return `image_xscale=max(0,${value});image_yscale=image_xscale;`;case 'alpha':return `image_alpha=max(0,min(1,${value}));`;
  case 'turn':return `rf_spin=${value};`;case 'acceleration':return `rf_accel=${value};`;case 'aim':return `rf_dir=point_direction(x,y,rf_expression_player(0),rf_expression_player(1))+${value};`;
  case 'home':return `rf_target=point_direction(x,y,rf_expression_player(0),rf_expression_player(1));rf_delta=(((rf_target-rf_dir) mod 360+540) mod 360)-180;rf_dir+=max(-abs(${value}),min(abs(${value}),rf_delta));`;
  case 'collision':return `rf_collision=(${value}>=.5);`;case 'clear':return 'rf_dead=1;';default:return '';
}}
function actionCode(a,t,index,s){
  if(a.enabled===false||a.type==='spawn')return '';const age=`rf_age-${num(a.start)}-rf_index*${num(a.stagger)}`;
  const value=a.expression?expressionCall(a.expression,t,'action'+index,null,expressionContext):'rf_value';
  let code=`rf_at=${age};if rf_at>=0 && rf_at<=${num(a.duration)} {rf_u=1;${a.duration?`rf_u=rf_ease('${a.easing}',rf_at/${num(a.duration)},3,.3);`:''}rf_value=${num(a.from)}+(${num(a.to)}-${num(a.from)})*rf_u;`;
  if(a.relative){if(a.type==='scale')code+='rf_value*=rf_base_scale;';if(a.type==='alpha')code+='rf_value*=rf_base_alpha;';if(a.type==='direction')code+='rf_value+=rf_base_dir;';if(a.type==='speed')code+='rf_value+=rf_base_speed;';}
  if(a.type==='split'){const template=s.tracks.findIndex(t=>t.id===a.templateId&&t.kind==='emitter');code+=`if rf_at=0 && rf_generation<${num(a.maxGeneration)} {rf_split_template[rf_split]=${template>=0?template:'rf_source'};rf_split_plain[rf_split]=${template>=0?0:1};rf_split_count[rf_split]=${num(a.count)};rf_split_speed[rf_split]=${num(a.childSpeed)};rf_split_spread[rf_split]=${num(a.spread)};rf_split+=1;}`;}
  else if(['aim','clear'].includes(a.type))code+=`if rf_at=0 {${applyCode(a.type,value)}}`;
  else code+=applyCode(a.type,value);
  return code+'}';
}
function eventCode(e,index,s){
  const source=s.tracks.findIndex(t=>t.id===e.targetSource);let match=`rf_age>=${num(e.targetMinAge)} && rf_age<=${num(e.targetMaxAge)}`;
  if(e.targetSource)match+=' && rf_source='+source;if(e.targetTag){const ids=s.tracks.map((t,i)=>t.tag===e.targetTag?i:null).filter(v=>v!==null);match+=' && ('+(ids.length?ids.map(i=>'rf_source='+i).join(' || '):'0')+')';}if(e.targetVolley>=0)match+=' && rf_volley='+num(e.targetVolley);if(e.targetParity!=='all')match+=' && rf_index mod 2='+(e.targetParity==='even'?0:1);
  const sample=e.controlExpression?`switch(rf_source){${s.tracks.map((t,i)=>['emitter','rod'].includes(t.kind)?`case ${i}:rf_value=${expressionCall(e.controlExpression,t,'control|'+e.id,null,{...expressionContext,frame:'rf_world'})};break;`:'').join('')}}`:'';
  const body=sample+(e.controlAction==='color'?`image_blend=${color(e.controlColor)};`:applyCode(e.controlAction,'rf_value'));
  return `if rf_world>=${num(e.start)} && rf_world<=${num(e.end)} {if rf_cmd[${index}]=0 && (${match})${e.includeFuture?'':` && rf_birth<=${num(e.start)} && rf_world=${num(e.start)}`} rf_cmd[${index}]=1;
if rf_cmd[${index}] {rf_u=1;${e.end>e.start?`rf_u=rf_ease('${e.controlEasing}',(rf_world-${num(e.start)})/${num(e.end-e.start)},3,.3);`:''}rf_value=${num(e.controlFrom)}+(${num(e.controlValue)}-${num(e.controlFrom)})*rf_u;${body}}}`;
}
export function bulletSystemScripts(s){
  const events=s.tracks.filter(t=>t.enabled!==false&&t.kind==='control');
  const spawn=`var ${locals},rf_born,rf_sample_time,rf_spawn_owner,rf_idx,rf_n,rf_wave,rf_heading,rf_result,rf_e,rf_epu,rf_epx,rf_epy,rf_epa,rf_oldx,rf_oldy,rf_tdx,rf_tdy;
rf_born=argument1;rf_sample_time=rf_born-argument12;rf_spawn_owner=argument13;rf_idx=argument2;rf_n=argument3;rf_wave=argument4;switch(argument0){${s.tracks.map((t,i)=>['emitter','rod'].includes(t.kind)?spawnCase(t,i,events.length,s):'').join('\n')}}return noone;`;
  const tick=`var rf_world,rf_at,rf_value,rf_u,rf_target,rf_delta,rf_slot,rf_split,rf_split_template,rf_split_plain,rf_split_count,rf_split_speed,rf_split_spread,rf_j,rf_child,rf_split_i;
rf_world=argument0;if rf_kinematic return 0;if rf_last_update=rf_world return 0;rf_last_update=rf_world;rf_age=rf_world-rf_birth;if rf_age<0 {sprite_index=-1;return 0;}if rf_age>=rf_life {instance_destroy();return 0;}
rf_split=0;global.rf_tick_world=rf_world;switch(rf_behavior){${s.tracks.map((t,i)=>['emitter','rod'].includes(t.kind)?`case ${i}: ${t.flightMode==='aim'?`if rf_age>rf_delay {${applyCode('aim','0')}}`:t.flightMode==='turn'?`if rf_age>rf_delay {${applyCode('home','rf_turn')}}`:t.flightMode==='expression'?`if rf_age>rf_delay rf_dir=${expressionCall('('+t.flightExpression+')+0*frame',t,'flight',null,expressionContext)};`:''}${t.bulletActions.map((a,j)=>actionCode(a,t,j,s)).join('\n')}break;`:'').join('\n')}}
${events.map((e,i)=>eventCode(e,i,s)).join('\n')}
if rf_dead {instance_destroy();return 0;}if rf_age>0 {rf_speed+=rf_accel;rf_dir+=rf_spin;x+=lengthdir_x(rf_speed,rf_dir);y+=lengthdir_y(rf_speed,rf_dir);}if rf_rotate image_angle=rf_sprite_angle+rf_dir;
sprite_index=-1;if rf_collision sprite_index=rf_sprite;rf_slot=rf_age mod 301;rf_hx[rf_slot]=x;rf_hy[rf_slot]=y;rf_ha[rf_slot]=image_angle;rf_hs[rf_slot]=image_xscale;rf_hal[rf_slot]=image_alpha;
rf_template_bullet_actions(rf_world);if rf_dead {instance_destroy();return 0;}
for(rf_split_i=0;rf_split_i<rf_split;rf_split_i+=1){for(rf_j=0;rf_j<rf_split_count[rf_split_i];rf_j+=1){rf_child=rf_pattern_spawn(rf_split_template[rf_split_i],rf_world,rf_j,rf_split_count[rf_split_i],rf_volley,1,x,y,rf_dir+(rf_j-(rf_split_count[rf_split_i]-1)/2)*rf_split_spread[rf_split_i],rf_split_speed[rf_split_i],rf_generation+1,rf_split_plain[rf_split_i],rf_clock_shift,noone);if instance_exists(rf_child) {if rf_split_plain[rf_split_i] {rf_child.image_blend=image_blend;rf_child.image_xscale=image_xscale;rf_child.image_yscale=image_yscale;rf_child.rf_base_scale=image_xscale;rf_child.image_alpha=image_alpha;rf_child.rf_base_alpha=image_alpha;}with(rf_child) rf_pattern_tick(global.rf_tick_world);}}}
return 0;`;
  const update=`var ${locals},rf_now,rf_j,rf_n,rf_i,rf_instance,rf_heading;rf_now=argument0;rf_template_step(rf_now);
${s.tracks.map((t,index)=>{
    if(t.enabled===false)return '';if(t.kind==='emitter')return `if rf_template_visible(${index},-1,noone) && (${activeCode(t,'rf_now',s.tracks)}) && (rf_now-${num(t.start)}) mod ${parameterCode(t,'interval',1)}=0 {${stateCode(t,'rf_now')}rf_n=max(1,min(300,round(rf_count)));for(rf_j=0;rf_j<rf_n;rf_j+=1)rf_pattern_spawn(${index},rf_now+rf_j*${num(t.indexDelay)},rf_j,rf_n,(rf_now-${num(t.start)}) div ${parameterCode(t,'interval',1)},0,0,0,0,0,0,0,0,noone);}`;
    if(t.kind==='rod'&&t.releaseFrame>=0)return `if rf_now=${num(t.releaseFrame)} && rf_template_visible(${index},-1,noone) && (${activeCode(t,'rf_now',s.tracks)}) {${stateCode(t,'rf_now')}${shapeSetup(t,'rf_now')}rf_n=rf_count;for(rf_j=0;rf_j<rf_n;rf_j+=1){if !rf_template_visible(${index},rf_j,noone) continue;${shapePoint(t)}rf_color_sequence=${rodSequence(t)};rf_heading=${t.releaseDirection==='fixed'?'rf_angle':t.releaseDirection==='aim'?'point_direction(rf_px,rf_py,rf_expression_player(0),rf_expression_player(1))':'point_direction(rf_x,rf_y,rf_px,rf_py)'+(t.releaseDirection==='tangent'?'+90':'')};rf_instance=rf_pattern_spawn(${index},rf_now,rf_j,rf_n,0,1,rf_px,rf_py,rf_heading,rf_speed,0,0,0,noone);${releasedRainbow(t,'rf_now','rf_instance')}}}`;return '';
  }).join('\n')}
global.rf_tick_world=rf_now;with(objRufBullet) if rf_system rf_pattern_tick(global.rf_tick_world);return 0;`;
  return [['rf_group_pose',groupPoseGml(s)],['rf_pattern_spawn',spawn],['rf_pattern_tick',tick],['rf_pattern_step',update]];
}
