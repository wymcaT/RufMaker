import assert from 'node:assert/strict';
import test from 'node:test';
import {normalizeState,stateCode,valueAt,parameterFields} from './motion.mjs';
import {rainbowCode} from './rainbow.mjs';
import {bulletSystemScripts} from './bullet-system.mjs';
const scene=t=>normalizeState({version:2,fps:50,duration:50,tracks:[{id:'e',kind:'emitter',start:0,end:10,keyframes:[],...t}]}).tracks[0];
test('explicit frame is independent from retained animation and samples keys/formulas',()=>{
 const t=scene({imageIndex:2,imageSpeed:.5,imageDelay:3,spriteFrame:-1,keyframes:[{frame:0,property:'spriteFrame',value:-1},{frame:10,property:'spriteFrame',value:4}]});
 assert.equal(t.imageIndex,2);assert.equal(t.imageSpeed,.5);assert.equal(t.imageDelay,3);assert.equal(valueAt(t,'spriteFrame',10),4);assert(parameterFields.includes('spriteFrame'));assert.match(stateCode(t,'rf_frame'),/rf_spriteFrame=/);
 const code=rainbowCode(t,'0','rf_frame');assert.match(code,/rf_spriteFrame>=0/);assert.match(code,/rf_imageIndex/);assert.match(code,/rf_imageDelay/);
 const s=normalizeState({version:2,fps:50,duration:50,tracks:[t]});const spawn=new Map(bulletSystemScripts(s)).get('rf_pattern_spawn');assert.match(spawn,/if rf_spriteFrame>=0 \{rf_result.rf_frozen_image=floor\(rf_spriteFrame\)/);assert.match(spawn,/rf_anim_birth=rf_born/);
 const legacy=scene({});assert.equal(legacy.spriteFrame,-1);assert.equal(legacy.imageIndex,0);
});
