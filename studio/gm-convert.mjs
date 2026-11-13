import fs from 'node:fs';
import path from 'node:path';
import {readGmAsset,gmAssetNames} from './gm-resources.mjs';
import {codeOnly} from './native-import.mjs';
import {evaluateExpression} from './random-parameters.mjs';
import {normalizeState} from './motion.mjs';

const properties={x:'x',y:'y',speed:'speed',direction:'angle',gravity:'bulletGravity',gravity_direction:'bulletGravityDirection',friction:'bulletFriction',image_speed:'imageSpeed',image_index:'imageIndex',image_angle:'spriteAngle',image_alpha:'alpha',depth:'depth'};
const colors={c_white:'#FFFFFF',c_black:'#000000',c_red:'#FF0000',c_lime:'#00FF00',c_blue:'#0000FF',c_yellow:'#FFFF00',c_aqua:'#00FFFF',c_fuchsia:'#FF00FF',c_gray:'#808080',c_silver:'#C0C0C0'};
function statements(source){
  const result=[];let begin=0,depth=0,quote='';
  for(let i=0;i<source.length;i++){
    const c=source[i];if(quote){if(c===quote)quote='';continue;}
    if(c==='"'||c==="'")quote=c;else if(c==='(')depth++;else if(c===')')depth--;
    else if(depth===0&&(c===';'||c==='\n')){const s=source.slice(begin,i).trim();if(s)result.push(s);begin=i+1;}
  }
  const s=source.slice(begin).trim();if(s)result.push(s);return result;
}

// Deliberately accepts linear initialization only. This never executes GML and
// never discards an unrecognized event in order to claim a successful conversion.
export function convertGmObject(project,{name,start=0,end=1499,x=400,y=304,seed=12345}={}){
  const issues=[],recognized=[],values={speed:0,angle:0,imageSpeed:1,imageIndex:0,imageDelay:1,bulletGravity:0,bulletGravityDirection:270,bulletFriction:0,scale:1,alpha:1,spriteAngle:0};
  const expressions={},data=readGmAsset(project,{kind:'objects',name});
  if(!gmAssetNames(project,'objects').includes(name)||/^objRuf/i.test(name))throw Error('请选择工程中的原生 GM 对象。');
  if(!Number.isInteger(start)||!Number.isInteger(end)||start<0||end<start||![x,y].every(Number.isFinite))throw Error('转换时段或坐标无效。');
  const spriteNames=fs.existsSync(path.join(project,'sprites/index.yyd'))?fs.readFileSync(path.join(project,'sprites/index.yyd'),'utf8').split(/\r?\n/).filter(Boolean):[];
  let sprite=data.object.sprite,scaleX=1,scaleY=1,horizontal=null,vertical=null,color='#FFFFFF';
  values.depth=data.object.depth;
  if(data.object.solid)issues.push('实心碰撞需要保留原生事件');
  if(data.object.persistent)issues.push('持久对象不能转换为时间轴弹幕');
  if(data.object.mask)issues.push('独立碰撞蒙版暂不支持转换');
  if(!data.object.visible)issues.push('不可见控制器请保留为原生对象');
  if(data.object.parent)issues.push('包含父对象 '+data.object.parent+'，需要保留继承事件');
  const number=(text,field)=>{
    const normalized=String(text).trim().replace(/\btrue\b/g,'1').replace(/\bfalse\b/g,'0');
    // The editor expression parser supplies seeded random/choose and geometry;
    // variables from arbitrary native code are not valid initialization inputs.
    if(/\b(?:self|other|global)\s*\.|[;={}]/.test(normalized))throw Error('依赖原生变量或代码块');
    const v=evaluateExpression(normalized,seed,name+'|convert|'+field,{x,y,playerX:400,playerY:304,frame:start,age:0,speed:values.speed,direction:values.angle});
    if(!Number.isFinite(v)||Math.abs(v)>1000000)throw Error('数值超出范围');
    return {value:v,text:normalized};
  };
  const assign=(key,text,relative=false)=>{
    if(key==='sprite_index'){
      const chosen=String(text).trim();if(relative||!spriteNames.includes(chosen))throw Error('贴图名称无效');sprite=chosen;recognized.push(key);return;
    }
    if(key==='image_blend'){
      if(relative)throw Error('不支持颜色相对赋值');
      if(colors[text.trim()])color=colors[text.trim()];else {const n=Math.round(number(text,key).value);if(n<0||n>16777215)throw Error('颜色无效');color='#'+[n&255,(n>>8)&255,(n>>16)&255].map(v=>v.toString(16).padStart(2,'0')).join('').toUpperCase();}
      recognized.push(key);return;
    }
    const p=properties[key];if(!p&&!['image_xscale','image_yscale','hspeed','vspeed'].includes(key))throw Error('不支持的属性: '+key);
    if(relative&&expressions[p])throw Error('随机属性的连续相对赋值暂不支持转换');
    const v=number(text,p||key);let current=p?values[p]??(p==='x'?x:p==='y'?y:0):key==='image_xscale'?scaleX:key==='image_yscale'?scaleY:key==='hspeed'?horizontal||0:vertical||0;
    if(!p&&/\b(?:random_range|random|choose|irandom|irandom_range)\s*\(/.test(v.text))throw Error('此项随机初始化暂不支持转换');
    if(key==='hspeed'||key==='vspeed'){
      if(expressions.speed||expressions.angle)throw Error('随机速度方向与分量速度混用');
      const rad=values.angle*Math.PI/180,vx=Math.cos(rad)*values.speed,vy=-Math.sin(rad)*values.speed;
      horizontal=key==='hspeed'?(relative?vx:0)+v.value:vx;vertical=key==='vspeed'?(relative?vy:0)+v.value:vy;
      values.speed=Math.hypot(horizontal,vertical);values.angle=values.speed?Math.atan2(-vertical,horizontal)*180/Math.PI:0;
      recognized.push(key);return;
    }
    const value=(relative?current:0)+v.value;
    if(key==='image_xscale')scaleX=value;else if(key==='image_yscale')scaleY=value;else if(key==='hspeed')horizontal=value;else if(key==='vspeed')vertical=value;
    else {values[p]=value;if(/\b(?:random_range|random|choose|irandom|irandom_range)\s*\(/.test(v.text)){
      if(relative||!['speed','angle','imageSpeed','imageIndex','bulletGravity','bulletGravityDirection','bulletFriction','alpha','spriteAngle'].includes(p))throw Error('此项随机初始化暂不支持转换');
      expressions[p]=v.text;
    }else delete expressions[p];}
    recognized.push(key);
  };
  for(const event of data.events){
    if(!event.actions.length){if(event.id==='Draw_0')issues.push('Draw_0：包含需要原生运行的事件');continue;}
    if(event.id!=='Create_0'){issues.push(event.id+'：包含需要原生运行的事件');continue;}
    for(const action of event.actions){
      try{
        const v=action.visual;
        if(v&&v.appliesTo!=='self')throw Error('动作作用于其他对象');
        if(v?.kind==='variable')assign(v.name,v.value,v.relative);
        else if(v?.kind==='motion'){assign('direction',v.direction,v.relative);assign('speed',v.speed,v.relative);}
        else if(v?.kind==='sprite'){assign('sprite_index',v.target);assign('image_index',v.subimage);assign('image_speed',v.animationSpeed);}
        else if(action.editable){
          const source=readGmAsset(project,{kind:'objects',name,section:event.id,action:action.index}).content;
          const code=codeOnly(source).trim();
          for(const statement of statements(code)){
            const m=/^([A-Za-z_]\w*)\s*(=|\+=|-=)\s*([\s\S]+)$/.exec(statement);
            if(!m)throw Error('包含分支、函数调用或多行语句');
            assign(m[1],m[2]==='-='?'-('+m[3]+')':m[3],m[2]!=='=');
          }
        }else throw Error('动作 '+action.id+' 暂不支持转换');
      }catch(error){issues.push(event.id+' / '+(action.index+1)+'：'+error.message);}
    }
  }
  if(!sprite||!spriteNames.includes(sprite))issues.push('没有可用贴图');
  if(scaleX!==scaleY||scaleX<0)issues.push('不等比缩放或镜像暂不支持转换');
  values.scale=scaleX;
  if(!Number.isInteger(values.depth)||values.bulletFriction<0||values.alpha<0||values.alpha>1||values.scale<0)issues.push('存在无法等价表达的深度、阻力或外观值');
  if(issues.length)return {supported:false,name,issues:[...new Set(issues)],recognized:[...new Set(recognized)]};
  const track={id:'converted',name,kind:'emitter',enabled:true,start,end,lane:0,x,y,sprite,color,easing:'linear',keyframes:[],count:1,spread:0,interval:end-start+1,life:end-start+1,lifetimeMode:'room',advancedBullets:true,emissionFacing:'fixed',...values,expressions};
  normalizeState({version:2,fps:50,duration:end,randomSeed:seed,tracks:[track]});
  if(track.bulletAcceleration||track.bulletFriction||track.bulletGravity)track.objectProperties=(track.objectProperties||0)|(1<<5);
  track.objectProperties=(track.objectProperties||0)|(1<<6);
  return {supported:true,name,issues:[],recognized:[...new Set(recognized)],track};
}
