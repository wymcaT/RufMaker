import test from 'node:test';
import assert from 'node:assert/strict';
import {normalizeState} from './motion.mjs';
import {bulletSystemScripts} from './bullet-system.mjs';
import {trailXYGml} from './trails.mjs';
const emitter=extra=>({id:'p',kind:'emitter',start:0,end:0,count:1,speed:2,life:100,keyframes:[],...extra});
const scripts=tracks=>Object.fromEntries(bulletSystemScripts(normalizeState({tracks,duration:100})));
test('one unified gravity initializer/integrator with birth move and independent animation clock',()=>{
 const map=scripts([emitter({moveOnBirth:true,bulletGravity:1,imageSpeed:1,despawnBounds:'sprite-mask'})]);
 assert.equal((map.rf_pattern_spawn.match(/rf_result\.rf_gravity=rf_bulletGravity;/g)||[]).length,1);
 assert.equal((map.rf_pattern_tick.match(/rf_velocity_x=lengthdir_x\(rf_speed,rf_dir\)/g)||[]).length,1);
 assert.match(map.rf_pattern_tick,/rf_age>0 \|\| rf_birth_move/);
 assert.match(map.rf_pattern_tick,/rf_world-rf_anim_birth-rf_anim_delay/);
 assert.ok(map.rf_pattern_tick.indexOf('image_index=floor')<map.rf_pattern_tick.indexOf('rf_sprite_mask_tick'));
 assert.match(trailXYGml,/floor\(argument14\) mod rf_images/);
});
test('animation updates before action-time capture; reset never resets animation birth',()=>{
 const map=scripts([emitter({imageSpeed:1,bulletActions:[{type:'spawn',templateId:'child',spawnTiming:'action-time',start:2}]}),emitter({id:'child',templateOnly:true}),{id:'reset',kind:'control',start:3,end:3,eventPreset:'reset-motion',eventResetLife:true}]);
 assert.ok(map.rf_pattern_tick.indexOf('image_index=floor')<map.rf_pattern_tick.indexOf('rf_template_bullet_action(rf_world,'));
 assert.match(map.rf_pattern_tick,/rf_birth=rf_world;rf_age=0/);
 assert.doesNotMatch(map.rf_pattern_tick,/rf_anim_birth=/);
 assert.match(map.rf_pattern_tick,/rf_orbiting=0;rf_gravity=0;rf_gravity_dir=270;/);
});
