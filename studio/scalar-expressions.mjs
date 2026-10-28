import {expressionCall} from './random-parameters.mjs';
import {gmlNumber as num} from './gml.mjs';
export function actionValue(source,a,property,context={}){
  const text=a.expressions?.[property];return text?expressionCall(text,source,'actionField'+[...source.spawnActions,...source.bulletActions].indexOf(a)+':'+property,null,context):num(a[property]);
}

export function scaleBinding(values,field,factor){if(factor!==1&&values?.[field])values[field]='('+values[field]+')*'+num(factor);}
export function scaleActionExpressions(a,ratio){if(['speed','turn','home','acceleration'].includes(a.type)){const factor=1/(a.type==='acceleration'?ratio**2:ratio);for(const field of ['from','to'])scaleBinding(a.expressions,field,factor);}scaleBinding(a.expressions,'childSpeed',1/ratio);}
export function scaleEvent(t,ratio){const divisor=t.eventPreset==='group-set'?(['speed','turn'].includes(t.eventProperty)?ratio:t.eventProperty==='acceleration'?ratio**2:1):ratio;t.controlValue/=divisor;t.eventAcceleration/=ratio**2;t.eventTurn/=ratio;t.eventCopyLife=Math.max(1,Math.round(t.eventCopyLife*ratio));scaleBinding(t.expressions,'controlValue',1/divisor);scaleBinding(t.expressions,'eventAcceleration',1/ratio**2);scaleBinding(t.expressions,'eventTurn',1/ratio);}
