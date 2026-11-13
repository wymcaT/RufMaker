// Run after desktop/Test-FormationShapes.ps1. Creates an isolated project under build/.
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {spawn} from 'node:child_process';
import {createProject} from '../../studio/project-files.mjs';
import {normalizeState,stateCode} from '../../studio/motion.mjs';
import {shapeSetup,shapePoint,formationPoint} from '../../studio/shapes.mjs';
import {rainbowCode,rodSequence} from '../../studio/rainbow.mjs';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..'),base=path.join(root,'build/formation-shapes-check');
const fixture=JSON.parse(fs.readFileSync(path.join(base,'fixtures.json'),'utf8'));
const created=createProject(root,{name:'Formation GM '+Date.now(),parent:base,preset:'floor',width:800,height:608,fps:50});
process.env.RUFMAKER_ROOT=root;process.env.RUFMAKER_PROJECT=created.path;
const project=await import('../../studio/project.mjs');const state=normalizeState({...project.readState(),tracks:fixture.state.tracks,duration:200});project.syncToGame(state);
for(const name of ['heart','star2']){
  const file=path.join(project.project,'scripts',name+'.gml'),original=fs.readFileSync(path.join(root,'assets/gml/formations',name+'.gml'));
  if(!fs.readFileSync(file).equals(original))throw Error('Public script was not copied verbatim: '+name);
  const customized=fs.readFileSync(file,'utf8')+'\n// User customization must survive sync.\n';fs.writeFileSync(file,customized);project.syncToGame(state);
  if(fs.readFileSync(file,'utf8')!==customized)throw Error('Sync overwrote user script: '+name);
}
const action='/*"/*\'/**//* YYD ACTION\nlib_id=1\naction_id=603\napplies_to=self\n*/';
const objects=path.join(project.project,'objects');
fs.writeFileSync(path.join(objects,'objRufShapeProbe.txt'),'sprite=\nvisible=0\nsolid=0\npersistent=0\ndepth=0\nparent=\nmask=\n');
fs.writeFileSync(path.join(objects,'objRufShapeProbe.gml'),'#define Create_0\n'+action+'\n');
for(const [file,line] of [['index.yyd','objRufShapeProbe'],['tree.yyd','\t|objRufShapeProbe']])fs.appendFileSync(path.join(objects,file),line+'\n');
const expected=new Map(),write=(name,frame,index,x,y)=>`file_text_write_string(rf_probe,'${name},${frame},'+string(${index})+','+string_format(${x},0,8)+','+string_format(${y},0,8));file_text_writeln(rf_probe);`;
const probe=["rf_probe=file_text_open_write(working_directory+'\\formation-probe.csv');"];
for(const sample of fixture.samples){
  const t=state.tracks.find(t=>t.id===sample.id);probe.push(`rf_frame=${sample.frame};`,stateCode(t,'rf_frame'),shapeSetup(t),`for(rf_j=0;rf_j<rf_count;rf_j+=1){`,shapePoint(t),write(sample.id,sample.frame,'rf_j','rf_px','rf_py'),t.rainbow?write('colors-'+sample.id,sample.frame,'rf_j',rainbowCode(t,rodSequence(t),'rf_frame'),'0'):'','}');
  sample.points.forEach((p,i)=>expected.set([sample.id,sample.frame,i].join(','),p));
  if(t.rainbow)sample.images.forEach((x,i)=>expected.set(['colors-'+sample.id,sample.frame,i].join(','),{x,y:0}));
}
// Run the supplied scripts themselves. One velocity step equals their formation outline.
probe.push('heart(60,112.5,objRufShapeProbe,0,0,-63);');
for(let i=1;i<=60;i++){
  probe.push(`rf_inst=idx[${i}];`,write('native-heart',0,i,'lengthdir_x(rf_inst.speed,rf_inst.direction)','lengthdir_y(rf_inst.speed,rf_inst.direction)'));
  expected.set(['native-heart',0,i].join(','),formationPoint('heart',i/60,120,27,5,1,.45));
}
probe.push('with(objRufShapeProbe) instance_destroy();star2(5,12,120,objRufShapeProbe,0,0,27);');
for(let k=0;k<5;k++)for(let i=0;i<12;i++){
  probe.push(`rf_inst=idx[${k},${i}];`,write('native-star2',0,k*12+i,'lengthdir_x(rf_inst.speed,rf_inst.direction)','lengthdir_y(rf_inst.speed,rf_inst.direction)'));
  expected.set(['native-star2',0,k*12+i].join(','),formationPoint('star2',(k+i/12)/5,120,27,5,1,.45));
}
probe.push('file_text_close(rf_probe);game_end();');
const controller=path.join(objects,'objRufController.gml');let code=fs.readFileSync(controller,'utf8');const event=code.indexOf('#define Step_0');code=code.slice(0,event)+'\n'+probe.join('\n')+'\n'+code.slice(event);
code=code.replace('rf_frame=0; rf_target=0;','window_set_visible(false);rf_frame=0; rf_target=0;').replace('FMODInstanceSetPaused(rf_music,0)','FMODInstanceSetPaused(rf_music,1)');fs.writeFileSync(controller,code);
const settings=path.join(project.project,'settings/settings.txt');fs.writeFileSync(settings,fs.readFileSync(settings,'utf8').replace('show_error_messages=1','show_error_messages=0').replace('always_abort=0','always_abort=1'));
const log=path.join(project.project,'formation-compiler.log');fs.writeFileSync(log,'');
function run(file,args,cwd,timeout){return new Promise((resolve,reject)=>{
  const child=spawn(file,args,{cwd,windowsHide:true,stdio:['ignore','pipe','pipe']});for(const stream of [child.stdout,child.stderr])stream.on('data',d=>fs.appendFileSync(log,d));
  const timer=setTimeout(()=>{child.kill();reject(Error('Timed out: '+file+'; logs '+project.project));},timeout);
  child.on('error',error=>{clearTimeout(timer);reject(error);});child.on('close',exit=>{clearTimeout(timer);if(exit!==0)reject(Error('Exit '+exit+': '+file+'; logs '+project.project));else resolve();});
});}
const exe=path.join(project.project,'formation-probe.exe');await run(project.gameMaker,[project.projectFile,'--build',exe],path.dirname(project.gameMaker),60000);await run(exe,[],project.project,15000);
const errors=path.join(project.project,'game_errors.log');if(fs.existsSync(errors)&&fs.readFileSync(errors,'utf8').trim())throw Error(fs.readFileSync(errors,'utf8'));
const report=path.join(project.project,'formation-probe.csv');if(!fs.existsSync(report))throw Error('Runner did not finish its report: '+project.project);
const lines=fs.readFileSync(report,'utf8').trim().split(/\r?\n/);if(lines.length!==expected.size)throw Error('Incomplete report: '+lines.length+'/'+expected.size);
for(const line of lines){const [name,frame,index,x,y]=line.split(','),key=[name,frame,index].join(','),p=expected.get(key);if(!p||Math.abs(Number(x)-p.x)>.0002||Math.abs(Number(y)-p.y)>.0002)throw Error('Mismatch '+line+'; expected '+JSON.stringify(p));expected.delete(key);}
if(expected.size)throw Error('Unreported samples: '+expected.size);
console.log(`PASS: ${lines.length} GM8.2 coordinate/palette samples match editor/reference, including signed height, spatial rainbows, original heart/star2 scripts, scrolling and parent transforms. ${project.project}`);
