import test from 'node:test';
import assert from 'node:assert/strict';
import {evaluateExpression,isDynamic,expressionScripts,expressionCall,parameterCode,randomInitialization,playerAt,expressionContext} from './random-parameters.mjs';
import {normalizeState,valueAt,stateCode} from './motion.mjs';
import {bulletFlightGml,flightSpawn} from './bullet-flight.mjs';
import {bulletSystemScripts} from './bullet-system.mjs';

test('player expressions sample the enabled trace at the current song frame',()=>{
  const t={id:'trace-aim',kind:'emitter',start:0,end:50,x:200,y:180,expressions:{angle:'point_direction(x,y,player.x,player.y)'},keyframes:[]};
  const state=normalizeState({version:2,fps:50,duration:100,nativeTest:{x:400,y:304},usePlayerTrace:true,playerTrace:[{frame:0,x:600,y:100},{frame:10,x:100,y:500},{frame:20,x:700,y:300}],tracks:[t]});
  for(const frame of [0,3,5,10,15,20]){const player=playerAt(state,frame);assert.equal(expressionContext(t,frame)['player.x'],player.x);assert.equal(expressionContext(t,frame)['player.y'],player.y);assert.ok(Math.abs(valueAt(t,'angle',frame)-evaluateExpression('point_direction(200,180,player.x,player.y)',1,'expected',{'player.x':player.x,'player.y':player.y}))<1e-9);}
  assert.deepEqual(playerAt(state,5),{x:350,y:300});assert.deepEqual(playerAt(state,25),state.nativeTest);state.usePlayerTrace=false;assert.deepEqual(playerAt(state,10),state.nativeTest);
});

test('aimed emission samples live player again at the actual birth of a delayed bullet',()=>{
  const state=normalizeState({version:2,fps:50,duration:100,tracks:[{id:'delayed-aim',kind:'emitter',start:0,end:20,advancedBullets:true,indexDelay:3,emissionFacing:'aim',keyframes:[]}]});
  const scripts=Object.fromEntries(bulletSystemScripts(state));assert.match(scripts.rf_pattern_spawn,/rf_result\.rf_birth_aim=1/);assert.match(scripts.rf_pattern_tick,/if rf_age=0 && rf_birth_aim/);assert.match(scripts.rf_pattern_tick,/rf_dir=point_direction\(x,y,rf_expression_player\(0\),rf_expression_player\(1\)\)\+rf_aim_offset/);
  const lookup=Object.fromEntries(expressionScripts(state,['player'])).rf_expression_player;assert.match(lookup,/return rf_p\.x;return rf_p\.y/);
});

test('single age aim applies the target offset at its trigger even with a nonzero duration',()=>{
  const state=normalizeState({version:2,fps:50,duration:100,tracks:[{id:'age-aim',kind:'emitter',start:0,end:0,advancedBullets:true,keyframes:[],bulletActions:[{type:'aim',start:10,duration:30,from:0,to:3}]}]});
  const code=Object.fromEntries(bulletSystemScripts(state)).rf_pattern_tick;assert.match(code,/rf_value=3;if rf_at=0 \{rf_dir=point_direction\(x,y,rf_expression_player\(0\),rf_expression_player\(1\)\)\+rf_value;/);
});

test('player coordinates, geometry, degree directions and bounded turning',()=>{
  const context={'player.x':400,'player.y':304,x:400,y:404,frame:50,fps:50};
  for(const [expression,expected] of [['player.x',400],['player.y',304],['point_direction(x,y,player.x,player.y)',90],['point_distance(x,y,player.x,player.y)',100],['point_direction(0,0,0,-1)',90],['point_direction(0,0,-1,0)',180],['point_direction(0,0,0,1)',270],['lengthdir_y(100,90)',-100],['turn_towards(359,1,3)',361],['turn_towards(1,359,1)',0],['sin(time*pi/2)*10',10],['clamp(lerp(0,100,.3),0,20)',20]])assert.ok(Math.abs(evaluateExpression(expression,42,'test',context)-expected)<1e-9,expression);
  for(const text of ['player.z','point_direction(1,2,3)','eval(1)','random(1);game_end()'])assert.throws(()=>evaluateExpression(text,1,'x'));
});
test('live expressions survive repeated saves and seek deterministically',()=>{
  const t={id:'live',kind:'emitter',start:0,end:100,x:100,y:200,motion:'velocity',velocityX:2,expressions:{x:'x+10',angle:'point_direction(x,y,player.x,player.y)',speed:'random_range(1,5)+0*frame'},keyframes:[]};
  const state={version:2,fps:50,duration:100,randomSeed:42,nativeTest:{x:400,y:304},tracks:[t]};normalizeState(state);
  const angle=valueAt(t,'angle',10),speed=valueAt(t,'speed',10);assert.equal(t.x,100);assert.ok(Math.abs(angle-evaluateExpression('point_direction(130,200,400,304)',42,'a'))<1e-9);
  for(let i=0;i<4;i++){normalizeState(state);assert.equal(t.x,100);assert.equal(valueAt(t,'angle',10),angle);assert.equal(valueAt(t,'speed',10),speed);}
  assert.notEqual(valueAt(t,'speed',11),speed);state.nativeTest.x=100;normalizeState(state);assert.notEqual(valueAt(t,'angle',10),angle);
  const code=stateCode(t,'rf_sample');assert.ok(code.indexOf('rf_x+=')<code.indexOf('rf_angle='));assert.ok(code.indexOf('rf_x1=')<code.indexOf('rf_x='));assert.match(code,/rf_expr_[a-f0-9]+\(rf_x,rf_y,rf_sample/);assert.doesNotMatch(randomInitialization(state),/rf_expr_/);
});
test('animated endpoints are available to direction formulas',()=>{
  const t={id:'rod',kind:'rod',start:0,end:100,expressions:{spriteAngle:'point_direction(x1,y1,x2,y2)',x1:'player.x-100',y1:'player.y',x2:'player.x+100',y2:'player.y+100'},keyframes:[]};normalizeState({version:2,fps:50,duration:100,tracks:[t]});assert.ok(Math.abs(valueAt(t,'spriteAngle',10)-333.434948822922)<1e-9);const code=stateCode(t,'rf_frame');assert.ok(code.indexOf('rf_x1=')<code.indexOf('rf_spriteAngle='));
});
test('generated runtime scripts use safe player lookup and bullet history',()=>{
  const t={id:'homing',kind:'emitter',start:0,end:100,flightMode:'expression',flightExpression:'turn_towards(direction,point_direction(x,y,player.x,player.y),3)',keyframes:[],expressions:{angle:'point_direction(x,y,player.x,player.y)'}};
  const state=normalizeState({version:2,duration:100,fps:50,tracks:[t]});const scripts=expressionScripts(state,['objPlayer']);assert.equal(scripts.length,3);assert.match(scripts.at(-1)[1],/instance_exists\(objPlayer\)/);assert.match(scripts.at(-1)[1],/return 400;return 304/);
  assert.match(bulletFlightGml(state),/rf_expr_.*\(x,y,\(rf_birth\+rf_age\),rf_age,rf_dir,rf_speed/);assert.match(bulletFlightGml(state),/rf_hx\[rf_slot\]=x/);assert.match(flightSpawn(t,0,true),/repeat\(other.rf_frame-rf_birth\)/);assert.ok(isDynamic('player.x'));assert.ok(!isDynamic('choose(1,2)'));assert.match(parameterCode(t,'angle'),/rf_expr_/);
});

test('source center variables compile without exceeding the GM8.2 argument limit',()=>{
  const expression='point_distance(x,y,source.x,source.y)*0.02+0*time+0*fps',context={x:130,y:240,'source.x':100,'source.y':200,frame:50,fps:50};
  assert.equal(evaluateExpression(expression,42,'source',context),1);assert.ok(isDynamic('source.x'));assert.throws(()=>evaluateExpression('source.z',1,'x'));
  const t={id:'source',kind:'emitter',start:0,end:0,expressions:{speed:expression},keyframes:[]},s=normalizeState({version:2,fps:50,tracks:[t]});
  const body=expressionScripts(s,[])[0][1];assert.match(body,/argument11/);assert.match(body,/argument15/);assert.match(body,/argument2\/50/);assert.doesNotMatch(body,/argument16|argument17/);
  const call=expressionCall(expression,t,'speed',null,{x:'x',y:'y',sourceX:'rf_source_x',sourceY:'rf_source_y'});assert.match(call,/rf_source_x,0,1,0,rf_source_y\)/);assert.match(call,/rf_random_seed.*104729/);
});
