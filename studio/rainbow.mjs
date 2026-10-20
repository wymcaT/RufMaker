const n=(v,d=0)=>String(Number.isFinite(Number(v))?Number(v):d);
export const rainbowSprite='sprrainbowcherry';
export function normalizeRainbow(t) {
  const defaults={rainbow:false,rainbowReverse:false,rainbowDynamic:false,rainbowSourceSprite:'',rainbowSourceColor:'',rainbowDensity:1,rainbowOffset:0,rainbowSpeed:.25};
  for(const [key,value] of Object.entries(defaults))if(t[key]===undefined)t[key]=value;
  if(t.rainbow){t.sprite=rainbowSprite;t.color='#FFFFFF';}
}
export function rainbowCode(t,sequence,time,{sampled=true}={}) {
  if(!t.rainbow)return '0';
  return `rf_rainbow_index(${sequence},${time},${sampled?'rf_rainbowDensity':n(t.rainbowDensity,1)},${sampled?'rf_rainbowOffset':n(t.rainbowOffset)},${sampled?'rf_rainbowSpeed':n(t.rainbowSpeed,.25)},${n(t.start)},${t.rainbowDynamic?1:0},${t.rainbowReverse?1:0},32)`;
}
export const rodSequence=t=>t.rodMode==='center'?'abs(rf_fraction-.5)*(rf_count-1)':'rf_j';
export const rainbowGml=`// sequence, timeline frame, density, phase, speed, start, dynamic, reverse, frame count
var p,n;p=argument0*argument2+argument3;
if argument6 p+=(argument1-argument5)*argument4;
if argument7 p=-p;
n=max(1,argument8);return ((floor(p) mod n)+n) mod n;
`;
