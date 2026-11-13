import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {inflateSync} from 'node:zlib';
export function pngCrc(bytes){let crc=0xffffffff;for(const b of bytes){crc^=b;for(let k=0;k<8;k++)crc=(crc>>>1)^((crc&1)?0xedb88320:0);}return (crc^0xffffffff)>>>0;}
// Deliberately constrained decoder: no silent full-image approximation on unsupported input.
export function pngAlpha(bytes) {
  if(!bytes.subarray(0,8).equals(Buffer.from([137,80,78,71,13,10,26,10])))throw Error('Not a PNG');
  let width,height,type,channels,palette,trans,parts=[];
  for(let p=8;p<bytes.length;){if(p+12>bytes.length)throw Error('Truncated PNG chunk');const n=bytes.readUInt32BE(p),kind=bytes.toString('ascii',p+4,p+8);if(p+12+n>bytes.length)throw Error('Truncated PNG data');if(pngCrc(bytes.subarray(p+4,p+8+n))!==bytes.readUInt32BE(p+8+n))throw Error('Invalid PNG chunk CRC: '+kind);const b=bytes.subarray(p+8,p+8+n);p+=12+n;
    if(kind==='IHDR'){width=b.readUInt32BE(0);height=b.readUInt32BE(4);type=b[9];channels=({0:1,2:3,3:1,4:2,6:4})[type];if(b[8]!==8||!channels||b[10]||b[11]||b[12])throw Error('Sprite mask requires 8-bit noninterlaced standard PNG');if(!width||!height||width*height>67108864)throw Error('Unsupported PNG dimensions');}
    else if(kind==='PLTE')palette=b;else if(kind==='tRNS')trans=b;else if(kind==='IDAT')parts.push(b);else if(kind==='IEND')break;else if(kind[0]===kind[0].toUpperCase())throw Error('Unsupported critical PNG chunk: '+kind);
  }
  if(!channels)throw Error('Missing PNG IHDR');const stride=width*channels,raw=inflateSync(Buffer.concat(parts),{maxOutputLength:(stride+1)*height});if(raw.length!==(stride+1)*height)throw Error('Invalid PNG scanline size');
  const decoded=Buffer.alloc(stride*height),alpha=Buffer.alloc(width*height);
  for(let y=0;y<height;y++){const f=raw[y*(stride+1)];if(f>4)throw Error('Unsupported PNG filter');for(let x=0;x<stride;x++){const at=y*stride+x,a=x>=channels?decoded[at-channels]:0,b=y?decoded[at-stride]:0,c=y&&x>=channels?decoded[at-stride-channels]:0,p=a+b-c,pa=Math.abs(p-a),pb=Math.abs(p-b),pc=Math.abs(p-c);decoded[at]=(raw[y*(stride+1)+1+x]+(f===0?0:f===1?a:f===2?b:f===3?Math.floor((a+b)/2):pa<=pb&&pa<=pc?a:pb<=pc?b:c))&255;}}
  for(let i=0;i<alpha.length;i++){const p=i*channels;if(type===6)alpha[i]=decoded[p+3];else if(type===4)alpha[i]=decoded[p+1];else if(type===3){if(!palette||decoded[p]*3>=palette.length)throw Error('Invalid PNG palette');alpha[i]=trans&&decoded[p]<trans.length?trans[decoded[p]]:255;}else if(type===0)alpha[i]=trans&&decoded[p]===trans.readUInt16BE(0)?0:255;else alpha[i]=trans&&decoded[p]===trans.readUInt16BE(0)&&decoded[p+1]===trans.readUInt16BE(2)&&decoded[p+2]===trans.readUInt16BE(4)?0:255;}
  return {width,height,alpha};
}
const cache=new Map();
export function readSpriteMask(project,name) {
  const folder=path.join(project,'sprites',name),definition=fs.readFileSync(path.join(folder,'sprite.txt'));
  const values=Object.fromEntries(definition.toString().split(/\r?\n/).filter(s=>s.includes('=')).map(s=>s.trim().split('=')));
  const integer=(key,fallback,lo=-2147483648,hi=2147483647)=>{if(values[key]===undefined)return fallback;const text=values[key].trim(),v=Number(text);if(!text||!Number.isFinite(v)||!Number.isInteger(v)||v<lo||v>hi)throw Error(`Sprite ${name}: invalid ${key}; expected an integer in [${lo}, ${hi}].`);return v;};
  const frames=integer('frames',1,0,65536),originX=integer('origin_x',0),originY=integer('origin_y',0),bboxType=integer('bbox_type',0,0,2),perFrame=integer('per_frame_colliders',0,0,1)===1,tolerance=integer('alpha_tolerance',0,0,255);
  for(const k of ['bbox_left','bbox_top','bbox_right','bbox_bottom'])if(values[k]!==undefined)integer(k,0);
const files=Array.from({length:frames},(_,i)=>fs.readFileSync(path.join(folder,i+'.png'))),revision=crypto.createHash('sha256').update(definition);for(const b of files)revision.update(b);const rev=revision.digest('hex'),key=folder;
  if(cache.get(key)?.revision===rev)return cache.get(key);
  try {
    const images=files.map(pngAlpha),colliders=images.map(im=>{if(bboxType===1)return {left:0,top:0,right:im.width-1,bottom:im.height-1,empty:false};if(bboxType===2){const b=Object.fromEntries(['left','top','right','bottom'].map(k=>[k,Number(values['bbox_'+k])]));if(Object.values(b).some(v=>!Number.isFinite(v)))throw Error('Invalid manual bbox endpoints');return {...b,empty:b.right<b.left||b.bottom<b.top};}let left=im.width,top=im.height,right=-1,bottom=-1;for(let y=0;y<im.height;y++)for(let x=0;x<im.width;x++)if(im.alpha[y*im.width+x]>tolerance){left=Math.min(left,x);right=Math.max(right,x);top=Math.min(top,y);bottom=Math.max(bottom,y);}return {left,top,right,bottom,empty:right<left};});
    if(!perFrame&&colliders.length){const valid=colliders.filter(b=>!b.empty),union=valid.length?{left:Math.min(...valid.map(b=>b.left)),top:Math.min(...valid.map(b=>b.top)),right:Math.max(...valid.map(b=>b.right)),bottom:Math.max(...valid.map(b=>b.bottom)),empty:false}:{left:0,top:0,right:0,bottom:0,empty:true};colliders.splice(0,colliders.length,union);}
    const meta={originX,originY,frames,width:images[0]?.width??0,height:images[0]?.height??0,bboxType,perFrame,alphaTolerance:tolerance,revision:rev,colliders};cache.set(key,meta);return meta;
  } catch(e){throw Error(`Sprite ${name}: ${e.message}. Convert to a supported PNG or choose a valid boundary mask.`);}
}
const safeLegacyInteger=(text,fallback,lo,hi)=>{const n=Number(text);return text!==undefined&&text.trim()!==''&&Number.isInteger(n)&&n>=lo&&n<=hi?n:fallback;};
export function spriteMetadata(project,names){return Object.fromEntries(names.map(name=>{try{return [name,readSpriteMask(project,name)];}catch(e){let values={};try{values=Object.fromEntries(fs.readFileSync(path.join(project,'sprites',name,'sprite.txt'),'utf8').split(/\r?\n/).filter(s=>s.includes('=')).map(s=>s.trim().split('=')));}catch{}return [name,{error:`Sprite ${name}: ${e.message}`,originX:safeLegacyInteger(values.origin_x,0,-2147483648,2147483647),originY:safeLegacyInteger(values.origin_y,0,-2147483648,2147483647),frames:safeLegacyInteger(values.frames,1,1,65536)}];}}));}
export function maskBounds(meta,frame,x,y,sx,sy,angle,view=null){
  if(meta?.error)throw Error(meta.error);const boxes=meta?.colliders,box=boxes?.length?boxes[((Math.floor(frame)%boxes.length)+boxes.length)%boxes.length]:null;
  const b=!box||box.empty?{left:0,top:0,right:0,bottom:0}: {left:box.left-meta.originX,top:box.top-meta.originY,right:box.right-meta.originX,bottom:box.bottom-meta.originY};
  const a=angle*Math.PI/180,c=Math.cos(a),s=Math.sin(a),v=(view?.angle??0)*Math.PI/180,vc=Math.cos(v),vs=Math.sin(v);let left=Infinity,top=Infinity,right=-Infinity,bottom=-Infinity;
  for(const xx of [b.left,b.right])for(const yy of [b.top,b.bottom]){const lx=xx*sx,ly=yy*sy,wx=x+lx*c+ly*s,wy=y-lx*s+ly*c,dx=wx-(view?.x??0),dy=wy-(view?.y??0),px=view?dx*vc-dy*vs:wx,py=view?dx*vs+dy*vc:wy;left=Math.min(left,px);right=Math.max(right,px);top=Math.min(top,py);bottom=Math.max(bottom,py);}return {left,top,right,bottom};
}
export const maskOutside=(b,width,height,margin=0,centered=false)=>b.right<(centered?-width/2:0)-margin||b.left>(centered?width/2:width)+margin||b.bottom<(centered?-height/2:0)-margin||b.top>(centered?height/2:height)+margin;
