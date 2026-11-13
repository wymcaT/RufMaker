import test from 'node:test';import assert from 'node:assert/strict';import fs from 'node:fs';
import {bulletImageCode} from './rainbow.mjs';import {frozenImageCheck,dynamicsSpawn,animationTick} from './object-dynamics.mjs';import {spawnAppearanceGml} from './spawn-appearance.mjs';import {normalizeState} from './motion.mjs';import {groupEventScripts} from './group-events.mjs';
function sample(c){const keys=Object.keys(c);return Function(...keys,'floor','rf_rainbow_index','return '+bulletImageCode('rf_world'))(...keys.map(k=>c[k]),Math.floor,()=>13);}
const base={rf_image_frozen:0,rf_frozen_image:-1,rf_rainbow:0,rf_r_sequence:0,rf_r_density:1,rf_r_offset:0,rf_r_speed:0,rf_r_start:0,rf_r_dynamic:0,rf_r_reverse:0,rf_world:5,image_index:4};
test('frozen state is independent of negative index while explicit negative remains automatic',()=>{
 for(const frame of [-33,-2,-1,0,7])for(const rainbow of [0,1])assert.equal(sample({...base,rf_image_frozen:1,rf_frozen_image:frame,rf_rainbow:rainbow}),frame);
 assert.equal(sample({...base,rf_frozen_image:-1}),4);assert.equal(sample({...base,rf_frozen_image:7}),7);
 const spawn=dynamicsSpawn({kind:'emitter'});assert.match(spawn,/if rf_spriteFrame>=0/);assert.match(spawn,/rf_image_frozen=1/);assert.ok(animationTick.includes('!'+frozenImageCheck));
});
test('all generated inherited/detached/draw/trail paths retain independent frozen state',()=>{
 assert.match(spawnAppearanceGml,/rf_frozen_image=rf_owner.rf_inherit_image;rf_image_frozen=1/);
 const project=fs.readFileSync(new URL('./project.mjs',import.meta.url),'utf8');assert.match(project,/rf_image_frozen=0;rf_frozen_image=-1/);assert.ok((project.match(/\$\{frozenImageCheck\}/g)||[]).length>=5);assert.doesNotMatch(project,/rf_frozen_image<0/);
 const events=fs.readFileSync(new URL('./event-presets.mjs',import.meta.url),'utf8');assert.match(events,/self.rf_frozen_image=self.image_index;self.rf_image_frozen=1/);
});
test('in-place group copy samples its old image before marking the new frozen state',()=>{
 const s=normalizeState({version:2,tracks:[{id:'e',kind:'emitter',start:0,end:0,keyframes:[]},{id:'copy',kind:'control',start:1,end:1,eventPreset:'group-copy',keyframes:[]}]});
 const g=Object.fromEntries(groupEventScripts(s)).rf_event_clone;const start=g.indexOf('rf_new.rf_frozen_image=');const end=g.indexOf('rf_new.rf_rainbow=0;',start)+'rf_new.rf_rainbow=0;'.length;const code=g.slice(start,end);assert.ok(start>=0);
 for(const frame of [-2,-1,4]){const c={...base,rf_image_frozen:frame===4?0:1,rf_frozen_image:frame};c.rf_new=c;Function('c','floor','rf_rainbow_index','with(c){'+code+'}')(c,Math.floor,()=>13);assert.equal(c.rf_frozen_image,frame);assert.equal(c.rf_image_frozen,1);}
 const c={...base,rf_rainbow:1};c.rf_new=c;Function('c','floor','rf_rainbow_index','with(c){'+code+'}')(c,Math.floor,()=>13);assert.equal(c.rf_frozen_image,13);assert.equal(c.rf_image_frozen,1);
});
