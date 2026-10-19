#define Collision_player
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
if (onCollide && room_exists(roomTo)) {
    transition_kind = kind;
    room_goto(roomTo);
    if (warpX!=0 || warpY!=0) {
        if (!instance_exists(player)) {
            instance_create(warpX, warpY, player);
        }
        if player.vspeed<0{
            player.vspeed=0;
        }
        //when warping vertically(warpY!=0)
        //the player's x coordinate shouldn't move
        //(as respect to the current view)
        //if you want to warp from (2100,608) in room1
        //to (500,0) in room2
        //the creation code should be:
        //roomTo = room2;
        //warpY = 0;
        //screens = -2;
        if(warpX!=0){
            player.x = warpX;
        }
        else{
            player.x += screens*800;
        }
        //similar as travelling vertically
        if(warpY!=0){
            player.y = warpY;
        }
        else{
            player.y += screens*608;
        }
    }
    else{
        with(player){
            instance_destroy();
        }
        with(bow){
            instance_destroy();
        }
    }
}
