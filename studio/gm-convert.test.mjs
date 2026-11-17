import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import {convertGmObject} from './gm-convert.mjs';
import {editGmAsset} from './gm-resources.mjs';
import {normalizeState} from './motion.mjs';

const marker=`/*"/*'/**//* YYD ACTION\nlib_id=1\naction_id=603\napplies_to=self\n*/\n`;
function fixture(code){
  const base=path.resolve('build/object-dynamics-check');fs.mkdirSync(base,{recursive:true});const dir=fs.mkdtempSync(path.join(base,'convert-'));
  fs.mkdirSync(path.join(dir,'sprites'));fs.writeFileSync(path.join(dir,'sprites/index.yyd'),'sprCherry\n');
  editGmAsset(dir,{kind:'objects',name:'objSimple',operation:'create'});
  fs.writeFileSync(path.join(dir,'objects/objSimple.txt'),'sprite=sprCherry\nvisible=1\nsolid=0\npersistent=0\ndepth=-100\nparent=\nmask=\n');
  fs.writeFileSync(path.join(dir,'objects/objSimple.gml'),'#define Create_0\n'+marker+code+'\n');
  return dir;
}
test('converts initialized motion and animation without writing the native resource',()=>{
  const dir=fixture('speed=4;direction=45;gravity=.4;gravity_direction=270;friction=.05;image_speed=.25;image_index=2;image_blend=c_red;');
  const before=fs.readFileSync(path.join(dir,'objects/objSimple.gml'));
  const result=convertGmObject(dir,{name:'objSimple',start:20,end:149,x:120,y:240});
  assert.equal(result.supported,true);assert.equal(result.track.kind,'emitter');assert.equal(result.track.interval,130);
  assert.equal(result.track.angle,45);assert.equal(result.track.bulletGravity,.4);assert.equal(result.track.bulletFriction,.05);assert.equal(result.track.imageSpeed,.25);assert.equal(result.track.color,'#FF0000');assert.equal(result.track.depth,-100);
  assert.deepEqual(fs.readFileSync(path.join(dir,'objects/objSimple.gml')),before);
});
test('keeps seeded random expressions and applies native vector assignments in order',()=>{
  const dir=fixture('speed=choose(3,5,7);direction=random(360);');
  const result=convertGmObject(dir,{name:'objSimple'});assert.equal(result.supported,true);
  assert.equal(result.track.expressions.speed,'choose(3,5,7)');assert.equal(result.track.expressions.angle,'random(360)');
  fs.writeFileSync(path.join(dir,'objects/objSimple.gml'),'#define Create_0\n'+marker+'hspeed=3;vspeed=4;speed=10;hspeed+=1;');
  const vector=convertGmObject(dir,{name:'objSimple'});assert.equal(vector.supported,true);assert.ok(Math.abs(vector.track.speed-Math.hypot(7,8))<.000001);
});
test('reports unsupported events and controller behavior instead of silently dropping GML',()=>{
  const dir=fixture('if player.x>400 {speed=4;}');
  const source=path.join(dir,'objects/objSimple.gml');fs.appendFileSync(source,'\n#define Draw_0\n'+marker+'draw_self();\n');
  const before=fs.readFileSync(source);const result=convertGmObject(dir,{name:'objSimple'});
  assert.equal(result.supported,false);assert.ok(result.issues.some(s=>s.includes('Create_0')));assert.ok(result.issues.some(s=>s.includes('Draw_0')));assert.equal(result.track,undefined);assert.deepEqual(fs.readFileSync(source),before);
});
test('accepts semicolon-free initialization and does not replace an empty native Draw override',()=>{
  const dir=fixture('speed=4\ndirection=45\nimage_speed=.2\n');
  assert.equal(convertGmObject(dir,{name:'objSimple'}).supported,true);
  fs.appendFileSync(path.join(dir,'objects/objSimple.gml'),'\n#define Draw_0\n\n');
  const result=convertGmObject(dir,{name:'objSimple'});assert.equal(result.supported,false);assert.ok(result.issues.some(s=>s.includes('Draw_0')));
});
test('keeps image_index on action 541 subimage -1 and marks physics bit for random physics expressions',()=>{
  const dir=fixture('image_index=3;gravity=random(0);');
  const spriteAction=`/*"/*'/**//* YYD ACTION\nlib_id=1\naction_id=541\napplies_to=self\narg0=sprCherry\narg1=-1\narg2=0.25\n*/\n`;
  fs.writeFileSync(path.join(dir,'objects/objSimple.gml'),'#define Create_0\n'+marker+'image_index=3;gravity=random(0);\n'+spriteAction);
  const result=convertGmObject(dir,{name:'objSimple'});
  assert.equal(result.supported,true);assert.equal(result.track.imageIndex,3);assert.equal(result.track.imageSpeed,0.25);
  assert.equal(result.track.bulletGravity,0);assert.equal(result.track.expressions.bulletGravity,'random(0)');
  assert.equal(result.track.objectProperties,(1<<5)|(1<<6));
  const normalized=normalizeState({version:2,fps:50,duration:100,tracks:[result.track]});
  assert.equal(normalized.tracks[0].objectProperties,(1<<5)|(1<<6));
});
