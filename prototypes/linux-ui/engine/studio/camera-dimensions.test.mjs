import assert from 'node:assert/strict';
import fs from 'node:fs';
import test from 'node:test';
import {normalizeState,stateCode,valueAt,fields} from './motion.mjs';
import {cameraApplyGml,cameraUpdate} from './camera.mjs';
import {cameraPostGml} from './style-effects.mjs';
import {lifetimeScripts} from './bullet-lifetime.mjs';
import {validateMaskState} from './sprite-mask-gml.mjs';
import {randomInitialization} from './random-parameters.mjs';
const scene=t=>normalizeState({version:2,fps:50,duration:50,tracks:[{id:'camera',kind:'camera',start:0,end:20,keyframes:[],...t}]});
// Execute the scalar width/height selection emitted by cameraUpdate. This is
// generated arithmetic, not execution of the native renderer/projection API.
function generatedSize(t,frame){
 const code=cameraUpdate({tracks:[t]});
 const selection=code.slice(code.indexOf('rf_cam_width=room_width/rf_cam_zoom;'),code.indexOf('rf_cam_angle=rf_angle;'));
 assert.ok(selection.length>0);
 const js=selection.replace(/if (\w+>0) (\w+=\w+;)/g,'if ($1) {$2}');
 return Function('room_width','room_height','rf_cam_zoom','rf_cameraViewWidth','rf_cameraViewHeight','let rf_cam_width,rf_cam_height;'+js+'return [rf_cam_width,rf_cam_height];')(800,600,Math.max(.05,Math.min(20,valueAt(t,'cameraZoom',frame))),valueAt(t,'cameraViewWidth',frame),valueAt(t,'cameraViewHeight',frame));
}
test('camera dimensions survive defaults, keys and dynamic/static expressions',()=>{
 const legacy=scene({}).tracks[0];assert.equal(legacy.cameraViewWidth,0);assert.equal(legacy.cameraViewHeight,0);
 for(const p of ['cameraViewWidth','cameraViewHeight'])assert.ok(fields.includes(p));
 const t=scene({cameraZoom:2,cameraViewWidth:640}).tracks[0];assert.deepEqual(generatedSize(t,0),[640,300]);
 const keyed=scene({cameraZoom:2,keyframes:[{property:'cameraViewWidth',frame:0,value:0,easing:'linear'},{property:'cameraViewWidth',frame:10,value:640}]}).tracks[0];
 assert.deepEqual(generatedSize(keyed,0),[400,300]);assert.deepEqual(generatedSize(keyed,5),[320,300]);assert.deepEqual(generatedSize(keyed,10),[640,300]);
 const dynamic=scene({expressions:{cameraViewWidth:'640+frame',cameraViewHeight:'250+frame'}});
 assert.equal(valueAt(dynamic.tracks[0],'cameraViewWidth',5),645);assert.match(stateCode(dynamic.tracks[0],'rf_frame'),/rf_cameraViewWidth=rf_expr_/);
 const fixed=scene({expressions:{cameraViewWidth:'640+10',cameraViewHeight:'250+10'}});assert.equal(valueAt(fixed.tracks[0],'cameraViewWidth',0),650);assert.match(randomInitialization(fixed),/rf_random_arg0=640/);assert.match(stateCode(fixed.tracks[0],'rf_frame'),/rf_cameraViewWidth=objRufController.rf_random_/);
});
test('default and nonpositive dimensions preserve legacy zoom independently',()=>{
 for(const zoom of [.05,.5,1,2,20])for(const width of [0,-100])assert.deepEqual(generatedSize(scene({cameraZoom:zoom,cameraViewWidth:width}).tracks[0],0),[800/zoom,600/zoom]);
});
test('direct/post projection, mirrored tile ranges, blur and view boundaries use effective dimensions',()=>{
 assert.match(cameraApplyGml,/w=objRufController.rf_cam_width;h=objRufController.rf_cam_height/);
 assert.match(cameraApplyGml,/d3d_set_projection_ortho\(cx-w\/2,cy-h\/2,w,h,a\)/);
 assert.match(cameraPostGml,/hx=\(abs\(c\)\*rf_cam_width\+abs\(s\)\*rf_cam_height\)\/2/);
 assert.match(cameraPostGml,/d3d_set_projection_ortho\(rf_cam_x-rf_cam_width\/2,rf_cam_y-rf_cam_height\/2,rf_cam_width,rf_cam_height,rf_cam_angle\)/);
 assert.match(cameraPostGml,/draw_surface_ext\(src,px,py,sx,sy,0,c_white,1\)/);
 assert.ok(cameraPostGml.indexOf('rf_fx_opaque();surface_reset_target();')<cameraPostGml.indexOf('if rf_cam_blur>0'));
 assert.match(new Map(lifetimeScripts).get('rf_lifetime_inside'),/rf_hw=objRufController.rf_cam_width\/2;rf_hh=objRufController.rf_cam_height\/2/);
 const mask=fs.readFileSync(new URL('./sprite-mask-gml.mjs',import.meta.url),'utf8');assert.match(mask,/rf_hw=objRufController.rf_cam_width\/2;rf_hh=objRufController.rf_cam_height\/2/);
 assert.doesNotThrow(()=>validateMaskState({tracks:[{kind:'emitter',despawnBounds:'sprite-mask',despawnSpace:'view'},{kind:'camera',cameraViewWidth:640}]}));
 assert.doesNotThrow(()=>validateMaskState({tracks:[{kind:'emitter',despawnBounds:'sprite-mask',spriteFrame:2}]}));
});
test('actual C# Camera.At/WorldToView versus generated dimensions and projection arithmetic', {skip:!process.env.CAMERA_PARITY_ORACLE},()=>{
 const cases=JSON.parse(fs.readFileSync(process.env.CAMERA_PARITY_ORACLE,'utf8'));assert.equal(cases.length,54);
 const mask=fs.readFileSync(new URL('./sprite-mask-gml.mjs',import.meta.url),'utf8');
 const formulas=['rf_wx','rf_wy','rf_dx','rf_dy','rf_px','rf_py'].map(k=>`${k}=${mask.match(new RegExp(k+'=([^;]+);'))[1]};`).join('');
 const point=Function('x','y','rf_lx','rf_ly','rf_c','rf_s','rf_cx','rf_cy','rf_vc','rf_vs',`let rf_wx,rf_wy,rf_dx,rf_dy,rf_px,rf_py;${formulas}return [rf_px,rf_py];`);
 const outside=Function('rf_maxx','rf_minx','rf_maxy','rf_miny','rf_mask_view','rf_hw','rf_hh','rf_die_margin','return '+mask.match(/if (rf_maxx<[^\n]+?) \{if !rf_lifetime_request/)[1]);
 for(const v of cases){
 const t=scene({x:430,y:270,angle:v.angle,cameraZoom:2,cameraViewHeight:v.height,keyframes:[{property:'cameraViewWidth',frame:0,value:v.width,easing:'linear'},{property:'cameraViewWidth',frame:10,value:v.width+100,easing:'linear'}]}).tracks[0];
 const [w,h]=generatedSize(t,v.frame);assert.equal(w,v.effectiveWidth);assert.equal(h,v.effectiveHeight);
 const a=v.angle*Math.PI/180,c=Math.cos(a),s=Math.sin(a),px=400+800/w*(c*100-s*50),py=300+600/h*(s*100+c*50);
 assert.ok(Math.abs(px-v.projectX)<.001);assert.ok(Math.abs(py-v.projectY)<.001);
 const hx=(Math.abs(c)*w+Math.abs(s)*h)/2,hy=(Math.abs(s)*w+Math.abs(c)*h)/2;
 for(const [actual,expected] of [[430-hx,v.minx],[430+hx,v.maxx],[270-hy,v.miny],[270+hy,v.maxy]])assert.ok(Math.abs(actual-expected)<.001);
 const points=[];for(const x of [-10,20])for(const y of [-5,15])points.push(point(530,320,(x-2)*2,(y-3)*-.5,Math.cos(27*Math.PI/180),Math.sin(27*Math.PI/180),430,270,c,s));
 const box={left:Math.min(...points.map(p=>p[0])),right:Math.max(...points.map(p=>p[0])),top:Math.min(...points.map(p=>p[1])),bottom:Math.max(...points.map(p=>p[1]))};
 for(const k of Object.keys(box))assert.ok(Math.abs(box[k]-v.box[k])<1e-10);
 assert.equal(outside(box.right,box.left,box.bottom,box.top,1,w/2,h/2,5),v.maskOutside);
 }
});
