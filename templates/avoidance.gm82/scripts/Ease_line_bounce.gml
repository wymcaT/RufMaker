///Ease_line_bounce(t:0~1):0~1(size)
if argument0<0{
    return 0
}
else if argument0<0.4{
    return 3*argument0
}
else if argument0<1-sqrt(5)/5{
    return 2.5*sqr(argument-1)+0.3
}
else if argument0<0.7{
    return -2.5*sqr(argument-1)+1.3
}
else if argument0<-sqrt(3)/10+1{
    return 2.5*sqr(argument-1)+0.85
}
else if argument0<1{
    return -2.5*sqr(argument-1)+1
}
else{
    return 1
}
