import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {readProjectWaveform} from './waveform-cache.mjs';
function fixture(t) {
  const root=fs.mkdtempSync(path.join(os.tmpdir(),'ruf-waveform-'));
  t.after(()=>fs.rmSync(root,{recursive:true,force:true}));
  fs.mkdirSync(path.join(root,'cache'));
  return {root, cache:path.join(root,'cache','rufmaker-waveform.json')};
}
test('missing cache accepts default BGM without missing packaged fallback', t=>{
  const {root}=fixture(t);
  assert.deepEqual(readProjectWaveform(root,'Data/Music/BGM.ogg'),{duration:0,peaks:[]});
});
test('missing cache accepts arbitrary song', t=>{
  const {root}=fixture(t);
  assert.deepEqual(readProjectWaveform(root,'Data/Music/other.ogg'),{duration:0,peaks:[]});
});
test('matching project cache remains byte-for-byte unchanged', t=>{
  const {root,cache}=fixture(t), data={source:'Data/Music/BGM.ogg',duration:12,peaks:[0.1,0.5]};
  const bytes=JSON.stringify(data); fs.writeFileSync(cache,bytes);
  assert.deepEqual(readProjectWaveform(root,data.source),data);
  assert.equal(fs.readFileSync(cache,'utf8'),bytes);
});
test('different song does not borrow cached peaks', t=>{
  const {root,cache}=fixture(t);
  fs.writeFileSync(cache,JSON.stringify({source:'other.ogg',duration:12,peaks:[1]}));
  assert.deepEqual(readProjectWaveform(root,'Data/Music/BGM.ogg'),{duration:0,peaks:[]});
});
test('corrupt cache still reports failure rather than masking it', t=>{
  const {root,cache}=fixture(t);fs.writeFileSync(cache,'invalid JSON');
  assert.throws(()=>readProjectWaveform(root,'song.ogg'),SyntaxError);
});
