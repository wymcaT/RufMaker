// Trail samples are evaluated from their birth time, so seeking needs no simulation.
export const trailModes=['off','fade','shrink','shrink-fade','appear','custom'];
const num=v=>String(Number.isFinite(Number(v))?Number(v):0);
export const trailMode=t=>t.trailMode==='legacy'||t.trailMode===undefined?(t.trail?'fade':'off'):t.trailMode;
export const trailDirection=(t,seed)=>t.trailRandom?`((sin((${seed}+1)*12.9898)*43758.5453-floor(sin((${seed}+1)*12.9898)*43758.5453))*360)`:num(t.trailDirection);
export function trailCall(t,{sprite=t.sprite,x='rf_x',y='rf_y',scale='rf_scale',angle='rf_angle',alpha='rf_alpha',color,age,seed}) {
  return `rf_trail_draw(${sprite},${x},${y},${scale},${angle},${alpha},${color},${age},'${trailMode(t)}',${num(t.trailLife)},${num(t.trailStartAlpha)},${num(t.trailScaleDecay)},${num(t.trailAlphaDecay)},${num(t.trailSpeed)},${trailDirection(t,seed)});`;
}
export const trailGml=`// Arguments: sprite, x, y, scale, angle, alpha, color, age, mode,
// lifetime, initial opacity, scale decay/frame, alpha decay/frame, drift speed, drift angle.
var a,m,n,z,o,d;
a=argument7;m=argument8;n=max(1,argument9);z=1;o=argument10;
if m='off' || a<0 || a>=n return 0;
if m='shrink' || m='shrink-fade' z=1-a/n;
if m='fade' || m='shrink-fade' o*=1-a/n;
if m='appear' o*=a/n;
if m='custom' {z=max(0,1-argument11*a);o=argument10-argument12*a;}
if z<=0 || o<=0 return 0;
d=argument13*a;
draw_sprite_ext(argument0,0,argument1+lengthdir_x(d,argument14),argument2+lengthdir_y(d,argument14),argument3*z,argument3*z,argument4,argument6,argument5*max(0,min(1,o)));
return 0;
`;
