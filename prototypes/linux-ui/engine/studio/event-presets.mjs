import {clearGravity} from './object-dynamics.mjs';
import {eventValue,eventAssignment,setEventProperty} from './group-events.mjs';
import {eventGroupIndex,groupIndex} from './timeline-groups.mjs';
import {gmlNumber as num} from './gml.mjs';
import {activeCode} from './bullet-schema.mjs';
import {fields,stateCode} from './motion.mjs';
import {parameterCode as parameter} from './random-parameters.mjs';
import {shapeSetup,shapePoint} from './shapes.mjs';
import {rodSequence,releasedRainbow} from './rainbow.mjs';
import {templateBundles} from './template-system.mjs';
import {lifetimeSpawn} from './bullet-lifetime.mjs';

export const eventKeys=['clear-bullets','clear-danmaku','stop-bullets','reset-motion','orbit-bullets','group-copy','group-clear','group-set','group-orbit','player-move','player-set'];
export const isEvent=t=>t.kind==='control'&&eventKeys.includes(t.eventPreset);
export const nativeEventObjects=t=>String(t.eventNativeObjects||'').split(/[,，;；\r\n]/).map(s=>s.trim()).filter(Boolean).filter((s,i,a)=>a.indexOf(s)===i);
export const hasEvents=s=>s.tracks.some(isEvent);
export const isOrbit=e=>e.eventPreset==='orbit-bullets'||e.eventPreset==='group-orbit';
const selectiveOrbit=e=>isOrbit(e)&&!!(e.targetSource||e.targetTag);
export const orbitSourceMatches=(e,t)=>!isOrbit(e)||(!e.targetSource||e.targetSource===t.id||e.targetSource===t.clipSourceId)&&(!e.targetTag||e.targetTag===t.tag);
// Source rows are stable across templates; scope is the member's inherited group.
// Do not use ordinary age/parity/volley/includeFuture filters for these events.
export function orbitSelectorCode(e,s,source='rf_source'){
  if(!selectiveOrbit(e))return '';
  const ids=s.tracks.flatMap((t,i)=>orbitSourceMatches(e,t)?[i]:[]);
  return ids.length?'('+ids.map(i=>source+'='+i).join(' || ')+')':'0';
}

const clearAll=s=>s.tracks.filter(t=>isEvent(t)&&t.eventPreset==='clear-danmaku'&&t.enabled!==false);
const conditions=(tracks,time,s)=>tracks.map(t=>'('+activeCode(t,time,s.tracks)+')').join(' || ')||'0';
export function eventEarly(s){const events=s.tracks.filter(t=>isEvent(t)&&['clear-bullets','clear-danmaku'].includes(t.eventPreset)&&t.enabled!==false);return events.length?`if !rf_kinematic && rf_birth<=rf_world && (${conditions(events,'rf_world',s)}) {instance_destroy();return 0;}`:'';}
export function eventTick(e,s,index=-1){const active=activeCode(e,'rf_world',s.tracks);let action='';
  if(e.eventPreset==='stop-bullets')action='rf_speed=0;rf_stop_speed=1;';
  if(e.eventPreset==='reset-motion'||e.eventPreset==='group-set'){
    const isGroup=e.eventPreset==='group-set';
    const group=isGroup?eventGroupIndex(s,e):-1;
    if(isGroup&&group<0)return '';
    const groupGuard=isGroup?`if rf_scope_group=${group} `:'';
    let resetAction=`rf_stop_speed=0;rf_orbiting=0;${clearGravity}${e.eventRestoreBirth?'rf_speed=rf_base_speed;rf_dir=rf_base_dir;':eventAssignment(e,'controlValue',s,'rf_speed')+eventAssignment(e,'controlFrom',s,'rf_dir')}${eventAssignment(e,'eventAcceleration',s,'rf_accel')}${eventAssignment(e,'eventFriction',s,'rf_friction')}${eventAssignment(e,'eventTurn',s,'rf_spin')}rf_motion_reset=${e.eventDisableMotion?1:0};`;
    if(e.eventResetLife){
      const lifeCode=`rf_birth=rf_world;rf_age=0;rf_life=${e.eventLife>0?num(e.eventLife):'rf_life'};`;
      resetAction+=index>=0?`if rf_cmd[${index}]=0 {rf_cmd[${index}]=1;${lifeCode}}`:lifeCode;
    }
    action=`${groupGuard}{${resetAction}}`;
  }
  if(e.eventPreset==='orbit-bullets'||e.eventPreset==='group-orbit'){
    const isGroup=e.eventPreset==='group-orbit';
    const group=isGroup?eventGroupIndex(s,e):-1;
    if(isGroup&&group<0)return '';
    const selector=orbitSelectorCode(e,s),guard=[isGroup?'rf_scope_group='+group:'',selector].filter(Boolean).join(' && ');
    const groupGuard=guard?'if '+guard+' ':'';
    const cx=eventAssignment(e,'eventCenterX',s,'rf_orbit_cx');
    const cy=eventAssignment(e,'eventCenterY',s,'rf_orbit_cy');
    const spd=eventAssignment(e,'eventOrbitSpeed',s,'rf_orbit_spd');
    const rCode=e.eventRadiusMode==='fixed'?eventAssignment(e,'eventRadius',s,'rf_orbit_r'):'rf_orbit_r=point_distance(rf_orbit_cx,rf_orbit_cy,x,y);';
    let orbitAction=`rf_orbiting=1;${clearGravity}${cx}${cy}${spd}${rCode}rf_orbit_a=point_direction(rf_orbit_cx,rf_orbit_cy,x,y);rf_speed=0;rf_accel=0;rf_friction=0;rf_spin=0;rf_motion_reset=${e.eventDisableMotion?1:0};`;
    if(e.eventResetLife){
      const lifeCode=`rf_birth=rf_world;rf_age=0;rf_life=${e.eventLife>0?num(e.eventLife):'rf_life'};`;
      orbitAction+=index>=0?`if rf_cmd[${index}]=0 {rf_cmd[${index}]=1;${lifeCode}}`:lifeCode;
    }
    action=`${groupGuard}{${orbitAction}}`;
  }
  return action?`if (${active}) {${action}}`:'';
}
export function eventScripts(s,bundles=templateBundles(s)){if(!hasEvents(s))return [];const clears=clearAll(s),active=conditions(clears,'rf_now',s);
  const fixed=s.tracks.map((t,i)=>!t.templateOnly&&['rod','object'].includes(t.kind)?`if (${activeCode(t,'rf_now',s.tracks)}) rf_template_hide(${i},-1,noone);`:'').join('\n');
  const controlTracks=s.tracks.filter(t=>t.enabled!==false&&t.kind==='control');
  const resets=s.tracks.filter(t=>isEvent(t)&&['reset-motion','group-set','orbit-bullets','group-orbit'].includes(t.eventPreset)&&t.enabled!==false);

  // Materialize roots at their current pose; ordinary bullet events then own all
  // property evaluation, including source-specific random slots and life reset.
  const resetRods=resets.map(e=>{
    const group=e.eventPreset.startsWith('group-')?eventGroupIndex(s,e):-1;if(e.eventPreset.startsWith('group-')&&group<0)return '';
    return s.tracks.map((t,index)=>{
      if(t.templateOnly||t.kind!=='rod'||!orbitSourceMatches(e,t)||e.eventPreset.startsWith('group-')&&groupIndex(s,t)!==group)return '';
      const heading=t.releaseDirection==='fixed'?'rf_angle':t.releaseDirection==='aim'?'point_direction(rf_px,rf_py,rf_expression_player(0),rf_expression_player(1))':'point_direction(rf_x,rf_y,rf_px,rf_py)'+(t.releaseDirection==='tangent'?'+90':'');
      return `if (${activeCode(e,'rf_now',s.tracks)}) && rf_template_visible(${index},-1,noone) && (${activeCode(t,'rf_now',s.tracks)})${t.releaseFrame>=0?' && rf_now<'+num(t.releaseFrame):''} {${stateCode(t,'rf_now')}${shapeSetup(t,'rf_now')}rf_n=rf_count;${t.releaseKeepShape&&t.releaseDirection==='radial'?`rf_max_dist=0;for(rf_j=0;rf_j<rf_n;rf_j+=1){${shapePoint(t)}rf_d=point_distance(rf_x,rf_y,rf_px,rf_py);if rf_d>rf_max_dist rf_max_dist=rf_d;}`:''}for(rf_j=0;rf_j<rf_n;rf_j+=1){if !rf_template_visible(${index},rf_j,noone) continue;${shapePoint(t)}rf_color_sequence=${rodSequence(t)};rf_heading=${heading};${t.releaseKeepShape&&t.releaseDirection==='radial'?`rf_spd_out=rf_speed;if rf_max_dist>0.0001 rf_spd_out=rf_speed*(point_distance(rf_x,rf_y,rf_px,rf_py)/rf_max_dist);`:''}rf_inst=rf_pattern_spawn(${index},rf_now,rf_j,rf_n,0,1,rf_px,rf_py,rf_heading,${t.releaseKeepShape&&t.releaseDirection==='radial'?'rf_spd_out':'rf_speed'},0,0,0,noone,rf_x,rf_y);${releasedRainbow(t,'rf_now','rf_inst')}if instance_exists(rf_inst){rf_inst.rf_motion_reset=${e.eventDisableMotion?1:0};}${selectiveOrbit(e)?`rf_template_hide(${index},rf_j,noone);`:''}}${selectiveOrbit(e)?'':`rf_template_hide(${index},-1,noone);`}}`;
    }).filter(Boolean).join('\n');
  }).join('\n');
  // Find members by owner, not the root track's rf_tf row: group children and
  // nested/circular/grid copies can belong to many source rows. All writes in
  // nested with blocks are explicit self fields (rf_speed etc. are local vars).
  const resetTemplates=resets.map(e=>{
    const group=e.eventPreset.startsWith('group-')?eventGroupIndex(s,e):-1;if(e.eventPreset.startsWith('group-')&&group<0)return '';
    const selective=selectiveOrbit(e),selector=orbitSelectorCode(e,s,'self.rf_source');
    const memberGuard=[e.eventPreset.startsWith('group-')?'self.rf_scope_group='+group:'',selector].filter(Boolean).join(' && ');
    const life=s.tracks.map((t,i)=>['rod','object'].includes(t.kind)?`case ${i}:self.rf_life=${parameter(t,'life',160,null,{frame:'(rf_now-self.rf_clock_shift)',i:'self.rf_index',n:'self.rf_n',volley:'self.rf_volley'})};self.rf_rotate=${t.rotateSprite?1:0};${lifetimeSpawn(t,'self')}break;`:'').join('');
    return `if (${activeCode(e,'rf_now',s.tracks)}) {with(objRufTemplate) ${e.eventPreset.startsWith('group-')?'if rf_scope_group='+group+' ':''}{rf_tmpl_inst=id;rf_event_detached=0;with(objRufBullet) if self.rf_kinematic && self.rf_template_owner=rf_tmpl_inst${selective?' && ('+memberGuard+')':''} {self.rf_kinematic=0;self.rf_fixed=0;self.rf_system=1;self.rf_template_owner=noone;self.rf_behavior=self.rf_source;self.rf_motion_reset=${e.eventDisableMotion?1:0};self.rf_birth=rf_now;self.rf_age=0;self.rf_last_update=rf_now-1;self.rf_base_scale=self.rf_size;self.rf_base_alpha=self.rf_logic_alpha;self.rf_frozen_image=self.image_index;self.rf_image_frozen=1;self.rf_rainbow=0;self.image_speed=0;switch(self.rf_source){${life}}self.rf_sprite_angle=self.image_angle-self.rf_rotate*self.rf_dir;${selective?'rf_tmpl_inst.rf_tf[self.rf_source,self.rf_index]=noone;rf_template_hide(self.rf_source,self.rf_index,rf_tmpl_inst);':'rf_tmpl_inst.rf_event_detached=1;'}}${selective?'':'if rf_event_detached instance_destroy();'}}}`;
  }).join('\n');
  const template=`var rf_now,rf_clock;rf_now=argument0;rf_clock=rf_now-rf_shift;if (${active}) {switch(rf_template_source){${bundles.map(b=>`case ${b.rootIndex}:${b.tracks.map(t=>['rod','object'].includes(t.kind)?`if (${activeCode(t,'rf_clock',b.tracks)}) rf_template_hide(${s.tracks.findIndex(x=>x.id===t.id)},-1,id);`:'').join('\n')}break;`).join('\n')}}}return 0;`;
  const native=s.tracks.filter(t=>isEvent(t)&&!t.eventPreset.startsWith('group-')&&!t.eventPreset.startsWith('player-')&&t.enabled!==false&&nativeEventObjects(t).length).map(t=>{
    const ctx={x:'x',y:'y',frame:'rf_now',age:'0',direction:'direction',speed:'speed',i:'0',n:'1',volley:'0',sourceX:'x',sourceY:'y',randomOffset:'(id*9176)'};
    const action=t.eventPreset.startsWith('clear-')?'instance_destroy();':t.eventPreset==='stop-bullets'?'speed=0;':t.eventPreset==='orbit-bullets'?'speed=0;gravity=0;friction=0;':`speed=${eventValue(t,'controlValue',t,ctx)};direction=${eventValue(t,'controlFrom',t,ctx)};gravity=${eventValue(t,'eventAcceleration',t,ctx)};friction=${eventValue(t,'eventFriction',t,ctx)};gravity_direction=direction;`;
    return `if rf_now=objRufController.rf_frame && (${activeCode(t,'rf_now',s.tracks)}) {${nativeEventObjects(t).map(name=>`with(${name}) if object_index!=objRufBullet {${action}}`).join('\n')}}`;
  }).join('\n');
  const playerEvents=s.tracks.filter(t=>isEvent(t)&&['player-move','player-set'].includes(t.eventPreset)&&t.enabled!==false).map(e=>{
    const active=activeCode(e,'rf_now',s.tracks);
    if(e.eventPreset==='player-move'){
      const context={x:'x',y:'y',sourceX:'x',sourceY:'y',frame:'rf_now',age:`(rf_now-${num(e.start)})`,direction:'direction',speed:'speed',i:'0',n:'1',volley:'0',randomMix:false,randomOffset:'0'};
      const px=eventValue(e,'playerTargetX',e,context);
      const py=eventValue(e,'playerTargetY',e,context);
      const keepMotion=e.playerKeepMotion?'':'hspeed=0;vspeed=0;speed=0;gravity=0;';
      const resetJump=e.playerResetJump?'curJumps=1;if variable_local_exists("djump") djump=1;':'';
      return `if (${active}) {if instance_exists(player) with(player){x=${px};y=${py};${keepMotion}${resetJump}}}`;
    }
    if(e.eventPreset==='player-set'){
      const infJump=e.playerInfJump?'1':'0';
      const godmode=e.playerInvincible?'1':'0';
      const frozen=e.playerCanMove?'0':'1';
      const stopMotion=e.playerCanMove?'':'hspeed=0;vspeed=0;speed=0;gravity=0;';
      return `if (${active}) {global.infJump=${infJump};global.godmode=${godmode};global.frozen2=${frozen};global.frozen=${frozen};if instance_exists(player) with(player){infJump=${infJump};godmode=${godmode};if variable_local_exists("wudi") wudi=${godmode};${stopMotion}}}`;
    }
    return '';
  }).filter(Boolean).join('\n');
  const locals=`var rf_now,rf_world,rf_tmpl_inst,rf_j,rf_n,rf_inst,rf_heading,rf_px,rf_py,rf_len,rf_phase,rf_fraction,rf_seg,rf_mix,rf_color_sequence,rf_max_dist,rf_d,rf_spd_out,${fields.map(f=>'rf_'+f).join(',')};`;
  return [['rf_event_prepare',`${locals}rf_now=argument0;rf_world=rf_now;if (${active}) {${fixed}}${resetRods}${resetTemplates}${native}${playerEvents}return 0;`],['rf_event_template',template]];
}
