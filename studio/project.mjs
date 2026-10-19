import fs from 'node:fs';
import path from 'node:path';
import {normalizeState,stateCode,easeGml,bounceGml,fields} from "./motion.mjs";
import {effectTypes,fxResetGml,fxInitialization,fxCleanup,makeFxObjects} from './effects.mjs';
import crypto from 'node:crypto';
import {trailModes,trailMode,trailCall,trailGml} from './trails.mjs';
import { fileURLToPath } from 'node:url';

const sourceDir=path.dirname(fileURLToPath(import.meta.url));
export const root = path.resolve(process.env.RUFMAKER_ROOT || path.join(sourceDir,'..'));
export const project = path.join(root, 'projects', 'rufmaker-demo.gm82');
export const projectFile = path.join(project, 'rufmaker-demo.gm82');
export const makerFile = path.join(project, 'rufmaker.json');
const installedGameMaker = path.join(process.env.APPDATA || '', 'GameMaker8.2', 'GameMaker.exe');
const bundledGameMaker = path.join(root, 'GameMaker8.2', 'GameMaker.exe');
export const gameMaker = process.env.RUFMAKER_GAMEMAKER || (fs.existsSync(bundledGameMaker) ? bundledGameMaker : installedGameMaker);
export const pack = path.join(root, 'Ultimate Avoidance Sprite Pack');
export const buildDir = path.join(root, 'build');
const action = `/*"/*'/**//* YYD ACTION\nlib_id=1\naction_id=603\napplies_to=self\n*/`;

const defaultState = {
  version: 1,
  fps: 50,
  bpm: 120,
  duration: 1500,
  room: 'rAvoidance',
  music: 'Data/Music/BGM.ogg',
  tracks: [
    { id: 'demo-emitter', name: '环形弹幕', kind: 'emitter', enabled: true, sprite: 'sprBullet', start: 50, end: 550, interval: 25, count: 12, speed: 3, spread: 30, angle: 0, life: 180, easing: 'smooth', trail: true, color: '#ff5577', keyframes: [{ frame: 50, x: 400, y: 235, angle: 0, scale: 1, alpha: 1 }, { frame: 550, x: 400, y: 235, angle: 180, scale: 1, alpha: 1 }] },
    { id: 'demo-flash', name: '段落闪屏', kind: 'effect', enabled: true, effect: 'flash', start: 550, end: 560, strength: 1, color: '#ffffff', keyframes: [] }
  ]
};

export function readState() {
  if (!fs.existsSync(projectFile) || !fs.readFileSync(projectFile,'utf8').startsWith('gm82_version=')) throw new Error(`GameMaker 工程入口格式不正确：${projectFile}`);
  if (!fs.existsSync(makerFile)) {
    fs.writeFileSync(makerFile, JSON.stringify(defaultState, null, 2), 'utf8');
  }
  return normalizeState(JSON.parse(fs.readFileSync(makerFile, 'utf8')));
}

function safeName(s) {
  return typeof s === 'string' && /^[A-Za-z_][A-Za-z_0-9]*$/.test(s);
}

export function validateState(s) {
  if (!s || ![1,2].includes(s.version) || !Array.isArray(s.tracks) || s.tracks.length > 200 || !Number.isInteger(s.duration) || s.duration < 1 || s.duration > 1000000 || !Number.isInteger(s.fps) || s.fps < 15 || s.fps > 240) throw new Error('Invalid project state');
  if (!safeName(s.room) || !fs.existsSync(path.join(project,'rooms',s.room,'room.txt'))) throw new Error('Invalid room');
  for (const t of s.tracks) {
    if (!['emitter', 'object', 'rod', 'effect', 'script'].includes(t.kind) || !Array.isArray(t.keyframes) || t.keyframes.length > 10000 || !Number.isFinite(t.start) || !Number.isFinite(t.end) || t.start < 0 || t.end > s.duration || t.start > t.end) throw new Error('Invalid track');
    if (t.kind === 'emitter' && (!safeName(t.sprite) || !Number.isInteger(t.count) || t.count < 1 || t.count > 300 || !Number.isInteger(t.interval) || t.interval < 1 || !Number.isFinite(t.speed) || !Number.isFinite(t.life) || t.life < 1)) throw new Error('Invalid emitter');
    if (t.kind === 'object' && !safeName(t.sprite)) throw new Error('Invalid sprite');
    if(t.kind==='effect'&&!effectTypes.includes(t.effect))throw new Error('Invalid screen effect');
    for (const k of t.keyframes) if (!Number.isFinite(k.frame) || !fields.includes(k.property) || !Number.isFinite(k.value) || k.frame<0 || k.frame>s.duration) throw new Error('Invalid keyframe');
  }
}

function lines(file) { return fs.readFileSync(file, 'utf8').replace(/\r/g, '').split('\n'); }
function appendUnique(file, value) { const arr = lines(file); if (!arr.includes(value)) fs.appendFileSync(file, `${value}\n`, 'utf8'); }
function atomicWrite(file, value) { if (fs.existsSync(file) && fs.readFileSync(file,'utf8')===value) return; const temp = `${file}.${crypto.randomBytes(4).toString('hex')}.tmp`; fs.writeFileSync(temp, value, 'utf8'); fs.renameSync(temp, file); }
function gnum(n, fallback = 0) { return Number.isFinite(Number(n)) ? String(Number(n)) : String(fallback); }
function gcolor(hex) { const v = /^#[0-9a-f]{6}$/i.test(hex || '') ? hex.slice(1) : 'ffffff'; return `make_color_rgb(${parseInt(v.slice(0,2),16)},${parseInt(v.slice(2,4),16)},${parseInt(v.slice(4,6),16)})`; }

const stateAt=stateCode;

function bulletSpawn(track, fromHistory) {
  const time = fromHistory ? 'rf_i' : 'rf_frame';
  const age = fromHistory ? '(rf_frame-rf_i)' : '0';
  const pieces = [];
  pieces.push(stateAt(track,time));
  pieces.push(`if (${age}<rf_life) { rf_count=max(1,min(300,round(rf_count))); for (rf_j=0;rf_j<rf_count;rf_j+=1) {`);
  pieces.push(`rf_dir=rf_angle+(rf_j-(rf_count-1)/2)*rf_spread;`);
  pieces.push(`rf_inst=instance_create(rf_x+lengthdir_x(rf_speed*${age},rf_dir),rf_y+lengthdir_y(rf_speed*${age},rf_dir),objRufBullet);`);
  pieces.push(`rf_inst.rf_age=${age}; rf_inst.rf_life=rf_life; rf_inst.rf_speed=rf_speed; rf_inst.rf_dir=rf_dir;`);
  pieces.push(`rf_inst.sprite_index=${track.sprite}; rf_inst.image_blend=${gcolor(track.color)}; rf_inst.image_xscale=rf_scale; rf_inst.image_yscale=rf_scale; rf_inst.image_alpha=rf_alpha; rf_inst.rf_trail=${track.trail ? 1 : 0};`);
  pieces.push(`rf_inst.image_angle=rf_spriteAngle+${track.rotateSprite?'rf_dir':'0'};rf_inst.rf_rotate=${track.rotateSprite?1:0};rf_inst.rf_sprite_angle=rf_spriteAngle;rf_inst.rf_birth=${time};rf_inst.rf_index=rf_j;`);
  pieces.push(`rf_inst.rf_t_mode='${trailMode(track)}';rf_inst.rf_t_interval=${gnum(track.trailInterval,3)};rf_inst.rf_t_life=${gnum(track.trailLife,18)};rf_inst.rf_t_alpha=${gnum(track.trailStartAlpha,.55)};rf_inst.rf_t_scale_decay=${gnum(track.trailScaleDecay,.035)};rf_inst.rf_t_alpha_decay=${gnum(track.trailAlphaDecay,.035)};rf_inst.rf_t_speed=${gnum(track.trailSpeed)};rf_inst.rf_t_direction=${gnum(track.trailDirection)};rf_inst.rf_t_random=${track.trailRandom?1:0};`);
  pieces.push('} }');
  return pieces.join('\n');
}

function rodUpdate(t,index) {
  const prefix=`rf_rod${index}`,parts=[`if (rf_frame>=${gnum(t.start)} && rf_frame<=${gnum(t.end)}) {`,stateAt(t,'rf_frame')];
  if(t.rodMode==='center')parts.push('rf_x1=rf_x-lengthdir_x(rf_length/2,rf_angle);rf_y1=rf_y-lengthdir_y(rf_length/2,rf_angle);rf_x2=rf_x+lengthdir_x(rf_length/2,rf_angle);rf_y2=rf_y+lengthdir_y(rf_length/2,rf_angle);');
  else if(t.rodMode==='anchor')parts.push('rf_x1=rf_x;rf_y1=rf_y;');
  parts.push(`rf_len=point_distance(rf_x1,rf_y1,rf_x2,rf_y2);rf_phase=0;rf_count=max(1,min(300,round(rf_count)));`);
  if(t.rolling)parts.push(`if rf_len>.001 rf_phase=(rf_frame-${gnum(t.start)})*rf_rollSpeed/rf_len;`);
  parts.push(`for(rf_j=0;rf_j<max(rf_count,${prefix}_count);rf_j+=1) {`);
  parts.push(`if rf_j>=rf_count {if instance_exists(${prefix}[rf_j]) with (${prefix}[rf_j]) instance_destroy();${prefix}[rf_j]=noone;} else {`);
  parts.push(`if !instance_exists(${prefix}[rf_j]) {${prefix}[rf_j]=instance_create(0,0,objRufBullet);${prefix}[rf_j].rf_fixed=1;}rf_inst=${prefix}[rf_j];`);
  parts.push(t.rolling?'rf_fraction=rf_j/rf_count+rf_phase;rf_fraction-=floor(rf_fraction);':'if rf_count=1 rf_fraction=.5;else rf_fraction=rf_j/(rf_count-1);');
  parts.push(`rf_inst.x=rf_x1+(rf_x2-rf_x1)*rf_fraction;rf_inst.y=rf_y1+(rf_y2-rf_y1)*rf_fraction;rf_inst.image_angle=rf_spriteAngle+${t.rotateSprite?'rf_angle':'0'};rf_inst.sprite_index=${t.sprite};rf_inst.image_blend=${gcolor(t.color)};rf_inst.image_xscale=rf_scale;rf_inst.image_yscale=rf_scale;rf_inst.image_alpha=rf_alpha;rf_inst.rf_trail=${t.trail?1:0};`);
  parts.push(`}} ${prefix}_count=rf_count; } else if ${prefix}_count>0 {for(rf_j=0;rf_j<${prefix}_count;rf_j+=1) {if instance_exists(${prefix}[rf_j]) with (${prefix}[rf_j]) instance_destroy();${prefix}[rf_j]=noone;}${prefix}_count=0;}`);
  return parts.join('\n');
}


function spatialTrails(t) {
  if(trailMode(t)==='off')return '';
  const interval=Math.max(1,t.trailInterval),life=Math.max(1,Math.min(300,t.trailLife));
  const parts=[`if rf_frame>=${gnum(t.start)} && rf_frame<=${gnum(t.end)} {for(rf_sample=floor(rf_frame/${interval})*${interval};rf_sample>=max(${gnum(t.start)},rf_frame-${life}+1);rf_sample-=${interval}) {`,stateAt(t,'rf_sample')];
  if(t.kind==='object')parts.push(trailCall(t,{color:gcolor(t.color),age:'rf_frame-rf_sample',seed:'rf_sample'}));
  else {
    if(t.rodMode==='center')parts.push('rf_x1=rf_x-lengthdir_x(rf_length/2,rf_angle);rf_y1=rf_y-lengthdir_y(rf_length/2,rf_angle);rf_x2=rf_x+lengthdir_x(rf_length/2,rf_angle);rf_y2=rf_y+lengthdir_y(rf_length/2,rf_angle);');
    else if(t.rodMode==='anchor')parts.push('rf_x1=rf_x;rf_y1=rf_y;');
    parts.push('rf_len=point_distance(rf_x1,rf_y1,rf_x2,rf_y2);rf_count=max(1,min(300,round(rf_count)));rf_phase=0;');
    if(t.rolling)parts.push(`if rf_len>.001 rf_phase=(rf_sample-${gnum(t.start)})*rf_rollSpeed/rf_len;`);
    parts.push('for(rf_j=0;rf_j<rf_count;rf_j+=1){');
    parts.push(t.rolling?'rf_fraction=rf_j/rf_count+rf_phase;rf_fraction-=floor(rf_fraction);':'if rf_count=1 rf_fraction=.5;else rf_fraction=rf_j/(rf_count-1);');
    parts.push(trailCall(t,{x:'rf_x1+(rf_x2-rf_x1)*rf_fraction',y:'rf_y1+(rf_y2-rf_y1)*rf_fraction',angle:`rf_spriteAngle+${t.rotateSprite?'rf_angle':'0'}`,color:gcolor(t.color),age:'rf_frame-rf_sample',seed:'rf_sample*301+rf_j'}),'}');
  }
  parts.push('}}');return parts.join('\n');
}

function makeController(s) {
  const create = [`#define Create_0`,action,`rf_frame=0; rf_target=0; rf_preview=0; rf_ready=0; rf_music=FMODSoundPlay(BGM_Ad,1); global.BGMHandler=rf_music;`,
    `if file_exists(working_directory+'\\Data\\rufmaker_preview.ini') { ini_open(working_directory+'\\Data\\rufmaker_preview.ini'); rf_target=ini_read_real('preview','frame',0); ini_close(); rf_preview=1; }`,
    `rf_frame=max(0,min(${gnum(s.duration)},round(rf_target))); rf_target=rf_frame;`,
    `FMODInstanceSetPosition(rf_music,(rf_frame/${gnum(s.fps)}*1000)/FMODSoundGetLength(BGM_Ad)); FMODInstanceSetPaused(rf_music,0);`,
    `var rf_i,rf_j,rf_u,rf_dir,rf_inst,rf_len,rf_phase,rf_fraction,${fields.map(f=>'rf_'+f).join(',')};`, `rf_custom_create();`,fxInitialization(s)];
  const step = [`#define Step_0`,action,`rf_frame+=1;`, `if rf_frame>${gnum(s.duration)} { room_goto(rEnd); exit; }`,
    `var rf_i,rf_j,rf_u,rf_dir,rf_inst,rf_len,rf_phase,rf_fraction,${fields.map(f=>'rf_'+f).join(',')};`, `rf_custom_step();`];
  const draw = [`#define Draw_0`,action,`var rf_sample,rf_len,rf_phase,rf_fraction,rf_i,rf_j,rf_u,rf_dir,rf_inst,${fields.map(f=>'rf_'+f).join(',')};`, `rf_custom_draw();`];
  let rodIndex=0;
  for (const t of s.tracks.filter(x=>x.enabled!==false)) {
    if(t.kind==='rod'||t.kind==='object')draw.push(spatialTrails(t));
    if (t.kind==='emitter') {
      const maxLife=Math.max(t.life||120,...t.keyframes.filter(k=>k.property==='life').map(k=>k.value));
      create.push(`// RufMaker emitter\nfor (rf_i=${gnum(t.start)}+max(0,ceil((rf_frame-${maxLife}+1-${gnum(t.start)})/${gnum(t.interval,1)}))*${gnum(t.interval,1)};rf_i<=min(${gnum(t.end)},rf_frame);rf_i+=${gnum(t.interval,1)}) { ${bulletSpawn(t,true)} }`);
      step.push(`if (rf_frame>=${gnum(t.start)} && rf_frame<=${gnum(t.end)} && (rf_frame-${gnum(t.start)}) mod ${gnum(t.interval,1)}=0) { ${bulletSpawn(t,false)} }`);
    } else if(t.kind==='rod') {
      const prefix=`rf_rod${rodIndex}`;
      create.push(`${prefix}_count=0;for(rf_j=0;rf_j<300;rf_j+=1) ${prefix}[rf_j]=noone;`,rodUpdate(t,rodIndex));
      step.push(rodUpdate(t,rodIndex++));
    } else if (t.kind==='object') {
      draw.push(`if (rf_frame>=${gnum(t.start)} && rf_frame<=${gnum(t.end)}) { ${stateAt(t,'rf_frame')} draw_sprite_ext(${t.sprite},0,rf_x,rf_y,rf_scale,rf_scale,rf_angle,${gcolor(t.color)},rf_alpha); }`);
    } else if (t.kind==='effect') {
      // Screen effects are evaluated every Draw, including native preview started mid-track.
    } else if (t.kind==='script') {
      step.push(`if rf_frame=${gnum(t.start)} {\n${t.code || ''}\n}`);
    }
  }
  step.push(`if rf_preview && !rf_ready { rf_ready=1; ini_open(working_directory+'\\Data\\rufmaker_preview_status.ini'); ini_write_real('preview','frame',rf_target); ini_write_real('preview','current_frame',rf_frame); ini_write_real('preview','fps',room_speed); ini_write_string('preview','room',room_get_name(room)); ini_write_real('preview','bullets',instance_number(objRufBullet)); ini_write_real('preview','players',instance_number(player)); ini_close(); }`);
  step.push('FMODUpdate();');
  const destroy=[`#define Destroy_0`,action,`if FMODInstanceIsPlaying(rf_music) FMODInstanceStop(rf_music);`,fxCleanup(s)];
  const roomStart=[`#define Other_4`,action,`room_speed=${gnum(s.fps)};`];
  return [create.join('\n'),step.join('\n'),draw.join('\n'),destroy.join('\n'),roomStart.join('\n')].join('\n')+'\n';
}

const bulletGml = `#define Create_0
${action}
rf_age=0;rf_life=120;rf_speed=0;rf_dir=0;rf_trail=0;rf_fixed=0;rf_rotate=0;rf_sprite_angle=0;rf_birth=0;rf_index=0;
rf_t_mode='off';rf_t_interval=3;rf_t_life=18;rf_t_alpha=.55;rf_t_scale_decay=.035;rf_t_alpha_decay=.035;rf_t_speed=0;rf_t_direction=0;rf_t_random=0;
#define Step_0
${action}
rf_custom_bullet();if rf_fixed exit;
rf_age+=1;x+=lengthdir_x(rf_speed,rf_dir);y+=lengthdir_y(rf_speed,rf_dir);if rf_rotate image_angle=rf_sprite_angle+rf_dir;
if rf_age>=rf_life || x<-80 || x>room_width+80 || y<-80 || y>room_height+80 instance_destroy();
#define Draw_0
${action}
var rf_sample,rf_tail,rf_drift,rf_seed;
if !rf_fixed && rf_t_mode!='off' {
for(rf_sample=floor(rf_age/max(1,rf_t_interval))*max(1,rf_t_interval);rf_sample>=max(0,rf_age-rf_t_life+1);rf_sample-=max(1,rf_t_interval)) {
rf_tail=rf_age-rf_sample;rf_seed=(rf_birth+rf_sample)*301+rf_index;
rf_drift=rf_t_direction;if rf_t_random rf_drift=(sin((rf_seed+1)*12.9898)*43758.5453-floor(sin((rf_seed+1)*12.9898)*43758.5453))*360;
rf_trail_draw(sprite_index,x-lengthdir_x(rf_speed*rf_tail,rf_dir),y-lengthdir_y(rf_speed*rf_tail,rf_dir),image_xscale,image_angle,image_alpha,image_blend,rf_tail,rf_t_mode,rf_t_life,rf_t_alpha,rf_t_scale_decay,rf_t_alpha_decay,rf_t_speed,rf_drift);
}}
draw_sprite_ext(sprite_index,image_index,x,y,image_xscale,image_yscale,image_angle,image_blend,image_alpha);
`;

export function syncToGame(s) {
  if (!fs.existsSync(projectFile) || !fs.readFileSync(projectFile,'utf8').startsWith('gm82_version=')) throw new Error(`GameMaker 工程入口格式不正确：${projectFile}`);
  normalizeState(s);
  validateState(s);
  for (const [name,code] of [['rf_ease',easeGml],['rf_bounce',bounceGml],['rf_fx_reset_target',fxResetGml],['rf_trail_draw',trailGml]]) {atomicWrite(path.join(project,'scripts',`${name}.gml`),code);appendUnique(path.join(project,'scripts','index.yyd'),name);appendUnique(path.join(project,'scripts','tree.yyd'),`	|${name}`);}
  const objects = path.join(project,'objects');
  const backgroundFile=path.join(objects,'objBackground.gml');
  if(fs.existsSync(backgroundFile)){const source=fs.readFileSync(backgroundFile,'utf8');atomicWrite(backgroundFile,source.replace(/\bsurface_reset_target\s*\(\s*\)/g,'rf_fx_reset_target()'));}
  for (const name of ['rf_custom_create','rf_custom_step','rf_custom_draw','rf_custom_bullet']) {
    const file=path.join(project,'scripts',`${name}.gml`);
    if(!fs.existsSync(file)) atomicWrite(file,`/// ${name}: handwritten GameMaker GML hook\nreturn 0;\n`);
    appendUnique(path.join(project,'scripts','index.yyd'),name);
    appendUnique(path.join(project,'scripts','tree.yyd'),`\t|${name}`);
  }
  atomicWrite(path.join(project,'scripts','rf_preview_boot.gml'),`// Generated by RufMaker: enter the timeline room for native preview.\nif file_exists(working_directory+'\\Data\\rufmaker_preview.ini') { room_goto(${s.room}); return 1; }\nreturn 0;\n`);
  appendUnique(path.join(project,'scripts','index.yyd'),'rf_preview_boot');
  appendUnique(path.join(project,'scripts','tree.yyd'),'\t|rf_preview_boot');
  const initFile=path.join(objects,'init.gml');
  const initCode=fs.readFileSync(initFile,'utf8');
  if (!initCode.includes('if (rf_preview_boot()) exit;')) {
    const patched=initCode.replace(/(#define Alarm_0\r?\n\/\*[\s\S]*?\*\/\r?\n)/,'$1if (rf_preview_boot()) exit;\n');
    if (patched===initCode) throw new Error('找不到引擎初始化事件，无法接入原生试玩');
    atomicWrite(initFile,patched);
  }
  for (const [name,info,code] of [
    ['objRufController','sprite=\nvisible=1\nsolid=0\npersistent=0\ndepth=-7000000\nparent=\nmask=\n',makeController(s)],
    ['objRufBullet','sprite=sprBullet\nvisible=1\nsolid=0\npersistent=0\ndepth=-100\nparent=playerKiller\nmask=\n',bulletGml],
    ...makeFxObjects(s)
  ]) {
    atomicWrite(path.join(objects,`${name}.txt`),info);
    atomicWrite(path.join(objects,`${name}.gml`),code);
    appendUnique(path.join(objects,'index.yyd'),name);
    appendUnique(path.join(objects,'tree.yyd'),`\t|${name}`);
  }
  for (const entry of fs.readdirSync(path.join(project,'rooms'),{withFileTypes:true})) {
    if (!entry.isDirectory()) continue;
    const instFile=path.join(project,'rooms',entry.name,'instances.txt');
    if (!fs.existsSync(instFile)) continue;
    const original=lines(instFile).filter(Boolean);
    const inst=original.filter(x=>!x.startsWith('objRufController,') && (entry.name!==s.room || !x.startsWith('objAd,')));
    if (entry.name===s.room) inst.push('objRufController,32,32,00F0F0F0,0,1,1,4294967295,0,0');
    if (inst.join('\n')!==original.join('\n')) atomicWrite(instFile,inst.join('\n')+'\n');
  }
  const roomSettings=path.join(project,'rooms',s.room,'room.txt');
  const settings=fs.readFileSync(roomSettings,'utf8');
  const updated=settings.replace(/^roomspeed=\d+$/m,`roomspeed=${s.fps}`);
  if (updated!==settings) atomicWrite(roomSettings,updated);
  atomicWrite(makerFile,JSON.stringify(s,null,2)+'\n');
}

export function roomInfo(room='rAvoidance') {
  if (!safeName(room)) throw new Error('Invalid room');
  const rfile=path.join(project,'rooms',room,'room.txt');
  const settings=Object.fromEntries(lines(rfile).filter(x=>x.includes('=')).map(x=>x.split(/=(.*)/s).slice(0,2)));
  const instances=lines(path.join(project,'rooms',room,'instances.txt')).filter(Boolean).map((line,index)=>{ const a=line.split(','); return { index,id:a[3],name:a[0],x:Number(a[1]),y:Number(a[2]),raw:line }; });
  return { name:room,width:Number(settings.width),height:Number(settings.height),fps:Number(settings.roomspeed),instances };
}

export function inventory() {
  const dirs={ scripts:'scripts',sprites:'sprites',objects:'objects',rooms:'rooms' };
  const out={};
  for (const [key,dir] of Object.entries(dirs)) {
    const list=fs.readdirSync(path.join(project,dir),{withFileTypes:true});
    out[key]=list.filter(x=> key==='sprites'||key==='rooms' ? x.isDirectory() : x.isFile() && x.name.endsWith(key==='scripts'?'.gml':'.txt')).map(x=>key==='sprites'||key==='rooms'?x.name:path.parse(x.name).name).sort((a,b)=>a.localeCompare(b));
  }
  out.objectSprites={};
  for (const name of out.objects) {
    const f=path.join(project,'objects',`${name}.txt`);
    const line=lines(f).find(x=>x.startsWith('sprite='));
    out.objectSprites[name]=line?.slice(7) || '';
  }
  out.pack=(fs.existsSync(pack)?fs.readdirSync(pack,{withFileTypes:true}):[]).filter(x=>x.isDirectory()).map(x=>x.name);
  return out;
}

export function sourcePath(kind,name) {
  if (!safeName(name) || !['scripts','objects','rooms'].includes(kind)) throw new Error('Invalid resource');
  if (kind==='objects' && ['objRufController','objRufBullet','objRufFxBegin','objRufFxEnd'].includes(name)) throw new Error('Generated object: edit its timeline track instead');
  const p=kind==='rooms'?path.join(project,kind,name,'code.gml'):path.join(project,kind,`${name}.gml`);
  if (!fs.existsSync(p)) throw new Error('Resource not found');
  return p;
}

export function moveRoomInstance(room,index,x,y) {
  if (!safeName(room) || !Number.isInteger(index) || !Number.isFinite(x) || !Number.isFinite(y)) throw new Error('Invalid instance');
  const f=path.join(project,'rooms',room,'instances.txt');
  const ls=lines(f).filter(Boolean);
  if (!ls[index]) throw new Error('Instance not found');
  const a=ls[index].split(','); a[1]=String(Math.round(x)); a[2]=String(Math.round(y)); ls[index]=a.join(',');
  atomicWrite(f,ls.join('\n')+'\n');
}

export function spriteFile(name) {
  if (!safeName(name)) throw new Error('Invalid sprite');
  const p=path.join(project,'sprites',name,'0.png');
  if (!fs.existsSync(p)) throw new Error('Sprite not found');
  return p;
}

export function packFiles(folder) {
  if (!fs.readdirSync(pack).includes(folder)) throw new Error('Invalid folder');
  const base=path.join(pack,folder);
  return fs.readdirSync(base,{withFileTypes:true}).filter(x=>x.isFile()&&x.name.toLowerCase().endsWith('.png')).slice(0,500).map(x=>x.name);
}

export function packFile(folder,name) {
  if (!fs.readdirSync(pack).includes(folder) || path.basename(name)!==name || !name.toLowerCase().endsWith('.png')) throw new Error('Invalid asset');
  const p=path.join(pack,folder,name); if(!fs.existsSync(p)) throw new Error('Asset not found'); return p;
}

export function importSprite(folder,name) {
  const from=packFile(folder,name);
  let base='rf_'+path.parse(name).name.replace(/[^A-Za-z0-9_]/g,'_').slice(0,42);
  if (!safeName(base)) base='rf_sprite';
  let out=base, n=2;
  while(fs.existsSync(path.join(project,'sprites',out))) out=`${base}_${n++}`;
  const dir=path.join(project,'sprites',out); fs.mkdirSync(dir);
  fs.copyFileSync(from,path.join(dir,'0.png'));
  const png=fs.readFileSync(from); const width=png.readUInt32BE(16),height=png.readUInt32BE(20);
  atomicWrite(path.join(dir,'sprite.txt'),`frames=1\norigin_x=${Math.floor(width/2)}\norigin_y=${Math.floor(height/2)}\ncollision_shape=0\nalpha_tolerance=0\nper_frame_colliders=0\nbbox_type=0\nbbox_left=0\nbbox_top=0\nbbox_right=${width-1}\nbbox_bottom=${height-1}\n`);
  appendUnique(path.join(project,'sprites','index.yyd'),out);
  appendUnique(path.join(project,'sprites','tree.yyd'),`\t|${out}`);
  return out;
}
