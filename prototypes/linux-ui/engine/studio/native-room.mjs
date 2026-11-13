import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {roomInstances} from './bullet-boundaries.mjs';

export const resourceName=name=>typeof name==='string'&&/^[A-Za-z_][\w]*$/.test(name);
export const fileRevision=bytes=>crypto.createHash('sha256').update(bytes).digest('hex');
const rows=text=>text.split(/\r?\n/).filter(Boolean);
function number(value,fallback=0){
  return value!==undefined&&value!==''&&Number.isFinite(Number(value))?Number(value):fallback;
}

export function readRoom(project,room,io=fs){
  if(typeof room!=='string'||!/^[A-Za-z0-9_]+$/.test(room))throw Error('无效的 GameMaker 房间名称');
  const folder=path.join(project,'rooms',room);
  const settings=Object.fromEntries(rows(io.readFileSync(path.join(folder,'room.txt'),'utf8'))
    .filter(line=>line.includes('=')).map(line=>[line.slice(0,line.indexOf('=')),line.slice(line.indexOf('=')+1)]));
  const width=number(settings.width,800),height=number(settings.height,608);
  const instanceText=io.readFileSync(path.join(folder,'instances.txt'));
  const instances=roomInstances(project,room,io,instanceText.toString('utf8'));
  const backgrounds=[],views=[],tiles=[];
  for(let i=0;i<8;i++){
    const source=settings['bg_source'+i];
    if(settings['bg_visible'+i]==='1'&&resourceName(source))backgrounds.push({
      source,foreground:settings['bg_is_foreground'+i]==='1',x:number(settings['bg_xoffset'+i]),
      y:number(settings['bg_yoffset'+i]),speedX:number(settings['bg_hspeed'+i]),speedY:number(settings['bg_vspeed'+i]),
      tileX:settings['bg_tile_h'+i]==='1',tileY:settings['bg_tile_v'+i]==='1',stretch:settings['bg_stretch'+i]==='1'
    });
    if(settings.views_enabled==='1'&&settings['view_visible'+i]==='1')views.push({
      slot:i,x:number(settings['view_xview'+i]),y:number(settings['view_yview'+i]),
      width:number(settings['view_wview'+i],width),height:number(settings['view_hview'+i],height),
      portX:number(settings['view_xport'+i]),portY:number(settings['view_yport'+i]),
      portWidth:number(settings['view_wport'+i],width),portHeight:number(settings['view_hport'+i],height),
      follow:settings['view_fol_target'+i]||''
    });
  }
  const layerFile=path.join(folder,'layers.txt');
  if(io.existsSync(layerFile))for(const depth of rows(io.readFileSync(layerFile,'utf8')).map(line=>line.trim())){
    if(!/^-?\d+$/.test(depth))continue;
    const file=path.join(folder,depth+'.txt');
    if(!io.existsSync(file))continue;
    for(const line of rows(io.readFileSync(file,'utf8'))){
      const [source,x,y,left,top,w,h,,sx,sy,blend]=line.split(',');
      if(resourceName(source)&&number(w)>0&&number(h)>0)tiles.push({
        source,depth:number(depth),x:number(x),y:number(y),left:number(left),top:number(top),width:number(w),height:number(h),
        scaleX:number(sx,1),scaleY:number(sy,1),blend:number(blend,4294967295)
      });
    }
  }
  const playerFile=path.join(project,'objects','player.txt');
  const playerDepth=io.existsSync(playerFile)?number(/^depth=([^\r\n]*)/m.exec(io.readFileSync(playerFile,'utf8'))?.[1],-10):-10;
  return {name:room,width,height,fps:number(settings.roomspeed,50),bgColor:number(settings.bg_color,2105376),playerDepth,
    instances,backgrounds,views,tiles,instanceRevision:fileRevision(instanceText)};
}
