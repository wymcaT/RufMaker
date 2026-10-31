import {parameterCode as parameter} from './random-parameters.mjs';
import {gmlNumber as n} from './gml.mjs';

export const cameraPostGml=`// Render an already composed room through a camera, including mirrored borders.
var src,dst,swap,a,c,s,z,hx,hy,ix,iy,x0,xn,y0,yn,px,py,dx,dy,sx,sy,i,offset;
src=argument0;dst=argument1;a=degtorad(rf_cam_angle);c=cos(a);s=sin(a);z=rf_cam_zoom;
surface_set_target(dst);d3d_set_projection_ortho(0,0,room_width,room_height,0);draw_set_blend_mode(bm_normal);draw_set_alpha(1);draw_clear(c_black);
hx=(abs(c)*room_width+abs(s)*room_height)/(2*z);hy=(abs(s)*room_width+abs(c)*room_height)/(2*z);
x0=0;xn=0;y0=0;yn=0;
if rf_cam_mirror {x0=floor((rf_cam_x-hx)/room_width);xn=floor((rf_cam_x+hx)/room_width);y0=floor((rf_cam_y-hy)/room_height);yn=floor((rf_cam_y+hy)/room_height);}
for(iy=y0;iy<=yn;iy+=1)for(ix=x0;ix<=xn;ix+=1){
 sx=1;sy=1;px=ix*room_width;py=iy*room_height;
 if abs(ix) mod 2=1 {sx=-1;px+=room_width;}if abs(iy) mod 2=1 {sy=-1;py+=room_height;}
 dx=room_width/2+z*(c*(px-rf_cam_x)-s*(py-rf_cam_y));dy=room_height/2+z*(s*(px-rf_cam_x)+c*(py-rf_cam_y));
 draw_surface_ext(src,dx,dy,sx*z,sy*z,-rf_cam_angle,c_white,1);
}
rf_fx_opaque();surface_reset_target();
if rf_cam_blur>0 {
 swap=rf_fx_a;if dst=rf_fx_a swap=rf_fx_b;surface_set_target(swap);draw_clear(c_black);draw_set_blend_mode(bm_add);
 for(i=0;i<9;i+=1){
  offset=(i/8-.5)*rf_cam_blur;dx=lengthdir_x(offset,rf_cam_blur_angle);dy=lengthdir_y(offset,rf_cam_blur_angle);
  for(iy=min(0,-sign(dy));iy<=max(0,-sign(dy));iy+=1)for(ix=min(0,-sign(dx));ix<=max(0,-sign(dx));ix+=1){sx=1;sy=1;px=ix*room_width;py=iy*room_height;if ix!=0 {sx=-1;px+=room_width;}if iy!=0 {sy=-1;py+=room_height;}draw_surface_ext(dst,px+dx,py+dy,sx,sy,0,c_white,1/9);}
 }
 draw_set_blend_mode(bm_normal);rf_fx_opaque();surface_reset_target();return swap;
}return dst;
`;

// Caller is objRufController; do not touch the original engine RNG.
export const styleFxGml=`var src,dst,kind,strength,opacity,angle,zoom,flash,retention,slices,hold,start,index,age,bucket,seed,v,rf_slice_y,h,dx,dy,i,z,history;
src=argument0;dst=argument1;kind=argument2;strength=max(0,argument3);opacity=max(0,min(1,argument4));angle=argument5;zoom=max(1,min(3,argument6));flash=max(0,min(1,argument7));retention=max(0,min(1,argument8));slices=max(1,min(32,round(argument9)));hold=max(1,min(12,round(argument10)));start=argument11;index=argument12;
d3d_set_projection_ortho(0,0,room_width,room_height,0);draw_set_blend_mode(bm_normal);draw_set_alpha(1);
if opacity<=0 {draw_surface(src,0,0);return 0;}
if kind='rgb-impact' {
 z=1+(zoom-1)*opacity;dx=round(lengthdir_x(strength*opacity,angle));dy=round(lengthdir_y(strength*opacity,angle));draw_set_blend_mode(bm_add);
 draw_surface_ext(src,room_width*(1-z)/2+dx,room_height*(1-z)/2+dy,z,z,0,c_red,1);
 draw_surface_ext(src,room_width*(1-z)/2,room_height*(1-z)/2,z,z,0,c_lime,1);
 draw_surface_ext(src,room_width*(1-z)/2-dx,room_height*(1-z)/2-dy,z,z,0,c_blue,1);draw_set_blend_mode(bm_normal);
 if (retention*opacity>0) {
  z=1+(z-1)*1.75;draw_surface_ext(src,room_width*(1-z)/2+strength*.55*opacity,room_height*(1-z)/2,z,z,0,c_white,retention*opacity);
 }
 if (flash*opacity>0) {
  draw_set_color(argument13);draw_set_alpha(flash*opacity);draw_rectangle(0,0,room_width,room_height,0);draw_set_color(c_white);draw_set_alpha(1);
 }
}else{
 age=max(0,floor(rf_frame-start));bucket=floor(age/hold);
 if !surface_exists(rf_fx_history[index]) rf_fx_history[index]=surface_create(room_width,room_height);
 if surface_exists(rf_fx_history[index]) {
  if rf_frame<rf_fx_history_frame[index] || rf_frame-rf_fx_history_frame[index]>hold || floor((rf_fx_history_frame[index]-start)/hold)!=bucket {
   surface_reset_target();surface_copy_part(rf_fx_history[index],0,0,src,0,0,room_width,room_height);surface_set_target(dst);rf_fx_history_frame[index]=rf_frame;
  }src=rf_fx_history[index];
 }
 dx=round(lengthdir_x(8*opacity,angle));dy=round(lengthdir_y(8*opacity,angle));draw_set_blend_mode(bm_add);
 draw_surface_ext(src,dx,dy,1,1,0,c_red,1);draw_surface_ext(src,0,0,1,1,0,c_lime,1);draw_surface_ext(src,-dx,-dy,1,1,0,c_blue,1);draw_set_blend_mode(bm_normal);
 for(i=0;i<slices;i+=1){
  seed=bucket*131+i*37+start;v=sin((seed+1)*12.9898)*43758.5453;v-=floor(v);rf_slice_y=floor(v*max(1,room_height-24));
  v=sin((seed+10)*12.9898)*43758.5453;v-=floor(v);h=min(room_height-rf_slice_y,4+floor(v*20));
  v=sin((seed+18)*12.9898)*43758.5453;v-=floor(v);dx=round((v*2-1)*strength*opacity);
  draw_surface_part_ext(src,0,rf_slice_y,room_width,h,dx,rf_slice_y,1,1,c_white,1);draw_surface_part_ext(src,0,rf_slice_y,room_width,h,dx-room_width,rf_slice_y,1,1,c_white,1);draw_surface_part_ext(src,0,rf_slice_y,room_width,h,dx+room_width,rf_slice_y,1,1,c_white,1);
 }
 draw_set_color(c_black);draw_set_alpha(.18*opacity);for(rf_slice_y=0;rf_slice_y<room_height;rf_slice_y+=4)draw_line(0,rf_slice_y,room_width,rf_slice_y);draw_set_color(c_white);draw_set_alpha(1);
}return 0;
`;
export function stylePass(t,index){return `rf_style_fx(rf_src,rf_dst,'${t.effect}',rf_strength,rf_fxOpacity,rf_fxAngle,rf_fxZoom,rf_fxFlash,rf_fxRetention,${n(t.fxSlices,11)},${n(t.fxStutter,2)},${n(t.start)},${index},${color(t)});`;}
function color(t){const hex=/^#[a-f\d]{6}$/i.test(t.color||'')?t.color.slice(1):'ffffff';return `make_color_rgb(${parseInt(hex.slice(0,2),16)},${parseInt(hex.slice(2,4),16)},${parseInt(hex.slice(4),16)})`;}
export const postCameraPass=`if rf_cam_enabled && rf_cam_post {rf_src=rf_camera_post(rf_src,rf_dst);rf_dst=rf_fx_a;if rf_src=rf_fx_a rf_dst=rf_fx_b;}`;
