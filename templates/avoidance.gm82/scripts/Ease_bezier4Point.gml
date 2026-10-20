///Ease_bezier4Point(t:0~1,P1,P2,P3,P4):0~1(size)
var T1,T2,T3;
T1=Merge_Number(argument1,argument2,argument0)
T2=Merge_Number(argument2,argument3,argument0)
T3=Merge_Number(argument3,argument4,argument0)
return Merge_Number(Merge_Number(T1,T2,argument0),Merge_Number(T2,T3,argument0),argument0)
