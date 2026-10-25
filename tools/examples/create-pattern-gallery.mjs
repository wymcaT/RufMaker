import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {createProject} from '../../studio/project-files.mjs';

export const galleryChapters=[
  ['periodic-star','周期星星扩散'],['sequential-star','星星成员循环发射'],['moving-trace','移动母弹留点成线'],
  ['counter-spiral','双向螺旋'],['aimed-fan','移动自机狙扇形'],['seeded-rain','固定种子散雨'],
  ['stop-aim-dash','减速、瞄准、冲刺'],['delayed-bloom','延迟开花'],['tangent-rings','圆环切线释放'],
  ['gap-wall','移动缺口弹墙'],['heart-pulse','心形波纹与淡出']
].map(([id,name],index)=>({id,name,index,start:index*400,end:index*400+399,seconds:index*8,preview:index*400+180}));

export function patternGalleryTracks() {
  const tracks=[];
  const emitter=(id,name,start,end,extra={})=>({id,name,kind:'emitter',lane:0,start,end,x:400,y:200,count:1,interval:25,angle:0,spread:0,speed:3,life:150,scale:.48,sprite:'sprCherryWhite',color:'#66D9EF',emissionFacing:'fixed',advancedBullets:true,deathEffect:'shrink-fade',deathFrames:15,keyframes:[],...extra});
  const shot=(id,name,extra={})=>emitter(id,'模板 · '+name,0,0,{lane:3+tracks.filter(t=>t.templateOnly).length,x:0,y:0,templateOnly:true,...extra});
  const spawn=(templateId,extra={})=>({type:'spawn',templateId,start:0,duration:0,stagger:0,spawnRepeat:0,spawnOrigin:'center',inheritAngle:false,destroyParent:false,maxGeneration:1,...extra});
  const key=(property,frame,value,easing='linear')=>({property,frame,value,easing});
  const rod=(id,name,start,end,extra={})=>({id,name,kind:'rod',lane:0,start,end,x:400,y:250,shape:'star2',radius:90,count:60,sprite:'sprCherry',rainbow:true,rainbowMode:'angle',releaseFrame:-1,keyframes:[],...extra});

  tracks.push(rod('periodic-star','01 周期星星扩散 · 原星星保留',0,299,{x:280,y:215,
    keyframes:[key('x',0,280),key('x',299,520),key('orientation',0,0),key('orientation',299,90)],
    spawnActions:[spawn('echo-shot',{start:50,duration:200,spawnRepeat:50,spawnOrigin:'each',inheritAngle:true})]}));
  tracks.push(shot('echo-shot','01 距来源中心距离 × 0.018',{life:120,color:'#6DE8FF',
    bulletActions:[{type:'speed',start:0,duration:0,expression:'point_distance(x,y,source.x,source.y)*0.018'}]}));

  tracks.push(rod('sequential-star','02 星星成员循环发射 · 每轮 70 帧',400,620,{count:30,radius:100,
    keyframes:[key('orientation',400,0),key('orientation',620,90)],
    spawnActions:[spawn('uniform-shot',{spawnOrigin:'each',stagger:2,spawnRepeat:70,duration:140,spawnAngle:-90,spawnAngleStep:12})]}));
  tracks.push(shot('uniform-shot','02 等速单发 · 速度 3',{color:'#FFC65C',speed:3,life:150}));

  tracks.push(emitter('trace-mother','03 移动母弹 · 每 6 帧留一个点',800,800,{x:70,y:240,speed:2.5,life:240,scale:.8,color:'#FF75AD',
    flightMode:'expression',flightExpression:'25*sin(age/25)',
    bulletActions:[spawn('trace-dot',{spawnRepeat:6,duration:234})]}));
  tracks.push(shot('trace-dot','03 静止留点 · 真实弹幕并非拖影',{speed:0,life:100,scale:.4,color:'#6DE8FF',deathFrames:20}));

  for(const [id,sign,color,phase] of [['spiral-cw',1,'#FF75AD',0],['spiral-ccw',-1,'#6DE8FF',60]])
    tracks.push(emitter(id,'04 双向螺旋 · '+(sign>0?'正向':'反向'),1200,1420,{lane:sign>0?0:1,x:400,y:240,count:3,spread:120,interval:5,speed:2.4,life:160,emissionFacing:'fan',color,expressions:{angle:`${phase}+age*${sign*2.1}`}}));

  tracks.push(emitter('aimed-fan','05 移动自机狙 · 每次出生瞄准',1600,1820,{x:180,y:105,count:5,spread:14,interval:30,speed:2.8,life:165,emissionFacing:'fan',color:'#FFC65C',
    keyframes:[key('x',1600,180),key('x',1820,620,'sin-inout')],expressions:{angle:'point_direction(x,y,player.x,player.y)'}}));

  tracks.push(emitter('seeded-rain','06 固定种子散雨 · 逐颗取样',2000,2220,{x:400,y:40,count:12,interval:30,life:160,emissionShape:'line',emitWidth:700,color:'#A0E878',
    expressions:{x:'400+random_range(-12,12)',y:'40+random_range(-15,15)',angle:'random_range(258,282)',speed:'random_range(1.6,2.8)'}}));

  tracks.push(emitter('stop-aim-dash','07 减速 → 等待 → 瞄准 → 冲刺',2400,2520,{x:400,y:160,count:12,spread:30,interval:60,speed:3.5,life:210,color:'#B998FF',emissionFacing:'fan',expressions:{angle:'volley*11'},
    bulletActions:[{type:'speed',start:0,duration:25,from:3.5,to:0,easing:'power-out'},
      {type:'aim',start:45,duration:0,to:0},{type:'speed',start:50,duration:25,from:0,to:5,easing:'power-in'}]}));

  tracks.push(emitter('bloom-mother','08 延迟开花 · 母弹变成一圈新弹',2800,2940,{x:400,y:135,count:3,spread:25,interval:70,angle:270,speed:1.6,life:90,emissionFacing:'fan',scale:.75,color:'#FF75AD',
    bulletActions:[spawn('bloom-ring',{start:45,destroyParent:true})]}));
  tracks.push(shot('bloom-ring','08 14 路开花环',{count:14,spread:360/14,speed:2.2,life:140,emissionFacing:'fan',color:'#FFC65C'}));

  tracks.push({id:'tangent-host',name:'09 圆环切线释放 · 每 75 帧一环',kind:'object',lane:0,start:3200,end:3350,x:400,y:240,sprite:'sprCherry',scale:.55,alpha:.4,color:'#6DE8FF',
    spawnActions:[spawn('tangent-ring',{spawnRepeat:75,duration:150})],keyframes:[]});
  tracks.push(rod('tangent-ring','模板 · 09 先长大、再沿切线释放',0,80,{lane:6,x:0,y:0,shape:'ring',count:28,templateOnly:true,releaseFrame:45,releaseDirection:'tangent',speed:2.8,life:100,scale:.5,deathEffect:'shrink-fade',deathFrames:15,
    keyframes:[key('radius',0,15),key('radius',35,90,'power-out'),key('orientation',0,0),key('orientation',45,65)]}));

  for(const right of [false,true])tracks.push(emitter(right?'wall-right':'wall-left','10 移动缺口弹墙 · '+(right?'右半':'左半'),3600,3810,{lane:right?1:0,x:right?635:165,y:60,count:10,interval:35,speed:3.6,angle:270,life:140,color:'#6DE8FF',emissionShape:'line',emitWidth:330,
    expressions:{x:(right?'635':'165')+'+100*sin(age/35)',emitWidth:'330'+(right?'-':'+')+'200*sin(age/35)'}}));

  tracks.push({id:'heart-host',name:'11 心形波纹 · 每 90 帧一层',kind:'object',lane:0,start:4000,end:4180,x:400,y:250,sprite:'sprCherry',scale:.5,alpha:.25,
    spawnActions:[spawn('heart-pulse',{spawnRepeat:90,duration:180})],keyframes:[]});
  tracks.push(rod('heart-pulse','模板 · 11 原 heart 脚本、放大与淡出',0,130,{lane:7,x:0,y:0,shape:'heart',count:48,templateOnly:true,scale:.5,
    keyframes:[key('radius',0,12),key('radius',30,80,'power-out'),key('radius',130,110),key('alpha',0,0),key('alpha',20,1),key('alpha',100,1),key('alpha',130,0)]}));
  return tracks;
}

export async function createPatternGallery(root,parent) {
  fs.mkdirSync(parent,{recursive:true});let name='Pattern Gallery',index=2;
  while(fs.existsSync(path.join(parent,name)))name='Pattern Gallery '+index++;
  const created=createProject(root,{name,parent,preset:'floor',width:800,height:608,fps:50,spawnX:400,spawnY:560,autoSpawn:false});
  // Set selection before importing the synchronizing backend, never touch the open user project.
  process.env.RUFMAKER_ROOT=root;process.env.RUFMAKER_PROJECT=created.path;
  const project=await import('../../studio/project.mjs');
  const state={...project.readState(),name:'弹幕制作范例 · 11 个分章节教程',duration:4400,timelineInitialized:true,laneCount:8,randomSeed:24681357,randomizeOnPlay:false,nativeTest:{x:400,y:536},tracks:patternGalleryTracks()};
  project.syncToGame(state);
  const home=path.dirname(created.path);
  fs.copyFileSync(path.join(root,'docs/examples/pattern-gallery.md'),path.join(home,'示例教程.md'));
  fs.writeFileSync(path.join(home,'章节索引.json'),JSON.stringify(galleryChapters,null,2)+'\n');
  return {descriptor:created.path,project:project.project,state:path.join(project.project,'rufmaker.json'),chapters:galleryChapters};
}

if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)) {
  const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
  console.log(JSON.stringify(await createPatternGallery(root,path.resolve(process.argv[2]||path.join(root,'projects/examples')))));
}
