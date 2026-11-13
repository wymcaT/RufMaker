// Run Test-TemplateSystem.ps1 first. The GameMaker project and reports are isolated under build/.
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {execFileSync,spawn} from 'node:child_process';
import {createProject} from '../../studio/project-files.mjs';

const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
const folder=path.join(root,'build','object-properties-check');
fs.mkdirSync(folder,{recursive:true});
const created=createProject(root,{name:'Boundaries '+Date.now(),parent:folder,preset:'floor',width:800,height:608,fps:50});
process.env.RUFMAKER_ROOT=root;
process.env.RUFMAKER_PROJECT=created.path;
const project=await import('../../studio/project.mjs');
const state=project.readState();
const instanceFile=path.join(project.project,'rooms',state.room,'instances.txt');
fs.appendFileSync(instanceFile,'block,300,240,007B0001,0,2,1,4294967295,0,0\nblock,64,96,007B0002,0,1,1,4294967295,0,0\n');
for(let i=0;i<24;i++)fs.appendFileSync(instanceFile,`block,${32*i},384,${(0x7B0003+i).toString(16).padStart(8,'0')},0,1,1,4294967295,0,0\n`);
const rainbow=path.resolve(project.project,'sprites','sprrainbowcherry');
assert.equal(path.relative(project.project,rainbow),path.join('sprites','sprrainbowcherry'));
fs.renameSync(rainbow,path.join(path.dirname(project.project),'rainbow-original'));
project.ensureRainbowSprite();
project.ensureRainbowSprite();
assert(fs.existsSync(path.join(rainbow,'0.png')));
assert.equal(fs.readFileSync(path.join(project.project,'sprites','index.yyd'),'utf8').split(/\r?\n/).filter(name=>name==='sprrainbowcherry').length,1);

const shot=(id,x,y,speed,options={})=>({id,name:id,kind:'emitter',sprite:'sprBullet',start:0,end:0,x,y,speed,angle:0,count:1,interval:25,life:3,lifetimeMode:'room',keyframes:[],...options});
state.tracks=[
  shot('edge',790,70,30,{boundaryMode:'reflect'}),
  shot('wrap',790,80,30,{boundaryMode:'wrap'}),
  shot('fast-wrap',5,90,8000.25,{boundaryMode:'wrap'}),
  shot('fast-wrap-left',5,100,8000.25,{boundaryMode:'wrap',angle:180}),
  shot('exact-wrap',5,110,8795,{boundaryMode:'wrap',lifetimeMode:'fixed',life:30}),
  shot('brick',270,250,80,{bounceBricks:true}),
  shot('corner',260,200,60*Math.SQRT2,{angle:315,bounceBricks:true}),
  shot('wrap-brick',790,112,100,{boundaryMode:'wrap',bounceBricks:true}),
  shot('floor',280,554,55,{angle:270,bounceBricks:true}),
  shot('out',790,50,30),
  shot('legacy',400,60,0,{lifetimeMode:'fixed'}),
  shot('scatter',790,160,30,{kind:'rod',shape:'ring',radius:0,releaseFrame:1,releaseDirection:'fixed',end:3,boundaryMode:'reflect'}),
];
state.tracks.forEach((track,lane)=>track.lane=lane);
project.syncToGame(state);
const room=project.roomInfo(state.room);
assert(room.instances.some(i=>i.id==='007B0001'&&i.bulletBrick.right===64));
assert.match(fs.readFileSync(path.join(project.project,'scripts','rf_bullet_bricks_init.gml'),'utf8'),/rf_brick_cell_member/);
const scenePath=path.join(project.project,'boundary-scene.json'),roomPath=path.join(project.project,'boundary-room.json');
const samples=[0,1,2,3,7,25],expectedPath=path.join(project.project,'boundary-preview.json');
fs.writeFileSync(scenePath,JSON.stringify(state));
fs.writeFileSync(roomPath,JSON.stringify(room));
execFileSync(path.join(root,'build','template-system-check','TemplateSystem.tests.exe'),['--samples',scenePath,samples.join(','),expectedPath,roomPath],{windowsHide:true});
const expected=JSON.parse(fs.readFileSync(expectedPath,'utf8'));
for(const [id,x,y] of [['edge',780,70],['wrap',20,80],['fast-wrap',5.25,90],['fast-wrap-left',4.75,100],['exact-wrap',800,110],['brick',250,250],['corner',280,220],['wrap-brick',38,112],['floor',280,543]]){
  const bullet=expected[1].find(b=>b.source===id);
  assert(bullet&&Math.abs(bullet.x-x)<.001&&Math.abs(bullet.y-y)<.001,`Wrong preview contact: ${id} ${JSON.stringify(bullet)}`);
}
assert(!expected[1].some(b=>b.source==='out')&&!expected[3].some(b=>b.source==='legacy'));

const controller=path.join(project.project,'objects','objRufController.gml');
const source=fs.readFileSync(controller,'utf8').replace('rf_frame=0; rf_target=0;','window_set_visible(false);rf_frame=0; rf_target=0;');
const step=source.indexOf('#define Step_0');
const dump=`with(objRufBullet) if self.rf_system && self.rf_birth<=global.rf_boundary_probe_at {file_text_write_string(global.rf_boundary_probe,string(self.rf_source)+','+string(self.rf_birth)+','+string(self.rf_index)+','+string_format(x,0,8)+','+string_format(y,0,8)+','+string_format(self.rf_dir,0,8));file_text_writeln(global.rf_boundary_probe);}`;
const probe=`var rf_probe_at,rf_probe_mode;
global.rf_boundary_probe=file_text_open_write(working_directory+'\\boundary-gm.csv');
${samples.map(at=>`global.rf_boundary_probe_at=${at};for(rf_probe_mode=0;rf_probe_mode<2;rf_probe_mode+=1){
with(objRufTemplate)instance_destroy();with(objRufBullet)instance_destroy();rf_global_init();rf_template_init();
global.rf_preview_target=${at};global.rf_preview_replaying=rf_probe_mode;
for(rf_probe_at=0;rf_probe_at<=${at};rf_probe_at+=1)rf_pattern_step(rf_probe_at);
file_text_write_string(global.rf_boundary_probe,'F,${at},'+string(rf_probe_mode));file_text_writeln(global.rf_boundary_probe);${dump}}
`).join('\n')}
file_text_close(global.rf_boundary_probe);game_end();
`;
assert(step>=0);
fs.writeFileSync(controller,source.slice(0,step)+probe+source.slice(step));
const settings=path.join(project.project,'settings','settings.txt');
fs.writeFileSync(settings,fs.readFileSync(settings,'utf8').replace('show_error_messages=1','show_error_messages=0').replace('always_abort=0','always_abort=1'));
const log=path.join(project.project,'boundary-compiler.log');
async function run(file,args,cwd){
  await new Promise((resolve,reject)=>{
    const child=spawn(file,args,{cwd,windowsHide:true,stdio:['ignore','pipe','pipe']});
    for(const stream of [child.stdout,child.stderr])stream.on('data',data=>fs.appendFileSync(log,data));
    const timer=setTimeout(()=>{child.kill();reject(Error('Timed out: '+file+'; '+log));},60000);
    child.on('error',error=>{clearTimeout(timer);reject(error);});
    child.on('close',code=>{clearTimeout(timer);if(code===0)resolve();else reject(Error('Exit '+code+'; '+log));});
  });
}
const exe=path.join(project.project,'boundary-probe.exe');
console.log('Isolated project: '+project.project);
await run(project.gameMaker,[project.projectFile,'--build',exe],path.dirname(project.gameMaker));
await run(exe,[],project.project);
const errors=path.join(project.project,'game_errors.log');
if(fs.existsSync(errors)&&fs.readFileSync(errors,'utf8').trim())throw Error(fs.readFileSync(errors,'utf8'));
let frame,mode,count=0;
const checked=new Map();
for(const row of fs.readFileSync(path.join(project.project,'boundary-gm.csv'),'utf8').trim().split(/\r?\n/)){
  const fields=row.split(',');
  if(fields[0]==='F'){frame=Number(fields[1]);mode=Number(fields[2]);checked.set(`${frame}:${mode}`,new Set());continue;}
  const [source,birth,index,x,y,direction]=fields.map(Number),id=state.tracks[source].id;
  const bullet=expected[frame].find(b=>b.source===id&&b.birth===birth&&b.index===index);
  assert(bullet,`Unexpected bullet ${id} at ${frame}, replay ${mode}`);
  assert(Math.abs(x-bullet.x)<.002&&Math.abs(y-bullet.y)<.002,`${id} at ${frame}, replay ${mode}: GM ${x},${y}, C# ${bullet.x},${bullet.y}`);
  assert(Math.abs(((direction-bullet.direction+180)%360+360)%360-180)<.002,`${id} direction differs at ${frame}`);
  checked.get(`${frame}:${mode}`).add(`${id}:${birth}:${index}`);count++;
}
for(const frame of samples)for(const mode of [0,1])assert.equal(checked.get(`${frame}:${mode}`)?.size,expected[frame].length,`Missing bullets at ${frame}, replay ${mode}`);
console.log(`PASS: ${count} GM8.2 and C# boundary states agree, including grid brick filtering, high-speed wrap, exact edges and F5 replay pruning.`);
