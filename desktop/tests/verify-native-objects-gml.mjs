import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {spawn} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import {createProject} from '../../studio/project-files.mjs';
import {readRoom} from '../../studio/native-room.mjs';

const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
const parent=path.join(root,'build','native-integration');
fs.mkdirSync(parent,{recursive:true});
const created=createProject(root,{name:'Native objects '+Date.now(),parent,preset:'floor',width:800,height:608,fps:50});
process.env.RUFMAKER_ROOT=root;
process.env.RUFMAKER_PROJECT=created.path;
const p=await import('../../studio/project.mjs');
const state=p.readState(),roomFolder=path.join(p.project,'rooms',state.room);
const action='/*"/*\'/**//* YYD ACTION\nlib_id=1\naction_id=603\napplies_to=self\n*/';
const event=(name,body)=>`#define ${name}\n${action}\n${body}\n`;
function object(name,code,sprite='',parent=''){
  const folder=path.join(p.project,'objects');
  fs.writeFileSync(path.join(folder,name+'.txt'),`sprite=${sprite}\nvisible=1\nsolid=${parent?1:0}\npersistent=0\ndepth=0\nparent=${parent}\nmask=\n`);
  fs.writeFileSync(path.join(folder,name+'.gml'),code);
  fs.appendFileSync(path.join(folder,'index.yyd'),name+'\n');
  fs.appendFileSync(path.join(folder,'tree.yyd'),'\t|'+name+'\n');
}
const create=event('Create_0','age=0;');
const step=event('Step_0','age+=1;x+=1;');
object('objFixtureDoor',create+step,'sprRufBlock','block');
object('objFixtureKeep',create+step,'sprRufBlock','block');
object('objFixtureWitness',event('Step_2',`
if !instance_exists(objRufController) exit;
var f,probe,doorx,doorAge,collides,kept;
f=objRufController.rf_frame;
if f=2 || f=4 || f=14 || f=15 || f=21 || f=26 {
  doorx=-1;doorAge=-1;collides=0;kept=instance_number(objFixtureKeep);
  if instance_exists(objFixtureDoor) {
    doorx=objFixtureDoor.x;doorAge=objFixtureDoor.age;
    collides=collision_point(doorx+8,objFixtureDoor.y+8,objFixtureDoor,1,0)!=noone;
  }
  probe=file_text_open_append(working_directory+'\\samples.csv');
  file_text_write_string(probe,string(f)+','+string(doorx)+','+string(doorAge)+','+string(collides)+','+string(kept));
  file_text_writeln(probe);file_text_close(probe);
}
if f=18 && objRufController.rf_preview && !file_exists(working_directory+'\\retry.once') {
  probe=file_text_open_write(working_directory+'\\retry.once');file_text_close(probe);
  with(objFixtureDoor) {x=5;age=999;}
  with(objRufController) rf_preview_retry();
}
if f>=26 game_end();`));
const inst=path.join(roomFolder,'instances.txt');
fs.appendFileSync(inst,'objFixtureWitness,0,0,0012AA01,0,1,1,4294967295,0,0\n');
state.tracks=[
  {id:'door',name:'Door',kind:'native-instance',nativeObject:'objFixtureDoor',x:150,y:220,start:4,end:20,code:'age=40;exit;',keyframes:[]},
  {id:'keep',name:'Keep',kind:'native-instance',nativeObject:'objFixtureKeep',nativeKeepAlive:true,x:300,y:220,start:7,end:12,keyframes:[]}
];
p.syncToGame(state);
const sampleRoom=path.join(path.dirname(p.project),'Verse3-room');
fs.mkdirSync(path.join(sampleRoom,'rooms'),{recursive:true});
fs.cpSync(path.join(root,'non-ruf projects','Verse3','rooms','rMenu'),path.join(sampleRoom,'rooms','rMenu'),{recursive:true});
const nativeSample=readRoom(sampleRoom,'rMenu');
assert(nativeSample.tiles.some(t=>t.source==='AllTiles'&&t.depth===1000));
assert(nativeSample.views.some(v=>v.width===800&&v.height===600));
const tileFile=path.join(sampleRoom,'rooms','rMenu','1000.txt'),tileSource=fs.readFileSync(tileFile,'utf8');
fs.writeFileSync(tileFile,tileSource.replace(/^AllTiles,96,416,/, 'AllTiles,104,416,'));
assert.equal(readRoom(sampleRoom,'rMenu').tiles[0].x,104);
const first=p.roomInfo(state.room),witness=first.instances.find(i=>i.name==='objFixtureWitness');
const external=fs.readFileSync(inst,'utf8').replace(witness.raw,witness.raw.replace(',1,1,4294967295,0,0',',-2,1.5,4286578943,90,0'));
fs.writeFileSync(inst,external);
assert.throws(()=>p.moveRoomInstance(state.room,witness.index,20,30,first.instanceRevision,witness.id),/GameMaker 已修改/);
const reloaded=p.roomInfo(state.room),changed=reloaded.instances.find(i=>i.id===witness.id);
assert.deepEqual([changed.scaleX,changed.scaleY,changed.angle],[-2,1.5,90]);
p.syncToGame(state);
assert.equal(fs.readFileSync(inst,'utf8'),external);
const edited=p.moveRoomInstance(state.room,witness.index,20,30,reloaded.instanceRevision,witness.id,{scaleX:1,scaleY:2,blend:4294967295,angle:30});
assert.equal(edited.instances.find(i=>i.id===witness.id).angle,30);

const settings=path.join(p.project,'settings','settings.txt');
fs.writeFileSync(settings,fs.readFileSync(settings,'utf8').replace('show_error_messages=1','show_error_messages=0').replace('always_abort=0','always_abort=1'));
const log=path.join(path.dirname(p.project),'compiler.log');
async function run(file,args,cwd){
  await new Promise((resolve,reject)=>{
    const child=spawn(file,args,{cwd,windowsHide:true,stdio:['ignore','pipe','pipe']});
    for(const stream of [child.stdout,child.stderr])stream.on('data',data=>fs.appendFileSync(log,data));
    const timer=setTimeout(()=>{child.kill();reject(Error('Timed out: '+file+'; '+log));},50000);
    child.on('error',error=>{clearTimeout(timer);reject(error);});
    child.on('close',code=>{clearTimeout(timer);if(code===0)resolve();else reject(Error('Exit '+code+'; '+log));});
  });
}
const exe=path.join(p.project,'native-object-probe.exe');
console.log('Isolated project: '+p.project);
await run(p.gameMaker,[p.projectFile,'--build',exe],path.dirname(p.gameMaker));
assert(fs.statSync(exe).size>1024);
async function sample(mode,target){
  const dir=path.join(path.dirname(p.project),mode);fs.mkdirSync(dir);
  fs.cpSync(path.join(p.project,'Data'),path.join(dir,'Data'),{recursive:true});
  if(target!=null)fs.writeFileSync(path.join(dir,'Data','rufmaker_preview.ini'),`[preview]\r\nframe=${target}\r\ngod=0\r\n`);
  await run(exe,[],dir);
  const errors=path.join(dir,'game_errors.log');
  if(fs.existsSync(errors)&&fs.readFileSync(errors,'utf8').trim())throw Error(fs.readFileSync(errors,'utf8'));
  return fs.readFileSync(path.join(dir,'samples.csv'),'utf8').trim().split(/\r?\n/).map(line=>line.split(',').map(Number));
}
const normal=await sample('export',null),preview=await sample('F5',14);
assert.deepEqual(normal.find(row=>row[0]===2),[2,-1,-1,0,0]);
assert.equal(normal.find(row=>row[0]===4)[3],1);
assert.deepEqual(normal.find(row=>row[0]===21),[21,-1,-1,0,1]);
assert.deepEqual(preview.find(row=>row[0]===14),normal.find(row=>row[0]===14));
assert.deepEqual(preview.filter(row=>row[0]===15),[normal.find(row=>row[0]===15),normal.find(row=>row[0]===15)]);
assert.deepEqual(preview.at(-1),normal.at(-1));
const nativeTrack={id:'gm-native',name:'Native',kind:'native',start:0,end:state.duration,keyframes:[]};
state.nativeProject={imported:true,room:state.room,defaultSprite:'sprBullet',killerParent:'playerKiller',duration:state.duration};
state.tracks.unshift(nativeTrack);
p.syncToGame(state);
const controller=path.join(p.project,'objects','objRufController.gml');
assert.match(fs.readFileSync(controller,'utf8'),/rf_native_objects\(rf_frame\)/);
state.tracks=[nativeTrack];
p.syncToGame(state);
assert.doesNotMatch(fs.readFileSync(controller,'utf8'),/rf_native_objects\(rf_frame\)/);
assert.equal(fs.readFileSync(path.join(p.project,'scripts','rf_native_object_init.gml'),'utf8'),'return 0;');
console.log('PASS: Verse3 tile reload, views and room revision conflicts; GM object events and collision, F5 history, R restore, cleanup and removal from imported projects.');
