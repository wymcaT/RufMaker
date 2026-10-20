#define Create_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
//锟斤拷論x锟絈锟饺忥拷艒韽滐拷锟斤拷锟絑锟絒锟絬锟絴锟紺锟斤拷锟絞
image_speed=0;
saveTimer = -1; //锟絑锟絒锟絬锟絕锟紺锟絵锟絒
if(global.difficulty > 1){
  instance_destroy();
}
#define Step_2
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
if saveTimer > 0{
    saveTimer -= 1;
}
if place_meeting(x,y,bullet) || (place_meeting(x,y,player) && keyboard_check_pressed(global.shootbutton)){
    if(saveTimer <= 0 && instance_exists(player) == true){
        saveTimer = 30;
        image_index = 1;
        image_speed = 0.017;
        saveGame();
    }
}
#define Other_7
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
image_speed=0;
image_index=0;
