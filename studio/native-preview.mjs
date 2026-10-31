import fs from 'node:fs';
import path from 'node:path';
import {spawn} from 'node:child_process';
import crypto from 'node:crypto';
import {codeOnly} from './native-import.mjs';
import {gmlNumber} from './gml.mjs';
import {checkpointScripts} from './native-checkpoint.mjs';
import {retryParticleCleanup,guardPendingDeath} from './retry-particles.mjs';
import {nativeMusicPolicy,installNativeMusicSync} from './music-sync.mjs';

const action=`/*"/*'/**//* YYD ACTION\nlib_id=1\naction_id=603\napplies_to=self\n*/`;
const generator=crypto.createHash('sha256').update(fs.readFileSync(new URL(import.meta.url))).update(fs.readFileSync(new URL('./native-import.mjs',import.meta.url))).update(fs.readFileSync(new URL('./native-checkpoint.mjs',import.meta.url))).update(fs.readFileSync(new URL('./retry-particles.mjs',import.meta.url))).update(fs.readFileSync(new URL('./music-sync.mjs',import.meta.url))).digest('hex');
const section=(event,code)=>`#define ${event}\n${action}\n${code}\n`;
const write=(f,text)=>fs.writeFileSync(f,text,'utf8');
function append(file,line){const source=fs.readFileSync(file,'utf8');if(!source.split(/\r?\n/).includes(line))fs.appendFileSync(file,(source.endsWith('\n')?'':'\n')+line+'\n');}
export function copyNativeExtras(project,destination){for(const entry of fs.readdirSync(project,{withFileTypes:true}).filter(e=>e.isFile()&&e.name!=='rufmaker.json'&&(/\.(dll|ini|dat|cfg|bin|png|jpe?g|bmp|gif|webp|mp3|ogg|wav|flac|mp4|avi|wmv|json)$/i.test(e.name)||/^save\d*$/i.test(e.name))))fs.copyFileSync(path.join(project,entry.name),path.join(destination,entry.name));}
function allFiles(folder){const result=[];for(const e of fs.readdirSync(folder,{withFileTypes:true})){const f=path.join(folder,e.name);if(e.isDirectory()&&!['cache','build'].includes(e.name))result.push(...allFiles(f));else if(e.isFile()&&!/rufmaker_(?:preview|native)/.test(e.name))result.push(f);}return result;}
export function nativePreviewStamp(project){return allFiles(project).reduce((n,f)=>Math.max(n,fs.statSync(f).mtimeMs),0);}
export function commandText(frame,playing=false,playable=false,position={},godMode=false){return `[control]\r\nserial=1\r\nframe=${Math.max(0,Math.round(frame))}\r\nplaying=${playing?1:0}\r\nplayable=${playable?1:0}\r\ntest_x=${gmlNumber(position.x,400)}\r\ntest_y=${gmlNumber(position.y,304)}\r\ngod=${godMode?1:0}\r\nseek=1\r\n` ;}
export function nativePlayers(project){return fs.readdirSync(path.join(project,'objects')).filter(n=>/^(?:(?:obj_?|o)?player[12]?)\.txt$/i.test(n)).map(n=>n.slice(0,-4));}
export function patchTestPlayerEvents(source,player=false){
  return source.split(/(?=^#define\s)/m).map(part=>{const header=/^#define\s+(\S+)[^\r\n]*\r?\n/.exec(part);if(!header)return part;
    if(/^(?:KeyPress|Keyboard)_82$/.test(header[1]))return section(header[1],'with(objRufNativePreview) rf_native_retry();');
    if(player&&/^(?:Step_|Keyboard_|KeyPress_|KeyRelease_|Collision_)/.test(header[1])){
      const guard='\nif instance_exists(objRufNativePreview) {if !objRufNativePreview.rf_presented {rf_native_place();exit;}}\n',body=part.slice(header[0].length);let found=false;
      const patched=body.replace(/\/\*"\/\*'\/\*\*\/\/\* YYD ACTION[\s\S]*?\*\//g,metadata=>{if(/action_id=603\b/.test(metadata)){found=true;return metadata+guard;}return metadata;});
      return header[0]+(found?patched:action+guard+body);
    }
    return part;
  }).join('');
}
export function rewriteNativeRuntime(source,state){
  const clean=codeOnly(source).replace(/(['"])[\s\S]*?\1/g,m=>' '.repeat(m.length)),edits=[];
  for(const m of clean.matchAll(/\b(room_speed|ffps|FMODInstanceSetVolume)\b|\brandomize\s*\(\s*\)/g)){
    const before=clean.slice(0,m.index),after=clean.slice(m.index+m[0].length);
    if(/\.\s*$/.test(before))continue;
    if(m[0].startsWith('randomize')){edits.push([m.index,m[0].length,'random_set_seed(1234567)']);continue;}
    if(m[1]==='FMODInstanceSetVolume'){
      if(state.nativeProject.hasFmod&&/^\s*\(/.test(after))edits.push([m.index,m[0].length,'rf_native_volume']);
      continue;
    }
    // Leave every write target intact, including compound assignments and ++/--.
    if(/^\s*(?:=(?!=)|(?:<<|>>|[+\-*/%&|^])=|\+\+|--)/.test(after)||/(?:\+\+|--)\s*$/.test(before))continue;
    edits.push([m.index,m[0].length,String(state.fps)]);
  }
  let result=source;for(const [at,length,value] of edits.reverse())result=result.slice(0,at)+value+result.slice(at+length);
  return result;
}
export function makeNativeHarness(state,{playable=false}={}) {
  const n=state.nativeProject,fps=state.fps;
  const fmod=n.hasFmod?`if variable_global_exists('BGMHandler') {if global.BGMHandler>0 {FMODInstanceSetVolume(global.BGMHandler,rf_volume);}}`:'';
  return section('Create_0',`window_set_visible(0);global.rf_native_preview=1;global.rf_native_frame=0;rf_target=0;rf_playing=0;rf_playable=0;rf_sequence=-1;rf_test=0;rf_boot=0;rf_entered=0;rf_presented=0;rf_refresh=1;rf_pause_draw=0;rf_rewind=0;rf_restart=0;rf_restores=0;rf_checkpoint_ready=0;rf_test_x=400;rf_test_y=304;rf_god_mode=0;global.rf_god_mode=0;random_set_seed(1234567);${playable?"var rf_trace_file;rf_trace_file=file_text_open_write(working_directory+'\\\\Data\\\\rufmaker_player_trace.csv');file_text_close(rf_trace_file);":""}`)+
  section('Step_1',`rf_native_read();if (variable_global_exists('rf_god_mode') && global.rf_god_mode) with(player) godmode=1;if !rf_playable rf_native_keys();
${playable?"rf_native_restore();if rf_playable && rf_presented && keyboard_check_pressed(ord('R')) {keyboard_clear(ord('R'));rf_native_retry();exit;}\nif !rf_presented rf_native_place();":''}
if !rf_presented window_set_visible(0);
if !rf_entered {rf_boot+=1;if rf_boot=3 {${n.musicInitializer||''}room_goto(${state.room});}exit;}
if !rf_playing && rf_target<global.rf_native_frame rf_native_wait_rewind();
if global.rf_native_frame>=rf_target {
 room_speed=${fps};
 if !rf_presented {${playable?'rf_native_place();rf_native_release();':''}rf_presented=1;screen_refresh();window_set_visible(1);${playable?'rf_native_checkpoint();':''}}
}
if !rf_playable && global.rf_native_frame>=${state.duration} {rf_playing=0;rf_target=global.rf_native_frame;}
if global.rf_native_frame>=rf_target && !rf_playing {
 if !rf_pause_draw {if global.rf_native_frame=0 screen_redraw();screen_refresh();if !rf_playable screen_save(working_directory+'\\Data\\rufmaker_native_frame.bmp');rf_pause_draw=1;rf_refresh=0;}
 rf_native_status();
 while !rf_playing && rf_target<=global.rf_native_frame {
  sleep(15);io_handle();rf_native_read();if !rf_playable rf_native_keys();
  if rf_target<global.rf_native_frame rf_native_wait_rewind();
  if rf_refresh {screen_refresh();rf_refresh=0;rf_native_status();}
 }
}
rf_pause_draw=0;
room_speed=${fps};if global.rf_native_frame<rf_target room_speed=1000;
var rf_volume;rf_volume=0;if (rf_playable || ${state.music?'0':'1'}) && rf_playing && global.rf_native_frame>=rf_target rf_volume=1;
sound_global_volume(rf_volume);${fmod}
if rf_playing && global.rf_native_frame>=rf_target rf_target=global.rf_native_frame;
${playable?"if rf_presented rf_record_trace();\n":""}global.rf_native_frame+=1;`)+
  section('Step_2',`if rf_entered {if rf_presented || global.rf_native_frame mod 10=0 rf_native_status();}`)+
  section('Other_4',`if room=${state.room} && !rf_entered {global.rf_native_frame=0;rf_entered=1;rf_boot=0;rf_pause_draw=0;room_speed=${fps};}`);
}
export function writeNativeHarness(project,state,{playable=false}={}){
  const native=state.nativeProject,fps=state.fps;
  const data=path.join(project,'Data');fs.mkdirSync(data,{recursive:true});
  const scripts={
    rf_native_read:`if file_exists(working_directory+'\\Data\\rufmaker_native_command.ini') {ini_open(working_directory+'\\Data\\rufmaker_native_command.ini');var rf_serial,rf_seek;rf_serial=ini_read_real('control','serial',1);if rf_serial!=rf_sequence {rf_sequence=rf_serial;rf_refresh=1;rf_seek=ini_read_real('control','seek',1);if rf_seek {rf_target=max(0,min(${state.duration},ini_read_real('control','frame',0)));}else if global.rf_native_frame>=rf_target {rf_target=global.rf_native_frame;}rf_playing=ini_read_real('control','playing',0);if rf_seek && rf_target<global.rf_native_frame rf_playing=0;rf_playable=ini_read_real('control','playable',0);rf_test_x=ini_read_real('control','test_x',400);rf_test_y=ini_read_real('control','test_y',304);rf_god_mode=ini_read_real('control','god',0);global.rf_god_mode=rf_god_mode;if (global.rf_god_mode) with(player) godmode=1;}ini_close();}return 0;`,
    rf_native_status:`ini_open(working_directory+'\\Data\\rufmaker_native_status.ini');ini_write_real('native','test',rf_test);ini_write_real('native','sequence',rf_sequence);ini_write_real('native','frame',global.rf_native_frame);ini_write_real('native','target',rf_target);ini_write_real('native','playing',rf_playing);ini_write_real('native','ready',rf_presented);ini_write_real('native','rewind',rf_rewind);ini_write_real('native','restart',rf_restart);ini_write_string('native','room',room_get_name(room));ini_close();return 0;`,
    rf_native_wait_rewind:`rf_rewind=1;rf_playing=0;rf_native_status();while rf_target<global.rf_native_frame {sleep(15);io_handle();rf_native_read();if rf_refresh {rf_refresh=0;rf_native_status();}}rf_rewind=0;rf_native_status();return 0;`,
    rf_native_keys:`if keyboard_check_pressed(vk_space) {keyboard_clear(vk_space);rf_playing=1-rf_playing;rf_target=global.rf_native_frame;}if keyboard_check_pressed(vk_f5) {keyboard_clear(vk_f5);rf_test+=1;rf_native_status();}return 0;`
  };
  if(playable){
    const players=nativePlayers(project);if(!players.length)throw new Error('未识别玩家对象，无法设置试玩起点。');
    scripts.rf_native_place=players.map(name=>`with(${name}) {if !variable_local_exists('rf_native_gravity') rf_native_gravity=gravity;x=objRufNativePreview.rf_test_x;y=objRufNativePreview.rf_test_y;hspeed=0;vspeed=0;gravity=0;}`).join('\n')+'\nreturn 0;';
    scripts.rf_native_release=players.map(name=>`with(${name}) {if variable_local_exists('rf_native_gravity') gravity=rf_native_gravity;}`).join('\n')+'\nreturn 0;';
    scripts.rf_release_trace=`return 0;`;
    scripts.rf_record_trace=`if !rf_playable || !rf_presented return 0;
var rf_file,rf_p,rf_px,rf_py,rf_spr,rf_sub,rf_xs,rf_ys,rf_alp,rf_ang,rf_dead;
rf_file=file_text_open_append(working_directory+'\\Data\\rufmaker_player_trace.csv');
if instance_exists(player){
  rf_p=instance_find(player,0);
  rf_px=rf_p.x;
  rf_py=rf_p.y;
  if (rf_p.sprite_index>=0) rf_spr=sprite_get_name(rf_p.sprite_index); else rf_spr='sprPlayerIdle';
  rf_sub=floor(rf_p.image_index);
  rf_xs=rf_p.image_xscale;if (rf_xs==0) rf_xs=1;
  rf_ys=rf_p.image_yscale;if (rf_ys==0) rf_ys=1;
  rf_alp=rf_p.image_alpha;
  rf_ang=rf_p.image_angle;
  rf_dead=0;
}else{
  rf_px=rf_test_x;
  rf_py=rf_test_y;
  rf_spr='sprPlayerIdle';
  rf_sub=0;
  rf_xs=1;
  rf_ys=1;
  rf_alp=0;
  rf_ang=0;
  rf_dead=1;
}
file_text_write_string(rf_file,string(floor(global.rf_native_frame))+','+string_format(rf_px,0,6)+','+string_format(rf_py,0,6)+','+rf_spr+','+string(rf_sub)+','+string_format(rf_xs,0,2)+','+string_format(rf_ys,0,2)+','+string_format(rf_alp,0,2)+','+string_format(rf_ang,0,2)+','+string(rf_dead));
file_text_writeln(rf_file);
file_text_close(rf_file);
return 0;`;
    Object.assign(scripts,checkpointScripts(state,retryParticleCleanup(project),nativeMusicPolicy(project,state).enabled?"if variable_global_exists('BGMHandler') {if FMODInstanceIsPlaying(global.BGMHandler) FMODInstanceSetPitch(global.BGMHandler,1);}with(objRufMusicSync) rf_music_sync_init();":''));
    for(const file of fs.readdirSync(path.join(project,'objects')).filter(f=>f.endsWith('.gml'))){const target=path.join(project,'objects',file);write(target,patchTestPlayerEvents(fs.readFileSync(target,'utf8'),players.includes(file.slice(0,-4))));}
    for(const file of fs.readdirSync(path.join(project,'scripts')).filter(f=>/^(?:loadGame|scrLoadGame)\.gml$/i.test(f))){const target=path.join(project,'scripts',file);write(target,"if instance_exists(objRufNativePreview) {if objRufNativePreview.rf_playable && objRufNativePreview.rf_presented {with(objRufNativePreview) rf_native_retry();return 0;}}\n"+fs.readFileSync(target,'utf8'));}
  }
  if(native.hasFmod)scripts.rf_native_volume=`var volume;volume=argument1;if instance_exists(objRufNativePreview) {if (!objRufNativePreview.rf_playable && ${state.music?'1':'0'}) || !objRufNativePreview.rf_playing || global.rf_native_frame<objRufNativePreview.rf_target volume=0;}return FMODInstanceSetVolume(argument0,volume);`;
  for(const [name,code] of Object.entries(scripts)){write(path.join(project,'scripts',name+'.gml'),code+'\n');append(path.join(project,'scripts','index.yyd'),name);append(path.join(project,'scripts','tree.yyd'),'\t|'+name);}
  const name='objRufNativePreview';write(path.join(project,'objects',name+'.txt'),'sprite=\nvisible=1\nsolid=0\npersistent=1\ndepth=1000000000\nparent=\nmask=\n');write(path.join(project,'objects',name+'.gml'),makeNativeHarness(state,{playable}));append(path.join(project,'objects','index.yyd'),name);append(path.join(project,'objects','tree.yyd'),'\t|'+name);
  const firstRoom=fs.readFileSync(path.join(project,'rooms','index.yyd'),'utf8').split(/\r?\n/).find(Boolean);fs.appendFileSync(path.join(project,'rooms',firstRoom,'instances.txt'),'\n'+name+',0,0,00FADE01,0,1,1,4294967295,0,0\n');
  const settings=path.join(project,'settings','settings.txt');if(fs.existsSync(settings))write(settings,fs.readFileSync(settings,'utf8').replace(/^freeze_on_lose_focus=\d+/m,'freeze_on_lose_focus=0').replace(/^fullscreen=\d+/m,'fullscreen=0'));
  const boot=native.bootObject?path.join(project,'objects',native.bootObject+'.gml'):'';
  if(boot&&fs.existsSync(boot)){let source=fs.readFileSync(boot,'utf8');source=source.replace(/\broom_goto_next\s*\(\s*\)\s*;?/,`{${native.musicInitializer||''}room_goto(${state.room});}`);write(boot,source);}
  const controller=native.controller?path.join(project,'objects',native.controller+'.gml'):'';
  if(controller&&fs.existsSync(controller)){
    let source=fs.readFileSync(controller,'utf8');const createEnd=source.search(/^#define (?!Create_0)/m),create=source.slice(0,createEnd<0?source.length:createEnd),rest=createEnd<0?'':source.slice(createEnd);
    // Reset authoring test offsets only in the disposable preview build.
    const variables=[native.clock,'start_pos','STARTSTEP'].filter(v=>/^[A-Za-z_]\w*$/.test(v||''));let patched=create;
    for(const v of variables)patched=patched.replace(new RegExp('(^|[;{}\\n])([ \\t]*)'+v+'\\s*=\\s*([^;\\n{}]+)','g'),(_,prefix,space)=>prefix+space+v+'=0');
    if(native.clock&&!new RegExp('\\b'+native.clock+'\\s*=').test(codeOnly(create)))patched=patched.replace(/(\*\/\s*\n)/,'$1'+native.clock+'=0;\n');
    write(controller,patched+rest);
  }
  // Preserve frame-based timing while the runner fast-forwards actual GM steps.
  // Other native code, surfaces, draw events, resources and rooms stay intact.
  for(const file of allFiles(project).filter(f=>f.endsWith('.gml')&&!/^(objRufNativePreview|rf_native_\w+)\.gml$/.test(path.basename(f)))){
    const source=fs.readFileSync(file,'utf8'),result=rewriteNativeRuntime(source,state);if(result!==source)write(file,result);
  }
  for(const file of fs.readdirSync(path.join(project,'scripts')).filter(name=>/^(killPlayer|scrKillPlayer)\.gml$/i.test(name))){const f=path.join(project,'scripts',file);write(f,"if (variable_global_exists('rf_god_mode') && global.rf_god_mode) return 0;\n"+(playable?guardPendingDeath('',true):'')+"if variable_global_exists('rf_native_preview') {if global.rf_native_preview {if instance_exists(objRufNativePreview) {if !objRufNativePreview.rf_playable || global.rf_native_frame<=objRufNativePreview.rf_target return 0;}}}\n"+fs.readFileSync(f,'utf8'));}
  installNativeMusicSync(project,state);
}
let building=null;
export async function prepareNativeSession({project,projectFile,buildDir,cacheDir,gameMaker,state,frame=0,playable=false,godMode=false}) {
  if(building)return building;
  building=(async()=>{
    const outputRoot=cacheDir||buildDir,base=path.join(outputRoot,playable?'native-play':'native-render'),source=path.join(base,'source'),exe=path.join(base,'rufmaker-native.exe'),marker=path.join(base,'stamp.json'),stamp=nativePreviewStamp(project);
    fs.mkdirSync(base,{recursive:true});let cached=false;
    if(fs.existsSync(exe)&&fs.existsSync(marker)){const old=JSON.parse(fs.readFileSync(marker,'utf8'));cached=old.stamp===stamp&&old.playable===playable&&old.generator===generator;}
    if(!cached){
      // This directory is exclusively generated by RufMaker; verify the target before replacing it.
      if(path.resolve(source)!==path.resolve(outputRoot,playable?'native-play':'native-render','source'))throw new Error('原生预览构建路径异常');
      fs.rmSync(source,{recursive:true,force:true});fs.cpSync(project,source,{recursive:true,filter:f=>{const rel=path.relative(path.toNamespacedPath(project),path.toNamespacedPath(f)),parts=rel.split(path.sep);return !parts.some(p=>['build','cache','.git'].includes(p))&&!/\.(log|tmp)$/i.test(f)&&!(parts.length===1&&/\.exe$/i.test(f));}});writeNativeHarness(source,state,{playable});
      const entry=path.join(source,path.basename(projectFile)),log=path.join(base,'compiler.log');write(log,gameMaker+'\n'+entry+'\n');
      await new Promise((resolve,reject)=>{const process=spawn(gameMaker,[entry,'--build',exe],{cwd:path.dirname(gameMaker),windowsHide:true,stdio:['ignore','pipe','pipe']});const timer=setTimeout(()=>{process.kill();reject(new Error('原生预览编译超时；请在 GameMaker 检查原工程。记录：'+log));},180000);for(const pipe of [process.stdout,process.stderr])pipe.on('data',x=>fs.appendFileSync(log,x));process.once('error',e=>{clearTimeout(timer);reject(e);});process.once('close',code=>{clearTimeout(timer);code===0?resolve():reject(new Error('原工程或预览接入代码编译失败。请查看 '+log+'；检查扩展包、缺失资源及原 GML 报错。'));});});
      if(!fs.existsSync(exe)||fs.statSync(exe).size<1024)throw new Error('GameMaker 没有生成原生预览程序');if(nativePreviewStamp(project)!==stamp)throw new Error('编译期间工程已修改，请重新打开原生渲染或再次试玩。');write(marker,JSON.stringify({stamp,playable,generator}));
    }
    const data=path.join(base,'Data');fs.cpSync(path.join(source,'Data'),data,{recursive:true,force:true});const commandFile=path.join(data,'rufmaker_native_command.ini'),statusFile=path.join(data,'rufmaker_native_status.ini'),frameFile=path.join(data,'rufmaker_native_frame.bmp');for(const file of [statusFile,frameFile,...['rufmaker_checkpoint.sav','rufmaker_checkpoint_restore','rufmaker_checkpoint_count.ini'].map(n=>path.join(data,n))])if(fs.existsSync(file))fs.unlinkSync(file);write(commandFile,commandText(frame,playable,playable,state.nativeTest||{},godMode));
    // Keep root-level plugin/config resources needed by older engines alongside the EXE.
    copyNativeExtras(project,base);
    return {exe,dir:base,commandFile,statusFile,frameFile,cached,width:state.nativeProject.width||800,height:state.nativeProject.height||608};
  })();try{return await building;}finally{building=null;}
}
