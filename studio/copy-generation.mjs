import {gmlNumber as num} from './gml.mjs';
export const copyDefaults={copyMode:'directions',copyFacing:'keep',copyColorMode:'inherit',copyColor:'#FFFFFF',copyUp:true,copyDown:true,copyLeft:true,copyRight:true,copyCenter:false,copyCount:1,copySpacingX:128,copySpacingY:128,copyWidth:640,copyHeight:384,copyRadius:180,copyAngle:0,copyFacingAngle:0,copyHue:0,copyColorDensity:1};
const direction=(x,y)=>x===0&&y===0?0:(Math.atan2(-y,x)*180/Math.PI+360)%360;
export function copyPlacements(a){const points=[];const add=(x,y)=>{if(!a.copyCenter&&Math.abs(x)<1e-6&&Math.abs(y)<1e-6)return;points.push({x,y,angle:direction(x,y)});};
  if(!Number.isInteger(a.copyCount)||a.copyCount<1||a.copyCount>300||!['copySpacingX','copySpacingY','copyWidth','copyHeight','copyRadius','copyAngle','copyFacingAngle','copyHue','copyColorDensity'].every(k=>Number.isFinite(a[k])))throw Error('复制参数无效');
  if(a.copyMode==='circle')for(let i=0;i<a.copyCount;i++){const r=(a.copyAngle+360*i/a.copyCount)*Math.PI/180;add(a.copyRadius*Math.cos(r),-a.copyRadius*Math.sin(r));}
  else if(a.copyMode==='grid'){if(a.copySpacingX<=0||a.copySpacingY<=0||a.copyWidth<0||a.copyHeight<0)throw Error('网格间距必须大于0，宽高不能为负数。');const columns=Math.floor(a.copyWidth/a.copySpacingX)+1,rows=Math.floor(a.copyHeight/a.copySpacingY)+1;if(columns*rows>300)throw Error('每轮最多复制300个物件，请增大间距或缩小网格。');const r=a.copyAngle*Math.PI/180;for(let y=0;y<rows;y++)for(let x=0;x<columns;x++){const px=(x-(columns-1)/2)*a.copySpacingX,py=(y-(rows-1)/2)*a.copySpacingY;add(px*Math.cos(r)+py*Math.sin(r),-px*Math.sin(r)+py*Math.cos(r));}}
  else for(let layer=1;layer<=a.copyCount;layer++){const d=layer*a.copySpacingX;if(a.copyUp)add(0,-d);if(a.copyDown)add(0,d);if(a.copyLeft)add(-d,0);if(a.copyRight)add(d,0);}
  if(points.length>300)throw Error('每轮最多复制300个物件，请减少复制数量。');return points.map((p,index)=>({...p,index,count:points.length}));
}
export function copySpawnGml(a,target,c){return copyPlacements(a).map(p=>{
  const facing=a.copyFacing==='keep'?`(${c.heading}+${num(a.copyFacingAngle)})`:a.copyFacing==='fixed'?num(a.copyFacingAngle):num(p.angle+(a.copyFacing==='inward'?180:a.copyFacing==='tangent'?90:0)+a.copyFacingAngle);
  const angle=`(${facing}-rf_copy_anchor_angle(${target}))`,hue=a.copyHue+360*p.index/Math.max(1,p.count)*a.copyColorDensity;
  let tint=c.color,image=c.lookImage,lookColor=c.lookColor,lookSprite=a.inheritAppearance?c.lookSprite:'-1';if(c.source.rainbow&&a.copyColorMode==='inherit')lookSprite='-1';
  if(a.copyColorMode==='fixed'){const v=/^#[0-9a-f]{6}$/i.test(a.copyColor)?a.copyColor.slice(1):'ffffff';tint=`make_color_rgb(${parseInt(v.slice(0,2),16)},${parseInt(v.slice(2,4),16)},${parseInt(v.slice(4,6),16)})`;}
  if(a.copyColorMode==='rainbow'){tint=`rf_color_hue(${num(hue)})`;if(c.source.rainbow){lookSprite=c.lookSprite;image=String(Math.floor(((hue%360+360)%360)*32/360));lookColor='c_white';tint='c_white';}}
  return `rf_inst=rf_template_spawn(${target},rf_now,${c.x}+(${num(p.x)})+lengthdir_x(${num(a.spawnOffsetX)},${angle})+lengthdir_x(${num(a.spawnOffsetY)},${angle}-90),${c.y}+(${num(p.y)})+lengthdir_y(${num(a.spawnOffsetX)},${angle})+lengthdir_y(${num(a.spawnOffsetY)},${angle}-90),${angle},${c.generation}+1,${c.sourceX},${c.sourceY},${lookSprite},${lookColor},${image},${a.inheritScale?1:0},${c.lookScale},${tint});if instance_exists(rf_inst) {${c.after}}`;
}).join('\n');}
