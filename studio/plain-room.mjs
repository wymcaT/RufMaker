import fs from 'node:fs';
import path from 'node:path';

// Replace only the supplied avoidance room's legacy presentation. Once migrated,
// native room background settings and newly edited sprites are left to the author.
export function preparePlainRoom(root,project,state,write,append,io=fs) {
  for(const [name,base,sprite,solid,height] of [['objRufBlock','block','sprRufBlock',1,32],['objRufPlatform','platform','sprRufPlatform',0,8]]) {
    const spriteDir=path.join(project,'sprites',sprite);
    if(!io.existsSync(spriteDir)){
      io.mkdirSync(spriteDir);io.copyFileSync(path.join(root,'assets','terrain',`${sprite}.png`),path.join(spriteDir,'0.png'));
      write(path.join(spriteDir,'sprite.txt'),`frames=1\norigin_x=0\norigin_y=0\ncollision_shape=0\nalpha_tolerance=0\nper_frame_colliders=0\nbbox_type=0\nbbox_left=0\nbbox_top=0\nbbox_right=31\nbbox_bottom=${height-1}\n`);
      append(path.join(project,'sprites','index.yyd'),sprite);append(path.join(project,'sprites','tree.yyd'),`\t|${sprite}`);
    }
    const object=path.join(project,'objects',name);
    if(!io.existsSync(object+'.txt')){
      write(object+'.txt',`sprite=${sprite}\nvisible=1\nsolid=${solid}\npersistent=0\ndepth=100\nparent=${base}\nmask=\n`);
      write(object+'.gml',`#define Draw_0\n/*"/*'/**//* YYD ACTION\nlib_id=1\naction_id=603\napplies_to=self\n*/\ndraw_self();\n`);
      append(path.join(project,'objects','index.yyd'),name);append(path.join(project,'objects','tree.yyd'),`\t|${name}`);
    }
  }
  const room=path.join(project,'rooms',state.room),instances=path.join(room,'instances.txt');
  write(instances,io.readFileSync(instances,'utf8').split(/\r?\n/).filter(Boolean).filter(line=>!/^obj(Background|Ad),/.test(line)).map(line=>line.replace(/^block,/, 'objRufBlock,').replace(/^platform,/,'objRufPlatform,')).join('\n')+'\n');
  state.plainRooms??=[];
  if(!state.plainRooms.includes(state.room)){
    const settings=path.join(room,'room.txt');
    write(settings,io.readFileSync(settings,'utf8').replace(/^bg_color=.*$/m,'bg_color=2105376').replace(/^clear_screen=.*$/m,'clear_screen=1').replace(/^bg_visible\d+=.*$/gm,line=>line.split('=')[0]+'=0').replace(/^bg_source\d+=.*$/gm,line=>line.split('=')[0]+'='));
    state.plainRooms.push(state.room);
  }
  const world=path.join(project,'objects','world.gml');
  if(io.existsSync(world))write(world,io.readFileSync(world,'utf8').replace(/^surface_free\(global\.(surface[12])\)[ \t]*$/gm,(_,name)=>`if variable_global_exists('${name}') {if surface_exists(global.${name}) surface_free(global.${name});}`));
}
