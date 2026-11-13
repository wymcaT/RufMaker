import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';

const resourceFolders=['backgrounds','datafiles','fonts','objects','paths','rooms','scripts','settings','sounds','sprites','timelines','triggers','Data'];
// Read in bounded chunks: songs and sprites must also invalidate the cache when
// an external tool preserves both length and timestamps.
export function fileDigest(file) {
  const hash=crypto.createHash('sha256'),buffer=Buffer.allocUnsafe(64*1024),fd=fs.openSync(file,'r');
  try {let count;while((count=fs.readSync(fd,buffer,0,buffer.length,null))>0)hash.update(buffer.subarray(0,count));}
  finally {fs.closeSync(fd);}
  return hash.digest('hex');
}
export function buildFingerprint(project,gameMaker='',projectFile='') {
  const hash=crypto.createHash('sha256').update('rufmaker-build-v1\0'+path.resolve(project)+'\0'+gameMaker+'\0'+projectFile+'\0');
  const visit=file=>{
    const stat=fs.lstatSync(file),relative=path.relative(project,file).replaceAll('\\','/');
    if(stat.isSymbolicLink())throw new Error(`构建输入不支持符号链接：${file}`);
    if(stat.isDirectory()){
      hash.update(JSON.stringify(['directory',relative]));
      for(const name of fs.readdirSync(file).sort())visit(path.join(file,name));
    }else if(stat.isFile())hash.update(JSON.stringify(['file',relative,fileDigest(file)]));
    else throw new Error(`无法读取构建输入：${file}`);
  };
  // Include root resource/configuration files as well as the GM resource trees,
  // but never our build/cache output directories.
  for(const entry of fs.readdirSync(project,{withFileTypes:true}).sort((a,b)=>a.name<b.name?-1:a.name>b.name?1:0)){
    if(entry.isFile()||entry.isSymbolicLink()||resourceFolders.includes(entry.name))visit(path.join(project,entry.name));
  }
  if(gameMaker&&fs.existsSync(gameMaker))hash.update(fileDigest(gameMaker));
  return hash.digest('hex');
}
export function buildIsFresh(exe,manifest,fingerprint) {
  try {
    const saved=JSON.parse(fs.readFileSync(manifest,'utf8'));
    return saved!==null&&typeof saved==='object'&&saved.version===1&&saved.source===fingerprint&&saved.output===fileDigest(exe);
  }catch(error){if(error.code==='ENOENT'||error instanceof SyntaxError)return false;throw error;}
}
export function recordBuild(exe,manifest,fingerprint) {
  const temporary=manifest+'.'+crypto.randomUUID()+'.tmp';
  try {
    fs.writeFileSync(temporary,JSON.stringify({version:1,source:fingerprint,output:fileDigest(exe)})+'\n');
    fs.renameSync(temporary,manifest);
  }finally{if(fs.existsSync(temporary))fs.unlinkSync(temporary);}
}
