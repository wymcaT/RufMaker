import test from 'node:test';import assert from 'node:assert/strict';import fs from 'node:fs';import path from 'node:path';import vm from 'node:vm';import {fileURLToPath} from 'node:url';
import {normalizeState} from './motion.mjs';import {blendScope,blendModeCode} from './sprite-blend.mjs';import {blendTrailCall} from './trails.mjs';import {templateScripts} from './template-system.mjs';import {groupEventScripts} from './group-events.mjs';import {spawnAppearanceGml} from './spawn-appearance.mjs';
const sourceRoot=fileURLToPath(new URL('..',import.meta.url)),base=path.join(sourceRoot,'build/visual-export-parity');fs.mkdirSync(base,{recursive:true});const root=fs.mkdtempSync(path.join(base,'blend-fixture-')),project=path.join(root,'project');
fs.cpSync(path.join(sourceRoot,'templates/avoidance.gm82'),project,{recursive:true});fs.mkdirSync(path.join(root,'assets/gml'),{recursive:true});fs.cpSync(path.join(sourceRoot,'assets/gml/formations'),path.join(root,'assets/gml/formations'),{recursive:true});
fs.writeFileSync(path.join(project,'rufmaker.json'),JSON.stringify({version:2,tracks:[]}));
process.env.RUFMAKER_ROOT=root;process.env.RUFMAKER_PROJECT=path.join(project,'rufmaker-demo.gm82');const {syncToGame}=await import('./project.mjs');
const track=(id,kind,blendMode='add',extra={})=>({id,kind,blendMode,sprite:'sprBullet',start:0,end:30,keyframes:[],trailMode:'fade',count:2,interval:5,life:20,...extra});
const scene=tracks=>normalizeState({version:2,fps:50,room:'rAvoidance',duration:100,music:'',nativeProject:{imported:true,defaultSprite:'sprBullet',killerParent:'playerKiller',hasFmod:false},tracks});
const read=n=>fs.readFileSync(path.join(project,n),'utf8');
test('missing blend defaults to normal, valid values persist and invalid explicit modes fail',()=>{
 const t=track('old','object');delete t.blendMode;assert.equal(scene([t]).tracks[0].blendMode,'normal');assert.equal(blendModeCode({blendMode:'add'}),'bm_add');for(const bad of ['multiply','',4,false])assert.throws(()=>scene([track('bad','object',bad)]),/Unsupported blendMode/);
 const s=scene([track('clip','pattern','normal',{children:[track('child','object')]})]);assert.equal(scene(JSON.parse(JSON.stringify(s.tracks))).tracks[0].children[0].blendMode,'add');
});
test('sprite scopes establish a mode and restore normal after additive, normal and empty trail draws',()=>{
 let mode=7,seen=[];const c={bm_add:1,bm_normal:0,draw_set_blend_mode:m=>mode=m,draw:()=>seen.push(mode),rf_trail_draw_xy:()=>seen.push(mode)};
 vm.runInNewContext(blendScope('draw();','bm_add')+blendScope('draw();'),c);assert.deepEqual(seen,[1,0]);assert.equal(mode,0);
 vm.runInNewContext(blendScope('rf_trail_draw_xy();','bm_add'),c);assert.equal(mode,0);
 assert.match(blendTrailCall(track('a','object'),{color:'0',age:'1',seed:'0'}),/^\{draw_set_blend_mode\(bm_add\);rf_trail_draw_xy\(/);
});
test('actual project sync wires object, ordinary emitter, rods and both trail paths without changing animation or axes',()=>{
 const s=scene([track('o','object'),track('e','emitter'),track('r','rod'),track('n','object','normal')]);syncToGame(s);
 assert.match(read('scripts/rf_visual_draw.gml'),/draw_set_blend_mode\(bm_add\);draw_sprite_ext\(/);assert.match(read('scripts/rf_visual_draw.gml'),/draw_set_blend_mode\(bm_normal\);draw_sprite_ext\(/);
 const ctl=read('objects/objRufController.gml');assert.match(ctl,/rf_inst.rf_blend_mode=bm_add;rf_inst.sprite_index=/);assert.match(ctl,/rf_inst.rf_blend_mode=bm_add;rf_inst.x=rf_px/);
 const bullet=read('objects/objRufBullet.gml');assert.match(bullet,/rf_blend_mode=bm_normal;image_speed=0/);assert.match(bullet,/draw_set_blend_mode\(rf_blend_mode\);draw_sprite_ext\([^\n]+draw_set_blend_mode\(bm_normal\)/);assert.match(bullet,/rf_anim_index\+max/);
 assert.match(read('objects/objRufTrails.gml'),/draw_set_blend_mode\(bm_add\);rf_trail_draw_xy\(/);assert.match(read('scripts/rf_bullet_trails.gml'),/draw_set_blend_mode\(rf_blend_mode\);rf_trail_draw_xy\([^\n]+draw_set_blend_mode\(bm_normal\)/);
});
test('advanced spawns, inherited target appearance, linked copies and group clones keep the source track mode',()=>{
 const s=scene([track('host','object','add',{spawnActions:[{type:'spawn',templateId:'target',start:0,inheritAppearance:true},{type:'spawn',generationPreset:'copy-object',copyLinked:true,start:2}]}),track('target','rod','normal',{templateOnly:true}),track('e','emitter','add',{advancedBullets:true,bulletActions:[{type:'split',start:2,count:2}]}),track('copy','control','normal',{eventPreset:'group-copy',start:3,end:3,eventGroup:'g'})]);s.timelineGroups=[{id:'g',name:'Group',color:'#ffffff',lanes:s.tracks.map(t=>t.lane)}];syncToGame(s);
 assert.match(read('scripts/rf_pattern_spawn.gml'),/rf_result.rf_blend_mode=bm_add;/);const templates=templateScripts(s).map(x=>x[1]).join('\n');assert.match(templates,/(?:rf_inst|self).rf_blend_mode=bm_normal;/);assert.match(templates,/(?:rf_inst|self).rf_blend_mode=bm_add;/);assert.match(templates,/rf_copy_rule/);
 assert.doesNotMatch(spawnAppearanceGml,/rf_blend_mode=/);assert.match(groupEventScripts(s).map(x=>x[1]).join('\n'),/rf_new.rf_blend_mode=rf_blend_mode;/);
});
