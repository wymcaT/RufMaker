#define Step_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
//this object is created by script 'createShadow'
image_alpha -= alpha_speed;
image_xscale -= scale_speed;
image_yscale = image_xscale;
if(image_alpha<0 || image_xscale<0){
    instance_destroy();
}
