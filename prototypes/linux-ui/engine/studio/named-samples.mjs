// Track-local, persistent named constant sample tables. No random draws occur here.
export const sampleReference=name=>/^sample\.[a-z_][a-z0-9_]*$/.test(name);
export function validateSampleTables(tables){
  if(tables===undefined||tables===null)return {};
  if(typeof tables!=='object'||Array.isArray(tables))throw Error('命名取样表必须是对象。');
  const rows=Object.entries(tables);if(rows.length>64)throw Error('每个物件最多64张命名取样表。');
  for(const [name,t] of rows){
    if(!/^[a-z_][a-z0-9_]*$/.test(name))throw Error('取样表名仅允许小写英文字母、数字、下划线，且不能以数字开头。');
    if(!t||typeof t!=='object'||Array.isArray(t)||!Array.isArray(t.values)||t.values.length<1||t.values.length>4096||!Array.from(t.values).every(v=>typeof v==='number'&&Number.isFinite(v)))throw Error('取样表 '+name+' 必须包含1–4096个有限数值。');
    if(!['clamp','wrap','error'].includes(t.mode))throw Error('取样表 '+name+' 必须显式指定 clamp、wrap 或 error 越界策略。');
  }return tables;
}
export function getSampleTable(tables,reference){
  const name=reference.slice(7);if(!tables||!Object.prototype.hasOwnProperty.call(tables,name))throw Error('未定义命名取样表：'+reference);
  const table=tables[name];validateSampleTables({[name]:table});return table;
}
export function sampleValue(table,index){
  if(!Number.isFinite(index))throw Error('取样索引必须是有限数值。');
  let i=Math.floor(index),n=table.values.length;
  if(table.mode==='error'&&(i<0||i>=n))throw Error('命名取样表索引越界。');
  i=table.mode==='wrap'?((i%n)+n)%n:Math.max(0,Math.min(n-1,i));return table.values[i];
}
export function sampleSignature(tables){return JSON.stringify(Object.entries(validateSampleTables(tables)).sort(([a],[b])=>a.localeCompare(b)).map(([name,t])=>[name,t.mode,t.values]));}
