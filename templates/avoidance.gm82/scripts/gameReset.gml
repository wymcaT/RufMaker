// reset the game, re-initialize the global variables
for(i=1;i<=global.boss_number;i+=1){
    global.boss[i] = 0;
}
for(i=1;i<=global.item_number;i+=1){
    global.item[i] = 0;
}
global.frozen = 0;
global.frozen2 = 0;
global.savenum = 1;
global.reverse = 0;
global.clear = 0;
global.difficulty = 1;
room_goto(rInit);
