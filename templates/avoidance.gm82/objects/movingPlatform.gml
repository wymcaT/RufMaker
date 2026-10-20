#define Create_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
//*********  DO NOT use 2 as moving speed  *********//
//*********       use 2.0001 instead       *********//

//two modes:
//
// 1. normal mode
//      creation code:
//          hspeed = horizontal speed;
//          vspeed = vertical speed;
// 2. touch mode
//      the platform starts moving when player touches it
//      creation code:
//          touch = true;
//          hsp = horizontal speed;
//          vsp = vertical speed;
#define Step_2
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/

//锟斤拷锟斤拷锟斤拷锟教掞拷锟剿曉傦拷苽锟斤拷v锟斤拷锟紺锟斤拷锟絒锟斤拷锟紾锟斤拷膫锟狡傦拷锟狡傦拷锟教愝掞拷
//锟斤拷锟斤拷锟斤拷葌锟斤拷锟斤拷苽锟斤拷锟斤拷锟竭傦拷锟斤拷
if (!place_free(x+hspeed,y)) {
    hspeed=-hspeed;
}
if (!place_free(x,y+vspeed+yspeed)) {
    if (vspeed!=0) {
        yspeed=-vspeed;
        vspeed=0;
    }
    else {
        vspeed=-yspeed;
        yspeed=0;
    }
}
y+=yspeed;
if(collision_line(x-2,y+8-25*player.yflag,x+33,y+8-25*player.yflag,player,0,1) and
    collision_line(x-2,y+8-10*player.yflag,x+33,y+8-10*player.yflag,player,0,1)) {
    with(player){
        if(place_free(x+other.hspeed,y)) x+=other.hspeed;
        if(place_free(x,y+other.vspeed+other.yspeed)) y+=(other.vspeed+other.yspeed);
    }
}
if (vspeed<0) {
    yspeed=vspeed;
    vspeed=0;
}
if (yspeed>0) {
    vspeed=yspeed;
    yspeed=0;
}
#define Collision_player
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
if(touch){
    hspeed = hsp;
    vspeed = vsp;
    touch = false;
}
