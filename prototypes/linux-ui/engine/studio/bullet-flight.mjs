import {expressionCall,parameterCode} from './random-parameters.mjs';

export function bulletFlightGml(state){
  const cases=state.tracks.map((t,index)=>{
    if(t.kind!=='emitter'||!['aim','turn','expression'].includes(t.flightMode))return '';
    let code=t.flightMode==='aim'?'rf_dir=point_direction(x,y,rf_expression_player(0),rf_expression_player(1));':t.flightMode==='turn'?'rf_target=point_direction(x,y,rf_expression_player(0),rf_expression_player(1));rf_delta=(((rf_target-rf_dir) mod 360+540) mod 360)-180;rf_dir+=max(-rf_turn,min(rf_turn,rf_delta));':`rf_dir=${expressionCall('('+t.flightExpression+')+0*frame',t,'flight',null,{x:'x',y:'y',frame:'(rf_birth+rf_age)',age:'rf_age',direction:'rf_dir',speed:'rf_speed'})};`;
    return `case ${index}:if rf_age>rf_delay {${code}}break;`;
  }).join('\n');
  return `var rf_target,rf_delta,rf_slot;\nrf_age+=1;switch(rf_flight){\n${cases}\n}\nx+=lengthdir_x(rf_speed,rf_dir);y+=lengthdir_y(rf_speed,rf_dir);if rf_rotate image_angle=rf_sprite_angle+rf_dir;\nif rf_flight>=0 {rf_slot=rf_age mod 301;rf_hx[rf_slot]=x;rf_hy[rf_slot]=y;rf_ha[rf_slot]=image_angle;}\nreturn 0;\n`;
}
export function flightSpawn(t,index,history){
  const active=['aim','turn','expression'].includes(t.flightMode);
  return `rf_inst.rf_flight=${active?index:-1};rf_inst.rf_turn=${parameterCode(t,'turnRate',3,null,{frame:history?'rf_i':'rf_frame',x:'rf_x',y:'rf_y'})};rf_inst.rf_delay=${parameterCode(t,'homingDelay',0,null,{frame:history?'rf_i':'rf_frame',x:'rf_x',y:'rf_y'})};`+(active?`rf_inst.rf_hx[0]=rf_inst.x;rf_inst.rf_hy[0]=rf_inst.y;rf_inst.rf_ha[0]=rf_inst.image_angle;${history?'with(rf_inst) repeat(other.rf_frame-rf_birth) rf_bullet_advance();':''}`:'');
}
