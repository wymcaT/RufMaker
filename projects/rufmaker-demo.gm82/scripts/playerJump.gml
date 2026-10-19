if(keyboard_check_pressed(global.jumpbutton) && !keyboard_check_pressed(ord('P'))){
    //check whether we can first jump
    isFirstJump = place_meeting(x,y+yflag,block) || onPlatform ||(place_meeting(x,y+yflag,obj_wall));
    //first jump
    if(isFirstJump){
        FMODSoundPlay(sndJump);
        vspeed = -yflag*jump[1];
        //playerVJump
        if(!global.enable_jump_cancel){
            alarm[0] = 1;
        }
        //go to second jump
        curJumps = 2;
    }
    //second jump, (third jump...)
    else{
        if(curJumps == 1 && !isFirstJump){
            curJumps += 1;
        }
        if((curJumps<=maxJumps)){
            //if fall down and jump, make it a second jump
            FMODSoundPlay(sndDJump);
            vspeed = -yflag*jump[curJumps];
            //playerVJump
            if(!global.enable_jump_cancel){
                alarm[0] = 1;
            }
            //next jump
            curJumps += 1;
            sprite_index = sprPlayerJump;
        }
        //infinite jump
        else if(infJump || place_meeting(x,y+yflag,objWater)){
            FMODSoundPlay(sndDJump);
            //this will set the jumping speed to the last jump
            vspeed = -yflag*jump[maxJumps];
            //playerVJump
            if(!global.enable_jump_cancel){
                alarm[0] = 1;
            }
            curJumps = maxJumps+1;
        }
    }
}
