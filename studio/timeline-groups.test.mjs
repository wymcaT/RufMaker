import test from 'node:test';import assert from 'node:assert/strict';
import {normalizeState} from './motion.mjs';import {expandPatterns} from './bullet-schema.mjs';
import {groupId,groupIndex,eventGroupIndex} from './timeline-groups.mjs';
import {bulletSystemScripts} from './bullet-system.mjs';import {templateScripts} from './template-system.mjs';import {expressionScripts} from './random-parameters.mjs';
const item=(id,kind,lane,extra={})=>({id,name:id,kind,lane,start:0,end:30,keyframes:[],...extra});
const groups=[{id:'A',name:'用户组',color:'#5887B8',lanes:[2,4]},{id:'B',name:'B',color:'#9B73B0',lanes:[3]}];
const scene=tracks=>normalizeState({version:2,fps:50,duration:300,timelineGroups:structuredClone(groups),tracks});
test('groups belong to lanes, have stable IDs, and survive raw JSON independently of legacy transform parents',()=>{
  const s=scene([item('a','emitter',2),item('second','rod',2,{start:40,end:60}),item('b','rod',3)]);
  assert.equal(groupId(s,s.tracks[0]),'A');assert.equal(groupId(s,s.tracks[1]),'A');assert.equal(groupIndex(s,s.tracks[2]),1);
  const round=normalizeState(JSON.parse(JSON.stringify(s)));assert.deepEqual(round.timelineGroups,s.timelineGroups);assert.equal(round.timelineGroups[0].name,'用户组');assert.ok(s.tracks.every(t=>!t.parentId));
  assert.equal(eventGroupIndex(s,{lane:4,eventGroup:''}),0);assert.equal(eventGroupIndex(s,{lane:4,eventGroup:'B'}),1);assert.equal(eventGroupIndex(s,{lane:4,eventGroup:'deleted'}),-1);
});
test('nested clip members and their group events inherit the outer track scope',()=>{
  const nested=item('inner','pattern',0,{children:[item('shot','emitter',0),item('event','control',1,{eventPreset:'group-copy'})]});
  const s=scene([item('outer','pattern',2,{children:[nested]})]),expanded=expandPatterns(s);
  assert.equal(expanded.tracks.length,2);assert.ok(expanded.tracks.every(t=>groupId(expanded,t)==='A'));
});
test('group operations generate complete scripts with per-member expression contexts and scoped descendant membership',()=>{
  const s=scene([item('shot','emitter',2,{bulletActions:[{type:'speed',expressions:{from:'choose(1,2)',to:'random_range(3,6)'}}]}),item('ring','rod',3),item('template','rod',0,{templateOnly:true}),...['group-copy','group-clear','group-set','reset-motion'].map((key,i)=>item('e'+i,'control',4,{eventPreset:key,start:5+i,end:5+i,eventRestoreBirth:false,expressions:{controlValue:'random_range(1,4)',controlFrom:'choose(0,90,180)'}}))]);
  s.tracks[0].spawnActions.push({type:'spawn',templateId:'template',expressions:{spawnAngle:'choose(0,45,90)'}});normalizeState(s);
  const scripts=[...bulletSystemScripts(s),...templateScripts(s),...expressionScripts(s,[])],map=new Map(scripts);
  assert.match(map.get('rf_event_group_step'),/ds_list_create/);assert.match(map.get('rf_event_group_step'),/ds_list_destroy/);assert.match(map.get('rf_event_group_step'),/rf_scope_group=0/);assert.match(map.get('rf_event_clone'),/rf_behavior=-1/);assert.match(map.get('rf_template_spawn'),/rf_scope_group=global.rf_spawn_group/);assert.match(map.get('rf_pattern_spawn'),/rf_scope_group=rf_spawn_owner.rf_scope_group/);assert.match(map.get('rf_pattern_tick'),/rf_random_mix/);
  for(const [name,body] of scripts){let count=0;for(const c of body.replace(/'[^']*'|"[^"]*"/g,'')){if(c==='{')count++;if(c==='}')count--;assert.ok(count>=0,name);}assert.equal(count,0,name);}
});

test('copy replacement is opt-in and each lifetime mode uses the shared runtime with birth-boundary checks',()=>{
  const old=scene([item('shot','emitter',2),item('copy','control',4,{eventPreset:'group-copy'})]);assert.equal(old.tracks[1].eventCopyDestroyOriginal,false);
  for(const [mode,index] of [['fixed',0],['room',1],['view',2]]){
    const s=scene([item('shot','emitter',2),item('rod','rod',2),item('copy','control',4,{eventPreset:'group-copy',eventCopyDestroyOriginal:true,lifetimeMode:mode,despawnMargin:12})]);const map=new Map(bulletSystemScripts(s));
    assert.ok(map.get('rf_event_clone').includes('rf_new.rf_life_mode='+index+';'));assert.match(map.get('rf_event_clone'),/rf_new.rf_die_margin=12/);assert.match(map.get('rf_event_clone'),/with\(rf_new\)if rf_lifetime_enabled rf_lifetime_tick\(\)/);assert.match(map.get('rf_event_clone'),/if !argument2.*rf_template_hide.*instance_destroy/s);assert.match(map.get('rf_event_group_step'),/rf_template_hide\(1,rf_j,noone\)/);
  }
  assert.throws(()=>scene([item('bad','control',4,{eventPreset:'group-copy',eventCopyDestroyOriginal:'false'})]));
});
