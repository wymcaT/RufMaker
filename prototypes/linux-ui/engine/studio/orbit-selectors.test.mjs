import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import {normalizeState} from './motion.mjs';
import {expandPatterns} from './bullet-schema.mjs';
import {eventTick,eventScripts} from './event-presets.mjs';
import {templateScripts} from './template-system.mjs';
const scene=(tracks,timelineGroups=[])=>normalizeState({version:2,preserveTrackOrder:true,fps:50,duration:30,tracks,timelineGroups});
const track=(id,kind,extra={})=>({id,name:id,kind,start:0,end:10,keyframes:[],...extra});
const event=(extra={})=>track('orbit','control',{start:2,end:2,eventPreset:'group-orbit',eventGroup:'family',targetTag:'selected',eventResetLife:false,...extra});
const groups=[{id:'family',name:'Family',color:'#ffffff',lanes:[0]},{id:'other',name:'Other',color:'#ffffff',lanes:[9]}];
const predicate=(guard,locals)=>Function(...Object.keys(locals),'return '+guard.replace(/(?<![<>=!])=(?!=)/g,'==='))(...Object.values(locals));
export function tickMatches(e,s,source,scope){
  const tick=eventTick(e,s),match=/\{if ([\s\S]*?) \{rf_orbiting=1;/.exec(tick);
  if(!tick)return false;
  return match?!!predicate(match[1],{rf_source:source,rf_scope_group:scope}):true;
}
function bodyAt(text,start){let depth=0;for(let i=start;i<text.length;i++){if(text[i]==='{')depth++;if(text[i]==='}'&&--depth===0)return text.slice(start+1,i);}throw Error('Unbalanced generated body');}
function memberBlock(prep){const marker='with(objRufBullet) if ',start=prep.indexOf(marker);assert.ok(start>=0);const open=prep.indexOf('{',start);return {guard:prep.slice(start+marker.length,open).trim(),body:bodyAt(prep,open)};}
function mixed(e=event()){
  return scene([track('seed','group',{lane:0,start:0,end:0,spawnActions:[{type:'spawn',templateId:'bundle'}]}),track('bundle','group',{templateOnly:true}),track('root','object',{parentId:'bundle',tag:'selected',lane:9}),track('fast','rod',{parentId:'bundle',tag:'selected',count:2}),track('slow','rod',{parentId:'bundle',tag:'other',count:2}),track('object','object',{parentId:'bundle',tag:'other'}),track('stream','emitter',{parentId:'bundle',tag:'stream'}),track('future','object',{parentId:'bundle',tag:'selected',start:4}),e],groups);
}
test('orbit event guards intersect inherited group, source and tag, including clipped source identities',()=>{
  const s=scene([track('fast','emitter',{tag:'fast',clipSourceId:'original-fast'}),track('slow','emitter',{tag:'slow'}),event()],groups);
  for(const key of ['orbit-bullets','group-orbit'])for(const [source,tag,want] of [['fast','fast',[true,false]],['','fast',[true,false]],['fast','',[true,false]],['','',[true,true]],['fast','slow',[false,false]],['missing','',[false,false]],['original-fast','fast',[true,false]]]){
    const e={...s.tracks[2],eventPreset:key,targetSource:source,targetTag:tag,targetMinAge:999,targetMaxAge:1000,targetVolley:77,targetParity:'odd',includeFuture:false};
    for(let i=0;i<2;i++){assert.equal(tickMatches(e,s,i,0),want[i]);assert.equal(tickMatches(e,s,i,1),key==='group-orbit'?false:want[i]);}
    const tick=eventTick(e,s);assert.doesNotMatch(tick,/rf_volley=77|rf_age>=999|rf_index mod/);
  }
});
test('selectors gate root materialization and member detachment before any destructive mutation',()=>{
  const e=event({targetSource:'fast',targetTag:'selected'}),s=mixed(e);s.tracks[3].templateOnly=false;s.tracks[3].parentId='';s.tracks[3].lane=0;s.tracks[4].parentId='';s.tracks[4].lane=0;
  const prep=new Map(eventScripts(s)).get('rf_event_prepare');
  assert.match(prep,/rf_pattern_spawn\(3,rf_now/);assert.doesNotMatch(prep,/rf_pattern_spawn\(4,rf_now/);
  assert.match(prep,/rf_template_hide\(3,rf_j,noone\)/);assert.doesNotMatch(prep.slice(prep.indexOf('if (rf_now>=2')),/rf_template_hide\(3,-1,noone\)/);
  const block=memberBlock(prep);assert.match(block.guard,/self\.rf_scope_group=0/);assert.match(block.guard,/self\.rf_source=3/);
  assert.ok(prep.indexOf(block.guard)<prep.indexOf('self.rf_kinematic=0;'));
  assert.match(block.body,/rf_tmpl_inst\.rf_tf\[self\.rf_source,self\.rf_index\]=noone;rf_template_hide\(self\.rf_source,self\.rf_index,rf_tmpl_inst\);/);
  assert.doesNotMatch(prep.slice(prep.indexOf('with(objRufTemplate)')),/instance_destroy\(\)/,'selected orbit must not destroy the owner');
});
test('execute generated member predicate and release body: mixed owners and repeated prepare retain unaffected fixed slots',()=>{
  const s=mixed(),prep=new Map(eventScripts(s)).get('rf_event_prepare'),{guard,body}=memberBlock(prep);
  // Execute the actual generated release body. Only GML 2D array syntax is
  // adapted here; this is a focused script probe, not a GameMaker runner.
  const release=Function('self','rf_tmpl_inst','rf_now','noone','rf_template_hide',body.replace('rf_tf[self.rf_source,self.rf_index]','rf_tf[self.rf_source][self.rf_index]'));
  const owner={rf_tf:Array.from({length:s.tracks.length},()=>[])},hidden=new Set(),members=[];
  for(const [row,n] of [[2,1],[3,2],[4,2],[5,1]])for(let index=0;index<n;index++){
    const b={rf_kinematic:1,rf_fixed:1,rf_template_owner:owner,rf_source:row,rf_scope_group:0,rf_index:index,rf_size:1,rf_logic_alpha:1,image_index:2,image_angle:10,rf_dir:0};members.push(b);owner.rf_tf[row][index]=b;
  }
  const outside={...members[0],rf_scope_group:1};members.push(outside);
  const hide=(row,index,target)=>{assert.equal(target,owner);hidden.add(row+'|'+index);};
  const prepare=()=>{for(const self of members)if(predicate(guard,{self,rf_tmpl_inst:owner}))release(self,owner,2,null,hide);};
  prepare();const once=JSON.stringify(members.map(b=>({source:b.rf_source,kinematic:b.rf_kinematic,birth:b.rf_birth,owner:b.rf_template_owner===owner})));
  prepare();assert.equal(JSON.stringify(members.map(b=>({source:b.rf_source,kinematic:b.rf_kinematic,birth:b.rf_birth,owner:b.rf_template_owner===owner}))),once);
  for(const b of members){const selected=b.rf_scope_group===0&&[2,3].includes(b.rf_source);assert.equal(b.rf_kinematic,selected?0:1);if(b!==outside){assert.equal(owner.rf_tf[b.rf_source][b.rf_index],selected?null:b);assert.equal(hidden.has(b.rf_source+'|'+b.rf_index),selected);}}
  assert.deepEqual([...hidden],['2|0','3|0','3|1']);assert.ok(!hidden.has('7|0'),'future matching row must not be hidden before it exists');
  const template=new Map(templateScripts(s)).get('rf_template_instance_step');assert.match(template,/!rf_template_visible\(2,rf_j,id\)/,'object slot must honor its index hide');assert.match(template,/rf_template_visible\(6,-1,id\)/,'emitter row retains row visibility');
});
test('blank selectors retain legacy owner disposal, non-orbit selectors and native object lists remain independent',()=>{
  const selected=mixed(event({eventPreset:'orbit-bullets',eventNativeObjects:'objNativeBullet'}));
  const defaults=structuredClone(selected);defaults.tracks.at(-1).targetTag='';
  const selectedPrep=new Map(eventScripts(selected)).get('rf_event_prepare'),defaultPrep=new Map(eventScripts(defaults)).get('rf_event_prepare');
  assert.match(defaultPrep,/if rf_event_detached instance_destroy\(\);/);assert.doesNotMatch(selectedPrep,/if rf_event_detached instance_destroy/);
  const native=s=>s.slice(s.indexOf('if rf_now=objRufController.rf_frame'),s.lastIndexOf('return 0;'));
  assert.equal(native(selectedPrep),native(defaultPrep));assert.match(native(selectedPrep),/with\(objNativeBullet\) if object_index!=objRufBullet \{speed=0;gravity=0;friction=0;\}/);
  const group=mixed(event({eventNativeObjects:'objNativeBullet'}));assert.doesNotMatch(new Map(eventScripts(group)).get('rf_event_prepare'),/with\(objNativeBullet\)/);
  for(const key of ['reset-motion','group-set']){const s=mixed(event({eventPreset:key,targetSource:'missing',targetTag:'missing'})),prep=new Map(eventScripts(s)).get('rf_event_prepare');assert.match(prep,/if rf_event_detached instance_destroy\(\);/);assert.doesNotMatch(memberBlock(prep).guard,/rf_source/);}
});
test('normalization, clip copies and event-local references preserve existing selector fields',()=>{
  const s=scene([track('clip','pattern',{start:0,end:20,repeatCount:2,repeatGap:2,children:[track('fast','emitter',{start:0,end:0,tag:'fast'}),event({eventPreset:'orbit-bullets',targetSource:'fast',targetTag:'fast'})]})]);
  const expanded=expandPatterns(s),sources=expanded.tracks.filter(t=>t.kind==='emitter'),events=expanded.tracks.filter(t=>t.kind==='control');
  assert.equal(events.length,2);for(let i=0;i<2;i++){assert.equal(events[i].targetSource,sources[i].id);assert.equal(events[i].targetTag,'fast');for(let j=0;j<2;j++)assert.equal(tickMatches(events[i],expanded,expanded.tracks.indexOf(sources[j]),-1),i===j);}
  const old=scene([event({targetTag:undefined,targetSource:undefined})]).tracks[0];assert.equal(old.targetSource,'');assert.equal(old.targetTag,'');
});
test('UI exposes only the two existing orbit selectors and documents independent native lists',()=>{
  const code=fs.readFileSync(new URL('../desktop/EventPresets.cs',import.meta.url),'utf8');const helper=code.slice(code.indexOf('private void OrbitTargetFields'),code.indexOf('private void EventFields'));
  assert.match(code,/if\(EventPresets\.IsOrbit\(t\)\)OrbitTargetFields\(bag,t\);\s*if\(EventPresets\.GroupEvent/);assert.match(helper,/来源物件/);assert.match(helper,/标签（留空不限）/);assert.match(helper,/new\[\]\{"emitter","rod","object"\}/);assert.doesNotMatch(helper,/t\.targetMinAge|t\.targetMaxAge|t\.targetVolley|t\.targetParity|t\.includeFuture/);assert.match(helper,/独立名单/);
});
