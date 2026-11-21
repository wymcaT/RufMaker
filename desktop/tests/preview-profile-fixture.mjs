import fs from 'node:fs';
import path from 'node:path';
import {readRoom} from '../../studio/native-room.mjs';

const [source, output] = process.argv.slice(2);
const project = path.resolve(source);
const read = file => fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, '');
const settings = file => Object.fromEntries(read(file).split(/\r?\n/).filter(line=>line.includes('=')).map(line=>[line.slice(0,line.indexOf('=')),line.slice(line.indexOf('=')+1)]));
const state = JSON.parse(read(path.join(project,'rufmaker.json')));
const spriteMeta = {}, objectSprites = {};
for(const dir of fs.readdirSync(path.join(project,'sprites'))){
  const file=path.join(project,'sprites',dir,'sprite.txt');
  if(fs.existsSync(file)){const s=settings(file);spriteMeta[dir]={originX:+s.origin_x,originY:+s.origin_y,frames:+s.frames};}
}
for(const file of fs.readdirSync(path.join(project,'objects')).filter(f=>f.endsWith('.txt'))){
  objectSprites[path.basename(file,'.txt')]=settings(path.join(project,'objects',file)).sprite||'';
}
fs.writeFileSync(output,JSON.stringify({project,state,room:readRoom(project,state.room),spriteMeta,inventory:{objectSprites}}));
