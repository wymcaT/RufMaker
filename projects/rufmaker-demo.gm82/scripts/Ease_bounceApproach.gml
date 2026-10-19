///Ease_bounceApproach(t:0~1):0~1(size)
if argument<0.3{
    return power(argument0/0.3,2)
}
else{
    return 1-20/3*(argument0-0.3)*exp(-10*(argument0-0.3))+40*exp(-7)*(argument0-1)+(cos((argument0-0.3)/0.7*pi)-1)*7/3*exp(-7)+(cos((argument0-0.3)/0.7*pi)+1)*14*exp(-7)
}
