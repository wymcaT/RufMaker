import test from 'node:test';
import assert from 'node:assert/strict';
import {normalizeState,stateCode} from './motion.mjs';
import {normalizeBulletState,expandPatterns,advancedBullets} from './bullet-schema.mjs';
import {bulletSystemScripts} from './bullet-system.mjs';
import {evaluateExpression,expressionScripts} from './random-parameters.mjs';

const emitter=(id='e')=>({id,name:id,kind:'emitter',start:0,end:20,count:3,interval:10,speed:3,life:100,sprite:'sprBullet',keyframes:[]});
test('age actions generate separately from song commands and keep random scopes',()=>{
  const t=emitter();t.advancedBullets=true;t.bulletActions=[{type:'speed',start:0,duration:20,from:5,to:0},{type:'aim',start:30,duration:0},{type:'split',start:50,duration:0,count:5}];t.expressions={speed:'random_range(2,6)+i'};
  const command={id:'stop',kind:'control',start:60,end:70,targetSource:'e',targetParity:'even',controlAction:'speed',controlExpression:'speed+i*0',keyframes:[]};const s=normalizeState({version:2,fps:50,duration:200,tracks:[t,command]});assert.ok(advancedBullets(s));const scripts=Object.fromEntries(bulletSystemScripts(s));assert.match(scripts.rf_pattern_tick,/rf_age-30-rf_index\*0/);assert.match(scripts.rf_pattern_tick,/rf_world>=60 && rf_world<=70/);assert.match(scripts.rf_pattern_tick,/rf_index mod 2=0/);assert.match(scripts.rf_pattern_spawn,/rf_wave\*1009\+rf_idx\*9176/);assert.match(scripts.rf_pattern_tick,/rf_split_template\[rf_split\]/);assert.doesNotMatch(scripts.rf_pattern_tick,/other\.rf_world/);assert.match(scripts.rf_pattern_tick,/sprite_index=-1;if rf_collision/);
  const expressions=expressionScripts(s,['player']);assert.ok(expressions.some(([name,code])=>code.includes('argument12')));assert.equal(evaluateExpression('i+n+u+volley',1,'slot',{i:2,n:5,volley:3}),10.5);
});
test('looped clips keep internal references and scale local action time',()=>{
  const child=emitter();child.parentId='g';child.bulletActions=[{type:'speed',start:10,duration:20,from:2,to:4}];const clip={id:'clip',kind:'pattern',start:100,end:300,repeatCount:2,repeatGap:5,timeScale:2,children:[{id:'g',kind:'group',start:0,end:20,keyframes:[]},child],keyframes:[]};const s=normalizeState({version:2,fps:50,duration:400,tracks:[clip]});const expanded=normalizeState(expandPatterns(s));assert.equal(expanded.tracks.length,4);const bullets=expanded.tracks.filter(t=>t.kind==='emitter');assert.equal(bullets[0].start,100);assert.equal(bullets[1].start,116);assert.equal(bullets[0].bulletActions[0].start,5);assert.equal(bullets[0].bulletActions[0].to,8);assert.ok(expanded.tracks.some(t=>t.id===bullets[0].parentId));assert.match(stateCode(bullets[0],'rf_frame'),/rf_group_pose\(/);assert.equal(s.tracks[0].children[1].bulletActions[0].start,10,'Source clip remains editable');
});
test('invalid behavior values and cyclic parenting report specific errors',()=>{
  assert.throws(()=>normalizeBulletState({tracks:[{...emitter(),bulletActions:[{type:'split',count:301}]}]}),/行为参数/);assert.throws(()=>normalizeBulletState({tracks:[{id:'a',kind:'group',parentId:'b'},{id:'b',kind:'group',parentId:'a'}]}),/循环引用/);
});
