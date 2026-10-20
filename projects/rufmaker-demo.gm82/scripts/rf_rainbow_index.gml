// sequence, timeline frame, density, phase, speed, start, dynamic, reverse, frame count
var p,n;p=argument0*argument2+argument3;
if argument6 p+=(argument1-argument5)*argument4;
if argument7 p=-p;
n=max(1,argument8);return ((floor(p) mod n)+n) mod n;
