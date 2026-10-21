import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import assert from 'node:assert/strict';
import test from 'node:test';
import {resolveGameMaker} from './engine-path.mjs';

test('chosen engine settings, explicit process path and portable fallback',()=>{
  const root=fs.mkdtempSync(path.join(os.tmpdir(),'rufmaker-engine-path-'));
  try {
    fs.mkdirSync(path.join(root,'user-data'));const chosen=path.join(root,'custom','GameMaker.exe'),portable=path.join(root,'GameMaker8.2','GameMaker.exe');
    for(const file of [chosen,portable]){fs.mkdirSync(path.dirname(file),{recursive:true});fs.writeFileSync(file,'test path');}
    fs.writeFileSync(path.join(root,'user-data','settings.json'),JSON.stringify({gameMaker:chosen,language:'en'}));
    assert.equal(resolveGameMaker(root,{}),chosen);assert.equal(resolveGameMaker(root,{RUFMAKER_GAMEMAKER:portable}),portable);
    fs.unlinkSync(chosen);assert.equal(resolveGameMaker(root,{}),portable);
  } finally {
    const resolved=path.resolve(root);assert.equal(path.dirname(resolved),path.resolve(os.tmpdir()));assert(path.basename(resolved).startsWith('rufmaker-engine-path-'));fs.rmSync(resolved,{recursive:true});
  }
});
