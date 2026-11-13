import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
import {normalizeState} from './motion.mjs';
import {advancedBullets,expandPatterns} from './bullet-schema.mjs';
import {bulletSystemScripts} from './bullet-system.mjs';
import {evaluateExpression,expressionScripts,expressionScriptName,parseExpression,expressionCall} from './random-parameters.mjs';
const emitter=(extra={})=>({id:'e',name:'Emitter',kind:'emitter',start:0,end:0,count:1,speed:5,life:20,sprite:'sprBullet',keyframes:[],...extra});
test('birth motion defaults off, opts into simulator and survives JSON/clip expansion',()=>{
 const a=normalizeState({tracks:[emitter()]});assert.equal(a.tracks[0].moveOnBirth,false);assert.equal(advancedBullets(a),false);
 const b=normalizeState({tracks:[emitter({moveOnBirth:true})]});assert.equal(advancedBullets(b),true);assert.equal(normalizeState(JSON.parse(JSON.stringify(b))).tracks[0].moveOnBirth,true);
 const c=normalizeState({duration:100,tracks:[{id:'clip',kind:'pattern',start:10,end:40,repeatCount:2,children:[emitter({moveOnBirth:true})]}]});assert.ok(expandPatterns(c).tracks.every(t=>t.moveOnBirth));
 assert.throws(()=>normalizeState({tracks:[emitter({moveOnBirth:1})]}),/布尔/);
});
test('export birth step changes order gate, not birth coordinate or age; consumption precedes descendants',()=>{
 const s=normalizeState({tracks:[emitter({moveOnBirth:true,bulletActions:[{type:'speed',start:0,duration:10,expression:'speed*0.5'},{type:'split',start:0,duration:0,count:2}]})]});
 const g=Object.fromEntries(bulletSystemScripts(s));assert.match(g.rf_pattern_spawn,/rf_result.rf_birth_move=1;/);assert.match(g.rf_pattern_tick,/if rf_age>0 \|\| rf_birth_move/);
 const tick=g.rf_pattern_tick;assert.ok(tick.indexOf('rf_speed=')<tick.indexOf('if rf_age>0 || rf_birth_move'));assert.ok(tick.indexOf('rf_birth_move=0;')<tick.indexOf('rf_template_bullet_actions(rf_world)'));assert.ok(tick.indexOf('rf_birth_move=0;')<tick.indexOf('for(rf_split_i='));
 assert.match(fs.readFileSync(new URL('./project.mjs',import.meta.url),'utf8'),/rf_birth_move=0;/);
 const legacy=Object.fromEntries(bulletSystemScripts(normalizeState({tracks:[emitter()]})));assert.match(legacy.rf_pattern_spawn,/rf_result.rf_birth_move=0;/);
});
test('indexed value uses zero based floor and clamp, rejects empty list, supports large indices',()=>{
 for(const [i,want] of [[-1e100,11],[-.1,11],[0,11],[.99,11],[1.99,22],[2,33],[1e100,33]])assert.equal(evaluateExpression(`at(${i},11,22,33)`,1,'x'),want);
 assert.equal(evaluateExpression('at(8,42)',1,'x'),42);assert.throws(()=>parseExpression('at()'));assert.throws(()=>parseExpression('at(3)'));
 assert.equal(evaluateExpression('at(i%3,11,22,33)',1,'x',{i:5}),33);
 assert.equal(evaluateExpression('at(1,at(0,3,4),5+6)',1,'x'),11);
 assert.equal(evaluateExpression('at(1,1,2)+random(100)',123,'x'),2+evaluateExpression('random(100)',123,'x'),'at itself consumes no random draw');
});
test('100 member values compile within the existing 16 argument expression-call ABI',()=>{
 const expr='at(i,'+Array.from({length:100},(_,i)=>(i*.123-5).toPrecision(17)).join(',')+')';const t=emitter({expressions:{speed:expr}});const s=normalizeState({tracks:[t]});
 assert.ok(expr.length<32767);const call=expressionCall(expr,t);let depth=0,args=1;for(const ch of call.slice(call.indexOf('(')+1,-1)){if(ch==='(')depth++;if(ch===')')depth--;if(ch===','&&depth===0)args++;}assert.equal(args,16);
 const code=Object.fromEntries(expressionScripts(s,[]))[expressionScriptName(expr)];assert.doesNotMatch(code,/\bat\(/);assert.doesNotMatch(code,/rf_random_next\(/);assert.match(code,/max\(0,min\(99,floor\(/);
 // Execute the emitted straight-line numeric GML subset in JS as a generator check,
 // not a GM compiler/runtime claim. This catches branch/variable-order regressions.
 const body=code.replace(/(else )?if ([^\n]+?) (rf_random_arg\d+=.*?;)/g,(_,e,c,a)=>(e||'')+'if ('+c.replace(/(?<![<>=!])=(?!=)/g,'===')+') '+a);
 for(const i of [-1,.99,55.7,99,101]){const context={argument10:1,argument12:i,floor:Math.floor,min:Math.min,max:Math.max};const got=vm.runInNewContext('(function(){'+body+'})()',context);assert.equal(got,evaluateExpression(expr,1,'x',{i}));}
});
test('native property and expression template are wired with both language labels',()=>{
 const tools=fs.readFileSync(new URL('../desktop/BulletTools.cs',import.meta.url),'utf8');assert.match(tools,/Field\(bag,"出生帧执行移动","高级设置",\(\)=>t.moveOnBirth,v=>t.moveOnBirth=v/);
 const editor=fs.readFileSync(new URL('../desktop/ExpressionEditor.cs',import.meta.url),'utf8');assert.match(editor,/at\(i,2,4,6,8\)/);
 const en=JSON.parse(fs.readFileSync(new URL('../assets/locales/en.json',import.meta.url),'utf8'));assert.equal(en['出生帧执行移动'],'Move on birth frame');assert.equal(en['按编号取列表值'],'Value by member index');
});
