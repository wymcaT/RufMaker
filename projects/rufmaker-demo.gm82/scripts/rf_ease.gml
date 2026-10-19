// Shared with the native editor: normalized outgoing segment easing.
var e,u,p,period,v;
e=argument0;u=max(0,min(1,argument1));p=max(.1,argument2);period=max(.01,argument3);
if u=0 || u=1 return u;
switch(e) {
case 'in':return sqr(u);case 'out':return 1-sqr(1-u);case 'smooth':return u*u*(3-2*u);
case 'sin-in':return 1-cos(u*pi/2);case 'sin-out':return sin(u*pi/2);case 'sin-inout':return (1-cos(u*pi))/2;
case 'circle-in':return 1-sqrt(1-u*u);case 'circle-out':return sqrt(1-sqr(u-1));case 'circle-inout':if u<.5 return (1-sqrt(1-4*u*u))/2;return (sqrt(1-4*sqr(u-1))+1)/2;
case 'power-in':return power(u,p);case 'power-out':return 1-power(1-u,p);case 'power-inout':if u<.5 return power(2*u,p)/2;return 1-power(2*(1-u),p)/2;
case 'expo-in':return power(2,10*(u-1));case 'expo-out':return 1-power(2,-10*u);case 'expo-inout':if u<.5 return power(2,20*u-10)/2;return 1-power(2,-20*u+10)/2;
case 'bounce':return rf_bounce(u);case 'bounce-in':return 1-rf_bounce(1-u);case 'bounce-inout':if u<.5 return (1-rf_bounce(1-2*u))/2;return (1+rf_bounce(2*u-1))/2;
case 'expsin':return power(2,-10*u)*sin((u-period/4)*2*pi/period)+1;
case 'back':return 1+2.70158*power(u-1,3)+1.70158*sqr(u-1);case 'step':return 0;
}return u;
