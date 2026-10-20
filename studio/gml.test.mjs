import assert from 'node:assert/strict';
import {test} from 'node:test';
import {gmlNumber} from './gml.mjs';
import {stateCode,normalizeState} from './motion.mjs';
import {trailCall} from './trails.mjs';
import {rainbowCode} from './rainbow.mjs';

test('GM8 numeric literals expand scientific notation without losing the value',()=>{
  for(const n of [-1.617293538674902e-15,Math.sin(Math.PI*2),1e-7,-1e-7,1e21,-1e21,1.2345e30,Number.MIN_VALUE,Number.MAX_VALUE,0,-0,1.23456789]){
    const literal=gmlNumber(n);assert(!/[eE]/.test(literal),literal);assert.equal(Number(literal),n===0?0:n);
  }
  assert.equal(gmlNumber(undefined,400),'400');assert.equal(gmlNumber(Infinity,3),'3');
});
test('reported camera keyframe and tiny motion/trail values generate ordinary GM8 decimals',()=>{
  const t=normalizeState({version:2,tracks:[{kind:'camera',motion:'velocity',velocityX:1e-8,angularSpeed:-1e-15,start:1997,end:2002,keyframes:[{property:'angle',frame:1997,value:.255345115711229736,easing:'sin-inout'},{property:'angle',frame:2002,value:-1.617293538674902e-15}]}]}).tracks[0];
  const code=stateCode(t,'rf_frame');assert(!/\d[eE][+-]?\d/.test(code));assert(code.includes('-0.000000000000001617293538674902'));
  assert(!/\d[eE][+-]?\d/.test(trailCall({...t,sprite:'sprCherry',trailAlphaDecay:1e-10},{color:'c_white',age:'age',seed:'0'})));
  assert(!/\d[eE][+-]?\d/.test(rainbowCode({...t,rainbow:true,rainbowSpeed:1e-10},'0','frame',{sampled:false})));
});
