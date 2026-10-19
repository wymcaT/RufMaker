///Ease_expoN_outin(t:0~1,N):0~1(size)
if argument0<=0{
    return 0
}
else if argument0<0.5{
    return 0.5-Ease_expoN_in(1-argument0*2,argument1)*0.5
}
else if argument0<=1{
    return 0.5+Ease_expoN_in(argument0*2-1,argument1)*0.5
}
else return 1
