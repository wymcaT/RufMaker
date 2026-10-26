import {gmlNumber as num} from './gml.mjs';
import {activeCode} from './bullet-schema.mjs';

export const eventKeys=['clear-bullets','clear-danmaku','stop-bullets','reset-motion'];
export const isEvent=t=>t.kind==='control'&&eventKeys.includes(t.eventPreset);
export const nativeEventObjects=t=>String(t.eventNativeObjects||'').split(/[,，;；\r\n]/).map(s=>s.trim()).filter(Boolean).filter((s,i,a)=>a.indexOf(s)===i);
export const hasEvents=s=>s.tracks.some(isEvent);
const clearAll=s=>s.tracks.filter(t=>isEvent(t)&&t.eventPreset==='clear-danmaku'&&t.enabled!==false);
const conditions=(tracks,time,s)=>tracks.map(t=>'('+activeCode(t,time,s.tracks)+')').join(' || ')||'0';
export function eventEarly(s){const events=s.tracks.filter(t=>isEvent(t)&&['clear-bullets','clear-danmaku'].includes(t.eventPreset)&&t.enabled!==false);return events.length?`if !rf_kinematic && rf_birth<=rf_world && (${conditions(events,'rf_world',s)}) {instance_destroy();return 0;}`:'';}
export function eventTick(e,s){const active=activeCode(e,'rf_world',s.tracks);let action='';
  if(e.eventPreset==='stop-bullets')action='rf_speed=0;rf_stop_speed=1;';
  if(e.eventPreset==='reset-motion')action=`rf_stop_speed=0;rf_speed=${e.eventRestoreBirth?'rf_base_speed':num(e.controlValue)};rf_dir=${e.eventRestoreBirth?'rf_base_dir':num(e.controlFrom)};rf_accel=${num(e.eventAcceleration)};rf_spin=${num(e.eventTurn)};rf_motion_reset=${e.eventDisableMotion?1:0};`;
  return action?`if (${active}) {${action}}`:'';
}
export function eventScripts(s,bundles){if(!hasEvents(s))return [];const clears=clearAll(s),active=conditions(clears,'rf_now',s);
  const fixed=s.tracks.map((t,i)=>!t.templateOnly&&['rod','object'].includes(t.kind)?`if (${activeCode(t,'rf_now',s.tracks)}) rf_template_hide(${i},-1,noone);`:'').join('\n');
  const template=`var rf_now,rf_clock;rf_now=argument0;rf_clock=rf_now-rf_shift;if (${active}) {switch(rf_template_source){${bundles.map(b=>`case ${b.rootIndex}:${b.tracks.map(t=>['rod','object'].includes(t.kind)?`if (${activeCode(t,'rf_clock',b.tracks)}) rf_template_hide(${s.tracks.findIndex(x=>x.id===t.id)},-1,id);`:'').join('\n')}break;`).join('\n')}}}return 0;`;
  const native=s.tracks.filter(t=>isEvent(t)&&t.enabled!==false&&nativeEventObjects(t).length).map(t=>{
    const action=t.eventPreset.startsWith('clear-')?'instance_destroy();':t.eventPreset==='stop-bullets'?'speed=0;':`speed=${num(t.controlValue)};direction=${num(t.controlFrom)};gravity=${num(t.eventAcceleration)};friction=0;gravity_direction=direction;`;
    return `if rf_now=objRufController.rf_frame && (${activeCode(t,'rf_now',s.tracks)}) {${nativeEventObjects(t).map(name=>`with(${name}) if object_index!=objRufBullet {${action}}`).join('\n')}}`;
  }).join('\n');
  return [['rf_event_prepare',`var rf_now;rf_now=argument0;if (${active}) {${fixed}}${native}return 0;`],['rf_event_template',template]];
}
