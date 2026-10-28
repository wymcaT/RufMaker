import test from 'node:test';import assert from 'node:assert/strict';
import {normalizeState} from './motion.mjs';import {expandPatterns} from './bullet-schema.mjs';import {bulletSystemScripts} from './bullet-system.mjs';import {templateScripts} from './template-system.mjs';import {eventKeys,nativeEventObjects} from './event-presets.mjs';
const track=(id,kind='control',extra={})=>({id,name:id,kind,start:10,end:10,keyframes:[],...extra});
const scene=tracks=>normalizeState({version:2,fps:50,duration:300,tracks});
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
  const e=track('reset','control',{eventPreset:'reset-motion',controlValue:6,controlFrom:90,eventAcceleration:2,eventTurn:4});const s=scene([track('clip','pattern',{start:50,end:100,timeScale:2,repeatCount:1,children:[e]})]);const t=expandPatterns(s).tracks.find(t=>t.eventPreset);assert.equal(t.controlValue,12);assert.equal(t.controlFrom,90);assert.equal(t.eventAcceleration,8);assert.equal(t.eventTurn,8);
});
