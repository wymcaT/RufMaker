import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import {test,after} from 'node:test';
import {fileURLToPath} from 'node:url';
import {waitForNativeReplay} from './native-replay.mjs';

const parent=fileURLToPath(new URL('../build/',import.meta.url));fs.mkdirSync(parent,{recursive:true});
const fixture=fs.mkdtempSync(path.join(parent,'native-replay-test-'));
after(()=>{assert.equal(path.dirname(fixture),parent.replace(/[\\/]$/,''));assert(path.basename(fixture).startsWith('native-replay-test-'));fs.rmSync(fixture,{recursive:true,force:true});});
function setup(name,target=6278) {
  const statusFile=path.join(fixture,name+'.ini');let time=0;
  const write=(frame,ready=0)=>fs.writeFileSync(statusFile,`[native]\r\nframe=${frame}\r\nready=${ready}\r\nroom=room_wymcaTad\r\n`);
  return {statusFile,target,dir:fixture,child:{exitCode:null,signalCode:null},now:()=>time,advance:ms=>{time+=ms;},write};
}

test('reported late seek can take over two minutes while native frames keep advancing',async()=>{
  const run=setup('slow');let frame=0;run.write(frame);const frames=[];
  const result=await waitForNativeReplay({...run,onProgress:p=>frames.push(p.frame),delay:async()=>{
    run.advance(30000);frame=Math.min(run.target,frame+500);run.write(frame,frame===run.target?1:0);
  }});
  assert(run.now()>120000);assert.equal(Number(result.frame),6278);assert.equal(frames.at(-1),6278);assert(frames.includes(2000));
});

test('repeated status writes without frame progress still time out with the last frame',async()=>{
  const run=setup('stalled');run.write(2161);let writes=0;
  await assert.rejects(waitForNativeReplay({...run,delay:async()=>{run.advance(15000);run.write(2161);writes++;}}),/连续 60 秒没有前进（第 2161 \/ 6278 帧）/);
  assert.equal(writes,4);
});

test('partially written status and transient backwards values do not erase highest frame progress',async()=>{
  const run=setup('partial',100);run.write(80);let poll=0;
  const result=await waitForNativeReplay({...run,delay:async()=>{
    run.advance(10000);poll++;
    if(poll===1)fs.writeFileSync(run.statusFile,'[native]\r\nframe=');
    else if(poll===2)run.write(70);
    else run.write(100,1);
  }});
  assert.equal(result.ready,'1');assert.equal(result.frame,'100');
});

test('cancel a progressing replay and detect an exited native process',async()=>{
  const run=setup('cancelled'),cancel=new AbortController();run.write(0);
  await assert.rejects(waitForNativeReplay({...run,signal:cancel.signal,delay:async()=>{cancel.abort();}}),error=>error.name==='AbortError');
  run.child.exitCode=1;
  await assert.rejects(waitForNativeReplay(run),/重放阶段退出/);
});
