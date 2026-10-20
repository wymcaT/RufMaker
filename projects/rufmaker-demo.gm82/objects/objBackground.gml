#define Create_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
global.surface1=surface_create(800,608)
global.surface2=surface_create(800,608)

P1yp[0]=0
for(i=0;i<40;i+=1){
    P1yp[i+1]=irandom_range(round(i*608/40),round((i+1)*608/40))
}
P1yp[41]=608
for(i=0;i<=40;i+=1){
    P1T[i]=irandom_range(1,20)
    P1xd[i]=random_range(-50,50)
}
#define Step_2
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
surface_set_target(global.surface1)
draw_clear_alpha(c_black,0)
if Part=0{
    //draw_set_blend_mode(bm_add)
    draw_set_blend_mode_ext(bm_one,bm_inv_src_alpha)
}
if Part=1{
    //draw_set_blend_mode_ext(bm_one,bm_inv_src_alpha)
    //draw_set_blend_mode(bm_add)
}
#define Draw_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
draw_set_blend_mode(bm_normal)
with(player)draw_self()
if Part=0{
    rf_fx_reset_target()
    draw_clear(c_black)
    draw_set_color(make_color_rgb(125,227,121))
    draw_rectangle(-100,-100,899,707,0)
    draw_set_color(make_color_rgb(82,177,54))
    with(block){
        draw_rectangle(x,y,x+31,y+31,0)
    }

    rf_camera_composite(global.surface1,0,0)
    if P0on>0{
        surface_set_target(global.surface2)
        draw_set_color(make_color_rgb(32,32,32))
        draw_rectangle(-100,-100,899,707,0)
        draw_set_color(make_color_rgb(118,214,145))
        with(block){
            draw_rectangle(x,y,x+31,y+31,0)
        }
        draw_set_color(make_color_rgb(96,96,96))
        with(block){
            draw_rectangle(x+2,y+2,x+29,y+29,0)
            if place_meeting(x-1,y,block) draw_rectangle(x,y+2,x+1,y+29,0)
            if place_meeting(x+1,y,block) draw_rectangle(x+30,y+2,x+31,y+29,0)
            if place_meeting(x,y-1,block) draw_rectangle(x+2,y,x+29,y+1,0)
            if place_meeting(x,y+1,block) draw_rectangle(x+2,y+30,x+29,y+31,0)
        }
        with(player)draw_self()
        rf_fx_reset_target()
        draw_set_color(c_white)
        len0+=(200-len0)/10
        sc0+=((P0on==2)-sc0)/10
        draw_primitive_begin_texture(pr_trianglefan,surface_get_texture(global.surface2))
        draw_vertex_texture(399.5,303.5,0.5,0.5)
        for(i=0;i<61;i+=1){
            m=400+lengthdir_x(len0+45*sc0,i*6)
            n=304+lengthdir_y(len0+45*sc0,i*6)
            draw_vertex_texture(m-0.5,n-0.5,m/800,n/608)
        }
        draw_primitive_end()
        dir0+=(20+75*(P0on==2)-dir0)/10
        draw_primitive_begin_texture(pr_trianglestrip,surface_get_texture(global.surface2))
        //draw_vertex_texture(399.5,303.5,0.5,0.5)
        for(i=0;i<21;i+=1){
            m=400+lengthdir_x(600,(i-10)*dir0/10)
            n=304+lengthdir_y(600,(i-10)*dir0/10)
            draw_vertex_texture(m-0.5,n-0.5,m/800,n/608)
            m=400+lengthdir_x(len0+50-10*sc0,(i-10)*dir0/10)
            n=304+lengthdir_y(len0+50-10*sc0,(i-10)*dir0/10)
            draw_vertex_texture(m-0.5,n-0.5,m/800,n/608)
        }
        draw_primitive_end()
        draw_primitive_begin_texture(pr_trianglestrip,surface_get_texture(global.surface2))
        //draw_vertex_texture(399.5,303.5,0.5,0.5)
        for(i=0;i<21;i+=1){
            m=400+lengthdir_x(600,(i-10)*dir0/10+180)
            n=304+lengthdir_y(600,(i-10)*dir0/10+180)
            draw_vertex_texture(m-0.5,n-0.5,m/800,n/608)
            m=400+lengthdir_x(len0+50-10*sc0,(i-10)*dir0/10+180)
            n=304+lengthdir_y(len0+50-10*sc0,(i-10)*dir0/10+180)
            draw_vertex_texture(m-0.5,n-0.5,m/800,n/608)
        }
        draw_primitive_end()
    }
}
if Part=1{
    with(objBullet2){
        draw_sprite_ext(sprite_index,0,x,y,image_xscale,image_yscale,0,make_color_rgb(22,179,144),1)
    }
    with(objBullet2){
        draw_sprite_ext(sprite_index,0,x,y,image_xscale-4/30,image_yscale-4/30,0,0,1)
    }
    if P1on{
        surface_set_target(global.surface2)
        draw_set_color(make_color_rgb(16,16,16))
        draw_rectangle(-100,-100,899,707,0)
        draw_set_color(make_color_rgb(224,224,224))
        with(block){
            draw_rectangle(x,y,x+31,y+31,0)
        }
        draw_set_color(make_color_rgb(64,64,64))
        with(block){
            draw_rectangle(x+2,y+2,x+29,y+29,0)
            if place_meeting(x-1,y,block) draw_rectangle(x,y+2,x+1,y+29,0)
            if place_meeting(x+1,y,block) draw_rectangle(x+30,y+2,x+31,y+29,0)
            if place_meeting(x,y-1,block) draw_rectangle(x+2,y,x+29,y+1,0)
            if place_meeting(x,y+1,block) draw_rectangle(x+2,y+30,x+29,y+31,0)
        }
        with(player)draw_self()
        with(objBullet2){
            draw_sprite_ext(sprite_index,0,x,y,image_xscale,image_yscale,0,c_white,1)
        }
        with(objBullet2){
            draw_sprite_ext(sprite_index,0,x,y,image_xscale-4/30,image_yscale-4/30,0,0,1)
        }
    }
    rf_fx_reset_target()
    draw_set_color(make_color_rgb(32,32,32))
    draw_rectangle(-100,-100,899,707,0)
    draw_set_color(make_color_rgb(118,214,145))
    with(block){
        draw_rectangle(x,y,x+31,y+31,0)
    }
    draw_set_color(make_color_rgb(96,96,96))
    with(block){
        draw_rectangle(x+2,y+2,x+29,y+29,0)
        if place_meeting(x-1,y,block) draw_rectangle(x,y+2,x+1,y+29,0)
        if place_meeting(x+1,y,block) draw_rectangle(x+30,y+2,x+31,y+29,0)
        if place_meeting(x,y-1,block) draw_rectangle(x+2,y,x+29,y+1,0)
        if place_meeting(x,y+1,block) draw_rectangle(x+2,y+30,x+29,y+31,0)
    }
    rf_camera_composite(global.surface1,0,0)
    if P1on{
        P1Tim+=1
        for(i=0;i<=40;i+=1){
            if P1Tim=P1T[i]{
                draw_surface_part(global.surface2,0,P1yp[i],800,P1yp[i+1]-P1yp[i],P1xd[i],P1yp[i])
            }
            if P1Tim=P1T[i]+1{
                draw_surface_part(global.surface2,0,P1yp[i],800,P1yp[i+1]-P1yp[i],P1xd[i]*0.3,P1yp[i])
            }
            if P1Tim>P1T[i]+1{
                draw_surface_part(global.surface2,0,P1yp[i],800,P1yp[i+1]-P1yp[i],0,P1yp[i])
            }
        }
/*    P1yp[0]=0
        for(i=0;i<40;i+=1){
            P1yp[i+1]=irandom_range(round(i*608/40),round((i+1)*608/40))
        }
        P1yp[41]=608
        for(i=0;i<=40;i+=1){
            P1T[i]=irandom(20)
            P1xd[i]=random_range(-50,50)
        }
        */
    }
}
if Part=2{
    Psc2-=Psc2/10
    Psc2_2-=Psc2_2/10
    Psc2_3+=(1-Psc2_3)/10
    surface_set_target(global.surface2)
    draw_set_color(merge_color(make_color_rgb(32,32,32),c_white,Psc2_2*0.4))
    draw_rectangle(-100,-100,899,707,0)
    draw_set_color(c_white)
    draw_set_alpha(0.05)
    with(objEffect){
        spe-=spe/2
        spe2+=(spe2+0.01)/4
        len+=spe-spe2
        if len<0{
            instance_destroy()
            //break;
        }
        draw_rectangle(x-len,0,x-1+len,707,0)
    }
    draw_set_alpha(1)
    draw_set_color(make_color_rgb(196,0,0))
    i=0
    with(block){
        draw_rectangle(x-other.i,y-other.i,x+31+other.i,y+31+other.i,0)
    }
    i=-round(Psc2*4)
    draw_set_color(merge_color(c_black,c_red,Psc2/3))
    with(block){
        draw_rectangle(x+2-other.i,y+2-other.i,x+29+other.i,y+29+other.i,0)
        if place_meeting(x-1,y,block) draw_rectangle(x,y+2-other.i,x+1-other.i,y+29+other.i,0)
        if place_meeting(x+1,y,block) draw_rectangle(x+30+other.i,y+2-other.i,x+31,y+29+other.i,0)
        if place_meeting(x,y-1,block) draw_rectangle(x+2-other.i,y,x+29+other.i,y+1-other.i,0)
        if place_meeting(x,y+1,block) draw_rectangle(x+2-other.i,y+30+other.i,x+29+other.i,y+31,0)
    }
    rf_fx_reset_target()
    draw_surface_ext(global.surface2,400-400*(1.3-Psc2_3*0.3),304-304*(1.3-Psc2_3*0.3),1.3-Psc2_3*0.3,1.3-Psc2_3*0.3,0,c_white,1)
    draw_surface_ext(global.surface1,400-400*(1.3-Psc2_3*0.3),304-304*(1.3-Psc2_3*0.3),1.3-Psc2_3*0.3,1.3-Psc2_3*0.3,0,c_white,1)
    Draw_Clc(0.8-Psc2_3*0.8,c_red)
}
