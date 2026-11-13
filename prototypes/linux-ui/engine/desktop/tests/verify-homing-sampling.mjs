// Execute the homing assignments and numeric tick subset produced by the real exporter.
// This is deliberately not a GameMaker runner, collision test or pixel test.
import fs from 'node:fs';
import vm from 'node:vm';
import assert from 'node:assert/strict';
import {normalizeState} from '../../studio/motion.mjs';
import {bulletSystemScripts} from '../../studio/bullet-system.mjs';
import {expressionScripts,parameterCode} from '../../studio/random-parameters.mjs';
import {flightSpawn} from '../../studio/bullet-flight.mjs';
const rows=JSON.parse(fs.readFileSync(process.argv[2],'utf8'));assert.equal(rows.length,72);
let checks=0;
const numeric=g=>g.replace(/\bmod\b/g,'%');
const near=(a,b,label)=>{assert.ok(Math.abs(a-b)<1e-9,label+': '+a+' / '+b);checks++;};
function context(state,row){
 const c={floor:Math.floor,round:Math.round,max:Math.max,min:Math.min,abs:Math.abs,rf_idx:row.index,rf_n:row.count,rf_wave:row.volley,rf_sample_time:row.birth-row.clockShift,rf_birth_source_x:row.sourceX,rf_birth_source_y:row.sourceY,rf_instance_random:row.ownerBirth*104729+row.ownerSerial*13007,objRufController:{rf_random_seed:row.seed},rf_expression_player:i=>i===0?400:304,rf_expression_nonfinite:()=>{throw Error('nonfinite');}};
 for(const [name,gml] of expressionScripts(state,[]))if(name==='rf_random_mix')c[name]=argument0=>vm.runInNewContext('(function(){'+numeric(gml)+'})()',{...c,argument0});
 for(const [name,gml] of expressionScripts(state,[]))if(name.startsWith('rf_expr_'))c[name]=(...args)=>{const local={...c,rf_rng_state:0,...Object.fromEntries(args.map((v,i)=>['argument'+i,v]))};local.rf_random_next=()=>{local.rf_rng_state=local.rf_rng_state*16807%2147483647;return(local.rf_rng_state-1)/2147483646;};return vm.runInNewContext('(function(){'+numeric(gml)+'})()',local);};
 return c;
}
for(const row of rows){
 const state=normalizeState({version:2,fps:50,randomSeed:row.seed,tracks:[row.track]});const scripts=Object.fromEntries(bulletSystemScripts(state));const c=context(state,row);
 const turn=scripts.rf_pattern_spawn.match(/rf_result\.rf_turn=([^;]+);/)[1],delay=scripts.rf_pattern_spawn.match(/rf_result\.rf_delay=([^;]+);/)[1];
 const sampledTurn=vm.runInNewContext(numeric(turn),c),sampledDelay=vm.runInNewContext(numeric(delay),c);near(sampledTurn,row.turn,'birth turn');near(sampledDelay,row.delay,'birth delay');
 const home=scripts.rf_pattern_tick.match(/case 0: if !rf_motion_reset && rf_age>rf_delay \{([^}]+)\}/)[1];
 assert.ok(scripts.rf_pattern_tick.includes('x+=lengthdir_x(rf_speed,rf_dir);y+=lengthdir_y(rf_speed,rf_dir);'));
 const first=row.trajectory[0];assert.equal(first.frame,row.birth);const p={...c,x:first.x,y:first.y,rf_dir:first.direction,rf_turn:sampledTurn,rf_delay:sampledDelay,rf_speed:row.track.speed,point_direction:(x,y,tx,ty)=>(Math.atan2(y-ty,tx-x)*180/Math.PI+360)%360,lengthdir_x:(s,d)=>s*Math.cos(d*Math.PI/180),lengthdir_y:(s,d)=>-s*Math.sin(d*Math.PI/180)};
 for(const point of row.trajectory){const age=point.frame-row.birth;if(age>sampledDelay)vm.runInNewContext(numeric(home),p);if(age>0)vm.runInNewContext('x+=lengthdir_x(rf_speed,rf_dir);y+=lengthdir_y(rf_speed,rf_dir);',p);near(p.rf_dir,point.direction,'trajectory direction');near(p.x,point.x,'trajectory x');near(p.y,point.y,'trajectory y');}
}
// Exact same scalar contract for the ordinary, non-advanced flight exporter.
for(const [field,expression,expected] of [['turnRate','-9+i',0],['homingDelay','1.5+i',2],['homingDelay','-1+i',0]]){
 const state=normalizeState({tracks:[{id:'edge',kind:'emitter',flightMode:'turn',turnRate:3,homingDelay:0,expressions:{[field]:expression},keyframes:[]}]});const t=state.tracks[0];const row={index:0,count:1,volley:0,birth:0,clockShift:0,sourceX:t.x,sourceY:t.y,ownerBirth:0,ownerSerial:0,seed:state.randomSeed};const c=context(state,row);
 const value=parameterCode(t,field,t[field],null,{birthSampling:true,i:'rf_idx',frame:'rf_sample_time',randomFrame:'0'});near(vm.runInNewContext(numeric(value),c),expected,'edge '+field+' '+expression);
 const ordinary=flightSpawn(t,0);assert.ok(ordinary.includes(field==='turnRate'?'rf_inst.rf_turn=max(0,':'rf_inst.rf_delay=max(0,round('));checks++;
}
console.log(JSON.stringify({status:'pass',rows:rows.length,comparisons:checks,scope:'C# homing birth samples and trajectories vs executed generated numeric GML; not native GM'}));
