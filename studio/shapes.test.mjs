import test from 'node:test';
import assert from 'node:assert/strict';
import {formationPoint,formationPerimeter,formationScripts,shapeSetup,shapePoint} from './shapes.mjs';
import {normalizeState} from './motion.mjs';
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
