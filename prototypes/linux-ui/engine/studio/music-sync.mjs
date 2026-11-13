import fs from 'node:fs';
import path from 'node:path';
import {codeOnly} from './native-import.mjs';
import {gmlNumber as num} from './gml.mjs';

// FMOD compatibility positions are normalized; channel sound lengths are milliseconds.
export const musicSyncLimits={softFrames:3,settledFrames:1,seekFrames:8,maxRateChange:.03,seekCooldownMs:500};
export function musicSyncScripts(){return [
  ['rf_music_sync_init',`rf_ms_handle=-1;rf_ms_length=0;rf_ms_rate=1;rf_ms_soft=0;rf_ms_last_frame=-1000000;rf_ms_last_seek=-1000000;rf_ms_error=0;rf_ms_seeks=0;return 0;`],
  ['rf_music_sync_tick',`// channel, logical song frame, logic FPS. Never advances game logic or restarts music.
var rf_ms_channel,rf_ms_frame,rf_ms_fps,rf_ms_actual,rf_ms_target,rf_ms_abs,rf_ms_wanted;
rf_ms_channel=argument0;rf_ms_frame=argument1;rf_ms_fps=max(1,argument2);
if !FMODInstanceIsPlaying(rf_ms_channel) return 0;
if rf_ms_channel!=rf_ms_handle {rf_music_sync_init();rf_ms_handle=rf_ms_channel;rf_ms_length=FMODInstanceSoundGetLength(rf_ms_channel);FMODInstanceSetPitch(rf_ms_channel,1);}
if rf_ms_length<=0 return 0;
if FMODInstanceGetPaused(rf_ms_channel) {if rf_ms_rate!=1 FMODInstanceSetPitch(rf_ms_channel,1);rf_ms_rate=1;rf_ms_soft=0;rf_ms_last_frame=rf_ms_frame;return 0;}
if rf_ms_frame>=rf_ms_last_frame && rf_ms_frame-rf_ms_last_frame<max(1,round(rf_ms_fps/10)) return 0;
rf_ms_last_frame=rf_ms_frame;
rf_ms_target=rf_ms_frame*1000/rf_ms_fps;
if rf_ms_target<0 || rf_ms_target>=rf_ms_length {if rf_ms_rate!=1 FMODInstanceSetPitch(rf_ms_channel,1);rf_ms_rate=1;rf_ms_soft=0;return 0;}
rf_ms_actual=FMODInstanceGetPosition(rf_ms_channel)*rf_ms_length;
rf_ms_error=(rf_ms_actual-rf_ms_target)*rf_ms_fps/1000;rf_ms_abs=abs(rf_ms_error);
if rf_ms_abs>${num(musicSyncLimits.seekFrames)} && current_time-rf_ms_last_seek>=${num(musicSyncLimits.seekCooldownMs)} {
 FMODInstanceSetPitch(rf_ms_channel,1);FMODInstanceSetPosition(rf_ms_channel,rf_ms_target/rf_ms_length);rf_ms_rate=1;rf_ms_soft=0;rf_ms_last_seek=current_time;rf_ms_seeks+=1;return 2;
}
if rf_ms_abs>${num(musicSyncLimits.softFrames)} rf_ms_soft=1;
if rf_ms_abs<${num(musicSyncLimits.settledFrames)} rf_ms_soft=0;
rf_ms_wanted=1;if rf_ms_soft rf_ms_wanted=1-clamp(rf_ms_error*.01,-${num(musicSyncLimits.maxRateChange)},${num(musicSyncLimits.maxRateChange)});
if abs(rf_ms_wanted-rf_ms_rate)>.001 {FMODInstanceSetPitch(rf_ms_channel,rf_ms_wanted);rf_ms_rate=rf_ms_wanted;}
return rf_ms_soft;
`]
];}

const action=`/*"/*'/**//* YYD ACTION\nlib_id=1\naction_id=603\napplies_to=self\n*/`;
export function nativeMusicPolicy(project,state,io=fs){
  const n=state.nativeProject;if(!n?.imported||!n.hasFmod||!/^[_a-zA-Z]\w*$/.test(n.controller||''))return {enabled:false,reason:'unrecognized'};
  const file=path.join(project,'objects',n.controller+'.gml');if(!io.existsSync(file))return {enabled:false,reason:'unrecognized'};
  const step=codeOnly(io.readFileSync(file,'utf8')).split(/(?=^#define\s)/m).filter(s=>/^#define Step_\d+\b/.test(s)).join('\n');
  if(/\bFMODInstanceGetPosition\s*\(/.test(step)&&/\bFMODInstanceSetPosition\s*\(/.test(step))return {enabled:false,reason:'engine-sync'};
  if(n.timeline)return {enabled:true,frame:`timeline_position/${num(n.timelineSpeed||1)}`,clock:'timeline_position',builtin:true};
  return /^[_a-zA-Z]\w*$/.test(n.clock||'')?{enabled:true,frame:n.clock,clock:n.clock}:{enabled:false,reason:'unrecognized'};
}
export function installNativeMusicSync(project,state,{io=fs,write=(f,s)=>fs.writeFileSync(f,s,'utf8'),append=(f,line)=>{const text=io.readFileSync(f,'utf8');if(!text.split(/\r?\n/).includes(line))write(f,text+(text.endsWith('\n')?'':'\n')+line+'\n');}}={}){
  const policy=nativeMusicPolicy(project,state,io),name='objRufMusicSync',object=path.join(project,'objects',name+'.gml');
  if(!policy.enabled){if(io.existsSync(object))write(object,`#define Step_2\n${action}\nexit;\n`);return false;}
  for(const [name,code] of musicSyncScripts()){write(path.join(project,'scripts',name+'.gml'),code+'\n');append(path.join(project,'scripts','index.yyd'),name);append(path.join(project,'scripts','tree.yyd'),'\t|'+name);}
  const harness=io.existsSync(path.join(project,'objects','objRufNativePreview.txt'));
  write(object,`#define Create_0\n${action}\nrf_music_sync_init();\n#define Step_2\n${action}
if room!=${state.room} exit;
${harness?"if instance_exists(objRufNativePreview) {if !objRufNativePreview.rf_playable || !objRufNativePreview.rf_presented exit;}":''}
rf_ms_clock_ready=0;rf_ms_song_frame=0;
if instance_exists(${state.nativeProject.controller}) with(${state.nativeProject.controller}) {if ${policy.builtin?'1':`variable_local_exists('${policy.clock}')`} {other.rf_ms_song_frame=${policy.frame};other.rf_ms_clock_ready=1;}}
if rf_ms_clock_ready && variable_global_exists('BGMHandler') rf_music_sync_tick(global.BGMHandler,rf_ms_song_frame,${num(state.fps,50)});
`);
  write(path.join(project,'objects',name+'.txt'),'sprite=\nvisible=0\nsolid=0\npersistent=0\ndepth=1000000000\nparent=\nmask=\n');append(path.join(project,'objects','index.yyd'),name);append(path.join(project,'objects','tree.yyd'),'\t|'+name);
  const instances=path.join(project,'rooms',state.room,'instances.txt');if(!io.readFileSync(instances,'utf8').split(/\r?\n/).some(l=>l.startsWith(name+',')))append(instances,name+',0,0,00FADE03,0,1,1,4294967295,0,0');return true;
}
