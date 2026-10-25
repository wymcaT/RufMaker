import {gmlNumber as num} from './gml.mjs';

export const actionTypes=['speed','direction','scale','alpha','turn','acceleration','aim','home','collision','clear','split','spawn'];
export const emissionShapes=['point','line','ring','arc','ellipse','sine','bezier'];
export const bulletDefaults={advancedBullets:false,templateOnly:false,tag:'',parentId:'',emissionShape:'point',emissionFacing:'fan',randomScope:'bullet',emitWidth:240,emitHeight:160,emitArc:360,emitPhase:0,emitRotation:0,emitWaves:1,indexSpeed:0,indexAngle:0,indexScale:0,indexDelay:0,releaseFrame:-1,releaseDirection:'radial',repeatCount:1,repeatGap:0,timeScale:1,targetSource:'',targetTag:'',targetParity:'all',controlAction:'speed',controlExpression:'',controlEasing:'linear',targetVolley:-1,targetMinAge:0,targetMaxAge:1000000,includeFuture:false,controlFrom:0,controlValue:0,controlColor:'#FFFFFF'};
export function normalizeBulletState(s,depth=0){
  if(depth>8)throw new Error('片段嵌套不能超过 8 层');
  for(const t of s.tracks){for(const [key,value] of Object.entries(bulletDefaults))if(t[key]===undefined)t[key]=value;t.bulletActions??=[];t.spawnActions??=[];t.children??=[];t.curvePoints??=[];
    if(t.spawnActions.length>200||t.bulletActions.length>200||t.children.length>200||!emissionShapes.includes(t.emissionShape))throw new Error('无效弹幕行为或阵列');
    for(const a of [...t.bulletActions,...t.spawnActions]){for(const [k,v] of Object.entries({spawnOrigin:'center',spawnParity:'all',spawnRepeat:0,spawnIndexFrom:0,spawnIndexTo:-1,spawnOffsetX:0,spawnOffsetY:0,inheritAngle:false,destroyParent:false,type:'speed',easing:'linear',expression:'',templateId:'',enabled:true,relative:false,start:0,duration:30,stagger:0,count:5,maxGeneration:1,from:0,to:3,spread:20,childSpeed:3}))if(a[k]===undefined)a[k]=v;
      if(!actionTypes.includes(a.type)||![a.start,a.duration,a.stagger,a.from,a.to,a.count,a.spread,a.childSpeed,a.maxGeneration].every(Number.isFinite)||a.start<0||a.duration<0||a.stagger<0||a.count<1||a.count>300||a.maxGeneration<1||a.maxGeneration>4)throw new Error('弹幕行为参数无效');if(a.type==='spawn'&&(!['center','each'].includes(a.spawnOrigin)||!['all','even','odd'].includes(a.spawnParity)||![a.spawnRepeat,a.spawnIndexFrom,a.spawnIndexTo].every(Number.isInteger)||a.spawnRepeat<0||a.spawnIndexFrom<0||a.spawnIndexTo< -1||![a.spawnOffsetX,a.spawnOffsetY].every(Number.isFinite)))throw new Error('生成模板参数无效');}
    if(!Number.isInteger(t.indexDelay)||t.indexDelay<0||t.indexDelay>1000000||!Number.isInteger(t.repeatCount)||t.repeatCount<1||t.repeatCount>100||!Number.isFinite(t.timeScale)||t.timeScale<.1||t.timeScale>10||!Number.isInteger(t.repeatGap)||t.repeatGap<0)throw new Error('阵列延迟或片段循环设置无效');
    if(t.kind==='pattern')normalizeBulletState({tracks:t.children},depth+1);
  }
  for(const t of s.tracks){let p=s.tracks.find(p=>p.id===t.parentId&&p.kind==='group'),seen=new Set([t.id]);while(p){if(seen.has(p.id))throw new Error('分组不能循环引用');seen.add(p.id);p=s.tracks.find(g=>g.id===p.parentId&&g.kind==='group');}Object.defineProperty(t,'_parentIndex',{value:s.tracks.findIndex(p=>p.id===t.parentId&&p.kind==='group'),writable:true,configurable:true});}
  return s;
}
export function advancedBullets(s){return s.usePlayerTrace||s.tracks.some(t=>t.spawnActions.length||t.bulletActions.some(a=>a.type==='spawn')||['control','group','pattern'].includes(t.kind)||t.kind==='rod'&&t.releaseFrame>=0||t.kind==='emitter'&&(t.advancedBullets||t.bulletActions.length||t.emissionShape!=='point'||t.indexDelay||t.indexSpeed||t.indexAngle||t.indexScale||(t.flightMode==='expression'&&t.flightExpression.includes('source.'))||Object.values(t.expressions||{}).some(v=>/\b(i|n|u|volley)\b|\bsource\.[xy]\b/.test(v))));}
export function expandPatterns(state){
  const s={...state,tracks:[]};
  const add=(clip,offset=0,rate=1,prefix='',depth=0)=>{
    if(depth>8)throw new Error('片段嵌套不能超过 8 层');const span=Math.max(0,...clip.children.map(t=>t.end))+1,scale=rate/clip.timeScale;
    for(let r=0;r<clip.repeatCount;r++){const idPrefix=prefix+clip.id+'/'+r+'/',origin=offset+Math.round((clip.start+r*(span/clip.timeScale+clip.repeatGap))*rate);
      for(const child of clip.children){if(child.kind==='pattern'){if(child.enabled!==false)add(child,origin,scale,idPrefix,depth+1);continue;}const t=structuredClone(child);t.id=idPrefix+child.id;if(t.parentId)t.parentId=idPrefix+t.parentId;if(t.targetSource)t.targetSource=idPrefix+t.targetSource;
        for(const a of [...t.bulletActions,...t.spawnActions]){a.spawnRepeat=Math.round(a.spawnRepeat*scale);if(a.templateId&&clip.children.some(c=>c.id===a.templateId))a.templateId=idPrefix+a.templateId;for(const p of ['start','duration','stagger'])a[p]=Math.round(a[p]*scale);if(['speed','turn','home'].includes(a.type)){a.from/=scale;a.to/=scale;}if(a.type==='acceleration'){a.from/=scale**2;a.to/=scale**2;}a.childSpeed/=scale;}
        t.start=origin+Math.round(t.start*scale);t.end=Math.min(state.duration,origin+Math.round(t.end*scale));if(t.start>state.duration)continue;
        for(const k of t.keyframes){k.frame=origin+Math.round(k.frame*scale);if(['speed','rollSpeed'].includes(k.property))k.value/=scale;if(k.property==='life')k.value*=scale;}
        for(const p of ['interval','life'])t[p]=Math.max(1,Math.round(t[p]*scale));for(const p of ['homingDelay','indexDelay'])t[p]=Math.round(t[p]*scale);
        for(const p of ['speed','indexSpeed','turnRate','angularSpeed','velocityX','velocityY','orbitSpeed','rollSpeed','rainbowSpeed','trailSpeed','trailScaleDecay','trailAlphaDecay'])t[p]/=scale;for(const p of ['accelerationX','accelerationY'])t[p]/=scale**2;for(const p of ['trailInterval','trailLife'])t[p]=Math.max(1,Math.round(t[p]*scale));for(const p of ['targetMinAge','targetMaxAge'])t[p]=Math.round(t[p]*scale);if(['speed','turn','home'].includes(t.controlAction)){t.controlFrom/=scale;t.controlValue/=scale;}if(t.controlAction==='acceleration'){t.controlFrom/=scale**2;t.controlValue/=scale**2;}if(t.releaseFrame>=0)t.releaseFrame=origin+Math.round(t.releaseFrame*scale);s.tracks.push(t);
      }
    }
  };
  for(const t of state.tracks){if(t.kind==='pattern'){if(t.enabled!==false)add(t);}else s.tracks.push(structuredClone(t));}return normalizeBulletState(s);
}
export function activeCode(t,time,tracks){if(t.enabled===false||t.templateOnly)return '0';let code=`${time}>=${num(t.start)} && ${time}<=${num(t.end)}`;let parent=tracks?.find(g=>g.id===t.parentId&&g.kind==='group');while(parent){code+=parent.enabled===false||parent.templateOnly?' && 0':` && ${time}>=${num(parent.start)} && ${time}<=${num(parent.end)}`;parent=tracks.find(g=>g.id===parent.parentId&&g.kind==='group');}return code;}
export function groupTransformCode(t,time){
  if(!(t._parentIndex>=0))return '';
  return `rf_group_pose(${t._parentIndex},${time});
rf_px=rf_x;rf_py=rf_y;rf_x=global.rf_gx+lengthdir_x(rf_px*global.rf_gscale,global.rf_ga)+lengthdir_x(rf_py*global.rf_gscale,global.rf_ga-90);rf_y=global.rf_gy+lengthdir_y(rf_px*global.rf_gscale,global.rf_ga)+lengthdir_y(rf_py*global.rf_gscale,global.rf_ga-90);
rf_px=rf_x1;rf_py=rf_y1;rf_x1=global.rf_gx+lengthdir_x(rf_px*global.rf_gscale,global.rf_ga)+lengthdir_x(rf_py*global.rf_gscale,global.rf_ga-90);rf_y1=global.rf_gy+lengthdir_y(rf_px*global.rf_gscale,global.rf_ga)+lengthdir_y(rf_py*global.rf_gscale,global.rf_ga-90);
rf_px=rf_x2;rf_py=rf_y2;rf_x2=global.rf_gx+lengthdir_x(rf_px*global.rf_gscale,global.rf_ga)+lengthdir_x(rf_py*global.rf_gscale,global.rf_ga-90);rf_y2=global.rf_gy+lengthdir_y(rf_px*global.rf_gscale,global.rf_ga)+lengthdir_y(rf_py*global.rf_gscale,global.rf_ga-90);
rf_angle+=global.rf_ga;rf_orientation+=global.rf_ga;rf_scale*=global.rf_gscale;rf_alpha*=global.rf_galpha;rf_length*=global.rf_gscale;rf_radius*=global.rf_gscale;
`;
}
