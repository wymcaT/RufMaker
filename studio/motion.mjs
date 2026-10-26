import {normalizeRainbow} from './rainbow.mjs';
import {normalizeBulletState,groupTransformCode} from './bullet-schema.mjs';
import {gmlNumber as num} from './gml.mjs';
import {resolveRandomState,parameterCode as parameterBase,evaluateExpression,isDynamic,clampParameter,expressionContext} from './random-parameters.mjs';
export const basic=['x','y','angle','scale','alpha'];
export const fields=['emitWidth','emitHeight','emitArc','emitPhase','emitRotation','emitWaves','x','y','angle','scale','alpha','speed','spread','count','life','x1','y1','x2','y2','length','rollSpeed','rainbowDensity','rainbowOffset','rainbowSpeed','spriteAngle','strength','fxOpacity','fxAngle','fxRetention','radius','orientation','sides','shapeAspect','shapeInner','cameraZoom','orbitRadius','orbitPhase','pathStartX','pathStartY','pathEndX','pathEndY','pathPeakX','pathPeakY','pathProgress','cameraIntensity','cameraBlur','cameraBlurAngle','fxZoom','fxFlash'];
export const defaults={emitWidth:240,emitHeight:160,emitArc:360,emitPhase:0,emitRotation:0,emitWaves:1,cameraIntensity:1,cameraBlur:0,cameraBlurAngle:0,fxZoom:1.13,fxFlash:.18,radius:120,orientation:0,sides:5,shapeAspect:1,shapeInner:.45,cameraZoom:1,x:400,y:304,angle:0,scale:1,alpha:1,speed:3,spread:30,count:12,life:160,x1:280,y1:304,x2:520,y2:304,length:240,rollSpeed:2,rainbowDensity:1,rainbowOffset:0,rainbowSpeed:.25,spriteAngle:0,strength:6,fxOpacity:1,fxAngle:0,fxRetention:.5};
export function normalizeState(s,globalSource=null) {
  if(globalSource)Object.defineProperty(s,'_globalSource',{value:globalSource,configurable:true});
  if(s.version===1) {
    for(const t of s.tracks)t.keyframes=t.keyframes.flatMap(k=>k.property?[k]:basic.map(property=>({frame:k.frame,property,value:(k[property]??defaults[property])+(property==='angle'&&t.kind==='emitter'?(t.angle||0):0),easing:''})));
    s.version=2;
  }
  for(const t of s.tracks)Object.assign(t,Object.fromEntries(Object.entries({motion:'keyframes',flightMode:'straight',flightExpression:'point_direction(x,y,player.x,player.y)',turnRate:3,homingDelay:0,shape:'rod',rodMode:'center',easePower:3,easePeriod:.3,velocityX:0,velocityY:0,accelerationX:0,accelerationY:0,angularSpeed:0,orbitRadius:100,orbitSpeed:1,orbitPhase:0,rolling:false,fxSamples:6,fxFrequency:1,cameraMirror:false,fxSlices:11,fxStutter:2,...defaults}).filter(([key])=>t[key]===undefined)));
  for(const t of s.tracks) {
    normalizeRainbow(t);
    if(t.trailMode===undefined||t.trailMode==='legacy')t.trailMode=t.trail?'fade':'off';
    t.trail=t.trailMode!=='off';
    Object.assign(t,Object.fromEntries(Object.entries({rotateSprite:false,trailRandom:false,trailInterval:3,trailLife:18,trailScaleDecay:.035,trailAlphaDecay:.035,trailStartAlpha:.55,trailSpeed:0,trailDirection:0}).filter(([key])=>t[key]===undefined)));
  }
  let next=Math.max(0,...s.tracks.map(t=>Number.isInteger(t.lane)&&t.lane>=0?t.lane+1:0));for(const t of s.tracks)if(!Number.isInteger(t.lane)||t.lane<0)t.lane=next++;
  s.laneCount=Math.max(1,s.laneCount||0,...s.tracks.map(t=>t.lane+1));
  s.tracks.sort((a,b)=>(a.kind==='native'?-1:a.lane)-(b.kind==='native'?-1:b.lane));
  normalizeBulletState(s);const identify=t=>{t.randomId||=t.id;t.children.forEach(identify);};s.tracks.forEach(identify);resolveRandomState(s);for(const t of s.tracks)if(t.kind==='pattern')normalizeState({version:2,fps:s.fps,duration:s.duration,randomSeed:s.randomSeed,nativeTest:s.nativeTest,tracks:t.children},s);return s;
}
function bounce(u) {if(u<1/2.75)return 7.5625*u*u;if(u<2/2.75)return 7.5625*(u-1.5/2.75)**2+.75;if(u<2.5/2.75)return 7.5625*(u-2.25/2.75)**2+.9375;return 7.5625*(u-2.625/2.75)**2+.984375;}
export function ease(e,u,p=3,period=.3) {
  u=Math.max(0,Math.min(1,u));if(u===0||u===1)return u;p=Math.max(.1,p);period=Math.max(.01,period);
  switch(e) {
    case 'edit-smooth':return u*u*(3-2*u);case 'edit-snap':return 1-2**(-10*u);case 'edit-back':return 1+2.70158*(u-1)**3+1.70158*(u-1)**2;case 'edit-elastic':return 2**(-8*u)*Math.sin((u-.075)*2*Math.PI/.3)+1;case 'in':return u*u;case 'out':return 1-(1-u)**2;case 'smooth':return u*u*(3-2*u);
    case 'sin-in':return 1-Math.cos(u*Math.PI/2);case 'sin-out':return Math.sin(u*Math.PI/2);case 'sin-inout':return (1-Math.cos(u*Math.PI))/2;
    case 'circle-in':return 1-Math.sqrt(1-u*u);case 'circle-out':return Math.sqrt(1-(u-1)**2);case 'circle-inout':return u<.5?(1-Math.sqrt(1-4*u*u))/2:(Math.sqrt(1-4*(u-1)**2)+1)/2;
    case 'power-in':return u**p;case 'power-out':return 1-(1-u)**p;case 'power-inout':return u<.5?(2*u)**p/2:1-(2*(1-u))**p/2;
    case 'expo-in':return 2**(10*(u-1));case 'expo-out':return 1-2**(-10*u);case 'expo-inout':return u<.5?2**(20*u-10)/2:1-2**(-20*u+10)/2;
    case 'bounce':return bounce(u);case 'bounce-in':return 1-bounce(1-u);case 'bounce-inout':return u<.5?(1-bounce(1-2*u))/2:(1+bounce(2*u-1))/2;
    case 'expsin':return 2**(-10*u)*Math.sin((u-period/4)*2*Math.PI/period)+1;
    case 'back':return 1+2.70158*(u-1)**3+1.70158*(u-1)**2;case 'step':return 0;default:return u;
  }
}
function valueCore(t,property,frame,context) {
  const keys=t.keyframes.filter(k=>k.property===property).sort((a,b)=>a.frame-b.frame);
  const value=k=>{const expr=k?.expression||!k&&t.expressions?.[property],fallback=k?k.value:t[property]??defaults[property]??0;return expr&&isDynamic(expr)?clampParameter(property,evaluateExpression(expr,t._expressionContext?.seed??12345,(t.randomId||t.id)+'|'+property+'|'+(k?.randomId||'base'),context)):fallback;};
  if(!keys.length)return value(null);if(frame<=keys[0].frame)return value(keys[0]);
  for(let i=1;i<keys.length;i++)if(frame<keys[i].frame){const a=keys[i-1],b=keys[i];return value(a)+(value(b)-value(a))*ease(a.easing||t.easing,(frame-a.frame)/(b.frame-a.frame),t.easePower,t.easePeriod);}
  return value(keys.at(-1));
}
function positionAt(t,frame,context){
  const value=p=>valueCore(t,p,frame,context);let x=value('x'),y=value('y');const dt=Math.max(0,frame-(t.clockStart??t.start));
  if(t.motion==='velocity'){x+=value('velocityX')*dt+value('accelerationX')*dt*dt/2;y+=value('velocityY')*dt+value('accelerationY')*dt*dt/2;}
  if(t.motion==='orbit'){const angle=(value('orbitPhase')+value('orbitSpeed')*dt)*Math.PI/180;x+=Math.cos(angle)*value('orbitRadius');y-=Math.sin(angle)*value('orbitRadius');}
  if(t.motion==='parabola'){const u=Math.max(0,Math.min(1,value('pathProgress'))),v=1-u;for(const axis of ['X','Y']){const delta=v*v*value('pathStart'+axis)+2*v*u*(2*value('pathPeak'+axis)-(value('pathStart'+axis)+value('pathEnd'+axis))/2)+u*u*value('pathEnd'+axis);if(axis==='X')x+=delta;else y+=delta;}}
  return {x,y};
}
export function valueAt(t,property,frame){const context=expressionContext(t,frame);if(!['x1','y1','x2','y2'].includes(property)){const raw={...context};for(const p of ['x1','y1','x2','y2'])context[p]=valueCore(t,p,frame,raw);}if(!['x','y','x1','y1','x2','y2'].includes(property))Object.assign(context,positionAt(t,frame,context));return valueCore(t,property,frame,context);}
export function stateCode(t,time,context={}) {
  const out=[],blocks=new Map();let positioned=false;
  const parameter=(track,property,fallback=0,key=null)=>parameterBase(track,property,fallback,key,{...context,frame:time,x:positioned?'rf_x':num(t.x,400),y:positioned?'rf_y':num(t.y,304),...Object.fromEntries(['x1','y1','x2','y2'].map(p=>[p,['x1','y1','x2','y2'].includes(property)?num(t[p],defaults[p]):'rf_'+p]))});
  for(const field of fields) {
    positioned=!['x1','y1','x2','y2','x','y','orbitRadius','orbitPhase','pathStartX','pathStartY','pathEndX','pathEndY','pathPeakX','pathPeakY','pathProgress'].includes(field);
    const code=[];blocks.set(field,code);
    const keys=t.keyframes.filter(k=>k.property===field).sort((a,b)=>a.frame-b.frame),base=t[field]??defaults[field];
    if(!keys.length){code.push(`rf_${field}=${parameter(t,field,base)};`);continue;}
    if(t.motionPreset&&['x','y'].includes(field)&&keys.length>32){
      const tree=(lo,hi)=>{if(hi-lo===1){const a=keys[lo],b=keys[hi];return `rf_u=rf_ease('${a.easing||t.easing||'linear'}',(${time}-(${num(a.frame)}))/${num(Math.max(1,b.frame-a.frame))},${parameter(t,'easePower',3)},${parameter(t,'easePeriod',.3)});rf_${field}=(${parameter(t,field,0,a)})+((${parameter(t,field,0,b)})-(${parameter(t,field,0,a)}))*rf_u;`;}const mid=Math.floor((lo+hi)/2);return `if ${time}<${num(keys[mid].frame)} {${tree(lo,mid)}}else {${tree(mid,hi)}}`;};
      code.push(`if ${time}<=${num(keys[0].frame)} {rf_${field}=${parameter(t,field,0,keys[0])};}else if ${time}>=${num(keys.at(-1).frame)} {rf_${field}=${parameter(t,field,0,keys.at(-1))};}else {${tree(0,keys.length-1)}}`);continue;
    }
    code.push(`if (${time}<=${num(keys[0].frame)}) {rf_${field}=${parameter(t,field,0,keys[0])};}`);
    for(let i=1;i<keys.length;i++){const a=keys[i-1],b=keys[i];code.push(`else if (${time}<${num(b.frame)}) {rf_u=rf_ease('${a.easing||t.easing||'linear'}',(${time}-(${num(a.frame)}))/${num(Math.max(1,b.frame-a.frame))},${parameter(t,'easePower',3)},${parameter(t,'easePeriod',.3)});rf_${field}=${parameter(t,field,0,a)}+(${parameter(t,field,0,b)}-${parameter(t,field,0,a)})*rf_u;}`);}
    code.push(`else {rf_${field}=${parameter(t,field,0,keys.at(-1))};}`);
  }
  const geometry=['x1','y1','x2','y2','x','y','orbitRadius','orbitPhase','pathStartX','pathStartY','pathEndX','pathEndY','pathPeakX','pathPeakY','pathProgress'];for(const field of geometry)out.push(...blocks.get(field));positioned=false;
  const dt=`max(0,${time}-(${num(t.clockStart??t.start)}))`;
  if(t.motion==='velocity')out.push(`rf_x+=${parameter(t,'velocityX')}*${dt}+${parameter(t,'accelerationX')}*sqr(${dt})/2;rf_y+=${parameter(t,'velocityY')}*${dt}+${parameter(t,'accelerationY')}*sqr(${dt})/2;`);
  if(t.motion==='orbit')out.push(`rf_x+=lengthdir_x(rf_orbitRadius,rf_orbitPhase+${parameter(t,'orbitSpeed',1)}*${dt});rf_y+=lengthdir_y(rf_orbitRadius,rf_orbitPhase+${parameter(t,'orbitSpeed',1)}*${dt});`);
  if(t.motion==='parabola')out.push(`rf_u=max(0,min(1,rf_pathProgress));rf_x+=sqr(1-rf_u)*rf_pathStartX+2*(1-rf_u)*rf_u*(2*rf_pathPeakX-(rf_pathStartX+rf_pathEndX)/2)+sqr(rf_u)*rf_pathEndX;rf_y+=sqr(1-rf_u)*rf_pathStartY+2*(1-rf_u)*rf_u*(2*rf_pathPeakY-(rf_pathStartY+rf_pathEndY)/2)+sqr(rf_u)*rf_pathEndY;`);
  positioned=true;for(const field of fields.filter(f=>!geometry.includes(f)))out.push(...blocks.get(field));
  out.push(`rf_angle+=${parameter(t,'angularSpeed')}*${dt};`,groupTransformCode(t,time));return out.join('\n');
}
export const easeGml=`// Shared with the native editor: normalized outgoing segment easing.
var e,u,p,period,v;
e=argument0;u=max(0,min(1,argument1));p=max(.1,argument2);period=max(.01,argument3);
if u=0 || u=1 return u;
switch(e) {
case 'edit-smooth':return u*u*(3-2*u);case 'edit-snap':return 1-power(2,-10*u);case 'edit-back':return 1+2.70158*power(u-1,3)+1.70158*sqr(u-1);case 'edit-elastic':return power(2,-8*u)*sin((u-.075)*2*pi/.3)+1;case 'in':return sqr(u);case 'out':return 1-sqr(1-u);case 'smooth':return u*u*(3-2*u);
case 'sin-in':return 1-cos(u*pi/2);case 'sin-out':return sin(u*pi/2);case 'sin-inout':return (1-cos(u*pi))/2;
case 'circle-in':return 1-sqrt(1-u*u);case 'circle-out':return sqrt(1-sqr(u-1));case 'circle-inout':if u<.5 return (1-sqrt(1-4*u*u))/2;return (sqrt(1-4*sqr(u-1))+1)/2;
case 'power-in':return power(u,p);case 'power-out':return 1-power(1-u,p);case 'power-inout':if u<.5 return power(2*u,p)/2;return 1-power(2*(1-u),p)/2;
case 'expo-in':return power(2,10*(u-1));case 'expo-out':return 1-power(2,-10*u);case 'expo-inout':if u<.5 return power(2,20*u-10)/2;return 1-power(2,-20*u+10)/2;
case 'bounce':return rf_bounce(u);case 'bounce-in':return 1-rf_bounce(1-u);case 'bounce-inout':if u<.5 return (1-rf_bounce(1-2*u))/2;return (1+rf_bounce(2*u-1))/2;
case 'expsin':return power(2,-10*u)*sin((u-period/4)*2*pi/period)+1;
case 'back':return 1+2.70158*power(u-1,3)+1.70158*sqr(u-1);case 'step':return 0;
}return u;
`;
export const bounceGml=`var u;u=argument0;if u<1/2.75 return 7.5625*u*u;if u<2/2.75 return 7.5625*sqr(u-1.5/2.75)+.75;if u<2.5/2.75 return 7.5625*sqr(u-2.25/2.75)+.9375;return 7.5625*sqr(u-2.625/2.75)+.984375;\n`;
