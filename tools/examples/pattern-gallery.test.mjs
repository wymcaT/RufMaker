// Run desktop/Test-TemplateSystem.ps1 first. Samples the actual C# preview simulation.
import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {execFileSync} from 'node:child_process';
import {patternGalleryTracks,galleryChapters} from './create-pattern-gallery.mjs';
import {normalizeState} from '../../studio/motion.mjs';

const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..'),folder=path.join(root,'build/gallery-check');
fs.mkdirSync(folder,{recursive:true});
const state=normalizeState({version:2,fps:50,duration:4400,randomSeed:24681357,randomizeOnPlay:false,nativeTest:{x:400,y:536},tracks:patternGalleryTracks()});
const scene=path.join(folder,'scene.json'),output=path.join(folder,'samples.json');fs.writeFileSync(scene,JSON.stringify(state,null,2));
const frames=[0,50,100,180,250,399,400,458,470,528,598,799,800,806,812,900,980,1199,...galleryChapters.flatMap(c=>[c.preview,c.end])];
execFileSync(path.join(root,'build/template-system-check/TemplateSystem.tests.exe'),['--samples',scene,[...new Set(frames)].sort((a,b)=>a-b).join(','),output],{windowsHide:true});
const samples=JSON.parse(fs.readFileSync(output,'utf8')),at=(frame,source)=>samples[frame].filter(b=>b.source===source);

test('each chapter produces visible bullets and finishes before the next chapter',()=>{
  for(const c of galleryChapters){assert.ok(samples[c.preview].some(b=>b.alpha>0&&b.scale>0),c.name+' has no visible preview');assert.equal(samples[c.end].length,0,c.name+' leaks into the next chapter');}
  assert.equal(new Set(state.tracks.map(t=>t.id)).size,state.tracks.length);
  for(const t of state.tracks)for(const a of [...t.spawnActions,...t.bulletActions].filter(a=>a.type==='spawn'))assert.ok(state.tracks.some(target=>target.id===a.templateId&&target.templateOnly));
});
test('periodic stars generate multiple complete copies with independently captured centers',()=>{
  const first=at(100,'echo-shot').filter(b=>b.birth===50),second=at(100,'echo-shot').filter(b=>b.birth===100);
  assert.equal(first.length,60);assert.equal(second.length,60);assert.ok(first[0].sourceX!==second[0].sourceX);
  for(const b of second)assert.ok(Math.abs(b.speed-Math.hypot(b.x-b.sourceX,b.y-b.sourceY)*.018)<.00001);
  assert.equal(at(250,'echo-shot').filter(b=>b.birth===250).length,60);
});
test('star members repeat three complete rounds, all shots retain equal speed and incremental directions',()=>{
  for(const [frame,base] of [[458,400],[528,470],[598,540]]){
    const round=at(frame,'uniform-shot').filter(b=>b.birth>=base&&b.birth<=base+58);assert.equal(round.length,30);
    for(const b of round){const member=(b.birth-base)/2;assert.equal(b.speed,3);assert.ok(Math.abs(b.direction-(-90+member*12))<.00001);}
  }
});
test('moving mother leaves independent stationary bullets at six-frame intervals',()=>{
  const dots=at(900,'trace-dot');assert.ok(dots.length>10);for(const b of dots){assert.equal((b.birth-800)%6,0);assert.equal(b.speed,0);assert.ok(Math.abs(b.x-b.sourceX)<.0001&&Math.abs(b.y-b.sourceY)<.0001);}
  const a=dots.find(b=>b.birth===896),b=at(980,'trace-dot').find(b=>b.birth===896);assert.ok(a&&b);assert.equal(a.x,b.x);assert.equal(a.y,b.y);
  assert.ok(Math.abs(at(980,'trace-mother')[0].x-at(900,'trace-mother')[0].x)>100);
});
