#define Create_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
alp = 0;
#define Step_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
if alp < 1
{
    alp += 1 / 20;
}
#define Draw_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
draw_set_alpha(alp);
draw_set_color(c_black);
draw_rectangle(0,0,800,608,0);
draw_set_alpha(1);
draw_set_color(c_white);
draw_sprite_ext(sprite_index,0,x,y,1,1,0,c_white,alp);
