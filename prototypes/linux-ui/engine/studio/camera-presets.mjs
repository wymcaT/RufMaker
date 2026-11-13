import {gmlNumber as n} from './gml.mjs';

export const cameraPresetNames=['变速推拉','甩镜过渡','旋转回弹','自然手持漂移','柔和摇摆','呼吸拉镜','缓慢推进','缓慢拉远','旋转缩放过渡','横向甩镜过渡','冲击回弹','轻微手持'];
export const cameraPresetFields=['x','y','angle','cameraZoom','cameraBlur','cameraBlurAngle'];
export const cameraBound=t=>t.kind==='camera'&&!!t.cameraPreset&&cameraPresetNames.includes(t.cameraPreset.name);

function cameraDirect(d,frame,fps,ease,progress){
  const u=progress??Math.max(0,Math.min(1,(frame-d.start)*d.speed/Math.max(1,d.end-d.start))),sec=u*Math.max(1,d.end-d.start)/Math.max(1,fps);let x=d.centerX,y=d.centerY,angle=d.baseAngle,zoom=d.baseZoom,blur=0,blurAngle=0;
  if(d.name==='变速推拉'){
    let k=0;if(u<.1)k=-.035*ease('smooth',u/.1);else if(u<.21)k=-.035+.43*ease('expo-out',(u-.1)/.11);else if(u<.8)k=.395*(1-ease('smooth',(u-.21)/.59));zoom*=1+d.zoomAmplitude*k/.43;blur=d.blurAmplitude*Math.max(0,1-Math.abs(u-.16)/.08);
  }else if(d.name==='甩镜过渡'){
    const v=u<.5?ease('expo-in',u*2):-(1-ease('expo-out',(u-.5)*2));x+=d.amplitudeX*v;angle+=d.angleAmplitude*v;zoom*=1+d.zoomAmplitude*Math.sin(Math.PI*u);blur=d.blurAmplitude*Math.sin(Math.PI*u)**4;
  }else if(d.name==='旋转回弹'){
    const s=u*1.6;angle+=d.angleAmplitude*Math.exp(-4.8*s)*Math.sin(18*s);zoom*=1+d.zoomAmplitude*Math.exp(-4*s)*Math.sin(10*s);blur=d.blurAmplitude*Math.exp(-10*s);blurAngle=45;
  }else if(d.name==='自然手持漂移'){
    const env=ease('sin-inout',Math.min(1,Math.min(u/.08,(1-u)/.08)));x+=env*d.amplitudeX*(Math.sin(sec*1.6)+Math.sin(sec*4.1+.6)/3+Math.sin(sec*8.7)/6);y+=env*d.amplitudeY*(Math.sin(sec*1.1+1.4)+.4*Math.sin(sec*3.7)+.12*Math.sin(sec*10.2));angle+=env*d.angleAmplitude*(Math.sin(sec*1.3)+.225*Math.sin(sec*4.6+1.3));zoom*=1+env*d.zoomAmplitude*(1+(.006/.065)*Math.sin(sec*1.2));
  }else if(d.name==='柔和摇摆'||d.name==='轻微手持'){
    const env=Math.sin(Math.PI*u),phase=sec*2*Math.PI/3,hand=d.name==='轻微手持';x+=env*d.amplitudeX*(hand?Math.sin(1.7*phase)+.6*Math.sin(4.3*phase):Math.sin(phase));y+=env*d.amplitudeY*(hand?Math.cos(2.3*phase)+.5*Math.sin(5.1*phase):Math.sin(1.3*phase));angle+=env*d.angleAmplitude*Math.sin((hand?2.1:.8)*phase);zoom*=1+d.zoomAmplitude*env;
  }else if(d.name==='呼吸拉镜')zoom*=1+d.zoomAmplitude*(1-Math.cos(4*Math.PI*u))/2;
  else if(d.name==='缓慢推进'||d.name==='缓慢拉远'){const w=ease('sin-inout',u);zoom*=1+d.zoomAmplitude*(d.name==='缓慢推进'?w:1-w);}
  else if(d.name==='旋转缩放过渡'){
    if(u<.4){const w=ease('sin-inout',u/.4);zoom*=1+d.zoomAmplitude*w;angle-=d.angleAmplitude*w;}
    else if(u<.6){const w=ease('sin-inout',(u-.4)/.2);zoom*=1+d.zoomAmplitude;angle+=d.angleAmplitude*(2*w-1);}
    else{const w=ease('sin-inout',(u-.6)/.4);zoom*=1+d.zoomAmplitude*(1-w);angle+=d.angleAmplitude*(1-w);}
  }else if(d.name==='横向甩镜过渡'){
    if(u<.45)x+=d.amplitudeX*ease('expo-in',u/.45);else if(u<.55)x+=d.amplitudeX;else x-=d.amplitudeX*(1-ease('expo-out',(u-.55)/.45));zoom*=1+d.zoomAmplitude*(u<.5?ease('sin-inout',u*2):1-ease('sin-inout',(u-.5)*2));
  }else if(d.name==='冲击回弹')zoom*=1+d.zoomAmplitude*(u<.2?ease('expo-out',u/.2):1-ease('expsin',(u-.2)/.8));
  if(u>=1&&d.name==='横向甩镜过渡')x=d.centerX;
  if(u>=1&&(d.name==='冲击回弹'||d.name==='旋转缩放过渡')){zoom=d.baseZoom;if(d.name==='旋转缩放过渡')angle=d.baseAngle;}
  if(['变速推拉','甩镜过渡','旋转回弹','自然手持漂移'].includes(d.name)&&(u<=0||u>=1)){x=d.centerX;y=d.centerY;angle=d.baseAngle;zoom=d.baseZoom;blur=0;}
  return {x,y,angle,cameraZoom:zoom,cameraBlur:blur,cameraBlurAngle:blurAngle};
}

const legacyCache=new WeakMap();
function legacyCurves(d,fps,ease){
  const signature=JSON.stringify(d)+'|'+fps,cached=legacyCache.get(d);if(cached?.signature===signature)return cached.curves;
  const data=new Map(),length=d.legacyLength>0?d.legacyLength:Math.max(1,d.end-d.start),sourceFps=d.legacyFps>0?d.legacyFps:fps,sourceStart=d.legacyLength>0?d.legacyStart:d.start,copy={...d,start:sourceStart,end:sourceStart+length,speed:1,legacySampling:false},roundEven=v=>{const base=Math.floor(v),fraction=v-base;return fraction===.5?(base%2?base+1:base):Math.round(v);},roundAway=v=>v<0?Math.ceil(v-.5):Math.floor(v+.5);
  const add=(field,u,curve)=>{const value=cameraDirect(copy,0,sourceFps,ease,u)[field];let frame=sourceStart+roundEven(length*u);for(const step of d.legacyTime??[])frame=step.origin+roundAway((frame-step.subtract)*step.scale);data.set(field+':'+frame,{property:field,frame,value,easing:curve});};
  if(['变速推拉','甩镜过渡','旋转回弹','自然手持漂移'].includes(d.name)){const count=Math.min(240,length);for(let i=0;i<=count;i++)for(const field of cameraPresetFields)add(field,i/count,'linear');}
  else if(d.name==='缓慢推进'||d.name==='缓慢拉远'){add('cameraZoom',0,'sin-inout');add('cameraZoom',1,'linear');}
  else if(d.name==='旋转缩放过渡'){for(const u of [0,.4,.6,1]){add('cameraZoom',u,'sin-inout');add('angle',u,'sin-inout');}}
  else if(d.name==='横向甩镜过渡'){add('x',0,'expo-in');add('x',.45,'step');add('x',.55,'expo-out');add('x',1,'linear');add('cameraZoom',0,'sin-inout');add('cameraZoom',.5,'sin-inout');add('cameraZoom',1,'linear');}
  else if(d.name==='冲击回弹'){add('cameraZoom',0,'expo-out');add('cameraZoom',.2,'expsin');add('cameraZoom',1,'linear');}
  else{const count=Math.max(8,Math.min(160,Math.floor(length/Math.max(1,Math.floor(sourceFps/10)))));for(let i=0;i<=count;i++){const u=i/count;if(d.name==='呼吸拉镜')add('cameraZoom',u,'linear');else for(const field of ['x','y','angle','cameraZoom'])add(field,u,'sin-inout');}}
  const curves=Object.fromEntries(cameraPresetFields.map(field=>[field,[...data.values()].filter(k=>k.property===field).sort((a,b)=>a.frame-b.frame)]));legacyCache.set(d,{signature,curves});return curves;
}
export function cameraSample(d,frame,fps,ease){
  if(!d.legacySampling)return cameraDirect(d,frame,fps,ease);const curves=legacyCurves(d,fps,ease),at=d.start+(frame-d.start)*d.speed,base={x:d.centerX,y:d.centerY,angle:d.baseAngle,cameraZoom:d.baseZoom,cameraBlur:0,cameraBlurAngle:0};
  for(const field of cameraPresetFields){const keys=curves[field];if(!keys.length)continue;if(at<=keys[0].frame){base[field]=keys[0].value;continue;}if(at>=keys.at(-1).frame){base[field]=keys.at(-1).value;continue;}let hi=keys.findIndex(k=>k.frame>at),a=keys[hi-1],b=keys[hi],u=ease(a.easing,(at-a.frame)/Math.max(1,b.frame-a.frame));base[field]=a.value+(b.value-a.value)*u;}
  return base;
}
function legacyCameraCode(d,time,fps,ease){
  const curves=legacyCurves(d,fps,ease),at=`(${n(d.start)}+((${time})-(${n(d.start)}))*${n(d.speed,1)})`,base={x:d.centerX,y:d.centerY,angle:d.baseAngle,cameraZoom:d.baseZoom,cameraBlur:0,cameraBlurAngle:0};
  return cameraPresetFields.map(field=>{const keys=curves[field];if(!keys.length)return `rf_${field}=${n(base[field])};`;
    const tree=(lo,hi)=>{if(hi-lo===1){const a=keys[lo],b=keys[hi];return `rf_u=rf_ease('${a.easing}',(${at}-(${n(a.frame)}))/${n(Math.max(1,b.frame-a.frame))},3,.3);rf_${field}=${n(a.value)}+(${n(b.value)}-${n(a.value)})*rf_u;`;}const mid=Math.floor((lo+hi)/2);return `if ${at}<${n(keys[mid].frame)} {${tree(lo,mid)}}else {${tree(mid,hi)}}`;};
    if(keys.length===1)return `rf_${field}=${n(keys[0].value)};`;
    return `if ${at}<=${n(keys[0].frame)} {rf_${field}=${n(keys[0].value)};}else if ${at}>=${n(keys.at(-1).frame)} {rf_${field}=${n(keys.at(-1).value)};}else {${tree(0,keys.length-1)}}`;
  }).join('');
}

export function cameraPresetCode(t,time,ease){
  if(!cameraBound(t))return '';const d=t.cameraPreset,fps=t._expressionContext?.fps??t._state?.fps??50;if(d.legacySampling)return legacyCameraCode(d,time,fps,ease);
  const L=n(Math.max(1,d.end-d.start)),sec=`(rf_u*${L}/${n(Math.max(1,fps))})`,E=(kind,x)=>`rf_ease('${kind}',${x},3,.3)`,out=[`rf_u=max(0,min(1,((${time})-(${n(d.start)}))*${n(d.speed,1)}/${L}));rf_x=${n(d.centerX)};rf_y=${n(d.centerY)};rf_angle=${n(d.baseAngle)};rf_cameraZoom=${n(d.baseZoom,1)};rf_cameraBlur=0;rf_cameraBlurAngle=0;`],X=n(d.amplitudeX),Y=n(d.amplitudeY),A=n(d.angleAmplitude),Z=n(d.zoomAmplitude),B=n(d.blurAmplitude);
  if(d.name==='变速推拉')out.push(`if rf_u<.1 rf_cameraZoom*=1+${Z}*(-.035*${E('smooth','rf_u/.1')})/.43;else if rf_u<.21 rf_cameraZoom*=1+${Z}*(-.035+.43*${E('expo-out','(rf_u-.1)/.11')})/.43;else if rf_u<.8 rf_cameraZoom*=1+${Z}*(.395*(1-${E('smooth','(rf_u-.21)/.59')}))/.43;rf_cameraBlur=${B}*max(0,1-abs(rf_u-.16)/.08);`);
  else if(d.name==='甩镜过渡')out.push(`if rf_u<.5 {rf_x+=${X}*${E('expo-in','rf_u*2')};rf_angle+=${A}*${E('expo-in','rf_u*2')};}else {rf_x-=${X}*(1-${E('expo-out','(rf_u-.5)*2')});rf_angle-=${A}*(1-${E('expo-out','(rf_u-.5)*2')});}rf_cameraZoom*=1+${Z}*sin(pi*rf_u);rf_cameraBlur=${B}*power(sin(pi*rf_u),4);`);
  else if(d.name==='旋转回弹')out.push(`rf_angle+=${A}*exp(-4.8*rf_u*1.6)*sin(18*rf_u*1.6);rf_cameraZoom*=1+${Z}*exp(-4*rf_u*1.6)*sin(10*rf_u*1.6);rf_cameraBlur=${B}*exp(-10*rf_u*1.6);rf_cameraBlurAngle=45;`);
  else if(d.name==='自然手持漂移'){const env=E('sin-inout','min(1,min(rf_u/.08,(1-rf_u)/.08))');out.push(`rf_x+=${env}*${X}*(sin(${sec}*1.6)+sin(${sec}*4.1+.6)/3+sin(${sec}*8.7)/6);rf_y+=${env}*${Y}*(sin(${sec}*1.1+1.4)+.4*sin(${sec}*3.7)+.12*sin(${sec}*10.2));rf_angle+=${env}*${A}*(sin(${sec}*1.3)+.225*sin(${sec}*4.6+1.3));rf_cameraZoom*=1+${env}*${Z}*(1+(.006/.065)*sin(${sec}*1.2));`);}
  else if(d.name==='柔和摇摆'||d.name==='轻微手持'){const h=d.name==='轻微手持',phase=`(${sec}*2*pi/3)`,env='sin(pi*rf_u)';out.push(`rf_x+=${env}*${X}*${h?`(sin(1.7*${phase})+.6*sin(4.3*${phase}))`:`sin(${phase})`};rf_y+=${env}*${Y}*${h?`(cos(2.3*${phase})+.5*sin(5.1*${phase}))`:`sin(1.3*${phase})`};rf_angle+=${env}*${A}*sin(${h?'2.1':'.8'}*${phase});rf_cameraZoom*=1+${Z}*${env};`);}
  else if(d.name==='呼吸拉镜')out.push(`rf_cameraZoom*=1+${Z}*(1-cos(4*pi*rf_u))/2;`);
  else if(d.name==='缓慢推进'||d.name==='缓慢拉远')out.push(`rf_cameraZoom*=1+${Z}*${d.name==='缓慢推进'?E('sin-inout','rf_u'):`(1-${E('sin-inout','rf_u')})`};`);
  else if(d.name==='旋转缩放过渡')out.push(`if rf_u<.4 {rf_cameraZoom*=1+${Z}*${E('sin-inout','rf_u/.4')};rf_angle-=${A}*${E('sin-inout','rf_u/.4')};}else if rf_u<.6 {rf_cameraZoom*=1+${Z};rf_angle+=${A}*(2*${E('sin-inout','(rf_u-.4)/.2')}-1);}else {rf_cameraZoom*=1+${Z}*(1-${E('sin-inout','(rf_u-.6)/.4')});rf_angle+=${A}*(1-${E('sin-inout','(rf_u-.6)/.4')});}`);
  else if(d.name==='横向甩镜过渡')out.push(`if rf_u<.45 rf_x+=${X}*${E('expo-in','rf_u/.45')};else if rf_u<.55 rf_x+=${X};else rf_x-=${X}*(1-${E('expo-out','(rf_u-.55)/.45')});if rf_u<.5 rf_cameraZoom*=1+${Z}*${E('sin-inout','rf_u*2')};else rf_cameraZoom*=1+${Z}*(1-${E('sin-inout','(rf_u-.5)*2')});`);
  else if(d.name==='冲击回弹')out.push(`if rf_u<.2 rf_cameraZoom*=1+${Z}*${E('expo-out','rf_u/.2')};else rf_cameraZoom*=1+${Z}*(1-${E('expsin','(rf_u-.2)/.8')});`);
  if(d.name==='横向甩镜过渡')out.push(`if rf_u>=1 rf_x=${n(d.centerX)};`);
  if(d.name==='冲击回弹'||d.name==='旋转缩放过渡')out.push(`if rf_u>=1 {rf_cameraZoom=${n(d.baseZoom,1)};${d.name==='旋转缩放过渡'?`rf_angle=${n(d.baseAngle)};`:''}}`);
  if(['变速推拉','甩镜过渡','旋转回弹','自然手持漂移'].includes(d.name))out.push(`if rf_u<=0 || rf_u>=1 {rf_x=${n(d.centerX)};rf_y=${n(d.centerY)};rf_angle=${n(d.baseAngle)};rf_cameraZoom=${n(d.baseZoom,1)};rf_cameraBlur=0;}`);
  return out.join('');
}
