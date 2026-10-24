import fs from 'node:fs';
import path from 'node:path';
import {formationPoint,formationPerimeter} from '../../studio/shapes.mjs';
import {normalizeState,valueAt} from '../../studio/motion.mjs';
const directory=process.argv[2];fs.mkdirSync(directory,{recursive:true});
const shapes=['ring','polygon','heart','star2','star','ellipse','flower','infinity'];
const geometry=[];
for(const shape of shapes)for(const sides of [3,5,6,7])for(const u of [-.25,0,.05,.2,.337,.5,.8,1,1.3]){
  const radius=127.5,orientation=23.4,aspect=1.3,inner=.36;
  geometry.push({shape,sides,u,radius,orientation,aspect,inner,...formationPoint(shape,u,radius,orientation,sides,aspect,inner),perimeter:formationPerimeter(shape,radius,sides,aspect,inner)});
}
const tracks=shapes.map((shape,i)=>({id:shape,name:shape,kind:'rod',shape,sprite:'sprCherry',start:0,end:200,count:60,sides:5,x:shape==='star2'?0:400,y:shape==='star2'?0:304,radius:120,orientation:shape==='heart'?0:90,angularSpeed:.25,rolling:true,rollSpeed:2,shapeAspect:1,shapeInner:.45,easing:'linear',keyframes:[{frame:0,property:'radius',value:80},{frame:200,property:'radius',value:140},{frame:0,property:'orientation',value:0},{frame:200,property:'orientation',value:135},{frame:0,property:'shapeAspect',value:.7},{frame:200,property:'shapeAspect',value:1.4}]}));
tracks.push({id:'group',name:'group',kind:'group',start:0,end:200,x:320,y:250,angle:37,scale:1.2,alpha:1,keyframes:[]});tracks.find(t=>t.id==='star2').parentId='group';
const state=normalizeState({version:2,name:'formation check',room:'rAvoidance',duration:200,fps:50,music:'Data/Music/BGM.ogg',tracks});
const samples=[];
for(const frame of [0,37,150])for(const t of state.tracks.filter(t=>t.kind==='rod')){
  const radius=valueAt(t,'radius',frame),sides=valueAt(t,'sides',frame),aspect=valueAt(t,'shapeAspect',frame),inner=valueAt(t,'shapeInner',frame),orientation=t.shape==='ring'?0:valueAt(t,'orientation',frame);
  let phase=(valueAt(t,'angle',frame)+t.angularSpeed*(frame-t.start))/360;
  const perimeter=formationPerimeter(t.shape,radius,sides,aspect,inner);if(perimeter>.001)phase+=(frame-t.start)*t.rollSpeed/perimeter;
  const points=Array.from({length:t.count},(_,i)=>{
    const p=formationPoint(t.shape,(i+(t.shape==='heart'?1:0))/t.count+phase,radius,orientation,sides,t.shape==='ring'?1:aspect,inner);p.x+=t.x;p.y+=t.y;
    if(t.parentId){const g=state.tracks.find(g=>g.id===t.parentId),a=g.angle*Math.PI/180,x=p.x,y=p.y;p.x=g.x+g.scale*(x*Math.cos(a)+y*Math.sin(a));p.y=g.y+g.scale*(-x*Math.sin(a)+y*Math.cos(a));}return p;
  });samples.push({id:t.id,frame,points});
}
fs.writeFileSync(path.join(directory,'fixtures.json'),JSON.stringify({geometry,state,samples}));
fs.writeFileSync(path.join(directory,'scene.json'),JSON.stringify(state));
