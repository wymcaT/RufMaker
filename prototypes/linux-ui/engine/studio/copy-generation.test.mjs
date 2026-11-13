import test from 'node:test';import assert from 'node:assert/strict';
import {copyDefaults,copyPlacements,liveCopyPlacements,copyGridPoint} from './copy-generation.mjs';import {normalizeState,stateCode} from './motion.mjs';import {templateScripts} from './template-system.mjs';
const track=(id,kind='rod',extra={})=>({id,name:id,kind,start:0,end:80,keyframes:[],...extra});
const scene=tracks=>normalizeState({version:2,fps:50,duration:400,tracks});
test('direction, rotated pixel grid and circle placements have stable order and limits',()=>{
  assert.equal(copyPlacements({...copyDefaults,copyCount:2}).length,8);const grid=copyPlacements({...copyDefaults,copyMode:'grid',copyWidth:200,copyHeight:100,copySpacingX:100,copySpacingY:100,copyAngle:90});assert.equal(grid.length,6);assert.ok(Math.abs(grid[0].x+50)<1e-8&&Math.abs(grid[0].y-100)<1e-8);
  const circle=copyPlacements({...copyDefaults,copyMode:'circle',copyCount:4,copyRadius:100});assert.ok(Math.abs(circle[1].x)<1e-8&&Math.abs(circle[1].y+100)<1e-8);assert.equal(circle[3].angle,270);
  assert.throws(()=>copyPlacements({...copyDefaults,copyMode:'grid',copyWidth:1e9,copySpacingX:.01}));assert.throws(()=>copyPlacements({...copyDefaults,copyCount:76}));
});
test('copy generation emits template transforms, per-copy colors and a starting-angle helper with balanced GML',()=>{
  for(const mode of ['directions','grid','circle']){const a={type:'spawn',generationPreset:'copy-object',templateId:'copy',copyLinked:false,copyMode:mode,copyCount:4,copyFacing:'inward',copyColorMode:'rainbow'};const s=scene([track('source','rod',{spawnActions:[a],rainbow:true}),track('copy','rod',{templateOnly:true})]);const scripts=templateScripts(s),map=new Map(scripts);assert.ok(map.has('rf_copy_anchor_angle'));assert.match(map.get('rf_template_step'),/rf_copy_anchor_angle\(1\)/);assert.match(map.get('rf_template_step'),/c_white/);for(const [name,body] of scripts){let n=0;for(const c of body.replace(/'[^']*'|"[^"]*"/g,'')){if(c==='{')n++;if(c==='}')n--;assert.ok(n>=0,name);}assert.equal(n,0,name);}}
});

test('linked copies use the current source graph and tile every member even outside the room',()=>{
  for(const angle of [0,31,90]){const a={...copyDefaults,copyMode:'grid',copyWidth:0,copyHeight:0,copySpacingX:160,copySpacingY:152,copyAngle:angle};const cells=liveCopyPlacements(a,800,608),r=angle*Math.PI/180,c=Math.cos(r),s=Math.sin(r);
    for(const x of [-120,-1720,120]){const points=cells.map(p=>copyGridPoint(a,p,800,608,x,304));for(let iy=-25;iy<=25;iy++)for(let ix=-25;ix<=25;ix++){const px=x+ix*160*c+iy*152*s,py=304-ix*160*s+iy*152*c;if(px<0||px>800||py<0||py>608)continue;assert.ok(points.some(p=>Math.abs(px-p.x)<1e-6&&Math.abs(py-p.y)<1e-6),'Missing member in rotated grid');}}
  }
  const a={type:'spawn',generationPreset:'copy-object',templateId:'old-snapshot',copyMode:'grid'},s=scene([track('parent','group',{x:300,angle:40}),track('source','rod',{parentId:'parent',spawnActions:[a],x:-120}),track('old-snapshot','rod',{templateOnly:true})]);assert.equal(s.tracks[1].spawnActions[0].copyLinked,true);const scripts=templateScripts(s),map=new Map(scripts);assert.match(map.get('rf_template_step'),/rf_copy_link_spawn\(0,noone,rf_now\)/);assert.match(map.get('rf_copy_link_spawn'),/rf_new\.rf_template_source=1/);assert.match(map.get('rf_copy_link_update'),/rf_clock=argument0-rf_shift/);assert.match(map.get('rf_copy_point'),/floor\(rf_cp_u\/rf_copy_sx\)/);
  for(const [name,body] of scripts){let n=0;for(const c of body.replace(/'[^']*'|"[^"]*"/g,'')){if(c==='{')n++;if(c==='}')n--;assert.ok(n>=0,name);}assert.equal(n,0,name);}
});
test('organizing preserves execution order through normalization and repeat keys use balanced lookup',()=>{
  const a=track('first','emitter',{lane:8}),b=track('second','variable',{lane:0,variableName:'q',variableExpression:'2'});const s=normalizeState({version:2,fps:50,duration:400,preserveTrackOrder:true,tracks:[a,b]});assert.deepEqual(s.tracks.map(t=>t.id),['first','second']);
  const effect=track('effect','effect',{effect:'rgb',end:120,keyframeCycle:{length:20,keys:[{frame:0,property:'fxOpacity',value:0}]},keyframes:Array.from({length:121},(_,frame)=>({frame,property:'fxOpacity',value:(frame%20)/20}))});const code=stateCode(scene([effect]).tracks[0],'frame');assert.match(code,/if frame<60/);assert.doesNotMatch(code,/else if \(frame<2\)/);
});
