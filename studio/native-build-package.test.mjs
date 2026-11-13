import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import assert from 'node:assert/strict';
import {test} from 'node:test';
import {buildFingerprint,buildIsFresh,recordBuild} from './native-build-cache.mjs';
import {replacePackage} from './native-package.mjs';

const base=fileURLToPath(new URL('../build/build-package-tests/',import.meta.url));
fs.mkdirSync(base,{recursive:true});
function fixture(t){
  const dir=fs.mkdtempSync(path.join(base,'case-'));t.after(()=>fs.rmSync(dir,{recursive:true,force:true}));
  const write=(name,content='same',time=100000)=>{const file=path.join(dir,name);fs.mkdirSync(path.dirname(file),{recursive:true});fs.writeFileSync(file,content);fs.utimesSync(file,new Date(time),new Date(time));return file;};
  write('game.gm82','gm82_version=8.2');write('rufmaker.json',JSON.stringify({version:2,fps:50,duration:100,room:'rTest',tracks:[]}));
  return {dir,write};
}
test('build fingerprint detects deletes, renames, additions and restored text/binary edits',t=>{
  const {dir,write}=fixture(t);write('objects/latest.gml','latest',200000);let file=write('objects/old.gml','x=1;');
  assert.notEqual(buildFingerprint(dir,'',path.join(dir,'a.gm82')),buildFingerprint(dir,'',path.join(dir,'b.gm82')));
  let stamp=buildFingerprint(dir);fs.renameSync(file,file+'.renamed');assert.notEqual(buildFingerprint(dir),stamp);
  stamp=buildFingerprint(dir);fs.unlinkSync(file+'.renamed');assert.notEqual(buildFingerprint(dir),stamp);
  stamp=buildFingerprint(dir);write('objects/old.gml','x=1;');assert.notEqual(buildFingerprint(dir),stamp);
  stamp=buildFingerprint(dir);write('objects/old.gml','x=2;');assert.notEqual(buildFingerprint(dir),stamp);
  write('Data/song.ogg',Buffer.from([0,1,2,3]));stamp=buildFingerprint(dir);write('Data/song.ogg',Buffer.from([0,1,3,2]));assert.notEqual(buildFingerprint(dir),stamp);
  stamp=buildFingerprint(dir);write('options.ini','setting');assert.notEqual(buildFingerprint(dir),stamp);
});
test('build output and cache do not invalidate sources; compiler changes do',t=>{
  const {dir,write}=fixture(t);const compiler=write('build/compiler.exe','compiler');const stamp=buildFingerprint(dir,compiler);
  write('build/game.exe');write('cache/movie.bin');assert.equal(buildFingerprint(dir,compiler),stamp);
  write('build/compiler.exe','changed!');assert.notEqual(buildFingerprint(dir,compiler),stamp);
});
test('old, corrupt or mismatched build manifests cannot reuse an executable',t=>{
  const {dir,write}=fixture(t),exe=write('build/game.exe','MZcompiled'),manifest=exe+'.build.json',stamp=buildFingerprint(dir);
  assert.equal(buildIsFresh(exe,manifest,stamp),false);recordBuild(exe,manifest,stamp);assert.equal(buildIsFresh(exe,manifest,stamp),true);
  write('objects/code.gml');assert.equal(buildIsFresh(exe,manifest,buildFingerprint(dir)),false);
  write('build/game.exe','MZtampered');assert.equal(buildIsFresh(exe,manifest,stamp),false);
  for(const invalid of ['{broken','null','[]','0']){fs.writeFileSync(manifest,invalid);assert.equal(buildIsFresh(exe,manifest,stamp),false);}
});
test('fresh package excludes removed assets/config and keeps previous saves untouched',t=>{
  const {dir,write}=fixture(t);write('release/Data/deleted.ogg','old');write('release/old.ini','old');write('release/save1','user save');
  const destination=path.join(dir,'release');const result=replacePackage(destination,stage=>{fs.mkdirSync(path.join(stage,'Data'));fs.writeFileSync(path.join(stage,'new.ini'),'new');});
  assert(!fs.existsSync(path.join(destination,'Data/deleted.ogg')));assert(!fs.existsSync(path.join(destination,'old.ini')));
  assert.equal(fs.readFileSync(path.join(result.previousDir,'save1'),'utf8'),'user save');assert.equal(fs.readFileSync(path.join(result.previousDir,'Data/deleted.ogg'),'utf8'),'old');
});
test('repeated packages preserve unmanaged saves without retaining duplicate media',t=>{
  const {dir}=fixture(t),destination=path.join(dir,'release');
  const populate=stage=>{fs.mkdirSync(path.join(stage,'Data'));fs.writeFileSync(path.join(stage,'Data/song.ogg'),'large song');fs.writeFileSync(path.join(stage,'game.exe'),'compiled');};
  replacePackage(destination,populate);fs.writeFileSync(path.join(destination,'save1'),'progress');
  for(let repeat=0;repeat<3;repeat++){
    const result=replacePackage(destination,populate);assert.equal(result.previousDir,null);
    assert.equal(fs.readFileSync(path.join(destination,'save1'),'utf8'),'progress');
  }
  const changed=replacePackage(destination,stage=>{fs.writeFileSync(path.join(stage,'game.exe'),'updated');});
  assert.equal(fs.readFileSync(path.join(destination,'save1'),'utf8'),'progress');
  assert(!fs.existsSync(path.join(destination,'Data/song.ogg')));
  assert.equal(fs.readFileSync(path.join(changed.previousDir,'Data/song.ogg'),'utf8'),'large song');
  assert(!fs.existsSync(path.join(changed.previousDir,'save1')));
});
test('a failed rollback reports the retained recoverable package path',t=>{
  const {dir,write}=fixture(t);write('release/game.exe','old');let count=0;
  const io={...fs,renameSync:(a,b)=>{if(++count>=2)throw Error('locked');return fs.renameSync(a,b);}};
  assert.throws(()=>replacePackage(path.join(dir,'release'),()=>{},io),/旧导出仍保存在 .*previous-.*自动恢复失败/);
  const backup=fs.readdirSync(dir).find(name=>name.includes('.previous-'));assert.equal(fs.readFileSync(path.join(dir,backup,'game.exe'),'utf8'),'old');
});
test('copy failure leaves previous package intact',t=>{
  const {dir,write}=fixture(t);write('release/game.exe','old');
  assert.throws(()=>replacePackage(path.join(dir,'release'),stage=>{fs.writeFileSync(path.join(stage,'partial'),'partial');throw Error('copy failed');}),/copy failed/);
  assert.equal(fs.readFileSync(path.join(dir,'release/game.exe'),'utf8'),'old');assert(!fs.readdirSync(dir).some(n=>n.includes('.staging-')));
});
test('locked destination and failed publication preserve the old package',t=>{
  const {dir,write}=fixture(t);write('release/game.exe','old');const destination=path.join(dir,'release');
  for(const failAt of [1,2]){
    let count=0;const io={...fs,renameSync:(a,b)=>{if(++count===failAt)throw Error('locked');return fs.renameSync(a,b);}};
    assert.throws(()=>replacePackage(destination,stage=>fs.writeFileSync(path.join(stage,'game.exe'),'new'),io),/locked/);
    assert.equal(fs.readFileSync(path.join(destination,'game.exe'),'utf8'),'old');assert(!fs.readdirSync(dir).some(n=>n.includes('.staging-')));
  }
});
test('packageGame uses recorded fingerprints and stages ordinary/imported extras and preview controls',async t=>{
  const {dir,write}=fixture(t);const prior={root:process.env.RUFMAKER_ROOT,project:process.env.RUFMAKER_PROJECT};
  process.env.RUFMAKER_ROOT=dir;process.env.RUFMAKER_PROJECT=path.join(dir,'game.gm82');
  t.after(()=>{for(const [key,value] of [['RUFMAKER_ROOT',prior.root],['RUFMAKER_PROJECT',prior.project]]){if(value===undefined)delete process.env[key];else process.env[key]=value;}});
  // Environment selected before the first project import: no real user project.
  const native=await import('./native.mjs'),p=await import('./project.mjs');
  write('build/rufmaker-game.exe','MZ'+'.'.repeat(2048));write('Data/old.dat','old');write('old.ini','old');
  const mark=()=>recordBuild(p.gameExe,p.gameExe+'.build.json',buildFingerprint(dir,p.gameMaker,p.projectFile));
  assert.equal(native.exportStatus().fresh,false);mark();assert.equal(native.exportStatus().fresh,true);
  assert.equal((await native.ensureBuild()).cached,true);
  assert.equal(native.exportStatus(false).verified,false);
  assert.equal(native.exportStatus(false).fresh,false);
  const first=native.packageGame(false);assert(fs.existsSync(path.join(first.dir,'Data/old.dat')));
  fs.unlinkSync(path.join(dir,'Data/old.dat'));fs.unlinkSync(path.join(dir,'old.ini'));
  assert.equal(native.exportStatus().fresh,false);assert.throws(()=>native.packageGame(false),/重新编译/);mark();
  const next=native.packageGame(false);assert(!fs.existsSync(path.join(next.dir,'Data/old.dat')));assert(!fs.existsSync(path.join(next.dir,'old.ini')));assert(fs.existsSync(path.join(next.previousDir,'Data/old.dat')));
  const preview=native.packageGame(true,999,true);assert.equal(fs.readFileSync(path.join(preview.dir,'Data/rufmaker_preview.ini'),'utf8'),'[preview]\r\nframe=100\r\ngod=1\r\n');
  const controls=['rufmaker_preview_status.ini','rufmaker_plain_checkpoint.sav','rufmaker_plain_restore','rufmaker_plain_restore_count.ini'];
  for(const name of controls)fs.writeFileSync(path.join(preview.dir,'Data',name),'stale runtime');
  fs.writeFileSync(path.join(preview.dir,'Data','my-save.sav'),'user progress');
  const replay=native.packageGame(true,42,false);
  for(const name of controls)assert(!fs.existsSync(path.join(replay.dir,'Data',name)),name);
  assert.equal(fs.readFileSync(path.join(replay.dir,'Data/my-save.sav'),'utf8'),'user progress');
  assert(fs.readFileSync(path.join(replay.dir,'Data/rufmaker_preview.ini'),'utf8').includes('frame=42'));
  write('Data/rufmaker_preview.ini','stale');write('Data/rufmaker_plain_checkpoint.sav','stale');mark();
  const release=native.packageGame(false);assert(!fs.existsSync(path.join(release.dir,'Data/rufmaker_preview.ini')));assert(!fs.existsSync(path.join(release.dir,'Data/rufmaker_plain_checkpoint.sav')));
  const state=JSON.parse(fs.readFileSync(p.makerFile));state.nativeProject={imported:true};write('rufmaker.json',JSON.stringify(state));write('texture.png','texture');mark();
  assert(fs.existsSync(path.join(native.packageGame(false).dir,'texture.png')));
  fs.unlinkSync(path.join(dir,'texture.png'));mark();assert(!fs.existsSync(path.join(native.packageGame(false).dir,'texture.png')));
  if(process.platform!=='win32'){
    // A protocol stub, not GameMaker: verifies cache publication and failures.
    fs.mkdirSync(path.dirname(p.gameMaker),{recursive:true});
    fs.writeFileSync(p.gameMaker,`#!${process.execPath}
const fs=require('node:fs'),path=require('node:path');
const root=path.dirname(process.argv[2]),mode=fs.readFileSync(path.join(root,'build/mode'),'utf8');
fs.appendFileSync(path.join(root,'build/count'),'1');
if(mode==='fail')process.exit(7);
if(mode==='mutate')fs.writeFileSync(path.join(root,'objects/during.gml'),'changed during compile');
const output=Buffer.alloc(2048);output.write(mode==='invalid'?'XX':'MZ');
setTimeout(()=>fs.writeFileSync(process.argv[4],output),30);
`,{mode:0o755});
    write('build/mode','ok');
    const builds=await Promise.all([native.ensureBuild(true),native.ensureBuild(true)]);
    assert(builds.every(result=>result.fresh&&!result.cached));assert.equal(fs.readFileSync(path.join(dir,'build/count'),'utf8'),'1');
    assert.equal((await native.ensureBuild()).cached,true);
    write('objects/changed.gml','new source');
    write('build/mode','fail');await assert.rejects(native.ensureBuild(),/编译失败/);assert.equal(native.exportStatus().fresh,false);
    write('build/mode','invalid');await assert.rejects(native.ensureBuild(),/不是 Windows 可执行文件/);assert.equal(native.exportStatus().fresh,false);
    write('build/mode','mutate');await assert.rejects(native.ensureBuild(),/编译期间工程已修改/);assert.equal(native.exportStatus().fresh,false);
    write('build/mode','ok');assert.equal((await native.ensureBuild()).fresh,true);
  }
});
