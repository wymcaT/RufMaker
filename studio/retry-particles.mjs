import fs from 'node:fs';
import path from 'node:path';
import {codeOnly} from './native-import.mjs';

// Death particles live outside the instance snapshot. Only collect resources
// owned by the engine's blood-emitter objects; scene particle systems stay live.
export function retryParticleCleanup(project,io=fs){
  const objects=path.join(project,'objects'),blocks=[];
  for(const file of io.readdirSync(objects).filter(name=>/^(?:obj_?)?blood_?emitter\.gml$/i.test(name))){
    const source=codeOnly(io.readFileSync(path.join(objects,file),'utf8')),fields=[];
    for(const match of source.matchAll(/\b(?:self\.)?([A-Za-z_]\w*)\s*=\s*part_(system|type)_create\s*\(/g))if(!fields.some(row=>row.name===match[1]))fields.push({name:match[1],kind:match[2]});
    if(!fields.length)continue;
    const values=fields.map((_,i)=>'rf_death_resource'+i);
    blocks.push(`with(${file.slice(0,-4)}){var ${values.join(',')};${fields.map((row,i)=>`${values[i]}=-1;if variable_local_exists('${row.name}') ${values[i]}=${row.name};`).join('')}instance_destroy();${fields.map((row,i)=>`if part_${row.kind}_exists(${values[i]}) part_${row.kind}_destroy(${values[i]});`).join('')}}`);
  }
  return blocks.join('\n');
}

export function guardPendingDeath(source,native=false){
  const marker='// RufMaker pending retry death ('+(native?'native':'plain')+')';if(source.includes(marker))return source;
  const owner=native?'objRufNativePreview':'objRufController',enabled=native?'rf_playable':'rf_preview',file=native?'rufmaker_checkpoint_restore':'rufmaker_plain_restore';
  return marker+`\nif instance_exists(${owner}) {if ${owner}.${enabled} && file_exists(working_directory+'\\Data\\${file}') return 0;}\n`+source;
}
