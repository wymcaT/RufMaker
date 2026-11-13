export function normalizeGroups(s){
  s.timelineGroups??=[];const ids=new Set(),lanes=new Set();
  for(const g of s.timelineGroups){if(typeof g.id!=='string'||!g.id||ids.has(g.id)||typeof g.name!=='string'||!/^#[a-f0-9]{6}$/i.test(g.color))throw Error('轨道组数据无效');ids.add(g.id);g.lanes=(g.lanes??[]).filter(n=>Number.isInteger(n)&&n>=0&&n<2147483647&&!lanes.has(n)&&(lanes.add(n),true));}
  return s;
}
export function groupId(s,t){return t._scopeGroup??s.timelineGroups?.find(g=>g.lanes.includes(t.lane))?.id??'';}
export function groupIndex(s,t){return s.timelineGroups?.findIndex(g=>g.id===groupId(s,t))??-1;}
export function eventGroupIndex(s,e){return s.timelineGroups?.findIndex(g=>g.id===(e.eventGroup||groupId(s,e)))??-1;}
