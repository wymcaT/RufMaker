import test from 'node:test';
import assert from 'node:assert/strict';
import {normalizeState} from './motion.mjs';
import {expandPatterns,advancedBullets} from './bullet-schema.mjs';
import {bulletSystemScripts} from './bullet-system.mjs';
import {templateScripts} from './template-system.mjs';
import {expressionScripts,expressionScriptName} from './random-parameters.mjs';
import {rainbowCode,rodSequence,releasedRainbow} from './rainbow.mjs';
import {lifetimeScripts} from './bullet-lifetime.mjs';

const track=(id,more={})=>({id,kind:'emitter',start:0,end:0,sprite:'sprBullet',keyframes:[],...more});
const scene=tracks=>normalizeState({version:2,fps:50,duration:100,randomSeed:12345,tracks});

test('region spawns retain world coordinates, independent rectangle samples and fade-in lifetime state',()=>{
  const s=scene([track('host',{kind:'object',x:-900,y:1200,end:50,spawnActions:[{type:'spawn',generationPreset:'random-region',templateId:'shot',spawnAtWorld:true,spawnOffsetX:400,spawnOffsetY:304,spawnRepeat:7}]}),track('shot',{templateOnly:true,emissionShape:'rectangle',emissionFacing:'fixed',randomPerInstance:true,fadeInFrames:12,expressions:{speed:'random_range(1,5)',angle:'choose(0,90,180)'}})]);
  assert.ok(advancedBullets(s));assert.equal(s.tracks[1].color,'#FFFFFF');
  const scripts=Object.fromEntries(bulletSystemScripts(s)),spawn=scripts.rf_pattern_spawn;
  assert.match(spawn,/rf_epx=\(rf_expr_/);assert.match(spawn,/rf_epy=\(rf_expr_/);assert.match(spawn,/rf_fade_in=12/);
  assert.match(templateScripts(s).find(([name])=>name==='rf_template_step')[1],/rf_now,0\+lengthdir_x\(400/);
  assert.ok(expressionScripts(s,[]).some(([name])=>name===expressionScriptName('random(1)')));
  assert.ok(expressionScripts(s,[]).some(([name])=>name==='rf_random_mix'));
  assert.match(lifetimeScripts.find(([name])=>name==='rf_lifetime_tick')[1],/image_alpha\*=clamp\(rf_age\/rf_fade_in,0,1\)/);
  const old=scene([track('old',{color:'#2468AC'})]).tracks[0];assert.equal(old.color,'#2468AC');assert.equal(old.fadeInFrames,0);assert.equal(old.rainbowRandom,false);
});

test('random palette is sampled once for projectiles and by stable member index for shapes',()=>{
  const s=scene([track('shot',{rainbow:true,rainbowRandom:true}),track('rod',{kind:'rod',rainbow:true,rainbowRandom:true,rainbowMode:'distance'})]);
  const code=Object.fromEntries(bulletSystemScripts(s)).rf_pattern_spawn;
  assert.match(code,/rf_r_sequence=floor\(rf_expr_/);assert.equal(rodSequence(s.tracks[1]),'rf_j');
  assert.match(rainbowCode(s.tracks[1],'rf_j','rf_frame'),/rf_random_mix/);
  assert.match(rainbowCode(s.tracks[1],'rf_j','rf_frame'),/objRufController\.rf_rainbow_seed/);
  assert.doesNotMatch(rainbowCode(s.tracks[1],'rf_r_sequence','rf_frame',{randomSampled:true}),/rf_expr_/);
  assert.equal(releasedRainbow(s.tracks[1],'rf_frame'),'');
  assert.ok(expressionScripts(s,[]).some(([name])=>name===expressionScriptName('random(32)')));
});

test('clip time scaling keeps fade duration and region coordinates without changing existing colors',()=>{
  const s=scene([track('clip',{kind:'pattern',start:20,end:100,timeScale:2,children:[track('child',{fadeInFrames:16,end:40,color:'#00FF00',spawnActions:[{type:'spawn',templateId:'child',spawnAtWorld:true,spawnOffsetX:320,spawnOffsetY:128,spawnRepeat:10}]})]})]);
  const [child]=expandPatterns(s).tracks;assert.equal(child.fadeInFrames,8);assert.equal(child.color,'#00FF00');assert.equal(child.spawnActions[0].spawnRepeat,5);assert.equal(child.spawnActions[0].spawnAtWorld,true);assert.equal(child.spawnActions[0].spawnOffsetX,320);
});
