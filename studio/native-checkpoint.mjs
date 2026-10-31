import {gmlNumber} from './gml.mjs';
const nextSeed="1+floor((current_time+date_get_second(date_current_datetime())*104729) mod 2147483646)";
export function plainRetryEvents(source){return source.split(/(?=^#define\s)/m).map(part=>{const header=/^#define\s+([^\r\n]+)/.exec(part);if(!header||!/^(?:KeyPress|Keyboard)_82$/.test(header[1].trim())||part.includes('// RufMaker F5 retry'))return part;return part.replace(/\/\*"\/\*'\/\*\*\/\/\* YYD ACTION[\s\S]*?\*\//g,metadata=>/action_id=603\b/.test(metadata)?metadata+"\n// RufMaker F5 retry\nif instance_exists(objRufController) {if objRufController.rf_preview && objRufController.rf_checkpoint_ready {with(objRufController)rf_preview_retry();exit;}}\n":metadata);}).join('');}
export function plainCheckpointScripts(state,cleanup='',particles=''){
  const count=Math.max(1,state.tracks.filter(t=>t.enabled!==false&&(t.kind==='effect'||t.kind==='camera')).length);
  return [
    ['rf_preview_checkpoint',`if !rf_preview || rf_checkpoint_ready return 0;rf_checkpoint_ready=1;rf_checkpoint_seed=random_get_seed();rf_checkpoint_music=(rf_target/${gmlNumber(state.fps,50)}*1000)/FMODSoundGetLength(BGM_Ad);game_save(working_directory+'\\Data\\rufmaker_plain_checkpoint.sav');return 1;`],
    ['rf_preview_retry',`if !rf_preview || !rf_checkpoint_ready || !file_exists(working_directory+'\\Data\\rufmaker_plain_checkpoint.sav') return 0;if file_exists(working_directory+'\\Data\\rufmaker_plain_restore') return 1;
var rf_marker;rf_marker=file_text_open_write(working_directory+'\\Data\\rufmaker_plain_restore');file_text_close(rf_marker);if FMODInstanceIsPlaying(rf_music) FMODInstanceSetPaused(rf_music,1);${particles}${cleanup}game_load(working_directory+'\\Data\\rufmaker_plain_checkpoint.sav');return 1;`],
    ['rf_preview_restore',`if !file_exists(working_directory+'\\Data\\rufmaker_plain_restore') return 0;file_delete(working_directory+'\\Data\\rufmaker_plain_restore');keyboard_clear(ord('R'));
${state.randomizeOnPlay?`randomize();rf_random_seed=${nextSeed};`:'random_set_seed(rf_checkpoint_seed);'}
if !FMODInstanceIsPlaying(rf_music) rf_music=FMODSoundPlay(BGM_Ad,1);global.BGMHandler=rf_music;FMODInstanceSetPitch(rf_music,1);FMODInstanceSetPosition(rf_music,rf_checkpoint_music);FMODInstanceSetPaused(rf_music,0);FMODInstanceSetVolume(rf_music,1);rf_music_sync_init();
rf_fx_scene=-1;rf_fx_a=-1;rf_fx_b=-1;rf_fx_rendering=0;var rf_h;for(rf_h=0;rf_h<${count};rf_h+=1){rf_fx_history[rf_h]=-1;rf_fx_history_frame[rf_h]=-1000000;}
ini_open(working_directory+'\\Data\\rufmaker_plain_restore_count.ini');rf_restores=ini_read_real('test','restores',0)+1;ini_write_real('test','restores',rf_restores);ini_close();return 1;`]
  ];
}

// Imported native sessions keep their checkpoint in the disposable F5 build.
export function checkpointScripts(state,particles='',musicReset='') {
  const fmod=state.nativeProject.hasFmod;
  return {
    rf_native_checkpoint:`if rf_checkpoint_ready return 0;
rf_checkpoint_ready=1;rf_checkpoint_seed=random_get_seed();rf_checkpoint_music=-1;
${fmod?"if variable_global_exists('BGMHandler') {if FMODInstanceIsPlaying(global.BGMHandler) rf_checkpoint_music=FMODInstanceGetPosition(global.BGMHandler);}":''}
game_save(working_directory+'\\Data\\rufmaker_checkpoint.sav');return 0;`,
    rf_native_retry:`if !rf_playable || !rf_presented || !rf_checkpoint_ready return 0;
if !file_exists(working_directory+'\\Data\\rufmaker_checkpoint.sav') {show_message('测试快照不存在，请在编辑器重新按 F5 建立测试起点。');return 0;}
if file_exists(working_directory+'\\Data\\rufmaker_checkpoint_restore') return 0;
var rf_marker;rf_marker=file_text_open_write(working_directory+'\\Data\\rufmaker_checkpoint_restore');file_text_close(rf_marker);
${particles}game_load(working_directory+'\\Data\\rufmaker_checkpoint.sav');return 0;`,
    rf_native_restore:`if !file_exists(working_directory+'\\Data\\rufmaker_checkpoint_restore') return 0;
file_delete(working_directory+'\\Data\\rufmaker_checkpoint_restore');
keyboard_clear(ord('R'));${state.randomizeOnPlay?'randomize();':'random_set_seed(rf_checkpoint_seed);'}sound_global_volume(1);
${state.randomizeOnPlay&&state.tracks.some(t=>t.kind!=='native')?`if instance_exists(objRufController) with(objRufController) rf_random_seed=${nextSeed};`:''}
${fmod?`if rf_checkpoint_music>=0 && variable_global_exists('BGMHandler') {
 if !FMODInstanceIsPlaying(global.BGMHandler) {if variable_global_exists('BGM_Av') global.BGMHandler=FMODSoundPlay(global.BGM_Av,1);}
 if FMODInstanceIsPlaying(global.BGMHandler) {FMODInstanceSetPosition(global.BGMHandler,rf_checkpoint_music);FMODInstanceSetPaused(global.BGMHandler,0);FMODInstanceSetVolume(global.BGMHandler,1);}
}`:''}
${musicReset}
// Surfaces are GPU resources, so discard temporal FX history after restoring game state.
${state.tracks.some(t=>t.kind!=='native')?`if instance_exists(objRufController) {with(objRufController) {var rf_h;for(rf_h=0;rf_h<${Math.max(1,state.tracks.filter(t=>t.enabled!==false&&(t.kind==='effect'||t.kind==='camera')).length)};rf_h+=1) rf_fx_history_frame[rf_h]=-1000000;}}`:''}
ini_open(working_directory+'\\Data\\rufmaker_checkpoint_count.ini');rf_restores=ini_read_real('test','restores',0)+1;ini_write_real('test','restores',rf_restores);ini_close();
rf_native_status();return 0;`
  };
}
