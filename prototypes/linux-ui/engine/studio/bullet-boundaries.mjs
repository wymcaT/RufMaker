import path from 'node:path';
import {gmlNumber as num} from './gml.mjs';

export function boundaryMode(track) {
  return track.boundaryMode==='reflect'?1:track.boundaryMode==='wrap'?2:0;
}

export function usesBulletBoundary(track) {
  return track.bounceBricks===true||boundaryMode(track)!==0;
}

function settings(io,file) {
  if(!io.existsSync(file))return {};
  return Object.fromEntries(io.readFileSync(file,'utf8').split(/\r?\n/).filter(line=>line.includes('=')).map(line=>line.split(/=(.*)/s).slice(0,2)));
}

export function roomInstances(project,room,io,text) {
  const objects=new Map();
  function objectInfo(name) {
    if(!/^[A-Za-z_]\w*$/.test(name))return {};
    if(!objects.has(name)){
      const object=settings(io,path.join(project,'objects',name+'.txt'));
      const sprite=object.solid==='1'&&(object.mask||object.sprite);
      let bounds=null;
      if(sprite&&/^[A-Za-z_]\w*$/.test(sprite)){
        const folder=path.join(project,'sprites',sprite);
        const info=settings(io,path.join(folder,'sprite.txt'));
        const ox=Number(info.origin_x)||0,oy=Number(info.origin_y)||0;
        const png=path.join(folder,'0.png');
        if(info.bbox_type==='1'&&io.existsSync(png)){
          const header=io.readFileSync(png);
          if(header.length>=24)bounds=[-ox,-oy,header.readUInt32BE(16)-ox,header.readUInt32BE(20)-oy];
        }
        if(!bounds){
          const [left,top,right,bottom]=['bbox_left','bbox_top','bbox_right','bbox_bottom'].map(key=>Number(info[key]));
          if([left,top,right,bottom].every(Number.isFinite)&&left<=right&&top<=bottom)bounds=[left-ox,top-oy,right+1-ox,bottom+1-oy];
        }
      }
      objects.set(name,{bounds,depth:Number(object.depth)||0,visible:object.visible!=='0'});
    }
    return objects.get(name);
  }
  return (text??io.readFileSync(path.join(project,'rooms',room,'instances.txt'),'utf8')).split(/\r?\n/).filter(Boolean).map((raw,index)=>{
    const [name,x,y,id,,sx=1,sy=1,blend=4294967295,angle=0]=raw.split(',');
    const {bounds,depth=0,visible=true}=objectInfo(name);
    const result={index,id,name,x:Number(x),y:Number(y),raw,scaleX:Number(sx),scaleY:Number(sy),angle:Number(angle),blend:Number(blend),depth,visible};
    if(!bounds||!Number(sx)||!Number(sy))return result;
    const [left,top,right,bottom]=bounds;
    const radians=Number(angle)*Math.PI/180,cos=Math.cos(radians),sin=Math.sin(radians);
    const corners=[left,right].flatMap(px=>[top,bottom].map(py=>[px*sx*cos+py*sy*sin,-px*sx*sin+py*sy*cos]));
    const xs=corners.map(p=>p[0]),ys=corners.map(p=>p[1]);
    result.bulletBrick={left:Math.min(...xs),top:Math.min(...ys),right:Math.max(...xs),bottom:Math.max(...ys)};
    return result;
  });
}

export function absoluteBrick(instance) {
  const b=instance.bulletBrick;
  return {left:instance.x+b.left,top:instance.y+b.top,right:instance.x+b.right,bottom:instance.y+b.bottom};
}

const cellSize=128;

export function brickGrid(bricks) {
  if(bricks.length<=16)return null;
  const cell=value=>Math.floor(value/cellSize);
  let left=Infinity,top=Infinity,right=-Infinity,bottom=-Infinity;
  for(const brick of bricks){
    left=Math.min(left,cell(brick.left));top=Math.min(top,cell(brick.top));
    right=Math.max(right,cell(brick.right));bottom=Math.max(bottom,cell(brick.bottom));
  }
  const columns=right-left+1,count=columns*(bottom-top+1);
  if(!(count>0&&count<=4096)||left< -2147483648||top< -2147483648||right>=2147483647||bottom>=2147483647)return null;
  const cells=Array.from({length:count},()=>[]);
  let memberships=0;
  for(const [index,brick] of bricks.entries()){
    for(let y=cell(brick.top);y<=cell(brick.bottom);y++)
      for(let x=cell(brick.left);x<=cell(brick.right);x++){
        if(++memberships>16000)return null;
        cells[(y-top)*columns+x-left].push(index);
      }
  }
  return {left,top,right,bottom,columns,cells};
}

export function boundaryBrickInitialization(bricks,grid) {
  const positions=bricks.map((b,i)=>['left','top','right','bottom'].map(k=>`global.rf_brick_${k}[${i}]=${num(b[k])};`).join(''));
  if(grid){
    positions.push(`var rf_cell;for(rf_cell=0;rf_cell<${grid.cells.length};rf_cell+=1) global.rf_brick_cell_count[rf_cell]=0;`);
    for(const [i,cell] of grid.cells.entries())if(cell.length){
      positions.push(`global.rf_brick_cell_count[${i}]=${cell.length};`);
      positions.push(...cell.map((brick,j)=>`global.rf_brick_cell_member[${i},${j}]=${brick};`));
    }
  }
  return positions.join('\n')+'\nreturn 0;';
}

function wrapAxis(axis,delta,extent){
  return `rf_end=${axis}+${delta};
  if ${extent}>0 && ${axis}>=0 && ${axis}<=${extent} && (rf_end<0 || rf_end>${extent}) {
    rf_wrapped=rf_end-floor(rf_end/${extent})*${extent};
    if rf_wrapped=0 && rf_end>${extent} rf_wrapped=${extent};
    ${axis}=rf_wrapped;
  }else ${axis}=rf_end;`;
}

// Match BulletBoundaries.Move. Probe the whole movement segment so thin bricks
// also reflect bullets that would have passed through in one tick.
export function boundaryMovementScript(tracks,bricks,grid) {
  const cases=tracks.map((t,i)=>usesBulletBoundary(t)?`case ${i}: rf_mode=${boundaryMode(t)};rf_bricks=${t.bounceBricks?bricks.length:0};break;`:'').join('\n');
  return `var rf_mode,rf_bricks,rf_dx,rf_dy,rf_eps,rf_pass,rf_t,rf_ex,rf_ey,rf_hx,rf_hy,rf_wx,rf_wy;
var rf_i,rf_l,rf_r,rf_top,rf_bot,rf_nx,rf_ny,rf_fx,rf_fy,rf_entry,rf_swap,rf_end,rf_wrapped;
${bricks.length?'var rf_left,rf_right,rf_above,rf_below;':''}
${grid?'var rf_cx,rf_cy,rf_endx,rf_endy,rf_cell,rf_slot,rf_count;':''}
rf_mode=0;rf_bricks=0;
switch(argument0){${cases}}
rf_dx=argument1;rf_dy=argument2;rf_eps=.0000001;
if rf_mode=0 && rf_bricks=0 {x+=rf_dx;y+=rf_dy;return 0;}
if rf_mode=2 && rf_bricks=0 {
  if abs(rf_dx)<=rf_eps && abs(rf_dy)<=rf_eps return 0;
  ${wrapAxis('x','rf_dx','room_width')}
  ${wrapAxis('y','rf_dy','room_height')}
  return 0;
}
for(rf_pass=0;rf_pass<8 && (abs(rf_dx)>rf_eps || abs(rf_dy)>rf_eps);rf_pass+=1){
  rf_t=1;rf_hx=0;rf_hy=0;rf_wx=0;rf_wy=0;
  if rf_mode!=0 {
    rf_ex=2;rf_ey=2;
    if x>=0 && x<=room_width {
      if rf_dx>0 && x+rf_dx>room_width rf_ex=(room_width-x)/rf_dx;
      if rf_dx<0 && x+rf_dx<0 rf_ex=-x/rf_dx;
    }
    if y>=0 && y<=room_height {
      if rf_dy>0 && y+rf_dy>room_height rf_ey=(room_height-y)/rf_dy;
      if rf_dy<0 && y+rf_dy<0 rf_ey=-y/rf_dy;
    }
    rf_t=min(1,min(rf_ex,rf_ey));
    rf_hx=(rf_ex<=rf_t+rf_eps);rf_hy=(rf_ey<=rf_t+rf_eps);
    rf_wx=(rf_hx && rf_mode=2);rf_wy=(rf_hy && rf_mode=2);
  }
  ${bricks.length?`if rf_bricks>0 {
    rf_left=min(x,x+rf_dx);rf_right=max(x,x+rf_dx);rf_above=min(y,y+rf_dy);rf_below=max(y,y+rf_dy);
    ${grid?`rf_cell=-1;rf_count=rf_bricks;
    rf_cx=floor(rf_left/${cellSize});rf_cy=floor(rf_above/${cellSize});rf_endx=floor(rf_right/${cellSize});rf_endy=floor(rf_below/${cellSize});
    if rf_cx>${num(grid.right)} || rf_cy>${num(grid.bottom)} || rf_endx<${num(grid.left)} || rf_endy<${num(grid.top)} rf_count=0;
    else if rf_cx=rf_endx && rf_cy=rf_endy {
      rf_cell=(rf_cy-(${num(grid.top)}))*${grid.columns}+rf_cx-(${num(grid.left)});
      rf_count=global.rf_brick_cell_count[rf_cell];
    }
    for(rf_slot=0;rf_slot<rf_count;rf_slot+=1){
    rf_i=rf_slot;if rf_cell>=0 rf_i=global.rf_brick_cell_member[rf_cell,rf_slot];`:'for(rf_i=0;rf_i<rf_bricks;rf_i+=1){'}
    rf_l=global.rf_brick_left[rf_i];rf_r=global.rf_brick_right[rf_i];rf_top=global.rf_brick_top[rf_i];rf_bot=global.rf_brick_bottom[rf_i];
    if rf_right<rf_l || rf_left>rf_r || rf_below<rf_top || rf_above>rf_bot continue;
    rf_nx=-100000000000000000000;rf_fx=100000000000000000000;rf_ny=-100000000000000000000;rf_fy=100000000000000000000;
    if abs(rf_dx)<=rf_eps {if x<rf_l || x>=rf_r continue;}
    else {rf_nx=(rf_l-x)/rf_dx;rf_fx=(rf_r-x)/rf_dx;if rf_dx<0 {rf_swap=rf_nx;rf_nx=rf_fx;rf_fx=rf_swap;}}
    if abs(rf_dy)<=rf_eps {if y<rf_top || y>=rf_bot continue;}
    else {rf_ny=(rf_top-y)/rf_dy;rf_fy=(rf_bot-y)/rf_dy;if rf_dy<0 {rf_swap=rf_ny;rf_ny=rf_fy;rf_fy=rf_swap;}}
    rf_entry=max(rf_nx,rf_ny);
    if rf_entry<(-rf_eps) || rf_entry>rf_t+rf_eps || min(rf_fx,rf_fy)<=rf_entry+rf_eps continue;
    if rf_entry<rf_t-rf_eps {rf_hx=0;rf_hy=0;rf_wx=0;rf_wy=0;}
    rf_t=max(0,min(rf_t,rf_entry));
    if rf_nx>=rf_ny-rf_eps {rf_hx=1;rf_wx=0;}
    if rf_ny>=rf_nx-rf_eps {rf_hy=1;rf_wy=0;}
  }}`:''}
  x+=rf_dx*rf_t;y+=rf_dy*rf_t;
  if !rf_hx && !rf_hy return 0;
  if rf_wx {if rf_dx>0 x=0;else x=room_width;}else if rf_hx {rf_dx=-rf_dx;rf_dir=180-rf_dir;}
  if rf_wy {if rf_dy>0 y=0;else y=room_height;}else if rf_hy {rf_dy=-rf_dy;rf_dir=-rf_dir;}
  rf_dx*=1-rf_t;rf_dy*=1-rf_t;
}
return 0;`;
}
