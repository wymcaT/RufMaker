/// music_load(musicFilePath)
{
    //load the music
    var _musicFile, _handler;
    _musicFile = FMODSoundAdd('Data\Music\' + argument0,0,0);
    _handler = FMODSoundPlay(_musicFile,1);
    variable_global_set('__' + string(_musicFile) + '_handler',_handler);
    return _musicFile;
}
