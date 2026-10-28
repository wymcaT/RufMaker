import fs from 'node:fs';
import path from 'node:path';

// All generated writes are prepared before any GameMaker resource is replaced.
// A pending journal is rolled back after an interrupted commit; a committed one
// only needs cleanup. The authoring JSON participates in the same transaction.
export class ProjectSyncTransaction {
  constructor(project,hooks={}) {
    this.project=path.resolve(project);this.folder=path.join(this.project,'.rufmaker-sync');this.hooks=hooks;
    this.files=new Map();this.directories=new Set();
    this.io={existsSync:file=>this.exists(file),readFileSync:(file,encoding)=>this.read(file,encoding),readdirSync:(file,options)=>this.entries(file,options),mkdirSync:file=>this.directory(file),copyFileSync:(source,target)=>this.write(target,this.read(source))};
  }
  target(file) {const value=path.resolve(file),relative=path.relative(this.project,value);if(!relative||relative.startsWith('..'+path.sep)||relative==='..'||path.isAbsolute(relative))throw Error('同步文件超出工程目录');return value;}
  exists(file) {file=path.resolve(file);return this.files.has(file)||this.directories.has(file)||fs.existsSync(file);}
  read(file,encoding) {const row=this.files.get(path.resolve(file)),value=row?row.next:fs.readFileSync(file);return encoding?value.toString(encoding):value;}
  directory(file) {file=this.target(file);while(file!==this.project&&!fs.existsSync(file)){this.directories.add(file);file=path.dirname(file);}}
  write(file,value) {
    file=this.target(file);const row=this.files.get(file),before=row?row.before:fs.existsSync(file)?fs.readFileSync(file):null,next=Buffer.isBuffer(value)?Buffer.from(value):Buffer.from(value,'utf8');
    if(before&&before.equals(next)){this.files.delete(file);return;}
    this.files.set(file,{before,next});if(!fs.existsSync(path.dirname(file)))this.directory(path.dirname(file));
  }
  appendUnique(file,value) {const text=this.read(file,'utf8');if(!text.replace(/\r/g,'').split('\n').includes(value))this.write(file,text+(text&&!text.endsWith('\n')?'\n':'')+value+'\n');}
  entries(file,options) {
    file=path.resolve(file);const result=new Map();
    if(fs.existsSync(file))for(const item of fs.readdirSync(file,options))result.set(typeof item==='string'?item:item.name,item);
    for(const target of [...this.directories,...this.files.keys()])if(path.dirname(target)===file&&!result.has(path.basename(target))){const name=path.basename(target),directory=this.directories.has(target);result.set(name,options?.withFileTypes?{name,isDirectory:()=>directory,isFile:()=>!directory}:name);}
    return [...result.values()];
  }
  replace(target,bytes) {const temporary=target+'.rufmaker-sync.tmp';fs.writeFileSync(temporary,bytes);try{fs.renameSync(temporary,target);}catch(error){try{fs.unlinkSync(temporary);}catch{}throw error;}}
  cleanup() {if(path.dirname(this.folder)!==this.project||path.basename(this.folder)!=='.rufmaker-sync')throw Error('同步记录路径无效');fs.rmSync(this.folder,{recursive:true,force:true});}
  restore(journal) {
    for(const row of journal.files){const target=this.target(path.join(this.project,row.path));if(row.existed)this.replace(target,fs.readFileSync(path.join(this.folder,row.index+'.old')));else {if(fs.existsSync(target))fs.unlinkSync(target);if(fs.existsSync(target+'.rufmaker-sync.tmp'))fs.unlinkSync(target+'.rufmaker-sync.tmp');}}
    for(const directory of (journal.directories||[]).slice().sort((a,b)=>b.length-a.length)){const target=this.target(path.join(this.project,directory));if(fs.existsSync(target)&&fs.readdirSync(target).length===0)fs.rmdirSync(target);}
  }
  recover() {
    if(!fs.existsSync(this.folder))return false;const manifest=path.join(this.folder,'journal.json');
    if(!fs.existsSync(manifest)){this.cleanup();return false;}
    const journal=JSON.parse(fs.readFileSync(manifest,'utf8'));if(journal.version!==1||!Array.isArray(journal.files)||journal.files.some(row=>!Number.isInteger(row.index)||row.index<0||row.index>=journal.files.length||typeof row.path!=='string')||!['pending','committed'].includes(journal.status))throw Error('工程同步恢复记录无效，请保留 .rufmaker-sync 目录');
    if(journal.status==='pending')this.restore(journal);this.cleanup();return journal.status==='pending';
  }
  commit() {
    if(!this.files.size)return;
    const rows=[...this.files];for(const [file,row] of rows){const current=fs.existsSync(file)?fs.readFileSync(file):null;if(current===null?row.before!==null:row.before===null||!current.equals(row.before))throw Error('工程文件已被其他程序修改，请重新读取后再保存：'+path.relative(this.project,file));}
    fs.mkdirSync(this.folder);const journal={version:1,status:'pending',files:rows.map(([file,row],index)=>({path:path.relative(this.project,file),index,existed:row.before!==null})),directories:[...this.directories].map(file=>path.relative(this.project,file))};
    let committed=false;try {
      for(let index=0;index<rows.length;index++){const row=rows[index][1];fs.writeFileSync(path.join(this.folder,index+'.new'),row.next);if(row.before!==null)fs.writeFileSync(path.join(this.folder,index+'.old'),row.before);}
      this.replace(path.join(this.folder,'journal.json'),Buffer.from(JSON.stringify(journal)));
      this.hooks.prepared?.(journal);
      for(let index=0;index<rows.length;index++){const [file,row]=rows[index];fs.mkdirSync(path.dirname(file),{recursive:true});this.replace(file,row.next);this.hooks.applied?.(index,file);}
      journal.status='committed';this.replace(path.join(this.folder,'journal.json'),Buffer.from(JSON.stringify(journal)));committed=true;this.cleanup();
    } catch(error) {
      if(committed)return; // Resource set is complete; cleanup can finish on the next load.
      if(fs.existsSync(path.join(this.folder,'journal.json'))){try{this.restore(journal);this.cleanup();}catch(restoreError){throw Error('保存失败，回滚尚未完成。请保留 .rufmaker-sync 并重新打开工程。原因：'+error.message+'；恢复：'+restoreError.message);}}
      else this.cleanup();throw error;
    }
  }
}

export function recoverProjectSync(project){return new ProjectSyncTransaction(project).recover();}
