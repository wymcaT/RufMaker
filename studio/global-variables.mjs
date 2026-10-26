import {parseExpression,evaluateExpression,expressionCall} from './random-parameters.mjs';
import {expandPatterns} from './bullet-schema.mjs';
import {gmlNumber as num} from './gml.mjs';
export const globalReference=name=>/^global\.[a-z_][a-z0-9_]*$/.test(name);
export function globalName(name){name=String(name??'').trim().toLowerCase();if(!/^[a-z_][a-z0-9_]*$/.test(name)||name.startsWith('rf_'))throw new Error('变量名请使用英文字母、数字和下划线，不能以数字或 rf_ 开头。');return name;}
export function attachGlobals(state){
  if(state._globalSource){Object.defineProperty(state,'_globalsTimeline',{value:null,writable:true,configurable:true});return state;}
  const has=tracks=>tracks.some(t=>t.kind==='variable'||has(t.children||[]));if(!has(state.tracks)){Object.defineProperty(state,'_globalsTimeline',{value:null,writable:true,configurable:true});return state;}
  const flat=state.tracks.some(t=>t.kind==='pattern')?expandPatterns(state):state,tracks=flat.tracks.filter(t=>t.kind==='variable');
  for(const t of tracks){t.variableName=globalName(t.variableName??'phase');t.variableExpression=String(t.variableExpression??'random(360)').trim()||'0';t.variableDefault??=0;t.variableRepeat??=0;if(!Number.isFinite(t.variableDefault)||!Number.isInteger(t.variableRepeat)||t.variableRepeat<0)throw new Error('全局变量参数无效。');parseExpression(t.variableExpression);}
  const timeline=tracks.length?new GlobalTimeline({...state,tracks:flat.tracks},tracks):null;Object.defineProperty(state,'_globalsTimeline',{value:timeline,writable:true,configurable:true});return state;
}
export function globalsAt(state,frame){return state?._globalSource?globalsAt(state._globalSource,frame):state?._globalsTimeline?.at(Math.max(0,Math.floor(frame)))??null;}
class GlobalTimeline{
  constructor(state,tracks){this.state=state;this.tracks=tracks;this.current={};this.history={};this.events=[];this.cachedFrame=-1;
    tracks.forEach((t,i)=>{if(!(t.variableName in this.current)){this.current[t.variableName]=t.variableDefault;this.history[t.variableName]=[{frame:-1,value:t.variableDefault}];}let first=t.clockStart??t.start;if(t.variableRepeat>0)first+=Math.max(0,Math.ceil((t.start-first)/t.variableRepeat))*t.variableRepeat;if(t.enabled!==false&&!t.templateOnly&&first>=t.start&&first<=t.end)this.events.push({frame:first,index:i});});
    const visit=n=>{if(globalReference(n.op)&&!(n.op.slice(7) in this.current))throw new Error('未定义全局变量：'+n.op);n.args.forEach(visit);};tracks.forEach(t=>visit(parseExpression(t.variableExpression)));
  }
  at(frame){if(frame===this.cachedFrame)return this.cached;
    while(true){this.events.sort((a,b)=>a.frame-b.frame||a.index-b.index);const e=this.events[0];if(!e||e.frame>frame)break;this.events.shift();const t=this.tracks[e.index];let parent=this.state.tracks.find(p=>p.id===t.parentId&&p.kind==='group'),active=true;while(parent){active&&=parent.enabled!==false&&e.frame>=parent.start&&e.frame<=parent.end;parent=this.state.tracks.find(p=>p.id===parent.parentId&&p.kind==='group');}
      if(active){let px=this.state.nativeTest?.x??400,py=this.state.nativeTest?.y??304;const trace=this.state.playerTrace||[];if(this.state.usePlayerTrace&&trace.length&&e.frame>=trace[0].frame&&e.frame<=trace.at(-1).frame){let lo=0,hi=trace.length-1;while(lo<hi){const mid=Math.ceil((lo+hi)/2);if(trace[mid].frame<=e.frame)lo=mid;else hi=mid-1;}const a=trace[lo],b=trace[Math.min(lo+1,trace.length-1)],u=(e.frame-a.frame)/Math.max(1,b.frame-a.frame);px=a.x+(b.x-a.x)*u;py=a.y+(b.y-a.y)*u;}const c={globals:this.current,'player.x':px,'player.y':py,x:t.x??400,y:t.y??304,'source.x':t.x??400,'source.y':t.y??304,x1:t.x1??280,y1:t.y1??304,x2:t.x2??520,y2:t.y2??304,direction:t.angle??0,speed:t.speed??3,frame:e.frame,age:e.frame-(t.clockStart??t.start),fps:this.state.fps,randomFrame:e.frame};const value=evaluateExpression(t.variableExpression,this.state.randomSeed??12345,`${t.randomId||t.id}|variable|base`,c);this.current[t.variableName]=value;this.history[t.variableName].push({frame:e.frame,value});}
      if(t.variableRepeat>0&&e.frame+t.variableRepeat<=Math.min(t.end,this.state.duration))this.events.push({frame:e.frame+t.variableRepeat,index:e.index});
    }
    const result={};for(const [name,list] of Object.entries(this.history)){let lo=0,hi=list.length-1;while(lo<hi){const mid=Math.ceil((lo+hi)/2);if(list[mid].frame<=frame)lo=mid;else hi=mid-1;}result[name]=list[lo].value;}this.cachedFrame=frame;return this.cached=result;
  }
}
export function globalScripts(state){const rows=state.tracks.filter(t=>t.kind==='variable'),seen=new Set(),init=['rf_global_last_frame=-1;'];
  for(const t of rows)if(!seen.has(t.variableName)){seen.add(t.variableName);init.push(`global.${t.variableName}=${num(t.variableDefault)};`);}
  const step=['var rf_now;rf_now=argument0;if rf_now<=objRufController.rf_global_last_frame return 0;objRufController.rf_global_last_frame=rf_now;'];
  for(const t of rows.filter(t=>t.enabled!==false&&!t.templateOnly)){let condition=t.variableRepeat>0?`rf_now>=${num(t.start)} && rf_now<=${num(t.end)} && (rf_now-(${num(t.clockStart??t.start)})) mod ${num(t.variableRepeat)}=0`:`rf_now=${num(t.clockStart??t.start)} && rf_now>=${num(t.start)} && rf_now<=${num(t.end)}`;let parent=state.tracks.find(p=>p.id===t.parentId&&p.kind==='group');while(parent){condition+=parent.enabled===false?' && 0':` && rf_now>=${num(parent.start)} && rf_now<=${num(parent.end)}`;parent=state.tracks.find(p=>p.id===parent.parentId&&p.kind==='group');}step.push(`if ${condition} {global.${t.variableName}=${expressionCall(t.variableExpression,t,'variable',null,{frame:'rf_now',randomFrame:'rf_now'})};}`);}
  return [['rf_global_init',init.join('\n')+'\nreturn 0;'],['rf_global_step',step.join('\n')+'\nreturn 0;']];}
