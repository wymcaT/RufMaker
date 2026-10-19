///Ease_circle_in(t:0~1):0~1(size)
if argument0>=1 return 1
else if argument0<=0 return 0
else return sqrt(1-power(1-argument0,2))
