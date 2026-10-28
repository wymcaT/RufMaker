import {gmlNumber as num} from './gml.mjs';
import crypto from 'node:crypto';
import {globalReference,globalsAt,attachGlobals} from './global-variables.mjs';
import {numericColor} from './spawn-appearance.mjs';

const cache=new Map();
export const expressionVariables=['player.x','player.y','source.x','source.y','x','y','x1','y1','x2','y2','frame','time','age','direction','speed','fps','pi','i','n','u','volley'];
const functions={random:1,random_range:2,choose:-1,irandom:1,irandom_range:2,point_direction:4,point_distance:4,lengthdir_x:2,lengthdir_y:2,sin:1,cos:1,abs:1,sqrt:1,floor:1,ceil:1,min:-1,max:-1,clamp:3,lerp:3,turn_towards:3};
export function isDynamic(expression){const visit=n=>expressionVariables.includes(n.op)&&n.op!=='pi'||globalReference(n.op)||n.args.some(visit);return visit(parseExpression(expression));}
export function playerAt(state,frame,fallback={x:400,y:304}){
  const reference=state?.nativeTest??fallback,trace=state?.playerTrace;
  if(!state?.usePlayerTrace||!trace?.length||frame<trace[0].frame||frame>trace.at(-1).frame)return reference;
  let lo=0,hi=trace.length-1;while(lo<hi){const mid=Math.ceil((lo+hi)/2);if(trace[mid].frame<=frame)lo=mid;else hi=mid-1;}
  const a=trace[lo],b=trace[Math.min(lo+1,trace.length-1)],u=(frame-a.frame)/Math.max(1,b.frame-a.frame);return {x:a.x+(b.x-a.x)*u,y:a.y+(b.y-a.y)*u};
}
export function expressionContext(track,frame,state={}){const source=state.tracks?state:track._expressionContext?.globalState??state,player=playerAt(source,frame,{x:state.nativeTest?.x??track._expressionContext?.['player.x']??400,y:state.nativeTest?.y??track._expressionContext?.['player.y']??304});return {globals:globalsAt(source,frame),'player.x':player.x,'player.y':player.y,x:track.x??400,y:track.y??304,'source.x':track.x??400,'source.y':track.y??304,x1:track.x1??280,y1:track.y1??304,x2:track.x2??520,y2:track.y2??304,frame,age:Math.max(0,frame-(track.clockStart??track.start)),direction:track.angle??0,speed:track.speed??3,fps:state.fps??track._expressionContext?.fps??50};}
export function turnTowards(current,target,limit){const delta=((target-current)%360+540)%360-180;return current+Math.max(-Math.abs(limit),Math.min(Math.abs(limit),delta));}
export function parseExpression(text) {
  if(cache.has(text))return cache.get(text);
  const source=String(text??'').replaceAll('（','(').replaceAll('）',')').replaceAll('，',',');let at=0;
  const space=()=>{while(/\s/.test(source[at]||'!'))at++;};
  const take=c=>{space();if(source[at]!==c)return false;at++;return true;};
  const error=()=>new Error(`参数表达式格式错误（位置 ${at+1}）；支持 player.x/y、point_direction、point_distance、random、choose 和四则运算。`);
  const binary=(next,operators)=>{let node=next();while(true){space();if(at>=source.length||!operators.includes(source[at]))return node;const op=source[at++];node={op,args:[node,next()]};}};
  const sum=()=>binary(product,'+-'),product=()=>binary(atom,'*/%');
  function atom() {
    if(take('+'))return atom();if(take('-'))return {op:'neg',args:[atom()]};
    if(take('(')){const result=sum();if(!take(')'))throw error();return result;}
    space();const match=source.slice(at).match(/^(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?/);
    if(match){at+=match[0].length;return {op:'number',number:Number(match[0]),args:[]};}
    const name=source.slice(at).match(/^[a-z_][a-z0-9_.]*/i)?.[0]?.toLowerCase();if(!name)throw error();at+=name.length;
    if(expressionVariables.includes(name)||globalReference(name))return {op:name,args:[]};
    const arity=functions[name];if(arity===undefined||!take('('))throw error();
    const args=[];if(!take(')')){do{args.push(sum());}while(take(','));if(!take(')'))throw error();}
    if(arity<0?!args.length:args.length!==arity)throw error();return {op:name,args};
  }
  const result=sum();space();if(at!==source.length)throw error();cache.set(text,result);return result;
}
export function hashSlot(text){let value=0;for(let i=0;i<text.length;i++)value=(value*31+text.charCodeAt(i))%2147483647;return value;}
export function mixRandomSeed(state){
  state^=Math.floor(state/65536);state=state*48271%2147483647;
  state^=Math.floor(state/32768);state=state*69621%2147483647;
  state^=Math.floor(state/65536);return 1+state%2147483646;
}
export function evaluateExpression(expression,seed,slot,context={}) {
  let state=1+(Math.max(0,seed)+hashSlot(slot)+(context.randomOffset||0)+Math.floor(context.randomFrame??(isDynamic(expression)?context.frame||0:0))*104729)%2147483646;
  if(context.randomMix)state=mixRandomSeed(Math.floor(state));
  const next=()=>{state=state*16807%2147483647;return (state-1)/2147483646;};
  function run(node){const value=calculate(node);if(!Number.isFinite(value))throw new Error('随机表达式的结果必须是有限数字，请检查除数和范围。');return value;}
  function calculate(node){if(node.op==='number')return node.number;if(globalReference(node.op)){const key=node.op.slice(7);if(!context.globals||!(key in context.globals))throw new Error('未定义全局变量：'+node.op);return context.globals[key];}if(expressionVariables.includes(node.op)){if(node.op==='pi')return Math.PI;if(node.op==='time')return (context.frame||0)/(context.fps||50);if(node.op==='u')return (context.n||1)<=1?0:(context.i||0)/(context.n-1);return context[node.op]??(node.op==='player.x'?400:node.op==='player.y'?304:node.op==='fps'?50:node.op==='n'?1:0);}const a=node.args.map(run);
    switch(node.op){case 'point_direction':return a[0]===a[2]&&a[1]===a[3]?0:(Math.atan2(a[1]-a[3],a[2]-a[0])*180/Math.PI+360)%360;case 'point_distance':return Math.hypot(a[2]-a[0],a[3]-a[1]);case 'lengthdir_x':return a[0]*Math.cos(a[1]*Math.PI/180);case 'lengthdir_y':return -a[0]*Math.sin(a[1]*Math.PI/180);case 'sin':return Math.sin(a[0]);case 'cos':return Math.cos(a[0]);case 'abs':return Math.abs(a[0]);case 'sqrt':return Math.sqrt(a[0]);case 'floor':return Math.floor(a[0]);case 'ceil':return Math.ceil(a[0]);case 'min':return Math.min(...a);case 'max':return Math.max(...a);case 'clamp':return Math.max(a[1],Math.min(a[2],a[0]));case 'lerp':return a[0]+(a[1]-a[0])*a[2];case 'turn_towards':return turnTowards(...a);case 'neg':return -a[0];case '+':return a[0]+a[1];case '-':return a[0]-a[1];case '*':return a[0]*a[1];case '/':return a[0]/a[1];case '%':return a[0]%a[1];case 'random':return next()*a[0];case 'irandom':return Math.floor(next()*(Math.floor(Math.abs(a[0]))+1));case 'choose':return a[Math.floor(next()*a.length)];default:{const u=next(),lo=Math.min(...a),hi=Math.max(...a);return node.op==='irandom_range'?Math.ceil(lo)+Math.floor(u*(Math.floor(hi)-Math.ceil(lo)+1)):lo+(hi-lo)*u;}}
  }
  const result=run(parseExpression(expression));if(!Number.isFinite(result))throw new Error('随机表达式的结果必须是有限数字，请检查除数和范围。');return result;
}
export function clampParameter(field,value) {
  if(field==='shapeAspect')return Math.max(-10,Math.min(10,value));if(field==='shapeInner')return Math.max(.01,Math.min(1,value));
  if(field==='cameraIntensity')return Math.max(0,Math.min(3,value));if(field==='fxFlash')return Math.max(0,Math.min(1,value));if(field==='fxZoom')return Math.max(1,Math.min(3,value));if(field==='cameraBlur')return Math.max(0,Math.min(100,value));
  if(field==='homingDelay')return Math.max(0,Math.round(value));if(field==='turnRate')return Math.max(0,value);
  if(['alpha','fxOpacity','fxRetention','trailStartAlpha'].includes(field))return Math.max(0,Math.min(1,value));
  if(field==='count')return Math.max(1,Math.min(300,Math.round(value)));
  if(field==='sides')return Math.max(3,Math.min(64,Math.round(value)));
  if(field==='fxSamples')return Math.max(2,Math.min(16,Math.round(value)));
  if(['interval','life','trailInterval','trailLife'].includes(field))return Math.max(1,Math.round(value));
  if(field==='cameraZoom')return Math.max(.05,Math.min(20,value));
  if(['radius','length','strength','rainbowDensity'].includes(field))return Math.max(0,value);
  if(field==='easePower')return Math.max(.1,value);
  if(['easePeriod','fxFrequency'].includes(field))return Math.max(.01,value);return value;
}
const numericFields=new Set(['controlFrom','controlValue','eventAcceleration','eventTurn','indexSpeed','indexAngle','indexScale','emitWidth','emitHeight','emitArc','emitPhase','emitRotation','emitWaves','x','y','angle','scale','alpha','speed','spread','count','life','interval','x1','y1','x2','y2','length','rollSpeed','rainbowDensity','rainbowOffset','rainbowSpeed','spriteAngle','strength','fxOpacity','fxAngle','fxRetention','radius','orientation','sides','shapeAspect','shapeInner','cameraZoom','velocityX','velocityY','accelerationX','accelerationY','angularSpeed','orbitRadius','orbitSpeed','orbitPhase','pathStartX','pathStartY','pathEndX','pathEndY','pathPeakX','pathPeakY','pathProgress','trailInterval','trailLife','trailScaleDecay','trailAlphaDecay','trailStartAlpha','trailSpeed','trailDirection','easePower','easePeriod','fxSamples','fxFrequency','turnRate','homingDelay','cameraIntensity','cameraBlur','cameraBlurAngle','fxZoom','fxFlash']);
export function randomBindings(track) {
  const base=Object.entries(track.expressions||{}).map(([property,expression])=>({property,expression,slot:'base',key:null}));
  return base.concat((track.keyframes||[]).filter(k=>k.expression?.trim()).map(k=>({property:k.property,expression:k.expression,slot:k.randomId||`${k.property}:${k.frame}`,key:k})));
}
export function resolveRandomState(state) {
  state.randomSeed=Number.isInteger(state.randomSeed)?Math.max(0,state.randomSeed):12345;state.randomizeOnPlay=!!state.randomizeOnPlay;attachGlobals(state);
  for(const track of state.tracks){track.randomId||=track.id;track.expressions??={};Object.defineProperty(track,'_expressionContext',{value:{...expressionContext(track,track.start,state),seed:state.randomSeed,globalState:state},writable:true,configurable:true});
    for(const binding of randomBindings(track)){
      if(!numericFields.has(binding.property))throw new Error('不支持随机的属性：'+binding.property);
      const value=clampParameter(binding.property,evaluateExpression(binding.expression,state.randomSeed,`${track.randomId}|${binding.property}|${binding.slot}`,expressionContext(track,binding.key?.frame??track.start,state)));
      if(binding.key){binding.key.randomId=binding.slot;binding.key.value=value;}else if(!isDynamic(binding.expression))track[binding.property]=value;
    }
  }return state;
}
function variable(track,property,key){const slot=key?`${key.frame}:${key.randomId||key.property}`:'base';return 'objRufController.rf_random_'+crypto.createHash('sha256').update(`${track.id}|${property}|${slot}`).digest('hex').slice(0,16);}
export function parameterCode(track,property,fallback=0,key=null,context={}) {
  const expression=key?.expression||!key&&track.expressions?.[property];
  if(expression&&context.birthSampling)return clampCode(property,expressionCall(expression,track,property,key,context));
  if(expression&&isDynamic(expression))return clampCode(property,expressionCall(expression,track,property,key,context));
  return (key?.expression||!key&&track.expressions?.[property])?variable(track,property,key):num(key?key.value:track[property],fallback);
}
function clampCode(field,v) {
  if(field==='shapeAspect')return `max(-10,min(10,${v}))`;if(field==='shapeInner')return `max(.01,min(1,${v}))`;
  if(field==='cameraIntensity')return `max(0,min(3,${v}))`;if(field==='fxFlash')return `max(0,min(1,${v}))`;if(field==='fxZoom')return `max(1,min(3,${v}))`;if(field==='cameraBlur')return `max(0,min(100,${v}))`;
  if(['alpha','fxOpacity','fxRetention','trailStartAlpha'].includes(field))return `max(0,min(1,${v}))`;
  if(field==='count')return `max(1,min(300,round(${v})))`;if(field==='sides')return `max(3,min(64,round(${v})))`;if(field==='fxSamples')return `max(2,min(16,round(${v})))`;
  if(['interval','life','trailInterval','trailLife'].includes(field))return `max(1,round(${v}))`;
  if(field==='cameraZoom')return `max(.05,min(20,${v}))`;
  if(['radius','length','strength','rainbowDensity'].includes(field))return `max(0,${v})`;
  if(field==='easePower')return `max(.1,${v})`;if(['easePeriod','fxFrequency'].includes(field))return `max(.01,${v})`;return v;
}
export const randomNextGml='rf_rng_state=(rf_rng_state*16807) mod 2147483647;return (rf_rng_state-1)/2147483646;\n';
export function randomInitialization(state) {
  const lines=[state.randomizeOnPlay?'rf_random_seed=1+floor((current_time+date_get_second(date_current_datetime())*104729) mod 2147483646);':`rf_random_seed=${num(state.randomSeed,12345)};`];
  const compiler=createCompiler(lines);
  const seen=new Set();for(const t of state.tracks)for(const b of randomBindings(t)){
    const target=variable(t,b.property,b.key);if(seen.has(target)||isDynamic(b.expression))continue;seen.add(target);
    lines.push(`rf_rng_state=1+((rf_random_seed+${hashSlot(`${t.randomId||t.id}|${b.property}|${b.slot}`)}) mod 2147483646);`);
    const result=compiler.emit(parseExpression(b.expression));lines.push(`${target}=${clampCode(b.property,result)};`);
  }
  return (compiler.variables.length?`var ${compiler.variables.join(',')};\n`:'')+lines.join('\n');
}

function createCompiler(lines,{fps=50}={}){
  let index=0;const variables=[];
  function emit(node){const a=node.args.map(emit),v='rf_random_arg'+index++;variables.push(v);let value;
    if(globalReference(node.op)){lines.push(`${v}=${node.op};`);return v;}
    switch(node.op){case 'number':value=num(node.number);break;
      case 'pi':value='pi';break;
      case 'player.x':value='rf_expression_player(0)';break;case 'player.y':value='rf_expression_player(1)';break;
      case 'time':value='(argument2/'+num(fps,50)+')';break;case 'fps':value=num(fps,50);break;
      case 'source.x':value='argument11';break;case 'source.y':value='argument15';break;
      case 'i':value='argument12';break;case 'n':value='argument13';break;case 'volley':value='argument14';break;case 'u':value='(argument12/max(1,argument13-1))';break;
      case 'x':case 'y':case 'frame':case 'age':case 'direction':case 'speed':case 'x1':case 'y1':case 'x2':case 'y2':value='argument'+['x','y','frame','age','direction','speed','x1','y1','x2','y2'].indexOf(node.op);break;
      case 'point_direction':case 'point_distance':case 'lengthdir_x':case 'lengthdir_y':case 'sin':case 'cos':case 'abs':case 'sqrt':case 'floor':case 'ceil':case 'min':case 'max':value=node.op+'('+a.join(',')+')';break;
      case 'clamp':value='max('+a[1]+',min('+a[2]+','+a[0]+'))';break;
      case 'lerp':value='('+a[0]+'+('+a[1]+'-'+a[0]+')*'+a[2]+')';break;
      case 'turn_towards':value='('+a[0]+'+max(-abs('+a[2]+'),min(abs('+a[2]+'),((('+a[1]+'-'+a[0]+') mod 360+540) mod 360)-180)))';break;case 'neg':value=`(-${a[0]})`;break;case '+':case '-':case '*':case '/':value=`(${a[0]}${node.op}${a[1]})`;break;case '%':value=`(${a[0]} mod ${a[1]})`;break;case 'random':value=`rf_random_next()*${a[0]}`;break;case 'irandom':value=`floor(rf_random_next()*(floor(abs(${a[0]}))+1))`;break;case 'choose':lines.push(`${v}=floor(rf_random_next()*${a.length});`);a.forEach((arg,i)=>lines.push(`${i?'else ':''}if ${v}=${i} ${v}=${arg};`));return v;default:value=node.op==='irandom_range'?`ceil(min(${a.join(',')}))+floor(rf_random_next()*(floor(max(${a.join(',')}))-ceil(min(${a.join(',')}))+1))`:`min(${a.join(',')})+(max(${a.join(',')})-min(${a.join(',')}))*rf_random_next()`;}
    lines.push(`${v}=${value};`);return v;
  }
  return {emit,variables};
}
export function expressionScriptName(expression){return 'rf_expr_'+crypto.createHash('sha256').update(expression).digest('hex').slice(0,16);}
export function expressionCall(expression,t,property='flight',key=null,context={}){
  const time=context.frame||'rf_frame',position=property==='x'||property==='y';
  const slot=num(hashSlot((t.randomId||t.id)+'|'+property+'|'+(key?.randomId||'base')));
  const randomSlot=context.randomOffset?`(${slot}+${context.randomOffset})`:slot,randomFrame=context.randomFrame??(isDynamic(expression)?time:'0');
  // Keep the GM8.2 16-argument limit: precompute the same RNG state and compile FPS as a constant.
  const initial=`(1+((objRufController.rf_random_seed+${randomSlot}+floor(${randomFrame})*104729) mod 2147483646))`;
  const seed=context.randomMix?`rf_random_mix(${initial})`:initial;
  const args=[context.x||num(t.x,400),context.y||num(t.y,304),time,context.age||('max(0,'+time+'-('+num(t.clockStart??t.start)+'))'),context.direction||num(t.angle),context.speed||num(t.speed,3),context.x1||num(t.x1,280),context.y1||num(t.y1,304),context.x2||num(t.x2,520),context.y2||num(t.y2,304),seed,context.sourceX??num(t.x,400),context.i||'0',context.n||'1',context.volley||'0',context.sourceY??num(t.y,304)];
  return expressionScriptName(expression)+'('+args.join(',')+')';
}
export function expressionScripts(state,players){
  const scripts=[];const seen=new Set();for(const t of state.tracks){const expressions=randomBindings(t).map(b=>b.expression);if(t.colorMode==='random')expressions.push(numericColor(t.colorExpression));for(const a of [...(t.spawnActions||[]),...(t.bulletActions||[])]){if(a.spawnColorExpression)expressions.push(numericColor(a.spawnColorExpression));expressions.push(...Object.values(a.expressions||{}));}if(t.kind==='variable')expressions.push(t.variableExpression);if([...t.spawnActions,...t.bulletActions].some(a=>a.spawnDirectionMode==='random'))expressions.push('random(360)');if(t.kind==='emitter'&&t.flightMode==='expression')expressions.push('('+t.flightExpression+')+0*frame');for(const a of t.bulletActions||[])if(a.expression)expressions.push(a.expression);if(t.kind==='control'&&t.controlExpression)expressions.push(t.controlExpression);
    for(const expression of expressions){if(seen.has(expression))continue;seen.add(expression);const lines=['rf_rng_state=argument10;'];const compiler=createCompiler(lines,{fps:state.fps});const result=compiler.emit(parseExpression(expression));scripts.push([expressionScriptName(expression),'var '+compiler.variables.join(',')+';\n'+lines.join('\n')+'\nreturn '+result+';\n']);}}
  if(state.tracks.some(t=>t.randomPerInstance||t.eventPreset))scripts.push(['rf_random_mix','var rf_seed;rf_seed=floor(argument0);\nrf_seed=rf_seed ^ floor(rf_seed/65536);rf_seed=(rf_seed*48271) mod 2147483647;\nrf_seed=rf_seed ^ floor(rf_seed/32768);rf_seed=(rf_seed*69621) mod 2147483647;\nrf_seed=rf_seed ^ floor(rf_seed/65536);return 1+(rf_seed mod 2147483646);\n']);
  scripts.push(['rf_expression_player',"var rf_p;rf_p=noone;\n"+players.map(p=>'if rf_p=noone && instance_exists('+p+') rf_p=instance_find('+p+',0);').join('\n')+'\nif rf_p!=noone {if argument0=0 return rf_p.x;return rf_p.y;}\nif argument0=0 return '+num(state.nativeTest?.x,400)+';return '+num(state.nativeTest?.y,304)+';\n']);return scripts;
}
