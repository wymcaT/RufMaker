import test from 'node:test';import assert from 'node:assert/strict';
import {normalizeState,valueAt} from './motion.mjs';import {globalsAt,globalScripts} from './global-variables.mjs';import {expandPatterns} from './bullet-schema.mjs';import {expressionScripts,evaluateExpression} from './random-parameters.mjs';
const track=(value={})=>({id:'t',kind:'variable',start:5,end:80,keyframes:[],...value});
test('global assignments retain defaults, sample intervals once and reconstruct prior history',()=>{
 const state=normalizeState({version:2,fps:50,duration:100,randomSeed:991,tracks:[track({id:'phase',variableName:'phase',variableExpression:'choose(0,90,180,270)',variableDefault:7,variableRepeat:10}),track({id:'count',start:2,variableName:'count',variableDefault:2,variableExpression:'global.count+floor(random_range(1,2))',variableRepeat:3}),track({id:'dist',start:3,end:3,variableName:'dist',variableExpression:'point_distance(0,0,3,4)+global.phase'}),track({id:'e',kind:'emitter',start:0,end:100,expressions:{angle:'global.phase',speed:'global.count'}})]});
 assert.equal(globalsAt(state,0).phase,7);assert.equal(globalsAt(state,8).count,5);assert.equal(globalsAt(state,3).dist,12);const expected=globalsAt(state,65);globalsAt(state,2);assert.deepEqual(globalsAt(state,65),expected);assert.equal(valueAt(state.tracks[3],'speed',5),4);
 const scripts=new Map(expressionScripts(state,['player']));assert([...scripts.values()].some(code=>code.includes('global.count')));const globals=new Map(globalScripts(state));assert.match(globals.get('rf_global_init'),/global.phase=7/);assert.match(globals.get('rf_global_step'),/mod 3=0/);
});
test('global expressions validate names, undefined references and ordinary syntax',()=>{
 assert.throws(()=>normalizeState({version:2,duration:100,tracks:[track({variableName:'2bad'})]}),/变量名/);
 assert.throws(()=>normalizeState({version:2,duration:100,tracks:[track({variableExpression:'global.missing+1'})]}),/未定义/);
 assert.throws(()=>evaluateExpression('global.not_defined',1,'slot'),/未定义/);
 assert.equal(evaluateExpression('floor(global.a)+choose(0)',1,'slot',{globals:{a:3.9}}),3);
});
test('pattern-local emitters and assignments resolve project globals after loop expansion',()=>{
 const state=normalizeState({version:2,fps:50,duration:300,randomSeed:10,tracks:[track({id:'global',start:0,end:0,variableName:'base',variableExpression:'choose(3,6,9)'}),track({id:'clip',kind:'pattern',start:50,end:140,repeatCount:2,timeScale:2,children:[track({id:'local',start:0,end:20,variableName:'loop_value',variableExpression:'global.base+frame',variableRepeat:10}),track({id:'emit',kind:'emitter',start:0,end:20,expressions:{speed:'global.loop_value'}})]})]});
 const flat=normalizeState(expandPatterns(state));const at=globalsAt(flat,50);assert.equal(at.loop_value,at.base+50);assert.equal(valueAt(flat.tracks.find(t=>t.id==='clip/0/emit'),'speed',50),at.loop_value);assert.equal(flat.tracks.find(t=>t.id==='clip/0/local').variableRepeat,5);
});
