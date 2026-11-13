import {expressionCall} from './random-parameters.mjs';
import {gmlNumber as num} from './gml.mjs';
export function actionValue(source,a,property,context={}){
  const text=a.expressions?.[property];return text?expressionCall(text,source,'actionField'+[...source.spawnActions,...source.bulletActions].indexOf(a)+':'+property,null,context):num(a[property]);
}

export function scaleBinding(values,field,factor){if(factor!==1&&values?.[field])values[field]='('+values[field]+')*'+num(factor);}
export function scaleActionExpressions(a,ratio){if(['speed','turn','home','acceleration','gravity'].includes(a.type)){const factor=1/(['acceleration','gravity'].includes(a.type)?ratio**2:ratio);for(const field of ['from','to'])scaleBinding(a.expressions,field,factor);if(a.type==='gravity'&&a.expression&&ratio!==1)a.expression='('+a.expression+')*'+num(factor);}scaleBinding(a.expressions,'childSpeed',1/ratio);}
export function scaleEvent(t,ratio){const divisor=t.eventPreset==='group-set'&&['scale','alpha','color'].includes(t.eventProperty)?1:ratio;t.controlValue/=divisor;t.eventAcceleration/=ratio**2;t.eventFriction/=ratio**2;t.eventTurn/=ratio;t.eventOrbitSpeed/=ratio;t.eventCopyLife=Math.max(1,Math.round(t.eventCopyLife*ratio));if(t.eventLife>0)t.eventLife=Math.max(1,Math.round(t.eventLife*ratio));scaleBinding(t.expressions,'controlValue',1/divisor);scaleBinding(t.expressions,'eventAcceleration',1/ratio**2);scaleBinding(t.expressions,'eventFriction',1/ratio**2);scaleBinding(t.expressions,'eventTurn',1/ratio);scaleBinding(t.expressions,'eventOrbitSpeed',1/ratio);}
