///Directionapproach(dirfrom,dirto,sca)
//example:dir+=Directionapproach(dir,dirto,10)
if abs(argument0-argument1)<=180{
  return (argument1-argument0)/argument2
}
if abs(argument0-argument1)>180{
  return (sign(argument0-argument1)*(360-abs(argument1-argument0)))/argument2
}
