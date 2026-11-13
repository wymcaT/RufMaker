import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {normalizeState} from './motion.mjs';
import {bulletSystemScripts} from './bullet-system.mjs';
import {templateScripts} from './template-system.mjs';
import {groupEventScripts} from './group-events.mjs';
const sourceRoot=fileURLToPath(new URL('..',import.meta.url));
const base=path.join(sourceRoot,'build/rainbow-tint-depth');fs.mkdirSync(base,{recursive:true});
const root=fs.mkdtempSync(path.join(base,'fixture-')),project=path.join(root,'project');
fs.cpSync(path.join(sourceRoot,'templates/avoidance.gm82'),project,{recursive:true});
fs.mkdirSync(path.join(root,'assets/gml'),{recursive:true});fs.cpSync(path.join(sourceRoot,'assets/gml/formations'),path.join(root,'assets/gml/formations'),{recursive:true});
fs.writeFileSync(path.join(project,'rufmaker.json'),JSON.stringify({version:2,tracks:[]}));
process.env.RUFMAKER_ROOT=root;process.env.RUFMAKER_PROJECT=path.join(project,'rufmaker-demo.gm82');
const {syncToGame}=await import('./project.mjs');
const tint='#804020',gmlTint='make_color_rgb(128,64,32)';
const track=(id,kind,extra={})=>({id,kind,sprite:'sprBullet',rainbowSprite:'sprBullet',rainbow:true,color:tint,spriteFrame:3,start:0,end:30,keyframes:[],count:2,interval:5,life:20,...extra});
const scene=tracks=>normalizeState({version:2,fps:50,room:'rAvoidance',duration:100,music:'',nativeProject:{imported:true,defaultSprite:'sprBullet',killerParent:'playerKiller',hasFmod:false},tracks});
const read=n=>fs.readFileSync(path.join(project,n),'utf8');
const explicitVariants=[{spriteFrame:3},{spriteFrame:-1,keyframes:[{property:'spriteFrame',frame:0,value:3}]},{spriteFrame:-1,expressions:{spriteFrame:'at(i,3,-1)'}},{spriteFrame:-1,expressions:{spriteFrame:'-1+i*0'}}];
test('explicit fixed, keyed and expression frames preserve tint through repeated and nested normalization',()=>{
 for(const v of explicitVariants){
  const s=scene([track('e','emitter',v),track('clip','pattern',{rainbow:false,children:[track('child','object',v)]})]);
  for(let i=0;i<3;i++){normalizeState(s);assert.equal(s.tracks[0].color,tint);assert.equal(s.tracks[1].children[0].color,tint);}
  const copy=scene(JSON.parse(JSON.stringify(s.tracks)));assert.equal(copy.tracks[0].color,tint);assert.equal(copy.tracks[0].sprite,'sprBullet');
 }
});
test('automatic rainbow still normalizes white and non-rainbow tint stays unchanged',()=>{
 const s=scene([track('auto','emitter',{spriteFrame:-1}),track('plain','object',{rainbow:false,spriteFrame:-1})]);
 assert.equal(s.tracks[0].color,'#FFFFFF');assert.equal(s.tracks[1].color,tint);
});
test('ordinary emitted bullets, fixed rods, objects and spatial trails export explicit-frame tint',()=>{
 const s=scene(['emitter','rod','object'].map((kind,i)=>track('t'+i,kind,{trailMode:'fade'})));syncToGame(s);
 const ctl=read('objects/objRufController.gml');
 assert.ok(ctl.includes('rf_inst.image_blend='+gmlTint));assert.doesNotMatch(ctl,/rf_inst\.image_blend=c_white;/);
 assert.ok(read('scripts/rf_visual_draw.gml').includes(gmlTint));assert.ok(read('objects/objRufTrails.gml').includes(gmlTint));
 const saved=JSON.parse(read('rufmaker.json'));for(const t of saved.tracks)assert.equal(t.color,tint);for(const t of normalizeState(saved).tracks)assert.equal(t.color,tint);
});
test('advanced spawn generation preserves tint independently from normalization',()=>{
 for(const v of explicitVariants){const s=scene([track('e','emitter',{...v,advancedBullets:true})]);s.tracks[0].color=tint;
  const code=Object.fromEntries(bulletSystemScripts(s)).rf_pattern_spawn;
  assert.ok(code.includes('rf_result.image_blend='+gmlTint),code.match(/rf_result\.image_blend=[^;]+;/)?.[0]);
  assert.doesNotMatch(code,/rf_result\.image_blend=c_white;/);
 }
});
test('template fixed members and inherited source appearance retain tint',()=>{
 const s=scene([track('host','object',{spawnActions:[{type:'spawn',templateId:'child',start:0,inheritAppearance:true}]}),track('child','rod',{templateOnly:true}),track('object-child','object',{templateOnly:true})]);
 for(const t of s.tracks)t.color=tint;
 const code=templateScripts(s).map(x=>x[1]).join('\n');
 assert.ok(code.includes('rf_look_color='+gmlTint));assert.ok(code.includes('self.image_blend='+gmlTint));
 assert.doesNotMatch(code,/(?:rf_look_color|self\.image_blend)=c_white;/);
});
test('group copy captures the explicit-frame tint from fixed rods',()=>{
 const s=scene([track('g','group',{rainbow:false}),track('rod','rod',{parentId:'g'}),track('copy','control',{rainbow:false,eventPreset:'group-copy',targetGroup:'g',eventGroupId:'g',start:3,end:3})]);
 for(const t of s.tracks)t.color=tint;
 s.timelineGroups=[{id:'g',name:'Group',color:'#ffffff',lanes:s.tracks.map(t=>t.lane)}];s.tracks.find(t=>t.id==='copy').eventGroup='g';
 const code=groupEventScripts(s).map(x=>x[1]).join('\n');
 assert.ok(code.includes('rf_new.image_blend='+gmlTint));assert.doesNotMatch(code,/rf_new\.image_blend=c_white;/);
 assert.ok(code.includes('rf_new.image_blend=image_blend;'),'live clone preserves actual captured tint');
});
test('actual trail depth excludes nontrailing backgrounds and preserves source/legacy/overflow rules',()=>{
 const t=(id,kind,depth,mode='off',extra={})=>track(id,kind,{rainbow:false,depth,trailMode:mode,...extra});
 const cases=[
  [[t('bg','object',500),t('e','emitter',-100,'fade')],-99],
  [[t('bg','object',-500),t('e','emitter',-700,'fade')],-699],
  [[t('bg','object',500,'fade'),t('e','emitter',-100)],501],
  [[t('bg','object',500),t('r','rod',400)],401],
  [[t('bg','object',500,'fade',{enabled:false}),t('e','emitter',-100)],-99],
  [[t('bg','object',500)],-6999999],
  [[t('bg','object',500,'legacy',{trail:true})],501],
  [[t('r','rod',2147483647)],2147483648],
  [[t('o','object',-7000100,'fade')],-6999999]
 ];
 for(const [tracks,expected] of cases){syncToGame(scene(tracks));const code=read('objects/objRufTrails.gml');
  const got=Number(code.match(/depth=max\(rf_owner\.depth\+1,(-?\d+)\)/)?.[1]);assert.equal(got,expected);
  assert.ok(code.includes('with(objRufBullet) other.depth=max(other.depth,depth+1);'),'live dynamic bullet depth remains accounted for');
 }
});
