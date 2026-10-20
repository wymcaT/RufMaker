///Move_throw(t:0~1,Max:from 1 to infty):0~1(size)___form 0 to 1
var a,b,X;
X=argument1;
b=2*(X+sqrt(X*X-X))
a=1-b
return a*sqr(argument0)+b*argument0
