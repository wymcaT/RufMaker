import test from 'node:test';
import assert from 'node:assert/strict';
import {normalizeState,ease,valueAt,stateCode} from './motion.mjs';
import {motionPoint} from './motion-presets.mjs';

function legacy(){
  const motionPreset={name:'圆周运动',easing:'linear',start:10,end:30,startX:100,startY:200,endX:100,endY:200,peakX:0,peakY:0,centerX:100,centerY:200,amplitudeX:0,amplitudeY:0,radiusStart:50,radiusEnd:50,turns:1,phase:0,hold:.6},keyframes=[];
  for(let frame=10;frame<=30;frame++){const p=motionPoint(motionPreset,frame,ease);for(const field of ['x','y'])keyframes.push({frame,property:field,value:Math.fround(p[field]),easing:'linear',expression:''});}
  keyframes.push({frame:20,property:'alpha',value:.5,easing:'linear'});return {id:'motion',kind:'object',start:10,end:30,motionPreset,keyframes};
}
const scene=t=>normalizeState({version:2,fps:50,duration:100,tracks:[t]});
test('legacy generated positions become a compact preset while independent keys remain',()=>{
  const t=legacy(),before=t.keyframes.find(k=>k.property==='x'&&k.frame===17).value;scene(t);assert(t.motionPreset);assert.deepEqual(t.keyframes.map(k=>k.property),['alpha']);assert.ok(Math.abs(valueAt(t,'x',17)-before)<.001);assert.ok(stateCode(t,'rf_frame').length<10000);
});
test('legacy position edits and expressions are preserved as manual animation',()=>{
  const edited=legacy();edited.keyframes.find(k=>k.property==='x'&&k.frame===17).value+=10;const count=edited.keyframes.length;scene(edited);assert.equal(edited.motionPreset,null);assert.equal(edited.keyframes.length,count);assert.equal(edited.keyframes.find(k=>k.property==='x'&&k.frame===17).value,Math.fround(motionPoint(legacy().motionPreset,17,ease).x)+10);
  const expression=legacy();expression.expressions={x:'player.x'};scene(expression);assert.equal(expression.motionPreset,null);assert.equal(expression.expressions.x,'player.x');
});
