import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {spawnSync} from 'node:child_process';
import {fileURLToPath,pathToFileURL} from 'node:url';
import {ProjectSyncTransaction,recoverProjectSync} from './project-sync.mjs';

const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),base=path.join(root,'build/optimization-check/save-transactions');fs.mkdirSync(base,{recursive:true});
function project(){return fs.mkdtempSync(path.join(base,'case-'));}
test('prepared writes and directory/index changes stay virtual until committed',()=>{
  const dir=project(),file=path.join(dir,'index.txt');fs.writeFileSync(file,'first\n');const tx=new ProjectSyncTransaction(dir);
  tx.appendUnique(file,'second');tx.appendUnique(file,'second');tx.io.mkdirSync(path.join(dir,'sprite'));tx.io.copyFileSync(file,path.join(dir,'sprite/image.bin'));tx.write(path.join(dir,'sprite/sprite.txt'),'frames=1');
  assert.equal(fs.readFileSync(file,'utf8'),'first\n');assert.ok(!fs.existsSync(path.join(dir,'sprite')));assert.equal(tx.io.readFileSync(file,'utf8'),'first\nsecond\n');assert.equal(tx.io.readdirSync(path.join(dir,'sprite')).length,2);
  tx.commit();assert.equal(fs.readFileSync(file,'utf8'),'first\nsecond\n');assert.equal(fs.readFileSync(path.join(dir,'sprite/image.bin'),'utf8'),'first\nsecond\n');assert.ok(!fs.existsSync(tx.folder));
});
test('unchanged output preserves timestamps and creates no journal',()=>{
  const dir=project(),file=path.join(dir,'state.json');fs.writeFileSync(file,'same');fs.utimesSync(file,new Date(100000),new Date(100000));const before=fs.statSync(file).mtimeMs;
  const tx=new ProjectSyncTransaction(dir);tx.write(file,'same');tx.commit();assert.equal(fs.statSync(file).mtimeMs,before);assert.ok(!fs.existsSync(tx.folder));
});
test('resource indexes without a trailing newline keep separate entries',()=>{
  const dir=project(),file=path.join(dir,'index.yyd');fs.writeFileSync(file,'existing');const tx=new ProjectSyncTransaction(dir);
  tx.appendUnique(file,'generated');tx.appendUnique(file,'generated');tx.commit();assert.equal(fs.readFileSync(file,'utf8'),'existing\ngenerated\n');
});
test('a mid-commit error restores overwritten files and removes newly created resources',()=>{
  const dir=project(),file=path.join(dir,'state.json');fs.writeFileSync(file,'old');const tx=new ProjectSyncTransaction(dir,{applied:index=>{if(index===1)throw Error('injected write failure');}});
  tx.write(file,'new');tx.write(path.join(dir,'new/resources.txt'),'resource');tx.write(path.join(dir,'last.txt'),'last');assert.throws(()=>tx.commit(),/injected write failure/);
  assert.equal(fs.readFileSync(file,'utf8'),'old');assert.ok(!fs.existsSync(path.join(dir,'new')));assert.ok(!fs.existsSync(path.join(dir,'last.txt')));assert.ok(!fs.existsSync(tx.folder));
});
test('an interrupted process is recovered before the next load',()=>{
  const dir=project(),file=path.join(dir,'state.json');fs.writeFileSync(file,'old');
  const source=`import{ProjectSyncTransaction}from ${JSON.stringify(pathToFileURL(path.join(root,'studio/project-sync.mjs')).href)};const tx=new ProjectSyncTransaction(${JSON.stringify(dir)},{applied:()=>process.exit(42)});tx.write(${JSON.stringify(file)},'new');tx.write(${JSON.stringify(path.join(dir,'later.txt'))},'later');tx.commit();`;
  const child=spawnSync(process.execPath,['--input-type=module','-e',source],{windowsHide:true,encoding:'utf8'});assert.equal(child.status,42);assert.equal(fs.readFileSync(file,'utf8'),'new');assert.equal(recoverProjectSync(dir),true);assert.equal(fs.readFileSync(file,'utf8'),'old');assert.ok(!fs.existsSync(path.join(dir,'later.txt')));
});
test('a concurrent external file edit is detected before committing',()=>{
  const dir=project(),file=path.join(dir,'script.gml');fs.writeFileSync(file,'old');const tx=new ProjectSyncTransaction(dir);tx.write(file,'generated');fs.writeFileSync(file,'manual');assert.throws(()=>tx.commit(),/其他程序修改/);assert.equal(fs.readFileSync(file,'utf8'),'manual');
});
