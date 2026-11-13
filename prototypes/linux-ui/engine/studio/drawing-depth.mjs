import {gmlNumber as num} from './gml.mjs';
import {trailMode} from './trails.mjs';

export function trackDepth(track){return track.depth??(track.kind==='object'?-7000000:-100);}
// Match DrawingDepth.TrailBase: opaque objects without trails are not anchors.
export function trailBase(tracks){return Math.max(-6999999,...tracks.filter(t=>t.enabled!==false&&(t.kind==='rod'||t.kind==='emitter'||t.kind==='object'&&trailMode(t)!=='off')).map(t=>trackDepth(t)+1));}
export function sourceDepthGml(state){
  return 'switch(argument0){'+state.tracks.map((track,index)=>`case ${index}:${
    track.depthMode!=='absolute'&&track.depth==null?'if argument1>0 return -7000000-argument1;':''
  }return ${num(trackDepth(track))}${track.depthMode==='absolute'?'':'-argument1'};`).join('\n')+'}\nreturn -100;';
}
