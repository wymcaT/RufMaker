import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {pathToFileURL} from 'node:url';

export function locateProject(root,selection='') {
  const legacy=path.join(root,'projects','rufmaker-demo.gm82','rufmaker-demo.gm82');
  const defaultDescriptor=path.join(root,'projects','rufmaker-demo.rufmaker');
  let file=path.resolve(selection||(fs.existsSync(defaultDescriptor)?defaultDescriptor:legacy)),descriptor='';
  if(fs.statSync(file).isDirectory()) {
    const entries=fs.readdirSync(file).filter(n=>n.endsWith('.rufmaker'));
    if(entries.length===1)file=path.join(file,entries[0]);
    else {const entries=fs.readdirSync(file).filter(n=>n.endsWith('.gm82')&&fs.statSync(path.join(file,n)).isFile());if(entries.length!==1)throw new Error('请选择 .rufmaker 工程文件');file=path.join(file,entries[0]);}
  }
  let name=path.basename(path.dirname(file));
  if(file.toLowerCase().endsWith('.rufmaker')) {
    const data=JSON.parse(fs.readFileSync(file,'utf8'));
    if(data.format!=='rufmaker-project'||data.version!==1||typeof data.gameProject!=='string')throw new Error('不是有效的 RufMaker 工程');
    descriptor=file;name=data.name||path.basename(file,'.rufmaker');file=path.resolve(path.dirname(file),data.gameProject);
  } else if(path.basename(file)==='rufmaker.json') {
    const folder=path.dirname(file),entries=fs.readdirSync(folder).filter(n=>n.endsWith('.gm82')&&fs.statSync(path.join(folder,n)).isFile());
    if(entries.length!==1)throw new Error('找不到 GameMaker 工程入口');file=path.join(folder,entries[0]);
  }
  if(!fs.readFileSync(file,'utf8').startsWith('gm82_version='))throw new Error('请选择 RufMaker 工程或文本格式的 GameMaker 8.2 工程');
  const folder=path.dirname(file);if(!fs.existsSync(path.join(folder,'rufmaker.json')))throw new Error('此 GameMaker 工程尚无 RufMaker 时间轴，请使用顶栏“导入非 RufMaker 工程”创建兼容副本。');
  const home=descriptor?path.dirname(descriptor):folder;
  const isLegacy=path.resolve(file).toLowerCase()===legacy.toLowerCase();
  return {project:folder,projectFile:file,descriptor:descriptor||file,home,name,buildDir:isLegacy?path.join(root,'build'):path.join(home,'build'),gameExe:isLegacy?path.join(root,'build','demo','rufmaker-game.exe'):path.join(home,'build','rufmaker-game.exe')};
}

const finite=(v,d)=>Number.isFinite(Number(v))?Number(v):d;
const integer=(v,d,min,max)=>Math.max(min,Math.min(max,Math.round(finite(v,d))));
export function terrainLayout(input={}) {
  const width=integer(input.width,800,320,3840),height=integer(input.height,608,240,2160),tile=32;
  const preset=['floor','staggered','center','empty'].includes(input.preset)?input.preset:'floor';
  const cells=new Map(),platforms=[];
  const add=(x,y,kind='block')=>{const key=x+','+y;if(!cells.has(key)||kind==='block')cells.set(key,{x,y,kind,width:32,height:kind==='platform'?8:32});};
  const line=(x,y,count,kind)=>{platforms.push({x,y,width:count*32,height:kind==='platform'?8:32});for(let i=0;i<count;i++)add(x+i*tile,y,kind);};
  if(input.floor||preset==='floor')for(let x=0;x<width;x+=tile)add(x,height-tile);
  if(input.sideWalls)for(let y=0;y<height;y+=tile){add(0,y);add(width-tile,y);}
  if(input.ceiling)for(let x=0;x<width;x+=tile)add(x,0);
  const kind=input.oneWay?'platform':'block',platformTiles=integer(input.platformTiles,5,1,40);
  if(preset==='center')line(Math.round((width-platformTiles*32)/64)*32,Math.round(height*.65/32)*32,platformTiles,kind);
  if(preset==='staggered') {
    const left=integer(input.leftMarginTiles,3,0,100)*tile,top=integer(input.topTiles,3,0,60)*tile,span=integer(input.spanTiles,12,1,60)*tile;
    const right=left+(platformTiles+integer(input.gapTiles,2,0,60))*tile;
    const levels=[[left,integer(input.leftLevels,5,1,20)],[right,integer(input.rightLevels,4,1,20)]];
    if(levels.some(([,count])=>count>span/tile+1))throw new Error('平台层数超过纵向跨度的格数，请增大跨度');
    for(const [x,count] of levels)
      for(let i=0;i<count;i++)line(x,top+(count===1?0:Math.round(i*span/(count-1)/tile)*tile),platformTiles,kind);
  }
  for(const p of platforms)if(p.x<0||p.y<0||p.x+p.width>width||p.y+p.height>height)throw new Error('平台超出房间，请调整房间大小、平台宽度或间距');
  const bases=platforms.slice();if(input.floor||preset==='floor')bases.push({x:width/2-16,y:height-tile,width:32});
  const base=bases.length?bases.reduce((a,b)=>b.y>a.y?b:a):{x:width/2-16,y:height/2+12,width:32};
  const spawnX=input.autoSpawn===false?finite(input.spawnX,width/2):base.x+base.width/2;
  const spawnY=input.autoSpawn===false?finite(input.spawnY,height-44):base.y-12;
  if(spawnX<0||spawnX>width||spawnY<0||spawnY>height)throw new Error('出生点必须位于房间内');
  return {width,height,preset,cells:[...cells.values()],spawnX,spawnY};
}

export function copySong(project,file) {
  const ext=path.extname(file).toLowerCase();if(!['.ogg','.mp3','.wav','.flac'].includes(ext)||!fs.statSync(file).isFile())throw new Error('请选择 OGG、MP3、WAV 或 FLAC 音乐');
  const data=fs.readFileSync(file),hash=crypto.createHash('sha256').update(data).digest('hex').slice(0,12);
  const music=`Data/Music/song-${hash}${ext}`,destination=path.join(project,music);fs.mkdirSync(path.dirname(destination),{recursive:true});
  if(!fs.existsSync(destination))fs.writeFileSync(destination,data);
  return {music,title:path.basename(file)};
}

export function createProject(root,input) {
  const name=String(input.name||'').trim();if(!name||/[<>:"/\\|?*\x00-\x1f]/.test(name)||/[. ]$/.test(name)||/^(con|prn|aux|nul|com[1-9]|lpt[1-9])$/i.test(name))throw new Error('请输入有效的工程名称');
  const home=path.join(path.resolve(input.parent),name);if(fs.existsSync(home))throw new Error('同名工程文件夹已存在，请换一个名称');
  const layout=terrainLayout(input),template=path.join(root,'templates','avoidance.gm82'),project=path.join(home,'GameMaker.gm82');
  if(!fs.existsSync(template))throw new Error('缺少 templates/avoidance.gm82 引擎模板');
  if(input.song){const ext=path.extname(input.song).toLowerCase();if(!['.ogg','.mp3','.wav','.flac'].includes(ext)||!fs.statSync(input.song).isFile())throw new Error('请选择有效的歌曲文件');}
  fs.mkdirSync(home);fs.cpSync(template,project,{recursive:true});
  const descriptor=path.join(home,name+'.rufmaker');
  const manifest={format:'rufmaker-project',version:1,name,gameProject:'GameMaker.gm82/rufmaker-demo.gm82'};
  fs.writeFileSync(descriptor,JSON.stringify(manifest,null,2)+'\n');
  const song=input.song?copySong(project,input.song):{music:'Data/Music/BGM.ogg',title:'BGM.ogg'};
  const state={version:2,name,fps:integer(input.fps,50,15,240),bpm:120,duration:1500,room:'rAvoidance',music:song.music,musicTitle:song.title,timelineInitialized:false,directStart:true,beatSnap:false,beatDivision:4,beatOffsetMs:0,tracks:[],terrain:{...input,song:undefined,parent:undefined,name:undefined},player:{gravity:finite(input.gravity,.4),moveSpeed:finite(input.moveSpeed,3),jumpSpeed:finite(input.jumpSpeed,8.5),doubleJumpSpeed:finite(input.doubleJumpSpeed,7),maxJumps:integer(input.maxJumps,2,1,8),infiniteJump:!!input.infiniteJump}};
  fs.writeFileSync(path.join(project,'rufmaker.json'),JSON.stringify(state,null,2)+'\n');
  const instance=(name,x,y)=>`${name},${x},${y},${crypto.randomBytes(4).toString('hex').toUpperCase()},0,1,1,4294967295,0,0`;
  const instances=layout.cells.map(c=>instance(c.kind,c.x,c.y));instances.push(instance('playerStart',layout.spawnX-17,layout.spawnY-23),instance('objBackground',64,32));
  const room=path.join(project,'rooms','rAvoidance');fs.writeFileSync(path.join(room,'instances.txt'),instances.join('\n')+'\n');
  let settings=fs.readFileSync(path.join(room,'room.txt'),'utf8');
  for(const [key,value] of Object.entries({width:layout.width,height:layout.height,roomspeed:state.fps,view_wview0:layout.width,view_hview0:layout.height,view_wport0:layout.width,view_hport0:layout.height}))settings=settings.replace(new RegExp('^'+key+'=.*$','m'),key+'='+value);
  fs.writeFileSync(path.join(room,'room.txt'),settings);
  const background=path.join(project,'objects','objBackground.gml');
  fs.writeFileSync(background,fs.readFileSync(background,'utf8').replaceAll('surface_create(800,608)','surface_create(room_width,room_height)').replaceAll('draw_rectangle(-100,-100,899,707,0)','draw_rectangle(-100,-100,room_width+100,room_height+100,0)'));
  const entry=path.join(project,'rufmaker-demo.gm82');let entryText=fs.readFileSync(entry,'utf8');entryText=entryText.replace(/^gameid=.*$/m,'gameid='+crypto.randomInt(100000000,2000000000));fs.writeFileSync(entry,entryText);
  execFileSync(process.execPath,['--input-type=module','-e',`const m=await import(${JSON.stringify(pathToFileURL(path.join(root,'studio','project.mjs')).href)});m.syncToGame(m.readState());`],{env:{...process.env,RUFMAKER_ROOT:root,RUFMAKER_PROJECT:descriptor},windowsHide:true});
  return {path:descriptor};
}
