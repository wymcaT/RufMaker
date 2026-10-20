// Geometry is shared by persistent collision bullets and analytically sampled trails.
export function shapeSetup(t,time='rf_frame') {
  if((t.shape||'rod')==='rod') {
    return `${t.rodMode==='center'?'rf_x1=rf_x-lengthdir_x(rf_length/2,rf_angle);rf_y1=rf_y-lengthdir_y(rf_length/2,rf_angle);rf_x2=rf_x+lengthdir_x(rf_length/2,rf_angle);rf_y2=rf_y+lengthdir_y(rf_length/2,rf_angle);':t.rodMode==='anchor'?'rf_x1=rf_x;rf_y1=rf_y;':''}
rf_len=point_distance(rf_x1,rf_y1,rf_x2,rf_y2);rf_phase=0;rf_count=max(1,min(300,round(rf_count)));
${t.rolling?`if rf_len>.001 rf_phase=(${time}-${t.start})*rf_rollSpeed/rf_len;`:''}`;
  }
  return `rf_radius=max(0,rf_radius);rf_sides=max(3,min(64,round(rf_sides)));rf_count=max(1,min(300,round(rf_count)));rf_phase=rf_angle/360;
rf_len=${t.shape==='polygon'?'2*rf_sides*rf_radius*sin(pi/rf_sides)':'2*pi*rf_radius'};
${t.rolling?`if rf_len>.001 rf_phase+=(${time}-${t.start})*rf_rollSpeed/rf_len;`:''}`;
}
export function shapePoint(t) {
  if((t.shape||'rod')==='rod')return `${t.rolling?'rf_fraction=rf_j/rf_count+rf_phase;rf_fraction-=floor(rf_fraction);':'if rf_count=1 rf_fraction=.5;else rf_fraction=rf_j/(rf_count-1);'}
rf_px=rf_x1+(rf_x2-rf_x1)*rf_fraction;rf_py=rf_y1+(rf_y2-rf_y1)*rf_fraction;`;
  const start='rf_fraction=rf_j/rf_count+rf_phase;rf_fraction-=floor(rf_fraction);';
  if(t.shape==='ring')return `${start}rf_px=rf_x+lengthdir_x(rf_radius,rf_fraction*360);rf_py=rf_y+lengthdir_y(rf_radius,rf_fraction*360);`;
  return `${start}rf_seg=floor(rf_fraction*rf_sides);rf_mix=rf_fraction*rf_sides-rf_seg;
rf_px=rf_x+lerp(lengthdir_x(rf_radius,rf_orientation+rf_seg*360/rf_sides),lengthdir_x(rf_radius,rf_orientation+(rf_seg+1)*360/rf_sides),rf_mix);
rf_py=rf_y+lerp(lengthdir_y(rf_radius,rf_orientation+rf_seg*360/rf_sides),lengthdir_y(rf_radius,rf_orientation+(rf_seg+1)*360/rf_sides),rf_mix);`;
}
