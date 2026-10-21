import {gmlNumber as num} from './gml.mjs';
import crypto from 'node:crypto';

export function parseExpression(text) {
  const source=String(text??'').replaceAll('（','(').replaceAll('）',')').replaceAll('，',',');let at=0;
  const space=()=>{while(/\s/.test(source[at]||'!'))at++;};
  const take=c=>{space();if(source[at]!==c)return false;at++;return true;};
  const error=()=>new Error(`随机表达式格式错误（位置 ${at+1}）；支持 random、random_range、choose 和四则运算。`);
  const binary=(next,operators)=>{let node=next();while(true){space();if(at>=source.length||!operators.includes(source[at]))return node;const op=source[at++];node={op,args:[node,next()]};}};
  const sum=()=>binary(product,'+-'),product=()=>binary(atom,'*/%');
  function atom() {
    if(take('+'))return atom();if(take('-'))return {op:'neg',args:[atom()]};
    if(take('(')){const result=sum();if(!take(')'))throw error();return result;}
    space();const match=source.slice(at).match(/^(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?/);
    if(match){at+=match[0].length;return {op:'number',number:Number(match[0]),args:[]};}
    const name=source.slice(at).match(/^[a-z_]+/i)?.[0]?.toLowerCase();if(!['random','random_range','choose','irandom','irandom_range'].includes(name))throw error();at+=name.length;if(!take('('))throw error();
    const args=[];if(!take(')')){do{args.push(sum());}while(take(','));if(!take(')'))throw error();}
    if(['random','irandom'].includes(name)&&args.length!==1||['random_range','irandom_range'].includes(name)&&args.length!==2||name==='choose'&&!args.length)throw error();return {op:name,args};
  }
  const result=sum();space();if(at!==source.length)throw error();return result;
}
export function hashSlot(text){let value=0;for(let i=0;i<text.length;i++)value=(value*31+text.charCodeAt(i))%2147483647;return value;}
export function evaluateExpression(expression,seed,slot) {
  let state=1+(Math.max(0,seed)+hashSlot(slot))%2147483646;
  const next=()=>{state=state*16807%2147483647;return (state-1)/2147483646;};
  function run(node){const value=calculate(node);if(!Number.isFinite(value))throw new Error('随机表达式的结果必须是有限数字，请检查除数和范围。');return value;}
  function calculate(node){if(node.op==='number')return node.number;const a=node.args.map(run);
    switch(node.op){case 'neg':return -a[0];case '+':return a[0]+a[1];case '-':return a[0]-a[1];case '*':return a[0]*a[1];case '/':return a[0]/a[1];case '%':return a[0]%a[1];case 'random':return next()*a[0];case 'irandom':return Math.floor(next()*(Math.floor(Math.abs(a[0]))+1));case 'choose':return a[Math.floor(next()*a.length)];default:{const u=next(),lo=Math.min(...a),hi=Math.max(...a);return node.op==='irandom_range'?Math.ceil(lo)+Math.floor(u*(Math.floor(hi)-Math.ceil(lo)+1)):lo+(hi-lo)*u;}}
  }
  const result=run(parseExpression(expression));if(!Number.isFinite(result))throw new Error('随机表达式的结果必须是有限数字，请检查除数和范围。');return result;
}
export function clampParameter(field,value) {
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
const numericFields=new Set(['x','y','angle','scale','alpha','speed','spread','count','life','interval','x1','y1','x2','y2','length','rollSpeed','rainbowDensity','rainbowOffset','rainbowSpeed','spriteAngle','strength','fxOpacity','fxAngle','fxRetention','radius','orientation','sides','cameraZoom','velocityX','velocityY','accelerationX','accelerationY','angularSpeed','orbitRadius','orbitSpeed','orbitPhase','pathStartX','pathStartY','pathEndX','pathEndY','pathPeakX','pathPeakY','pathProgress','trailInterval','trailLife','trailScaleDecay','trailAlphaDecay','trailStartAlpha','trailSpeed','trailDirection','easePower','easePeriod','fxSamples','fxFrequency']);
export function randomBindings(track) {
  const base=Object.entries(track.expressions||{}).map(([property,expression])=>({property,expression,slot:'base',key:null}));
  return base.concat((track.keyframes||[]).filter(k=>k.expression?.trim()).map(k=>({property:k.property,expression:k.expression,slot:k.randomId||`${k.property}:${k.frame}`,key:k})));
}
export function resolveRandomState(state) {
  state.randomSeed=Number.isInteger(state.randomSeed)?Math.max(0,state.randomSeed):12345;state.randomizeOnPlay=!!state.randomizeOnPlay;
  for(const track of state.tracks){track.randomId||=track.id;track.expressions??={};
    for(const binding of randomBindings(track)){
      if(!numericFields.has(binding.property))throw new Error('不支持随机的属性：'+binding.property);
      const value=clampParameter(binding.property,evaluateExpression(binding.expression,state.randomSeed,`${track.randomId}|${binding.property}|${binding.slot}`));
      if(binding.key){binding.key.randomId=binding.slot;binding.key.value=value;}else track[binding.property]=value;
    }
  }return state;
}
function variable(track,property,key){const slot=key?`${key.frame}:${key.randomId||key.property}`:'base';return 'objRufController.rf_random_'+crypto.createHash('sha256').update(`${track.id}|${property}|${slot}`).digest('hex').slice(0,16);}
export function parameterCode(track,property,fallback=0,key=null) {
  return (key?.expression||!key&&track.expressions?.[property])?variable(track,property,key):num(key?key.value:track[property],fallback);
}
function clampCode(field,v) {
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
  let index=0;const variables=[];
  function emit(node){const a=node.args.map(emit),v='rf_random_arg'+index++;variables.push(v);let value;
    switch(node.op){case 'number':value=num(node.number);break;case 'neg':value=`(-${a[0]})`;break;case '+':case '-':case '*':case '/':value=`(${a[0]}${node.op}${a[1]})`;break;case '%':value=`(${a[0]} mod ${a[1]})`;break;case 'random':value=`rf_random_next()*${a[0]}`;break;case 'irandom':value=`floor(rf_random_next()*(floor(abs(${a[0]}))+1))`;break;case 'choose':lines.push(`${v}=floor(rf_random_next()*${a.length});`);a.forEach((arg,i)=>lines.push(`${i?'else ':''}if ${v}=${i} ${v}=${arg};`));return v;default:value=node.op==='irandom_range'?`ceil(min(${a.join(',')}))+floor(rf_random_next()*(floor(max(${a.join(',')}))-ceil(min(${a.join(',')}))+1))`:`min(${a.join(',')})+(max(${a.join(',')})-min(${a.join(',')}))*rf_random_next()`;}
    lines.push(`${v}=${value};`);return v;
  }
  const seen=new Set();for(const t of state.tracks)for(const b of randomBindings(t)){
    const target=variable(t,b.property,b.key);if(seen.has(target))continue;seen.add(target);
    lines.push(`rf_rng_state=1+((rf_random_seed+${hashSlot(`${t.randomId||t.id}|${b.property}|${b.slot}`)}) mod 2147483646);`);
    const result=emit(parseExpression(b.expression));lines.push(`${target}=${clampCode(b.property,result)};`);
  }
  return (variables.length?`var ${variables.join(',')};\n`:'')+lines.join('\n');
}
