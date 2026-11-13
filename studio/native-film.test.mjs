import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import assert from 'node:assert/strict';
import {test,after} from 'node:test';
import {fixedPlayerReads,pinNativeEvents,makeFilmHarness,writeFilmHarness,nativeFilmStatus} from './native-film.mjs';
import {normalizeState} from './motion.mjs';

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
test('native film signature includes root configuration, entry and runtime dependencies',()=>{
  const state=normalizeState({version:2,room:'rAvoidance',fps:50,duration:30,music:'',tracks:[]});
  write('options.ini','mode=easy');write('game.gm82','gm82_version=1\nextensions=core');write('plugin.dll','first');
  let before=nativeFilmStatus(fixture,state).sourceSignature;
  write('options.ini','mode=hard');let after=nativeFilmStatus(fixture,state).sourceSignature;assert.notEqual(after,before);before=after;
  write('game.gm82','gm82_version=1\nextensions=sound');after=nativeFilmStatus(fixture,state).sourceSignature;assert.notEqual(after,before);before=after;
  fs.unlinkSync(path.join(fixture,'plugin.dll'));assert.notEqual(nativeFilmStatus(fixture,state).sourceSignature,before);
});
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
  const counts=read('scripts/rf_film_counts.gml'),status=read('scripts/rf_film_status.gml');assert(counts.includes('with(all)'));assert(counts.includes('object_get_name(object_index)'));assert(counts.includes('object_index!=objRufNativePreview'));assert(counts.includes('ds_map_destroy(rf_counts)'));
  assert(status.includes("'objects='+rf_film_counts()"));assert(status.indexOf('file_text_close(f)')<status.indexOf('file_rename(rf_temp,rf_path)'));assert(status.includes("rf_temp=rf_path+'.tmp'"));
});
test('native clip recording keeps a schedule controller and fingerprints code and timing',()=>{
  const state=normalizeState({version:2,room:'rAvoidance',fps:50,duration:30,music:'',nativeProject:{duration:30},
    tracks:[{id:'clip',name:'Actor',kind:'native-instance',nativeObject:'objAd',start:5,end:20,x:20,y:30,keyframes:[]}]});
  const before=nativeFilmStatus(fixture,state);
  state.tracks[0].name='Rename';state.tracks[0].lane=20;
  assert.equal(nativeFilmStatus(fixture,state).sourceSignature,before.sourceSignature);
  state.tracks[0].x=21;
  assert.notEqual(nativeFilmStatus(fixture,state).sourceSignature,before.sourceSignature);
  assert.deepEqual(before.nativeTrackIds,['clip']);
  assert(before.output.endsWith('objects.rufvideo'));
  fs.appendFileSync(path.join(fixture,'rooms','rAvoidance','instances.txt'),'\nobjRufController,0,0,2,0,1,1,0,0,0\n');
  writeFilmHarness(fixture,state,{objectClips:true});
  const controller=fs.readFileSync(path.join(fixture,'objects','objRufController.gml'),'utf8');
  assert(controller.includes('rf_native_objects(rf_frame)'));
  assert(!controller.includes('rf_pattern_step('));
  assert(fs.readFileSync(path.join(fixture,'rooms','rAvoidance','instances.txt'),'utf8').includes('objRufController,'));
});
