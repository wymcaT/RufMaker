import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {createProject} from '../../studio/project-files.mjs';

export function starEchoTracks() {
  return [
    {id:'echo-star',name:'原星星 · 移动并保留',kind:'rod',shape:'star2',lane:0,start:0,end:450,x:230,y:190,radius:95,count:60,sides:5,orientation:0,sprite:'sprCherry',rainbow:true,rainbowMode:'angle',rainbowDensity:1,releaseFrame:-1,easing:'linear',
      keyframes:[{property:'x',frame:0,value:230},{property:'x',frame:400,value:460},{property:'y',frame:0,value:190},{property:'y',frame:400,value:290},{property:'orientation',frame:0,value:0},{property:'orientation',frame:400,value:120}],
      spawnActions:[{type:'spawn',templateId:'echo-shot',start:100,duration:0,stagger:0,spawnRepeat:0,spawnOrigin:'each',inheritAngle:true,destroyParent:false,maxGeneration:1}]},
    {id:'echo-shot',name:'扩散弹模板 · 来源距离 × 0.02',kind:'emitter',lane:1,start:0,end:0,x:0,y:0,angle:0,count:1,interval:25,speed:1,life:200,scale:.7,sprite:'sprCherry',rainbow:true,rainbowOffset:16,templateOnly:true,advancedBullets:true,emissionShape:'point',emissionFacing:'fixed',flightMode:'straight',
      keyframes:[],bulletActions:[{type:'speed',start:0,duration:0,stagger:0,relative:false,expression:'point_distance(x,y,source.x,source.y)*0.02'}]}
  ];
}

export async function createStarEchoExample(root,parent) {
  fs.mkdirSync(parent,{recursive:true});let name='Star Echo',index=2;
  while(fs.existsSync(path.join(parent,name)))name='Star Echo '+index++;
  const created=createProject(root,{name,parent,preset:'floor',width:800,height:608,fps:50});
  // Select the newly created project before importing the synchronizing backend.
  process.env.RUFMAKER_ROOT=root;process.env.RUFMAKER_PROJECT=created.path;
  const project=await import('../../studio/project.mjs');
  const state={...project.readState(),name:'星星复制扩散示例',duration:500,timelineInitialized:true,tracks:starEchoTracks()};
  project.syncToGame(state);
  fs.copyFileSync(path.join(root,'docs/examples/star-echo.md'),path.join(path.dirname(created.path),'示例说明.md'));
  return {descriptor:created.path,project:project.project,state:path.join(project.project,'rufmaker.json')};
}

if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)) {
  const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
  console.log(JSON.stringify(await createStarEchoExample(root,path.resolve(process.argv[2]||path.join(root,'projects/examples')))));
}
