import crypto from 'node:crypto';
import {codeOnly} from './native-import.mjs';

const digest=source=>crypto.createHash('sha256').update(source).digest('hex');
// Keep offsets intact while excluding comments and string contents from syntax scans.
function syntax(source){return codeOnly(source).replace(/(['"])[\s\S]*?\1/g,m=>m.replace(/[^\r\n]/g,' '));}
function lineStart(source,at){const start=source.lastIndexOf('\n',at-1)+1;return /^\s*$/.test(source.slice(start,at))?start:at;}
export function nativeEventBlock(source,event){
  const code=syntax(source),lines=[0];for(let i=0;i<source.length;i++)if(source[i]==='\n')lines.push(i+1);
  const at=lines[event.line-1];if(at===undefined)throw new Error('事件位置已变化，请重新读取工程。');
  const nextSection=code.indexOf('\n#define ',at+1),limit=nextSection<0?code.length:nextSection+1;
  let start,end;
  if(event.kind==='timeline'){
    if(!/^#define\s+\d/.test(code.slice(at)))throw new Error('找不到所选 Timeline moment。');
    start=source.indexOf('\n',at)+1;end=limit;
    // YYD metadata belongs to the resource, not to its editable GML body.
    const action=/^\s*\/\*"\/\*'\/\*\*\/\/\* YYD ACTION[\s\S]*?\*\/[\t ]*\r?\n/.exec(source.slice(start,end));if(action)start+=action[0].length;
  }else if(event.kind==='step'){
    const header=/\bcase\s+[^:\r\n]+:/.exec(code.slice(at,code.indexOf('\n',at)<0?limit:code.indexOf('\n',at)));
    if(!header)throw new Error('找不到所选 case，请重新读取工程。');
    start=at+header.index+header[0].length;end=limit;let depth=0;
    for(const token of code.slice(start,limit).matchAll(/\bcase\b|\bdefault\s*:|[{}]/g)){
      if(token[0]==='{')depth++;
      else if(token[0]==='}'){if(depth===0){end=lineStart(source,start+token.index);break;}depth--;}
      else if(depth===0){end=lineStart(source,start+token.index);break;}
    }
    if(source.slice(start,start+2)==='\r\n')start+=2;else if(source[start]==='\n')start++;
  }else{
    const lineEnd=code.indexOf('\n',at),header=/\bif\s*\(/.exec(code.slice(at,lineEnd<0?limit:lineEnd));
    if(!header)throw new Error('这个表达式没有独立的 if 代码块，请使用“编辑整个源文件”。');
    start=at+header.index;let cursor=start+header[0].length,parens=1;
    while(cursor<limit&&parens){if(code[cursor]==='(')parens++;if(code[cursor]===')')parens--;cursor++;}
    while(cursor<limit&&/\s/.test(code[cursor]))cursor++;
    if(code[cursor]==='{'){let braces=1;cursor++;while(cursor<limit&&braces){if(code[cursor]==='{')braces++;if(code[cursor]==='}')braces--;cursor++;}end=cursor;}
    else{end=cursor;while(end<limit&&!/[;\r\n]/.test(code[end]))end++;if(code[end]===';')end++;}
  }
  if(start<0||end<start||end>source.length)throw new Error('无法确定事件代码块的边界，请重新读取工程。');
  return {content:source.slice(start,end),hash:digest(source),start,end};
}
export function replaceNativeEventBlock(source,event,content,hash){
  if(digest(source)!==hash)throw new Error('源文件在编辑期间已改变，请重新打开该事件后保存。');
  if(typeof content!=='string'||/^\s*#define\b/m.test(content))throw new Error('事件代码中不能添加 GM 资源事件头。');
  const block=nativeEventBlock(source,event);const newline=source.includes('\r\n')?'\r\n':'\n';
  let body=content.replace(/\r?\n/g,newline);if(block.content.endsWith('\n')&&!body.endsWith('\n'))body+=newline;
  return source.slice(0,block.start)+body+source.slice(block.end);
}
