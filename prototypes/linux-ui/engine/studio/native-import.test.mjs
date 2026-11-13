import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import {test,after} from 'node:test';
import {fileURLToPath} from 'node:url';
import {analyzeNativeProject,refreshNativeState} from './native-import.mjs';
import {nativeEventBlock,replaceNativeEventBlock} from './native-blocks.mjs';

const root=fileURLToPath(new URL('..',import.meta.url));
const parent=path.join(root,'build');fs.mkdirSync(parent,{recursive:true});
const fixture=fs.mkdtempSync(path.join(parent,'native-import-test-'));
function write(file,content){const target=path.join(fixture,file);fs.mkdirSync(path.dirname(target),{recursive:true});fs.writeFileSync(target,content);}
after(()=>{assert.equal(path.dirname(fixture),parent);assert(path.basename(fixture).startsWith('native-import-test-'));fs.rmSync(fixture,{recursive:true,force:true});});
const action=`/*"/*'/**//* YYD ACTION\nlib_id=1\naction_id=603\napplies_to=self\n*/\n`;
const controller=`#define Create_0\n${action}clock=742;\nstart_pos=clock;\n#define Step_0\n${action}clock+=1;\nswitch(clock){\ncase 10:\n    initial=irandom(10);\n    switch(kind){\n    case 9: text="case 30: { }";break;\n    default: initial+=2;\n    }\n    // case 30: fake comment\n    break;\ncase 30:\n    x=initial;\n    break;\ncase 500:\n    room_goto(rEnd);\n    break;\n}\n#define Draw_0\n${action}draw_self();\n`;
write('game.gm82','gm82_version=5\n');write('objects/objAd.gml',controller);write('objects/world.gml',`#define Other_4\n${action}room_speed=50;\n`);
for(const room of ['rAvoidance','rEnd']){write(`rooms/${room}/room.txt`,'width=800\nheight=608\nroomspeed=30\n');write(`rooms/${room}/instances.txt`,room==='rAvoidance'?'objAd,0,0,1234,0,1,1,4294967295,0,0\n':'');write(`rooms/${room}/code.gml`,'');}
write('rooms/index.yyd','rAvoidance\nrEnd\n');
const entry=path.join(fixture,'game.gm82');

test('50 FPS and terminal event determine native duration',()=>{
  const report=analyzeNativeProject(entry);assert.equal(report.fps,50);assert.equal(report.duration,500);assert.deepEqual(report.events.map(e=>e.frame),[10,30,500]);assert(report.rooms.every(r=>r.fps===50));
});
test('case editor preserves nested switches, strings, sibling cases and CRLF',()=>{
  const report=analyzeNativeProject(entry),source=controller.replace(/\n/g,'\r\n');
  for(const event of report.events){const block=nativeEventBlock(source,event);assert.equal(replaceNativeEventBlock(source,event,block.content,block.hash),source);assert(!block.content.includes('#define'));}
  const event=report.events[0],block=nativeEventBlock(source,event);assert(block.content.includes('case 9:'));assert(!block.content.includes('\r\ncase 30:'));
  const content=block.content.replace('initial=irandom(10)','initial=irandom(50)'),result=replaceNativeEventBlock(source,event,content,block.hash);
  assert.equal(result.slice(0,block.start),source.slice(0,block.start));assert(result.endsWith(source.slice(block.end)));assert(result.includes('case 30:\r\n    x=initial;'));assert.throws(()=>replaceNativeEventBlock(source+' ',event,content,block.hash),/已改变/);
});
test('timeline block excludes moment headers and YYD metadata',()=>{
  const source=`#define 18\n${action}random_value=irandom(30);\n#define 36\n${action}x=random_value;\n`,event={kind:'timeline',line:1};
  const block=nativeEventBlock(source,event);assert.equal(block.content,'random_value=irandom(30);\n');assert.equal(replaceNativeEventBlock(source,event,block.content,block.hash),source);assert.throws(()=>replaceNativeEventBlock(source,event,'#define 12\nx=1;',block.hash),/事件头/);
});

test('LF event bodies displayed as Windows CRLF save back without changing sibling code',()=>{
  const event=analyzeNativeProject(entry).events[0],block=nativeEventBlock(controller,event),display=block.content.replace(/\n/g,'\r\n');
  assert(block.content.split('\n').length>5);assert.equal(replaceNativeEventBlock(controller,event,display,block.hash),controller);
  const result=replaceNativeEventBlock(controller,event,display.replace('irandom(10)','irandom(20)'),block.hash);
  assert(!result.includes('\r'));assert(result.endsWith(controller.slice(block.end)));assert(result.includes('initial=irandom(20)'));
});
test('legacy import migration backs up old settings and keeps all event frames',()=>{
  const report=analyzeNativeProject(entry),state={fps:30,duration:560,nativeProject:{...report,version:1,fps:30,duration:560},tracks:[{id:'gm-native',kind:'native',start:0,end:560,keyframes:[]}]};write('rufmaker.json',JSON.stringify(state));
  refreshNativeState(entry,state);assert.equal(state.fps,50);assert.equal(state.duration,500);assert.equal(state.nativeProject.version,2);assert.equal(state.tracks[0].end,500);assert.deepEqual(state.nativeProject.events.map(e=>e.frame),[10,30,500]);
  assert(fs.readFileSync(path.join(fixture,'cache/native-migration-v2/rooms/rAvoidance/room.txt'),'utf8').includes('roomspeed=30'));
  assert(fs.readFileSync(path.join(fixture,'rooms/rEnd/room.txt'),'utf8').includes('roomspeed=50'));
  assert.equal(fs.readFileSync(path.join(fixture,'objects/objAd.gml'),'utf8'),controller);const before=JSON.stringify(state);refreshNativeState(entry,state);assert.equal(JSON.stringify(state),before);
});
