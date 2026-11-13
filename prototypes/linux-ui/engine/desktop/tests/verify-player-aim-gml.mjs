import fs from 'node:fs';
import path from 'node:path';
import {spawn} from 'node:child_process';
import {createProject} from '../../studio/project-files.mjs';
import {playerAt} from '../../studio/random-parameters.mjs';
import {gmlNumber} from '../../studio/gml.mjs';

const root=path.resolve(import.meta.dirname,'../..'),folder=path.join(root,'build/player-aim-check');
const fixture=JSON.parse(fs.readFileSync(path.join(folder,'scene.json'),'utf8'));
const expected=JSON.parse(fs.readFileSync(path.join(folder,'expected.json'),'utf8'));
const made=createProject(root,{name:'GM Player Aim '+Date.now(),parent:folder,preset:'floor',width:800,height:608,fps:50});
process.env.RUFMAKER_ROOT=root;process.env.RUFMAKER_PROJECT=made.path;
const p=await import('../../studio/project.mjs');
const state={...p.readState(),...fixture,directStart:true,randomizeOnPlay:false};p.syncToGame(state);
const controller=path.join(p.project,'objects/objRufController.gml');let code=fs.readFileSync(controller,'utf8');
const dump=`var rf_probe_file,rf_probe_frame;rf_probe_file=file_text_open_write(working_directory+'\\player-aim-probe.csv');with(objRufTemplate)instance_destroy();with(objRufBullet)instance_destroy();rf_global_init();rf_template_init();if !instance_exists(player) instance_create(400,304,player);
${Array.from({length:61},(_,frame)=>{const player=playerAt(state,frame);return `with(player){x=${gmlNumber(player.x)};y=${gmlNumber(player.y)};}rf_frame=${frame};rf_pattern_step(${frame});global.rf_probe_frame=${frame};global.rf_probe_file=rf_probe_file;with(objRufBullet)if self.rf_system && !self.rf_kinematic && self.rf_birth<=global.rf_probe_frame {file_text_write_string(global.rf_probe_file,string(global.rf_probe_frame)+','+string(self.rf_source)+','+string(self.rf_birth)+','+string(self.rf_index)+','+string(self.rf_generation)+','+string_format(x,0,8)+','+string_format(y,0,8)+','+string_format(self.rf_dir,0,8)+','+string_format(self.rf_speed,0,8));file_text_writeln(global.rf_probe_file);}`;}).join('\n')}
file_text_close(rf_probe_file);game_end();
`;
const next=code.indexOf('#define Step_0');code=code.slice(0,next)+dump+code.slice(next);
code=code.replace('rf_frame=0; rf_target=0;','window_set_visible(false);rf_frame=0; rf_target=0;').replace('FMODInstanceSetPaused(rf_music,0)','FMODInstanceSetPaused(rf_music,1)');fs.writeFileSync(controller,code);
const settings=path.join(p.project,'settings/settings.txt');fs.writeFileSync(settings,fs.readFileSync(settings,'utf8').replace('show_error_messages=1','show_error_messages=0').replace('always_abort=0','always_abort=1'));
const log=path.join(p.project,'compiler.log');fs.writeFileSync(log,'');
async function run(file,args,cwd,timeout){await new Promise((resolve,reject)=>{const child=spawn(file,args,{cwd,windowsHide:true,stdio:['ignore','pipe','pipe']});for(const stream of [child.stdout,child.stderr])stream.on('data',data=>fs.appendFileSync(log,data));const timer=setTimeout(()=>{child.kill();reject(Error('Timeout '+file));},timeout);child.on('error',error=>{clearTimeout(timer);reject(error);});child.on('close',status=>{clearTimeout(timer);status===0?resolve():reject(Error('Exit '+status+'; '+log));});});}
const exe=path.join(p.project,'player-aim-probe.exe');console.log('Project: '+p.project);
await run(p.gameMaker,[p.projectFile,'--build',exe],path.dirname(p.gameMaker),60000);
await run(exe,[],p.project,30000);
const errors=path.join(p.project,'game_errors.log');if(fs.existsSync(errors)&&fs.readFileSync(errors,'utf8').trim())throw Error(fs.readFileSync(errors,'utf8'));
const actual=Object.fromEntries(Array.from({length:61},(_,i)=>[i,[]]));
for(const line of fs.readFileSync(path.join(p.project,'player-aim-probe.csv'),'utf8').trim().split(/\r?\n/).filter(Boolean)){const a=line.split(',').map(Number);actual[a[0]].push({source:state.tracks[a[1]].id,birth:a[2],index:a[3],generation:a[4],x:a[5],y:a[6],direction:a[7],speed:a[8]});}
fs.writeFileSync(path.join(folder,'actual-gm.json'),JSON.stringify(actual));
const key=b=>[b.source,b.birth,b.index,b.generation].join('|');let count=0;
for(let frame=0;frame<=60;frame++){const a=actual[frame].sort((a,b)=>key(a).localeCompare(key(b))),e=expected[frame].sort((a,b)=>key(a).localeCompare(key(b)));if(a.length!==e.length)throw Error('Count '+frame+': '+a.length+'/'+e.length);for(let i=0;i<a.length;i++)for(const field of Object.keys(e[i])){const x=a[i][field],y=e[i][field],delta=field==='direction'?Math.abs(((x-y)%360+540)%360-180):Math.abs(x-y);if(typeof y==='number'?delta>.002:x!==y)throw Error(`${frame} ${key(e[i])} ${field}: ${x}/${y}`);}count+=a.length;}
console.log(`PASS: ${count} actual GM/C# bullet states across 61 moving-player frames, including delayed aimed shots, released formations, generated templates, age/global aim, expressions and continuous/limited homing.`);
