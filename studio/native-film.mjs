import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {spawn} from 'node:child_process';
import {codeOnly} from './native-import.mjs';
import {writeNativeHarness,copyNativeExtras} from './native-preview.mjs';

const action=`/*"/*'/**//* YYD ACTION\nlib_id=1\naction_id=603\napplies_to=self\n*/`;
const section=(name,code)=>`#define ${name}\n${action}\n${code}\n`;
const write=(file,text)=>fs.writeFileSync(file,text,'utf8');
function append(file,line){const text=fs.readFileSync(file,'utf8');if(!text.split(/\r?\n/).includes(line))fs.appendFileSync(file,'\n'+line+'\n');}
const generated=new Set(['objRufController','objRufBullet','objRufTrails','objRufFxBegin','objRufFxEnd']);
export function filmCachePath(project){return path.join(project,'cache','native-film','base.rufvideo');}
export function fixedPlayerReads(source,players){
  const clean=codeOnly(source).replace(/(['"])[\s\S]*?\1/g,m=>' '.repeat(m.length)),edits=[];
  const names=players.map(n=>n.replace(/[.*+?^${}()|[\]\\]/g,'\\$&')).join('|');if(!names)return source;
  for(const m of clean.matchAll(new RegExp('\\b(?:'+names+')\\s*\\.\\s*([xy])\\b','g'))){
    const before=clean.slice(0,m.index),after=clean.slice(m.index+m[0].length);
    if(/\.\s*$/.test(before)||/^\s*(?:=(?!=)|(?:<<|>>|[+\-*/%&|^])=|\+\+|--)/.test(after)||/(?:\+\+|--)\s*$/.test(before))continue;
    edits.push([m.index,m[0].length,m[1]==='x'?'400':'304']);
  }
  let result=source;for(const [at,len,value] of edits.reverse())result=result.slice(0,at)+value+result.slice(at+len);return result;
}
export function pinNativeEvents(source,player=false){
  return source.split(/(?=^#define\s)/m).map(part=>{
    const header=/^#define\s+(\S+)[^\r\n]*\r?\n/.exec(part);if(!header)return part;
    if(player&&/^(?:Step_|Keyboard_|KeyPress_|KeyRelease_|Collision_)/.test(header[1]))return section(header[1],'rf_film_pin();');
    return header[0]+action+'\nrf_film_pin();\n'+part.slice(header[0].length);
  }).join('');
}
export function makeFilmHarness(state){
  return section('Create_0',`window_set_visible(0);global.rf_native_preview=1;global.rf_native_frame=0;rf_playable=0;rf_playing=1;rf_target=${state.nativeProject.duration};rf_entered=0;rf_boot=0;random_set_seed(1234567);`)+
    section('Other_4',`if room=${state.room} && !rf_entered {rf_entered=1;global.rf_native_frame=0;}room_speed=50;rf_film_pin();`)+
    section('Step_1',`window_set_visible(0);sound_global_volume(0);room_speed=50;rf_film_pin();
if !rf_entered {rf_boot+=1;if rf_boot=3 {${state.nativeProject.musicInitializer||''}room_goto(${state.room});}exit;}
if global.rf_native_frame=0 screen_redraw();
screen_save(working_directory+'\\Data\\rufmaker_film_frame.bmp');
rf_film_status(0);
while !file_exists(working_directory+'\\Data\\rufmaker_film_ack\\'+string(global.rf_native_frame)+'.ok') {sleep(1);io_handle();}
if global.rf_native_frame>=${state.nativeProject.duration} {rf_film_status(1);game_end();exit;}
global.rf_native_frame+=1;`);
}
export function writeFilmHarness(project,state){
  writeNativeHarness(project,state);
  const objects=path.join(project,'objects'),scripts=path.join(project,'scripts');
  const players=fs.readdirSync(objects).filter(n=>/^(?:(?:obj_?|o)?player[12]?)\.txt$/i.test(n)).map(n=>n.slice(0,-4));
  if(!players.length)throw new Error('未识别 player 对象，不能固定玩家坐标；请检查原生工程的玩家对象名称。');
  for(const name of fs.readdirSync(objects).filter(n=>n.endsWith('.gml'))){
    const object=name.slice(0,-4);if(object==='objRufNativePreview'||generated.has(object))continue;
    const file=path.join(objects,name);write(file,pinNativeEvents(fixedPlayerReads(fs.readFileSync(file,'utf8'),players),players.includes(object)));
  }
  for(const name of fs.readdirSync(scripts).filter(n=>n.endsWith('.gml')&&!n.startsWith('rf_'))){const file=path.join(scripts,name),source=fixedPlayerReads(fs.readFileSync(file,'utf8'),players);write(file,'rf_film_pin();\n'+(/^(killPlayer|scrKillPlayer)\.gml$/i.test(name)?'return 0;\n':'')+source);}
  // Native timeline and room creation code can also aim at the player.
  for(const folder of ['timelines','rooms']){const visit=dir=>{if(!fs.existsSync(dir))return;for(const e of fs.readdirSync(dir,{withFileTypes:true})){const file=path.join(dir,e.name);if(e.isDirectory())visit(file);else if(e.name.endsWith('.gml'))write(file,fixedPlayerReads(fs.readFileSync(file,'utf8'),players));}};visit(path.join(project,folder));}
  for(const room of fs.readdirSync(path.join(project,'rooms'),{withFileTypes:true}).filter(e=>e.isDirectory())){const file=path.join(project,'rooms',room.name,'instances.txt');if(fs.existsSync(file))write(file,fs.readFileSync(file,'utf8').split(/\r?\n/).filter(line=>!generated.has(line.split(',')[0])).join('\n'));}
  const pin=players.map(name=>`with(${name}) {x=400;y=304;hspeed=0;vspeed=0;gravity=0;wudi=1;muteki=1;}`).join('\n');
  const additions={rf_film_pin:pin+'\nreturn 0;',rf_film_status:`var f;f=file_text_open_write(working_directory+'\\Data\\rufmaker_film_status.txt');file_text_write_string(f,'frame='+string(global.rf_native_frame));file_text_writeln(f);file_text_write_string(f,'done='+string(argument0));file_text_writeln(f);file_text_write_string(f,'room='+room_get_name(room));file_text_writeln(f);file_text_close(f);return 0;`};
  for(const [name,code] of Object.entries(additions)){write(path.join(scripts,name+'.gml'),code+'\n');append(path.join(scripts,'index.yyd'),name);append(path.join(scripts,'tree.yyd'),'\t|'+name);}
  if(state.nativeProject.hasFmod)write(path.join(scripts,'rf_native_volume.gml'),'return FMODInstanceSetVolume(argument0,0);\n');
  // Do not let the user's keyboard affect a background recording.
  const input=/\b(?:keyboard_check(?:_pressed|_released|_direct)?|keyboard_get_numlock|keyboard_set_numlock|mouse_check_button(?:_pressed|_released)?)\s*\(/g;
  const patchInputs=dir=>{for(const e of fs.readdirSync(dir,{withFileTypes:true})){const file=path.join(dir,e.name);if(e.isDirectory())patchInputs(file);else if(e.name.endsWith('.gml')&&!e.name.startsWith('rf_')){const source=fs.readFileSync(file,'utf8'),clean=codeOnly(source).replace(/(['"])[\s\S]*?\1/g,m=>' '.repeat(m.length)),edits=[...clean.matchAll(input)];let result=source;for(const m of edits.reverse())result=result.slice(0,m.index)+'rf_film_input('+result.slice(m.index+m[0].length);if(result!==source)write(file,result);}}};patchInputs(objects);patchInputs(scripts);
  write(path.join(scripts,'rf_film_input.gml'),'return 0;\n');append(path.join(scripts,'index.yyd'),'rf_film_input');append(path.join(scripts,'tree.yyd'),'\t|rf_film_input');
  write(path.join(objects,'objRufNativePreview.gml'),makeFilmHarness(state));
  const settings=path.join(project,'settings','settings.txt');if(fs.existsSync(settings))write(settings,fs.readFileSync(settings,'utf8').replace(/^show_error_messages=\d+/m,'show_error_messages=0').replace(/^log_errors=\d+/m,'log_errors=1').replace(/^always_abort=\d+/m,'always_abort=1'));
  return players;
}
export async function prepareNativeFilm({root,project,projectFile,gameMaker,state}){
  if(!state.nativeProject?.imported)throw new Error('当前不是外部导入的原生 GM 工程');
  const key=crypto.createHash('sha256').update(project.toLowerCase()).digest('hex').slice(0,16),base=path.join(root,'build','native-projects',key,'native-film'),source=path.join(base,'source');
  if(path.resolve(source)!==path.resolve(root,'build','native-projects',key,'native-film','source'))throw new Error('底片工作目录异常');
  fs.mkdirSync(base,{recursive:true});fs.rmSync(source,{recursive:true,force:true});
  fs.cpSync(project,source,{recursive:true,filter:f=>{const rel=path.relative(path.toNamespacedPath(project),path.toNamespacedPath(f)),parts=rel.split(path.sep);return !parts.some(p=>['cache','build','.git'].includes(p))&&!/\.(log|tmp)$/i.test(f)&&!(parts.length===1&&/\.exe$/i.test(f));}});
  const players=writeFilmHarness(source,state),exe=path.join(base,'rufmaker-film.exe'),entry=path.join(source,path.basename(projectFile)),log=path.join(base,'compiler.log');write(log,entry+'\n');
  await new Promise((resolve,reject)=>{const child=spawn(gameMaker,[entry,'--build',exe],{cwd:path.dirname(gameMaker),windowsHide:true,stdio:['ignore','pipe','pipe']});const timer=setTimeout(()=>{child.kill();reject(new Error('底片录制程序编译超时：'+log));},180000);for(const stream of [child.stdout,child.stderr])stream.on('data',x=>fs.appendFileSync(log,x));child.once('error',e=>{clearTimeout(timer);reject(e);});child.once('close',code=>{clearTimeout(timer);code===0?resolve():reject(new Error('底片录制程序编译失败：'+log));});});
  if(!fs.existsSync(exe))throw new Error('未生成底片录制程序：'+log);
  fs.cpSync(path.join(source,'Data'),path.join(base,'Data'),{recursive:true,force:true});copyNativeExtras(project,base);
  const data=path.join(base,'Data');for(const file of ['rufmaker_film_status.txt','rufmaker_film_frame.bmp','rufmaker_film_ack.txt']){const target=path.join(data,file);if(fs.existsSync(target))fs.unlinkSync(target);}
  const roomInstances=fs.readFileSync(path.join(project,'rooms',state.room,'instances.txt'),'utf8').split(/\r?\n/).filter(Boolean).map(line=>line.split(',')[3]);
  return {exe,dir:base,statusFile:path.join(data,'rufmaker_film_status.txt'),frameFile:path.join(data,'rufmaker_film_frame.bmp'),ackDirectory:path.join(data,'rufmaker_film_ack'),output:filmCachePath(project),fps:50,width:state.nativeProject.width,height:state.nativeProject.height,frameCount:state.nativeProject.duration+1,room:state.room,playerX:400,playerY:304,nativeIds:roomInstances,players};
}
