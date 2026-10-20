import fs from 'node:fs';
import path from 'node:path';
import {EventEmitter} from 'node:events';
import {fileURLToPath} from 'node:url';
import assert from 'node:assert/strict';
import {test,after} from 'node:test';
import {NativePlaySession} from './native-play-session.mjs';
import {commandText,patchTestPlayerEvents,makeNativeHarness} from './native-preview.mjs';
import {checkpointScripts} from './native-checkpoint.mjs';

const parent=fileURLToPath(new URL('../build',import.meta.url));fs.mkdirSync(parent,{recursive:true});const fixture=fs.mkdtempSync(path.join(parent,'native-test-session-'));
after(()=>{assert.equal(path.dirname(fixture),parent);assert(path.basename(fixture).startsWith('native-test-session-'));fs.rmSync(fixture,{recursive:true,force:true});});

test('R retry keeps the original F5 process alive and restores its checkpoint in-game',async()=>{
  const target=2012,result={exe:'fixture.exe',dir:fixture,commandFile:path.join(fixture,'command.ini'),statusFile:path.join(fixture,'status.ini')};
  const original=commandText(target,true,true,{x:271.5,y:450.25});fs.writeFileSync(result.commandFile,original);const children=[],phases=[];
  const launch=()=>{assert.equal(fs.readFileSync(result.commandFile,'utf8'),original);const child=new EventEmitter();child.exitCode=null;child.signalCode=null;child.unref=()=>{};child.kill=()=>{child.exitCode=0;queueMicrotask(()=>child.emit('close'));return true;};children.push(child);
    fs.writeFileSync(result.statusFile,`frame=${target}\nready=1\nrestart=0\nroom=test\n`);queueMicrotask(()=>child.emit('spawn'));return child;
  };
  const session=new NativePlaySession({result,target,launch,pollMs:100000,onProgress:p=>phases.push(p.phase)});
  try{await session.start();assert.equal(children.length,1);fs.writeFileSync(result.statusFile,'frame=2012\nready=1\nrestart=1\nroom=test\n');await session.poll();assert.equal(children.length,1);assert.equal(children[0].exitCode,null);assert(!phases.includes('restart'));assert.equal(phases.at(-1),'ready');await session.poll();assert.equal(children.length,1);}
  finally{await session.stop();}fs.writeFileSync(result.statusFile,'restart=1');await session.poll();assert.equal(children.length,1);
  const code=makeNativeHarness({fps:50,room:'rTest',duration:6000,nativeProject:{}},{playable:true});assert(!/\bgame_restart\s*\(/.test(code));assert(code.includes("keyboard_check_pressed(ord('R'))"));assert(code.includes('rf_native_place();rf_native_release();'));
  assert(code.includes('rf_native_checkpoint();'));assert(code.includes('rf_native_restore();'));
  const scripts=checkpointScripts({tracks:[],nativeProject:{hasFmod:true}});assert(scripts.rf_native_checkpoint.includes('game_save('));assert(scripts.rf_native_retry.includes('game_load('));assert(!/game_end|game_restart/.test(scripts.rf_native_retry));assert(scripts.rf_native_restore.includes('FMODInstanceSetPosition'));assert(!scripts.rf_native_restore.includes('objRufController'));
});
test('playable clone replaces R save-loading events and guards each native player code action during warmup',()=>{
  const metadata=`/*"/*'/**//* YYD ACTION\nlib_id=1\naction_id=603\napplies_to=self\n*/\n`;
  const source=`#define Step_0\n${metadata}x+=2;\n${metadata}gravity=.4;\n#define KeyPress_82\n${metadata}loadGame();\n#define Draw_0\n${metadata}draw_self();\n`;
  const patched=patchTestPlayerEvents(source,true);assert(!patched.includes('loadGame();'));assert(patched.includes('rf_native_retry();'));assert.equal((patched.match(/!objRufNativePreview.rf_presented/g)||[]).length,2);assert(patched.includes('x+=2;'));assert(patched.includes('draw_self();'));
});
