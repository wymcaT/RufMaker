import test from 'node:test';import assert from 'node:assert/strict';
import {normalizeState} from './motion.mjs';import {expandPatterns,activeCode} from './bullet-schema.mjs';import {templateBundles,templateScripts,spawnCondition} from './template-system.mjs';
const track=t=>({id:'x',kind:'object',start:0,end:100,keyframes:[],...t});
const spawn=(target,more={})=>({type:'spawn',templateId:target,start:5,duration:0,...more});
test('template definitions detach only their root and retain member motion without mutating source',()=>{
 const state=normalizeState({version:2,fps:50,tracks:[track({id:'outer',kind:'group'}),track({id:'parent',kind:'group',parentId:'outer',templateOnly:true}),track({id:'child',kind:'rod',parentId:'parent',shape:'heart'}),track({id:'host',spawnActions:[spawn('parent')]})]});
 const [{root,tracks}]=templateBundles(state);assert.equal(root.parentId,'');assert.equal(root.templateOnly,false);assert.equal(tracks.find(t=>t.id==='child')._parentIndex,1);assert.equal(state.tracks[1].parentId,'outer');assert.equal(state.tracks[1].templateOnly,true);assert.equal(activeCode(state.tracks[1],'f',state.tracks),'0');
});
test('loop expansion keeps external templates and remaps internal generation references and repeat intervals',()=>{
 const state=normalizeState({version:2,fps:50,duration:500,tracks:[track({id:'external',templateOnly:true}),track({id:'clip',kind:'pattern',start:100,end:300,repeatCount:2,timeScale:2,children:[track({id:'inside',templateOnly:true}),track({id:'host',spawnActions:[spawn('external',{spawnRepeat:20}),spawn('inside')]})]})]});
 const flat=expandPatterns(state),hosts=flat.tracks.filter(t=>t.id.endsWith('/host'));assert.equal(hosts.length,2);for(const t of hosts){assert.equal(t.spawnActions[0].templateId,'external');assert.equal(t.spawnActions[0].spawnRepeat,10);assert.equal(t.spawnActions[1].templateId,t.id.replace(/host$/,'inside'));}
});
test('spawn options validate and generated scripts balance branches across recursive and group cases',()=>{
 const state=normalizeState({version:2,fps:50,tracks:[track({id:'heart',kind:'rod',templateOnly:true,spawnActions:[spawn('heart',{maxGeneration:3})]}),track({id:'host',kind:'emitter',bulletActions:[spawn('heart',{spawnRepeat:10,spawnParity:'even',destroyParent:true})]})]});
 for(const [name,source] of templateScripts(state)){const clean=source.replace(/'[^']*'/g,''),balance=[...clean].reduce((n,c)=>n+(c==='{'?1:c==='}'?-1:0),0);assert.equal(balance,0,name);}
 assert.match(spawnCondition(state.tracks[1].bulletActions[0],'age','i','gen'),/i mod 2=0/);
 assert.throws(()=>normalizeState({version:2,tracks:[track({spawnActions:[spawn('bad',{spawnRepeat:-1})]})]}),/生成模板/);
});
