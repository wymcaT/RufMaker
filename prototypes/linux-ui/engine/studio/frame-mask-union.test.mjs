import test from 'node:test';
import assert from 'node:assert/strict';
import {bulletImageCode} from './rainbow.mjs';
import {validateMaskState} from './sprite-mask-gml.mjs';
import {normalizeState} from './motion.mjs';
import {templateScripts} from './template-system.mjs';
import {groupEventScripts} from './group-events.mjs';
const sample=(fields,time)=>{
 const keys=['rf_image_frozen','rf_frozen_image','rf_rainbow','rf_r_sequence','rf_r_density','rf_r_offset','rf_r_speed','rf_r_start','rf_r_dynamic','rf_r_reverse','image_index'];
 const rainbow=(sequence,now,density,offset,speed,start,dynamic,reverse,n)=>{let p=sequence*density+offset+(dynamic?(now-start)*speed:0);if(reverse)p=-p;return ((Math.floor(p)%n)+n)%n;};
 return Function(...keys,'rf_world','floor','rf_rainbow_index','return '+bulletImageCode('rf_world'))(...keys.map(k=>fields[k]??0),time,Math.floor,rainbow);
};
test('shared generated image expression prioritizes frozen frames and uses one world clock',()=>{
 for(const rainbow of [0,1])for(const frozen of [0,1,3,101])assert.equal(sample({rf_frozen_image:frozen,rf_rainbow:rainbow,image_index:7,rf_r_sequence:11,rf_r_density:1},108),frozen);
 assert.equal(sample({rf_frozen_image:-1,rf_rainbow:1,rf_r_sequence:1,rf_r_density:1,rf_r_offset:2,rf_r_speed:1,rf_r_start:105,rf_r_dynamic:1},108),6);
 assert.equal(sample({rf_frozen_image:-1,rf_rainbow:0,image_index:-1.5},108),-2);
});
test('mask validation accepts explicit/keyed/expression frames but retains unrelated validation',()=>{
 for(const t of [{spriteFrame:0},{spriteFrame:101},{keyframes:[{property:'spriteFrame',frame:0,value:2}]},{expressions:{spriteFrame:'at(i,2,-1)'}}])assert.doesNotThrow(()=>validateMaskState({tracks:[{kind:'emitter',despawnBounds:'sprite-mask',...t}]}));
 assert.throws(()=>validateMaskState({tracks:[{kind:'object',despawnBounds:'sprite-mask',spriteFrame:2}]}),/flying emitter/);
 assert.throws(()=>validateMaskState({tracks:[{kind:'emitter',despawnBounds:'sprite-mask',boundsSprite:'not a resource',spriteFrame:2}]}),/resource name/);
});
test('ordinary/action-time inherited spawns and group copy share frozen-first image selection',()=>{
 const s=normalizeState({version:2,duration:30,tracks:[{id:'g',kind:'group',start:0,end:30},{id:'p',kind:'emitter',parentId:'g',start:0,end:0,spriteFrame:3,rainbow:true,keyframes:[],bulletActions:[{type:'spawn',templateId:'child',inheritAppearance:true,start:1},{type:'spawn',templateId:'child',inheritAppearance:true,spawnTiming:'action-time',start:2}]},{id:'child',kind:'emitter',start:0,end:0,templateOnly:true},{id:'copy',kind:'control',start:3,end:3,eventPreset:'group-copy',targetGroup:'g',eventGroupId:'g'}].map(t=>({keyframes:[],...t}))});
 const template=Object.fromEntries(templateScripts(s));
 assert.ok(template.rf_template_bullet_actions.includes(bulletImageCode('rf_now')));
 assert.ok(template.rf_template_bullet_action.includes(bulletImageCode('rf_now')));
 const group=Object.fromEntries(groupEventScripts(s));
 assert.ok(group.rf_event_clone.includes('rf_new.rf_frozen_image='+bulletImageCode('rf_world')));
 assert.ok(!group.rf_event_clone.includes('rf_world-rf_clock_shift'));
});
