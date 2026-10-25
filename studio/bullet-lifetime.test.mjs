import test from 'node:test';
import assert from 'node:assert/strict';
import {normalizeLifetime,lifetimeEnabled,lifetimeSpawn,lifetimeScripts} from './bullet-lifetime.mjs';
import {normalizeState} from './motion.mjs';
import {advancedBullets,expandPatterns} from './bullet-schema.mjs';
import {templateScripts} from './template-system.mjs';

test('old projects keep fixed life with no transition; boundary/fade settings round trip',()=>{
  const t={};normalizeLifetime(t);assert.deepEqual(t,{lifetimeMode:'fixed',deathEffect:'none',deathFrames:15,despawnMargin:0});assert.equal(lifetimeEnabled(t),false);
  const s=normalizeState({version:2,fps:50,tracks:[{id:'a',kind:'emitter',start:0,end:20,lifetimeMode:'view',deathEffect:'shrink-fade',deathFrames:8,despawnMargin:-20,keyframes:[]}]});
  assert.equal(advancedBullets(s),true);assert.equal(s.tracks[0].despawnMargin,-20);assert.match(lifetimeSpawn(s.tracks[0]),/rf_life_mode=2;.*rf_die_fx=3;.*rf_die_frames=8/);assert.equal(lifetimeScripts.length,3);
  assert.equal(JSON.parse(JSON.stringify(s)).tracks[0].deathEffect,'shrink-fade');
});
test('looped clips scale transition time and keep spatial margins and per-member angles',()=>{
  const s=normalizeState({version:2,fps:50,duration:100,tracks:[{id:'loop',kind:'pattern',start:0,end:40,timeScale:.5,children:[{id:'a',kind:'emitter',start:0,end:20,deathFrames:7,lifetimeMode:'room',despawnMargin:12,spawnActions:[{type:'spawn',templateId:'a',spawnAngle:10,spawnAngleStep:15}],keyframes:[]}],keyframes:[]}]});
  const t=expandPatterns(s).tracks[0];assert.equal(t.deathFrames,14);assert.equal(t.despawnMargin,12);assert.equal(t.spawnActions[0].spawnAngleStep,15);
  assert.match(templateScripts(expandPatterns(s)).map(x=>x[1]).join('\n'),/\+10\+rf_j\*15/);
});
