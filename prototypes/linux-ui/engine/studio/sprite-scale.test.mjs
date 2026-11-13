import test from 'node:test';import assert from 'node:assert/strict';import fs from 'node:fs';import vm from 'node:vm';
import {normalizeState,fields,defaults,valueAt,stateCode,ease} from './motion.mjs';
import {expressionScripts,expressionScriptName,evaluateExpression} from './random-parameters.mjs';
import {bulletSystemScripts} from './bullet-system.mjs';import {groupEventScripts,setEventProperty} from './group-events.mjs';import {templateScripts} from './template-system.mjs';
import {setSpriteScale,setUniformScale} from './sprite-scale.mjs';import {trailCall,trailGml,trailXYGml} from './trails.mjs';
const track=(extra={})=>({id:'e',kind:'emitter',start:0,end:10,interval:1,count:1,speed:0,life:20,sprite:'sprBullet',keyframes:[],...extra});
const scene=(...tracks)=>normalizeState({version:2,fps:50,duration:100,tracks});
const run=(code,c={})=>{vm.runInNewContext(code,{...math,...c});return c;};
const math={min:Math.min,max:Math.max,floor:Math.floor,abs:Math.abs,sqr:x=>x*x,rf_ease:ease,lengthdir_x:(r,a)=>r*Math.cos(a*Math.PI/180),lengthdir_y:(r,a)=>-r*Math.sin(a*Math.PI/180)};
// Numeric generator check only: interpret the limited emitted subset, not GameMaker execution.
function gmlSubset(code){return code.replace(/\bmod\b/g,'%').replace(/\bif ([^\n{]+?) (return [^;]+;|\{)/g,(_,c,a)=>'if ('+c.replace(/(?<![<>=!])=(?!=)/g,'===')+') '+a).replace(/\bif ([^\n]+?) ([zo][*]?=[^;]+;)/g,(_,c,a)=>'if ('+c.replace(/(?<![<>=!])=(?!=)/g,'===')+') '+a);}
function trail(code,args){let draws=[];let context={...math,sprite_get_number:()=>8,draw_sprite_ext:(...v)=>draws.push(v)};args.forEach((v,i)=>context['argument'+i]=v);vm.runInNewContext('(function(){'+gmlSubset(code)+'})()',context);return draws;}
test('axes default to one, preserve signed zero crossings, keys and expressions',()=>{
 const s=scene(track());assert.equal(s.tracks[0].scaleX,1);assert.equal(s.tracks[0].scaleY,1);for(const p of ['scaleX','scaleY'])assert(fields.includes(p)&&defaults[p]===1);
 const t=scene(track({scale:2,scaleY:-3,keyframes:[{property:'scaleX',frame:0,value:-2},{property:'scaleX',frame:10,value:2}]})).tracks[0];
 for(const f of [0,2,5,7,10]){let c={...math,rf_frame:f};vm.runInNewContext(stateCode(t,'rf_frame'),c);assert.equal(c.rf_scaleX,valueAt(t,'scaleX',f));assert.equal(c.rf_scaleY,-3);}assert.equal(valueAt(t,'scaleX',5),0);
 const e=scene(track({expressions:{scaleX:'at(i,-2,0,3)',scaleY:'1+age/10'}}));assert.equal(e.tracks[0].expressions.scaleX,'at(i,-2,0,3)');assert.ok(expressionScripts(e,[]).some(([n])=>n===expressionScriptName(e.tracks[0].expressions.scaleX)));assert.equal(evaluateExpression(e.tracks[0].expressions.scaleX,1,'scaleX',{i:1}),0);
 const round=scene(JSON.parse(JSON.stringify(t))).tracks[0];assert.equal(round.scaleY,-3);assert.equal(valueAt(round,'scaleX',7),valueAt(t,'scaleX',7));
});
test('uniform controls preserve signed independent axes without dividing by zero',()=>{
 for(const axes of [[-2,3],[0,-4],[1,1]]){const c={max:Math.max,rf_scale:2,rf_scaleX:axes[0],rf_scaleY:axes[1],b:{}};vm.runInNewContext(setSpriteScale('b'),c);assert.equal(c.b.image_xscale,2*axes[0]);assert.equal(c.b.image_yscale,2*axes[1]);
 Object.assign(c,c.b);vm.runInNewContext(setUniformScale('5'),c);assert.equal(c.image_xscale,5*axes[0]);assert.equal(c.image_yscale,5*axes[1]);vm.runInNewContext(setEventProperty('scale','0'),c);assert.equal(Math.abs(c.image_xscale)+Math.abs(c.image_yscale),0);vm.runInNewContext(setUniformScale('2'),c);assert.equal(c.image_yscale,2*axes[1]);}
});
test('advanced spawn, plain split, life scaling and trail histories retain both axes',()=>{
 const s=scene(track({advancedBullets:true,scaleX:-2,scaleY:3,deathEffect:'shrink',deathFrames:10,trailMode:'fade',bulletActions:[{type:'scale',start:1,duration:0,to:2},{type:'split',start:2,duration:0,count:1}]}));const code=Object.fromEntries(bulletSystemScripts(s));
 assert.match(code.rf_pattern_spawn,/rf_result.rf_axis_x=rf_scaleX;rf_result.rf_axis_y=rf_scaleY;/);assert.match(code.rf_pattern_tick,/rf_size=max\(0,rf_value\);image_xscale=rf_size\*rf_axis_x;image_yscale=rf_size\*rf_axis_y;/);assert.match(code.rf_pattern_tick,/rf_child.rf_size=rf_size;rf_child.rf_axis_x=rf_axis_x;rf_child.rf_axis_y=rf_axis_y;/);assert.match(code.rf_pattern_tick,/rf_hsy\[rf_slot\]=image_yscale/);assert.match(code.rf_pattern_spawn,/rf_result.rf_hsy\[0\]=rf_result.image_yscale/);
 const lifetime=fs.readFileSync(new URL('./bullet-lifetime.mjs',import.meta.url),'utf8');assert.match(lifetime,/image_xscale\*=rf_die_factor;image_yscale\*=rf_die_factor/);
});
test('group copies, fixed template axes and uniform size inheritance use separate quantities',()=>{
 const s=scene(track({lane:0,scaleX:-2,scaleY:3}),track({id:'copy',kind:'control',lane:1,eventPreset:'group-copy',start:2,end:2,eventGroup:'g'}),track({id:'host',kind:'object',spawnActions:[{type:'spawn',start:0,duration:0,templateId:'template',inheritScale:true}]}),track({id:'template',kind:'rod',templateOnly:true}));s.timelineGroups=[{id:'g',lanes:[0,1]}];
 const g=Object.fromEntries(groupEventScripts(s));assert.match(g.rf_event_clone,/rf_new.rf_axis_x=rf_axis_x;rf_new.rf_axis_y=rf_axis_y/);assert.match(g.rf_event_clone,/rf_new.image_xscale=rf_new.rf_size\*rf_new.rf_axis_x/);
 const t=templateScripts(s).map(x=>x[1]).join('\n');assert.match(t,/self.rf_axis_x=rf_scaleX;self.rf_axis_y=rf_scaleY/);assert.match(t,/self.rf_hsy\[self.rf_age mod 301\]=self.image_yscale/);
 const a=fs.readFileSync(new URL('./spawn-appearance.mjs',import.meta.url),'utf8');assert.match(a,/rf_size\*=rf_owner.rf_scale_multiplier;image_xscale=rf_size\*rf_axis_x;image_yscale=rf_size\*rf_axis_y/);
});
test('XY trail helper is 15 arguments and preserves legacy default-axis output across modes',()=>{
 for(const mode of ['off','fade','shrink','shrink-fade','appear','custom'])for(const age of [-1,0,2,9,10]){
  const old=[10,20,30,2,45,.8,16777215,age,mode,10,.6,.08,.04,3,30,4],d=3*age;
  const next=[10,20+math.lengthdir_x(d,30),30+math.lengthdir_y(d,30),2,2,45,.8,16777215,age,mode,10,.6,.08,.04,4];assert.deepEqual(trail(trailXYGml,next),trail(trailGml,old));
 }
 const draw=trail(trailXYGml,[10,20,30,-4,6,0,1,123,5,'shrink',10,1,0,0,0])[0];assert.equal(draw[4],-2);assert.equal(draw[5],3);
 const t=scene(track({trailMode:'fade'})).tracks[0],call=trailCall(t,{color:'123',age:'5',seed:'0'});let depth=0,n=1;for(const c of call.slice(call.indexOf('(')+1,call.lastIndexOf(')'))){if(c==='(')depth++;if(c===')')depth--;if(c===','&&!depth)n++;}assert.equal(n,15);assert.match(call,/\(rf_scale\)\*\(rf_scaleX\),\(rf_scale\)\*\(rf_scaleY\)/);
});
test('object and simple emitter/rod exports wire axes and helper resources',()=>{
 const p=fs.readFileSync(new URL('./project.mjs',import.meta.url),'utf8');assert.match(p,/draw_sprite_ext\(\$\{t.sprite\}[^\n]*rf_scale\*rf_scaleX,rf_scale\*rf_scaleY/);assert.match(p,/\['rf_trail_draw_xy',trailXYGml\]/);assert.match(p,/rf_tsy=rf_hsy\[rf_slot\]/);assert.match(p,/rf_size=1;rf_axis_x=1;rf_axis_y=1;rf_hsy\[0\]=1/);assert.equal((p.match(/setSpriteScale\('rf_inst'\)/g)||[]).length,2);
 const cs=fs.readFileSync(new URL('../desktop/GroupEvents.cs',import.meta.url),'utf8');assert.match(cs,/axisX=original.axisX,axisY=original.axisY/);
});
