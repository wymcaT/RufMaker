///Ease_bounce(t:0~1):0~1(size)
if argument0<1/2.75
    return 7.5625*argument0*argument0
else if argument0<2/2.75
    return 7.5625*power(argument0-1.5/2.75,2)+0.75
else if argument0<2.5/2.75
    return 7.5625*power(argument0-2.25/2.75,2)+0.9375
else
    return 7.5625*power(argument0-2.625/2.75,2)+0.984375
