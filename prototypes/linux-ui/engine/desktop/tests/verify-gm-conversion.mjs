// Actual native built-in movement versus the converted RufMaker source.
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {spawn} from 'node:child_process';
import {createProject} from '../../studio/project-files.mjs';
import {editGmAsset} from '../../studio/gm-resources.mjs';
import {convertGmObject} from '../../studio/gm-convert.mjs';
const root=path.resolve(import.meta.dirname,'../..'),base=path.join(root,'build/object-dynamics-check');
const made=createProject(root,{name:'Native conversion '+Date.now(),parent:base,preset:'floor',width:800,height:608,fps:50});
process.env.RUFMAKER_ROOT=root;process.env.RUFMAKER_PROJECT=made.path;
const p=await import('../../studio/project.mjs');p.ensureRainbowSprite();
const action=`/*"/*'/**//* YYD ACTION\nlib_id=1\naction_id=603\napplies_to=self\n*/\n`;
editGmAsset(p.project,{kind:'objects',name:'objConvertProbe',operation:'create'});
const meta=path.join(p.project,'objects/objConvertProbe.txt');fs.writeFileSync(meta,fs.readFileSync(meta,'utf8').replace(/^sprite=.*$/m,'sprite=sprrainbowcherry'));
fs.writeFileSync(path.join(p.project,'objects/objConvertProbe.gml'),'#define Create_0\n'+action+'speed=3;direction=30;gravity=.4;gravity_direction=270;friction=.02;image_speed=.25;image_index=2;image_alpha=.8;\n');
const result=convertGmObject(p.project,{name:'objConvertProbe',start:0,end:99,x:200,y:200});assert.equal(result.supported,true,JSON.stringify(result));
const state=p.readState();state.duration=100;state.tracks=[result.track];state.directStart=true;p.syncToGame(state);
const controller=path.join(p.project,'objects/objRufController.gml');let source=fs.readFileSync(controller,'utf8');
const init="window_set_visible(false);rf_native_probe=instance_create(200,200,objConvertProbe);rf_probe_file=file_text_open_write(working_directory+'\\conversion.csv');global.rf_probe_file=rf_probe_file;global.rf_conversion_open=1;\n";
source=source.replace('#define Step_0',init+'#define Step_0');
const nativeValues=['x','y','speed','direction','image_index','image_alpha'].map(k=>`string_format(${k},0,8)`).join("+','+");
fs.appendFileSync(path.join(p.project,'objects/objConvertProbe.gml'),'\n#define Draw_0\n'+action+`if !global.rf_conversion_open exit;file_text_write_string(global.rf_probe_file,'O,'+string(objRufController.rf_frame)+','+${nativeValues});file_text_writeln(global.rf_probe_file);draw_self();\n`);
const report=`if rf_frame>20 {global.rf_conversion_open=0;file_text_close(rf_probe_file);game_end();exit;}global.rf_probe_file=rf_probe_file;global.rf_probe_frame=rf_frame;with(objRufBullet)if rf_system {file_text_write_string(global.rf_probe_file,'B,'+string(global.rf_probe_frame)+','+string_format(x,0,8)+','+string_format(y,0,8)+','+string_format(self.rf_speed,0,8)+','+string_format(self.rf_dir,0,8)+','+string_format(image_index,0,8)+','+string_format(image_alpha,0,8));file_text_writeln(global.rf_probe_file);}\n`;
const endStep='#define Step_2\n'+action;assert.ok(source.includes(endStep));source=source.replace(endStep,endStep+report);fs.writeFileSync(controller,source);
const settings=path.join(p.project,'settings/settings.txt');fs.writeFileSync(settings,fs.readFileSync(settings,'utf8').replace('show_error_messages=1','show_error_messages=0').replace('always_abort=0','always_abort=1'));
const log=path.join(p.project,'conversion.log');
async function run(file,args,cwd){await new Promise((resolve,reject)=>{const child=spawn(file,args,{cwd,windowsHide:true,stdio:['ignore','pipe','pipe']});for(const stream of [child.stdout,child.stderr])stream.on('data',d=>fs.appendFileSync(log,d));const timer=setTimeout(()=>{child.kill();reject(Error('Timed out: '+log));},60000);child.on('error',e=>{clearTimeout(timer);reject(e);});child.on('close',code=>{clearTimeout(timer);code===0?resolve():reject(Error('Exit '+code+': '+log));});});}
const exe=path.join(p.project,'conversion.exe');await run(p.gameMaker,[p.projectFile,'--build',exe],path.dirname(p.gameMaker));await run(exe,[],p.project);
const errors=path.join(p.project,'game_errors.log');if(fs.existsSync(errors)&&fs.readFileSync(errors,'utf8').trim())throw Error(fs.readFileSync(errors,'utf8'));
const rows=fs.readFileSync(path.join(p.project,'conversion.csv'),'utf8').trim().split(/\r?\n/).map(s=>s.split(',')),native=new Map(rows.filter(r=>r[0]==='O').map(r=>[+r[1],r.slice(2).map(Number)]));
const converted=rows.filter(r=>r[0]==='B');assert.equal(converted.length,20);
for(const row of converted){const frame=+row[1],a=row.slice(2).map(Number),b=native.get(frame);for(let i=0;i<a.length;i++){const actual=i===4?Math.floor(b[i]):b[i];assert.ok(Math.abs(a[i]-actual)<.002,`frame ${frame}, field ${i}: converted ${a[i]}, native ${actual}; ${p.project}`);}}
console.log('PASS: native and converted position, speed, direction, animation and opacity agree for 20 actual GM frames. '+p.project);
