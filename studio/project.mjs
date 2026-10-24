import fs from 'node:fs';
import {gmlNumber as gnum} from './gml.mjs';
import {locateProject} from './project-files.mjs';
import {resolveGameMaker} from './engine-path.mjs';
import {parameterCode as parameter,randomInitialization,randomNextGml,expressionScripts,isDynamic} from './random-parameters.mjs';
import {cameraPostGml,styleFxGml} from './style-effects.mjs';
import {bulletFlightGml,flightSpawn} from './bullet-flight.mjs';
import {advancedBullets,expandPatterns,activeCode} from './bullet-schema.mjs';
import {bulletSystemScripts} from './bullet-system.mjs';
import {templateScripts,templateObject} from './template-system.mjs';
import {preparePlainRoom} from './plain-room.mjs';
import {shapeSetup,shapePoint,formationScripts} from './shapes.mjs';
import {cameraApplyGml,cameraCompositeGml,cameraUpdate} from './camera.mjs';
import path from 'node:path';
import {normalizeState,stateCode,easeGml,bounceGml,fields} from "./motion.mjs";
import {effectTypes,fxResetGml,fxOpaqueGml,fxInitialization,fxCleanup,makeFxObjects} from './effects.mjs';
import crypto from 'node:crypto';
import {rainbowCode,rainbowGml,rodSequence} from './rainbow.mjs';
import {trailModes,trailMode,trailCall,trailGml} from './trails.mjs';
import {nativeComposeGml,nativeSurfaceResetGml,patchNativeSurfaceReset} from './native-compositor.mjs';
import { fileURLToPath } from 'node:url';

const sourceDir=path.dirname(fileURLToPath(import.meta.url));
export const root = path.resolve(process.env.RUFMAKER_ROOT || path.join(sourceDir,'..'));
const location=locateProject(root,process.env.RUFMAKER_PROJECT);
export const {project,projectFile,descriptor,home,buildDir,gameExe}=location;
export const projectName=location.name;
export const makerFile = path.join(project, 'rufmaker.json');
export const gameMaker = resolveGameMaker(root);
export const pack = path.join(root, 'assets','sprite-packs','Ultimate Avoidance Sprite Pack');

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
    if (!['emitter', 'object', 'rod', 'effect', 'script', 'camera','native','group','control','pattern'].includes(t.kind) || !Array.isArray(t.keyframes) || t.keyframes.length > 10000 || !Number.isFinite(t.start) || !Number.isFinite(t.end) || t.start < 0 || t.end > s.duration || t.start > t.end) throw new Error('Invalid track');
    if(t.kind==='native'&&(!s.nativeProject?.imported||t.id!=='gm-native'))throw new Error('原生轨道必须来自工程导入');
    if (t.kind === 'emitter' && (!safeName(t.sprite) || !Number.isInteger(t.count) || t.count < 1 || t.count > 300 || !Number.isInteger(t.interval) || t.interval < 1 || !Number.isFinite(t.speed) || !Number.isFinite(t.life) || t.life < 1)) throw new Error('Invalid emitter');
    if (t.kind === 'object' && !safeName(t.sprite)) throw new Error('Invalid sprite');
    if(t.kind==='effect'&&!effectTypes.includes(t.effect))throw new Error('Invalid screen effect');
    for (const k of t.keyframes) if (!Number.isFinite(k.frame) || !fields.includes(k.property) || !Number.isFinite(k.value) || k.frame<0 || k.frame>s.duration) throw new Error('Invalid keyframe');
  }
}

function lines(file) { return fs.readFileSync(file, 'utf8').replace(/\r/g, '').split('\n'); }
function appendUnique(file, value) { const arr = lines(file); if (!arr.includes(value)) fs.appendFileSync(file, `${value}\n`, 'utf8'); }
function atomicWrite(file, value) { if (fs.existsSync(file) && fs.readFileSync(file,'utf8')===value) return; const temp = `${file}.${crypto.randomBytes(4).toString('hex')}.tmp`; fs.writeFileSync(temp, value, 'utf8'); fs.renameSync(temp, file); }
function gcolor(hex) { const v = /^#[0-9a-f]{6}$/i.test(hex || '') ? hex.slice(1) : 'ffffff'; return `make_color_rgb(${parseInt(v.slice(0,2),16)},${parseInt(v.slice(2,4),16)},${parseInt(v.slice(4,6),16)})`; }

const stateAt=stateCode;

function bulletSpawn(track, fromHistory, sequence='0',index=-1) {
  const time = fromHistory ? 'rf_i' : 'rf_frame';
  const age = fromHistory ? '(rf_frame-rf_i)' : '0';
  const pieces = [];
  pieces.push(stateAt(track,time));
  pieces.push(`if (${age}<rf_life) { rf_count=max(1,min(300,round(rf_count))); for (rf_j=0;rf_j<rf_count;rf_j+=1) {`);
  pieces.push(`rf_dir=rf_angle+(rf_j-(rf_count-1)/2)*rf_spread;`);
  pieces.push(`rf_inst=instance_create(rf_x+lengthdir_x(rf_speed*${track.flightMode!=='straight'?'0':age},rf_dir),rf_y+lengthdir_y(rf_speed*${track.flightMode!=='straight'?'0':age},rf_dir),objRufBullet);`);
  pieces.push(`rf_inst.rf_age=${track.flightMode!=='straight'?'0':age}; rf_inst.rf_life=rf_life; rf_inst.rf_speed=rf_speed; rf_inst.rf_dir=rf_dir;`);
  pieces.push(`rf_inst.sprite_index=${track.sprite}; rf_inst.image_blend=${gcolor(track.color)}; rf_inst.image_xscale=rf_scale; rf_inst.image_yscale=rf_scale; rf_inst.image_alpha=rf_alpha; rf_inst.rf_trail=${track.trail ? 1 : 0};`);
  pieces.push(`rf_inst.image_angle=rf_spriteAngle+${track.rotateSprite?'rf_dir':'0'};rf_inst.rf_rotate=${track.rotateSprite?1:0};rf_inst.rf_sprite_angle=rf_spriteAngle;rf_inst.rf_birth=${time};rf_inst.rf_index=rf_j;`);
  pieces.push(`rf_inst.rf_t_mode='${trailMode(track)}';rf_inst.rf_t_interval=${parameter(track,'trailInterval',3)};rf_inst.rf_t_life=${parameter(track,'trailLife',18)};rf_inst.rf_t_alpha=${parameter(track,'trailStartAlpha',.55)};rf_inst.rf_t_scale_decay=${parameter(track,'trailScaleDecay',.035)};rf_inst.rf_t_alpha_decay=${parameter(track,'trailAlphaDecay',.035)};rf_inst.rf_t_speed=${parameter(track,'trailSpeed')};rf_inst.rf_t_direction=${parameter(track,'trailDirection')};rf_inst.rf_t_random=${track.trailRandom?1:0};`);
  if(track.rainbow)pieces.push(`rf_inst.rf_rainbow=1;rf_inst.rf_r_sequence=${sequence}+rf_j;rf_inst.rf_r_density=rf_rainbowDensity;rf_inst.rf_r_offset=rf_rainbowOffset;rf_inst.rf_r_speed=rf_rainbowSpeed;rf_inst.rf_r_start=${gnum(track.start)};rf_inst.rf_r_dynamic=${track.rainbowDynamic?1:0};rf_inst.rf_r_reverse=${track.rainbowReverse?1:0};rf_inst.image_speed=0;rf_inst.image_blend=c_white;rf_inst.image_index=${rainbowCode(track,`${sequence}+rf_j`,'rf_frame')};`);
  pieces.push(flightSpawn(track,index,fromHistory));
  pieces.push('} }');if(track.rainbow)pieces.push(`${sequence}+=max(1,min(300,round(rf_count)));`);
  return pieces.join('\n');
}

function rodUpdate(t,index) {
  const source=t._allTracks.indexOf(t);const prefix=`rf_rod${index}`,parts=[`if rf_template_visible(${source},-1,noone) && (${activeCode(t,'rf_frame',t._allTracks)}${t.releaseFrame>=0?' && rf_frame<'+gnum(t.releaseFrame):''}) {`,stateAt(t,'rf_frame')];
  parts.push(shapeSetup(t));
  parts.push(`for(rf_j=0;rf_j<max(rf_count,${prefix}_count);rf_j+=1) {`);
  parts.push(`if rf_j>=rf_count || !rf_template_visible(${source},rf_j,noone) {if instance_exists(${prefix}[rf_j]) with (${prefix}[rf_j]) instance_destroy();${prefix}[rf_j]=noone;} else {`);
  parts.push(`if !instance_exists(${prefix}[rf_j]) {${prefix}[rf_j]=instance_create(0,0,objRufBullet);${prefix}[rf_j].rf_fixed=1;}rf_inst=${prefix}[rf_j];rf_inst.rf_static_source=${source};rf_inst.rf_index=rf_j;`);
  parts.push(shapePoint(t));
  parts.push(`rf_inst.x=rf_px;rf_inst.y=rf_py;rf_inst.image_angle=rf_spriteAngle+${t.rotateSprite?'rf_angle':'0'};rf_inst.sprite_index=${t.sprite};rf_inst.image_blend=${gcolor(t.color)};rf_inst.image_xscale=rf_scale;rf_inst.image_yscale=rf_scale;rf_inst.image_alpha=rf_alpha;rf_inst.rf_trail=${t.trail?1:0};`);
  if(t.rainbow)parts.push(`rf_inst.image_speed=0;rf_inst.image_blend=c_white;rf_inst.image_index=${rainbowCode(t,rodSequence(t),'rf_frame')};`);
  parts.push(`}} ${prefix}_count=rf_count; } else if ${prefix}_count>0 {for(rf_j=0;rf_j<${prefix}_count;rf_j+=1) {if instance_exists(${prefix}[rf_j]) with (${prefix}[rf_j]) instance_destroy();${prefix}[rf_j]=noone;}${prefix}_count=0;}`);
  return parts.join('\n');
}


function spatialTrails(t) {
  if(trailMode(t)==='off')return '';
  const interval=parameter(t,'trailInterval',3),life=`min(300,${parameter(t,'trailLife',18)})`;
  const parts=[`if rf_template_visible(${t._allTracks.indexOf(t)},-1,noone) && (${activeCode(t,'rf_frame',t._allTracks)}) {for(rf_sample=floor(rf_frame/${interval})*${interval};rf_sample>=max(${gnum(t.start)},rf_frame-${life}+1);rf_sample-=${interval}) {`,stateAt(t,'rf_sample')];
  if(t.kind==='object')parts.push(trailCall(t,{color:gcolor(t.color),age:'rf_frame-rf_sample',seed:'rf_sample',subimage:rainbowCode(t,'0','rf_sample')}));
  else {
    parts.push(shapeSetup(t,'rf_sample'),'for(rf_j=0;rf_j<rf_count;rf_j+=1){',shapePoint(t));
    parts.push(`if rf_template_visible(${t._allTracks.indexOf(t)},rf_j,noone)`,trailCall(t,{x:'rf_px',y:'rf_py',angle:`rf_spriteAngle+${t.rotateSprite?'rf_angle':'0'}`,color:gcolor(t.color),age:'rf_frame-rf_sample',seed:'rf_sample*301+rf_j',subimage:rainbowCode(t,rodSequence(t),'rf_sample')}),'}');
  }
  parts.push('}}');return parts.join('\n');
}

function makeController(s) {
  const beta=advancedBullets(s);
  const native=Boolean(s.nativeProject?.imported),nativeClock=s.nativeProject?.timeline?`timeline_position/${gnum(s.nativeProject.timelineSpeed,1)}`:s.nativeProject?.clock;
  const create = [`#define Create_0`,action,native?`rf_frame=0;rf_target=0;rf_preview=0;rf_ready=0;`:`rf_frame=0; rf_target=0; rf_preview=0; rf_ready=0; rf_music=FMODSoundPlay(BGM_Ad,1); global.BGMHandler=rf_music;`,
    `if file_exists(working_directory+'\\Data\\rufmaker_preview.ini') { ini_open(working_directory+'\\Data\\rufmaker_preview.ini'); rf_target=ini_read_real('preview','frame',0); ini_close(); rf_preview=1; }`,
    `rf_frame=max(0,min(${gnum(s.duration)},round(rf_target))); rf_target=rf_frame;`,
    native?'':`FMODInstanceSetPosition(rf_music,(rf_frame/${gnum(s.fps)}*1000)/FMODSoundGetLength(BGM_Ad)); FMODInstanceSetPaused(rf_music,0);`,
    `var rf_k,rf_i,rf_j,rf_u,rf_dir,rf_inst,rf_len,rf_phase,rf_fraction,rf_px,rf_py,rf_seg,rf_mix,${fields.map(f=>'rf_'+f).join(',')};`,randomInitialization(s), `rf_template_init();rf_custom_create();`,fxInitialization(s),cameraUpdate(s),`rf_inst=instance_create(0,0,objRufTrails);rf_inst.rf_owner=id;`];
  const step = [`#define Step_0`,action,native&&nativeClock&&safeName(s.nativeProject.controller)?`rf_frame+=1;if instance_exists(${s.nativeProject.controller}) with(${s.nativeProject.controller}) {other.rf_frame=${nativeClock};}`:`rf_frame+=1;`,native?'':`if rf_frame>${gnum(s.duration)} { room_goto(rEnd); exit; }`,
    `var rf_k,rf_i,rf_j,rf_u,rf_dir,rf_inst,rf_len,rf_phase,rf_fraction,rf_px,rf_py,rf_seg,rf_mix,${fields.map(f=>'rf_'+f).join(',')};`, `rf_custom_step();`,cameraUpdate(s)];
  const draw = [`#define Draw_0`,action,`var rf_sample,rf_len,rf_phase,rf_fraction,rf_px,rf_py,rf_seg,rf_mix,rf_i,rf_j,rf_u,rf_dir,rf_inst,${fields.map(f=>'rf_'+f).join(',')};`, `rf_custom_draw();`];
  let rodIndex=0,emitterIndex=0;
  for (const t of s.tracks.filter(x=>x.enabled!==false)) {

    if (t.kind==='emitter') {
      if(beta)continue;
      const lives=[parameter(t,'life',120),...t.keyframes.filter(k=>k.property==='life').map(k=>parameter(t,'life',120,k))],maxLife=(t.expressions?.life&&isDynamic(t.expressions.life)||t.keyframes.some(k=>k.property==='life'&&k.expression&&isDynamic(k.expression)))?gnum(s.duration+1):lives.length>1?`max(${lives.join(',')})`:lives[0];
      const sequence=`rf_emitter${emitterIndex++}_sequence`,first=`${gnum(t.start)}+max(0,ceil((rf_frame-${maxLife}+1-${gnum(t.start)})/${parameter(t,'interval',1)}))*${parameter(t,'interval',1)}`;
      if(t.rainbow) {
        create.push(`${sequence}=0;`);
        if(t.keyframes.some(k=>k.property==='count'))create.push(`for(rf_k=${gnum(t.start)};rf_k<min(${first},${gnum(t.end)}+1);rf_k+=${parameter(t,'interval',1)}){${stateAt(t,'rf_k')} ${sequence}+=max(1,min(300,round(rf_count)));}`);
        else create.push(`${sequence}=max(0,ceil((rf_frame-${maxLife}+1-${gnum(t.start)})/${parameter(t,'interval',1)}))*max(1,min(300,round(${parameter(t,'count',12)})));`);
      }
      create.push(`// RufMaker emitter\nfor (rf_i=${first};rf_i<=min(${gnum(t.end)},rf_frame);rf_i+=${parameter(t,'interval',1)}) { ${bulletSpawn(t,true,sequence,s.tracks.indexOf(t))} }`);
      step.push(`if (rf_frame>=${gnum(t.start)} && rf_frame<=${gnum(t.end)} && (rf_frame-${gnum(t.start)}) mod ${parameter(t,'interval',1)}=0) { ${bulletSpawn(t,false,sequence,s.tracks.indexOf(t))} }`);
    } else if(t.kind==='rod') {
      const prefix=`rf_rod${rodIndex}`;
      create.push(`${prefix}_count=0;for(rf_j=0;rf_j<300;rf_j+=1) ${prefix}[rf_j]=noone;`,rodUpdate(t,rodIndex));
      step.push(rodUpdate(t,rodIndex++));
    } else if (t.kind==='object') {
      draw.push(`if rf_template_visible(${s.tracks.indexOf(t)},-1,noone) && (${activeCode(t,'rf_frame',s.tracks)}) { ${stateAt(t,'rf_frame')} draw_sprite_ext(${t.sprite},${rainbowCode(t,'0','rf_frame')},rf_x,rf_y,rf_scale,rf_scale,rf_angle,${gcolor(t.color)},rf_alpha); }`);
    } else if (t.kind==='effect') {
      // Screen effects are evaluated every Draw, including native preview started mid-track.
    } else if (t.kind==='script') {
      step.push(`if rf_frame=${gnum(t.start)} {\n${t.code || ''}\n}`);
    }
  }
  step.push(`if rf_preview && !rf_ready { rf_ready=1; ini_open(working_directory+'\\Data\\rufmaker_preview_status.ini'); ini_write_real('preview','frame',rf_target); ini_write_real('preview','current_frame',rf_frame); ini_write_real('preview','fps',room_speed); ini_write_string('preview','room',room_get_name(room)); ini_write_real('preview','bullets',instance_number(objRufBullet)); ini_write_real('preview','players',instance_number(player)); ini_close(); }`);
  if(beta){create.push('var rf_replay;for(rf_replay=0;rf_replay<=rf_frame;rf_replay+=1) rf_pattern_step(rf_replay);');step.push('rf_pattern_step(rf_frame);');}
  create.push("if rf_preview {var rf_trace_file;rf_trace_file=file_text_open_write(working_directory+'\\Data\\rufmaker_player_trace.csv');file_text_close(rf_trace_file);}");
  step.push('rf_record_trace();');
  step.push('FMODUpdate();');
  const destroy=[`#define Destroy_0`,action,native?'':`if FMODInstanceIsPlaying(rf_music) FMODInstanceStop(rf_music);`,fxCleanup(s)];
  const roomStart=[`#define Other_4`,action,`room_speed=${gnum(s.fps)};`];
  if(s.player)roomStart.push(`with(player){grav=${gnum(s.player.gravity,.4)};maxSpeed=${gnum(s.player.moveSpeed,3)};jump[1]=${gnum(s.player.jumpSpeed,8.5)};jump[2]=${gnum(s.player.doubleJumpSpeed,7)};maxJumps=max(1,min(8,${gnum(s.player.maxJumps,2)}));var rf_jump;for(rf_jump=3;rf_jump<=maxJumps;rf_jump+=1)jump[rf_jump]=jump[2];infJump=${s.player.infiniteJump?1:0};}`);
  return [create.join('\n'),step.join('\n'),draw.join('\n'),destroy.join('\n'),roomStart.join('\n')].join('\n')+'\n';
}

const bulletGml = `#define Create_0
${action}
image_speed=0;image_index=0;rf_rainbow=0;rf_r_sequence=0;rf_r_density=1;rf_r_offset=0;rf_r_speed=.25;rf_r_start=0;rf_r_dynamic=0;rf_r_reverse=0;
rf_kinematic=0;rf_template_owner=noone;rf_static_source=-1;rf_clock_shift=0;rf_system=0;rf_source=-1;rf_behavior=-1;rf_last_update=-1;rf_spin=0;rf_accel=0;rf_collision=1;rf_dead=0;rf_sprite=sprite_index;
rf_flight=-1;rf_turn=3;rf_delay=0;rf_age=0;rf_life=120;rf_speed=0;rf_dir=0;rf_trail=0;rf_fixed=0;rf_rotate=0;rf_sprite_angle=0;rf_birth=0;rf_index=0;
rf_t_mode='off';rf_t_interval=3;rf_t_life=18;rf_t_alpha=.55;rf_t_scale_decay=.035;rf_t_alpha_decay=.035;rf_t_speed=0;rf_t_direction=0;rf_t_random=0;
#define Step_0
${action}
rf_custom_bullet();if rf_fixed || rf_system exit;
rf_bullet_advance();
if rf_age>=rf_life instance_destroy();
#define Draw_0
${action}
var rf_sample,rf_tail,rf_drift,rf_seed,rf_world,rf_subimage;
rf_world=rf_birth+rf_age;if instance_exists(objRufController) rf_world=objRufController.rf_frame;
if rf_rainbow {image_speed=0;image_blend=c_white;image_index=rf_rainbow_index(rf_r_sequence,rf_world,rf_r_density,rf_r_offset,rf_r_speed,rf_r_start,rf_r_dynamic,rf_r_reverse,32);}
if !rf_system || rf_world>=rf_birth draw_sprite_ext(rf_system*rf_sprite+(!rf_system)*sprite_index,image_index,x,y,image_xscale,image_yscale,image_angle,image_blend,image_alpha);
`;


const bulletTrailGml=`// The separate trail renderer stays behind every source, including custom GML depth changes.
var rf_sample,rf_tail,rf_drift,rf_seed,rf_world,rf_subimage,rf_tx,rf_ty,rf_ta,rf_slot,rf_ts,rf_talpha,rf_tsprite;
rf_world=rf_birth+rf_age;if instance_exists(objRufController) rf_world=objRufController.rf_frame;
if !rf_fixed && rf_t_mode!='off' && (!rf_system || rf_world>=rf_birth) {
for(rf_sample=floor(rf_age/max(1,rf_t_interval))*max(1,rf_t_interval);rf_sample>=max(0,rf_age-rf_t_life+1);rf_sample-=max(1,rf_t_interval)) {
rf_tail=rf_age-rf_sample;rf_seed=(rf_birth+rf_sample)*301+rf_index;
rf_drift=rf_t_direction;if rf_t_random rf_drift=(sin((rf_seed+1)*12.9898)*43758.5453-floor(sin((rf_seed+1)*12.9898)*43758.5453))*360;
rf_subimage=image_index;if rf_rainbow rf_subimage=rf_rainbow_index(rf_r_sequence,rf_world-rf_tail,rf_r_density,rf_r_offset,rf_r_speed,rf_r_start,rf_r_dynamic,rf_r_reverse,32);
rf_tx=x-lengthdir_x(rf_speed*rf_tail,rf_dir);rf_ty=y-lengthdir_y(rf_speed*rf_tail,rf_dir);rf_ta=image_angle;rf_ts=image_xscale;rf_talpha=image_alpha;rf_tsprite=sprite_index;if rf_flight>=0 || rf_system {if rf_tail>300 continue;rf_slot=rf_sample mod 301;rf_tx=rf_hx[rf_slot];rf_ty=rf_hy[rf_slot];rf_ta=rf_ha[rf_slot];if rf_system {rf_ts=rf_hs[rf_slot];rf_talpha=rf_hal[rf_slot];rf_tsprite=rf_sprite;}}
rf_trail_draw(rf_tsprite,rf_tx,rf_ty,rf_ts,rf_ta,rf_talpha,image_blend,rf_tail,rf_t_mode,rf_t_life,rf_t_alpha,rf_t_scale_decay,rf_t_alpha_decay,rf_t_speed,rf_drift,rf_subimage);
}}

return 0;
`;
function makeTrails(s) {
  const spatial=s.tracks.filter(t=>t.enabled!==false&&(t.kind==='rod'||t.kind==='object')).map(spatialTrails).join('\n');
  return `#define Create_0\n${action}\nrf_owner=noone;
#define Step_2
${action}
if !instance_exists(rf_owner) {instance_destroy();exit;}
depth=rf_owner.depth+1;with(objRufBullet) other.depth=max(other.depth,depth+1);
#define Draw_0
${action}
if !instance_exists(rf_owner) {instance_destroy();exit;}
with(objRufBullet) rf_bullet_trails();
with(rf_owner) {
var rf_sample,rf_len,rf_phase,rf_fraction,rf_px,rf_py,rf_seg,rf_mix,rf_i,rf_j,rf_u,rf_dir,rf_inst,${fields.map(f=>'rf_'+f).join(',')};
${spatial}
}
`;
}

export function syncToGame(s) {
  if (!fs.existsSync(projectFile) || !fs.readFileSync(projectFile,'utf8').startsWith('gm82_version=')) throw new Error(`GameMaker 工程入口格式不正确：${projectFile}`);
  normalizeState(s);
  validateState(s);
  const storedState=s;s=normalizeState(expandPatterns(s));for(const t of s.tracks)Object.defineProperty(t,'_allTracks',{value:s.tracks,configurable:true});
  const native=Boolean(s.nativeProject?.imported);
  if(native){for(const t of s.tracks.filter(t=>t.kind==='native')){t.start=0;t.end=Math.min(s.duration,s.nativeProject.duration||s.duration);t.enabled=true;}if(!s.tracks.some(t=>t.kind!=='native')){atomicWrite(makerFile,JSON.stringify(s,null,2)+'\n');return;}}
  if(!native)preparePlainRoom(path.resolve(sourceDir,'..'),project,s,atomicWrite,appendUnique);
  if(native){
    appendUnique(path.join(project,'settings','extensions.txt'),'Game Maker 8.2 DirectX9');
    for(const dir of ['objects','scripts'])for(const file of fs.readdirSync(path.join(project,dir)).filter(f=>f.endsWith('.gml')&&!/^(?:objRuf|rf_)/.test(f))){const target=path.join(project,dir,file);atomicWrite(target,patchNativeSurfaceReset(fs.readFileSync(target,'utf8')));}
    for(const [name,code] of [['rf_native_compose',nativeComposeGml(s)],['rf_native_surface_reset',nativeSurfaceResetGml]]){atomicWrite(path.join(project,'scripts',name+'.gml'),code);appendUnique(path.join(project,'scripts','index.yyd'),name);appendUnique(path.join(project,'scripts','tree.yyd'),'\t|'+name);}
  }
  for (const [name,code] of [...templateScripts(s),...formationScripts,...expressionScripts(s,fs.readdirSync(path.join(project,'objects')).filter(n=>/^(?:(?:obj_?|o)?player[12]?)\.txt$/i.test(n)).map(n=>n.slice(0,-4))),['rf_camera_post',cameraPostGml],['rf_style_fx',styleFxGml],['rf_bullet_advance',bulletFlightGml(s)],['rf_random_next',randomNextGml],['rf_camera_apply',cameraApplyGml],['rf_camera_composite',cameraCompositeGml],['rf_bullet_trails',bulletTrailGml],['rf_ease',easeGml],['rf_bounce',bounceGml],['rf_fx_reset_target',fxResetGml],['rf_fx_opaque',fxOpaqueGml],['rf_trail_draw',trailGml],['rf_rainbow_index',rainbowGml]]) {atomicWrite(path.join(project,'scripts',`${name}.gml`),code);appendUnique(path.join(project,'scripts','index.yyd'),name);appendUnique(path.join(project,'scripts','tree.yyd'),`	|${name}`);}
  if(!native&&(typeof s.music!=='string'||!/^Data\/Music\/[A-Za-z0-9_. -]+\.(ogg|mp3|wav|flac)$/i.test(s.music)||!fs.existsSync(path.join(project,s.music))))throw new Error('工程音乐不存在，请重新导入歌曲');
  for(const [name,code] of bulletSystemScripts(s)){atomicWrite(path.join(project,'scripts',name+'.gml'),code);appendUnique(path.join(project,'scripts','index.yyd'),name);appendUnique(path.join(project,'scripts','tree.yyd'),'\t|'+name);}
  atomicWrite(path.join(project,'scripts','rf_record_trace.gml'),"if !rf_preview || rf_frame<rf_target return 0;\nvar rf_file;rf_file=file_text_open_append(working_directory+'\\Data\\rufmaker_player_trace.csv');file_text_write_string(rf_file,string(floor(rf_frame))+','+string_format(rf_expression_player(0),0,6)+','+string_format(rf_expression_player(1),0,6));file_text_writeln(rf_file);file_text_close(rf_file);return 0;\n");appendUnique(path.join(project,'scripts','index.yyd'),'rf_record_trace');appendUnique(path.join(project,'scripts','tree.yyd'),'\t|rf_record_trace');
  // Preserve native or handwritten versions. Only seed missing public helpers.
  for(const name of ['heart','star2']){
    const file=path.join(project,'scripts',name+'.gml');
    if(!fs.existsSync(file))fs.copyFileSync(path.join(root,'assets','gml','formations',name+'.gml'),file);
    appendUnique(path.join(project,'scripts','index.yyd'),name);appendUnique(path.join(project,'scripts','tree.yyd'),'\t|'+name);
  }
  const musicInit=path.join(project,'scripts','music_init.gml');
  if(!native)atomicWrite(musicInit,fs.readFileSync(musicInit,'utf8').replace(/^BGM_Ad\s*=.*$/m,`BGM_Ad = FMODSoundAdd('${s.music}',0,0);`).replace(/global\.music_directory\s*\+\s*'([^']+)'/g,(_,file)=>`'Data/Music/${file}'`));
  const objects = path.join(project,'objects');
  const backgroundFile=path.join(objects,'objBackground.gml');
  if(!native&&fs.existsSync(backgroundFile)){const source=fs.readFileSync(backgroundFile,'utf8');atomicWrite(backgroundFile,source.replace(/\bsurface_reset_target\s*\(\s*\)/g,'rf_fx_reset_target()').replace(/\bdraw_surface\(global\.surface1,0,0\)/g,'rf_camera_composite(global.surface1,0,0)'));}
  for (const name of ['rf_custom_create','rf_custom_step','rf_custom_draw','rf_custom_bullet']) {
    const file=path.join(project,'scripts',`${name}.gml`);
    if(!fs.existsSync(file)) atomicWrite(file,`/// ${name}: handwritten GameMaker GML hook\nreturn 0;\n`);
    appendUnique(path.join(project,'scripts','index.yyd'),name);
    appendUnique(path.join(project,'scripts','tree.yyd'),`\t|${name}`);
  }
  atomicWrite(path.join(project,'scripts','rf_preview_boot.gml'),`// Generated by RufMaker: enter the timeline room for native preview.\nif ${s.directStart?'1 || ':''}file_exists(working_directory+'\\Data\\rufmaker_preview.ini') { room_goto(${s.room}); return 1; }\nreturn 0;\n`);
  appendUnique(path.join(project,'scripts','index.yyd'),'rf_preview_boot');
  appendUnique(path.join(project,'scripts','tree.yyd'),'\t|rf_preview_boot');
  const initFile=path.join(objects,'init.gml');
  const initCode=fs.existsSync(initFile)?fs.readFileSync(initFile,'utf8'):'';
  if (!native&&!initCode.includes('if (rf_preview_boot()) exit;')) {
    const patched=initCode.replace(/(#define Alarm_0\r?\n\/\*[\s\S]*?\*\/\r?\n)/,'$1if (rf_preview_boot()) exit;\n');
    if (patched===initCode) throw new Error('找不到引擎初始化事件，无法接入原生试玩');
    atomicWrite(initFile,patched);
  }
  for (const [name,info,code] of [
    ['objRufController','sprite=\nvisible=1\nsolid=0\npersistent=0\ndepth=-7000000\nparent=\nmask=\n',makeController(s)],
    ['objRufBullet',`sprite=${native?s.nativeProject.defaultSprite:'sprBullet'}\nvisible=1\nsolid=0\npersistent=0\ndepth=-100\nparent=${native?s.nativeProject.killerParent:'playerKiller'}\nmask=\n`,bulletGml],
    ['objRufTemplate','sprite=\nvisible=0\nsolid=0\npersistent=0\ndepth=0\nparent=\nmask=\n',templateObject],
    ['objRufTrails','sprite=\nvisible=1\nsolid=0\npersistent=0\ndepth=-99\nparent=\nmask=\n',makeTrails(s)],
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
    const inst=original.filter(x=>!x.startsWith('objRufController,') && (native||entry.name!==s.room || !x.startsWith('objAd,')));
    if (entry.name===s.room) inst.push('objRufController,32,32,00F0F0F0,0,1,1,4294967295,0,0');
    if (inst.join('\n')!==original.join('\n')) atomicWrite(instFile,inst.join('\n')+'\n');
  }
  const roomSettings=path.join(project,'rooms',s.room,'room.txt');
  const settings=fs.readFileSync(roomSettings,'utf8');
  const updated=settings.replace(/^roomspeed=\d+$/m,`roomspeed=${s.fps}`);
  if (updated!==settings) atomicWrite(roomSettings,updated);
  atomicWrite(makerFile,JSON.stringify(storedState,null,2)+'\n');
}

export function roomInfo(room='rAvoidance') {
  if (!safeName(room)) throw new Error('Invalid room');
  const rfile=path.join(project,'rooms',room,'room.txt');
  const settings=Object.fromEntries(lines(rfile).filter(x=>x.includes('=')).map(x=>x.split(/=(.*)/s).slice(0,2)));
  const instances=lines(path.join(project,'rooms',room,'instances.txt')).filter(Boolean).map((line,index)=>{ const a=line.split(','); return { index,id:a[3],name:a[0],x:Number(a[1]),y:Number(a[2]),raw:line }; });
  return { name:room,width:Number(settings.width),height:Number(settings.height),fps:Number(settings.roomspeed),bgColor:Number(settings.bg_color??2105376),instances };
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
  if (kind==='objects' && ['objRufController','objRufBullet','objRufTrails','objRufFxBegin','objRufFxEnd'].includes(name)) throw new Error('Generated object: edit its timeline track instead');
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
