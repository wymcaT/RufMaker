/// drawLife(x1,y1,x2,y2,curVal,maxVal,col1,col2,outline)
//
//  Draws a lifebar in a region with the given colors
//
//      x1,y1       1st corner of the rectangle, real
//      x2,y2       2nd corner of the rectangle, real
//      curVal      current value(current hp), real
//      maxVal      maximum value(maximum hp), real
//      col1        color when curVal=maxVal, real
//      col2        color when curVal=0, real
//      outline     true to draw the outline, bool
//
{
    var percentage;
    percentage = argument4/argument5;
    draw_set_alpha(percentage);
    draw_rectangle_color(argument0,argument1,argument0+(argument2-argument0)*percentage,argument3,argument6,argument6,argument6,argument6,0);
    draw_set_alpha(1-percentage);
    draw_rectangle_color(argument0,argument1,argument0+(argument2-argument0)*percentage,argument3,argument7,argument7,argument7,argument7,0);
    draw_set_alpha(1);
    if(argument8){
        draw_set_color(c_black)
        draw_rectangle(argument0,argument1,argument2,argument3,1)
    }
}
