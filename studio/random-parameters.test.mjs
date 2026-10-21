import test from 'node:test';
import assert from 'node:assert/strict';
import {evaluateExpression,resolveRandomState,randomInitialization,parameterCode} from './random-parameters.mjs';
import {normalizeState,stateCode,valueAt} from './motion.mjs';

test('random expressions accept arithmetic, nested calls and Chinese punctuation',()=>{
  assert.equal(evaluateExpression('choose(3，3，3）',50,'x'),3);
  assert.equal(evaluateExpression('2 + 3 * 4',1,'x'),14);
  assert.equal(evaluateExpression('-(3+4) / 2',1,'x'),-3.5);
  for(const text of ['random(100)','random_range(-20,50)','choose(random(100), random_range(200,300), -2)','irandom(10)','irandom_range(-3,8)']){
    const a=evaluateExpression(text,12345,'object|x|base');assert.equal(a,evaluateExpression(text,12345,'object|x|base'));assert.ok(Number.isFinite(a));
  }
  for(const text of ['random_range(1)','choose()','global.x','random(10);game_end()','1/0','random(20)+'])assert.throws(()=>evaluateExpression(text,2,'x'));
});
test('seeking, saving and moving keys keep samples; changing seed rerolls',()=>{
  const s=normalizeState({version:2,fps:50,duration:500,randomSeed:1357,tracks:[{id:'one',kind:'object',start:0,end:500,expressions:{x:'random_range(0,800)'},keyframes:[{property:'alpha',frame:0,expression:'random_range(.1,.9)',randomId:'key-a'},{property:'alpha',frame:500,value:1}]}]});
  const t=s.tracks[0],x=t.x,alpha=t.keyframes[0].value;for(let i=0;i<=500;i++)assert.equal(valueAt(t,'x',i),x);
  resolveRandomState(s);assert.equal(t.x,x);t.keyframes[0].frame=20;resolveRandomState(s);assert.equal(t.keyframes[0].value,alpha);
  s.randomSeed++;resolveRandomState(s);assert.notEqual(t.x,x);assert.notEqual(t.keyframes[0].value,alpha);
  const roundTrip=normalizeState(JSON.parse(JSON.stringify(s)));assert.deepEqual(roundTrip,s);
  assert.deepEqual(s.tracks.map(t=>t.lane),[0]);
});
test('game initializes samples once, uses independent RNG and preserves seed modes',()=>{
  const s=normalizeState({version:2,randomSeed:2468,tracks:[{id:'a',kind:'object',start:0,end:200,expressions:{x:'random_range(random(20), choose(100,200))'},keyframes:[]}]});
  const t=s.tracks[0],code=randomInitialization(s),reference=parameterCode(t,'x');assert.ok(code.includes('rf_random_seed=2468;'));assert.ok(code.includes(reference+'='));assert.ok(stateCode(t,'rf_frame').includes(reference));assert.ok(!/\brandom(?:ize|_set_seed)?\(/.test(code));
  const clone=structuredClone(t);clone.id='copy';s.tracks.push(clone);assert.notEqual(parameterCode(t,'x'),parameterCode(clone,'x'));
  assert.equal(resolveRandomState(s).tracks[0].x,s.tracks[1].x);assert.ok(randomInitialization(s).includes(parameterCode(clone,'x')+'='));
  s.randomizeOnPlay=true;assert.ok(randomInitialization(s).includes('current_time'));
});
test('lane migration keeps old layouts and mixed objects can share a lane',()=>{
  const s=normalizeState({version:2,tracks:[{id:'a',kind:'object',keyframes:[]},{id:'b',kind:'effect',keyframes:[]},{id:'c',kind:'camera',keyframes:[]}]});
  assert.deepEqual(s.tracks.map(t=>t.lane),[0,1,2]);s.tracks[1].lane=0;s.tracks[2].lane=0;normalizeState(s);assert.deepEqual(s.tracks.map(t=>t.lane),[0,0,0]);
});
