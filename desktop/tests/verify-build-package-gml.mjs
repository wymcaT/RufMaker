// Real GM compilation and package replacement; all files stay in build/.
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {spawn} from 'node:child_process';
import {createProject} from '../../studio/project-files.mjs';
import {editGmAsset} from '../../studio/gm-resources.mjs';

const root=path.resolve(import.meta.dirname,'../..');
const parent=path.join(root,'build/build-package-gml');
fs.mkdirSync(parent,{recursive:true});
const made=createProject(root,{name:'Build package '+Date.now(),parent,preset:'floor',width:800,height:608,fps:50});
process.env.RUFMAKER_ROOT=root;process.env.RUFMAKER_PROJECT=made.path;
const p=await import('../../studio/project.mjs');
const native=await import('../../studio/native.mjs');
const action=`/*"/*'/**//* YYD ACTION\nlib_id=1\naction_id=603\napplies_to=self\n*/\n`;
const script=path.join(p.project,'scripts/scrCacheProbe.gml');
fs.writeFileSync(script,'return 101;\n');
for(const [file,line] of [['index.yyd','scrCacheProbe'],['tree.yyd','|scrCacheProbe']])fs.appendFileSync(path.join(p.project,'scripts',file),'\n'+line+'\n');
editGmAsset(p.project,{kind:'objects',name:'objCacheProbe',operation:'create'});
fs.writeFileSync(path.join(p.project,'objects/objCacheProbe.gml'),
  '#define Create_0\n'+action+'window_set_visible(false);rf_probe_age=0;\n'+
  '#define Step_0\n'+action+"rf_probe_age+=1;if rf_probe_age=4 {var rf_file;rf_file=file_text_open_write(working_directory+'\\cache-result.txt');file_text_write_real(rf_file,scrCacheProbe());file_text_close(rf_file);game_end();}\n");
const state=p.readState();state.duration=100;state.directStart=true;
state.tracks=[{id:'probe',name:'Cache probe',kind:'native-instance',nativeObject:'objCacheProbe',start:0,end:99,x:100,y:100,keyframes:[]}];
p.syncToGame(state);
const settings=path.join(p.project,'settings/settings.txt');
fs.writeFileSync(settings,fs.readFileSync(settings,'utf8').replace('show_error_messages=1','show_error_messages=0').replace('always_abort=0','always_abort=1'));
fs.writeFileSync(path.join(p.project,'Data/obsolete.dat'),'old managed data');
fs.writeFileSync(path.join(p.project,'obsolete.ini'),'old configuration');
const log=path.join(path.dirname(p.project),'runtime.log');
async function run(pack,expected){
  const result=path.join(pack.dir,'cache-result.txt');if(fs.existsSync(result))fs.unlinkSync(result);
  await new Promise((resolve,reject)=>{
    const child=spawn(pack.exe,[],{cwd:pack.dir,windowsHide:true,stdio:['ignore','pipe','pipe']});
    for(const stream of [child.stdout,child.stderr])stream.on('data',data=>fs.appendFileSync(log,data));
    const timer=setTimeout(()=>{child.kill();reject(Error('Runtime timed out: '+pack.dir));},30000);
    child.once('error',error=>{clearTimeout(timer);reject(error);});
    child.once('close',code=>{clearTimeout(timer);code===0?resolve():reject(Error('Runtime exit '+code+': '+pack.dir));});
  });
  const errors=path.join(pack.dir,'game_errors.log');
  if(fs.existsSync(errors)&&fs.readFileSync(errors,'utf8').trim())throw Error(fs.readFileSync(errors,'utf8'));
  assert.equal(Number(fs.readFileSync(result,'utf8').trim()),expected,'The compiled game must execute the current script');
}
console.log('Isolated project: '+p.project);
const first=await native.ensureBuild();assert.equal(first.cached,false);assert.equal(first.fresh,true);
assert.equal((await native.ensureBuild()).cached,true);
const initial=native.packageGame(false);await run(initial,101);
fs.writeFileSync(path.join(initial.dir,'Data/progress.sav'),'user progress');
const same=native.packageGame(false);assert.equal(same.previousDir,null,'Identical repackaging must not leave duplicate backups');
assert.equal(fs.readFileSync(path.join(same.dir,'Data/progress.sav'),'utf8'),'user progress');
const before=fs.statSync(script);fs.writeFileSync(script,'return 102;\n');fs.utimesSync(script,before.atime,before.mtime);
assert.equal(native.exportStatus().fresh,false,'Same-size source edits with restored timestamps must invalidate the build');
fs.unlinkSync(path.join(p.project,'Data/obsolete.dat'));fs.unlinkSync(path.join(p.project,'obsolete.ini'));
assert.throws(()=>native.packageGame(false),/重新编译/);
const rebuilt=await native.ensureBuild();assert.equal(rebuilt.cached,false);assert.equal(rebuilt.fresh,true);
const updated=native.packageGame(false);assert(updated.previousDir);
assert(!fs.existsSync(path.join(updated.dir,'Data/obsolete.dat')));assert(!fs.existsSync(path.join(updated.dir,'obsolete.ini')));
assert.equal(fs.readFileSync(path.join(updated.dir,'Data/progress.sav'),'utf8'),'user progress');
assert.equal(fs.readFileSync(path.join(updated.previousDir,'Data/obsolete.dat'),'utf8'),'old managed data');
await run(updated,102);
const preview=native.packageGame(true,0);await run(preview,102);
fs.writeFileSync(path.join(preview.dir,'Data/progress.sav'),'preview progress');
for(const name of ['rufmaker_preview_status.ini','rufmaker_plain_checkpoint.sav','rufmaker_plain_restore','rufmaker_plain_restore_count.ini'])fs.writeFileSync(path.join(preview.dir,'Data',name),'stale control');
const repeated=native.packageGame(true,20);
assert(fs.readFileSync(path.join(repeated.dir,'Data/rufmaker_preview.ini'),'utf8').includes('frame=20'));
assert.equal(fs.readFileSync(path.join(repeated.dir,'Data/progress.sav'),'utf8'),'preview progress');
for(const name of ['rufmaker_preview_status.ini','rufmaker_plain_checkpoint.sav','rufmaker_plain_restore','rufmaker_plain_restore_count.ini'])assert(!fs.existsSync(path.join(repeated.dir,'Data',name)),name);
assert.equal((await native.ensureBuild()).cached,true);
console.log('PASS: actual GM builds and runs updated GML; cache reuse, preserved-mtime invalidation, replacement exports, retained saves/backups and clean preview controls. '+p.project);
