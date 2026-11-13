import test from 'node:test';
import assert from 'node:assert/strict';
import {copyDefaults,copyPlacements,copySpawnGml,liveCopyPlacements} from './copy-generation.mjs';
import {normalizeState} from './motion.mjs';
import {templateScripts} from './template-system.mjs';
const action=n=>({...copyDefaults,type:'spawn',generationPreset:'copy-object',copyMode:'rotate',copyCount:n,templateId:'template',spawnOffsetX:0,spawnOffsetY:0});
test('rotate count includes original and preserves zero-radius sector angles',()=>{
 for(const n of [1,2,10,300])for(const center of [false,true]){const a={...action(n),copyCenter:center,copyRadius:0,copyAngle:73};const p=copyPlacements(a);assert.equal(p.length,n-1);assert.deepEqual(liveCopyPlacements(a,800,608),p);p.forEach((v,i)=>assert.deepEqual(v,{x:0,y:0,angle:360*(i+1)/n,index:i,count:n-1}));}
 assert.throws(()=>copyPlacements(action(0)));assert.throws(()=>copyPlacements(action(301)));
 assert.equal(copyPlacements({...copyDefaults,copyMode:'circle',copyRadius:0,copyCount:10}).length,0,'Legacy circle unchanged');
});
test('rotate survives normalized JSON and keeps one source animation graph',()=>{
 const s=normalizeState({version:2,fps:50,duration:100,tracks:[{id:'root',name:'root',kind:'group',keyframes:[],start:0,end:80,x:400,y:304,angle:23,spawnActions:[action(10)]},{id:'child',name:'child',kind:'object',start:0,end:80,parentId:'root',x:100,keyframes:[{frame:0,property:'x',value:100},{frame:20,property:'x',value:150}]}]});
 const restored=normalizeState(JSON.parse(JSON.stringify(s)));assert.equal(restored.tracks.length,2);assert.equal(restored.tracks[1].keyframes.length,2);assert.equal(restored.tracks[0].spawnActions[0].copyMode,'rotate');
 const scripts=new Map(templateScripts(restored));assert.equal((scripts.get('rf_copy_link_spawn').match(/instance_create\(/g)||[]).length,9);assert.match(scripts.get('rf_copy_link_update'),/rf_rotation=\(rf_heading\+rf_copy_pa\+0\)-rf_angle/);assert.match(scripts.get('rf_copy_link_update'),/rf_heading\+=rf_copy_owner.rf_rotation/);
 assert.doesNotMatch(scripts.get('rf_copy_link_spawn'),/rf_copy_pa=0;/);
});
test('independent emitted rotations include source heading, sector and anchor subtraction',()=>{
 const c={source:{},heading:'source_heading',x:'source_x',y:'source_y',generation:'0',sourceX:'source_x',sourceY:'source_y',color:'c_white',lookImage:'0',lookColor:'c_white',lookSprite:'-1',lookScale:'1',after:''};
 for(const facing of ['keep','fixed','inward','outward','tangent']){const code=copySpawnGml({...action(10),copyFacing:facing,copyFacingAngle:7},'target',c);assert.equal((code.match(/rf_template_spawn\(/g)||[]).length,9);for(let i=1;i<10;i++)assert.ok(code.includes(`((source_heading+${i*36}+7)-rf_copy_anchor_angle(target))`));}
 assert.equal(copySpawnGml(action(1),'target',c),'');
});
