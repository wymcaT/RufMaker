import fs from 'node:fs';
import {evaluateExpression} from '../../studio/random-parameters.mjs';
const cases=[];
for(const seed of [0,1,12345,2147483647])for(const slot of ['a|x|base','樱桃|angle|key-b'])for(const expression of ['random(200)','random_range(-400,300)','choose(12,44,-25,66）','choose(random(40),random_range(100,200),irandom(200))+4*2','-(.25+random(1e2))/5','irandom_range(-3,8)'])cases.push({expression,seed,slot,value:evaluateExpression(expression,seed,slot)});
fs.writeFileSync(process.argv[2],JSON.stringify(cases));
