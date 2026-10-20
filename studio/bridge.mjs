import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import readline from 'node:readline';
import {createProject,copySong,terrainLayout} from './project-files.mjs';
import {root,project,projectFile,descriptor,projectName,pack,readState,syncToGame,roomInfo,inventory,sourcePath,moveRoomInstance,importSprite} from './project.mjs';
import {ensureBuild,exportStatus,packageGame,runNative,openNative} from './native.mjs';

function spriteMetadata() {
  return Object.fromEntries(inventory().sprites.map(name=>{
    const file=path.join(project,'sprites',name,'sprite.txt');
    const values=Object.fromEntries(fs.readFileSync(file,'utf8').split(/\r?\n/).filter(x=>x.includes('=')).map(x=>x.split('=')));
    return [name,{originX:Number(values.origin_x)||0,originY:Number(values.origin_y)||0,frames:Number(values.frames)||1}];
  }));
}

function loadProject() {
  const state=readState();
  let waveform={duration:0,peaks:[]};
  const cache=path.join(project,'cache','rufmaker-waveform.json');
  if(fs.existsSync(cache)){const data=JSON.parse(fs.readFileSync(cache,'utf8'));if(data.source===state.music)waveform=data;}
  else if(state.music==='Data/Music/BGM.ogg')waveform={...JSON.parse(fs.readFileSync(new URL('./waveform.json',import.meta.url),'utf8')),source:state.music};
  return {state,inventory:inventory(),room:roomInfo(state.room),project,projectFile,descriptor,projectName,pack,spriteMeta:spriteMetadata(),waveform};
}

async function execute(command,payload={}) {
  switch(command) {
    case 'load': return loadProject();
    case 'new-project': return createProject(root,payload);
    case 'terrain-preview': return terrainLayout(payload);
    case 'import-song': return copySong(project,payload.path);
    case 'audio-cache': fs.mkdirSync(path.join(project,'cache'),{recursive:true});fs.writeFileSync(path.join(project,'cache','rufmaker-waveform.json'),JSON.stringify(payload));return {ok:true};
    case 'save': syncToGame(payload); return {room:roomInfo(payload.room),export:exportStatus()};
    case 'source-read': return {content:fs.readFileSync(sourcePath(payload.kind,payload.name),'utf8')};
    case 'source-save': fs.writeFileSync(sourcePath(payload.kind,payload.name),payload.content,'utf8'); return {ok:true};
    case 'move-instance': moveRoomInstance(payload.room,payload.index,payload.x,payload.y); return roomInfo(payload.room);
    case 'delete-instance': {
      const room=roomInfo(payload.room),target=room.instances.find(x=>x.id===payload.id);
      if(!target||target.name==='objRufController')throw new Error('找不到可删除的房间实例');
      const file=path.join(project,'rooms',payload.room,'instances.txt');
      fs.writeFileSync(file,room.instances.filter(x=>x.id!==payload.id).map(x=>x.raw).join('\n')+'\n');
      return roomInfo(payload.room);
    }
    case 'restore-room': {
      const current=roomInfo(payload.name),objects=inventory().objects;
      if(!Array.isArray(payload.instances)||payload.instances.some(x=>!objects.includes(x.name)||typeof x.raw!=='string'||x.raw.includes('\n')||x.raw.split(',').length!==10))throw new Error('无效的房间实例快照');
      fs.writeFileSync(path.join(project,'rooms',current.name,'instances.txt'),payload.instances.map(x=>x.raw).join('\n')+'\n');return roomInfo(current.name);
    }
    case 'clone-instance': {
      const room=roomInfo(payload.room),a=String(payload.raw||'').split(',');
      if(a.length!==10||payload.raw.includes('\n')||payload.raw.includes('\r')||!inventory().objects.includes(a[0])||a[0]==='objRufController')throw new Error('无效的房间实例');
      a[3]=crypto.randomBytes(4).toString('hex').toUpperCase();
      fs.appendFileSync(path.join(project,'rooms',room.name,'instances.txt'),a.join(',')+'\n');return roomInfo(room.name);
    }
    case 'add-instance': {
      if (!inventory().objects.includes(payload.name) || !inventory().rooms.includes(payload.room) || !Number.isFinite(payload.x)||!Number.isFinite(payload.y)) throw new Error('无效的房间对象');
      const id=crypto.randomBytes(4).toString('hex').toUpperCase();
      fs.appendFileSync(path.join(project,'rooms',payload.room,'instances.txt'),`${payload.name},${Math.round(payload.x)},${Math.round(payload.y)},${id},0,1,1,4294967295,0,0\n`);
      return roomInfo(payload.room);
    }
    case 'import': return {name:importSprite(payload.folder,payload.name),inventory:inventory(),spriteMeta:spriteMetadata()};
    case 'run': syncToGame(payload.state); return await runNative(payload.frame);
    case 'export': syncToGame(payload); await ensureBuild(); return packageGame(false);
    case 'open-native': return {pid:await openNative()};
    case 'status': return exportStatus();
    default: throw new Error('未知操作：'+command);
  }
}

const input=readline.createInterface({input:process.stdin,crlfDelay:Infinity});
input.on('line',async line=>{
  let request;
  try {
    request=JSON.parse(line);
    if(request.command==='shutdown') {input.close();return;}
    const result=await execute(request.command,request.payload);
    process.stdout.write(JSON.stringify({id:request.id,ok:true,result})+'\n');
  } catch(error) {
    process.stdout.write(JSON.stringify({id:request?.id||0,ok:false,error:error.message})+'\n');
  }
});
input.on('close',()=>{process.stdin.destroy();});
