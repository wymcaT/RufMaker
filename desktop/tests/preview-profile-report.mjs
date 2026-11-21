import fs from 'node:fs';
import path from 'node:path';

const folder=process.argv[2];
function read(name){
  const [head,...rows]=fs.readFileSync(path.join(folder,name),'utf8').trim().split(/\r?\n/);
  const keys=head.split(',');
  return rows.map(line=>Object.fromEntries(line.split(',').map((v,i)=>[keys[i],Number(v)])));
}
const summary=[];
for(const fixture of ['original','stress']){
  const cpu=read(fixture+'-cpu.csv'),gpu=read(fixture+'-gpu.csv');
  if(cpu.length!==gpu.length||gpu.some((r,i)=>r.frame!==cpu[i].frame||r.sprites!==cpu[i].sprites))throw Error('Mismatched workload: '+fixture);
  for(const [mode,rows] of [['cpu',cpu],['gpu',gpu]]){
    const mean=k=>Number((rows.reduce((sum,r)=>sum+r[k],0)/rows.length).toFixed(3));
    const sorted=rows.map(r=>r.total).sort((a,b)=>a-b);
    const row={fixture,mode,frames:rows.length,from:rows[0].frame,to:rows.at(-1).frame};
    for(const k of Object.keys(rows[0]).filter(k=>k!=='frame'))row[k]=mean(k);
    row.p95=sorted[Math.ceil(.95*rows.length)-1];row.max=sorted.at(-1);row.over20=rows.filter(r=>r.total>20).length;summary.push(row);
  }
}
fs.writeFileSync(path.join(folder,'summary.json'),JSON.stringify(summary,null,2)+'\n');
console.table(summary.map(({fixture,mode,total,p95,max,over20,trails,bodies,fused,readbacks,sprites})=>({fixture,mode,total,p95,max,over20,trails,bodies,fused,readbacks,sprites})));
