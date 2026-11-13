// Focused executor for emitted scalar arithmetic/assignment only, NOT a GM runtime.
import vm from 'node:vm';
import {expressionScripts,mixRandomSeed} from '../studio/random-parameters.mjs';
import {eventAssignment} from '../studio/group-events.mjs';
export function eventExpressionRuntime(state,overrides={}){
 const context={global:{cx:100,cy:200,omega:.8,radius:70,friction:.2},objRufController:{rf_random_seed:103},rf_source:0,x:100,y:200,rf_world:2,rf_birth:0,rf_dir:0,rf_speed:0,rf_index:0,rf_n:1,rf_volley:0,rf_generation:0,rf_source_x:100,rf_source_y:200,rf_logic_alpha:1,rf_result:0,
 point_distance:(x1,y1,x2,y2)=>Math.hypot(x2-x1,y2-y1),floor:Math.floor,min:Math.min,max:Math.max,abs:Math.abs,rf_random_mix:mixRandomSeed,rf_expression_nonfinite:()=>{throw Error('nonfinite');},...overrides};
 for(const [name,gml] of expressionScripts(state,[])){
  if(!name.startsWith('rf_expr_'))continue;
  const js=gml.replace(/(else )?if ([^\n]+?) (rf_random_arg\d+=.*?;)/g,(_,e,c,a)=>(e||'')+'if ('+c.replace(/(?<![<>=!])=(?!=)/g,'===')+') '+a).replace(/\bmod\b/g,'%');
  context[name]=(...args)=>{const local={...context,...Object.fromEntries(args.map((v,i)=>['argument'+i,v]))};local.rf_random_next=()=>{local.rf_rng_state=local.rf_rng_state*16807%2147483647;return(local.rf_rng_state-1)/2147483646;};return vm.runInNewContext('(function(){'+js+'})()',local);};
 }
 return {context,value(event,property){const code=eventAssignment(event,property,state,'rf_result').replace(/\bmod\b/g,'%');return vm.runInNewContext(code+';rf_result;',context);}};
}
