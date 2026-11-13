import fs from 'node:fs';
import path from 'node:path';
import {sectionHeaders} from './gm-resources.mjs';
import {fileRevision,resourceName} from './native-room.mjs';
import {gmlNumber as num} from './gml.mjs';

const blockPattern=/^[\t ]*\/\/gm82 fields begin[\t ]*\r?\n([\s\S]*?)^[\t ]*\/\/gm82 fields end[\t ]*(?:\r?\n|$)/m;
const fieldPattern=/^([\t ]*)\/\/field[\t ]+([A-Za-z_]\w*)(?:[\t ]*:[\t ]*([^\r\n]+?))?(?:[\t ]+-[\t ]+([^\r\n]*))?[\t ]*$/gm;
const assignment=/^([\t ]*)([A-Za-z_]\w*)(\[[01]\])?[\t ]*=[\t ]*(.*?)[\t ]*$/;
const finiteText=/^[+-]?(?:\d+(?:\.\d*)?|\.\d+)$/;
const indentSize=text=>text.replace(/\t/g,'    ').length;
const quoted=/^(?:"[^"\r\n]*"|'[^'\r\n]*')$/;
const resourceTypes=new Set(['sprite','sound','background','path','script','font','timeline','object','room','datafile','constant','instance']);

function readText(io,file){return io.existsSync(file)?io.readFileSync(file,'utf8'):'';}
function definitions(project,object,io,seen=new Set()){
  if(!resourceName(object)||seen.has(object.toLowerCase()))return [];
  seen.add(object.toLowerCase());
  const folder=path.join(project,'objects'),info=readText(io,path.join(folder,object+'.txt'));
  const parent=/^parent=([^\r\n]*)/m.exec(info)?.[1];
  const source=readText(io,path.join(folder,object+'.gml'));
  const start=sectionHeaders(source).find(section=>section.id==='Other_4');
  const fields=definitions(project,parent,io,seen);
  const parents=[];
  if(start)for(const m of source.slice(start.body,start.end).matchAll(fieldPattern)){
    const [,indent,name,type='value',description='']=m,space=indentSize(indent);
    while(parents.length&&parents.at(-1).space>=space)parents.pop();
    const spec=type.trim(),field={name,type:spec.split('(')[0].trim().toLowerCase(),spec,description,depends:parents.map(item=>item.name)};
    const same=fields.findIndex(entry=>entry.name===name);
    if(same>=0)fields[same]=field;else fields.push(field);
    parents.push({space,name});
  }
  return fields;
}
function locate(project,room,id,io){
  if(!/^[A-Za-z0-9_]+$/.test(room||'')||!/^[0-9a-f]{8}$/i.test(id||''))throw Error('房间实例标识无效。');
  const folder=path.join(project,'rooms',room),rows=readText(io,path.join(folder,'instances.txt')).split(/\r?\n/);
  const row=rows.find(line=>line.split(',')[3]===id);
  if(!row)throw Error('房间实例已不存在，请重新读取。');
  const file=path.join(folder,id+'.gml'),source=readText(io,file);
  return {file,source,row,hash:fileRevision(row+'\n'+source),object:row.split(',')[0]};
}
export function describeInstanceCode(project,room,id,io=fs){
  const data=locate(project,room,id,io);
  const block=blockPattern.exec(data.source),values=new Map();
  for(const line of (block?.[1]||'').split(/\r?\n/)){
    const m=assignment.exec(line);if(m)values.set(m[2]+(m[3]||''),m[4]);
  }
  return {content:data.source,hash:data.hash,fields:definitions(project,data.object,io).map(field=>({...field,
    value:field.type==='xy'?(values.has(field.name+'[0]')&&values.has(field.name+'[1]')?
      values.get(field.name+'[0]')+', '+values.get(field.name+'[1]'):''):values.get(field.name)||''}))};
}
function plainExpression(text){
  if(!text||text.length>2048||/[\r\n]/.test(text))return false;
  const code=text.replace(/"[^"]*"|'[^']*'/g,'_');
  return !/[;{}"'#]|\/\*|\/\//.test(code);
}
function fieldAssignments(field,value){
  const fail=()=>{throw Error('字段值无效：'+field.name);};
  if(field.type==='xy'){
    const parts=value.split(',').map(part=>part.trim());
    if(parts.length!==2||!parts.every(part=>finiteText.test(part)&&Number.isFinite(Number(part))))fail();
    return parts.map((part,i)=>field.name+'['+i+']='+num(part));
  }
  if(!plainExpression(value))fail();
  if(['bool','boolean','true','false'].includes(field.type)&&!/^(?:true|false|0|1)$/i.test(value))fail();
  if(field.type==='string'&&!quoted.test(value))fail();
  if(['number','angle','radius'].includes(field.type)){
    if(!finiteText.test(value)||!Number.isFinite(Number(value)))fail();
    const range=/\(\s*([^,]+)\s*,\s*([^,]+)\s*\)$/.exec(field.spec);
    if(range&&(Number(value)<Number(range[1])||Number(value)>Number(range[2])))fail();
    value=num(value);
  }
  if(resourceTypes.has(field.type)&&!/^(?:[A-Za-z_]\w*|-1)$/.test(value))fail();
  return [field.name+'='+value];
}
export function updateInstanceCode(project,room,id,input,io){
  const current=locate(project,room,id,io);
  if(input.hash!==current.hash)throw Error('GameMaker 已修改实例代码，请重新打开后再编辑。');
  if(typeof input.content==='string')return {file:current.file,before:current.source,source:input.content};
  if(!Array.isArray(input.fields))throw Error('无效的实例字段。');
  const definitionsByName=new Map(definitions(project,current.object,io).map(field=>[field.name,field]));
  const edits=new Map();
  for(const row of input.fields){
    const field=definitionsByName.get(row.name);
    if(!field||typeof row.value!=='string')throw Error('实例字段已经变化，请重新打开。');
    const value=row.value.trim();
    edits.set(field.name,value?fieldAssignments(field,value):[]);
  }
  const match=blockPattern.exec(current.source),eol=current.source.includes('\r\n')?'\r\n':'\n';
  const before=match?current.source.slice(0,match.index):'',after=match?current.source.slice(match.index+match[0].length):current.source;
  const lines=[],written=new Set();
  for(const line of (match?.[1]||'').split(/\r?\n/)){
    if(!line)continue;
    const name=assignment.exec(line)?.[2];
    if(!edits.has(name)){lines.push(line);continue;}
    if(!written.has(name)){lines.push(...edits.get(name));written.add(name);}
  }
  for(const [name,values] of edits)if(!written.has(name))lines.push(...values);
  const block=lines.length?'//gm82 fields begin'+eol+lines.join(eol)+eol+'//gm82 fields end'+eol:'';
  return {file:current.file,before:current.source,source:before+block+after};
}
