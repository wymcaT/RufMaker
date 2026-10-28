import fs from 'node:fs';
import path from 'node:path';
import {normalizeState,valueAt} from '../../studio/motion.mjs';
import {cameraSample} from '../../studio/camera-presets.mjs';
import {ease} from '../../studio/motion.mjs';

const base=path.resolve(import.meta.dirname,'../../build/preset-parameters-check');
const scene=normalizeState(JSON.parse(fs.readFileSync(path.join(base,'scene.json'),'utf8'))),expected=JSON.parse(fs.readFileSync(path.join(base,'expected.json'),'utf8'));
let count=0;
for(const [time,rows] of Object.entries(expected))for(const t of scene.tracks){
  const frame=Number(time),p=rows[t.id],props={x:valueAt(t,'x',frame),y:valueAt(t,'y',frame),angle:valueAt(t,'angle',frame),zoom:valueAt(t,'cameraZoom',frame),blur:valueAt(t,'cameraBlur',frame),blurAngle:valueAt(t,'cameraBlurAngle',frame),intensity:valueAt(t,'cameraIntensity',frame)};
  for(const [key,value] of Object.entries(props))if(Math.abs(value-p[key])>.002)throw Error(`${t.id} frame ${frame} ${key}: JS ${value} vs C# ${p[key]}`);
  if(t.cameraPreset){const c=cameraSample(t.cameraPreset,frame,scene.fps,ease);if(!t.motionPreset&&(Math.abs(c.x-p.x)>.002||Math.abs(c.y-p.y)>.002))throw Error(`Camera path mismatch ${t.id}/${frame}`);}
  count++;
}
console.log(`PASS: ${count} C#/JS preset poses, each with independent zoom, blur and intensity.`);
