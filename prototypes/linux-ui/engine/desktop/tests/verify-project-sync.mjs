import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import {createProject} from '../../studio/project-files.mjs';
const root=path.resolve(import.meta.dirname,'../..'),folder=path.join(root,'build/optimization-check');fs.mkdirSync(folder,{recursive:true});
const made=createProject(root,{name:'Sync '+Date.now(),parent:folder,preset:'floor',width:800,height:608,fps:50});process.env.RUFMAKER_ROOT=root;process.env.RUFMAKER_PROJECT=made.path;
const p=await import('../../studio/project.mjs');const state=p.readState();
state.tracks=[{id:'review-variable',name:'variable',kind:'variable',enabled:true,start:0,end:50,variableName:'review_value',variableExpression:'10',keyframes:[]}];p.syncToGame(state);
function files(directory){return fs.readdirSync(directory,{withFileTypes:true}).flatMap(e=>e.isDirectory()?files(path.join(directory,e.name)):[path.join(directory,e.name)]);}
const before=new Map(files(p.project).map(file=>[file,fs.readFileSync(file)]));state.tracks[0].variableExpression='20';const music=state.music;state.music='Data/Music/DoesNotExistForReview.ogg';assert.throws(()=>p.syncToGame(state),/音乐不存在/);
assert.deepEqual(files(p.project).sort(),[...before.keys()].sort());for(const [file,bytes]of before)assert.ok(fs.readFileSync(file).equals(bytes),file+' changed after failed save');
state.music=music;p.syncToGame(state);assert.equal(p.readState().tracks[0].variableExpression,'20');
for(const count of [201,1000]){state.tracks=Array.from({length:count},(_,i)=>({id:'object-'+i,name:'Object '+i,kind:'object',sprite:'sprBullet',enabled:true,lane:i,start:0,end:20,keyframes:[]}));state.timelineGroups=[{id:'large',name:'large',color:'#557799',lanes:[count-1]}];const begin=performance.now();p.syncToGame(state);const saved=p.readState();assert.equal(saved.tracks.length,count);assert.deepEqual(saved.timelineGroups[0].lanes,[count-1]);console.log('PASS: '+count+' editable objects saved/reopened with group membership in '+(performance.now()-begin).toFixed(1)+'ms');}
const result={path:made.path,project:p.project,objects:1000};fs.writeFileSync(path.join(folder,'save-project.json'),JSON.stringify(result,null,2));console.log('PASS: missing-song failure leaves all project files unchanged; valid retry and large-object save/reopen.');
