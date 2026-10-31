import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {musicSyncScripts,musicSyncLimits,nativeMusicPolicy,installNativeMusicSync} from './music-sync.mjs';
import {plainCheckpointScripts,checkpointScripts} from './native-checkpoint.mjs';

test('music policy has frame thresholds, bounded rate and no gameplay skipping or automatic resume',()=>{
  assert.deepEqual(musicSyncLimits,{softFrames:3,settledFrames:1,seekFrames:8,maxRateChange:.03,seekCooldownMs:500});
  const scripts=Object.fromEntries(musicSyncScripts()),tick=scripts.rf_music_sync_tick;
  assert.match(tick,/FMODInstanceSoundGetLength/);assert.match(tick,/rf_ms_frame\*1000\/rf_ms_fps/);
  assert.match(tick,/rf_ms_fps\/10/);assert.match(tick,/FMODInstanceGetPaused/);
  assert.doesNotMatch(tick,/SetPaused|SoundPlay|room_speed=|rf_frame\+=|game_restart/);
  const restore=Object.fromEntries(plainCheckpointScripts({fps:50,tracks:[]})).rf_preview_restore;
  assert.match(restore,/FMODInstanceSetPitch\(rf_music,1\)/);assert.match(restore,/rf_music_sync_init\(\)/);
  const native=checkpointScripts({nativeProject:{hasFmod:true},tracks:[]},'','rf_test_music_reset();');assert.match(native.rf_native_restore,/rf_test_music_reset/);
});

test('native projects keep existing music sync and add one observer only for recognized clocks',()=>{
  const root=path.resolve('build/music-sync-check/policy-'+Date.now());fs.mkdirSync(path.join(root,'objects'),{recursive:true});fs.mkdirSync(path.join(root,'scripts'));fs.mkdirSync(path.join(root,'rooms','test'),{recursive:true});
  for(const file of ['objects/index.yyd','objects/tree.yyd','scripts/index.yyd','scripts/tree.yyd','rooms/test/instances.txt'])fs.writeFileSync(path.join(root,file),'');
  const source=path.join(root,'objects/objAd.gml'),state={fps:50,room:'test',nativeProject:{imported:true,hasFmod:true,controller:'objAd',clock:'ticks'}};
  const own=`#define Create_0\nFMODInstanceSetPosition(global.BGMHandler,0);\n#define Step_0\nif abs(FMODInstanceGetPosition(global.BGMHandler)-m_time)>75 FMODInstanceSetPosition(global.BGMHandler,m_time);\n`;
  fs.writeFileSync(source,own);assert.equal(nativeMusicPolicy(root,state).reason,'engine-sync');assert.equal(installNativeMusicSync(root,state),false);assert.equal(fs.readFileSync(source,'utf8'),own);assert.equal(fs.existsSync(path.join(root,'objects/objRufMusicSync.gml')),false);
  const noSync='#define Create_0\nticks=0;\n#define Step_0\nticks+=1;\n';fs.writeFileSync(source,noSync);assert.equal(installNativeMusicSync(root,state),true);assert.equal(installNativeMusicSync(root,state),true);
  assert.equal(fs.readFileSync(path.join(root,'rooms/test/instances.txt'),'utf8').split('\n').filter(l=>l.startsWith('objRufMusicSync,')).length,1);
  const observer=path.join(root,'objects/objRufMusicSync.gml');assert.doesNotMatch(fs.readFileSync(observer,'utf8'),/objRufNativePreview/);assert.equal(fs.readFileSync(source,'utf8'),noSync);
  fs.writeFileSync(path.join(root,'objects/objRufNativePreview.txt'),'');installNativeMusicSync(root,state);assert.match(fs.readFileSync(observer,'utf8'),/rf_playable.*rf_presented/);
  const timeline={...state,nativeProject:{...state.nativeProject,timeline:'tlAd',timelineSpeed:.5}};assert.equal(nativeMusicPolicy(root,timeline).frame,'timeline_position/0.5');
  fs.writeFileSync(source,own);installNativeMusicSync(root,state);assert.match(fs.readFileSync(observer,'utf8'),/exit;/);assert.doesNotMatch(fs.readFileSync(observer,'utf8'),/sync_tick/);
});
