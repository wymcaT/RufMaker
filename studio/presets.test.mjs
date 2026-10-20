import assert from 'node:assert/strict';
import {test} from 'node:test';
import {normalizeState,stateCode,valueAt} from './motion.mjs';
import {cameraUpdate} from './camera.mjs';

test('portable rainbow presets retain the carried sprite name through project synchronization',()=>{
  const state={version:2,tracks:[{kind:'emitter',rainbow:true,sprite:'rfp_sprrainbowcherry_12345678',rainbowSprite:'rfp_sprrainbowcherry_12345678',keyframes:[]}]};
  normalizeState(state);assert.equal(state.tracks[0].sprite,'rfp_sprrainbowcherry_12345678');assert.equal(state.tracks[0].color,'#FFFFFF');
  const legacy=normalizeState({version:2,tracks:[{rainbow:true,sprite:'sprCherry',keyframes:[]}]});assert.equal(legacy.tracks[0].sprite,'sprrainbowcherry');
});
test('camera animation retains motion paths and independent zoom keys in generated GML',()=>{
  const track={kind:'camera',enabled:true,start:100,end:300,motion:'parabola',pathStartX:400,pathStartY:304,pathPeakX:520,pathPeakY:100,pathEndX:640,pathEndY:304,keyframes:[{property:'pathProgress',frame:100,value:0,easing:'linear'},{property:'pathProgress',frame:300,value:1,easing:'linear'},{property:'cameraZoom',frame:100,value:1,easing:'sin-inout'},{property:'cameraZoom',frame:300,value:1.2}]};
  const state=normalizeState({version:2,tracks:[track]}),gml=cameraUpdate(state);
  assert(gml.includes('rf_x+=sqr(1-rf_u)*rf_pathStartX'));assert(gml.includes('rf_cam_zoom=max(.05,min(20,rf_cameraZoom))'));
  assert.equal(valueAt(track,'pathProgress',200),.5);assert(Math.abs(valueAt(track,'cameraZoom',200)-1.1)<1e-12);assert(!/NaN|undefined/.test(stateCode(track,'rf_frame')));
});
