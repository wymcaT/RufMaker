import fs from 'node:fs';
import path from 'node:path';

export function codeOnly(source,segmented=false) {
  if(!segmented){const parts=source.split(/(?=^#define\s)/m);if(parts.length>1)return parts.map(part=>codeOnly(part,true)).join('');}
  source=source.replace(/\/\*"\/\*'\/\*\*\/\/\* YYD ACTION[\s\S]*?\*\//g,m=>m.replace(/[^\n]/g,' '));
  let out='',i=0;
  while(i<source.length){const c=source[i],n=source[i+1];if(c==='"'||c==="'"){const quote=c;out+=c;i++;while(i<source.length){const q=source[i++];out+=q;if(q===quote)break;}}else if(c==='/'&&n==='/'){while(i<source.length&&source[i]!=='\n'){out+=' ';i++;}}else if(c==='/'&&n==='*'){out+='  ';i+=2;while(i<source.length&&!(source[i]==='*'&&source[i+1]==='/')){out+=source[i]==='\n'?'\n':' ';i++;}if(i<source.length){out+='  ';i+=2;}}else{out+=c;i++;}}
  return out;
}
export function numberExpression(expression,constants={}) {
  const tokens=String(expression).match(/(?:\d+(?:\.\d*)?|\.\d+)|[A-Za-z_][\w.]*|[()+\-*/,]/g)||[];
  if(tokens.join('').toLowerCase()!==String(expression).replace(/\s/g,'').toLowerCase())return null;let at=0;
  function atom(){const t=tokens[at++];if(t==='+'||t==='-'){const n=atom();return t==='-'?-n:n;}if(t==='('){const n=sum();if(tokens[at++]!==')')throw 0;return n;}if(t===undefined)throw 0;if(/^\d|^\./.test(t))return Number(t);if(tokens[at]==='('){at++;const args=[];if(tokens[at]!==')'){args.push(sum());while(tokens[at]===','){at++;args.push(sum());}}if(tokens[at++]!==')')throw 0;const f={round:Math.round,floor:Math.floor,ceil:Math.ceil,abs:Math.abs,min:Math.min,max:Math.max}[t.toLowerCase()];if(f)return f(...args);if(t.toLowerCase()==='return_realtime'){if(args.length===3)return args[0]+Math.round(750*args[2]/args[1]);if(args.length===1&&Number.isFinite(constants.MusicBPM))return (constants.STARTBEAT??constants.STARTSTEP??0)+Math.round(750*args[0]/constants.MusicBPM);}throw 0;}if(t==='pi')return Math.PI;if(t==='true')return 1;if(t==='false')return 0;if(Number.isFinite(constants[t]))return constants[t];throw 0;}
  function product(){let n=atom();while(['*','/','mod','div'].includes(tokens[at])){const op=tokens[at++],v=atom();n=op==='*'?n*v:op==='/'?n/v:op==='mod'?n%v:Math.floor(n/v);}return n;}
  function sum(){let n=product();while(tokens[at]==='+'||tokens[at]==='-'){const op=tokens[at++],v=product();n=op==='+'?n+v:n-v;}return n;}
  try{const n=sum();return at===tokens.length&&Number.isFinite(n)?n:null;}catch{return null;}
}
const read=f=>fs.readFileSync(f,'utf8').replace(/^\uFEFF/,'').replace(/\r/g,'');
const names=(folder,ext)=>fs.existsSync(folder)?fs.readdirSync(folder,{withFileTypes:true}).filter(x=>ext?x.isFile()&&x.name.endsWith(ext):x.isDirectory()).map(x=>ext?x.name.slice(0,-ext.length):x.name):[];
const settings=file=>Object.fromEntries(read(file).split('\n').filter(x=>x.includes('=')).map(x=>{const p=x.indexOf('=');return [x.slice(0,p),x.slice(p+1)];}));
export function discoverNativeProjects(folder) {
  const found=[];const visit=(dir,depth)=>{if(depth>5)return;for(const entry of fs.readdirSync(dir,{withFileTypes:true})){if(entry.isSymbolicLink())continue;const file=path.join(dir,entry.name);if(entry.isFile()&&entry.name.toLowerCase().endsWith('.gm82')){const fd=fs.openSync(file,'r'),head=Buffer.alloc(64);try{fs.readSync(fd,head,0,64,0);}finally{fs.closeSync(fd);}if(head.toString().replace(/^\uFEFF/,'').startsWith('gm82_version='))found.push(file);}else if(entry.isDirectory()&&!['build','cache','.git','node_modules','Data','sprites','objects','backgrounds','scripts','sounds','fonts','rooms'].includes(entry.name))visit(file,depth+1);}};
  const absolute=path.resolve(folder);if(!fs.existsSync(absolute))throw new Error('所选目录不存在');if(fs.statSync(absolute).isFile()){if(!read(absolute).startsWith('gm82_version='))throw new Error('仅支持 GameMaker 8.2 文本工程；请先在 GM8.2 中另存为 .gm82 文本格式。');found.push(absolute);}else visit(absolute,0);
  if(!found.length)throw new Error('没有找到文本格式 .gm82 入口。请选择包含 objects、rooms、scripts 的工程文件夹；旧二进制 GMK 需先用 GM8.2 转换。');return found;
}
function constantsFrom(source,fps) {
  const result={room_speed:fps};const create=codeOnly(source).split(/^#define (?!Create_0)[^\n]*$/m)[0];
  const assignments=[...create.matchAll(/\b([A-Za-z_]\w*)\s*=\s*([^;\n{}]+)/g)];
  for(let pass=0;pass<4;pass++)for(const m of assignments){if(m[1] in result)continue;const n=numberExpression(m[2],result);if(n!==null)result[m[1]]=n;}
  return result;
}
function objectFacts(project,name) {
  const file=`objects/${name}.gml`,source=read(path.join(project,file)),code=codeOnly(source),fps=50,c=constantsFrom(source,fps);
  const clocks=[...code.matchAll(/\b([A-Za-z_]\w*)\s*(?:\+\+|\+=\s*1\b|=\s*\1\s*\+\s*1\b)/g)].map(m=>m[1]);
  const uses=[...code.matchAll(/\bswitch\s*\(\s*([A-Za-z_]\w*)\s*\)/g)].map(m=>m[1]);
  const clock=clocks.sort((a,b)=>(uses.includes(b)?20:0)+(/step|time|frame/i.test(b)?10:0)-(uses.includes(a)?20:0)-(/step|time|frame/i.test(a)?10:0))[0]||uses.find(x=>/step|time|frame/i.test(x))||'';
  const timeline=/\b(?:UseTimeLine|timeline_index)\s*=\s*([A-Za-z_]\w*)/i.exec(code)?.[1]||'';
  const timelineMode=Boolean(timeline)&&c.KoisRealtimeCountModeOn!==1;
  const timelineSpeed=numberExpression(/\btimeline_speed\s*=\s*([^;\n]+)/.exec(code)?.[1]||'1',c)??1;
  let score=(/obj.*ad|ad.*obj|avoid|boss.?control/i.test(name)?15:0)+(clock?20:0)+(timeline?25:0)+(code.includes('FMODSoundPlay')?15:0)+([...code.matchAll(/\bcase\b/g)].length>3?25:0);
  if(/_enter|_end|sample|bullet|dan|effect/i.test(name)&&!clock&&!timeline)score-=30;
  return {name,file,source,code,clock,timeline,timelineMode,timelineSpeed,constants:c,score};
}
function eventsFrom(facts,project,fps) {
  const events=[],unresolved=[];let serial=0;
  const add=(file,source,offset,expression,frame,kind,period=0)=>{const line=source.slice(0,offset).split('\n').length;const snippet=source.slice(offset,offset+700).split(/\n\s*(?:case |#define )/)[0].trim();if(frame===null||frame<0||frame>1000000){unresolved.push({file,line,expression,reason:'无法静态换算到帧数'});return;}events.push({id:`native-${serial++}`,frame:Math.round(frame),file,line,expression,kind,period,label:expression,snippet});};
  const source=facts.source,code=facts.code;
  if(facts.clock){const scopes=[];let pending='';for(const m of code.matchAll(/\bswitch\s*\(\s*([^)]*)\)|\bcase\s+([^:\n]+)\s*:|[{}]/g)){if(m[1]!==undefined){pending=m[1].trim();continue;}if(m[0]==='{'){scopes.push(pending);pending='';continue;}if(m[0]==='}'){scopes.pop();continue;}if(m[2]!==undefined&&scopes.filter(Boolean).at(-1)===facts.clock){const value=numberExpression(m[2],facts.constants);add(facts.file,source,m.index,`${facts.clock} = ${m[2].trim()}`,value,'step');}}
    const escaped=facts.clock.replace(/[.*+?^${}()|[\]\\]/g,'\\$&');const test=new RegExp(`\\bif\\s*\\(\\s*${escaped}\\s*(?:==|=|>=|>)\\s*([^)&|;\\n]+)`,'g');for(const m of code.matchAll(test))add(facts.file,source,m.index,`${facts.clock}: ${m[1].trim()}`,numberExpression(m[1],facts.constants),'condition');
    const repeat=new RegExp(`\\b${escaped}\\s+mod\\s+(\\d+(?:\\.\\d+)?)\\s*(?:==|=)\\s*0`,'g');for(const m of code.matchAll(repeat))add(facts.file,source,m.index,`${facts.clock} 每 ${m[1]} 帧`,Number(m[1]),'periodic',Number(m[1]));
  }
  if(facts.timelineMode&&facts.timeline){const file=`timelines/${facts.timeline}.gml`;if(fs.existsSync(path.join(project,file))){const tl=read(path.join(project,file));for(const m of tl.matchAll(/^#define\s+(\d+(?:\.\d+)?)\s*$/gm))add(file,tl,m.index,`${facts.timeline} · moment ${m[1]}`,facts.timelineSpeed>0?Math.ceil(Number(m[1])/facts.timelineSpeed):null,'timeline');}else unresolved.push({file,line:1,expression:facts.timeline,reason:'找不到原生 Timeline 资源'});}
  return {events:events.sort((a,b)=>a.frame-b.frame),unresolved};
}
function musicChoices(project,facts) {
  const files=[];const walk=(dir,depth)=>{if(depth>4||!fs.existsSync(dir))return;for(const e of fs.readdirSync(dir,{withFileTypes:true})){const full=path.join(dir,e.name);if(e.isDirectory())walk(full,depth+1);else if(/\.(ogg|mp3|wav|flac)$/i.test(e.name))files.push(path.relative(project,full).replace(/\\/g,'/'));}};walk(path.join(project,'Data'),0);walk(path.join(project,'sounds'),0);
  let reference=/\b(?:bgmBoss|BGM_Ad|global\.BGM_Av)\s*=\s*FMODSoundAdd\s*\([^;\n]*?['"]([^'"]+\.(?:ogg|mp3|wav|flac))['"]/i.exec(facts.code)?.[1];
  let initializer='';
  if(!reference){const all=[...names(path.join(project,'scripts'),'.gml').map(n=>`scripts/${n}.gml`),...names(path.join(project,'rooms')).flatMap(n=>fs.readdirSync(path.join(project,'rooms',n)).filter(f=>f.endsWith('.gml')).map(f=>`rooms/${n}/${f}`)),...names(path.join(project,'objects'),'.gml').map(n=>`objects/${n}.gml`)];const combined=all.map(f=>codeOnly(read(path.join(project,f)))).join('\n');const assigned=/\bglobal\.BGM_Av\s*=\s*([A-Za-z_]\w*)\b/.exec(combined);if(assigned){initializer=`global.BGM_Av=${assigned[1]};`;reference=new RegExp('\\b'+assigned[1]+'\\s*=\\s*FMODSoundAdd\\s*\\([^;\\n]*?[\'"]([^\'"]+\\.(?:ogg|mp3|wav|flac))[\'"]','i').exec(combined)?.[1];}if(!reference)reference=/\bglobal\.BGM_Av\s*=\s*FMODSoundAdd\s*\([^;\n]*?['"]([^'"]+\.(?:ogg|mp3|wav|flac))['"]/i.exec(combined)?.[1];}
  const detected=reference?files.find(f=>path.basename(f).toLowerCase()===path.basename(reference.replace(/\\/g,'/')).toLowerCase()):'';
  return {files,detected:detected||'',reference:reference||'',initializer};
}
export function analyzeNativeProject(entry,selectedRoom='',selectedController='',allowImported=false) {
  entry=path.resolve(entry);const project=path.dirname(entry);if(!read(entry).startsWith('gm82_version='))throw new Error('不是文本格式的 GM8.2 工程');if(!allowImported&&fs.existsSync(path.join(project,'rufmaker.json')))throw new Error('这个工程已经有 RufMaker 时间轴，请使用“打开工程”。');
  const objects=names(path.join(project,'objects'),'.gml').map(n=>objectFacts(project,n));
  const index=path.join(project,'rooms','index.yyd'),registered=fs.existsSync(index)?read(index).split('\n').filter(Boolean):[];
  const rooms=names(path.join(project,'rooms')).filter(n=>!registered.length||registered.includes(n)).map(name=>{const props=settings(path.join(project,'rooms',name,'room.txt')),inst=read(path.join(project,'rooms',name,'instances.txt')).split('\n').filter(Boolean).map(x=>x.split(',')[0]),roomCode=codeOnly(read(path.join(project,'rooms',name,'code.gml')));const candidates=objects.filter(o=>o.score>10&&(inst.includes(o.name)||new RegExp(`\\b${o.name}\\b`).test(roomCode)||inst.some(n=>objects.find(o=>o.name===n)?.code.includes(o.name)))).map(o=>({name:o.name,score:o.score,direct:inst.includes(o.name)})).sort((a,b)=>b.score+(b.direct?10:0)-a.score-(a.direct?10:0));return {name,width:Number(props.width)||800,height:Number(props.height)||608,fps:Number(props.roomspeed)||50,score:candidates[0]?.score||0,controllers:candidates};}).sort((a,b)=>b.score-a.score);
  if(!rooms.length)throw new Error('工程没有可读取的房间');const room=rooms.find(r=>r.name===selectedRoom)||rooms[0];
  const facts=objects.find(o=>o.name===(selectedController||room.controllers[0]?.name))||{name:'',source:'',code:'',file:'',clock:'',timeline:'',timelineMode:false,timelineSpeed:1,constants:{},score:0};
  // Avoidance engines run at 50 FPS even when old room resources still say 30.
  for(const candidate of rooms)candidate.fps=50;
  const {events,unresolved}=eventsFrom(facts,project,50),music=musicChoices(project,facts),warnings=[];
  if(room.controllers.filter(c=>c.score>=facts.score-10).length>1||rooms.filter(r=>r.score===rooms[0].score).length>1)warnings.push('存在多个可能的耐久房间或控制对象，请在导入窗口确认选择。');
  if(!facts.name)warnings.push('未找到可靠的耐久控制对象。可继续保留原游戏，但不能自动定位事件；请手动选择对象或检查房间创建代码。');
  if(!events.length)warnings.push('没有识别出可定位的计时事件。原 GML 仍会保留；建议检查 Step 中的计时变量、switch/case 或 timeline_index。');
  if(facts.timelineMode&&fs.existsSync(path.join(project,'timelines',facts.timeline+'.gml'))&&!read(path.join(project,'timelines',facts.timeline+'.gml')).trim())warnings.push(`控制对象绑定的 ${facts.timeline} 资源为空；请检查原 GML 的 UseTimeLine 配置，或改选其他耐久房间。`);
  if(unresolved.length)warnings.push(`${unresolved.length} 个时间表达式无法换算帧数，已记入报告；它们仍由原生引擎执行。`);
  if(!music.detected)warnings.push('未能确定主歌曲，请从导入窗口手动选择；找不到音频时仍可静默编辑。');
  const originalStart=facts.constants[facts.clock]??facts.constants.start_pos??0;
  if(originalStart>0)warnings.push(`原控制对象设置了测试起始值 ${originalStart}。原生预览副本会从 0 重放，导出保留原始设置。`);
  const boot=objects.find(o=>/\broom_goto_next\s*\(/.test(o.code)&&/init/i.test(o.name));if(!boot)warnings.push('未识别标准初始化跳转；原生预览使用延迟进入房间，请检查全局变量和插件初始化。');
  warnings.push('原生事件仅显示定位标记，不转换为 UI 关键帧。随机数、鼠标输入、外部计时/DLL 及绘图中修改变量的代码可能影响重放结果。');
  const bpm=facts.constants.MusicBPM||numberExpression(/global\.SPB\s*=\s*3000\s*\/\s*([\d.]+)/.exec(facts.code)?.[1]||'')||120;
  const last=events.at(-1),terminal=last&&/\b(?:room_goto|room_goto_next|game_end)\s*\(/.test(codeOnly(last.snippet));
  const duration=terminal?Math.max(1,last.frame):Math.max(1500,...events.map(e=>e.frame+100));
  return {entry,name:path.basename(project),rooms,room:room.name,controller:facts.name,controllers:objects.sort((a,b)=>b.score-a.score).map(o=>o.name),fps:50,width:room.width,height:room.height,clock:facts.clock,timeline:facts.timelineMode?facts.timeline:'',timelineSpeed:facts.timelineSpeed,originalStart,bpm,musicChoices:music.files,music:music.detected,events,unresolved,warnings,bootObject:boot?.name||'',musicInitializer:music.initializer,confidence:events.length?'已识别':'需要手动检查',duration,hasFmod:objects.some(o=>o.code.includes('FMODSoundPlay'))};
}
export function refreshNativeState(entry,state){
  const project=path.dirname(entry),native=state.nativeProject,fresh=analyzeNativeProject(entry,state.room,native.controller,true),legacy=(native.version||1)<2;
  const roomFiles=names(path.join(project,'rooms')).map(room=>path.join('rooms',room,'room.txt')).filter(file=>fs.existsSync(path.join(project,file)));
  const changes=roomFiles.filter(file=>!/^roomspeed=50\s*$/m.test(read(path.join(project,file))));
  if(legacy||changes.length){
    const backup=path.join(project,'cache','native-migration-v2');
    for(const file of ['rufmaker.json',...changes]){const target=path.join(backup,file);if(!fs.existsSync(target)&&fs.existsSync(path.join(project,file))){fs.mkdirSync(path.dirname(target),{recursive:true});fs.copyFileSync(path.join(project,file),target);}}
    for(const file of changes){const target=path.join(project,file),text=fs.readFileSync(target,'utf8');fs.writeFileSync(target,/^roomspeed=/m.test(text)?text.replace(/^roomspeed=[^\r\n]*/m,'roomspeed=50'):text+'\nroomspeed=50\n');}
  }
  if(legacy||state.duration===native.duration){
    const extra=legacy&&state.duration>native.duration?Math.round(state.duration*50/(state.fps||50)):0;
    const uiEnd=Math.max(0,...state.tracks.filter(t=>t.kind!=='native').map(t=>Math.max(t.end,...t.keyframes.map(k=>k.frame))));
    state.duration=Math.max(fresh.duration,extra,uiEnd);
  }
  for(const key of ['events','unresolved','warnings','clock','timeline','timelineSpeed','bootObject','musicInitializer','confidence','fps','width','height','duration','hasFmod'])native[key]=fresh[key];
  native.version=2;state.fps=50;
  for(const track of state.tracks.filter(t=>t.kind==='native')){track.start=0;track.end=Math.min(state.duration,fresh.duration);}
  return fresh;
}
export function scanNativeFolder(folder,entry='',room='',controller='') {const entries=discoverNativeProjects(folder);if(entry&&!entries.includes(path.resolve(entry)))throw new Error('工程入口不在所选目录中');const result=analyzeNativeProject(entry||entries[0],room,controller);return {...result,entries};}
export function importNativeProject(root,options) {
  const report=analyzeNativeProject(options.entry,options.room,options.controller);const name=String(options.name||report.name+'-导入').trim();if(!name||/[<>:"/\\|?*\x00-\x1f]/.test(name)||/[. ]$/.test(name)||/^(con|prn|aux|nul|com[1-9]|lpt[1-9])$/i.test(name))throw new Error('工程名称无效');
  const parent=path.resolve(options.parent),home=path.join(parent,name),source=path.dirname(report.entry);if(home.toLowerCase().startsWith(source.toLowerCase()+path.sep)||home.toLowerCase()===source.toLowerCase())throw new Error('请选择原工程之外的导入保存目录');if(fs.existsSync(home))throw new Error('目标工程文件夹已存在，请更换名称');
  fs.mkdirSync(home,{recursive:true});const project=path.join(home,'GameMaker.gm82');
  fs.cpSync(source,project,{recursive:true,filter:(file)=>{const rel=path.relative(path.toNamespacedPath(source),path.toNamespacedPath(file)),parts=rel.split(path.sep),base=path.basename(file);return !parts.some(p=>['cache','build','.git','node_modules'].includes(p))&&!/\.(log|tmp)$/i.test(base)&&!(parts.length===1&&/\.exe$/i.test(base));}});
  let music=String(options.music===undefined?report.music||'':options.music);if(music&&!report.musicChoices.includes(music))throw new Error('歌曲不在检测到的音频列表中');
  const spriteNames=names(path.join(project,'sprites'));
  const nativeProject={...report,entry:path.basename(report.entry),source:report.entry,music,imported:true,version:2,defaultSprite:spriteNames.find(n=>/cherry|bullet/i.test(n))||spriteNames[0]||'',killerParent:names(path.join(project,'objects'),'.txt').find(n=>/^playerKiller$/i.test(n))||''};delete nativeProject.rooms;delete nativeProject.controllers;delete nativeProject.musicChoices;
  const duration=Math.max(report.duration,Math.round((Number(options.duration)||0)*report.fps));
  const state={version:2,name,fps:report.fps,bpm:report.bpm,duration,room:report.room,music,musicTitle:path.basename(music),timelineInitialized:false,nativeProject,tracks:[{id:'gm-native',name:'GM 工程 · 原生',kind:'native',start:0,end:duration,enabled:true,keyframes:[]}]};
  refreshNativeState(path.join(project,path.basename(report.entry)),state);
  fs.writeFileSync(path.join(project,'rufmaker.json'),JSON.stringify(state,null,2)+'\n');
  fs.writeFileSync(path.join(home,'native-import-report.json'),JSON.stringify(report,null,2)+'\n');
  const descriptor=path.join(home,name+'.rufmaker');fs.writeFileSync(descriptor,JSON.stringify({format:'rufmaker-project',version:1,name,gameProject:'GameMaker.gm82/'+path.basename(report.entry)},null,2)+'\n');
  return {path:descriptor,warnings:report.warnings,events:report.events.length};
}
