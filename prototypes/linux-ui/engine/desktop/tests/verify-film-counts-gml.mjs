// Isolated GM8.2 probe for the scripts embedded in native film recordings.
import fs from 'node:fs';import path from 'node:path';import {fileURLToPath} from 'node:url';import {spawn} from 'node:child_process';import assert from 'node:assert/strict';
import {createProject} from '../../studio/project-files.mjs';import {writeFilmHarness} from '../../studio/native-film.mjs';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..'),base=path.join(root,'build/audio-statistics-check');
const created=createProject(root,{name:'Film counts GM '+Date.now(),parent:base,preset:'floor',width:800,height:608,fps:50});
process.env.RUFMAKER_ROOT=root;process.env.RUFMAKER_PROJECT=created.path;
const project=await import('../../studio/project.mjs');const state=project.readState();state.tracks=[];state.duration=10;project.syncToGame(state);
state.nativeProject={duration:10,controller:'objAd',clock:'time',hasFmod:false};writeFilmHarness(project.project,state);
const objects=path.join(project.project,'objects'),action='/*"/*\'/**//* YYD ACTION\nlib_id=1\naction_id=603\napplies_to=self\n*/';
fs.writeFileSync(path.join(objects,'objCountTarget.txt'),'sprite=\nvisible=0\nsolid=0\npersistent=0\ndepth=0\nparent=\nmask=\n');fs.writeFileSync(path.join(objects,'objCountTarget.gml'),'#define Create_0\n'+action+'\n');
for(const [file,line] of [['index.yyd','objCountTarget'],['tree.yyd','\t|objCountTarget']])fs.appendFileSync(path.join(objects,file),'\n'+line+'\n');
fs.writeFileSync(path.join(objects,'objRufNativePreview.gml'),`#define Create_0\n${action}\nwindow_set_visible(0);global.rf_native_frame=0;var rf_f;repeat(3)instance_create(20,20,objCountTarget);rf_f=file_text_open_write(working_directory+'\\counts-probe.txt');file_text_write_string(rf_f,rf_film_counts());file_text_writeln(rf_f);with(objCountTarget)instance_destroy();file_text_write_string(rf_f,rf_film_counts());file_text_writeln(rf_f);file_text_close(rf_f);rf_film_status(1);game_end();\n`);
const log=path.join(project.project,'probe-compile.log');fs.writeFileSync(log,'');
function run(file,args,cwd,timeout){return new Promise((resolve,reject)=>{const child=spawn(file,args,{cwd,windowsHide:true,stdio:['ignore','pipe','pipe']});for(const stream of [child.stdout,child.stderr])stream.on('data',d=>fs.appendFileSync(log,d));const timer=setTimeout(()=>{child.kill();reject(Error('Timeout; '+project.project));},timeout);child.once('error',e=>{clearTimeout(timer);reject(e);});child.once('close',code=>{clearTimeout(timer);code===0?resolve():reject(Error('Exit '+code+'; '+project.project));});});}
const exe=path.join(project.project,'count-probe.exe');await run(project.gameMaker,[project.projectFile,'--build',exe],path.dirname(project.gameMaker),60000);await run(exe,[],project.project,15000);
const errors=path.join(project.project,'game_errors.log');if(fs.existsSync(errors)&&fs.readFileSync(errors,'utf8').trim())throw Error(fs.readFileSync(errors,'utf8'));
const rows=fs.readFileSync(path.join(project.project,'counts-probe.txt'),'utf8').trim().split(/\r?\n/);assert(rows[0].includes('objCountTarget:3|'));assert(!rows[1].includes('objCountTarget:'));assert(!rows.some(r=>r.includes('objRufNativePreview:')));
const status=fs.readFileSync(path.join(project.project,'Data/rufmaker_film_status.txt'),'utf8');assert(status.includes('frame=0'));assert(status.includes('objects='));assert(!fs.existsSync(path.join(project.project,'Data/rufmaker_film_status.txt.tmp')));
console.log('PASS: GM8.2 executes live instance counting, spawn/destruction, recorder exclusion and atomic frame status. '+project.project);
