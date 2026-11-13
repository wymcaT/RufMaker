import {readProjectWaveform} from './waveform-cache.mjs';
import {spriteMetadata as readSpriteMetadata} from './sprite-mask.mjs';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import readline from 'node:readline';
import {createProject,copySong,terrainLayout} from './project-files.mjs';
import {scanNativeFolder,importNativeProject,analyzeNativeProject,refreshNativeState} from './native-import.mjs';
import {nativeEventBlock,replaceNativeEventRange} from './native-blocks.mjs';
import {root,project,projectFile,descriptor,projectName,pack,readState,syncToGame,roomInfo,inventory,sourcePath,moveRoomInstance,editRoomInstances,importSprite,ensureRainbowSprite} from './project.mjs';
import {fileRevision} from './native-room.mjs';
import {ProjectSyncTransaction} from './project-sync.mjs';
import {readGmAsset,editGmAsset,nativePathAssets,gmObjectReferences} from './gm-resources.mjs';
import {convertGmObject} from './gm-convert.mjs';
import {describeInstanceCode,updateInstanceCode} from './gm-instance-fields.mjs';
import {ensureBuild,exportStatus,packageGame,runNative,openNative,nativeRunStatus,cancelNativeRun,closeNativePlay} from './native.mjs';
import {prepareNativeSession} from './native-preview.mjs';
import {prepareNativeFilm,nativeFilmStatus} from './native-film.mjs';
import {buildDir,gameMaker} from './project.mjs';

function spriteMetadata() {return readSpriteMetadata(project,inventory().sprites);}

function resourceSource(file){
  const bytes=fs.readFileSync(file);return {content:bytes.toString('utf8'),hash:fileRevision(bytes)};
}
function saveResourceSource(file,{content,hash}){
  const transaction=new ProjectSyncTransaction(project);transaction.recover();
  const bytes=Buffer.from(content,'utf8');
  transaction.write(file,bytes);
  const prepared=transaction.files.get(file);
  if(hash&&prepared&&(!prepared.before||fileRevision(prepared.before)!==hash))throw Error('GameMaker 已修改这个 GML 文件，请重新打开后再编辑。');
  transaction.commit();
  return {ok:true,hash:fileRevision(bytes)};
}

function loadProject() {
  const state=readState();
  if(state.nativeProject?.imported)refreshNativeState(projectFile,state);
  syncToGame(state);
  const waveform=readProjectWaveform(project,state.music);
  return {state,inventory:inventory(),room:roomInfo(state.room),project,projectFile,descriptor,projectName,pack,spriteMeta:spriteMetadata(),nativePaths:nativePathAssets(project),waveform};
}

async function execute(command,payload={}) {
  switch(command) {
    case 'load': return loadProject();
    case 'new-project': return createProject(root,payload);
    case 'native-scan': return scanNativeFolder(payload.folder,payload.entry,payload.room,payload.controller);
    case 'native-import': return importNativeProject(root,payload);
    case 'native-film-prepare': return prepareNativeFilm({root,project,projectFile,gameMaker,state:readState()});
    case 'native-film-status': return nativeFilmStatus(project,readState(),{projectFile});
    case 'native-preview': {const state=readState();if(!state.nativeProject?.imported)throw new Error('当前不是原生导入工程');const cacheDir=path.join(root,'build','native-projects',crypto.createHash('sha256').update(project.toLowerCase()).digest('hex').slice(0,16));return prepareNativeSession({project,projectFile,buildDir,cacheDir,gameMaker,state,frame:payload.frame||0,playable:false});}
    case 'terrain-preview': return terrainLayout(payload);
    case 'import-song': return copySong(project,payload.path);
    case 'audio-cache': fs.mkdirSync(path.join(project,'cache'),{recursive:true});fs.writeFileSync(path.join(project,'cache','rufmaker-waveform.json'),JSON.stringify(payload));return {ok:true};
    case 'save': syncToGame(payload); return {room:roomInfo(payload.room),export:exportStatus(false)};
    case 'room-read': return roomInfo(payload.room);
    case 'gm-resources-read': return {room:roomInfo(payload.room),inventory:inventory(),spriteMeta:spriteMetadata(),nativePaths:nativePathAssets(project)};
    case 'gm-asset-read': return readGmAsset(project,payload);
    case 'gm-object-convert': return convertGmObject(project,payload);
    case 'gm-asset-edit': return editGmAsset(project,payload);
    case 'gm-object-references': return gmObjectReferences(project,payload.name);
    case 'gm-native-timing': {
      const state=readState();
      return state.nativeProject?.imported?analyzeNativeProject(projectFile,state.room,state.nativeProject.controller,true):null;
    }
    case 'gm-instance-code-read': return describeInstanceCode(project,payload.room,payload.id);
    case 'gm-instance-code-save': {let codeHash;const room=editRoomInstances(payload.room,payload.revision,(rows,transaction)=>{
      const index=rows.findIndex(row=>row.split(',')[3]===payload.id);
      if(index<0)throw Error('房间实例已不存在，请重新读取。');
      const result=updateInstanceCode(project,payload.room,payload.id,payload,transaction.io);
      const fields=rows[index].split(',');
      if(fields.length!==10)throw Error('当前房间格式无法安全写回实例创建代码。');
      fields[9]=result.source.trim()?'1':'0';rows[index]=fields.join(',');
      codeHash=fileRevision(rows[index]+'\n'+result.source);
      transaction.write(result.file,result.source);
      const prepared=transaction.files.get(result.file);
      if(prepared&&(prepared.before?.toString('utf8')||'')!==result.before)throw Error('GameMaker 已修改实例代码，请重新打开后再编辑。');
    });return {...room,codeHash};}
    case 'source-read': return resourceSource(sourcePath(payload.kind,payload.name));
    case 'asset-info': return {inventory:inventory(),spriteMeta:spriteMetadata()};
    case 'source-save': return saveResourceSource(sourcePath(payload.kind,payload.name),payload);
    case 'native-source-read': return resourceSource(nativeSourcePath(payload.path));
    case 'native-source-save': return saveResourceSource(nativeSourcePath(payload.path),payload);
    case 'native-event-read': {const file=nativeSourcePath(payload.event.file);return nativeEventBlock(fs.readFileSync(file,'utf8'),verifiedNativeEvent(payload.event));}
    case 'native-event-save': {const file=nativeSourcePath(payload.event.file),source=fs.readFileSync(file,'utf8');const range=payload.start===undefined?nativeEventBlock(source,verifiedNativeEvent(payload.event)):payload;const saved=replaceNativeEventRange(source,range,payload.content,payload.hash);fs.writeFileSync(file,saved.content,'utf8');return {ok:true,hash:saved.hash,start:saved.start,end:saved.end};}
    case 'move-instance': return moveRoomInstance(payload.room,payload.index,payload.x,payload.y,payload.revision,payload.id,payload.transform);
    case 'delete-instance': {
      return editRoomInstances(payload.room,payload.revision,rows=>{
        const index=rows.findIndex(row=>{const fields=row.split(',');return fields[3]===payload.id&&fields[0]!=='objRufController';});
        if(index<0)throw Error('找不到可删除的房间实例');
        rows.splice(index,1);
      });
    }
    case 'restore-room': {
      const objects=inventory().objects;
      if(!Array.isArray(payload.instances)||payload.instances.some(x=>!objects.includes(x.name)||typeof x.raw!=='string'||x.raw.includes('\n')||x.raw.split(',').length!==10))throw new Error('无效的房间实例快照');
      return editRoomInstances(payload.name,payload.revision,rows=>{
        const controllerIndex=rows.findIndex(row=>row.startsWith('objRufController,'));
        const controller=rows[controllerIndex];rows.length=0;
        for(const instance of payload.instances)if(instance.name!=='objRufController')rows.push(instance.raw);
        if(controller)rows.splice(Math.min(controllerIndex,rows.length),0,controller);
      });
    }
    case 'clone-instance': {
      const a=String(payload.raw||'').split(',');
      if(a.length!==10||payload.raw.includes('\n')||payload.raw.includes('\r')||!inventory().objects.includes(a[0])||a[0]==='objRufController')throw new Error('无效的房间实例');
      const oldId=a[3];
      a[3]=crypto.randomBytes(4).toString('hex').toUpperCase();
      return editRoomInstances(payload.room,payload.revision,(rows,transaction)=>{
        if(a[9]==='1'&&/^[0-9a-f]{8}$/i.test(oldId)){
          const folder=path.join(project,'rooms',payload.room),source=path.join(folder,oldId+'.gml');
          if(transaction.exists(source))transaction.write(path.join(folder,a[3]+'.gml'),transaction.read(source));
          else a[9]='0';
        }
        rows.push(a.join(','));
      });
    }
    case 'add-instance': {
      if (!inventory().objects.includes(payload.name) || !inventory().rooms.includes(payload.room) || !Number.isFinite(payload.x)||!Number.isFinite(payload.y)) throw new Error('无效的房间对象');
      const state=readState(),plain=!state.nativeProject?.imported&&state.room===payload.room;
      const name=plain?({block:'objRufBlock',platform:'objRufPlatform'}[payload.name]||payload.name):payload.name;
      const id=crypto.randomBytes(4).toString('hex').toUpperCase();
      return editRoomInstances(payload.room,payload.revision,rows=>rows.push(`${name},${Math.round(payload.x)},${Math.round(payload.y)},${id},0,1,1,4294967295,0,0`));
    }
    case 'import': return {name:importSprite(payload.folder,payload.name),inventory:inventory(),spriteMeta:spriteMetadata()};
    case 'ensure-rainbow': ensureRainbowSprite();return {inventory:inventory(),spriteMeta:spriteMetadata()};
    case 'run': syncToGame(payload.state); return await runNative(payload.frame, Boolean(payload.godMode));
    case 'run-progress': return nativeRunStatus();
    case 'run-cancel': return cancelNativeRun();
    case 'export': syncToGame(payload); await ensureBuild(); return packageGame(false);
    case 'open-native': return {pid:await openNative()};
    case 'status': return exportStatus();
    default: throw new Error('未知操作：'+command);
  }
}

function nativeSourcePath(relative){if(!readState().nativeProject?.imported||typeof relative!=='string'||!relative.endsWith('.gml')||!['objects','scripts','timelines','rooms'].includes(relative.split('/')[0]))throw new Error('无效的原生 GML 路径');const file=path.resolve(project,relative);if(!file.startsWith(path.resolve(project)+path.sep)||!fs.existsSync(file))throw new Error('原生 GML 文件不存在');return file;}
function verifiedNativeEvent(event){const state=readState(),report=analyzeNativeProject(projectFile,state.room,state.nativeProject.controller,true);const found=report.events.find(e=>e.file===event.file&&e.line===event.line&&e.kind===event.kind&&e.expression===event.expression);if(!found)throw new Error('所选原生事件已变化，请重新读取工程。');return found;}

const input=readline.createInterface({input:process.stdin,crlfDelay:Infinity});
input.on('line',async line=>{
  let request;
  try {
    request=JSON.parse(line);
    if(request.command==='shutdown') {await closeNativePlay();input.close();return;}
    const result=await execute(request.command,request.payload);
    process.stdout.write(JSON.stringify({id:request.id,ok:true,result})+'\n');
  } catch(error) {
    process.stdout.write(JSON.stringify({id:request?.id||0,ok:false,error:error.message})+'\n');
  }
});
input.on('close',()=>{process.stdin.destroy();});
