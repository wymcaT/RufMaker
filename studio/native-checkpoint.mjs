// These scripts exist only in the disposable F5 build, never in exported games.
export function checkpointScripts(state) {
  const fmod=state.nativeProject.hasFmod;
  return {
    rf_native_checkpoint:`if rf_checkpoint_ready return 0;
rf_checkpoint_ready=1;rf_checkpoint_seed=random_get_seed();rf_checkpoint_music=-1;
${fmod?"if variable_global_exists('BGMHandler') {if FMODInstanceIsPlaying(global.BGMHandler) rf_checkpoint_music=FMODInstanceGetPosition(global.BGMHandler);}":''}
game_save(working_directory+'\\Data\\rufmaker_checkpoint.sav');return 0;`,
    rf_native_retry:`if !rf_playable || !rf_presented || !rf_checkpoint_ready return 0;
if !file_exists(working_directory+'\\Data\\rufmaker_checkpoint.sav') {show_message('测试快照不存在，请在编辑器重新按 F5 建立测试起点。');return 0;}
var rf_marker;rf_marker=file_text_open_write(working_directory+'\\Data\\rufmaker_checkpoint_restore');file_text_close(rf_marker);
game_load(working_directory+'\\Data\\rufmaker_checkpoint.sav');return 0;`,
    rf_native_restore:`if !file_exists(working_directory+'\\Data\\rufmaker_checkpoint_restore') return 0;
file_delete(working_directory+'\\Data\\rufmaker_checkpoint_restore');
keyboard_clear(ord('R'));random_set_seed(rf_checkpoint_seed);sound_global_volume(1);
${fmod?`if rf_checkpoint_music>=0 && variable_global_exists('BGMHandler') {
 if !FMODInstanceIsPlaying(global.BGMHandler) {if variable_global_exists('BGM_Av') global.BGMHandler=FMODSoundPlay(global.BGM_Av,1);}
 if FMODInstanceIsPlaying(global.BGMHandler) {FMODInstanceSetPosition(global.BGMHandler,rf_checkpoint_music);FMODInstanceSetPaused(global.BGMHandler,0);FMODInstanceSetVolume(global.BGMHandler,1);}
}`:''}
// Surfaces are GPU resources, so discard temporal FX history after restoring game state.
${state.tracks.some(t=>t.kind!=='native')?`if instance_exists(objRufController) {with(objRufController) {var rf_h;for(rf_h=0;rf_h<${Math.max(1,state.tracks.filter(t=>t.enabled!==false&&(t.kind==='effect'||t.kind==='camera')).length)};rf_h+=1) rf_fx_history_frame[rf_h]=-1000000;}}`:''}
ini_open(working_directory+'\\Data\\rufmaker_checkpoint_count.ini');rf_restores=ini_read_real('test','restores',0)+1;ini_write_real('test','restores',rf_restores);ini_close();
rf_native_status();return 0;`
  };
}
