import fs from 'node:fs';
import path from 'node:path';
import {normalizeState} from '../../studio/motion.mjs';
import {expandPatterns} from '../../studio/bullet-schema.mjs';
const root=path.resolve(import.meta.dirname,'../..'),base=path.join(root,'build/object-dynamics-check');
const tracks=[];
function add(t){tracks.push({id:t.id,name:t.id,kind:'emitter',enabled:true,start:0,end:0,lane:tracks.length,x:200,y:200,sprite:'sprCherry',count:2,interval:20,life:70,lifetimeMode:'fixed',speed:3,angle:30,keyframes:[],...t});}
add({id:'gravity',bulletGravity:.4,bulletFriction:.02,bulletAcceleration:.03,imageIndex:2,imageSpeed:.25,expressions:{bulletGravity:'choose(.3,.4,.5)',imageIndex:'choose(1,2,3)'}});
add({id:'animation',start:3,end:3,imageIndex:7,imageSpeed:-.5,bulletFriction:.1,indexDelay:2});
add({id:'release',kind:'rod',shape:'star',x:500,y:250,count:10,start:0,end:50,radius:40,shapeInner:.4,releaseFrame:7,releaseDirection:'radial',releaseKeepShape:true,bulletGravity:.15,imageSpeed:.25});
add({id:'template',kind:'rod',templateOnly:true,shape:'ring',x:400,y:304,count:5,start:20,end:45,releaseFrame:27,radius:35,speed:2,bulletGravity:.2,imageIndex:1,imageSpeed:.2});
add({id:'host',kind:'object',start:3,end:3,x:100,y:100,spawnActions:[{type:'spawn',templateId:'template',start:0,duration:0,maxGeneration:1}]});
add({id:'reset',kind:'control',lane:7,start:12,end:12,eventPreset:'group-set',eventGroup:'gravity',eventRestoreBirth:false,controlValue:2,controlFrom:0,eventResetLife:true,eventLife:40});
add({id:'clip',kind:'pattern',start:20,end:45,repeatCount:1,timeScale:2,children:[{id:'child',name:'child',kind:'emitter',start:0,end:20,x:500,y:100,sprite:'sprCherry',count:1,interval:10,life:70,speed:2,bulletGravity:.1,imageSpeed:.2,keyframes:[{frame:0,property:'bulletFriction',value:.01},{frame:20,property:'bulletFriction',value:.03}]}]});
const scene=normalizeState({version:2,name:'Object physics and animation',room:'rAvoidance',duration:100,fps:50,randomSeed:781,nativeTest:{x:400,y:500},tracks,timelineGroups:[{id:'gravity',name:'Gravity',color:'#557755',lanes:[0,7]}]});
// The shared GM probe uses source indices, so give it the final expanded order.
fs.mkdirSync(base,{recursive:true});fs.writeFileSync(path.join(base,'scene.json'),JSON.stringify(normalizeState(expandPatterns(scene))));
if(!process.argv.includes('--prepare')){
  process.env.RUFMAKER_TEST_BUILD='object-dynamics-check';process.env.RUFMAKER_TEST_SAMPLER='ObjectDynamics.tests.exe';process.env.RUFMAKER_TEST_FRAMES='0,1,2,3,4,5,6,7,8,9,10,11,12,13,15,20,25,30,40,50,60';
  await import('./verify-template-gml.mjs');
}
