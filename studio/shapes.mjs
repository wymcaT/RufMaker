// heart / star2 are adapted from the user's Verse3 scripts. See assets/gml/formations.
// Geometry is shared by persistent collision bullets, releases and sampled trails.
export const formationIds={heart:1,star2:2,star:3,ellipse:4,flower:5,infinity:6};
export function formationPoint(shape,u,radius=1,orientation=0,sides=5,aspect=1,inner=.45){
  u-=Math.floor(u);sides=Math.max(3,Math.min(64,Math.round(sides)));aspect=Math.max(.05,Math.min(10,aspect));inner=Math.max(.01,Math.min(1,inner));
  const theta=u*2*Math.PI;let x,y;
  if(shape==='heart'){const s=Math.sin(theta);x=s*s*s;y=(-13*Math.cos(theta)+5*Math.cos(2*theta)+2*Math.cos(3*theta)+Math.cos(4*theta))/16;}
  else if(['star','star2','polygon'].includes(shape)){
    const vertices=shape==='star'?sides*2:sides,v=u*vertices,segment=Math.floor(v),mix=v-segment,a=segment*2*Math.PI/vertices,b=(segment+(shape==='star2'?2:1))*2*Math.PI/vertices;
    const ra=shape==='star'&&segment%2===1?inner:1,rb=shape==='star'&&segment%2===0?inner:1;
    x=Math.cos(a)*ra*(1-mix)+Math.cos(b)*rb*mix;y=-Math.sin(a)*ra*(1-mix)-Math.sin(b)*rb*mix;
  }else if(shape==='infinity'){const s=Math.sin(theta),den=1+s*s;x=Math.cos(theta)/den;y=-2*s*Math.cos(theta)/den;}
  else {const r=shape==='flower'?inner+(1-inner)*(.5+.5*Math.cos(sides*theta)):1;x=r*Math.cos(theta);y=-r*Math.sin(theta);}
  y*=shape==='polygon'?1:aspect;const rotation=orientation*Math.PI/180,c=Math.cos(rotation),s=Math.sin(rotation);
  return {x:radius*(x*c+y*s),y:radius*(-x*s+y*c)};
}
export function formationPerimeter(shape,radius=1,sides=5,aspect=1,inner=.45){
  if(shape==='ring')return 2*Math.PI*radius;
  if(shape==='polygon')return 2*sides*radius*Math.sin(Math.PI/sides);
  if(shape==='star2'||shape==='star'){
    const vertices=shape==='star'?2*sides:sides;let total=0;
    for(let i=0;i<vertices;i++){const a=formationPoint(shape,i/vertices,radius,0,sides,aspect,inner),b=formationPoint(shape,(i+(shape==='star2'?2:1))/vertices,radius,0,sides,aspect,inner);total+=Math.hypot(b.x-a.x,b.y-a.y);}return total;
  }
  let length=0,previous=formationPoint(shape,0,radius,0,sides,aspect,inner);
  for(let i=1;i<=128;i++){const p=formationPoint(shape,i/128,radius,0,sides,aspect,inner);length+=Math.hypot(p.x-previous.x,p.y-previous.y);previous=p;}return length;
}
export const formationScripts=[['rf_formation_point',`// axis, shape, phase, radius, orientation, sides, height ratio, inner ratio
var rf_kind,rf_t,rf_n,rf_aspect,rf_inner,rf_theta,rf_ux,rf_uy,rf_s,rf_den,rf_vertices,rf_v,rf_segment,rf_mix,rf_a,rf_b,rf_ra,rf_rb,rf_r,rf_c;
rf_kind=argument1;rf_t=argument2-floor(argument2);rf_n=clamp(round(argument5),3,64);rf_aspect=clamp(argument6,.05,10);rf_inner=clamp(argument7,.01,1);rf_theta=rf_t*2*pi;
if rf_kind=1 {rf_s=sin(rf_theta);rf_ux=rf_s*rf_s*rf_s;rf_uy=(-13*cos(rf_theta)+5*cos(2*rf_theta)+2*cos(3*rf_theta)+cos(4*rf_theta))/16;}
else if rf_kind=2 || rf_kind=3 {
rf_vertices=rf_n;if rf_kind=3 rf_vertices*=2;rf_v=rf_t*rf_vertices;rf_segment=floor(rf_v);rf_mix=rf_v-rf_segment;rf_a=rf_segment*2*pi/rf_vertices;rf_b=(rf_segment+1+(rf_kind=2))*2*pi/rf_vertices;
rf_ra=1;rf_rb=1;if rf_kind=3 {if rf_segment mod 2=1 rf_ra=rf_inner;else rf_rb=rf_inner;}
rf_ux=cos(rf_a)*rf_ra*(1-rf_mix)+cos(rf_b)*rf_rb*rf_mix;rf_uy=-sin(rf_a)*rf_ra*(1-rf_mix)-sin(rf_b)*rf_rb*rf_mix;
} else if rf_kind=6 {rf_s=sin(rf_theta);rf_den=1+rf_s*rf_s;rf_ux=cos(rf_theta)/rf_den;rf_uy=-2*rf_s*cos(rf_theta)/rf_den;}
else {rf_r=1;if rf_kind=5 rf_r=rf_inner+(1-rf_inner)*(.5+.5*cos(rf_n*rf_theta));rf_ux=rf_r*cos(rf_theta);rf_uy=-rf_r*sin(rf_theta);}
rf_uy*=rf_aspect;rf_a=degtorad(argument4);rf_c=cos(rf_a);rf_s=sin(rf_a);
if argument0=0 return argument3*(rf_ux*rf_c+rf_uy*rf_s);return argument3*(-rf_ux*rf_s+rf_uy*rf_c);
`],['rf_formation_perimeter',`// shape, radius, sides, height ratio, inner ratio
var rf_i,rf_steps,rf_step,rf_total,rf_ax,rf_ay,rf_bx,rf_by;
rf_total=0;rf_steps=128;rf_step=1;
if argument0=2 || argument0=3 {rf_steps=clamp(round(argument2),3,64);if argument0=3 rf_steps*=2;else rf_step=2;}
for(rf_i=0;rf_i<rf_steps;rf_i+=1){
rf_ax=rf_formation_point(0,argument0,rf_i/rf_steps,argument1,0,argument2,argument3,argument4);rf_ay=rf_formation_point(1,argument0,rf_i/rf_steps,argument1,0,argument2,argument3,argument4);
rf_bx=rf_formation_point(0,argument0,(rf_i+rf_step)/rf_steps,argument1,0,argument2,argument3,argument4);rf_by=rf_formation_point(1,argument0,(rf_i+rf_step)/rf_steps,argument1,0,argument2,argument3,argument4);
rf_total+=point_distance(rf_ax,rf_ay,rf_bx,rf_by);
}return rf_total;
`]];
export function shapeSetup(t,time='rf_frame') {
  // Parent transforms scale movement too. Noncircular shapes rotate by orientation;
  // adding the parent's angle to their phase would move bullets along the outline again.
  const speed=t._parentIndex>=0?'(rf_rollSpeed*global.rf_gscale)':'rf_rollSpeed';
  if((t.shape||'rod')==='rod') {
    return `${t.rodMode==='center'?'rf_x1=rf_x-lengthdir_x(rf_length/2,rf_angle);rf_y1=rf_y-lengthdir_y(rf_length/2,rf_angle);rf_x2=rf_x+lengthdir_x(rf_length/2,rf_angle);rf_y2=rf_y+lengthdir_y(rf_length/2,rf_angle);':t.rodMode==='anchor'?'rf_x1=rf_x;rf_y1=rf_y;':''}
rf_len=point_distance(rf_x1,rf_y1,rf_x2,rf_y2);rf_phase=0;rf_count=max(1,min(300,round(rf_count)));
${t.rolling?`if rf_len>.001 rf_phase=(${time}-${t.start})*${speed}/rf_len;`:''}`;
  }
  const perimeter=formationIds[t.shape]?`rf_formation_perimeter(${formationIds[t.shape]},rf_radius,rf_sides,rf_shapeAspect,rf_shapeInner)`:t.shape==='polygon'?'2*rf_sides*rf_radius*sin(pi/rf_sides)':'2*pi*rf_radius';
  const phase=t._parentIndex>=0&&t.shape!=='ring'?'(rf_angle-global.rf_ga)/360':'rf_angle/360';
  return `rf_radius=max(0,rf_radius);rf_sides=max(3,min(64,round(rf_sides)));rf_count=max(1,min(300,round(rf_count)));rf_phase=${phase};
rf_len=${t.rolling?perimeter:'0'};
${t.rolling?`if rf_len>.001 rf_phase+=(${time}-${t.start})*${speed}/rf_len;`:''}`;
}
export function shapePoint(t) {
  if((t.shape||'rod')==='rod')return `${t.rolling?'rf_fraction=rf_j/rf_count+rf_phase;rf_fraction-=floor(rf_fraction);':'if rf_count=1 rf_fraction=.5;else rf_fraction=rf_j/(rf_count-1);'}
rf_px=rf_x1+(rf_x2-rf_x1)*rf_fraction;rf_py=rf_y1+(rf_y2-rf_y1)*rf_fraction;`;
  const start=`rf_fraction=(rf_j+${t.shape==='heart'?1:0})/rf_count+rf_phase;rf_fraction-=floor(rf_fraction);`;
  if(formationIds[t.shape])return `${start}rf_px=rf_x+rf_formation_point(0,${formationIds[t.shape]},rf_fraction,rf_radius,rf_orientation,rf_sides,rf_shapeAspect,rf_shapeInner);rf_py=rf_y+rf_formation_point(1,${formationIds[t.shape]},rf_fraction,rf_radius,rf_orientation,rf_sides,rf_shapeAspect,rf_shapeInner);`;
  if(t.shape==='ring')return `${start}rf_px=rf_x+lengthdir_x(rf_radius,rf_fraction*360);rf_py=rf_y+lengthdir_y(rf_radius,rf_fraction*360);`;
  return `${start}rf_seg=floor(rf_fraction*rf_sides);rf_mix=rf_fraction*rf_sides-rf_seg;
rf_px=rf_x+lerp(lengthdir_x(rf_radius,rf_orientation+rf_seg*360/rf_sides),lengthdir_x(rf_radius,rf_orientation+(rf_seg+1)*360/rf_sides),rf_mix);
rf_py=rf_y+lerp(lengthdir_y(rf_radius,rf_orientation+rf_seg*360/rf_sides),lengthdir_y(rf_radius,rf_orientation+(rf_seg+1)*360/rf_sides),rf_mix);`;
}
