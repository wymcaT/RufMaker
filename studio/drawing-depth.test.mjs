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
