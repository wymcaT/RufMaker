#define Create_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
rf_frame=0; rf_target=0; rf_preview=0; rf_ready=0; rf_music=FMODSoundPlay(BGM_Ad,1); global.BGMHandler=rf_music;
if file_exists(working_directory+'\Data\rufmaker_preview.ini') { ini_open(working_directory+'\Data\rufmaker_preview.ini'); rf_target=ini_read_real('preview','frame',0); ini_close(); rf_preview=1; }
rf_frame=max(0,min(15716,round(rf_target))); rf_target=rf_frame;
FMODInstanceSetPosition(rf_music,(rf_frame/50*1000)/FMODSoundGetLength(BGM_Ad)); FMODInstanceSetPaused(rf_music,0);
var rf_i,rf_j,rf_u,rf_dir,rf_inst,rf_len,rf_phase,rf_fraction,rf_x,rf_y,rf_angle,rf_scale,rf_alpha,rf_speed,rf_spread,rf_count,rf_life,rf_x1,rf_y1,rf_x2,rf_y2,rf_length,rf_rollSpeed,rf_spriteAngle,rf_strength,rf_fxOpacity,rf_fxAngle,rf_fxRetention;
rf_custom_create();
rf_fx_scene=-1;rf_fx_a=-1;rf_fx_b=-1;rf_fx_active=0;rf_fx_rendering=0;rf_fx_w=0;rf_fx_h=0;
for(rf_j=0;rf_j<1;rf_j+=1){rf_fx_history[rf_j]=-1;rf_fx_history_frame[rf_j]=-1000000;}

rf_rod0_count=0;for(rf_j=0;rf_j<300;rf_j+=1) rf_rod0[rf_j]=noone;
if (rf_frame>=512 && rf_frame<=1186) {
if (rf_frame<=512) {rf_x=432;}
else if (rf_frame<841) {rf_u=rf_ease('linear',(rf_frame-512)/329,3,0.3);rf_x=432+(400-432)*rf_u;}
else if (rf_frame<1142) {rf_u=rf_ease('linear',(rf_frame-841)/301,3,0.3);rf_x=400+(400-400)*rf_u;}
else {rf_x=400;}
if (rf_frame<=512) {rf_y=352;}
else if (rf_frame<832) {rf_u=rf_ease('linear',(rf_frame-512)/320,3,0.3);rf_y=352+(304-352)*rf_u;}
else if (rf_frame<1142) {rf_u=rf_ease('linear',(rf_frame-832)/310,3,0.3);rf_y=304+(304-304)*rf_u;}
else {rf_y=304;}
if (rf_frame<=842) {rf_angle=0;}
else if (rf_frame<1142) {rf_u=rf_ease('linear',(rf_frame-842)/300,3,0.3);rf_angle=0+(0-0)*rf_u;}
else {rf_angle=0;}
if (rf_frame<=848) {rf_scale=1;}
else if (rf_frame<1093) {rf_u=rf_ease('linear',(rf_frame-848)/245,3,0.3);rf_scale=1+(2-1)*rf_u;}
else if (rf_frame<1142) {rf_u=rf_ease('linear',(rf_frame-1093)/49,3,0.3);rf_scale=2+(1-2)*rf_u;}
else {rf_scale=1;}
if (rf_frame<=512) {rf_alpha=1;}
else if (rf_frame<831) {rf_u=rf_ease('linear',(rf_frame-512)/319,3,0.3);rf_alpha=1+(1-1)*rf_u;}
else if (rf_frame<960) {rf_u=rf_ease('linear',(rf_frame-831)/129,3,0.3);rf_alpha=1+(1-1)*rf_u;}
else if (rf_frame<1142) {rf_u=rf_ease('linear',(rf_frame-960)/182,3,0.3);rf_alpha=1+(1-1)*rf_u;}
else {rf_alpha=1;}
rf_speed=3;
rf_spread=30;
rf_count=11;
rf_life=160;
if (rf_frame<=512) {rf_x1=312;}
else if (rf_frame<624) {rf_u=rf_ease('linear',(rf_frame-512)/112,3,0.3);rf_x1=312+(168-312)*rf_u;}
else if (rf_frame<659) {rf_u=rf_ease('linear',(rf_frame-624)/35,3,0.3);rf_x1=168+(777-168)*rf_u;}
else if (rf_frame<819) {rf_u=rf_ease('linear',(rf_frame-659)/160,3,0.3);rf_x1=777+(280-777)*rf_u;}
else {rf_x1=280;}
rf_y1=15;
rf_x2=773;
rf_y2=591;
rf_length=240;
rf_rollSpeed=2.08;
rf_spriteAngle=0;
rf_strength=6;
rf_fxOpacity=1;
rf_fxAngle=0;
rf_fxRetention=0.5;
rf_angle+=-0.04*max(0,rf_frame-512);
rf_len=point_distance(rf_x1,rf_y1,rf_x2,rf_y2);rf_phase=0;rf_count=max(1,min(300,round(rf_count)));
if rf_len>.001 rf_phase=(rf_frame-512)*rf_rollSpeed/rf_len;
for(rf_j=0;rf_j<max(rf_count,rf_rod0_count);rf_j+=1) {
if rf_j>=rf_count {if instance_exists(rf_rod0[rf_j]) with (rf_rod0[rf_j]) instance_destroy();rf_rod0[rf_j]=noone;} else {
if !instance_exists(rf_rod0[rf_j]) {rf_rod0[rf_j]=instance_create(0,0,objRufBullet);rf_rod0[rf_j].rf_fixed=1;}rf_inst=rf_rod0[rf_j];
rf_fraction=rf_j/rf_count+rf_phase;rf_fraction-=floor(rf_fraction);
rf_inst.x=rf_x1+(rf_x2-rf_x1)*rf_fraction;rf_inst.y=rf_y1+(rf_y2-rf_y1)*rf_fraction;rf_inst.image_angle=rf_spriteAngle+0;rf_inst.sprite_index=sprCherryAzure;rf_inst.image_blend=make_color_rgb(255,109,145);rf_inst.image_xscale=rf_scale;rf_inst.image_yscale=rf_scale;rf_inst.image_alpha=rf_alpha;rf_inst.rf_trail=1;
}} rf_rod0_count=rf_count; } else if rf_rod0_count>0 {for(rf_j=0;rf_j<rf_rod0_count;rf_j+=1) {if instance_exists(rf_rod0[rf_j]) with (rf_rod0[rf_j]) instance_destroy();rf_rod0[rf_j]=noone;}rf_rod0_count=0;}
#define Step_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
rf_frame+=1;
if rf_frame>15716 { room_goto(rEnd); exit; }
var rf_i,rf_j,rf_u,rf_dir,rf_inst,rf_len,rf_phase,rf_fraction,rf_x,rf_y,rf_angle,rf_scale,rf_alpha,rf_speed,rf_spread,rf_count,rf_life,rf_x1,rf_y1,rf_x2,rf_y2,rf_length,rf_rollSpeed,rf_spriteAngle,rf_strength,rf_fxOpacity,rf_fxAngle,rf_fxRetention;
rf_custom_step();
if (rf_frame>=512 && rf_frame<=1186) {
if (rf_frame<=512) {rf_x=432;}
else if (rf_frame<841) {rf_u=rf_ease('linear',(rf_frame-512)/329,3,0.3);rf_x=432+(400-432)*rf_u;}
else if (rf_frame<1142) {rf_u=rf_ease('linear',(rf_frame-841)/301,3,0.3);rf_x=400+(400-400)*rf_u;}
else {rf_x=400;}
if (rf_frame<=512) {rf_y=352;}
else if (rf_frame<832) {rf_u=rf_ease('linear',(rf_frame-512)/320,3,0.3);rf_y=352+(304-352)*rf_u;}
else if (rf_frame<1142) {rf_u=rf_ease('linear',(rf_frame-832)/310,3,0.3);rf_y=304+(304-304)*rf_u;}
else {rf_y=304;}
if (rf_frame<=842) {rf_angle=0;}
else if (rf_frame<1142) {rf_u=rf_ease('linear',(rf_frame-842)/300,3,0.3);rf_angle=0+(0-0)*rf_u;}
else {rf_angle=0;}
if (rf_frame<=848) {rf_scale=1;}
else if (rf_frame<1093) {rf_u=rf_ease('linear',(rf_frame-848)/245,3,0.3);rf_scale=1+(2-1)*rf_u;}
else if (rf_frame<1142) {rf_u=rf_ease('linear',(rf_frame-1093)/49,3,0.3);rf_scale=2+(1-2)*rf_u;}
else {rf_scale=1;}
if (rf_frame<=512) {rf_alpha=1;}
else if (rf_frame<831) {rf_u=rf_ease('linear',(rf_frame-512)/319,3,0.3);rf_alpha=1+(1-1)*rf_u;}
else if (rf_frame<960) {rf_u=rf_ease('linear',(rf_frame-831)/129,3,0.3);rf_alpha=1+(1-1)*rf_u;}
else if (rf_frame<1142) {rf_u=rf_ease('linear',(rf_frame-960)/182,3,0.3);rf_alpha=1+(1-1)*rf_u;}
else {rf_alpha=1;}
rf_speed=3;
rf_spread=30;
rf_count=11;
rf_life=160;
if (rf_frame<=512) {rf_x1=312;}
else if (rf_frame<624) {rf_u=rf_ease('linear',(rf_frame-512)/112,3,0.3);rf_x1=312+(168-312)*rf_u;}
else if (rf_frame<659) {rf_u=rf_ease('linear',(rf_frame-624)/35,3,0.3);rf_x1=168+(777-168)*rf_u;}
else if (rf_frame<819) {rf_u=rf_ease('linear',(rf_frame-659)/160,3,0.3);rf_x1=777+(280-777)*rf_u;}
else {rf_x1=280;}
rf_y1=15;
rf_x2=773;
rf_y2=591;
rf_length=240;
rf_rollSpeed=2.08;
rf_spriteAngle=0;
rf_strength=6;
rf_fxOpacity=1;
rf_fxAngle=0;
rf_fxRetention=0.5;
rf_angle+=-0.04*max(0,rf_frame-512);
rf_len=point_distance(rf_x1,rf_y1,rf_x2,rf_y2);rf_phase=0;rf_count=max(1,min(300,round(rf_count)));
if rf_len>.001 rf_phase=(rf_frame-512)*rf_rollSpeed/rf_len;
for(rf_j=0;rf_j<max(rf_count,rf_rod0_count);rf_j+=1) {
if rf_j>=rf_count {if instance_exists(rf_rod0[rf_j]) with (rf_rod0[rf_j]) instance_destroy();rf_rod0[rf_j]=noone;} else {
if !instance_exists(rf_rod0[rf_j]) {rf_rod0[rf_j]=instance_create(0,0,objRufBullet);rf_rod0[rf_j].rf_fixed=1;}rf_inst=rf_rod0[rf_j];
rf_fraction=rf_j/rf_count+rf_phase;rf_fraction-=floor(rf_fraction);
rf_inst.x=rf_x1+(rf_x2-rf_x1)*rf_fraction;rf_inst.y=rf_y1+(rf_y2-rf_y1)*rf_fraction;rf_inst.image_angle=rf_spriteAngle+0;rf_inst.sprite_index=sprCherryAzure;rf_inst.image_blend=make_color_rgb(255,109,145);rf_inst.image_xscale=rf_scale;rf_inst.image_yscale=rf_scale;rf_inst.image_alpha=rf_alpha;rf_inst.rf_trail=1;
}} rf_rod0_count=rf_count; } else if rf_rod0_count>0 {for(rf_j=0;rf_j<rf_rod0_count;rf_j+=1) {if instance_exists(rf_rod0[rf_j]) with (rf_rod0[rf_j]) instance_destroy();rf_rod0[rf_j]=noone;}rf_rod0_count=0;}
if rf_preview && !rf_ready { rf_ready=1; ini_open(working_directory+'\Data\rufmaker_preview_status.ini'); ini_write_real('preview','frame',rf_target); ini_write_real('preview','current_frame',rf_frame); ini_write_real('preview','fps',room_speed); ini_write_string('preview','room',room_get_name(room)); ini_write_real('preview','bullets',instance_number(objRufBullet)); ini_write_real('preview','players',instance_number(player)); ini_close(); }
FMODUpdate();
#define Draw_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
var rf_sample,rf_len,rf_phase,rf_fraction,rf_i,rf_j,rf_u,rf_dir,rf_inst,rf_x,rf_y,rf_angle,rf_scale,rf_alpha,rf_speed,rf_spread,rf_count,rf_life,rf_x1,rf_y1,rf_x2,rf_y2,rf_length,rf_rollSpeed,rf_spriteAngle,rf_strength,rf_fxOpacity,rf_fxAngle,rf_fxRetention;
rf_custom_draw();
if rf_frame>=512 && rf_frame<=1186 {for(rf_sample=floor(rf_frame/3)*3;rf_sample>=max(512,rf_frame-18+1);rf_sample-=3) {
if (rf_sample<=512) {rf_x=432;}
else if (rf_sample<841) {rf_u=rf_ease('linear',(rf_sample-512)/329,3,0.3);rf_x=432+(400-432)*rf_u;}
else if (rf_sample<1142) {rf_u=rf_ease('linear',(rf_sample-841)/301,3,0.3);rf_x=400+(400-400)*rf_u;}
else {rf_x=400;}
if (rf_sample<=512) {rf_y=352;}
else if (rf_sample<832) {rf_u=rf_ease('linear',(rf_sample-512)/320,3,0.3);rf_y=352+(304-352)*rf_u;}
else if (rf_sample<1142) {rf_u=rf_ease('linear',(rf_sample-832)/310,3,0.3);rf_y=304+(304-304)*rf_u;}
else {rf_y=304;}
if (rf_sample<=842) {rf_angle=0;}
else if (rf_sample<1142) {rf_u=rf_ease('linear',(rf_sample-842)/300,3,0.3);rf_angle=0+(0-0)*rf_u;}
else {rf_angle=0;}
if (rf_sample<=848) {rf_scale=1;}
else if (rf_sample<1093) {rf_u=rf_ease('linear',(rf_sample-848)/245,3,0.3);rf_scale=1+(2-1)*rf_u;}
else if (rf_sample<1142) {rf_u=rf_ease('linear',(rf_sample-1093)/49,3,0.3);rf_scale=2+(1-2)*rf_u;}
else {rf_scale=1;}
if (rf_sample<=512) {rf_alpha=1;}
else if (rf_sample<831) {rf_u=rf_ease('linear',(rf_sample-512)/319,3,0.3);rf_alpha=1+(1-1)*rf_u;}
else if (rf_sample<960) {rf_u=rf_ease('linear',(rf_sample-831)/129,3,0.3);rf_alpha=1+(1-1)*rf_u;}
else if (rf_sample<1142) {rf_u=rf_ease('linear',(rf_sample-960)/182,3,0.3);rf_alpha=1+(1-1)*rf_u;}
else {rf_alpha=1;}
rf_speed=3;
rf_spread=30;
rf_count=11;
rf_life=160;
if (rf_sample<=512) {rf_x1=312;}
else if (rf_sample<624) {rf_u=rf_ease('linear',(rf_sample-512)/112,3,0.3);rf_x1=312+(168-312)*rf_u;}
else if (rf_sample<659) {rf_u=rf_ease('linear',(rf_sample-624)/35,3,0.3);rf_x1=168+(777-168)*rf_u;}
else if (rf_sample<819) {rf_u=rf_ease('linear',(rf_sample-659)/160,3,0.3);rf_x1=777+(280-777)*rf_u;}
else {rf_x1=280;}
rf_y1=15;
rf_x2=773;
rf_y2=591;
rf_length=240;
rf_rollSpeed=2.08;
rf_spriteAngle=0;
rf_strength=6;
rf_fxOpacity=1;
rf_fxAngle=0;
rf_fxRetention=0.5;
rf_angle+=-0.04*max(0,rf_sample-512);
rf_len=point_distance(rf_x1,rf_y1,rf_x2,rf_y2);rf_count=max(1,min(300,round(rf_count)));rf_phase=0;
if rf_len>.001 rf_phase=(rf_sample-512)*rf_rollSpeed/rf_len;
for(rf_j=0;rf_j<rf_count;rf_j+=1){
rf_fraction=rf_j/rf_count+rf_phase;rf_fraction-=floor(rf_fraction);
rf_trail_draw(sprCherryAzure,rf_x1+(rf_x2-rf_x1)*rf_fraction,rf_y1+(rf_y2-rf_y1)*rf_fraction,rf_scale,rf_spriteAngle+0,rf_alpha,make_color_rgb(255,109,145),rf_frame-rf_sample,'fade',18,0.55,0.035,0.035,0,0);
}
}}

if (rf_frame>=509 && rf_frame<=764) { rf_x=385;
if (rf_frame<=514) {rf_y=-85;}
else if (rf_frame<759) {rf_u=rf_ease('circle-in',(rf_frame-514)/245,3,0.3);rf_y=-85+(682--85)*rf_u;}
else {rf_y=682;}
rf_angle=0;
rf_scale=1;
rf_alpha=1;
rf_speed=3;
rf_spread=30;
rf_count=12;
rf_life=160;
rf_x1=280;
rf_y1=304;
rf_x2=520;
rf_y2=304;
rf_length=240;
rf_rollSpeed=2;
rf_spriteAngle=0;
rf_strength=6;
rf_fxOpacity=1;
rf_fxAngle=0;
rf_fxRetention=0.5;
rf_angle+=3*max(0,rf_frame-509); draw_sprite_ext(sprGAMEOVER,0,rf_x,rf_y,rf_scale,rf_scale,rf_angle,make_color_rgb(255,109,145),rf_alpha); }
#define Destroy_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
if FMODInstanceIsPlaying(rf_music) FMODInstanceStop(rf_music);
if surface_exists(rf_fx_scene) surface_free(rf_fx_scene);if surface_exists(rf_fx_a) surface_free(rf_fx_a);if surface_exists(rf_fx_b) surface_free(rf_fx_b);var rf_k;for(rf_k=0;rf_k<0;rf_k+=1)if surface_exists(rf_fx_history[rf_k]) surface_free(rf_fx_history[rf_k]);
#define Other_4
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
room_speed=50;
