import assert from 'node:assert/strict';
import test from 'node:test';
import {normalizeState} from './motion.mjs';
import {advancedBullets,expandPatterns} from './bullet-schema.mjs';
import {bulletSystemScripts} from './bullet-system.mjs';
import {hasPhysics} from './object-dynamics.mjs';
const shot=overrides=>({id:'shot',name:'shot',kind:'emitter',start:0,end:20,keyframes:[],...overrides});
const scene=tracks=>normalizeState({version:2,fps:50,duration:1500,tracks});
test('old tracks stay still visually and new physics uses the shared seeded sampling path',()=>{
  const old=scene([shot({})]);assert.equal(hasPhysics(old.tracks[0]),false);assert.equal(old.tracks[0].imageSpeed,0);
  const s=scene([shot({bulletGravity:.4,bulletFriction:.03,imageSpeed:.25,expressions:{bulletAcceleration:'random_range(.1,.3)'}})]);
  assert.equal(advancedBullets(s),true);const scripts=Object.fromEntries(bulletSystemScripts(s));
  assert.match(scripts.rf_pattern_spawn,/rf_result\.rf_gravity=rf_bulletGravity/);assert.match(scripts.rf_pattern_spawn,/rf_anim_speed=rf_imageSpeed/);
  assert.match(scripts.rf_pattern_tick,/rf_age>0 \{if rf_orbiting/);assert.match(scripts.rf_pattern_tick,/rf_gravity!=0/);
});
test('clip speed changes scale physics, animation, keys and expressions together',()=>{
  const child=shot({bulletGravity:.4,bulletFriction:.2,imageSpeed:.5,expressions:{bulletGravity:'.4'},keyframes:[{frame:0,property:'imageSpeed',value:.5},{frame:10,property:'bulletFriction',value:.2}]});
  const source=scene([{id:'clip',kind:'pattern',name:'clip',start:0,end:10,keyframes:[],repeatCount:1,timeScale:2,children:[child]}]);
  const t=expandPatterns(source).tracks[0];assert.equal(t.bulletGravity,1.6);assert.equal(t.imageSpeed,1);assert.equal(t.keyframes.find(k=>k.property==='imageSpeed').value,1);assert.equal(t.keyframes.find(k=>k.property==='bulletFriction').value,.8);assert.match(t.expressions.bulletGravity,/\*4$/);
});
