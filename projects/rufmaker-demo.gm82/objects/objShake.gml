#define Create_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
{
    //get the x,y value of current view
    xview=view_xview[0];
    yview=view_yview[0];
}
#define Alarm_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
{
    //restoring original views
    view_xview[0]=xview;
    view_yview[0]=yview;
    instance_destroy();
}
#define Step_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
{
    //shaking views
    view_xview[0]=xview+random_range(-shakeX,shakeX);
    view_yview[0]=yview+random_range(-shakeY,shakeY);
}
