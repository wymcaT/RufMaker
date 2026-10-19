///Draw_Ring(x,y,R,DirStart,Dird,precision):using the color setted before
var i,dir;
draw_primitive_begin(pr_trianglefan)
draw_vertex(argument0,argument1)
for(i=0;i<=argument5;i+=1){
    dir=i*argument4/argument5
    draw_vertex(argument0+lengthdir_x(argument2,argument3+dir),argument1+lengthdir_y(argument2,argument3+dir))
}
draw_primitive_end()
