import fs from 'node:fs';
import path from 'node:path';
import { spawn } from 'node:child_process';
import { root, project, projectFile, makerFile, gameMaker, buildDir, gameExe, readState } from './project.mjs';

const exportedExe=gameExe;
let buildTask=null;
let previewProcess=null;

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
    fs.copyFileSync(output,exportedExe);
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
  fs.cpSync(path.join(project,'Data'),path.join(dir,'Data'),{recursive:true,force:true});
  const ini=path.join(dir,'Data','rufmaker_preview.ini');
  const status=path.join(dir,'Data','rufmaker_preview_status.ini');
  if (fs.existsSync(status)) fs.unlinkSync(status);
  if (preview) fs.writeFileSync(ini,`[preview]\r\nframe=${Math.max(0,Math.min(readState().duration,Math.floor(frame)))}\r\n`);
  else if (fs.existsSync(ini)) fs.unlinkSync(ini);
  return {exe,dir,bytes:fs.statSync(exe).size};
}

export async function runNative(frame=0) {
  const build=await ensureBuild();
  if (previewProcess && previewProcess.exitCode===null) {
    const previous=previewProcess;
    await new Promise((resolve,reject)=>{
      const timer=setTimeout(()=>reject(new Error('上一次试玩尚未退出，请关闭游戏窗口后重试')),3000);
      previous.once('close',()=>{clearTimeout(timer);resolve();});
      if (!previous.kill()) {clearTimeout(timer);resolve();}
    });
  }
  const result=packageGame(true,frame);
  const child=spawn(result.exe,[],{cwd:result.dir,detached:true,stdio:'ignore'});
  previewProcess=child;
  let launchError;
  child.once('error',error=>{launchError=error;});
  child.unref();
  const statusFile=path.join(result.dir,'Data','rufmaker_preview_status.ini');
  const deadline=Date.now()+15000;
  while (Date.now()<deadline) {
    if (launchError) throw launchError;
    if (fs.existsSync(statusFile)) {
      const status=Object.fromEntries(fs.readFileSync(statusFile,'utf8').split(/\r?\n/).filter(x=>x.includes('=')).map(x=>{const i=x.indexOf('=');return [x.slice(0,i).trim(),x.slice(i+1).trim().replace(/^"|"$/g,'')];}));
      return {...result,pid:child.pid,ready:true,status,cached:build.cached};
    }
    if (child.exitCode!==null) throw new Error('游戏在进入试玩房间前退出，请查看游戏错误记录');
    await new Promise(resolve=>setTimeout(resolve,100));
  }
  return {...result,pid:child.pid,ready:false,cached:build.cached};
}
