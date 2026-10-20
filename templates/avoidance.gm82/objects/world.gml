#define Create_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
///initializing
setGlobals();
if !directory_exists(global.saving_directory){
    directory_create(global.saving_directory)
}
global.savenum = 1;
global.difficulty = 3;
global.room_caption_def = "";
global.death = 0;
global.timestep = 0;

controls_init();
cleanmem_init(0);
#define Step_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
///room captions
if(room != rInit && room != rMenu && room != rSelectStage && room != rTitle && room != rOptions){
    // death captions
    room_caption = global.room_caption_def + " Death["+string(global.death)+"]";
    // time captions
    time=global.timestep div (2*room_speed);
    hour=time div 3600;
    atime=time mod 3600;
    muni=atime div 60;
    sec=atime mod 60;
    msec=global.timestep-time*2*room_speed;
    if round(msec)>=10 {
        room_caption += " Time["+string(hour)+":"
        +string(muni)+":"+string(sec)+"."+string(round(msec))+"]";
    }
    else{
        room_caption += " Time["+string(hour)+":"
        +string(muni)+":"+string(sec)+".0"+string(round(msec))+"]";
    }
}
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
///calculate time
if(room != rInit && room != rMenu && room != rTitle && room != rSelectStage && room != rOptions){
    if(pauseon = false and fps!=0 and !global.clear){
        global.timestep += 2;
    }
}
//cleanmem
memory_count+=1;
if memory_count=10{
    //cleanmem();
    memory_count=0;
}
//set message position
message_position(window_get_x()+240,window_get_y()+261);
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
///cheat preventing
if(global.cheat_detection && room != rInit && room != rMenu && room != rSelectStage && room != rTitle && room != rOptions){
    check_timer += 1;
    if(check_timer == 10){
        date_time_previous = date_time;
        date_time = date_current_time();
        time_span = date_second_span(date_time_previous,date_time);
        if(round(50*time_span*fps/room_speed) > 10 && date_time_previous != 0){
            cheat_time += 5*time_span*fps/room_speed;
        }
        else{
            cheat_time = 0;
        }
        check_timer = 0;
    }

    if(cheat_time > 100){
        show_message("Cheat Detected.");
        game_end();
    }
}
#define Other_2
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
// FMOD锟斤拷始锟斤拷
FMODinit(100,0);
// 锟斤拷锟绞癸拷锟斤拷锟斤拷锟斤拷旨锟斤拷锟?
// 锟斤拷FMODSetPassword锟斤拷锟?'锟侥筹拷锟姐当时锟斤拷锟斤拷锟斤拷锟斤拷旨锟斤拷锟斤拷锟斤拷搿?
// 锟斤拷锟矫伙拷锟绞癸拷镁锟斤拷锟斤拷锟斤拷税锟?
FMODSetPassword('');
// 锟斤拷锟斤拷锟斤拷锟斤拷
music_init();
#define Other_3
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
///unload the dlls
FMODfree();
cleanmem_init(1);
#define Other_4
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
///room captions

// set the game speed, 1 second = 50 steps
room_speed=50;
// set room captions
room_caption=global.game_title;
if(global.lite=0){
    if(room != rInit && room != rMenu && room != rSelectStage && room != rTitle && room!=rOptions){
    room_caption += " Mode["
    if(global.difficulty == 0){
        room_caption += "Medium";
    }
    else if(global.difficulty == 1){
        room_caption += "Hard";
    }
    else if(global.difficulty == 2){
        room_caption += "VeryHard";
    }
    else if(global.difficulty == 3){
        room_caption += "Impossible";
    }
    room_caption += "]";
    }
}
global.room_caption_def = room_caption;
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
///others
//play music
//musicFunctions();

//anti-cheat initialize
date_time_previous = date_current_time();
date_time = date_current_time();

//enable the use of views
if(!view_enabled){
    view_enabled = true;
    view_visible[0] = true;
    if(instance_exists(playerStart)){
        view_xview[0] = floor(playerStart.x/800)*800;
        view_yview[0] = floor(playerStart.y/608)*608;
    }
    if(instance_exists(player)){
        view_xview[0] = floor(player.x/800)*800;
        view_yview[0] = floor(player.y/608)*608;
    }
    view_wview[0] = 800;
    view_hview[0] = 608;
    view_wport[0] = 800;
    view_hport[0] = 608;
}
#define Draw_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
if (pauseon == true) {
    draw_background(pauseback,view_xview[0],view_yview[0]);
    draw_set_alpha(0.5);
    draw_set_color(c_black);
    draw_rectangle(0,0,room_width,room_height,0);
    draw_set_alpha(1);
    draw_set_color(c_white);
    draw_set_blend_mode(bm_normal);
    draw_sprite(sprPause,-1,view_xview[0]+400,view_yview[0]+304);
    draw_set_font(font2);
    draw_set_halign(fa_left);
    draw_set_valign(fa_top);
    draw_text(38,500,"Time  : "+string(hour)+":"+string(muni)+":"+
        string(sec)+"#Death : "+string(global.death));
 }
#define KeyPress_8
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
// return to a room when press backspace

/*
global.frozen = false;
roomTo = rStage01

if(room != rInit && room != rMenu && room != rSelectStage && room != rTitle && room!=rOptions){
    room_goto(roomTo);
    with(player){
        instance_destroy();
    }
}
#define KeyPress_13
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
//Alt+Enter - switch between screen modes
if(keyboard_check(vk_alt) && global.enable_fullscreen){
    if(window_get_fullscreen()){
        window_set_fullscreen(false);
    }
    else{
        window_set_fullscreen(true);
    }
}
#define KeyPress_27
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
///quit
saveDeathTime();
game_end();
#define KeyPress_82
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
//if the game is not paused...
if(pauseon == false){
    // sound function
    if FMODInstanceIsPlaying(global.BGMHandler)
        FMODInstanceStop(global.BGMHandler);
    sound_stop(sndDeath);
    // restart function
    if(room != rInit && room != rTitle && room != rMenu && room != rSelectStage && room!=rOptions){
        //make up for the frame that player presses 'R'
        global.timestep += 2;
        saveDeathTime();
        with(player){
            instance_destroy();
        }
        with(bow){
            instance_destroy();
        }
        //unfreeze
        global.frozen = 0;
        global.frozen2 = 0;
        loadGame();
    }
}
surface_reset_target()
surface_free(global.surface1)
surface_free(global.surface2)
#define KeyPress_115
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
//Alt+F4 - end game
if(keyboard_check(vk_alt) == true){
    game_end();
}
//F4 - switch between screen modes
else{
    if(global.enable_fullscreen){
        if(window_get_fullscreen()){
            window_set_fullscreen(false);
        }
        else{
            window_set_fullscreen(true);
        }
    }
}
