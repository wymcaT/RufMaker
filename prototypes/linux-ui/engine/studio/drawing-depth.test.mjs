import assert from 'node:assert/strict';
import test from 'node:test';
import {normalizeState} from './motion.mjs';
import {trackDepth,sourceDepthGml} from './drawing-depth.mjs';

test('legacy defaults and custom depths survive normalization and nested clips',()=>{
  const tracks=[
    {id:'object',kind:'object',start:0,end:20,keyframes:[]},
    {id:'rod',kind:'rod',start:0,end:20,depth:0,keyframes:[]},
    {id:'clip',kind:'pattern',start:0,end:20,keyframes:[],children:[
      {id:'child',kind:'object',start:0,end:10,depth:-250,keyframes:[]}
    ]}
  ];
  const state=normalizeState({version:2,duration:50,fps:50,tracks});
  assert.equal(state.tracks[0].depth,null);
  assert.equal(trackDepth(state.tracks[0]),-7000000);
  assert.equal(trackDepth(state.tracks[1]),0);
  assert.equal(state.tracks[2].children[0].depth,-250);
  assert.equal(JSON.parse(JSON.stringify(state)).tracks[2].children[0].depth,-250);
  assert.match(sourceDepthGml(state),/case 0:if argument1>0 return -7000000-argument1;/);
  assert.match(sourceDepthGml(state),/case 1:return 0-argument1;/);
});
test('invalid depth values fail before project synchronization',()=>{
  assert.throws(()=>normalizeState({version:2,tracks:[{id:'bad',kind:'object',depth:1.5,keyframes:[]}]}),/有效的整数/);
});
test('teleport coordinates accept and retain expressions during save normalization',()=>{
  const state=normalizeState({version:2,duration:50,fps:50,tracks:[
    {id:'move',kind:'control',eventPreset:'player-move',start:3,end:3,keyframes:[],expressions:{playerTargetX:'choose(600,600)',playerTargetY:'player.y+10'}}
  ]});
  assert.equal(state.tracks[0].playerTargetX,600);
  assert.equal(state.tracks[0].expressions.playerTargetY,'player.y+10');
});
test('absolute depth preserves exact source layer across generations and JSON',()=>{
 const state=normalizeState({version:2,fps:50,duration:50,tracks:[
  {id:'absolute',kind:'emitter',start:0,end:20,depth:-100,depthMode:'absolute',keyframes:[]},
  {id:'legacy',kind:'rod',start:0,end:20,depth:-100,keyframes:[]},
  {id:'unset',kind:'rod',start:0,end:20,keyframes:[]},
  {id:'default-fixed',kind:'rod',start:0,end:20,depthMode:'absolute',keyframes:[]},
  {id:'clip',kind:'pattern',start:0,end:20,keyframes:[],children:[{id:'child',kind:'object',start:0,end:10,depth:-100,depthMode:'absolute',keyframes:[]}]}
 ]});
 assert.equal(state.tracks[1].depthMode,'generation');
 const restored=normalizeState(JSON.parse(JSON.stringify(state)));
 assert.equal(restored.tracks[0].depthMode,'absolute');assert.equal(restored.tracks[4].children[0].depthMode,'absolute');
 const helper=sourceDepthGml(restored);assert.match(helper,/case 0:return -100;/);assert.match(helper,/case 1:return -100-argument1;/);assert.match(helper,/case 2:if argument1>0 return -7000000-argument1;return -100-argument1;/);assert.match(helper,/case 3:return -100;/);
 // Evaluate the actual return expressions emitted for all three generation values.
 for(const gen of [0,1,2])for(const index of [0,1,3]){const body=helper.match(new RegExp(`case ${index}:return ([^;]+);`))[1];const result=Function('argument1',`return ${body}`)(gen);assert.equal(result,index===1?-100-gen:-100);}
 assert.throws(()=>normalizeState({version:2,tracks:[{id:'bad-mode',kind:'object',depthMode:'typo',keyframes:[]}]}),/深度模式/);
});
