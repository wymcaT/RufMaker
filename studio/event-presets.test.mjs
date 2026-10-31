import test from 'node:test';import assert from 'node:assert/strict';
import {normalizeState} from './motion.mjs';import {expandPatterns} from './bullet-schema.mjs';import {bulletSystemScripts} from './bullet-system.mjs';import {templateScripts} from './template-system.mjs';import {eventKeys,nativeEventObjects} from './event-presets.mjs';
const track=(id,kind='control',extra={})=>({id,name:id,kind,start:10,end:10,keyframes:[],...extra});
const scene=(tracks,timelineGroups=[])=>normalizeState({version:2,fps:50,duration:300,timelineGroups,tracks});
test('events normalize defaults without changing legacy controls; native names are plain object identifiers',()=>{
  const s=scene([track('old'),track('new','control',{eventPreset:'reset-motion',eventNativeObjects:'objBullet, 子弹;'})].slice(0,1));assert.equal(s.tracks[0].eventPreset,'');assert.equal(s.tracks[0].controlAction,'speed');
  const e=scene([track('event','control',{eventPreset:'reset-motion'})]).tracks[0];assert.equal(e.eventRestoreBirth,true);assert.equal(e.controlAction,'event');
  assert.deepEqual(nativeEventObjects({...e,eventNativeObjects:'objBullet, objCherry;objBullet'}),['objBullet','objCherry']);
  for(const extra of [{eventPreset:'unknown'},{eventPreset:'reset-motion',eventRestoreBirth:'false'},{eventPreset:'clear-bullets',eventNativeObjects:'objBullet);game_end()'}])assert.throws(()=>scene([track('invalid','control',extra)]));
});
test('all event scripts have balanced blocks; clear and reset cover templates, native objects and motion guards',()=>{
  const s=scene([track('e','emitter',{start:0,end:90,flightMode:'aim',bulletActions:[{type:'speed',start:20,duration:20,from:6,to:6}]}),track('f','rod',{start:0,end:90}),track('template','rod',{templateOnly:true,start:0,end:30}),...eventKeys.map((key,i)=>track('event'+i,'control',{start:i*10,end:i*10,eventPreset:key,eventNativeObjects:'objNativeBullet'}))]);
  s.tracks[0].spawnActions.push({type:'spawn',templateId:'template'});const normalized=scene(s.tracks);const scripts=[...bulletSystemScripts(normalized),...templateScripts(normalized)];
  for(const [name,body] of scripts){const code=body.replace(/'[^']*'|"[^"]*"/g,'');let balance=0;for(const c of code){if(c==='{')balance++;if(c==='}')balance--;assert.ok(balance>=0,name+' closes too early');}assert.equal(balance,0,name+' misses a closing block');}
  const map=new Map(scripts);assert.match(map.get('rf_event_prepare'),/with\(objNativeBullet\)/);assert.equal((map.get('rf_event_prepare').match(/with\(objNativeBullet\)/g)||[]).length,4,'group events must not accidentally execute stale native-object settings');assert.match(map.get('rf_event_prepare'),/rf_template_hide/);assert.match(map.get('rf_template_instance_step'),/rf_event_template/);assert.match(map.get('rf_pattern_tick'),/!rf_motion_reset && rf_age/);assert.match(map.get('rf_pattern_tick'),/rf_stop_speed=1/);assert.match(map.get('rf_pattern_tick'),/if !rf_stop_speed rf_speed\+=rf_accel/);
});
test('event reset units follow nested clip time scaling without rotating direction units',()=>{
  const e=track('reset','control',{eventPreset:'reset-motion',controlValue:6,controlFrom:90,eventAcceleration:2,eventTurn:4,eventLife:100});const s=scene([track('clip','pattern',{start:50,end:100,timeScale:2,repeatCount:1,children:[e]})]);const t=expandPatterns(s).tracks.find(t=>t.eventPreset);assert.equal(t.controlValue,12);assert.equal(t.controlFrom,90);assert.equal(t.eventAcceleration,8);assert.equal(t.eventTurn,8);assert.equal(t.eventLife,50);
});
test('reset-motion event disintegrates composite objects, destroys rod container and resets bullet life and kinematics',()=>{
  const rod=track('my-rod','rod',{start:0,end:100,shape:'polygon',sides:5,count:5,releaseFrame:-1});
  const reset=track('my-reset','control',{start:25,end:25,eventPreset:'reset-motion',eventRestoreBirth:false,controlValue:5,controlFrom:60,eventAcceleration:0.2,eventTurn:1.5,eventResetLife:true,eventLife:120});
  const s=scene([rod,reset]);const scripts=new Map(bulletSystemScripts(s));
  const prep=scripts.get('rf_event_prepare');assert.ok(prep,'rf_event_prepare script must be generated');
  assert.match(prep,/rf_pattern_spawn\(0,rf_now,rf_j,rf_n,0,1,rf_px,rf_py,rf_heading,rf_speed/);
  assert.match(scripts.get('rf_pattern_tick'),/rf_accel=0\.2/);
  assert.match(scripts.get('rf_pattern_tick'),/rf_spin=1\.5/);
  assert.match(scripts.get('rf_pattern_tick'),/rf_birth=rf_world;rf_age=0;rf_life=120;/);
  assert.match(prep,/rf_template_hide\(0,-1,noone\);/);
  assert.match(prep,/with\(objRufTemplate\)/);
  assert.match(prep,/with\(objRufBullet\) if self\.rf_kinematic && self\.rf_template_owner=rf_tmpl_inst/);assert.doesNotMatch(prep,/rf_tc\[rf_template_source\]/);assert.match(prep,/self\.rf_last_update=rf_now-1/);
  const tick=scripts.get('rf_pattern_tick');assert.ok(tick,'rf_pattern_tick script must be generated');
  assert.match(tick,/rf_birth=rf_world;rf_age=0;rf_life=120;/);
});
test('group-set event supports complete motion and life reset and disintegrates group templates',()=>{
  const g={id:'grp1',name:'Group 1',color:'#ff0000'};
  const groupReset=track('grp-reset','control',{start:40,end:40,eventPreset:'group-set',eventGroup:'grp1',eventRestoreBirth:false,controlValue:7,controlFrom:120,eventAcceleration:0.3,eventTurn:-2,eventResetLife:true,eventLife:90});
  const s=scene([groupReset],[g]);
  const scripts=new Map(bulletSystemScripts(s));
  const prep=scripts.get('rf_event_prepare');
  assert.match(prep,/with\(objRufTemplate\) if rf_scope_group=0/);
  assert.match(scripts.get('rf_pattern_tick'),/rf_speed=7/);
  assert.match(scripts.get('rf_pattern_tick'),/rf_dir=120/);
  assert.match(scripts.get('rf_pattern_tick'),/rf_accel=0\.3/);
  assert.match(scripts.get('rf_pattern_tick'),/rf_spin=-2/);
  assert.match(scripts.get('rf_pattern_tick'),/rf_life=90/);
  const tick=scripts.get('rf_pattern_tick');
  assert.match(tick,/if rf_scope_group=0 \{rf_stop_speed=0;rf_speed=7;rf_dir=120;rf_accel=0\.3;rf_spin=-2;rf_motion_reset=1;if rf_cmd\[0\]=0 \{rf_cmd\[0\]=1;rf_birth=rf_world;rf_age=0;rf_life=90;\}\}/);
});
