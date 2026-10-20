#define Create_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
volume = FMODInstanceGetVolume(global.BGMHandler);
curVolume = volume;
#define Step_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
if FMODInstanceIsPlaying(global.BGMHandler)
{
    if curVolume > 0
    {
        curVolume -= volume / 50;
        FMODInstanceSetVolume(global.BGMHandler,curVolume);
    }
    else
    {
        FMODInstanceStop(global.BGMHandler);
    }
}
else
{
    instance_destroy();
}
