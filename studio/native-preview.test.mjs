import fs from 'node:fs';
import assert from 'node:assert/strict';
import {test} from 'node:test';
import {rewriteNativeRuntime,makeNativeHarness,commandText} from './native-preview.mjs';

const state={fps:50,nativeProject:{hasFmod:true}};
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
