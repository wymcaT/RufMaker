import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import assert from 'node:assert/strict';
import {test} from 'node:test';
import {rewriteNativeRuntime,makeNativeHarness,commandText,nativePreviewStamp} from './native-preview.mjs';

const state={fps:50,nativeProject:{hasFmod:true}};
function stampFixture(t){
  const parent=fileURLToPath(new URL('../build/',import.meta.url));fs.mkdirSync(parent,{recursive:true});
  const folder=fs.mkdtempSync(path.join(parent,'native-stamp-test-'));
  t.after(()=>fs.rmSync(folder,{recursive:true,force:true}));
  const write=(name,text,time)=>{const file=path.join(folder,name);fs.mkdirSync(path.dirname(file),{recursive:true});fs.writeFileSync(file,text);if(time!==undefined)fs.utimesSync(file,new Date(time),new Date(time));return file;};
  return {folder,write};
}
test('native preview cache invalidates when a non-newest source is removed or renamed',t=>{
  const {folder,write}=stampFixture(t);write('newest.gml','x=3;',200000);const older=write('older.gml','x=1;',100000);
  const before=nativePreviewStamp(folder);fs.renameSync(older,path.join(folder,'renamed.gml'));
  const renamed=nativePreviewStamp(folder);assert.notEqual(renamed,before);
  fs.unlinkSync(path.join(folder,'renamed.gml'));assert.notEqual(nativePreviewStamp(folder),renamed);
});
test('native preview cache detects same-size source edits with preserved timestamps',t=>{
  const {folder,write}=stampFixture(t);write('newest.gml','x=3;',200000);write('older.gml','x=1;',100000);
  const before=nativePreviewStamp(folder);write('older.gml','x=2;',100000);assert.notEqual(nativePreviewStamp(folder),before);
});
test('generated build and cache output does not invalidate native preview sources',t=>{
  const {folder,write}=stampFixture(t);write('source.gml','x=1;',100000);const before=nativePreviewStamp(folder);
  write('build/output.log','generated');write('cache/film.rufvideo','generated');
  assert.equal(nativePreviewStamp(folder),before);
});
test('generated preview uses the host rewind protocol without unsupported GM game_restart',()=>{
  const code=makeNativeHarness({...state,room:'rAvoidance',duration:6700});
  assert(!/\bgame_restart\s*\(/.test(code));assert(code.includes('rf_native_wait_rewind()'));
  assert(commandText(40).includes('seek=1\r\n'));
});
test('preserve native debug speed write targets from the reported world Step error',()=>{
  const source='room_speed += 50;\nif(room_speed>10)room_speed -= 10;\nroom_speed = 50;\n';
  assert.equal(rewriteNativeRuntime(source,state),'room_speed += 50;\nif(50>10)room_speed -= 10;\nroom_speed = 50;\n');
});
test('all assignment and increment targets remain variables while RHS reads use 50 FPS',()=>{
  for(const variable of ['room_speed','ffps']){
    for(const op of ['=','+=','-=','*=','/=','%=','&=','|=','^=','<<=','>>=']){
      const source=`${variable} ${op} room_speed + ffps;`;
      assert.equal(rewriteNativeRuntime(source,state),`${variable} ${op} 50 + 50;`);
    }
    for(const source of [`++${variable};`,`--${variable};`,`${variable}++;`,`${variable}--;`,`++ /* comment */ ${variable};`])assert.equal(rewriteNativeRuntime(source,state),source);
  }
});
test('preserve strings, comments and qualified fields; adapt only native timing reads and function calls',()=>{
  const source=`// room_speed += 50;\ntext='room_speed += 50; ffps';\nobj . room_speed += 2;global.ffps=1;\nm_time+=1000/room_speed;\nm_fps=round(ffps);\nif(room_speed==50) x=1;\nFMODInstanceSetVolume(handle,1);\nrandomize();\n`;
  assert.equal(rewriteNativeRuntime(source,state),`// room_speed += 50;\ntext='room_speed += 50; ffps';\nobj . room_speed += 2;global.ffps=1;\nm_time+=1000/50;\nm_fps=round(50);\nif(50==50) x=1;\nrf_native_volume(handle,1);\nrandom_set_seed(1234567);\n`);
});
test('real Verse3 world object keeps every original room_speed write target',t=>{
  const file=new URL('../non-ruf projects/Verse3/objects/world.gml',import.meta.url);if(!fs.existsSync(file)){t.skip('local reference project unavailable');return;}
  const source=fs.readFileSync(file,'utf8'),result=rewriteNativeRuntime(source,state);
  const writes=source.match(/\broom_speed\s*(?:[+\-*/]?=)\s*\d+\s*;/g);
  assert(writes.length>=3);for(const statement of writes)assert(result.includes(statement),statement);
  assert(!/\b50\s*(?:[+\-*/]=|\+\+|--)/.test(result));
});
