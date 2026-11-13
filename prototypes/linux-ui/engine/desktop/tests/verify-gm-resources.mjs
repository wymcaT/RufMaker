import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {execFileSync,spawn} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import {createProject} from '../../studio/project-files.mjs';
import {editGmAsset,nativePathAssets} from '../../studio/gm-resources.mjs';
import {describeInstanceCode,updateInstanceCode} from '../../studio/gm-instance-fields.mjs';

const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
const parent=path.join(root,'build','gm-resources');
fs.mkdirSync(parent,{recursive:true});
const created=createProject(root,{name:'GM resources '+Date.now(),parent,preset:'floor',width:800,height:608,fps:50});
process.env.RUFMAKER_ROOT=root;
process.env.RUFMAKER_PROJECT=created.path;
const p=await import('../../studio/project.mjs');
const project=p.project,output=path.dirname(project),state=p.readState();
const create=(kind,name)=>editGmAsset(project,{kind,name,operation:'create'});
const edit=(asset,operation,extra)=>editGmAsset(project,{name:asset.name,kind:asset.kind,revision:asset.revision,operation,...extra});
const code=(asset,section,content)=>edit(asset,'code',{section,action:0,content});
function add(asset,section,content){return code(edit(asset,'add',{section}),section,content);}

for(const [name,closed] of [['pathProbeOpen',false],['pathProbeClosed',true]]){
  edit(create('paths',name),'settings',{path:{smooth:true,closed,precision:4,points:[
    {x:12,y:7,speed:100},{x:43,y:80,speed:180},{x:114,y:40,speed:70},{x:203,y:180,speed:100}
  ]}});
}
let moments=create('timelines','tlProbe');
moments=edit(moments,'add',{frame:5});
moments=code(moments,'5','rf_probe_timeline=83;');
moments=edit(moments,'move',{section:'5',frame:8});
assert.deepEqual(moments.events.map(event=>event.id),['8']);

let platform=create('objects','objProbePlatform');
platform=edit(platform,'settings',{object:{sprite:'sprRufBlock',parent:'block',solid:true,visible:true,depth:200}});
platform=add(platform,'Create_0','rf_probe_platform=1;');
const addVisual=(asset,section,visual)=>edit(asset,'add-action',{section,visual});
const deleted=create('objects','objProbeDelete');
edit(deleted,'delete');
const probeScript='scrVisualProbe';
fs.writeFileSync(path.join(project,'scripts',probeScript+'.gml'),'rf_script_value=argument0*100+argument4;\n');
for(const [file,entry] of [['index.yyd',probeScript],['tree.yyd','|'+probeScript]]){
  const target=path.join(project,'scripts',file),source=fs.readFileSync(target,'utf8');
  fs.appendFileSync(target,(source.endsWith('\n')?'':'\n')+entry+'\n');
}
let base=create('objects','objProbeBase');
base=add(base,'Create_0','rf_parent_value=73;');
let visual=create('objects','objProbeVisual');
visual=edit(visual,'settings',{object:{...visual.object,parent:base.name}});
visual=edit(visual,'add',{section:'Create_0',empty:true});
visual=addVisual(visual,'Create_0',{kind:'position',x:'11',y:'13',relative:true});
visual=addVisual(visual,'Create_0',{kind:'variable',name:'rf_visual',value:'41'});
visual=addVisual(visual,'Create_0',{kind:'variable',name:'rf_visual',value:'2',relative:true});
visual=addVisual(visual,'Create_0',{kind:'inherited'});
for(const section of ['Alarm_11','Keyboard_0','KeyRelease_32','KeyPress_32'])
  visual=edit(visual,'add',{section,empty:true});
let ephemeral=create('objects','objProbeEphemeral');
ephemeral=edit(ephemeral,'add',{section:'Create_0',empty:true});
ephemeral=addVisual(ephemeral,'Create_0',{kind:'destroy'});
let movement=create('objects','objProbeMovement');
movement=add(movement,'Create_0','rf_alarm_fired=0;');
movement=addVisual(movement,'Create_0',{kind:'motion',direction:'90',speed:'2'});
movement=addVisual(movement,'Create_0',{kind:'alarm',frames:'3',alarm:'0'});
movement=addVisual(movement,'Create_0',{kind:'alarm',frames:'2',alarm:'0',relative:true});
movement=edit(movement,'add',{section:'Alarm_0',empty:true});
movement=addVisual(movement,'Alarm_0',{kind:'variable',name:'rf_alarm_fired',value:'1'});
movement=addVisual(movement,'Alarm_0',{kind:'motion',direction:'90',speed:'0'});
let wall=create('objects','objProbeWall');
wall=edit(wall,'settings',{object:{...wall.object,sprite:'sprRufBlock',solid:true,visible:false}});
wall=add(wall,'Create_0','rf_collision_count=0;');
let bouncer=create('objects','objProbeBounce');
bouncer=edit(bouncer,'settings',{object:{...bouncer.object,sprite:'sprRufBlock',visible:false}});
bouncer=add(bouncer,'Create_0','rf_bounced=0;');
bouncer=addVisual(bouncer,'Create_0',{kind:'motion',direction:'0',speed:'4'});
bouncer=edit(bouncer,'add',{section:'Collision_objProbeWall',empty:true});
bouncer=addVisual(bouncer,'Collision_objProbeWall',{kind:'bounce',precise:'0',collisionAll:'0'});
bouncer=addVisual(bouncer,'Collision_objProbeWall',{kind:'variable',name:'rf_collision_count',value:'1',relative:true,appliesTo:'other'});
bouncer=addVisual(bouncer,'Collision_objProbeWall',{kind:'variable',name:'rf_bounced',value:'1'});
const frames=[1,3,7,11,15,22,29,30,33];
let witness=create('objects','objProbeWitness');
witness=add(witness,'Create_0',`
if !variable_local_exists('rf_amount') rf_amount=0;
rf_probe_timeline=0;
timeline_index=tlProbe;timeline_position=0;timeline_speed=1;timeline_running=1;
`);
witness=addVisual(witness,'Create_0',{kind:'create',target:'objProbeVisual',x:'100',y:'200'});
witness=addVisual(witness,'Create_0',{kind:'create',target:'objProbeEphemeral',x:'0',y:'0'});
witness=addVisual(witness,'Create_0',{kind:'create',target:'objProbeMovement',x:'20',y:'80'});
witness=addVisual(witness,'Create_0',{kind:'create',target:'objProbeWall',x:'64',y:'300'});
witness=addVisual(witness,'Create_0',{kind:'create',target:'objProbeBounce',x:'20',y:'300'});
witness=addVisual(witness,'Create_0',{kind:'sprite',target:'sprRufBlock',subimage:'0',animationSpeed:'0',appliesTo:'objProbeVisual'});
witness=addVisual(witness,'Create_0',{kind:'script',target:probeScript,arguments:['1','2','3','4','5'],appliesTo:'objProbeVisual'});
witness=add(witness,'Other_4','//field rf_amount: number(0,100) - Test number\nrf_probe_room_start=rf_amount;');
const checkVisual=String.raw`rf_report=file_text_open_write(working_directory+'\visual.csv');
  file_text_write_string(rf_report,string(objProbeVisual.x)+','+string(objProbeVisual.y)+','+string(objProbeVisual.rf_visual)+','+string(instance_number(objProbeEphemeral)));
  file_text_close(rf_report);
  rf_report=file_text_open_write(working_directory+'\actions.csv');
  file_text_write_string(rf_report,string(objProbeVisual.rf_parent_value)+','+string(objProbeVisual.sprite_index=sprRufBlock)+','+string(objProbeVisual.image_speed)+','+string(objProbeVisual.rf_script_value)+','+string(objProbeMovement.rf_alarm_fired)+','+string(objProbeMovement.speed)+','+string(objProbeMovement.y<80)+','+string(objProbeBounce.rf_bounced)+','+string(objProbeBounce.hspeed<0)+','+string(objProbeWall.rf_collision_count>0));
  file_text_close(rf_report);`;
witness=add(witness,'Step_2',`
if !instance_exists(objRufController) exit;
var rf_f,rf_report;
rf_f=objRufController.rf_frame;
if ${frames.map(frame=>'rf_f='+frame).join(' || ')} {
  rf_report=file_text_open_append(working_directory+'\\paths.csv');
  with(objRufBullet) if rf_birth=rf_f {
    file_text_write_string(rf_report,string(rf_birth)+','+string(rf_dir)+','+string_format(x,0,9)+','+string_format(y,0,9));file_text_writeln(rf_report);
  }
  file_text_close(rf_report);
}
if rf_f>=35 {
  rf_report=file_text_open_write(working_directory+'\\runtime.csv');
  file_text_write_string(rf_report,string(rf_probe_room_start)+','+string(rf_probe_timeline)+','+string(objProbePlatform.rf_probe_platform)+','+string_format(objProbePlatform.x,0,7)+','+string_format(objProbePlatform.y,0,7));
  file_text_close(rf_report);
  ${checkVisual}
  game_end();
}
`);
const instance='00126633';
const before=p.roomInfo(state.room);
p.editRoomInstances(state.room,before.instanceRevision,rows=>rows.push('objProbeWitness,0,0,'+instance+',0,1,1,4294967295,0,0'));
const instanceCode=describeInstanceCode(project,state.room,instance);
const current=p.roomInfo(state.room);
p.editRoomInstances(state.room,current.instanceRevision,(rows,tx)=>{
  const result=updateInstanceCode(project,state.room,instance,{hash:instanceCode.hash,fields:[{name:'rf_amount',value:'17'}]},tx.io);
  tx.write(result.file,result.source);
  const index=rows.findIndex(row=>row.split(',')[3]===instance);
  rows[index]=rows[index].slice(0,-1)+'1';
});
const emitter=(id,nativePath,angle)=>({id,name:id,kind:'emitter',start:0,end:60,x:240,y:160,sprite:'sprBullet',
  count:1,interval:1,speed:0,life:4,angle,lifetimeMode:'fixed',nativePath,nativePathFrames:30,nativePathLoop:true,keyframes:[]});
state.tracks=[
  emitter('open','pathProbeOpen',10),
  emitter('closed','pathProbeClosed',20),
  {id:'platform',name:'Platform',kind:'native-instance',nativeObject:'objProbePlatform',start:2,end:60,x:280,y:250,
    nativePath:'pathProbeOpen',nativePathSpeed:3,nativePathLoop:true,keyframes:[]}
];
p.syncToGame(state);
const input=path.join(output,'path-model.json'),expectedFile=path.join(output,'path-expected.json');
fs.writeFileSync(input,JSON.stringify({paths:nativePathAssets(project),frames}));
execFileSync(path.join(root,'build','native-integration','desktop','NativeIntegration.tests.exe'),['--path-probe',input,expectedFile],{windowsHide:true});
const expected=JSON.parse(fs.readFileSync(expectedFile,'utf8'));
const settings=path.join(project,'settings','settings.txt');
fs.writeFileSync(settings,fs.readFileSync(settings,'utf8').replace('show_error_messages=1','show_error_messages=0').replace('always_abort=0','always_abort=1'));
const log=path.join(output,'compiler.log');
async function run(file,args,cwd){
  fs.appendFileSync(log,JSON.stringify({file,args,cwd})+'\n');
  await new Promise((resolve,reject)=>{
    const child=spawn(file,args,{cwd,windowsHide:true,stdio:['ignore','pipe','pipe']});
    for(const stream of [child.stdout,child.stderr])stream.on('data',data=>fs.appendFileSync(log,data));
    const timer=setTimeout(()=>{fs.appendFileSync(log,'Timed out\n');child.kill();reject(Error('Timed out: '+file+'; '+log));},50000);
    child.on('error',error=>{clearTimeout(timer);reject(error);});
    child.on('close',code=>{clearTimeout(timer);fs.appendFileSync(log,'Exit '+code+'\n');if(code===0)resolve();else reject(Error('Exit '+code+'; '+log));});
  });
}
const exe=path.join(project,'gm-resource-probe.exe');
console.log('Isolated project: '+project);
await run(p.gameMaker,[p.projectFile,'--build',exe],path.dirname(p.gameMaker));
assert(fs.statSync(exe).size>1024);
await run(exe,[],project);
const errors=path.join(project,'game_errors.log');
if(fs.existsSync(errors)&&fs.readFileSync(errors,'utf8').trim())throw Error(fs.readFileSync(errors,'utf8'));
const samples=fs.readFileSync(path.join(project,'paths.csv'),'utf8').trim().split(/\r?\n/).map(row=>row.split(',').map(Number));
const [amount,timeline,platformEvent,x,y]=fs.readFileSync(path.join(project,'runtime.csv'),'utf8').split(',').map(Number);
assert.deepEqual([amount,timeline,platformEvent],[17,83,1]);
assert.deepEqual(fs.readFileSync(path.join(project,'visual.csv'),'utf8').split(',').map(Number),[111,213,43,0]);
assert.deepEqual(fs.readFileSync(path.join(project,'actions.csv'),'utf8').split(',').map(Number),[73,1,0,105,1,0,1,1,1,1]);
assert(Math.hypot(x-280,y-250)>20,'Native object failed to move with path_start');
assert.equal(samples.length,frames.length*2);
for(const [frame,angle,x,y] of samples){
  const name=angle===10?'pathProbeOpen':'pathProbeClosed',point=expected[name][frames.indexOf(frame)];
  assert(Math.abs(x-point[0])<.002&&Math.abs(y-point[1])<.002,`${name} f${frame}: GM ${x},${y}, C# ${point}`);
}
console.log('PASS: motion, alarm, sprite, script, inherited event, bounce, self/other/object scopes, object removal, native events, instance fields and '+samples.length+' matching GM8.2/C# emitter positions.');
