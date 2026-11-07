import {gmlNumber as num} from './gml.mjs';
import {groupTransformCode} from './bullet-schema.mjs';

export function nativeObjectSchedule(state){
  const tracks=state.tracks,clips=[];
  for(const track of tracks){
    if(track.kind!=='native-instance'||track.enabled===false||track.templateOnly)continue;
    let start=Math.max(0,track.start),end=track.end;
    for(let current=track;current._parentIndex>=0;){
      current=tracks[current._parentIndex];
      if(current.enabled===false||current.templateOnly){end=-1;break;}
      start=Math.max(start,current.start);end=Math.min(end,current.end);
    }
    if(start<=end)clips.push({track,start,end});
  }
  if(!clips.length)return null;
  const code=['var rf_at,rf_new,rf_x,rf_y,rf_px,rf_py,rf_x1,rf_x2,rf_y1,rf_y2,rf_angle,rf_orientation,rf_scale,rf_alpha,rf_length,rf_radius;','rf_at=argument0;'];
  const init=['rf_native_object_frame=-1;'],initializers=[],cleanup=[];
  clips.forEach(({track,start,end},slot)=>{
    const id=`rf_native_ids[${slot}]`,first=num(start),last=num(end);
    init.push(`${id}=noone;`);
    if(!track.nativeKeepAlive){
      const remove=`if instance_exists(${id}) {with(${id}) instance_destroy();${id}=noone;}`;
      code.push(`if rf_at>${last} {${remove}}`);
      cleanup.push(remove);
    }
    code.push(`if rf_native_object_frame<${first} && rf_at>=${first}${track.nativeKeepAlive?'':` && rf_at<=${last}`} {
  rf_x=${num(track.x,400)};rf_y=${num(track.y,304)};`);
    if(track._parentIndex>=0)code.push(`rf_x1=0;rf_x2=0;rf_y1=0;rf_y2=0;rf_angle=0;rf_orientation=0;rf_scale=1;rf_alpha=1;rf_length=0;rf_radius=0;`,
      groupTransformCode(track,first));
    code.push(`  rf_new=instance_create(rf_x,rf_y,${track.nativeObject});${id}=rf_new;`);
    if(track.nativePath)code.push(`  if instance_exists(rf_new) with(rf_new) path_start(${track.nativePath},${num(track.nativePathSpeed,2)},${track.nativePathLoop?1:0},${track.nativePathAbsolute?1:0});`);
    if(track.code?.trim()){
      code.push(`  if instance_exists(rf_new) with(rf_new) rf_native_object_init(${slot});`);
      initializers.push(`case ${slot}: {
${track.code}
} break;`);
    }
    code.push('}');
  });
  code.push('rf_native_object_frame=max(rf_native_object_frame,rf_at);return 0;');
  return {first:Math.min(...clips.map(clip=>clip.start)),initialization:init.join('\n'),script:code.join('\n'),
    initializer:initializers.length?`switch(argument0){\n${initializers.join('\n')}\n}\nreturn 0;`:'',
    cleanup:cleanup.join('\n')};
}

export function guardPlainReplayPlayer(source){
  const marker='// RufMaker native object replay input guard';
  return source.split(/(?=^#define\s)/m).map(part=>{
    if(!/^#define (?:Step_|Collision_|Keyboard_|KeyPress_|KeyRelease_)/.test(part)||part.includes(marker))return part;
    let first=true;
    return part.replace(/\/\*"\/\*'\/\*\*\/\/\* YYD ACTION[\s\S]*?\*\//g,metadata=>{
      if(!first||!/action_id=603\b/.test(metadata))return metadata;
      first=false;
      return metadata+`\n${marker}\nif instance_exists(objRufController) {if objRufController.rf_warming_native exit;}\n`;
    });
  }).join('');
}
