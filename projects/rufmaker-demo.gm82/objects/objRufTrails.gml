#define Create_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
rf_owner=noone;
#define Step_2
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
if !instance_exists(rf_owner) {instance_destroy();exit;}
depth=rf_owner.depth+1;with(objRufBullet) other.depth=max(other.depth,depth+1);
#define Draw_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
if !instance_exists(rf_owner) {instance_destroy();exit;}
with(objRufBullet) rf_bullet_trails();
with(rf_owner) {
var rf_sample,rf_len,rf_phase,rf_fraction,rf_px,rf_py,rf_seg,rf_mix,rf_i,rf_j,rf_u,rf_dir,rf_inst,rf_x,rf_y,rf_angle,rf_scale,rf_alpha,rf_speed,rf_spread,rf_count,rf_life,rf_x1,rf_y1,rf_x2,rf_y2,rf_length,rf_rollSpeed,rf_rainbowDensity,rf_rainbowOffset,rf_rainbowSpeed,rf_spriteAngle,rf_strength,rf_fxOpacity,rf_fxAngle,rf_fxRetention,rf_radius,rf_orientation,rf_sides,rf_cameraZoom;
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
rf_rainbowDensity=1;
rf_rainbowOffset=0;
rf_rainbowSpeed=0.25;
rf_spriteAngle=0;
rf_strength=6;
rf_fxOpacity=1;
rf_fxAngle=0;
rf_fxRetention=0.5;
rf_radius=120;
rf_orientation=0;
rf_sides=5;
rf_cameraZoom=1;
rf_angle+=-0.04*max(0,rf_sample-512);

rf_len=point_distance(rf_x1,rf_y1,rf_x2,rf_y2);rf_phase=0;rf_count=max(1,min(300,round(rf_count)));
if rf_len>.001 rf_phase=(rf_sample-512)*rf_rollSpeed/rf_len;
for(rf_j=0;rf_j<rf_count;rf_j+=1){
rf_fraction=rf_j/rf_count+rf_phase;rf_fraction-=floor(rf_fraction);
rf_px=rf_x1+(rf_x2-rf_x1)*rf_fraction;rf_py=rf_y1+(rf_y2-rf_y1)*rf_fraction;
rf_trail_draw(sprCherryAzure,rf_px,rf_py,rf_scale,rf_spriteAngle+0,rf_alpha,make_color_rgb(255,109,145),rf_frame-rf_sample,'fade',18,0.55,0.035,0.035,0,0,0);
}
}}

if rf_frame>=407 && rf_frame<=907 {for(rf_sample=floor(rf_frame/3)*3;rf_sample>=max(407,rf_frame-18+1);rf_sample-=3) {
if (rf_sample<=469) {rf_x=151;}
else if (rf_sample<533) {rf_u=rf_ease('linear',(rf_sample-469)/64,3,0.3);rf_x=151+(586.0697674418604-151)*rf_u;}
else if (rf_sample<598) {rf_u=rf_ease('linear',(rf_sample-533)/65,3,0.3);rf_x=586.0697674418604+(391-586.0697674418604)*rf_u;}
else if (rf_sample<694) {rf_u=rf_ease('expsin',(rf_sample-598)/96,3,0.3);rf_x=391+(619-391)*rf_u;}
else if (rf_sample<818) {rf_u=rf_ease('linear',(rf_sample-694)/124,3,0.3);rf_x=619+(167-619)*rf_u;}
else {rf_x=167;}
rf_y=239;
rf_angle=-355.2351466993025;
rf_scale=1;
rf_alpha=1;
rf_speed=3;
rf_spread=30;
rf_count=14;
rf_life=160;
rf_x1=280;
rf_y1=304;
rf_x2=520;
rf_y2=304;
rf_length=432.62917146211953;
rf_rollSpeed=2;
rf_rainbowDensity=1;
rf_rainbowOffset=0;
rf_rainbowSpeed=0.25;
rf_spriteAngle=0;
rf_strength=6;
rf_fxOpacity=1;
rf_fxAngle=0;
rf_fxRetention=0.5;
rf_radius=120;
rf_orientation=0;
rf_sides=5;
rf_cameraZoom=1;
rf_angle+=1*max(0,rf_sample-407);
rf_x1=rf_x-lengthdir_x(rf_length/2,rf_angle);rf_y1=rf_y-lengthdir_y(rf_length/2,rf_angle);rf_x2=rf_x+lengthdir_x(rf_length/2,rf_angle);rf_y2=rf_y+lengthdir_y(rf_length/2,rf_angle);
rf_len=point_distance(rf_x1,rf_y1,rf_x2,rf_y2);rf_phase=0;rf_count=max(1,min(300,round(rf_count)));

for(rf_j=0;rf_j<rf_count;rf_j+=1){
if rf_count=1 rf_fraction=.5;else rf_fraction=rf_j/(rf_count-1);
rf_px=rf_x1+(rf_x2-rf_x1)*rf_fraction;rf_py=rf_y1+(rf_y2-rf_y1)*rf_fraction;
rf_trail_draw(sprrainbowcherry,rf_px,rf_py,rf_scale,rf_spriteAngle+0,rf_alpha,make_color_rgb(255,255,255),rf_frame-rf_sample,'shrink',18,0.55,0.035,0.035,0.4,((sin((rf_sample*301+rf_j+1)*12.9898)*43758.5453-floor(sin((rf_sample*301+rf_j+1)*12.9898)*43758.5453))*360),rf_rainbow_index(abs(rf_fraction-.5)*(rf_count-1),rf_sample,rf_rainbowDensity,rf_rainbowOffset,rf_rainbowSpeed,407,1,0,32));
}
}}
}
