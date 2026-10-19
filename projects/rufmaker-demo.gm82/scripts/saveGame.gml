{
    var f,list,i;
    //open save data
    f = file_text_open_write(global.saving_directory+"SaveData"+string(global.savenum));
    //create the save data list
    list = ds_list_create();
    //saving game data to the list
    ds_list_add(list,global.death);
    ds_list_add(list,global.timestep);
    ds_list_add(list,room);
    ds_list_add(list,global.reverse);
    ds_list_add(list,player.xprevious);
    ds_list_add(list,player.yprevious);
    ds_list_add(list,player.image_xscale);
    ds_list_add(list,global.xysz);
    ds_list_add(list,global.prac);
    //write the encrypted string to save file
    file_text_write_string(f,dataEncrypt(ds_list_write(list)));
    //destroy the list
    ds_list_destroy(list);
    //close save data
    file_text_close(f);
}
