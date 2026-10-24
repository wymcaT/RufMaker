import assert from 'node:assert/strict';
import test from 'node:test';
import {normalizeState,ease} from './motion.mjs';
import {cameraUpdate} from './camera.mjs';
import {fxPassesGml,makeFxObjects,effectTypes} from './effects.mjs';
import {cameraPostGml,styleFxGml} from './style-effects.mjs';
import {nativeComposeGml} from './native-compositor.mjs';

test('new easing curves keep endpoints and match the approved demo equations',()=>{
  for(const name of ['edit-smooth','edit-snap','edit-back','edit-elastic']){assert.equal(ease(name,0),0);assert.equal(ease(name,1),1);}
  assert.equal(ease('edit-smooth',.5),.5);assert.equal(ease('edit-snap',.5),1-2**-5);assert.ok(ease('edit-back',.6)>1);assert.ok(ease('edit-elastic',.15)>1);
});
test('plain and imported games use the same post camera pass and compound effects',()=>{
  const state=normalizeState({version:2,fps:50,duration:200,tracks:[{id:'cam',kind:'camera',start:0,end:100,cameraMirror:true,cameraStyle:'变速推拉',cameraOriginX:400,cameraOriginY:304,cameraOriginZoom:1,cameraOriginAngle:0,cameraIntensity:.5,keyframes:[]},{id:'rgb',kind:'effect',effect:'rgb-impact',start:0,end:30,keyframes:[]},{id:'glitch',kind:'effect',effect:'glitch',start:40,end:70,keyframes:[]}]});
  const camera=cameraUpdate(state);assert.match(camera,/rf_cam_mirror=1/);assert.match(camera,/rf_cameraIntensity=max/);assert.match(camera,/rf_cam_post=rf_cam_mirror/);
  assert.ok(effectTypes.includes('rgb-impact')&&effectTypes.includes('glitch'));assert.equal((fxPassesGml(state).match(/rf_style_fx\(/g)||[]).length,2);
  assert.match(makeFxObjects(state)[1][2],/rf_camera_post\(rf_src,rf_dst\)/);assert.match(nativeComposeGml({...state,nativeProject:{imported:true}}),/rf_camera_post\(rf_src,rf_dst\)/);assert.match(cameraPostGml,/sx=-1/);assert.match(cameraPostGml,/rf_cam_blur/);assert.match(styleFxGml,/surface_copy_part/);assert.doesNotMatch(styleFxGml,/\brandom\(/);
});
