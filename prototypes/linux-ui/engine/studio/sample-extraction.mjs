// Copy-only editing operation. Does not normalize, expand templates or touch native GML.
import {parseExpression,validateTrackSampleReferences} from './random-parameters.mjs';
import {validateSampleTables} from './named-samples.mjs';
const binary=new Set(['+','-','*','/','%','<','>','<=','>=','==','!=']);
const literal=n=>n.op==='number'?n.number:n.op==='neg'&&n.args[0].op==='number'?-n.args[0].number:undefined;
const key=n=>n.op==='at'&&n.args.length>=2&&n.args.slice(1).every(a=>literal(a)!==undefined)?JSON.stringify(n.args.slice(1).map(literal)):null;
function fields(t){
 const out=[],add=(o,k,path)=>{if(typeof o?.[k]==='string'&&o[k].trim())out.push({o,k,path});};
 const map=(o,path)=>{for(const k of Object.keys(o||{}))add(o,k,path+'.'+k);};
 map(t.expressions,'expressions');(t.keyframes||[]).forEach((k,i)=>add(k,'expression',`keyframes[${i}].expression`));
 for(const collection of ['bulletActions','spawnActions'])(t[collection]||[]).forEach((a,i)=>{const p=`${collection}[${i}]`;for(const k of ['expression','condition','spawnColorExpression'])add(a,k,p+'.'+k);map(a.expressions,p+'.expressions');});
 if(t.flightMode==='expression')add(t,'flightExpression','flightExpression');
 if(t.kind==='variable')add(t,'variableExpression','variableExpression');
 if(t.kind==='control')add(t,'controlExpression','controlExpression');
 if(t.colorMode==='random')add(t,'colorExpression','colorExpression');
 return out;
}
function emit(n,tables){
 const name=tables.get(key(n));if(name)return `sample.${name}(${emit(n.args[0],tables)})`;
 if(n.op==='number')return Object.is(n.number,-0)?'-0':String(n.number);
 if(n.op==='neg')return `(-${emit(n.args[0],tables)})`;
 if(binary.has(n.op))return `(${emit(n.args[0],tables)}${n.op}${emit(n.args[1],tables)})`;
 return n.args.length?`${n.op}(${n.args.map(a=>emit(a,tables)).join(',')})`:n.op;
}
export function extractSampleTables(state,{minValues=4,minReferences=2}={}){
 if(!state||!Array.isArray(state.tracks))throw Error('Expected an editable scene JSON with tracks.');
 if(!Number.isInteger(minValues)||minValues<1||minValues>4096||!Number.isInteger(minReferences)||minReferences<1)throw Error('Invalid extraction thresholds.');
 const result=structuredClone(state),report=[];
 for(const [trackIndex,t] of result.tracks.entries()){
  validateSampleTables(t.sampleTables);const parsed=fields(t).map(f=>({...f,ast:parseExpression(f.o[f.k])})),found=new Map();
  const visit=(n,path)=>{const k=key(n);if(k&&n.args.length-1>=minValues&&n.args.length-1<=4096){if(!found.has(k))found.set(k,[]);found.get(k).push(path);}n.args.forEach(a=>visit(a,path));};
  parsed.forEach(f=>visit(f.ast,f.path));const replacements=new Map(),tables={...(t.sampleTables||{})};
  for(const [k,uses] of found){if(uses.length<minReferences)continue;
   let name=Object.keys(tables).find(n=>tables[n].mode==='clamp'&&JSON.stringify(tables[n].values)===k),created=false;
   if(!name){if(Object.keys(tables).length>=64)continue;let i=1;do{name='shared_'+i++;}while(Object.hasOwn(tables,name));tables[name]={mode:'clamp',values:JSON.parse(k)};created=true;}
   replacements.set(k,name);report.push({trackIndex,trackId:t.id??null,table:name,created,values:tables[name].values.length,references:uses.length,fields:uses});
  }
  if(!replacements.size)continue;t.sampleTables=tables;
  for(const f of parsed){let changes=false;const check=n=>{if(replacements.has(key(n)))changes=true;n.args.forEach(check);};check(f.ast);if(changes)f.o[f.k]=emit(f.ast,replacements);}
  validateTrackSampleReferences(t);
 }
 return {state:result,report};
}
