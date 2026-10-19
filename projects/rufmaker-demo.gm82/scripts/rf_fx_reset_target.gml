// Restore the timeline's full-frame target after the engine composites its own surfaces.
if instance_exists(objRufController) {
 if objRufController.rf_fx_active && surface_exists(objRufController.rf_fx_scene) {surface_set_target(objRufController.rf_fx_scene);return 0;}
}
surface_reset_target();return 0;
