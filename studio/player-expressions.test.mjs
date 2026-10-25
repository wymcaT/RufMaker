import test from 'node:test';
import assert from 'node:assert/strict';
import {evaluateExpression,isDynamic,expressionScripts,expressionCall,parameterCode,randomInitialization} from './random-parameters.mjs';
import {normalizeState,valueAt,stateCode} from './motion.mjs';
import {bulletFlightGml,flightSpawn} from './bullet-flight.mjs';

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
