import fs from 'node:fs';import path from 'node:path';import assert from 'node:assert/strict';import {createProject} from '../../studio/project-files.mjs';
const root=path.resolve(import.meta.dirname,'../..'),base=path.join(root,'build/play-copy-performance-check');fs.mkdirSync(base,{recursive:true});
const selected=process.argv[2]||createProject(root,{name:'Plain play '+Date.now(),parent:base,preset:'floor',width:800,height:608,fps:50}).path;
assert.ok(path.resolve(selected).toLowerCase().startsWith(path.join(root,'build').toLowerCase()+path.sep),'Use an isolated build project');
process.env.RUFMAKER_ROOT=root;process.env.RUFMAKER_PROJECT=selected;const p=await import('../../studio/project.mjs');if(!process.argv[2]){const state=p.readState();state.duration=3200;state.tracks=[{id:'burst',name:'burst',kind:'emitter',enabled:true,start:0,end:3000,interval:5,count:12,life:180,speed:2,advancedBullets:true,sprite:'sprBullet',keyframes:[]}];p.syncToGame(state);}
if(process.argv[2])p.syncToGame(p.readState());const settings=path.join(p.project,'settings/settings.txt');fs.writeFileSync(settings,fs.readFileSync(settings,'utf8').replace('show_error_messages=1','show_error_messages=0').replace('always_abort=0','always_abort=1'));
const n=await import('../../studio/native.mjs'),target=Math.min(1899,p.readState().duration),seen=[];const poll=setInterval(()=>{const s=n.nativeRunStatus();if(s)seen.push({...s});},25);let result;
try{result=await n.runNative(target);}finally{clearInterval(poll);}
assert.equal(result.ready,true);assert.equal(Number(result.status.ready),1);assert.equal(Number(result.status.frame),target);assert.ok(seen.some(s=>s.phase==='launch'));assert.ok(seen.some(s=>s.phase==='replay'&&s.frame<target),'Missing partial replay progress');try{process.kill(result.pid);}catch{}
const start=n.runNative(target),timer=setTimeout(()=>n.cancelNativeRun(),150);try{await assert.rejects(start,/取消/);}finally{clearTimeout(timer);await n.cancelNativeRun();}
console.log('PASS: plain F5 reports launch and partial replay progress, waits for ready=1 at the requested frame and cancels its test process.');
