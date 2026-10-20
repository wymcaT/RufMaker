///initializing music at the beginning of the game
globalvar BGM_Ad,sndDeath,sndDJump,sndJump,sndShoot;
BGM_Ad = FMODSoundAdd('Data/Music/BGM.ogg',0,0);
sndDeath = FMODSoundAdd('Data/Music/sndDeath.wav',0,0);
sndDJump = FMODSoundAdd('Data/Music/sndDJump.wav',0,0);
sndJump = FMODSoundAdd('Data/Music/sndJump.wav',0,0);
sndShoot = FMODSoundAdd('Data/Music/sndShoot.wav',0,0);

FMODSoundSetMaxVolume(BGM_Ad,1)
FMODSoundSetMaxVolume(sndDeath,1)
FMODSoundSetMaxVolume(sndDJump,1)
FMODSoundSetMaxVolume(sndJump,1)
FMODSoundSetMaxVolume(sndShoot,1)
