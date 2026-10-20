#define Create_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
///Initializing
//global variables:
//
//  global.frozen       set to true to make player uncontrollable
//  global.frozen2      set to true to make player unmovable
//  global.reverse      0: normal kid ; 1: reverse kid
//
//whether can infinite jump or not
infJump = false;
//first jump speed
jump[1] = 8.5;
//second jump speed
jump[2] = 7;
//maximum jump times
//you should have variables such as jump[3] if you need 3 jumps or more
maxJumps = 2;
//current jump times
curJumps = 1;
//horizontal speed
maxSpeed = 3;
//gravity
grav = 0.4;
//maximum speed when falling
maxVspeed = 9;
//reverse gravity
if(global.reverse == 0){
    yflag = 1;
}
if(global.reverse == 1){
    yflag = -1;
    y -= 4;
    curJumps = 1;
}
image_yscale = yflag;
image_speed = 0.2;
//save game when playing for the 1st time
if(room != rSelectStage && room != rOptions){
    if(file_exists(global.saving_directory+"SaveData"+string(global.savenum)) == false){
      global.death = 0;
      global.timestep = 0;
      saveGame();
    }
}
#define Alarm_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
if(!keyboard_check(global.jumpbutton) && yflag*vspeed < 0){
    vspeed *= 0.45;
    alarm[0] = 0;
}
else{
    alarm[0] = 1;
}
#define Alarm_5
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
wudi=0
if(rgfdo=0){
image_alpha=1
}
#define Step_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
///Player movements
if(global.frozen2 == 0){
    playerMove();
    playerWallJump();
    playerMisc();
}
else{
    gravity = 0;
    image_speed = 0;
    speed = 0;
}
room_speed=50
#define Step_2
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
///Checking for Slopes
playerSlope();
#define Collision_block
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
///collision with blocks
//horizontal
if(place_free(x+hspeed,y) == false){
    if(hspeed <= 0){
        move_contact_solid(180,abs(hspeed));
    }
    if(hspeed > 0){
        move_contact_solid(0,abs(hspeed));
    }
    hspeed = 0;
}
//vertical
if(place_free(x,y+vspeed) == false){
    if(global.reverse){
        if(vspeed <= 0){
            move_contact_solid(90,abs(vspeed));
            curJumps=1;
        }
        if(vspeed > 0){
            move_contact_solid(270,abs(vspeed));
        }
    }
    else{
        if(vspeed <= 0){
            move_contact_solid(90,abs(vspeed));
        }
        if(vspeed > 0){
            move_contact_solid(270,abs(vspeed));
            curJumps=1;
        }
    }
    vspeed = 0;
    gravity = 0;
}
if (place_free(x+hspeed,y+vspeed) == false){
    hspeed=0;
}
#define Collision_platform
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
///collision with platforms
if(global.reverse == 0){
    if(y-vspeed/2 <= other.y){
        if(other.vspeed >= 0){
            y = other.y-9;
            vspeed = other.y-other.yprevious;
            curJumps=1;
        }
        onPlatform = 1;
        curJumps=1;
    }
}
else{
    if(y-vspeed/2 >= other.y+15){
        if(other.vspeed >= 0){
            y = other.y+24;
            vspeed = other.y-other.yprevious;
            curJumps=1;
        }
        onPlatform = 1;
        curJumps=1;
    }
}
#define Collision_playerKiller
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
event_user(0)
#define Other_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
if(place_meeting(x,y,roomChanger) == false){
    killPlayer();
}
#define Other_4
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
//creating bow
if !instance_exists(bow){
    var a;
    a = instance_create(x,y,bow);
    a.image_xscale = image_xscale;
    a.image_yscale = image_yscale;
}
//spin view
if(global.reverse == 1){
    view_enabled = true;
    view_angle[0] = 180;
}
else{
    view_angle[0] = 0;
}
#define Other_10
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
if !godmode killPlayer();
#define KeyPress_71
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
godmode=1
#define KeyPress_81
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
//kill yourself
killPlayer();
