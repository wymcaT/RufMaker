import fs from 'node:fs';import path from 'node:path';
import {extractSampleTables} from '../studio/sample-extraction.mjs';
const [input,output,...extra]=process.argv.slice(2);
try{
 if(!input||extra.length)throw Error('Usage: node tools/extract-sample-tables.mjs scene.json [new-scene.json]. Without output: report only. Never overwrites a file.');
 if(output&&path.resolve(output)===path.resolve(input))throw Error('Output must be a separate new file.');
 const result=extractSampleTables(JSON.parse(fs.readFileSync(input,'utf8')));
 if(output)fs.writeFileSync(output,JSON.stringify(result.state,null,2)+'\n',{flag:'wx'});
 console.log(JSON.stringify({written:output??null,tables:result.report},null,2));
}catch(e){console.error(e.message);process.exitCode=1;}
