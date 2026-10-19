#define Draw_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
//creating a screenflash
draw_set_color(c_white);
draw_set_alpha(1-transparency);
draw_rectangle(view_xview[0],view_yview[0],view_xview[0]+view_wview[0],view_yview[0]+view_hview[0],0);
draw_set_alpha(1);
transparency += 1/time;
if transparency >= 1{
    instance_destroy();
}
