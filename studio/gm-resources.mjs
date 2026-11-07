import fs from 'node:fs';
import path from 'node:path';
import {ProjectSyncTransaction} from './project-sync.mjs';
import {resourceName,fileRevision} from './native-room.mjs';
import {codeOnly} from './native-import.mjs';
import {gmlNumber as num} from './gml.mjs';

const actionMarker='/*"/*\'/**//* YYD ACTION';
const codeAction=actionMarker+'\nlib_id=1\naction_id=603\napplies_to=self\n*/\n';
const objectDefaults={sprite:'',visible:'1',solid:'0',persistent:'0',depth:'0',parent:'',mask:''};
const assetTypes=['objects','paths','timelines'];
const validFrame=value=>Number.isInteger(value)&&value>=0&&value<=1000000;
const newline=text=>text.includes('\r\n')?'\r\n':'\n';
const normalize=(text,eol)=>String(text).replace(/\r?\n/g,eol);
const read=(io,file)=>io.existsSync(file)?io.readFileSync(file):null;
const string=bytes=>bytes?.toString('utf8')||'';
const same=(a,b)=>a===null?b===null:Buffer.isBuffer(b)&&a.equals(b);
const revision=(...bytes)=>bytes.map(data=>data===null?'-':fileRevision(data)).join(':');
const validName=name=>resourceName(name)&&!/^objRuf/i.test(name)&&!/^(?:con|prn|aux|nul|com[1-9]|lpt[1-9])$/i.test(name);
const visualActions={
  create:{id:201,args:['target','x','y'],relative:true,resource:'objects'},
  position:{id:109,args:['x','y'],relative:true},
  motion:{id:102,args:['direction','speed'],relative:true},
  alarm:{id:301,args:['frames','alarm'],relative:true},
  sprite:{id:541,args:['target','subimage','animationSpeed'],resource:'sprites'},
  bounce:{id:115,args:['precise','collisionAll']},
  script:{id:601,args:['target'],resource:'scripts'},
  variable:{id:611,args:['name','value'],relative:true},
  destroy:{id:203,args:[]},
  inherited:{id:604,args:[]}
};
function assetFiles(project,kind,name){
  if(!assetTypes.includes(kind)||!resourceName(name))throw Error('无效的 GM 资源名称');
  const folder=path.join(project,kind);
  return kind==='paths'?[path.join(folder,name,'path.txt'),path.join(folder,name,'points.txt')]:
    kind==='objects'?[path.join(folder,name+'.txt'),path.join(folder,name+'.gml')]:[path.join(folder,name+'.gml')];
}
function stage(transaction,file,before,text){
  transaction.write(file,text);
  const change=transaction.files.get(file);
  if(change&&!same(before,change.before))throw Error('GM 资源已被修改，请重新打开后再编辑。');
}
export function gmAssetNames(project,kind,io=fs){
  if(!assetTypes.includes(kind))return [];
  const folder=path.join(project,kind);
  const index=string(read(io,path.join(folder,'index.yyd')));
  const names=index.split(/\r?\n/).map(name=>name.trim()).filter(resourceName);
  return [...new Set(names)].filter(name=>io.existsSync(assetFiles(project,kind,name)[0]));
}
function keys(text){
  return Object.fromEntries([...text.matchAll(/^([a-z_][a-z_0-9]*)=([^\r\n]*)/gm)].map(row=>[row[1],row[2]]));
}
function replaceKeys(text,values){
  const eol=newline(text);
  for(const [key,value] of Object.entries(values)){
    const line=new RegExp('^'+key+'=[^\\r\\n]*','m');
    if(line.test(text))text=text.replace(line,()=>key+'='+value);
    else text+=(text&&!text.endsWith('\n')?eol:'')+key+'='+value+eol;
  }
  return text;
}
export function sectionHeaders(source){
  const syntax=codeOnly(source,true).replace(/(['"])[\s\S]*?\1/g,m=>m.replace(/[^\r\n]/g,' '));
  const matches=[...syntax.matchAll(/^#define[ \t]+([\w.]+)[ \t]*(?:\r?\n|$)/gm)];
  return matches.map((m,i)=>({id:m[1],start:m.index,body:m.index+m[0].length,end:matches[i+1]?.index??source.length}));
}
function actions(source,section){
  const body=source.slice(section.body,section.end);
  const markers=[...body.matchAll(/^\/\*"\/\*'\/\*\*\/\/\* YYD ACTION[\s\S]*?\*\/[ \t]*(?:\r?\n|$)/gm)];
  return markers.map((marker,i)=>{
    const blockStart=section.body+marker.index,start=blockStart+marker[0].length;
    const end=i+1===markers.length?section.end:section.body+markers[i+1].index;
    const header=keys(marker[0]),id=Number(header.action_id)||0;
    const lib=Number(header.lib_id)||0,editable=lib===1&&id===603;
    const content=editable?source.slice(start,end):'';
    return {index:i,id,lib,editable,blockStart,start,end,content,visual:readVisualAction(header,id,lib)};
  });
}
function events(source){
  return sectionHeaders(source).map(row=>({id:row.id,actions:actions(source,row).map(({index,id,lib,editable,visual})=>({index,id,lib,editable,visual}))}));
}
function addBlock(text,id,before,empty=false){
  const eol=newline(text),header='#define '+id+eol+(empty?'':normalize(codeAction,eol)+eol);
  return text.slice(0,before)+(before>0&&text[before-1]!=='\n'?eol:'')+header+text.slice(before);
}
function separateEmptyEvents(source){
  // GM8.2 splits on the newline before #define, then expects a newline
  // between the event name and its actions even when there are no actions.
  const eol=newline(source);
  for(const event of sectionHeaders(source).reverse()){
    const body=source.slice(event.body,event.end);
    if(!body.trim()&&!body.includes('\n'))source=source.slice(0,event.end)+eol+source.slice(event.end);
  }
  return source;
}
function eventId(io,project,id){
  if(/^(?:Create_0|Destroy_0|Step_[012]|Draw_0|Other_(?:[0-7]|1[0-9]|2[0-5])|Alarm_(?:[0-9]|1[01])|Key(?:Press|Release|board)_(?:\d{1,2}|1\d{2}|2[0-4]\d|25[0-5]))$/.test(id))return id;
  const collision=/^Collision_(\w+)$/.exec(id);
  if(collision&&gmAssetNames(project,'objects',io).includes(collision[1]))return id;
  throw Error('请选择支持的 GameMaker 事件。');
}
function actionExpression(value){
  const expression=String(value??'').trim();
  if(!expression||expression.length>500||/[\r\n]|\*\//.test(expression))throw Error('请输入有效的动作参数。');
  return /^[-+]?(?:\d+\.?\d*|\.\d+)(?:e[-+]?\d+)?$/i.test(expression)&&Number.isFinite(Number(expression))
    ?num(Number(expression)):expression;
}
function readVisualAction(header,id,lib){
  if(lib!==1)return null;
  const kind=Object.keys(visualActions).find(kind=>visualActions[kind].id===id);
  if(!kind)return null;
  const action={kind,relative:header.relative==='1',appliesTo:header.applies_to??'self'};
  visualActions[kind].args.forEach((key,index)=>action[key]=header['arg'+index]??'');
  if(kind==='script')action.arguments=Array.from({length:5},(_,i)=>header['arg'+(i+1)]??'0');
  return action;
}
function hasActionResource(project,io,kind,name){
  if(!resourceName(name))return false;
  const folder=path.join(project,kind),file=kind==='sprites'?path.join(folder,name,'sprite.txt'):
    path.join(folder,name+(kind==='objects'?'.txt':'.gml'));
  return io.existsSync(file)&&string(read(io,path.join(folder,'index.yyd'))).split(/\r?\n/).some(line=>line.trim()===name);
}
function visualFields(project,io,input){
  if(!Object.hasOwn(visualActions,input.kind))throw Error('请选择支持的 GM 动作。');
  const def=visualActions[input.kind],values={};
  if(def.resource){
    const name=input.target??'';
    if(!name)throw Error(input.kind==='create'?'请选择要创建的 GM 对象。':input.kind==='sprite'?'请选择有效的贴图。':'请选择有效的脚本。');
    if(!hasActionResource(project,io,def.resource,name))throw Error('GM 资源不存在：'+name);
  }
  if(input.kind!=='inherited'){
    const target=input.appliesTo??'self';
    if(!['self','other',''].includes(target)&&!hasActionResource(project,io,'objects',target))
      throw Error('请选择有效的动作作用对象。');
    values.applies_to=target;
  }
  if(input.kind==='variable'&&!/^[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*(?:\[[^\r\n;{}[\]]+\])?$/.test(input.name))throw Error('请输入有效的变量名称。');
  if(input.kind==='alarm'&&!/^(?:[0-9]|1[01])$/.test(String(input.alarm)))throw Error('请选择 Alarm 0 到 Alarm 11。');
  if(input.kind==='bounce'&&![input.precise,input.collisionAll].every(value=>['0','1'].includes(String(value))))
    throw Error('请选择有效的反弹方式。');
  if(def.relative)values.relative=input.relative?'1':'0';
  def.args.forEach((key,index)=>values['arg'+index]=key==='target'?input.target??'':actionExpression(input[key]));
  if(input.kind==='script'){
    if(!Array.isArray(input.arguments)||input.arguments.length!==5)throw Error('脚本动作需要五个参数。');
    input.arguments.forEach((value,index)=>values['arg'+(index+1)]=actionExpression(value));
  }
  return {id:def.id,values};
}
function newVisualAction(project,io,input,eol){
  const {id,values}=visualFields(project,io,input);
  const fields=Object.entries({lib_id:1,action_id:id,invert:0,...values});
  return [actionMarker,...fields.map(([key,value])=>key+'='+value),'*/',''].join(eol);
}
function pathData(text,points){
  const info=keys(text);
  const raw=points.split(/\r?\n/).filter(line=>line.trim());
  return {smooth:info.connection==='1',closed:info.closed==='1',precision:Number(info.precision)||4,
    points:raw.map(line=>{const [x,y,speed]=line.split(',').map(Number);return {x,y,speed};})};
}
function describe(project,kind,name,io=fs){
  const files=assetFiles(project,kind,name),raw=files.map(file=>read(io,file));
  if(raw[0]===null)throw Error('GM 资源不存在：'+name);
  const result={name,kind,revision:revision(...raw)};
  if(kind==='paths')result.path=pathData(string(raw[0]),string(raw[1]));
  else{
    result.events=events(string(raw[kind==='objects'?1:0]));
    if(kind==='objects'){
      const object={...objectDefaults,...keys(string(raw[0]))};
      result.object={sprite:object.sprite,parent:object.parent,mask:object.mask,depth:Number(object.depth)||0,
        visible:object.visible!=='0',solid:object.solid==='1',persistent:object.persistent==='1'};
    }
  }
  return result;
}

export function readGmAsset(project,{kind,name,section,action,whole}){
  if(section===undefined&&!whole)return describe(project,kind,name);
  if(kind==='paths')throw Error('路径没有 GML 事件。');
  const files=assetFiles(project,kind,name),raw=files.map(file=>read(fs,file));
  const source=string(raw[kind==='objects'?1:0]),selected=sectionHeaders(source).find(row=>row.id===section);
  if(whole)return {content:source,hash:revision(...raw)};
  const code=selected&&actions(source,selected).find(row=>row.index===action&&row.editable);
  if(!code)throw Error('找不到可编辑的 GML 动作。');
  return {content:code.content,hash:revision(...raw)};
}
function appendIndex(transaction,folder,name){
  const index=path.join(folder,'index.yyd'),tree=path.join(folder,'tree.yyd');
  for(const [file,value] of [[index,name],[tree,'|'+name]]){
    const old=read(transaction.io,file),text=string(old),eol=newline(text);
    if(!text.split(/\r?\n/).includes(value))stage(transaction,file,old,text+(text&&!text.endsWith('\n')?eol:'')+value+eol);
  }
}
function removeObjectIndex(transaction,folder,name){
  for(const [fileName,entry,replacement] of [['index.yyd',name,''],['tree.yyd','|'+name,null]]){
    const file=path.join(folder,fileName),old=read(transaction.io,file);
    if(old===null)continue;
    const text=string(old),lines=text.split(/\r?\n/);
    stage(transaction,file,old,lines.flatMap(line=>line.trim()===entry?(replacement===null?[]:[replacement]):[line]).join(newline(text)));
  }
}
export function gmObjectReferences(project,name,io=fs){
  if(!validName(name))throw Error('无效的 GM 资源名称');
  const refs=[],files=assetFiles(project,'objects',name);
  const token=new RegExp('\\b'+name+'\\b','i'),collision=new RegExp('^#define[ \\t]+Collision_'+name+'[ \\t]*\\r?$','im');
  const actionHeaders=/^\/\*"\/\*'\/\*\*\/\/\* YYD ACTION[\s\S]*?\*\//gm;
  const matchesCode=source=>token.test(codeOnly(source))||collision.test(source)||[...source.matchAll(actionHeaders)].some(row=>token.test(row[0]));
  function visit(folder){
    if(!io.existsSync(folder))return;
    for(const entry of io.readdirSync(folder,{withFileTypes:true})){
      const file=path.join(folder,entry.name);
      if(entry.isDirectory()){visit(file);continue;}
      if(!entry.isFile()||files.includes(file)||!/\.(gml|txt)$/i.test(entry.name))continue;
      const source=io.readFileSync(file,'utf8');
      if(file.endsWith('.gml')?matchesCode(source):token.test(source))refs.push(path.relative(project,file));
    }
  }
  for(const folder of ['objects','rooms','scripts','timelines','settings','triggers'])visit(path.join(project,folder));
  const authoring=read(io,path.join(project,'rufmaker.json'));
  if(authoring!==null){
    const state=JSON.parse(string(authoring).replace(/^\uFEFF/,''));
    const native=state.nativeProject||{};
    if([native.controller,native.bootObject,native.killerParent].some(value=>token.test(value??'')))refs.push('rufmaker.json · nativeProject');
    function tracks(rows){
      for(const track of rows||[]){
        if(token.test(track.nativeObject??'')||token.test(track.eventNativeObjects??'')||matchesCode(track.code??''))
          refs.push('rufmaker.json · '+(track.name||track.id||'tracks'));
        tracks(track.children);
      }
    }
    tracks(state.tracks);
  }
  return [...new Set(refs)];
}
function objectValues(project,io,name,object){
  const result={};
  for(const key of ['sprite','mask','parent']){
    const value=object[key]||'',folder=key==='parent'?'objects':'sprites',suffix=key==='parent'?value+'.txt':path.join(value,'sprite.txt');
    if(value&&(!resourceName(value)||!io.existsSync(path.join(project,folder,suffix))))throw Error('GM 资源不存在：'+value);
    result[key]=value;
  }
  let parent=result.parent;
  const visited=new Set([name.toLowerCase()]);
  while(parent){
    if(visited.has(parent.toLowerCase()))throw Error('对象不能循环继承。');
    visited.add(parent.toLowerCase());
    parent=keys(string(read(io,assetFiles(project,'objects',parent)[0]))).parent||'';
  }
  if(!Number.isInteger(object.depth)||object.depth< -2147483648||object.depth>2147483647)throw Error('对象深度无效。');
  result.depth=String(object.depth);
  for(const key of ['visible','solid','persistent'])result[key]=object[key]?'1':'0';
  return result;
}
function validatePath(data){
  if(!data||!Number.isInteger(data.precision)||data.precision<1||data.precision>8||!Array.isArray(data.points)||data.points.length>4096||
      data.points.some(p=>![p.x,p.y,p.speed].every(Number.isFinite)||p.speed<0||p.speed>10000||Math.abs(p.x)>1000000||Math.abs(p.y)>1000000))throw Error('路径点或精度无效。');
}
export function editGmAsset(project,input){
  const {kind,name,operation}=input;
  const files=assetFiles(project,kind,name),transaction=new ProjectSyncTransaction(project);
  transaction.recover();
  const io=transaction.io,raw=files.map(file=>read(io,file));
  if(operation==='create'){
    if(!validName(name)||raw.some(file=>file!==null)||gmAssetNames(project,kind,io).some(n=>n.toLowerCase()===name.toLowerCase()))throw Error('资源名称无效或已存在。');
    const source=kind==='objects'?[replaceKeys('',objectDefaults),'']:
      kind==='paths'?['connection=0\nclosed=0\nprecision=4\nbackground=\nsnap_x=16\nsnap_y=16\n',''] : [''];
    files.forEach((file,i)=>stage(transaction,file,raw[i],source[i]));
    appendIndex(transaction,path.join(project,kind),name);
  }else{
    if(!validName(name)||raw[0]===null||input.revision!==revision(...raw))throw Error('GM 资源已被修改，请重新打开后再编辑。');
    if(kind==='objects'&&operation==='delete'){
      const refs=gmObjectReferences(project,name,io);
      if(refs.length)throw Error('仍有资源引用此对象：'+refs.slice(0,8).join(', '));
      files.forEach((file,index)=>{
        transaction.remove(file);
        if(!same(raw[index],transaction.files.get(file)?.before??null))throw Error('GM 资源已被修改，请重新打开后再编辑。');
      });
      removeObjectIndex(transaction,path.join(project,kind),name);
      transaction.commit();
      return {name,deleted:true};
    }else if(kind==='paths'){
      if(operation!=='settings')throw Error('无效的路径操作。');
      const data=input.path;validatePath(data);
      const meta=string(raw[0]),points=string(raw[1]),eol=newline(points||meta);
      stage(transaction,files[0],raw[0],replaceKeys(meta,{connection:data.smooth?'1':'0',closed:data.closed?'1':'0',precision:data.precision}));
      stage(transaction,files[1],raw[1],data.points.map(p=>[p.x,p.y,p.speed].map(value=>num(value)).join(',')).join(eol)+(data.points.length?eol:''));
    }else if(kind==='objects'&&operation==='settings'){
      stage(transaction,files[0],raw[0],replaceKeys(string(raw[0]),objectValues(project,io,name,input.object)));
    }else{
      const codeIndex=kind==='objects'?1:0,source=string(raw[codeIndex]),sections=sectionHeaders(source);
      const eol=newline(source),row=sections.find(event=>event.id===input.section);
      let next;
      if(operation==='add'){
        const id=kind==='objects'?eventId(io,project,input.section):validFrame(input.frame)?String(input.frame):null;
        if(id===null||sections.some(event=>kind==='objects'?event.id===id:Number(event.id)===input.frame))throw Error('该事件或帧已存在，或时间超出范围。');
        const later=kind==='timelines'&&sections.find(event=>Number(event.id)>input.frame);
        next=addBlock(source,id,later?later.start:source.length,kind==='objects'&&input.empty===true);
      }else if(operation==='delete-section'&&kind==='objects'&&row){
        next=source.slice(0,row.start)+source.slice(row.end);
      }else if(operation==='change-section'&&kind==='objects'&&row){
        const id=eventId(io,project,input.nextSection);
        if(id===row.id)return describe(project,kind,name);
        if(sections.some(event=>event.id===id))throw Error('该事件或帧已存在，或时间超出范围。');
        next=source.slice(0,row.start)+source.slice(row.start,row.body).replace(row.id,id)+source.slice(row.body);
      }else if(operation==='move'&&kind==='timelines'){
        if(!row||!validFrame(input.frame)||sections.some(event=>event!==row&&Number(event.id)===input.frame))throw Error('该帧已存在或时间超出范围。');
        if(Number(row.id)===input.frame)return describe(project,kind,name);
        const other=source.slice(0,row.start)+source.slice(row.end),later=sectionHeaders(other).find(event=>Number(event.id)>input.frame);
        const at=later?later.start:other.length,body=source.slice(row.body,row.end);
        next=other.slice(0,at)+(at>0&&other[at-1]!=='\n'?eol:'')+'#define '+input.frame+eol+body+(later&&!body.endsWith('\n')?eol:'')+other.slice(at);
      }else if(operation==='add-action'&&row){
        const block=kind==='objects'&&input.visual?newVisualAction(project,io,input.visual,eol):normalize(codeAction,eol)+gmlBody(input.content??'',eol);
        next=source.slice(0,row.end)+(row.end>0&&source[row.end-1]!=='\n'?eol:'')+block+source.slice(row.end);
      }else if(kind==='objects'&&row&&['visual-action','delete-action','move-action'].includes(operation)){
        const list=actions(source,row),selected=list[input.action];
        if(!selected)throw Error('找不到所选动作。');
        if(operation==='delete-action')next=source.slice(0,selected.blockStart)+source.slice(selected.end);
        if(operation==='visual-action'){
          if(!selected.visual||selected.visual.kind!==input.visual?.kind)throw Error('找不到所选动作。');
          const header=source.slice(selected.blockStart,selected.start),end=header.lastIndexOf('*/');
          const {values}=visualFields(project,io,input.visual);
          next=source.slice(0,selected.blockStart)+replaceKeys(header.slice(0,end),values)+header.slice(end)+source.slice(selected.start);
        }
        if(operation==='move-action'){
          const neighbor=list[input.action+input.direction];
          if(!neighbor||Math.abs(input.direction)!==1)throw Error('找不到相邻动作。');
          const [first,second]=input.direction<0?[neighbor,selected]:[selected,neighbor];
          const last=source.slice(second.blockStart,second.end);
          next=source.slice(0,first.blockStart)+last+(last.endsWith('\n')?'':eol)+source.slice(first.blockStart,first.end)+source.slice(second.end);
        }
      }else if(operation==='source'&&typeof input.content==='string'){
        next=normalize(input.content,eol);
      }else if(operation==='code'&&row){
        const code=actions(source,row).find(item=>item.index===input.action&&item.editable);
        if(!code||typeof input.content!=='string')throw Error('无效的 GML 动作。');
        next=source.slice(0,code.start)+gmlBody(input.content,eol)+source.slice(code.end);
      }else throw Error('找不到所选事件或操作。');
      stage(transaction,files[codeIndex],raw[codeIndex],kind==='objects'?separateEmptyEvents(next):next);
    }
  }
  transaction.commit();
  return describe(project,kind,name);
}

function gmlBody(content,eol){
  if(typeof content!=='string'||/^\s*#define\b/m.test(content))throw Error('无效的 GML 动作。');
  const body=normalize(content,eol);
  return body.endsWith('\n')?body:body+eol;
}

export function nativePathAssets(project,io=fs){
  return Object.fromEntries(gmAssetNames(project,'paths',io).map(name=>[name,describe(project,'paths',name,io).path]));
}
