import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {spawn} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import {createProject} from '../../studio/project-files.mjs';

const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
const parent=path.join(root,'build','depth-player-preview');
fs.mkdirSync(parent,{recursive:true});
const created=createProject(root,{name:'Depth '+Date.now(),parent,preset:'floor',width:800,height:608,fps:50});
process.env.RUFMAKER_ROOT=root;process.env.RUFMAKER_PROJECT=created.path;
const p=await import('../../studio/project.mjs');
const project=p.project,output=path.dirname(project),fixture=path.join(root,'build','native-integration','desktop');
const state=p.readState();
state.duration=100;state.tracks=JSON.parse(fs.readFileSync(path.join(fixture,'depth-scene.json'),'utf8')).tracks;
const spriteMeta=fs.readFileSync(path.join(project,'sprites','sprRufBlock','sprite.txt'),'utf8')
  .replace(/^origin_x=.*$/m,'origin_x=16').replace(/^origin_y=.*$/m,'origin_y=16');
function index(folder,name){
  for(const [file,entry] of [['index.yyd',name],['tree.yyd','|'+name]]){
    const target=path.join(folder,file),text=fs.readFileSync(target,'utf8');
    fs.appendFileSync(target,(text.endsWith('\n')?'':'\n')+entry+'\n');
  }
}
for(const name of ['sprDepthRed','sprDepthBlue','sprDepthGreen']){
  const folder=path.join(project,'sprites',name);fs.mkdirSync(folder);
  fs.copyFileSync(path.join(fixture,'sprites',name,'0.png'),path.join(folder,'0.png'));
  fs.writeFileSync(path.join(folder,'sprite.txt'),spriteMeta);index(path.join(project,'sprites'),name);
}
fs.writeFileSync(path.join(project,'objects','objDepthGreen.txt'),'sprite=sprDepthGreen\nvisible=1\nsolid=0\npersistent=0\ndepth=150\nparent=\nmask=\n');
fs.writeFileSync(path.join(project,'objects','objDepthGreen.gml'),'');
index(path.join(project,'objects'),'objDepthGreen');
state.tracks.push({id:'teleport',name:'Move',kind:'control',eventPreset:'player-move',start:3,end:3,
  playerTargetX:600,playerTargetY:200,playerKeepMotion:false,playerResetJump:true,expressions:{playerTargetX:'choose(600,600)'},keyframes:[]});
p.syncToGame(state);
const action='/*"/*\'/**//* YYD ACTION\nlib_id=1\naction_id=603\napplies_to=self\n*/\n';
fs.writeFileSync(path.join(project,'objects','objDepthProbe.txt'),'sprite=\nvisible=1\nsolid=0\npersistent=0\ndepth=-2147483000\nparent=\nmask=\n');
fs.writeFileSync(path.join(project,'objects','objDepthProbe.gml'),'#define Draw_0\n'+action+String.raw`
if !instance_exists(objRufController) exit;
if objRufController.rf_frame<6 exit;
var rf_file,rf_test;
rf_file=file_text_open_write(working_directory+'\depth.csv');
file_text_write_string(rf_file,string(draw_getpixel(100,100))+','+string(draw_getpixel(200,100))+','+string(draw_getpixel(300,100))+','+string(draw_getpixel(400,100))+','+string(draw_getpixel(500,100))+','+string(draw_getpixel(600,100)));
file_text_close(rf_file);
rf_test=instance_create(0,0,objRufBullet);
rf_test.visible=0;rf_test.rf_source=0;rf_test.rf_generation=2;
with(rf_test) rf_spawn_appearance(noone);
rf_file=file_text_open_write(working_directory+'\state.csv');
file_text_write_string(rf_file,string(rf_test.depth)+','+string(player.x)+','+string(player.y)+','+string(objRufTrails.depth));
file_text_close(rf_file);
screen_save(working_directory+'\depth-game.png');
game_end();
`);
index(path.join(project,'objects'),'objDepthProbe');
const room=p.roomInfo(state.room);
p.editRoomInstances(state.room,room.instanceRevision,rows=>{
  rows.push('objDepthGreen,300,100,00366101,0,1,1,4294967295,0,0');
  rows.push('objDepthGreen,400,100,00366102,0,1,1,4294967295,0,0');
  rows.push('objDepthProbe,0,0,00366103,0,1,1,4294967295,0,0');
});

const settings=path.join(project,'settings','settings.txt');
fs.writeFileSync(settings,fs.readFileSync(settings,'utf8').replace('show_error_messages=1','show_error_messages=0').replace('always_abort=0','always_abort=1'));
const log=path.join(output,'compiler.log');
async function run(file,args,cwd){
  fs.appendFileSync(log,JSON.stringify({file,args,cwd})+'\n');
  await new Promise((resolve,reject)=>{
    const child=spawn(file,args,{cwd,windowsHide:true,stdio:['ignore','pipe','pipe']});
    for(const stream of [child.stdout,child.stderr])stream.on('data',data=>fs.appendFileSync(log,data));
    const timer=setTimeout(()=>{child.kill();reject(Error('Timed out; '+log));},50000);
    child.on('error',error=>{clearTimeout(timer);reject(error);});
    child.on('close',code=>{clearTimeout(timer);fs.appendFileSync(log,'Exit '+code+'\n');code===0?resolve():reject(Error('Exit '+code+'; '+log));});
  });
}
console.log('Isolated project: '+project);
const exe=path.join(project,'depth-probe.exe');
await run(p.gameMaker,[p.projectFile,'--build',exe],path.dirname(p.gameMaker));
await run(exe,[],project);
const errors=path.join(project,'game_errors.log');
if(fs.existsSync(errors)&&fs.readFileSync(errors,'utf8').trim())throw Error(fs.readFileSync(errors,'utf8'));
assert.deepEqual(fs.readFileSync(path.join(project,'depth.csv'),'utf8').split(',').map(Number),[255,16711680,65280,255,255,16711680]);
const [depth,x,y,trailDepth]=fs.readFileSync(path.join(project,'state.csv'),'utf8').split(',').map(Number);
assert.equal(depth,98);assert.equal(x,600);assert(Math.abs(y-200)<12);assert.equal(trailDepth,401);
console.log('PASS: GM/C# object, formation and room layer pixels; generated depth; player teleport expression.');
