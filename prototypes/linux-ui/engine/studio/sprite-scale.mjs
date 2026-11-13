// Keep the uniform behavior scale separate from signed per-axis sprite multipliers.
// No division by an axis: mirrors and zero-crossing keyframes must remain valid.
export function setSpriteScale(target,size='rf_scale',axisX='rf_scaleX',axisY='rf_scaleY') {
  const p=target?target+'.':'';
  return `${p}rf_size=${size};${p}rf_axis_x=${axisX};${p}rf_axis_y=${axisY};${p}image_xscale=${p}rf_size*${p}rf_axis_x;${p}image_yscale=${p}rf_size*${p}rf_axis_y;`;
}
export function setUniformScale(value) {
  return `rf_size=max(0,${value});image_xscale=rf_size*rf_axis_x;image_yscale=rf_size*rf_axis_y;`;
}
