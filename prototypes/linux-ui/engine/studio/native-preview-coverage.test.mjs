import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {nativeFilmStatus,writeFilmHarness} from './native-film.mjs';
import {normalizeState} from './motion.mjs';
const root=fileURLToPath(new URL('..',import.meta.url));
function fixture(run){
  const parent=path.join(root,'build');fs.mkdirSync(parent,{recursive:true});const dir=fs.mkdtempSync(path.join(parent,'coverage-source-'));
  const write=(name,text)=>{const file=path.join(dir,name);fs.mkdirSync(path.dirname(file),{recursive:true});fs.writeFileSync(file,text);};
  try{return run(dir,write);}finally{assert.equal(path.dirname(dir),parent);fs.rmSync(dir,{recursive:true,force:true});}
}
function scene(){return normalizeState({version:2,room:'rAvoidance',fps:50,duration:20,music:'',randomSeed:12345,tracks:[{id:'clip',kind:'native-instance',nativeObject:'objActor',x:30,y:40,start:0,end:20,keyframes:[]},{id:'live',kind:'object',x:0,y:0,start:0,end:20,keyframes:[]}]});}
test('film provenance records fixed reference and invalidates each available source/project dependency',()=>fixture((dir,write)=>{
  write('game.gm82','gm82_version=1');write('objects/objActor.gml','#define Step_0\nx+=1;');write('timelines/tlMain.gml','#define 0\nx+=2;');write('rooms/rAvoidance/instances.txt','objActor,1,1,123,0,1,1,0,0,0');
  const state=scene(),before=nativeFilmStatus(dir,state);
  assert.equal(before.coverageVersion,1);assert.equal(before.playerReference,'fixed');assert.deepEqual([before.playerX,before.playerY],[400,304]);assert.deepEqual(before.nativeTrackIds,['clip']);
  for(const mutate of [s=>s.randomSeed++,s=>s.room='rOther',s=>s.duration++,s=>s.fps=49,s=>s.music='Data/new.ogg',s=>s.tracks[0].code='x+=5;',s=>s.tracks[0].end--,s=>s.nativeProject={duration:20,timeline:'tlMain',timelineSpeed:1}]){const clone=structuredClone(state);mutate(clone);assert.notEqual(nativeFilmStatus(dir,clone).sourceSignature,before.sourceSignature);}
  for(const file of ['objects/objActor.gml','timelines/tlMain.gml','rooms/rAvoidance/instances.txt','game.gm82']){const old=fs.readFileSync(path.join(dir,file),'utf8'),stat=fs.statSync(path.join(dir,file));write(file,old+'\n//changed');fs.utimesSync(path.join(dir,file),stat.atime,stat.mtime);assert.notEqual(nativeFilmStatus(dir,state).sourceSignature,before.sourceSignature,file);write(file,old);}
}));
test('fixed native film is reusable for live-only and player-reference changes; these are not recorded dependencies',()=>fixture((dir)=>{
  const state=scene(),signature=nativeFilmStatus(dir,state).sourceSignature;
  state.tracks[1].x=500;state.tracks[1].code='ordinary field';state.nativeTest={x:90,y:100};state.usePlayerTrace=true;state.playerTrace=[{frame:0,x:1,y:2}];
  assert.equal(nativeFilmStatus(dir,state).sourceSignature,signature);
  state.tracks.push({id:'gml',kind:'script',start:1,end:1,code:'instance_create(0,0,objActor);',keyframes:[]});
  assert.equal(nativeFilmStatus(dir,state).sourceSignature,signature,'arbitrary timeline GML is NOT part of this recording, and must be reported separately');
}));
test('native timeline source remains executed in the film clone; custom timeline GML and live objects are not',()=>fixture((dir,write)=>{
  for(const folder of ['objects','scripts']){write(folder+'/index.yyd','');write(folder+'/tree.yyd','');}
  write('objects/player.txt','');write('objects/player.gml','#define Create_0\nx=1;');write('objects/objActor.gml','#define Step_0\nx+=1;');write('objects/objRufController.gml','#define Step_0\ncustom_not_recorded();');write('rooms/index.yyd','rAvoidance');write('rooms/rAvoidance/instances.txt','player,0,0,1,0,1,1,0,0,0\nobjRufController,0,0,2,0,1,1,0,0,0');write('timelines/tlMain.gml','#define 0\ninstance_create(player.x,player.y,objActor);');
  const state=scene();state.nativeProject={duration:20,controller:'objActor',clock:'clock',hasFmod:false};
  writeFilmHarness(dir,state,{objectClips:true});
  const timeline=fs.readFileSync(path.join(dir,'timelines/tlMain.gml'),'utf8'),controller=fs.readFileSync(path.join(dir,'objects/objRufController.gml'),'utf8');
  assert(timeline.includes('instance_create(400,304,objActor)'));assert(controller.includes('rf_native_objects(rf_frame)'));assert(!controller.includes('custom_not_recorded'));assert(!controller.includes('rf_pattern_step'));
}));
test('coverage UI strings have English entries and the explicit product build includes the new service',()=>{
  const source=fs.readFileSync(path.join(root,'desktop/NativePreviewCoverage.cs'),'utf8'),locale=JSON.parse(fs.readFileSync(path.join(root,'assets/locales/en.json'),'utf8'));
  for(const match of source.matchAll(/"([^"\n]*[\u3400-\u9fff][^"\n]*)"/g))assert(locale[match[1]],'Missing English text: '+match[1]);
  assert(fs.readFileSync(path.join(root,'desktop/Build-Native.ps1'),'utf8').includes("'NativePreviewCoverage'"));
});

test('selected project entry is a recording dependency even when both entry files already exist',()=>fixture((dir,write)=>{
  write('first.gm82','gm82_version=1');write('second.gm82','gm82_version=1');const state=scene();
  const first=nativeFilmStatus(dir,state,{projectFile:path.join(dir,'first.gm82')}),second=nativeFilmStatus(dir,state,{projectFile:path.join(dir,'second.gm82')});
  assert.equal(first.projectEntry,'first.gm82');assert.notEqual(first.sourceSignature,second.sourceSignature);
  const bridge=fs.readFileSync(path.join(root,'studio/bridge.mjs'),'utf8'),film=fs.readFileSync(path.join(root,'studio/native-film.mjs'),'utf8');
  assert(bridge.includes('nativeFilmStatus(project,readState(),{projectFile})'));assert(film.includes('nativeFilmStatus(project,state,{projectFile})'));
}));
