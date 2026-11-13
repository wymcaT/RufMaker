// GM8 has setters, not the modern gpu_* state stack. RufMaker sprite passes
// own a normal baseline, as do effects.mjs and native-compositor.mjs. Each
// individual sprite/trail selects its mode and restores that baseline, never
// leaving additive state behind for the next sprite or native draw event.
export function normalizeBlendMode(track) {
  if(track.blendMode==null)track.blendMode='normal';
  if(!['normal','add'].includes(track.blendMode))throw Error('不支持的混合模式 / Unsupported blendMode: '+track.blendMode);
}
export function blendModeCode(track) {
  normalizeBlendMode(track);
  return track.blendMode==='add'?'bm_add':'bm_normal';
}
export function blendScope(code,mode='bm_normal') {
  return `{draw_set_blend_mode(${mode});${code}draw_set_blend_mode(bm_normal);}`;
}
