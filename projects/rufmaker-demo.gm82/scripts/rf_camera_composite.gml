// The engine's surface1 has already been rendered through the camera.
d3d_set_projection_ortho(0,0,room_width,room_height,0);
draw_surface(argument0,argument1,argument2);rf_camera_apply();return 0;
