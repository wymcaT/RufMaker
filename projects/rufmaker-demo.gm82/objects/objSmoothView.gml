#define Create_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
/// this object can smooth the view changing
//  in the room view settings, set the object following to this object
//  and the Hbor should be 400, Vbor should be 304.
//
// DO NOT PUT 'camera' IN THE ROOM
//

view_object = id;
view_hborder[0] = 400;
view_vborder[0] = 304;
x=player.x;
y=player.y;
#define Step_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
if(instance_exists(player)){
    //set this object as the view object
    view_object[0]=id;
    //follow player smoothly
    hspeed=(player.x-x)/16;
    vspeed=(player.y-y)/12;
    if(player.x<=x+2 && player.x >= x-2){
        hspeed=0;
    }
    if(player.y<=y+2 && player.y >= y-2){
        vspeed=0;
    }
}
else{
    vspeed=0;
    hspeed=0;
}
