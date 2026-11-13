import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {fileDigest} from './native-build-cache.mjs';

const manifestName='.rufmaker-package.json';
function filesIn(dir) {
  const files=[];
  const visit=relative=>{
    const file=path.join(dir,relative),stat=fs.lstatSync(file);
    if(stat.isSymbolicLink())throw new Error(`导出目录不能包含符号链接：${file}`);
    if(stat.isDirectory())for(const name of fs.readdirSync(file))visit(path.join(relative,name));
    else if(stat.isFile())files.push(relative);
    else throw new Error(`导出目录包含不支持的文件：${file}`);
  };
  visit('');return files;
}
function removeEmptyDirectories(dir) {
  for(const entry of fs.readdirSync(dir,{withFileTypes:true}))if(entry.isDirectory())removeEmptyDirectories(path.join(dir,entry.name));
  if(fs.readdirSync(dir).length===0)fs.rmdirSync(dir);
}
// Stage fresh managed assets, preserving runtime saves/manual additions from
// packages carrying our manifest. Legacy packages are backed up in full once:
// without a manifest we cannot distinguish obsolete assets from user files.
export function replacePackage(destination,populate,io=fs,{skipUnmanaged=[]}={}) {
  io.mkdirSync(path.dirname(destination),{recursive:true});
  const stage=io.mkdtempSync(destination+'.staging-');
  let previousDir=null,published=false;
  try {
    populate(stage);
    const managed=filesIn(stage),entries=Object.fromEntries(managed.map(name=>[name.replaceAll('\\','/'),fileDigest(path.join(stage,name))]));
    let oldFiles=[],oldManifest=null;
    if(io.existsSync(destination)){
      oldFiles=filesIn(destination);
      try {
        const parsed=JSON.parse(fs.readFileSync(path.join(destination,manifestName),'utf8'));
        if(parsed?.version===1&&parsed.files&&typeof parsed.files==='object'&&!Array.isArray(parsed.files))oldManifest=parsed.files;
      }catch(error){if(error.code!=='ENOENT'&&!(error instanceof SyntaxError))throw error;}
      if(oldManifest)for(const name of oldFiles){
        const relative=name.replaceAll('\\','/');
        if(skipUnmanaged.includes(relative)||name===manifestName||Object.hasOwn(oldManifest,relative)||Object.hasOwn(entries,relative))continue;
        const target=path.join(stage,name);fs.mkdirSync(path.dirname(target),{recursive:true});fs.copyFileSync(path.join(destination,name),target);
      }
    }
    fs.writeFileSync(path.join(stage,manifestName),JSON.stringify({version:1,files:entries})+'\n');
    if(io.existsSync(destination)){
      previousDir=destination+'.previous-'+crypto.randomUUID();
      io.renameSync(destination,previousDir);
    }
    try {io.renameSync(stage,destination);published=true;}
    catch(error){
      if(previousDir){
        try {io.renameSync(previousDir,destination);previousDir=null;}
        catch(restoreError){throw new Error(`${error.message}；旧导出仍保存在 ${previousDir}，自动恢复失败：${restoreError.message}`);}
      }
      throw error;
    }
    // Retain only files that are not already present byte-for-byte in the new
    // package. This avoids a full song/EXE backup on every F5. Changed/removed
    // files and modified managed saves remain recoverable in previousDir.
    if(previousDir&&oldManifest){
      try {
        for(const name of oldFiles){
          const old=path.join(previousDir,name),current=path.join(destination,name);
          if(fs.existsSync(current)&&fs.lstatSync(current).isFile()&&fileDigest(old)===fileDigest(current))fs.unlinkSync(old);
        }
        removeEmptyDirectories(previousDir);
        if(!fs.existsSync(previousDir))previousDir=null;
      }catch { /* A backup cleanup error must not invalidate a published package. */ }
    }
    return {previousDir};
  }finally {
    if(!published&&io.existsSync(stage))io.rmSync(stage,{recursive:true,force:true});
  }
}
