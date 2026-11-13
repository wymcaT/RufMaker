import {gmlNumber as n} from './gml.mjs';

export const motionPresetNames=['抛物线运动','圆周运动','水平往返','垂直往返','水平滑入','水平滑出','向上漂浮','下坠弹跳','斜向穿越','S 形穿行','螺旋扩散','螺旋收束','八字运动','停顿后冲刺'];
export const motionBound=t=>!!t.motionPreset&&motionPresetNames.includes(t.motionPreset.name);
export function motionLegacyKeysMatch(t,ease){
  if(!motionBound(t))return true;const x=t.keyframes.filter(k=>k.property==='x').sort((a,b)=>a.frame-b.frame),y=t.keyframes.filter(k=>k.property==='y').sort((a,b)=>a.frame-b.frame);if(Object.hasOwn(t.expressions||{},'x')||Object.hasOwn(t.expressions||{},'y')||t.keyframeCycle?.keys?.some(k=>k.property==='x'||k.property==='y'))return false;if(!x.length&&!y.length)return true;
  if(x.length!==y.length)return false;
  for(let i=0;i<x.length;i++){if(x[i].frame!==y[i].frame||i>0&&x[i].frame!==x[i-1].frame+1)return false;const point=motionPoint(t.motionPreset,x[i].frame,ease);for(const [key,value] of [[x[i],Math.fround(point.x)],[y[i],Math.fround(point.y)]])if(key.expression||key.easing!=='linear'||Math.abs(key.value-value)>Math.max(.0001,Math.abs(value)*.0000001))return false;}
  return true;
}
export function motionPoint(d,frame,ease){
  const u=Math.max(0,Math.min(1,(frame-d.start)/Math.max(1,d.end-d.start))),v=1-u,a=2*Math.PI*(d.turns??1)*u+(d.phase??0)*Math.PI/180;let x=d.startX??0,y=d.startY??0;
  if(d.name==='抛物线运动'){x=v*v*d.startX+2*v*u*(2*d.peakX-(d.startX+d.endX)/2)+u*u*d.endX;y=v*v*d.startY+2*v*u*(2*d.peakY-(d.startY+d.endY)/2)+u*u*d.endY;}
  else if(['圆周运动','螺旋扩散','螺旋收束'].includes(d.name)){const r=d.radiusStart+(d.radiusEnd-d.radiusStart)*u;x=d.centerX+r*Math.cos(a);y=d.centerY-r*Math.sin(a);}
  else if(['水平往返','垂直往返'].includes(d.name)){const w=(1-Math.cos(a))/2;x+=d.amplitudeX*w;y+=d.amplitudeY*w;}
  else if(d.name==='八字运动'){x=d.centerX+d.amplitudeX*Math.sin(a);y=d.centerY+d.amplitudeY*Math.sin(2*a);}
  else{const progress=d.name==='停顿后冲刺'?Math.max(0,(u-d.hold)/Math.max(.001,1-d.hold)):u,w=ease(d.easing,progress);x+=(d.endX-d.startX)*w;y+=(d.endY-d.startY)*w;if(d.name==='向上漂浮')x+=d.amplitudeX*Math.sin(a);if(d.name==='S 形穿行')y+=d.amplitudeY*Math.sin(a);}
  return {x,y};
}
export function motionPresetCode(t,time){
  if(!motionBound(t))return '';const d=t.motionPreset,a=`(2*pi*${n(d.turns,1)}*rf_u+${n(d.phase)}*pi/180)`,p=`rf_u=max(0,min(1,((${time})-(${n(d.start)}))/${n(Math.max(1,d.end-d.start))}));`;
  if(d.name==='抛物线运动')return p+['X','Y'].map(axis=>`rf_${axis.toLowerCase()}=sqr(1-rf_u)*${n(d['start'+axis])}+2*(1-rf_u)*rf_u*(2*${n(d['peak'+axis])}-(${n(d['start'+axis])}+${n(d['end'+axis])})/2)+sqr(rf_u)*${n(d['end'+axis])};`).join('');
  if(['圆周运动','螺旋扩散','螺旋收束'].includes(d.name)){const r=`(${n(d.radiusStart)}+(${n(d.radiusEnd)}-${n(d.radiusStart)})*rf_u)`;return p+`rf_x=${n(d.centerX)}+${r}*cos(${a});rf_y=${n(d.centerY)}-${r}*sin(${a});`;}
  if(['水平往返','垂直往返'].includes(d.name))return p+`rf_x=${n(d.startX)}+${n(d.amplitudeX)}*(1-cos(${a}))/2;rf_y=${n(d.startY)}+${n(d.amplitudeY)}*(1-cos(${a}))/2;`;
  if(d.name==='八字运动')return p+`rf_x=${n(d.centerX)}+${n(d.amplitudeX)}*sin(${a});rf_y=${n(d.centerY)}+${n(d.amplitudeY)}*sin(2*${a});`;
  const progress=d.name==='停顿后冲刺'?`max(0,(rf_u-${n(d.hold,.6)})/max(.001,1-${n(d.hold,.6)}))`:'rf_u',w=`rf_ease('${d.easing||'linear'}',${progress},3,.3)`;
  return p+`rf_x=${n(d.startX)}+(${n(d.endX)}-${n(d.startX)})*${w}${d.name==='向上漂浮'?`+${n(d.amplitudeX)}*sin(${a})`:''};rf_y=${n(d.startY)}+(${n(d.endY)}-${n(d.startY)})*${w}${d.name==='S 形穿行'?`+${n(d.amplitudeY)}*sin(${a})`:''};`;
}
