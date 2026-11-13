// Executes generated numeric GML subsets only. This is NOT a GameMaker runner.
import fs from 'node:fs';import vm from 'node:vm';import assert from 'node:assert/strict';
import {normalizeState} from '../../studio/motion.mjs';
import {parameterCode,expressionScripts} from '../../studio/random-parameters.mjs';
import {bulletImageCode,rainbowGml,randomRainbowSequence} from '../../studio/rainbow.mjs';
const rows=JSON.parse(fs.readFileSync(process.argv[2],'utf8'));assert.equal(rows.length,96);
let checks=0;
const numeric=g=>g.replace(/\bmod\b/g,'%');
for(const row of rows){
 const state=normalizeState({version:2,randomSeed:row.seed,tracks:[row.track]});const t=state.tracks[0];
 const c={floor:Math.floor,max:Math.max,min:Math.min,clamp:(n,a,b)=>Math.max(a,Math.min(b,n)),rf_idx:row.index,rf_n:row.count,rf_wave:row.volley,rf_sample_time:row.birth-row.clockShift,rf_source_x:row.sourceX,rf_source_y:row.sourceY,rf_instance_random:(row.ownerBirth??0)*104729+(row.ownerSerial??0)*13007,objRufController:{rf_random_seed:row.seed},rf_expression_nonfinite:()=>{throw Error('nonfinite');}};
 for(const [name,gml] of expressionScripts(state,[]))if(name==='rf_random_mix')c[name]=argument0=>vm.runInNewContext('(function(){'+numeric(gml)+'})()',{...c,argument0});
 for(const [name,gml] of expressionScripts(state,[]))if(name.startsWith('rf_expr_'))c[name]=(...args)=>{const local={...c,rf_rng_state:0,...Object.fromEntries(args.map((v,i)=>['argument'+i,v]))};local.rf_random_next=()=>{local.rf_rng_state=local.rf_rng_state*16807%2147483647;return(local.rf_rng_state-1)/2147483646;};return vm.runInNewContext('(function(){'+numeric(gml)+'})()',local);};
 const context={birthSampling:true,randomMix:t.randomPerInstance,sourceX:'rf_source_x',sourceY:'rf_source_y',i:'rf_idx',n:'rf_n',volley:'rf_wave',frame:'rf_sample_time',randomFrame:'0',randomOffset:'('+(t.randomPerInstance?'rf_instance_random+':'')+(t.randomScope==='group'?'0':'rf_wave*1009'+(t.randomScope==='bullet'?'+rf_idx*9176':''))+')'};
 const actual={};for(const [field,key] of [['rainbowDensity','density'],['rainbowOffset','offset'],['rainbowSpeed','paletteSpeed']]){actual[key]=vm.runInNewContext(numeric(parameterCode(t,field,t[field],null,context)),c);assert.ok(Math.abs(actual[key]-row[key])<1e-10,field+' differs: '+actual[key]+' / '+row[key]);checks++;}
 const sequence=t.rainbowRandom?vm.runInNewContext(numeric(randomRainbowSequence(t,context)),c):row.volley*row.count+row.index;assert.equal(sequence,row.sequence);checks++;
 const palette=(...args)=>vm.runInNewContext('(function(){'+numeric(rainbowGml).replace(/if (argument\d+) (p[^;]*;)/g,'if ($1) $2')+'})()',{...c,...Object.fromEntries(args.map((v,i)=>['argument'+i,v]))});
 const frameContext={...c,rf_image_frozen:0,rf_frozen_image:-1,rf_rainbow:1,rf_r_sequence:sequence,rf_r_density:actual.density,rf_r_offset:actual.offset,rf_r_speed:actual.paletteSpeed,rf_r_start:(t.clockStart??t.start)+row.clockShift,rf_r_dynamic:t.rainbowDynamic?1:0,rf_r_reverse:t.rainbowReverse?1:0,rf_world:row.worldFrame,image_index:0,rf_rainbow_index:palette};
 const image=vm.runInNewContext(bulletImageCode('rf_world'),frameContext);assert.equal(image,row.image);checks++;
}
console.log(JSON.stringify({status:'pass',rows:rows.length,comparisons:checks,scope:'C# birth samples vs executed generated scalar, rainbow and frame expressions; not native GM'}));
