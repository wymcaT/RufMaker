
//  make the player dead
//
//
{
    FMODSoundPlay(sndDeath);
    //create death effects
    instance_create(0,0,objDeathSound);
    instance_create(player.x,player.y,bloodEmitter);
    instance_create(view_xview+400,view_yview+300,GAMEOVER);
    //destroy the player and load game
    if(instance_exists(player)){
        with(player){
          instance_destroy();
        }
    }
    global.death += 1;
    saveDeathTime();
}
