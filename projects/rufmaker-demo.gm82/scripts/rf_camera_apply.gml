// Apply a world-space camera to the current render target.
var cx,cy,z,a;cx=room_width/2;cy=room_height/2;z=1;a=0;
if instance_exists(objRufController) {if objRufController.rf_fx_rendering && objRufController.rf_cam_enabled {cx=objRufController.rf_cam_x;cy=objRufController.rf_cam_y;z=objRufController.rf_cam_zoom;a=objRufController.rf_cam_angle;}}
d3d_set_projection_ortho(cx-room_width/z/2,cy-room_height/z/2,room_width/z,room_height/z,a);
return 0;
