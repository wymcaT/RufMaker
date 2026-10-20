#define Draw_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
if xt=0{
  U=place_meeting(x,y-1,block)
  D=place_meeting(x,y+1,block)
  L=place_meeting(x-1,y,block)
  R=place_meeting(x+1,y,block)
  if (L && R && !U && !D) or LR{
    draw_background_part_ext(Alltiles,0,32,32,32,x,y,1,1,image_blend,image_alpha)
  }
  else if !L && !R && U && D{
    draw_background_part_ext(Alltiles,32,0,32,32,x,y,1,1,image_blend,image_alpha)
  }
  else{
    draw_background_part_ext(Alltiles,32,32,32,32,x,y,1,1,image_blend,image_alpha)
  }
}
