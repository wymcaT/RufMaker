import fs from 'node:fs';
import path from 'node:path';
import { spawn } from 'node:child_process';
import crypto from 'node:crypto';
import { root, project, projectFile, makerFile, gameMaker, buildDir, gameExe, readState } from './project.mjs';
import {prepareNativeSession,copyNativeExtras} from './native-preview.mjs';
import {NativePlaySession} from './native-play-session.mjs';
import {waitForNativeReplay} from './native-replay.mjs';

const exportedExe=gameExe;
let buildTask=null;
let previewProcess=null;
let nativeReplay=null;
let nativePlay=null;

export function nativeRunStatus(){return nativeReplay?{phase:nativeReplay.phase,frame:nativeReplay.frame,target:nativeReplay.target,room:nativeReplay.room,error:nativeReplay.error||''}:null;}
export async function cancelNativeRun(){const session=nativeReplay;session?.cancel.abort();if(nativePlay)await nativePlay.stop();if(session?.plain&&previewProcess&&previewProcess.exitCode===null)previewProcess.kill();if(session)session.phase='ended';return {cancelled:!!session};}
export async function closeNativePlay(){if(nativePlay){const previous=nativePlay;nativePlay=null;await previous.stop();}}

function sourceStamp() {
  let latest=Math.max(fs.statSync(makerFile).mtimeMs,fs.statSync(projectFile).mtimeMs);
  const visit=dir=>{
    if (!fs.existsSync(dir)) return;
    for (const item of fs.readdirSync(dir,{withFileTypes:true})) {
      const file=path.join(dir,item.name);
      if (item.isDirectory()) visit(file);
      else if (item.isFile()) latest=Math.max(latest,fs.statSync(file).mtimeMs);
    }
  };
  for (const folder of ['backgrounds','datafiles','fonts','objects','paths','rooms','scripts','settings','sounds','sprites','timelines','triggers','Data']) visit(path.join(project,folder));
  return latest;
}

export function exportStatus() {
  const exists=fs.existsSync(exportedExe);
  const modified=exists?fs.statSync(exportedExe).mtimeMs:0;
  return {expectedExe:exportedExe,exists,fresh:exists&&modified>=sourceStamp(),modified,building:buildTask!==null};
}

export async function ensureBuild(force=false) {
  if (buildTask) return buildTask;
  if (!force && exportStatus().fresh) return {cached:true,...exportStatus()};
  buildTask=(async()=>{
    readState();
    const stamp=sourceStamp();
    const dir=path.join(buildDir,'compiler');
    fs.mkdirSync(dir,{recursive:true});
    const output=path.join(dir,`game-${Date.now()}.exe`);
    const log=path.join(dir,'latest.log');
    // GM82 Hub uses this order: project first, then --build and the EXE path.
    const args=[projectFile,'--build',output];
    fs.writeFileSync(log,`${gameMaker}\n${JSON.stringify(args)}\n`);
    await new Promise((resolve,reject)=>{
      const child=spawn(gameMaker,args,{cwd:path.dirname(gameMaker),stdio:['ignore','pipe','pipe']});
      const timer=setTimeout(()=>{child.kill();reject(new Error(`GameMaker 编译超时，请在原生编辑器中检查工程。构建记录：${log}`));},180000);
      for (const stream of [child.stdout,child.stderr]) stream.on('data',chunk=>fs.appendFileSync(log,chunk));
      child.once('error',error=>{clearTimeout(timer);reject(error);});
      child.once('close',code=>{clearTimeout(timer);code===0?resolve():reject(new Error(`GameMaker 编译失败（${code}）。构建记录：${log}`));});
    });
    if (!fs.existsSync(output) || fs.statSync(output).size<1024) throw new Error(`GameMaker 未生成游戏 EXE。构建记录：${log}`);
    const fd=fs.openSync(output,'r');
    const signature=Buffer.alloc(2);
    try {fs.readSync(fd,signature,0,2,0);} finally {fs.closeSync(fd);}
    if (signature.toString()!=='MZ') throw new Error('GameMaker 输出不是 Windows 可执行文件');
    if (sourceStamp()!==stamp) throw new Error('编译期间工程已修改，请再次试玩或导出');
    fs.mkdirSync(path.dirname(exportedExe),{recursive:true});fs.copyFileSync(output,exportedExe);
    fs.unlinkSync(output);
    fs.appendFileSync(log,`\n完成：${exportedExe} (${fs.statSync(exportedExe).size} bytes)\n`);
    return {cached:false,log,...exportStatus()};
  })();
  try {return await buildTask;} finally {buildTask=null;}
}

export async function openNative() {
  return new Promise((resolve,reject)=>{
    const child=spawn(gameMaker,[projectFile],{cwd:path.dirname(gameMaker),detached:true,stdio:'ignore'});
    child.once('error',reject);
    child.once('spawn',()=>{child.unref();resolve(child.pid);});
  });
}

export function packageGame(preview,frame=0) {
  if (!exportStatus().fresh) throw new Error('工程已修改，请重新编译游戏');
  const dir=path.join(buildDir,preview?'preview':'release');
  fs.mkdirSync(dir,{recursive:true});
  const exe=path.join(dir,'rufmaker-game.exe');
  fs.copyFileSync(exportedExe,exe);
  if(fs.existsSync(path.join(project,'Data')))fs.cpSync(path.join(project,'Data'),path.join(dir,'Data'),{recursive:true,force:true});else fs.mkdirSync(path.join(dir,'Data'),{recursive:true});
  if(readState().nativeProject?.imported)copyNativeExtras(project,dir);else for(const item of fs.readdirSync(project,{withFileTypes:true}).filter(e=>e.isFile()&&/\.(dll|ini|dat|cfg)$/i.test(e.name)))fs.copyFileSync(path.join(project,item.name),path.join(dir,item.name));
  const ini=path.join(dir,'Data','rufmaker_preview.ini');
  const status=path.join(dir,'Data','rufmaker_preview_status.ini');
  if (fs.existsSync(status)) fs.unlinkSync(status);
  for(const name of ['rufmaker_plain_checkpoint.sav','rufmaker_plain_restore','rufmaker_plain_restore_count.ini']){const file=path.join(dir,'Data',name);if(fs.existsSync(file))fs.unlinkSync(file);}
  if (preview) fs.writeFileSync(ini,`[preview]\r\nframe=${Math.max(0,Math.min(readState().duration,Math.floor(frame)))}\r\n`);
  else if (fs.existsSync(ini)) fs.unlinkSync(ini);
  return {exe,dir,bytes:fs.statSync(exe).size};
}

async function stopPreviousPreview() {
  await closeNativePlay();
  if (previewProcess && previewProcess.exitCode===null) {
    const previous=previewProcess;
    await new Promise((resolve,reject)=>{
      const timer=setTimeout(()=>reject(new Error('上一次试玩尚未退出，请关闭游戏窗口后重试')),3000);
      previous.once('close',()=>{clearTimeout(timer);resolve();});
      if (!previous.kill()) {clearTimeout(timer);resolve();}
    });
  }
}

export async function runNative(frame=0) {
  const state=readState();
  if(state.nativeProject?.imported){
    const target=Math.max(0,Math.min(state.duration,Math.round(frame))),session={phase:'compile',frame:0,target,room:'',cancel:new AbortController()};nativeReplay=session;
    let play;
    try {
      await stopPreviousPreview();session.cancel.signal.throwIfAborted();
      const cacheDir=path.join(root,'build','native-projects',crypto.createHash('sha256').update(project.toLowerCase()).digest('hex').slice(0,16));
      const result=await prepareNativeSession({project,projectFile,buildDir,cacheDir,gameMaker,state,frame:target,playable:true});
      session.cancel.signal.throwIfAborted();session.phase='replay';
      play=new NativePlaySession({result,target,cancel:session.cancel,onProgress:progress=>Object.assign(session,progress)});nativePlay=play;
      const status=await play.start();return {...result,pid:play.child.pid,ready:true,status};
    } catch(error) {
      const cancelled=session.cancel.signal.aborted;
      if(play){await play.stop().catch(()=>{});if(nativePlay===play)nativePlay=null;}
      if(cancelled)throw new Error('已取消原生试玩重放。');
      throw error;
    } finally {if(nativeReplay===session&&!nativePlay)nativeReplay=null;}
  }
  const target=Math.max(0,Math.min(state.duration,Math.round(frame))),session={plain:true,phase:'compile',frame:0,target,room:'',cancel:new AbortController()};nativeReplay=session;
  try{
  await stopPreviousPreview();session.cancel.signal.throwIfAborted();
  const build=await ensureBuild();
  session.cancel.signal.throwIfAborted();const result=packageGame(true,target);session.phase='launch';
  const child=spawn(result.exe,[],{cwd:result.dir,detached:true,windowsHide:true,stdio:'ignore'});
  previewProcess=child;
  let launchError;
  child.once('error',error=>{launchError=error;});
  child.unref();
  const statusFile=path.join(result.dir,'Data','rufmaker_preview_status.ini');
  await new Promise((resolve,reject)=>{child.once('spawn',resolve);child.once('error',reject);});
  const status=await waitForNativeReplay({child,statusFile,target,dir:result.dir,signal:session.cancel.signal,onProgress:progress=>Object.assign(session,progress,{phase:'replay'})});
  if(launchError)throw launchError;return {...result,pid:child.pid,ready:true,status,cached:build.cached};
  }catch(error){if(previewProcess&&previewProcess.exitCode===null)previewProcess.kill();if(session.cancel.signal.aborted)throw new Error('已取消原生试玩重放。');throw error;}finally{if(nativeReplay===session)nativeReplay=null;}
}
