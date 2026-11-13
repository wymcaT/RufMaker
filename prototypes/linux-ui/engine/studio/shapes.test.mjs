import test from 'node:test';
import assert from 'node:assert/strict';
import {formationPoint,formationPerimeter,formationScripts,shapeSetup,shapePoint} from './shapes.mjs';
import {normalizeState,valueAt} from './motion.mjs';
import {clampParameter,randomInitialization,parameterCode} from './random-parameters.mjs';
import {normalizeRainbow,rodSequence} from './rainbow.mjs';
const near=(a,b)=>assert.ok(Math.abs(a-b)<1e-9,`${a} != ${b}`);
test('star2 follows original k to k+2 chords, including crossed even-sided figures',()=>{
  for(const n of [3,4,5,6,7])for(let k=0;k<n;k++)for(const f of [0,.2,.7]){
    const a=k*2*Math.PI/n,b=(k+2)*2*Math.PI/n,p=formationPoint('star2',(k+f)/n,120,0,n);
    near(p.x,120*(Math.cos(a)*(1-f)+Math.cos(b)*f));near(p.y,-120*(Math.sin(a)*(1-f)+Math.sin(b)*f));
  }
});
test('heart retains the supplied Fourier outline and source numbering',()=>{
  near(formationPoint('heart',0,16).x,0);near(formationPoint('heart',0,16).y,-5);
  near(formationPoint('heart',.25,16).x,16);near(formationPoint('heart',.25,16).y,-4);
  near(formationPoint('heart',.5,16).x,0);near(formationPoint('heart',.5,16).y,17);
  assert.match(shapePoint({shape:'heart'}),/rf_j\+1/);assert.match(shapePoint({shape:'star2'}),/rf_j\+0/);
});
test('shape dimensions survive old-project normalization and support independent keys/expressions',()=>{
  const state=normalizeState({version:2,fps:50,tracks:[{id:'heart',kind:'rod',shape:'heart',start:0,end:100,keyframes:[],expressions:{shapeInner:'random_range(.2,.4)'}}]});
  const t=state.tracks[0];assert.equal(t.shapeAspect,1);assert.ok(t.shapeInner>=.2&&t.shapeInner<=.4);assert.equal(t.shape,'heart');
  for(const shape of ['heart','star2','star','ellipse','flower','infinity'])assert.ok(formationPerimeter(shape,120,5,1,.45)>0);
  assert.match(shapeSetup({shape:'star2',rolling:true,start:0,_parentIndex:0}),/rf_angle-global.rf_ga/);
  assert.match(shapeSetup({shape:'star2',rolling:true,start:0,_parentIndex:0}),/rf_rollSpeed\*global.rf_gscale/);
  assert.equal(formationScripts.length,2);
});

test('signed height ratio mirrors the outline and interpolates continuously through zero',()=>{
  for(const shape of ['ellipse','heart','star2','star','flower','infinity'])for(const aspect of [.02,.65,1.3])for(const u of [0,.13,.25,.7]){
    const a=formationPoint(shape,u,120,0,5,aspect),b=formationPoint(shape,u,120,0,5,-aspect),zero=formationPoint(shape,u,120,0,5,0);
    near(a.x,b.x);near(a.y,-b.y);near(zero.y,0);near(formationPerimeter(shape,120,5,aspect),formationPerimeter(shape,120,5,-aspect));
  }
  for(const value of [-10,-.02,0,.02,10])assert.equal(clampParameter('shapeAspect',value),value);
  assert.equal(clampParameter('shapeAspect',-12),-10);assert.equal(clampParameter('shapeAspect',12),10);
  const state=normalizeState({version:2,fps:50,duration:100,tracks:[{id:'flip',kind:'rod',shape:'ellipse',start:0,end:100,easing:'linear',expressions:{shapeAspect:'choose(-.65,-.65)'},keyframes:[{frame:0,property:'shapeAspect',value:1},{frame:100,property:'shapeAspect',value:-1}]}]});
  const t=state.tracks[0];assert.equal(t.shapeAspect,-.65);near(valueAt(t,'shapeAspect',25),.5);near(valueAt(t,'shapeAspect',50),0);near(valueAt(t,'shapeAspect',100),-1);
  assert.match(randomInitialization(state),/max\(-10,min\(10,/);t.expressions.shapeAspect='1-frame/50';assert.match(parameterCode(t,'shapeAspect',1),/max\(-10,min\(10,/);
});

test('spatial rainbow modes preserve old defaults and use actual positions in generated code',()=>{
  const old={shape:'rod',rodMode:'center'};normalizeRainbow(old);assert.equal(old.rainbowMode,'sequence');assert.equal(rodSequence(old),'abs(rf_fraction-.5)*(rf_count-1)');
  const distance={rainbowMode:'distance'},angle={rainbowMode:'angle'};normalizeRainbow(distance);normalizeRainbow(angle);assert.equal(distance.rainbowMode,'distance');assert.equal(angle.rainbowMode,'angle');
  assert.match(rodSequence(distance),/round\(point_distance\(rf_x,rf_y,rf_px,rf_py\)/);assert.match(rodSequence(angle,{rotation:'rf_rotation'}),/point_direction.*rf_rotation.*32\/360/);
  const unknown={rainbowMode:'future-mode'};normalizeRainbow(unknown);assert.equal(unknown.rainbowMode,'sequence');
});
