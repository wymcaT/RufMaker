#define Create_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
global.BGMHandler = FMODSoundPlay(BGM_Ad,1);
FMODInstanceSetVolume(global.BGMHandler,1);
start_pos = 0//480//0//960//480//480//0
bgm_length = FMODSoundGetLength(BGM_Ad);
//bpm = 750//99.95;
//beat = 4;
//globalvar SPB;
//SPB = 3000 / bpm;
realtime=start_pos//round(start_pos*3000/(beat * bpm))

/*timeline_index = tl_Ad;
timeline_speed = (beat * bpm) / 3000;

timeline_running = 1;
timeline_position = start_pos;

start_pos = start_pos / timeline_speed;
*/
m_time = start_pos / 50 * 1000;
_st = start_pos / 50;
FMODInstanceSetPosition(global.BGMHandler,_st/bgm_length);
FMODInstanceSetPaused(global.BGMHandler,0);

if FMODInstanceIsPlaying(global.BGMHandler)
    m_time += 1000 / room_speed;

if abs(FMODInstanceGetPosition(global.BGMHandler)*bgm_length-m_time)>75
    FMODInstanceSetPosition(global.BGMHandler,m_time/bgm_length);

FMODUpdate();

/*
00:00 原曲
00:11 Hyper banquet
00:20 Frenchcore
00:24 Hardtek
00:29 Drumstep
00:34 Speedcore 1
01:24 Midtempo
01:43 Synthwave
02:02 Trap
02:09 Glitch Hop
02:21 House
02:36 Melodic Progressive House
02:51 Glitchtune
02:58 Chiptune
03:05 Hard Bass
03:19 Hardstyle
03:25 Psychedelic Trance
03:31 Dubstep
03:38 Colour Bass
03:43 Lo-Fi Hiphop
03:55 Kawaii Future Bass
04:16 又Speedcore
#define Destroy_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
if FMODInstanceIsPlaying(global.BGMHandler)
    FMODInstanceStop(global.BGMHandler);
#define Step_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
realtime+=1
//if the player died, stop the timeline to prevent spoilerino
if(instance_exists(player)=0){
    timeline_running=0
}
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
// 刷锟斤拷Debug锟矫憋拷锟斤拷
//if !debug_mode exit;
_curStep = 'curStep: ' + string(round(timeline_position));
_isPlaying = ' IsPlaying: ' + string(FMODInstanceIsPlaying(global.BGMHandler));
_instNum = ' nums: ' + string(instance_number(all));
_playerX = ' X: ';
_playerY = ' Y: ';
if instance_exists(player)
{
    _playerX += string(player.x);
    _playerY += string(player.y);
}
else
{
    _playerX += string(0);
    _playerY += string(0);
}
_curFps = ' FPS: ' + string(fps) + '/50';
#define Step_1
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
///Main code
if realtime<480{
    objBackground.Part=0
    switch(realtime){
        case Return_realtime(0,100,0):
            for(i=0;i<18;i+=1){inst_spdir_form(400,304,objBullet1,0,i*20,1)}
        break;

        case Return_realtime(0,100,8):
            with(objBullet1){if form=1{spe=30}}
            for(i=0;i<12;i+=1){with(inst_spdir_form(400,304,objBullet1,0,i*30,2))lr=1;with(inst_spdir_form(400,304,objBullet1,0,i*30,2))lr=-1}
        break;
        case Return_realtime(0,100,11):
            with(objBullet1){if form=2{on=1}}
        break;

        case Return_realtime(0,100,16):
            for(i=0;i<18;i+=1){inst_spdir_form(400,608,objBullet1,0,i*10+5,3)}
        break;
        case Return_realtime(0,100,17):
            for(i=0;i<17;i+=1){inst_spdir_form(400,608,objBullet1,0,i*10+10,4)}
        break;

        case Return_realtime(0,100,24):
            with(objBullet1){if form=3 or form=4{instance_destroy();with(inst_sca_form(x,y,objBullet2,1.2,2))mask_index=maskNothing;inst_sca_form(x,y,objBullet2,0.5,3)}}
            for(i=0;i<16;i+=1){with(inst_spdir_form(400,0,objBullet1,0,i*22.5,5))lr=1;with(inst_spdir_form(400,0,objBullet1,0,i*22.5,5))lr=-1}
        break;
        case Return_realtime(0,100,27):
            with(objBullet1)if form=5 on=1
        break;

        case Return_realtime(0,100,32):
            for(i=0;i<18;i+=1){with(inst_spdir_form(0,608,objBullet1,0,-i*5+87.5,6))denum=other.i}
            for(i=0;i<18;i+=1){with(inst_spdir_form(800,608,objBullet1,0,i*5+92.5,6))denum=other.i}
            inst_creator(1)
        break;

        case Return_realtime(0,100,40):
            px=player.x
            py=player.y
            for(i=0;i<22;i+=1){inst_spdir_form(200,608,objBullet1,i,point_direction(200,608,player.x,player.y),7)}
            for(i=0;i<22;i+=1){inst_spdir_form(600,608,objBullet1,i,point_direction(600,608,player.x,player.y),7)}
        break;
        case Return_realtime(0,100,43):
            with(objBullet1){if form=7 with(inst_sca_form(x,y,objBullet2,1.2,2))mask_index=maskNothing;instance_destroy()}
            dir=point_direction(200,608,px,py)
            for(i=0;i<30;i+=1){
                with(inst_spdir_form(200+lengthdir_x(i*45,dir),608+lengthdir_y(i*45,dir),objBullet1,0,dir+180,8)){lr=1}
                with(inst_spdir_form(200+lengthdir_x(i*45,dir),608+lengthdir_y(i*45,dir),objBullet1,0,dir+180,8)){lr=-1}
            }
            dir=point_direction(600,608,px,py)
            for(i=0;i<30;i+=1){
                with(inst_spdir_form(600+lengthdir_x(i*45,dir),608+lengthdir_y(i*45,dir),objBullet1,0,dir+180,8)){lr=1}
                with(inst_spdir_form(600+lengthdir_x(i*45,dir),608+lengthdir_y(i*45,dir),objBullet1,0,dir+180,8)){lr=-1}
            }
        break;

        case Return_realtime(0,100,48):
            with(objBullet1){if form=8 with(inst_sca_form(x,y,objBullet2,1.2,2))mask_index=maskNothing;instance_destroy()}
            with(objBullet2){if form=5 speed=15}
            for(i=0;i<36;i+=1){inst_spdir_form(400,608,objBullet1,0,i*5+2.5,9)}
            with(objCreator)instance_destroy()
            inst_creator(2)
        break;
        case Return_realtime(0,100,49):
            for(i=0;i<4;i+=1){
                for(j=0;j<16;j+=1){
                    inst_spdir_form(400,608,objBullet1,j*8,i*15+15,10)
                    inst_spdir_form(400,608,objBullet1,j*8,-i*15+165,10)
                }
            }
        break;

        case Return_realtime(0,100,56):
            with(objBullet1)mask_index=maskNothing
            with(objBullet2)mask_index=maskNothing
            with(objCreator)maskNt=1
            with(objBackground)P0on=1
        break;
        case Return_realtime(0,100,60):
            with(objBackground)P0on=2
        break;
    }
}
else if realtime<961{
    objBackground.Part=1
    switch(realtime){
        case Return_realtime(480,100,0):{
            with(objBullet1)instance_destroy()
            with(objBullet2)instance_destroy()
            with(objCreator)instance_destroy()
            lr=choose(-1,1)
            inst=inst_xycreator(400+lengthdir_x(150,210),304+lengthdir_y(150,210),3)
            inst.lr=lr
        }break;
        //case Return_realtime(480,100,3):{
        //}break;

        case Return_realtime(480,100,8):{
            lr=-lr
            inst=inst_xycreator(400+lengthdir_x(150,330),304+lengthdir_y(150,330),3)
            inst.lr=lr
        }break;
        //case Return_realtime(480,100,11):{
        //}break;

        case Return_realtime(480,100,16):{
            lr=-lr
            inst=inst_xycreator(400+lengthdir_x(150,90),304+lengthdir_y(150,90),3)
            inst.lr=lr
        }break;
        //case Return_realtime(480,100,19):{
        //}break;

        case Return_realtime(480,100,24):{
            with(objCreator){
                if form=3 stt=1
            }
            inst_xycreator(400,304,4)
        }break;
        case Return_realtime(480,100,28):{
            with(objCreator){
                if form=3 out=1
            }
        }break;
        case Return_realtime(480,100,30):{
            with(objCreator){
                if form=3 out=2
            }
        }break;
        case Return_realtime(480,100,32):{
            with(objCreator){
                if form=3 spread=1
            }
            inst_creator(5)
        }break;
        case Return_realtime(480,100,40):{
        }break;
        case Return_realtime(480,100,48):{
            objBackground.P1on=1
            inst_creator(6)
        }break;
        case Return_realtime(480,100,56):{
            dir=point_direction(400,0,player.x,player.y)
            for(i=0;i<24;i+=1){
                inst=inst_form(400,0,objBullet2,8)
                inst.direction=i*15+dir
                inst.speed=20
                inst.image_xscale=2.5
                inst.image_yscale=2.5
            }
        }break;
        case Return_realtime(480,100,58):{
            dir=point_direction(400,0,player.x,player.y)
            for(i=0;i<24;i+=1){
                inst=inst_form(400,0,objBullet2,8)
                inst.direction=i*15+dir
                inst.speed=20
                inst.image_xscale=2.5
                inst.image_yscale=2.5
            }
        }break;
        case Return_realtime(480,100,60):{
            dir=point_direction(400,0,player.x,player.y)
            for(i=0;i<24;i+=1){
                inst=inst_form(400,0,objBullet2,8)
                inst.direction=i*15+dir
                inst.speed=20
                inst.image_xscale=2.5
                inst.image_yscale=2.5
            }
        }break;
        case Return_realtime(480,100,62):{
            dir=point_direction(400,0,player.x,player.y)
            for(i=0;i<24;i+=1){
                inst=inst_form(400,0,objBullet2,8)
                inst.direction=i*15+dir
                inst.speed=20
                inst.image_xscale=2.5
                inst.image_yscale=2.5
            }
        }break;

    }
    switch(realtime){
        case Return_realtime(480,100,0):case Return_realtime(480,100,2):case Return_realtime(480,100,4):case Return_realtime(480,100,6):
        case Return_realtime(480,100,8):case Return_realtime(480,100,10):case Return_realtime(480,100,12):case Return_realtime(480,100,14):
        case Return_realtime(480,100,16):case Return_realtime(480,100,18):case Return_realtime(480,100,20):case Return_realtime(480,100,22):{
        }break;
    }
}
else if realtime<1202{
    objBackground.Part=2
    switch(realtime){
        case Return_realtime(961,200,0):
            with(objBullet2)instance_destroy()
            with(objCreator)instance_destroy()
        case Return_realtime(961,200,4):
        case Return_realtime(961,200,8):
        case Return_realtime(961,200,12):
        case Return_realtime(961,200,16):
        case Return_realtime(961,200,18):
        case Return_realtime(961,200,20):
        case Return_realtime(961,200,24):
        case Return_realtime(961,200,32):
        case Return_realtime(961,200,35):
        case Return_realtime(961,200,36):
        case Return_realtime(961,200,38):
        case Return_realtime(961,200,40):
        case Return_realtime(961,200,44):
        case Return_realtime(961,200,48):
        case Return_realtime(961,200,50):
        case Return_realtime(961,200,52):
        case Return_realtime(961,200,54):
        case Return_realtime(961,200,56):
        {
            objBackground.sc2=1
            repeat(2){
                inst=instance_create(random(800),0,objEffect)
                inst.spe=50
            }
        }
    }
    switch(realtime){
        //one vertical
        case Return_realtime(961,200,0):
        case Return_realtime(961,200,4):
        case Return_realtime(961,200,8):
        case Return_realtime(961,200,12):
        {
            i=(player.x+150+random(500))mod 800
            with(instance_create(i,0,objDrawer))form=1
            for(j=0;j<2;j+=1){
                inst=inst_spdir_form(i,random(304)+204,objBullet3,20,90,1)
                inst.image_blend=c_red
                inst.image_xscale=0.4
                inst.image_yscale=0.4
            }
        }

        break;
        //two vertical
        case Return_realtime(961,200,24):
        case Return_realtime(961,200,40):
        case Return_realtime(961,200,44):
        {
            k=random(500)
            i=(player.x+150+k)mod 800
            with(instance_create(i,0,objDrawer))form=1
            for(j=0;j<2;j+=1){
                inst=inst_spdir_form(i,random(304)+204,objBullet3,20,90,1)
                inst.image_blend=c_red
                inst.image_xscale=0.4
                inst.image_yscale=0.4
            }
            i=(player.x+650-k)mod 800
            with(instance_create(i,0,objDrawer))form=1
            for(j=0;j<2;j+=1){
                inst=inst_spdir_form(i,random(304)+204,objBullet3,20,90,1)
                inst.image_blend=c_red
                inst.image_xscale=0.4
                inst.image_yscale=0.4
            }
        }

        break;
        //one horizon
        case Return_realtime(961,200,16):
        case Return_realtime(961,200,18):
        case Return_realtime(961,200,20):
        case Return_realtime(961,200,22):
        case Return_realtime(961,200,32):
        case Return_realtime(961,200,35):
        case Return_realtime(961,200,36):
        case Return_realtime(961,200,38):
        {
            i=50+random(200)
            with(instance_create(0,i,objDrawer))form=2
            for(j=0;j<1;j+=1){
                inst=inst_spdir_form(random(400)+200,i,objBullet3,20,0,1)
                inst.image_blend=c_red
                inst.image_xscale=0.4
                inst.image_yscale=0.4
            }
            for(j=0;j<1;j+=1){
                inst=inst_spdir_form(random(400)+200,i,objBullet3,20,180,1)
                inst.image_blend=c_red
                inst.image_xscale=0.4
                inst.image_yscale=0.4
            }
        }

        break;
        //one ranking horizon
        case Return_realtime(961,200,48):
        case Return_realtime(961,200,50):
        case Return_realtime(961,200,52):
        case Return_realtime(961,200,54):
        case Return_realtime(961,200,56):
        {
            i=583-(realtime-Return_realtime(961,200,48))*4
            with(instance_create(0,i,objDrawer))form=2
            for(j=0;j<1;j+=1){
                inst=inst_spdir_form(random(400)+200,i,objBullet3,20,0,1)
                inst.image_blend=c_red
                inst.image_xscale=0.4
                inst.image_yscale=0.4
            }
            for(j=0;j<1;j+=1){
                inst=inst_spdir_form(random(400)+200,i,objBullet3,20,180,1)
                inst.image_blend=c_red
                inst.image_xscale=0.4
                inst.image_yscale=0.4
            }
        }
        break;
        case Return_realtime(961,200,62):
        {
            objBackground.P2on=1
        }
        break
        //case Return_realtime(961,200,64):
    }
    switch(realtime){
        case Return_realtime(961,200,28):{
            with(objBullet3){
                on=1
            }
            objBackground.sc2_2=1
        }
    }
}
else{
    objBackground.Part=2
    event_user(15)
}
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
// 锟斤拷锟斤拷同锟斤拷
if(FMODInstanceIsPlaying(global.BGMHandler)){
    m_time += 1000/room_speed;
}

if(abs(FMODInstanceGetPosition(global.BGMHandler)*bgm_length-m_time)>75){
    FMODInstanceSetPosition(global.BGMHandler,m_time/bgm_length);
}

m_fps = round(ffps);

if abs(room_speed - m_fps) > 1 && m_fps != 0
{
    FMODInstanceSetPitch(global.BGMHandler,1 / (room_speed / m_fps));
}
else
{
    FMODInstanceSetPitch(global.BGMHandler,1);
    FMODEffectSetParamValue(effect,0,1);
    if !instance_exists(objDeathSound)
        FMODInstanceSetVolume(global.BGMHandler,100/100);
}
#define Other_4
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
if room == rEntrance instance_destroy();
if room == rEnd instance_destroy();
#define Other_25
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
///temporary code
#define Draw_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
// 锟斤拷锟斤拷Debug锟斤拷息
//if !debug_mode exit;
draw_set_color(c_red);
draw_set_font(fntDebug);
draw_set_alpha(1);
draw_text(64,64,_curStep+_isPlaying+_instNum+_playerX+_playerY+_curFps);
draw_text(64,128,realtime)
if keyboard_check_pressed(vk_space){
    record[num]=string(realtime)
    num+=1
}
for(i=0;i<num;i+=1){
    draw_text(64,192+i*32,record[i])
}
draw_set_color(c_white);
draw_set_font(font12);
