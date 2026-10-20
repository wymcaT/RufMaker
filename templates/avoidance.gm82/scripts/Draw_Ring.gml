///Draw_Ring(x,y,R_inside,R_outside,precision):using the color setted before
var i,dir;
draw_primitive_begin(pr_trianglestrip)
for(i=0;i<=argument4;i+=1){
    dir=i*360/argument4
    draw_vertex(argument0+lengthdir_x(argument2,dir),argument1+lengthdir_y(argument2,dir))
    draw_vertex(argument0+lengthdir_x(argument3,dir),argument1+lengthdir_y(argument3,dir))
}
draw_primitive_end()
