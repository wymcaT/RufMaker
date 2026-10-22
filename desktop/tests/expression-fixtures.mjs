import fs from 'node:fs';
import {evaluateExpression} from '../../studio/random-parameters.mjs';
const context={'player.x':527,'player.y':301,x:100,y:200,x1:10,y1:20,x2:30,y2:40,frame:157,age:37,direction:359,speed:3,fps:50};
const expressions=['player.x','player.y+100','point_direction(x,y,player.x,player.y)','point_distance(x,y,player.x,player.y)','point_direction(x1,y1,x2,y2)','lengthdir_x(100,45)','lengthdir_y(100,45)','turn_towards(direction,1,3)','sin(time*2*pi)*30+cos(age)','clamp(lerp(0,1,.5),.2,.8)','min(1,2,3)+max(4,5)','floor(2.8)+ceil(3.1)+sqrt(9)+abs(-2)','choose(player.x,player.y,random(100))','random_range(-2,2)+frame*0','irandom_range(5,10)+random(3)','x+10'];
fs.writeFileSync(process.argv[2],JSON.stringify(expressions.map(expression=>({expression,value:evaluateExpression(expression,991,'fixture',context)}))));
