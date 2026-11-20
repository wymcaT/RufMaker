import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {spawnSync} from 'node:child_process';
import {createProject} from '../../studio/project-files.mjs';

const root=path.resolve(import.meta.dirname,'../..');
const parent=path.join(root,'build','effect-history-check');
fs.mkdirSync(parent,{recursive:true});
const fixture=createProject(root,{parent,name:'History '+Date.now(),preset:'floor',fps:50,width:800,height:608});
process.env.RUFMAKER_ROOT=root;
process.env.RUFMAKER_PROJECT=fixture.path;
const p=await import('../../studio/project.mjs');
const file=name=>path.join(p.project,name);
const state=p.readState();
state.duration=100;
state.directStart=true;
state.tracks=[
  {id:'blur',kind:'effect',effect:'motion-blur',start:1,end:25,strength:0,fxRetention:.5,fxSamples:2,keyframes:[]},
  {id:'hold',kind:'effect',effect:'glitch',start:24,end:27,fxOpacity:1,keyframes:[]}
];
const steps=[[1,'c_red'],[2,'c_blue'],[2,'c_blue'],[2,'c_lime'],[3,'c_black'],[1,'c_white'],[20,'c_blue'],[25,'c_red'],[26,'c_red'],[28,'c_black'],[22,'c_red'],[23,'c_lime']];
function run(exe,args,cwd) {
  const r=spawnSync(exe,args,{cwd,windowsHide:true,timeout:50000,encoding:'utf8'});
  fs.appendFileSync(file('history-probe.log'),[exe,r.stdout,r.stderr,r.error,r.status].join('\n')+'\n');
  if(r.error)throw r.error;
  assert.equal(r.status,0,'See '+file('history-probe.log'));
}
console.log('Isolated project: '+p.project);
for(const mode of ['plain','imported']){
  if(mode==='imported')state.nativeProject={imported:true,defaultSprite:'sprBullet',killerParent:'playerKiller',duration:100};
  p.syncToGame(state);
  const settings=file('settings/settings.txt');
  fs.writeFileSync(settings,fs.readFileSync(settings,'utf8').replace('show_error_messages=1','show_error_messages=0').replace('always_abort=0','always_abort=1'));
  fs.writeFileSync(file('scripts/rf_custom_create.gml'),'window_set_visible(false);rf_probe_tick=-1;rf_probe_color=c_black;');
  fs.writeFileSync(file('scripts/rf_custom_draw.gml'),'draw_set_alpha(1);draw_set_color(rf_probe_color);draw_rectangle(60,60,140,140,0);draw_set_color(c_white);');
  // Sample the completed pass on the following Step, before the next Draw.
  fs.writeFileSync(file('scripts/rf_custom_step.gml'),`
if rf_probe_tick>=0 {
 var rf_out,rf_pixel;
 rf_pixel=0;if surface_exists(rf_fx_a) rf_pixel=surface_getpixel(rf_fx_a,100,100);
 rf_out=file_text_open_append(working_directory+'\\${mode}.csv');
 file_text_write_string(rf_out,string(rf_pixel)+','+string(rf_fx_history[0])+','+string(rf_fx_prior[0])+','+string(rf_fx_history[1]));file_text_writeln(rf_out);file_text_close(rf_out);
}
rf_probe_tick+=1;
switch(rf_probe_tick){
${steps.map(([frame,color],i)=>`case ${i}: rf_frame=${frame};rf_probe_color=${color};break;`).join('\n')}
default: game_end();break;
}
if rf_probe_tick=11 rf_fx_w=room_width-1;`);
  const exe=file('probe-'+mode+'.exe');
  run(p.gameMaker,[p.projectFile,'--build',exe],path.dirname(p.gameMaker));
  run(exe,[],p.project);
  const errors=file('game_errors.log');
  if(fs.existsSync(errors))assert.equal(fs.readFileSync(errors,'utf8').trim(),'');
  const rows=fs.readFileSync(file(mode+'.csv'),'utf8').trim().split(/\r?\n/).map(line=>line.split(',').map(Number));
  assert.equal(rows.length,steps.length);
  const rgb=rows.map(([pixel])=>[pixel&255,(pixel>>8)&255,(pixel>>16)&255]);
  assert.ok(rgb[0][0]>245&&rgb[0][2]===0,mode+' first frame '+rgb[0]);
  assert.ok(rgb[1][0]>80&&rgb[1][2]>80,mode+' blended frame '+rgb[1]);
  assert.deepEqual(rgb[2],rgb[1],mode+' repeat');
  assert.ok(rgb[3][0]===rgb[1][0]&&rgb[3][1]>80&&rgb[3][2]===0,mode+' changed redraw '+rgb[3]);
  assert.ok(rgb[4][1]>20&&rgb[4][2]===0,mode+' next frame '+rgb[4]);
  assert.ok(rgb[5].every(v=>v>245),mode+' reverse');
  assert.ok(rgb[6][0]===0&&rgb[6][2]>245,mode+' seek');
  assert.ok(rows[7][1]>=0&&rows[7][3]>=0,mode+' overlap');
  assert.ok(rows[8][1]===-1&&rows[8][2]===-1&&rows[8][3]>=0,mode+' expired blur');
  assert.deepEqual(rows[9].slice(1),[-1,-1,-1],mode+' inactive');
  assert.ok(rgb[11][0]===0&&rgb[11][1]>245&&rows[11][2]===-1,mode+' resized targets');
  console.log('PASS: '+mode+' compositor, '+rows.length+' temporal samples.');
}
