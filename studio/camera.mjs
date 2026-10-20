import {stateCode} from './motion.mjs';
export const cameraApplyGml=`// Apply a world-space camera to the current render target.
var cx,cy,z,a;cx=room_width/2;cy=room_height/2;z=1;a=0;
if instance_exists(objRufController) {if objRufController.rf_fx_rendering && objRufController.rf_cam_enabled {cx=objRufController.rf_cam_x;cy=objRufController.rf_cam_y;z=objRufController.rf_cam_zoom;a=objRufController.rf_cam_angle;}}
d3d_set_projection_ortho(cx-room_width/z/2,cy-room_height/z/2,room_width/z,room_height/z,a);
return 0;
`;
export const cameraCompositeGml=`// The engine's surface1 has already been rendered through the camera.
d3d_set_projection_ortho(0,0,room_width,room_height,0);
draw_surface(argument0,argument1,argument2);rf_camera_apply();return 0;
`;
export function cameraUpdate(state) {
  return `rf_cam_enabled=0;rf_cam_x=room_width/2;rf_cam_y=room_height/2;rf_cam_zoom=1;rf_cam_angle=0;
${state.tracks.filter(t=>t.enabled!==false&&t.kind==='camera').map(t=>`if rf_frame>=${t.start} && rf_frame<=${t.end} {${stateCode(t,'rf_frame')} rf_cam_enabled=1;rf_cam_x=rf_x;rf_cam_y=rf_y;rf_cam_zoom=max(.05,min(20,rf_cameraZoom));rf_cam_angle=rf_angle;}`).join('\n')}`;
}
