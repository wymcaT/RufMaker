import {gmlNumber as num} from './gml.mjs';

export function trackDepth(track){return track.depth??(track.kind==='object'?-7000000:-100);}
export function sourceDepthGml(state){
  return 'switch(argument0){'+state.tracks.map((track,index)=>`case ${index}:${
    track.depth==null?'if argument1>0 return -7000000-argument1;':''
  }return ${num(trackDepth(track))}-argument1;`).join('\n')+'}\nreturn -100;';
}
