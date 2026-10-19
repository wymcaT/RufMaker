#define Create_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
//when using lite mode, load game instantly
if(global.lite_mode){
    global.savenum = 1;
    if(file_exists(global.saving_directory+"SaveData"+string(global.savenum))){
        loadGame();
    }
    else{
        global.death= 0;
        global.timestep = 0;
        global.difficulty = 1;
        room_goto(global.first_stage);
    }
}
#define Step_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
if(keyboard_check_pressed(global.jumpbutton) == true){
    global.difficulty = 0;
    if debug_mode
        room_goto(rAvoidance);
    else
    {
        if file_exists(global.saving_directory+'savedata1')
            loadGame()
        else
        room_goto(rEntrance);
    }
}
