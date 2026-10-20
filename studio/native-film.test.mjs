import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import assert from 'node:assert/strict';
import {test,after} from 'node:test';
import {fixedPlayerReads,pinNativeEvents,makeFilmHarness,writeFilmHarness} from './native-film.mjs';

test('fixed player reads preserve assignments, strings and object qualification',()=>{
  const code=`a=player.x;b=player2.y;player.x=200;player.y+=5;\n--player.x;player.y++;\ntext='player.x';// player.y\nglobal.player.x=1;`;
  assert.equal(fixedPlayerReads(code,['player','player2']),`a=400;b=304;player.x=200;player.y+=5;\n--player.x;player.y++;\ntext='player.x';// player.y\nglobal.player.x=1;`);
});
test('pin player movement events while retaining initialization and native draw logic',()=>{
  const source='#define Create_0\ngravity=0.4;\n#define Step_0\nx+=speed;\n#define Draw_0\ndraw_self();\n';
  const result=pinNativeEvents(source,true);assert(result.includes('gravity=0.4;'));assert(!result.includes('x+=speed;'));assert(result.includes('draw_self();'));assert.equal((result.match(/rf_film_pin\(\)/g)||[]).length,3);
});
test('capture handoff only checks unique ack markers and never opens acknowledgement files',()=>{
  const code=makeFilmHarness({room:'rAvoidance',nativeProject:{duration:100}});assert(code.includes("'.ok'"));assert(!/file_text_open_read|ini_open|game_restart|rf_native_read\(/.test(code));assert(code.includes('screen_save('));
});
const root=fileURLToPath(new URL('..',import.meta.url)),parent=path.join(root,'build');fs.mkdirSync(parent,{recursive:true});const fixture=fs.mkdtempSync(path.join(parent,'film-source-test-'));
const write=(file,text)=>{const f=path.join(fixture,file);fs.mkdirSync(path.dirname(f),{recursive:true});fs.writeFileSync(f,text);};
after(()=>{assert.equal(path.dirname(fixture),parent);assert(path.basename(fixture).startsWith('film-source-test-'));fs.rmSync(fixture,{recursive:true,force:true});});
test('recording clone keeps native event logic and excludes RufMaker live objects',()=>{
  for(const folder of ['objects','scripts']){write(folder+'/index.yyd','');write(folder+'/tree.yyd','');}
  write('objects/player.txt','');write('objects/player.gml','#define Create_0\nwudi=0;\n#define Step_0\nx+=3;\n');
  write('objects/objAd.gml','#define Create_0\nclock=742;\n#define Step_0\nclock+=1;\nif(clock=20) phase=irandom(100);\naim=point_direction(x,y,player.x,player.y);\n');
  write('objects/init.gml','#define Step_0\nroom_goto_next();\n');write('objects/objRufController.gml','#define Create_0\nshow_message(1);\n');
  write('scripts/aim.gml','return point_direction(x,y,player.x,player.y);\n');write('settings/settings.txt','show_error_messages=1\nlog_errors=0\nalways_abort=0\n');write('rooms/index.yyd','rInit\nrAvoidance\n');
  for(const room of ['rInit','rAvoidance'])write('rooms/'+room+'/instances.txt','player,0,0,1,0,1,1,0,0,0\nobjRufController,0,0,2,0,1,1,0,0,0\n');
  const state={room:'rAvoidance',fps:50,duration:100,music:'',nativeProject:{controller:'objAd',clock:'clock',bootObject:'init',hasFmod:false,duration:100}};
  assert.deepEqual(writeFilmHarness(fixture,state),['player']);const read=f=>fs.readFileSync(path.join(fixture,f),'utf8');
  assert(read('objects/objAd.gml').includes('phase=irandom(100)'));assert(read('objects/objAd.gml').includes('point_direction(x,y,400,304)'));assert(!read('objects/player.gml').includes('x+=3'));assert(!read('rooms/rAvoidance/instances.txt').includes('objRufController'));assert(read('scripts/rf_film_pin.gml').includes('x=400;y=304'));assert(read('settings/settings.txt').includes('show_error_messages=0'));
});
