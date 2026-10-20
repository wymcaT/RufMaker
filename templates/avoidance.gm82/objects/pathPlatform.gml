#define Create_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
//  move : whether the object will continue moving if player died (prevent spoilers)
//
// two modes:
//
// 1. normal mode
//      creation code:
//          path_start(pth,spd,enda,abso);
//
// 2. touch mode
//      the platform starts moving when player touches it
//      creation code:
//          touch = true;
//          pth = path name;
//          spd = path speed;
//          enda = endaction;
//          scl = scale;
#define Step_2
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
if(collision_line(x,y+8-25*player.yflag,x+31,y+8-25*player.yflag,player,0,1) and
    collision_line(x,y+8-10*player.yflag,x+31,y+8-10*player.yflag,player,0,1) and path_position!=path_positionprevious){
    player.x+=(path_get_x(path_index,path_position)-path_get_x(path_index,path_positionprevious))*path_scale;
    player.y+=(path_get_y(path_index,path_position)-path_get_y(path_index,path_positionprevious))*path_scale-lengthdir_y(path_speed,direction);
}
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
//end the path and continue moving if player died
if !instance_exists(player){
    if(move){
        speed = path_speed;
        path_end();
        move = false;
    }
}
#define Collision_player
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
if(touch){
    path_start(pth,spd+0.0001,enda,0);
    if(scl!=0){
        path_scale = scl;
    }
    touch=0;
}
