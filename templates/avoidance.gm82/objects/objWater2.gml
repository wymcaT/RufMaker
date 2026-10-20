#define Create_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
// this is the water that gives double jump when jump out
#define Collision_player
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
with(player){
    curJumps = 2;
    if(vspeed > 2){
        vspeed = 2;
    }
}
