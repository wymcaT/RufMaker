#define Create_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
// you should define the boss object 'boss_obj' when creating this object
// e.g. in the creation event of your boss
//      var inst;
//      inst = instance_create(view_xview[0],view_yview[0],objHPBar);
//      inst.boss_obj = myBoss;
//
//  also, the length of HP bar depends on the HP values of the boss object
//  in this object, you should use variable 'curHP' and 'maxHP' to indicate
//  the current HP and maximum HP of the boss object
#define Draw_0
/*"/*'/**//* YYD ACTION
lib_id=1
action_id=603
applies_to=self
*/
if instance_exists(boss_obj){
    drawLife(40,10,760,22,boss_obj.curHP,boss_obj.maxHP,c_lime,c_red,1,c_black);
}
