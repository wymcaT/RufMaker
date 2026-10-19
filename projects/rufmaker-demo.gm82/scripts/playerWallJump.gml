//wall jump left
if distance_to_object(WalljumpL) < 2 && place_free(x,y+yflag) {
    vspeed=yflag*2;
    sprite_index=sprPlayerSliding;
    image_xscale=1;
    image_speed=0.5;
    if ((keyboard_check_pressed(global.rightbutton) && (!global.reverse)) || (keyboard_check_pressed(global.leftbutton) && (global.reverse))){
        if keyboard_check(global.jumpbutton){
            sprite_index=sprPlayerJump;
            vspeed = -yflag*9;
            hspeed = 15;
            FMODSoundPlay(sndWallJump);
        }
        else{
            hspeed = 3;
            sprite_index = sprPlayerFall;
        }
    }
}
//wall jump right
if distance_to_object(WalljumpR) = 1 && place_free(x,y+yflag) {
    vspeed=yflag*2;
    sprite_index=sprPlayerSliding;
    image_xscale=-1;
    image_speed=0.5;
    if ((keyboard_check_pressed(global.leftbutton) && (!global.reverse)) || (keyboard_check_pressed(global.rightbutton) && (global.reverse))){
        if keyboard_check(global.jumpbutton){
            sprite_index=sprPlayerJump;
            vspeed = -yflag*9;
            hspeed = -15;
            FMODSoundPlay(sndWallJump);
        }
        else{
            hspeed = -3;
            sprite_index = sprPlayerFall;
        }
    }
}
