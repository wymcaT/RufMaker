import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import {spawn} from 'node:child_process';
import {importNativeProject} from '../../studio/native-import.mjs';

const root=path.resolve(import.meta.dirname,'../..'),base=path.join(root,'build/optimization-check');
const mode=process.argv[2];assert.ok(['large','native'].includes(mode),'Choose large or native');
function files(dir){return fs.readdirSync(dir,{withFileTypes:true}).flatMap(e=>e.isDirectory()?files(path.join(dir,e.name)):[path.join(dir,e.name)]);}
const digest=file=>crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
let descriptor,original;
if(mode==='large')descriptor=JSON.parse(fs.readFileSync(path.join(base,'save-project.json'),'utf8')).path;
else {
  const source=path.join(root,'non-ruf projects/Verse3');original=new Map(files(source).filter(f=>/\.(?:gml|txt|gm82|yyd)$/.test(f)).map(f=>[f,digest(f)]));
  descriptor=importNativeProject(root,{entry:path.join(source,'inverse.gm82'),parent:base,name:'Native Sync '+Date.now()}).path;
}
process.env.RUFMAKER_ROOT=root;process.env.RUFMAKER_PROJECT=descriptor;
const p=await import('../../studio/project.mjs');const state=p.readState();
if(mode==='native'){
  assert.ok(state.nativeProject.imported&&state.nativeProject.events.length>0);const before=new Map(files(p.project).filter(f=>!f.endsWith('rufmaker.json')).map(f=>[f,digest(f)]));
  p.syncToGame(state);for(const [file,value]of before)assert.equal(digest(file),value,'Native-only sync altered '+file);
  const controller=path.join(p.project,'objects',state.nativeProject.controller+'.gml'),code=fs.readFileSync(controller,'utf8');
  state.tracks.push({id:'overlay',kind:'emitter',name:'Overlay',sprite:state.nativeProject.defaultSprite,start:0,end:50,count:6,interval:10,life:50,speed:2,keyframes:[]});
  state.timelineGroups=[{id:'high',name:'High lane',color:'#557799',lanes:[300]}];state.tracks.at(-1).lane=300;p.syncToGame(state);
  const reopened=p.readState();assert.equal(reopened.tracks.length,2);assert.deepEqual(reopened.timelineGroups[0].lanes,[300]);assert.equal(fs.readFileSync(controller,'utf8'),code,'Native controller changed');
  for(const [file,value]of original)assert.equal(digest(file),value,'Source project was modified');
  console.log('PASS: Verse3 import, native-only no-op sync, overlay/group save/reopen and original code preserved.');
} else {
  assert.equal(state.tracks.length,1000);state.directStart=true;p.syncToGame(state);
  const controller=path.join(p.project,'objects/objRufController.gml');let code=fs.readFileSync(controller,'utf8');
  code=code.replace('#define Step_0',"global.rf_optimization_draws=0;window_set_visible(false);\n#define Step_0");
  code=code.replace('#define Draw_0',"if rf_frame>=3 {var rf_probe_file;rf_probe_file=file_text_open_write(working_directory+'\\optimization-probe.csv');file_text_write_string(rf_probe_file,string(rf_frame)+','+string(global.rf_optimization_draws));file_text_close(rf_probe_file);game_end();}\n#define Draw_0");
  code+='\nglobal.rf_optimization_draws+=1;\n';code=code.replace('FMODInstanceSetPaused(rf_music,0)','FMODInstanceSetPaused(rf_music,1)');fs.writeFileSync(controller,code);
}
const settings=path.join(p.project,'settings/settings.txt');fs.writeFileSync(settings,fs.readFileSync(settings,'utf8').replace('show_error_messages=1','show_error_messages=0').replace('always_abort=0','always_abort=1'));
const log=path.join(p.project,'optimization-compiler.log');fs.writeFileSync(log,'');
async function run(file,args,cwd,timeout){await new Promise((resolve,reject)=>{const child=spawn(file,args,{cwd,windowsHide:true,stdio:['ignore','pipe','pipe']});for(const stream of [child.stdout,child.stderr])stream.on('data',data=>fs.appendFileSync(log,data));const timer=setTimeout(()=>{child.kill();reject(Error('Timeout '+file));},timeout);child.on('error',e=>{clearTimeout(timer);reject(e);});child.on('close',c=>{clearTimeout(timer);c===0?resolve():reject(Error('Exit '+c+'; '+log));});});}
console.log('Project: '+p.project);const exe=path.join(p.project,'optimization-probe.exe');await run(p.gameMaker,[p.projectFile,'--build',exe],path.dirname(p.gameMaker),60000);
if(mode==='large'){
  await run(exe,[],p.project,15000);const errors=path.join(p.project,'game_errors.log');if(fs.existsSync(errors))assert.equal(fs.readFileSync(errors,'utf8').trim(),'');
  const [frame,draws]=fs.readFileSync(path.join(p.project,'optimization-probe.csv'),'utf8').split(',').map(Number);assert.ok(frame>=3&&draws>=1);console.log('PASS: 1,000-object GameMaker project compiled, started and executed normal Draw events.');
}else console.log('PASS: imported Verse3 with RufMaker overlay compiled in GameMaker 8.2 (compile check; native playthrough not repeated).');
assert.ok(!fs.existsSync(path.join(p.project,'.rufmaker-sync')),'Unfinished sync journal');
