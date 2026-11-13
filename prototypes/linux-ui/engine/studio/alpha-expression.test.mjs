import test from 'node:test';
import assert from 'node:assert/strict';
import {evaluateExpression as evaluate,parseExpression,usesAlpha,isDynamic,expressionContext,expressionCall,expressionScripts,expressionScriptName} from './random-parameters.mjs';

const value=(expression,context={})=>evaluate(expression,12345,'object|x|base',context);
test('comparisons return numeric booleans after arithmetic, including nested expressions',()=>{
  for(const [expression,expected] of [['2+3*4>13',1],['2+3*4<14',0],['2+3*4<=14',1],['14>=2+3*4',1],['14==2+3*4',1],['14!=2+3*4',0],['(1<2)+2*(3>=4)',1],['max(1>2,3<=3)',1],['3>2>0',1],['-2<-1',1]])assert.equal(value(expression),expected,expression);
  for(const expression of ['1=1','1===1','1!==1','1<>2','1&&2','!1','1<','<1','1< =2','1;2','alpha=0'])assert.throws(()=>parseExpression(expression),undefined,expression);
});
test('alpha is available only from an explicitly supplied finite instance value',()=>{
  assert.equal(value('alpha<=0',{alpha:0}),1);assert.equal(value('alpha<=0',{alpha:.5}),0);assert.equal(value('alpha+.1',{alpha:.5}),.6);
  for(const context of [{},{alpha:undefined},{alpha:null},{alpha:NaN},{alpha:Infinity},Object.create({alpha:0})])assert.throws(()=>value('alpha',context),/alpha/);
  assert.throws(()=>value('alpha',expressionContext({start:0},0)),/alpha/);
  assert.equal(usesAlpha('max(alpha,0)'),true);assert.equal(usesAlpha('global.alpha'),false);assert.equal(usesAlpha('1<2'),false);assert.equal(isDynamic('alpha'),true);
});
test('alpha code uses instance logic alpha and preserves the 16 argument ABI',()=>{
  const track={id:'test',x:0,y:0,start:0,spawnActions:[],bulletActions:[{type:'destroy',condition:'alpha<=0'}]};
  assert.throws(()=>expressionCall('alpha<=0',track),/alpha/);assert.throws(()=>expressionCall('alpha<=0',track,'condition',null,{instanceAlpha:1}),/alpha/);
  const call=expressionCall('alpha<=0',track,'condition',null,{instanceAlpha:true,frame:'0',age:'0'});
  assert.equal(call.slice(call.indexOf('(')+1,-1).split(',').length,16);
  const scripts=new Map(expressionScripts({fps:50,tracks:[track]},[])),code=scripts.get(expressionScriptName('alpha<=0'));
  assert.ok(code);assert.match(code,/=rf_logic_alpha;/);assert.match(code,/rf_random_arg\d+<=rf_random_arg\d+/);assert.doesNotMatch(code,/argument16|global\.rf_logic_alpha/);
});
test('legacy random streams and eager draw order remain unchanged',()=>{
  assert.equal(value('random(100)'),2.424714716546903);
  assert.equal(value('choose(random(100),random_range(200,300),-2)'),2.424714716546903);
  assert.equal(value('random(10)+random(20)*random(30)'),19.482822761351525);
  assert.equal(value('(random(10)<0)+random(20)*random(30)'),19.240351289696835);
});

test('raw alpha preserves initial, static, random, dynamic, keyframed and birth expressions',async()=>{
  const {normalizeState,valueAt}=await import('./motion.mjs');
  const {parameterCode,randomInitialization,clampParameter}=await import('./random-parameters.mjs');
  const make=(rawAlpha,extra={})=>normalizeState({version:2,fps:50,tracks:[{id:'raw',kind:'emitter',start:0,end:100,rawAlpha,alpha:1.04,keyframes:[],...extra}]});
  assert.equal(valueAt(make(true).tracks[0],'alpha',0),1.04);
  for(const rawAlpha of [false,true]){
    const expected=rawAlpha?1.04:1;
    for(const expression of ['1.04','random_range(1.04,1.04)','1.04+frame*.01']){
      const state=make(rawAlpha,{expressions:{alpha:expression}}),t=state.tracks[0];
      assert.equal(valueAt(t,'alpha',0),expected);
      assert.equal(valueAt(t,'alpha',2),rawAlpha&&expression.includes('frame')?1.06:expected);
      const birth=parameterCode(t,'alpha',1,null,{birthSampling:true});assert.equal(birth.startsWith('max(0,min(1,'),!rawAlpha);
      if(!expression.includes('frame'))assert.equal(randomInitialization(state).includes('=max(0,min(1,'),!rawAlpha);
    }
    for(const expression of ['random_range(1.04,1.04)','1.04+frame*.01']){
      const t=make(rawAlpha,{keyframes:[{property:'alpha',frame:0,expression,randomId:'alpha0'}]}).tracks[0];
      assert.equal(t.keyframes[0].value,expected);assert.equal(valueAt(t,'alpha',2),rawAlpha&&expression.includes('frame')?1.06:expected);
      assert.equal(parameterCode(t,'alpha',1,t.keyframes[0],{birthSampling:true}).startsWith('max(0,min(1,'),!rawAlpha);
    }
    assert.equal(clampParameter('alpha',-.04,rawAlpha),rawAlpha?-.04:0);
    assert.equal(clampParameter('fxOpacity',1.04,rawAlpha),1);
  }
  assert.throws(()=>make(true,{expressions:{alpha:'alpha+.1'}}),/alpha/);
});
