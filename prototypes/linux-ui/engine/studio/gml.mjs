// GM8.2 does not accept exponent notation in numeric literals.
export function gmlNumber(value,fallback=0) {
  let number=Number(value);if(!Number.isFinite(number))number=Number(fallback);if(!Number.isFinite(number))number=0;
  const text=String(number);if(!/[eE]/.test(text))return text;
  const sign=number<0?'-':'',[mantissa,exponent]=text.replace(/^-/, '').toLowerCase().split('e'),digits=mantissa.replace('.','');
  const point=(mantissa.includes('.')?mantissa.indexOf('.'):mantissa.length)+Number(exponent);
  return sign+(point<=0?'0.'+'0'.repeat(-point)+digits:point>=digits.length?digits+'0'.repeat(point-digits.length):digits.slice(0,point)+'.'+digits.slice(point));
}
