import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const sourceRoot=fileURLToPath(new URL('..',import.meta.url));
const base=path.join(sourceRoot,'build','backend-merge');fs.mkdirSync(base,{recursive:true});
const root=fs.mkdtempSync(path.join(base,'fixture-')),project=path.join(root,'project');
fs.cpSync(path.join(sourceRoot,'templates','avoidance.gm82'),project,{recursive:true});
fs.mkdirSync(path.join(root,'assets','gml'),{recursive:true});fs.cpSync(path.join(sourceRoot,'assets','gml','formations'),path.join(root,'assets','gml','formations'),{recursive:true});
const state={version:2,fps:50,room:'rAvoidance',duration:100,music:'',nativeProject:{imported:true,defaultSprite:'sprBullet',killerParent:'playerKiller',hasFmod:false},tracks:[
{id:'source',kind:'emitter',sprite:'sprBullet',start:0,end:20,count:1,interval:5,speed:2,life:60,scaleX:-2,scaleY:.5,imageSpeed:.25,imageIndex:2,bulletGravity:.4,rawAlpha:true,bulletActions:[{type:'spawn',templateId:'target',start:3,duration:1,once:true}],keyframes:[]},
{id:'target',kind:'emitter',sprite:'sprBullet',templateOnly:true,start:0,end:10,count:1,interval:5,speed:0,life:20,keyframes:[]},
{id:'visual',kind:'object',sprite:'sprBullet',start:0,end:30,scaleX:-1,scaleY:2,keyframes:[]}
]};
fs.writeFileSync(path.join(project,'rufmaker.json'),JSON.stringify(state));
process.env.RUFMAKER_ROOT=root;process.env.RUFMAKER_PROJECT=path.join(project,'rufmaker-demo.gm82');
const {readState,syncToGame,moveRoomInstance}=await import('./project.mjs');
test('merged generation preserves Windows animation and registers cloud lifecycle and axis scripts',()=>{
const s=readState();syncToGame(s);const read=n=>fs.readFileSync(path.join(project,n),'utf8');
const bullet=read('objects/objRufBullet.gml');assert.match(bullet,/rf_anim_enabled=0/);assert.match(bullet,/rf_anim_index\+max/);assert.match(bullet,/rf_action_once\[rf_action_init\]=0/);assert.match(bullet,/rf_axis_x=1/);
assert.match(read('scripts/rf_visual_draw.gml'),/rf_scale\*rf_scaleX,rf_scale\*rf_scaleY/);
assert.match(read('scripts/rf_bullet_trails.gml'),/rf_tsy=rf_hsy\[rf_slot\]/);assert.match(read('scripts/rf_bullet_trails.gml'),/rf_anim_index\+max/);
const scripts=new Set(read('scripts/index.yyd').split(/\r?\n/));const tree=read('scripts/tree.yyd');
for(const name of ['rf_trail_draw_xy','rf_template_bullet_actions','rf_pattern_spawn']){assert(scripts.has(name),name);assert(tree.includes('|'+name),name);assert(fs.existsSync(path.join(project,'scripts',name+'.gml')));}
const generated=fs.readdirSync(path.join(project,'scripts')).filter(n=>n.startsWith('rf_')&&n.endsWith('.gml'));
for(const file of generated){const code=read('scripts/'+file);for(const m of code.matchAll(/\b(rf_\w+)\s*\(/g)){assert(scripts.has(m[1]),`${file} references unregistered ${m[1]}`);}}
const saved=JSON.parse(read('rufmaker.json'));assert.equal(saved.tracks[0].imageSpeed,.25);assert.equal(saved.tracks[0].bulletGravity,.4);assert.equal(saved.tracks[0].scaleX,-2);assert.equal(saved.tracks[0].bulletActions[0].type,'spawn');
});
test('bridge keeps conversion and JSON-only protocol while exposing rich metadata and selected film entry',()=>{
const code=fs.readFileSync(path.join(sourceRoot,'studio/bridge.mjs'),'utf8');assert.match(code,/case 'gm-object-convert': return convertGmObject/);assert.match(code,/readProjectWaveform\(project,state.music\)/);assert.match(code,/readSpriteMetadata\(project,inventory\(\).sprites\)/);assert.match(code,/nativeFilmStatus\(project,readState\(\),\{projectFile\}\)/);assert.doesNotMatch(code,/console\.log\(/);
});
test('bridge process responds exclusively in JSON and exposes selected-entry provenance',async()=>{
 const {spawnSync}=await import('node:child_process');
 const run=spawnSync(process.execPath,[path.join(sourceRoot,'studio','bridge.mjs')],{env:{...process.env,RUFMAKER_ROOT:root,RUFMAKER_PROJECT:path.join(project,'rufmaker-demo.gm82')},input:JSON.stringify({id:41,command:'asset-info'})+'\n'+JSON.stringify({id:42,command:'native-film-status'})+'\n',encoding:'utf8'});
 assert.equal(run.status,0,run.stderr);const responses=run.stdout.trim().split('\n').map(x=>JSON.parse(x));assert.equal(responses.length,2);assert(responses.every(x=>x.ok),run.stdout);const film=responses.find(x=>x.id===42).result;assert.equal(film.coverageVersion,1);assert.equal(film.projectEntry,'rufmaker-demo.gm82');assert.equal(film.playerReference,'fixed');assert.equal(film.playerX,400);assert.equal(film.playerY,304);
});
