// Preview: desktop/Test-ArgonHandoff.ps1. Actual GM: runtime/node.exe <this file>.
// All fixtures and runtime output stay in build/argon-handoff-check/.
import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import {execFileSync} from 'node:child_process';
import {normalizeState} from '../../studio/motion.mjs';

const root=path.resolve(import.meta.dirname,'../..'),base=path.join(root,'build/argon-handoff-check');
const frames=[0,1,2,3,4,5,6,7,9,10,11,12,13,14,15,20,25,30,35,40,50,60];
const tracks=[];
function add(t){tracks.push({id:t.id,name:t.id,kind:'rod',enabled:true,lane:tracks.length,start:0,end:70,x:400,y:304,sprite:'sprCherry',count:10,interval:25,shape:'star',radius:100,shapeInner:.4,speed:5,life:90,lifetimeMode:'fixed',releaseFrame:10,releaseKeepShape:true,keyframes:[],...t});}
add({id:'root-star'});
add({id:'uniform-star',releaseKeepShape:false});
add({id:'root-line',shape:'rod',count:5,length:200,speed:4});
add({id:'spawned-star',templateOnly:true,start:20,end:50,releaseFrame:27,x:400,y:304});
add({id:'star-host',kind:'object',start:3,end:3,x:150,y:180,angle:35,spawnActions:[{type:'spawn',templateId:'spawned-star',start:0,duration:0,inheritAngle:true,maxGeneration:1}]});
add({id:'orbit-shot',kind:'emitter',lane:8,start:0,end:0,x:400,y:204,count:1,speed:0,releaseFrame:-1});
add({id:'orbit-event',kind:'control',lane:9,start:5,end:5,eventPreset:'group-orbit',eventGroup:'orbit',eventCenterX:400,eventCenterY:304,eventOrbitSpeed:3,eventRadiusMode:'keep',eventResetLife:true,eventLife:30});
add({id:'no-reset-shot',kind:'emitter',lane:10,start:0,end:0,x:500,y:304,count:1,speed:0,releaseFrame:-1});
add({id:'no-reset-event',kind:'control',lane:11,start:5,end:5,eventPreset:'group-orbit',eventGroup:'no-reset',eventCenterX:400,eventCenterY:304,eventOrbitSpeed:-2,eventRadiusMode:'fixed',eventRadius:60,eventResetLife:false});
add({id:'orbit-rod',lane:12,start:0,end:50,x:300,y:304,count:5,shape:'ring',radius:40,releaseFrame:-1});
add({id:'orbit-rod-event',kind:'control',lane:13,start:6,end:6,eventPreset:'group-orbit',eventGroup:'rod',eventCenterX:300,eventCenterY:304,eventOrbitSpeed:5,eventRadiusMode:'keep',eventResetLife:true,eventLife:40});
add({id:'reset-orbit',kind:'control',lane:14,start:15,end:15,eventPreset:'group-set',eventGroup:'orbit',eventRestoreBirth:false,controlValue:2,controlFrom:0,eventAcceleration:.2,eventFriction:.1,eventTurn:0,eventResetLife:true,eventLife:30});
add({id:'partial-star',lane:15,x:180,y:400,releaseFrame:12,spawnActions:[{type:'spawn',templateId:'short-shot',start:2,duration:0,spawnOrigin:'each',spawnParity:'even',destroyParent:true,maxGeneration:1}]});
add({id:'short-shot',lane:16,kind:'emitter',templateOnly:true,start:0,end:0,x:0,y:0,count:1,speed:0,life:2,releaseFrame:-1});
add({id:'partial-event-star',lane:17,x:600,y:400,releaseFrame:-1,spawnActions:[{type:'spawn',templateId:'short-shot',start:2,duration:0,spawnOrigin:'each',spawnParity:'even',destroyParent:true,maxGeneration:1}]});
add({id:'partial-reset',lane:18,kind:'control',start:12,end:12,eventPreset:'group-set',eventGroup:'partial',eventRestoreBirth:true,eventResetLife:true,eventLife:40});
const scene=normalizeState({version:2,name:'Argon handoff runtime checks',room:'rAvoidance',duration:100,fps:50,randomSeed:781,nativeTest:{x:400,y:500},tracks,
  timelineGroups:[{id:'orbit',name:'Orbit reset',color:'#555577',lanes:[8,9,14]},{id:'no-reset',name:'Orbit keep age',color:'#775555',lanes:[10,11]},{id:'rod',name:'Orbit formation',color:'#557755',lanes:[12,13]},{id:'partial',name:'Partial release',color:'#777755',lanes:[17,18]}]});
fs.mkdirSync(base,{recursive:true});
fs.writeFileSync(path.join(base,'scene.json'),JSON.stringify(scene));
if(process.argv.includes('--prepare'))process.exit(0);

if(process.argv.includes('--preview')){
  const samples=path.join(base,'preview.json');
  execFileSync(path.join(base,'ArgonHandoff.tests.exe'),['--samples',path.join(base,'scene.json'),frames.join(','),samples],{windowsHide:true});
  const rows=JSON.parse(fs.readFileSync(samples,'utf8'));
  const bullet=(f,id,index=0)=>rows[f].find(b=>b.source===id&&b.index===index&&!b.kinematic);
  const near=(a,b)=>assert.ok(Math.abs(a-b)<.002,`${a} != ${b}`);
  near(bullet(5,'orbit-shot').x,400);near(bullet(5,'orbit-shot').y,204);
  near(bullet(6,'orbit-shot').x,400+100*Math.cos(93*Math.PI/180));
  near(bullet(6,'orbit-shot').y,304-100*Math.sin(93*Math.PI/180));
  near(bullet(10,'root-line',2).speed,0);
  for(const b of rows[10].filter(b=>b.source==='root-star')){
    const r=Math.hypot(b.x-400,b.y-304);near(b.speed,r/20);
  }
  for(const b of rows[10].filter(b=>b.source==='spawned-star'&&!b.kinematic))assert.ok(b.speed<=5.002,'A translated/rotated copy exceeded the configured outer speed');
  for(const id of ['partial-star','partial-event-star']){
    const released=rows[12].filter(b=>b.source===id&&!b.kinematic);
    assert.equal(released.length,5,'Destroying alternate tips must leave five inner members');
    for(const b of released)near(b.speed,2);
  }
  assert.ok(rows[6].filter(b=>b.source==='orbit-rod').every(b=>!b.kinematic),'Orbit event did not detach the formation');
  assert.ok(!rows[50].some(b=>b.source==='orbit-shot'),'Reset lifetime did not expire');
  console.log('PASS: release geometry, translated copies, orbit reset boundary, fixed radius, formation detachment and lifetime.');
}else{
  process.env.RUFMAKER_TEST_BUILD='argon-handoff-check';
  process.env.RUFMAKER_TEST_SAMPLER='ArgonHandoff.tests.exe';
  process.env.RUFMAKER_TEST_FRAMES=frames.join(',');
  await import('./verify-template-gml.mjs');
}
