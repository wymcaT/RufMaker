import fs from 'node:fs';
import path from 'node:path';
import {spawn,execFileSync} from 'node:child_process';
import {createProject} from '../../studio/project-files.mjs';
import {normalizeState,stateCode} from '../../studio/motion.mjs';
import {cameraUpdate} from '../../studio/camera.mjs';

const root=path.resolve(import.meta.dirname,'../..'),base=path.join(root,'build/preset-parameters-check');
const fixture=JSON.parse(fs.readFileSync(path.join(base,'scene.json'),'utf8'));
const made=createProject(root,{name:'GM Presets '+Date.now(),parent:base,preset:'floor',width:800,height:608,fps:50});
process.env.RUFMAKER_ROOT=root;process.env.RUFMAKER_PROJECT=made.path;
const project=await import('../../studio/project.mjs');
const state={...project.readState(),...fixture,directStart:true,randomizeOnPlay:false};project.syncToGame(state);
const times=[0,10,11,20,35,50,60,70,90,102,103,110,129,130,131,132,150,240,250,251,274,275,276,277],input=path.join(base,'gm-state.json'),expectedFile=path.join(base,'gm-expected.json');
fs.writeFileSync(input,JSON.stringify(state));execFileSync(path.join(base,'PresetParameters.tests.exe'),['--samples',input,times.join(','),expectedFile],{windowsHide:true});
const expected=JSON.parse(fs.readFileSync(expectedFile,'utf8'));
const runtime=normalizeState(structuredClone(state)).tracks,controller=path.join(project.project,'objects/objRufController.gml');
let code=fs.readFileSync(controller,'utf8'),next=code.indexOf('#define Step_0');
const fields=['rf_x','rf_y','rf_angle','rf_cameraZoom','rf_cameraBlur','rf_cameraBlurAngle','rf_cameraIntensity'];
const output=items=>items.map(item=>`+','+string_format(${item},0,8)`).join('');
const probes=runtime.map((t,i)=>`${stateCode(t,'rf_probe_frame')}file_text_write_string(rf_probe_file,string(rf_probe_frame)+','+string(${i})${output(fields)});${t.kind==='camera'?`${cameraUpdate({tracks:[t]})}file_text_write_string(rf_probe_file,''${output(['rf_cam_x','rf_cam_y','rf_cam_angle','rf_cam_zoom','rf_cam_blur','rf_cam_blur_angle'])});`:''}file_text_writeln(rf_probe_file);`).join('\n');
const dump=`var rf_probe_file,rf_probe_frame;rf_probe_file=file_text_open_write(working_directory+'\\preset-probe.csv');for(rf_probe_frame=0;rf_probe_frame<=277;rf_probe_frame+=1){if ${times.map(f=>'rf_probe_frame='+f).join(' || ')} {rf_frame=rf_probe_frame;${probes}}}file_text_close(rf_probe_file);game_end();\n`;
code=code.slice(0,next)+dump+code.slice(next);code=code.replace('rf_frame=0; rf_target=0;','window_set_visible(false);rf_frame=0; rf_target=0;').replace('FMODInstanceSetPaused(rf_music,0)','FMODInstanceSetPaused(rf_music,1)');fs.writeFileSync(controller,code);
const settings=path.join(project.project,'settings/settings.txt');fs.writeFileSync(settings,fs.readFileSync(settings,'utf8').replace('show_error_messages=1','show_error_messages=0').replace('always_abort=0','always_abort=1'));
const log=path.join(project.project,'compiler.log');fs.writeFileSync(log,'');
async function run(file,args,cwd,timeout){await new Promise((resolve,reject)=>{const child=spawn(file,args,{cwd,windowsHide:true,stdio:['ignore','pipe','pipe']});for(const stream of [child.stdout,child.stderr])stream.on('data',d=>fs.appendFileSync(log,d));const timer=setTimeout(()=>{child.kill();reject(Error('Timeout '+file));},timeout);child.on('error',e=>{clearTimeout(timer);reject(e);});child.on('close',c=>{clearTimeout(timer);c===0?resolve():reject(Error('Exit '+c+'; '+log));});});}
const exe=path.join(project.project,'preset-probe.exe');console.log('Project: '+project.project);
await run(project.gameMaker,[project.projectFile,'--build',exe],path.dirname(project.gameMaker),60000);await run(exe,[],project.project,30000);
const errors=path.join(project.project,'game_errors.log');if(fs.existsSync(errors)&&fs.readFileSync(errors,'utf8').trim())throw Error(fs.readFileSync(errors,'utf8'));
const rows=fs.readFileSync(path.join(project.project,'preset-probe.csv'),'utf8').trim().split(/\r?\n/).filter(Boolean);let compared=0;
for(const row of rows){const values=row.split(',').map(Number),frame=values[0],t=runtime[values[1]];if(!t||values.some(v=>!Number.isFinite(v)))throw Error('Invalid probe row: '+row);const want=expected[frame][t.id];
  const names=['x','y','angle','zoom','blur','blurAngle','intensity'];for(let i=0;i<names.length;i++)if(Math.abs(values[i+2]-want[names[i]])>.002)throw Error(`${t.id} frame ${frame} ${names[i]}: GM ${values[i+2]} vs C# ${want[names[i]]}`);
  if(t.kind==='camera'&&frame>=t.start&&frame<=t.end){const view=['viewX','viewY','viewAngle','viewZoom','viewBlur','viewBlurAngle'];for(let i=0;i<view.length;i++)if(Math.abs(values[i+9]-want[view[i]])>.002)throw Error(`${t.id} frame ${frame} ${view[i]}: GM ${values[i+9]} vs C# ${want[view[i]]}`);}
  compared++;
}
if(compared!==runtime.length*times.length)throw Error(`Missing rows: ${compared}/${runtime.length*times.length}`);
console.log(`PASS: ${compared} actual GM/C# poses across 14 motion presets, 12 camera presets and combined motion/camera, including adjusted parameters and camera output.`);
