import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import {editGmAsset,readGmAsset,gmAssetNames,gmObjectReferences} from './gm-resources.mjs';
import {describeInstanceCode,updateInstanceCode} from './gm-instance-fields.mjs';

function project(){
  const folder=path.resolve('build/gm-resources');
  fs.mkdirSync(folder,{recursive:true});
  return fs.mkdtempSync(path.join(folder,'resources-'));
}
const edit=(project,asset,operation,options)=>editGmAsset(project,{kind:asset.kind,name:asset.name,revision:asset.revision,operation,...options});
const action=(id)=>`/*"/*'/**//* YYD ACTION
lib_id=1
action_id=${id}
applies_to=self
*/
`;
test('writes only the selected GML action and preserves other YYD actions and line endings',()=>{
  const dir=project(),kind='objects',name='objEdit';
  editGmAsset(dir,{kind,name,operation:'create'});
  const source=path.join(dir,'objects',name+'.gml');
  const original=('#define Create_0\n'+action(604)+'argument=2\n'+action(603)+'x=5;\n'+action(603)+'y=8;\n#define Other_4\n'+action(603)+'//field m: number\n').replace(/\n/g,'\r\n');
  fs.writeFileSync(source,original);
  const first=readGmAsset(dir,{kind,name});
  assert.deepEqual(first.events[0].actions.map(a=>a.editable),[false,true,true]);
  const selected=readGmAsset(dir,{kind,name,section:'Create_0',action:1});
  assert.equal(selected.content,'x=5;\r\n');
  edit(dir,first,'code',{section:'Create_0',action:1,content:'x=9;'});
  assert.equal(fs.readFileSync(source,'utf8'),original.replace('x=5;','x=9;'));
  assert.throws(()=>edit(dir,first,'code',{section:'Create_0',action:1,content:'x=4;'}),/已被修改/);
  assert.throws(()=>editGmAsset(dir,{kind,name:'ObjEdit',operation:'create'}),/名称无效或已存在/);
});
test('path metadata and timeline moment edits use actual indexed assets',()=>{
  const dir=project();
  let asset=editGmAsset(dir,{kind:'timelines',name:'tlNew',operation:'create'});
  asset=edit(dir,asset,'add',{frame:22});
  asset=edit(dir,asset,'add',{frame:4});
  asset=edit(dir,asset,'code',{section:'22',action:0,content:'alarm[0]=10;'});
  asset=edit(dir,asset,'move',{section:'22',frame:2});
  assert.deepEqual(asset.events.map(e=>e.id),['2','4']);
  assert.match(readGmAsset(dir,{kind:asset.kind,name:asset.name,section:'2',action:0}).content,/alarm\[0\]=10/);
  assert.deepEqual(gmAssetNames(dir,'timelines'),['tlNew']);
  let route=editGmAsset(dir,{kind:'paths',name:'route',operation:'create'});
  const settings=path.join(dir,'paths','route','path.txt');
  fs.appendFileSync(settings,'custom_key=preserve\n');
  route=readGmAsset(dir,{kind:'paths',name:'route'});
  const info=edit(dir,route,'settings',{path:{smooth:true,closed:true,precision:6,points:[{x:16,y:12,speed:150},{x:60,y:80,speed:80}]}});
  assert.equal(info.path.points[0].speed,150);
  assert.match(fs.readFileSync(settings,'utf8'),/custom_key=preserve/);
});
test('object events and visual actions can be added, edited and reordered without rewriting opaque actions',()=>{
  const dir=project(),kind='objects',name='objEditor';
  editGmAsset(dir,{kind,name,operation:'create'});
  const file=path.join(dir,kind,name+'.gml'),opaque=action(604)+'arg0=keep this exact text\n';
  fs.writeFileSync(file,'#define Create_0\n'+opaque);
  let asset=readGmAsset(dir,{kind,name});
  asset=edit(dir,asset,'add',{section:'Other_25',empty:true});
  assert.deepEqual(asset.events[1].actions,[]);
  asset=edit(dir,asset,'change-section',{section:'Create_0',nextSection:'Step_1'});
  asset=edit(dir,asset,'add-action',{section:'Step_1',visual:{kind:'create',target:name,x:'12',y:'15',relative:true}});
  asset=edit(dir,asset,'add-action',{section:'Step_1',content:'depth=10;'});
  asset=edit(dir,asset,'move-action',{section:'Step_1',action:1,direction:-1});
  assert.deepEqual(asset.events[0].actions.map(a=>a.id),[201,604,603]);
  asset=edit(dir,asset,'visual-action',{section:'Step_1',action:0,visual:{...asset.events[0].actions[0].visual,x:'3e-3'}});
  assert.deepEqual(asset.events[0].actions[0].visual,{kind:'create',target:name,x:'0.003',y:'15',relative:true,appliesTo:'self'});
  assert.ok(fs.readFileSync(file,'utf8').includes(opaque));
  asset=edit(dir,asset,'delete-action',{section:'Step_1',action:1});
  asset=edit(dir,asset,'add-action',{section:'Other_25',visual:{kind:'variable',name:'alarm[0]',value:'50'}});
  assert.equal(asset.events[1].actions[0].visual.name,'alarm[0]');
  asset=edit(dir,asset,'delete-section',{section:'Other_25'});
  assert.deepEqual(asset.events.map(e=>[e.id,e.actions.map(a=>a.id)]),[['Step_1',[201,603]]]);
  assert.equal(readGmAsset(dir,{kind,name,section:'Step_1',action:1}).content,'depth=10;\n');
});
test('object deletion reports project references and preserves the remaining resource indexes',()=>{
  const dir=project(),kind='objects',name='objTarget';
  let obj=editGmAsset(dir,{kind,name:'objFirst',operation:'create'});
  const target=editGmAsset(dir,{kind,name,operation:'create'});
  const other=editGmAsset(dir,{kind,name:'objTargetExtra',operation:'create'});
  const child=path.join(dir,kind,obj.name+'.txt'),rooms=path.join(dir,'rooms','rMain'),room=path.join(rooms,'instances.txt');
  const author=path.join(dir,'rufmaker.json'),gml=path.join(dir,kind,other.name+'.gml');
  fs.mkdirSync(rooms,{recursive:true});
  obj=edit(dir,obj,'settings',{object:{parent:name,visible:true,depth:0}});
  fs.writeFileSync(room,'objTarget,10,20,00122617,0,1,1,4294967295,0,0\n');
  fs.writeFileSync(gml,'#define Collision_objTarget\n'+action(603)+'// empty\n');
  fs.writeFileSync(author,JSON.stringify({tracks:[{children:[{name:'Nested clip',nativeObject:name}]}]}));
  assert.deepEqual(gmObjectReferences(dir,name).sort(),[path.relative(dir,child),path.relative(dir,gml),path.relative(dir,room),'rufmaker.json · Nested clip'].sort());
  assert.throws(()=>edit(dir,target,'delete'),/仍有资源引用此对象/);
  edit(dir,obj,'settings',{object:{parent:'',visible:true,depth:0}});
  fs.writeFileSync(room,'');
  fs.writeFileSync(gml,'#define Create_0\n'+action(603)+'// objTarget is a comment\ninstance_create(1,2,objTargetExtra);\n');
  fs.writeFileSync(author,'{"tracks":[]}');
  assert.deepEqual(edit(dir,target,'delete'),{name,deleted:true});
  assert.deepEqual(gmAssetNames(dir,kind),['objFirst','objTargetExtra']);
  assert.equal(fs.readFileSync(path.join(dir,kind,'index.yyd'),'utf8'),'objFirst\n\nobjTargetExtra\n');
  assert.equal(fs.readFileSync(path.join(dir,kind,'tree.yyd'),'utf8'),'|objFirst\n|objTargetExtra\n');
  assert(!fs.existsSync(path.join(dir,kind,name+'.gml')));
});
test('consecutive empty events retain the action separator required by the GM8.2 loader',()=>{
  const dir=project(),kind='objects',name='objEmpty';
  let asset=editGmAsset(dir,{kind,name,operation:'create'});
  asset=edit(dir,asset,'add',{section:'Create_0',empty:true});
  asset=edit(dir,asset,'add',{section:'Alarm_0'});
  asset=edit(dir,asset,'delete-action',{section:'Alarm_0',action:0});
  asset=edit(dir,asset,'add',{section:'Step_1',empty:true});
  const source=readGmAsset(dir,{kind,name,whole:true}).content;
  assert.equal(source,'#define Create_0\n\n#define Alarm_0\n\n#define Step_1\n\n');
  for(const block of source.trimStart().replace(/^#define /,'').split('\n#define '))
    assert(block.includes('\n'),'GM8.2 cannot split the empty event name from its action body');
});
test('native motion, alarms, sprite and script actions keep argument order, expressions and scope',()=>{
  const dir=project(),kind='objects',name='objActions';
  let asset=editGmAsset(dir,{kind,name,operation:'create'});
  editGmAsset(dir,{kind,name:'objScope',operation:'create'});
  fs.mkdirSync(path.join(dir,'sprites','sprAction'),{recursive:true});
  fs.writeFileSync(path.join(dir,'sprites','sprAction','sprite.txt'),'frames=1\n');
  fs.writeFileSync(path.join(dir,'sprites','index.yyd'),'sprAction\n');
  fs.mkdirSync(path.join(dir,'scripts'));
  fs.writeFileSync(path.join(dir,'scripts','scrAction.gml'),'return argument0;\n');
  fs.writeFileSync(path.join(dir,'scripts','index.yyd'),'scrAction\n');
  asset=edit(dir,asset,'add',{section:'Create_0',empty:true});
  const items=[
    {kind:'motion',direction:'random(360)',speed:'choose(3,5)',relative:true,appliesTo:'other'},
    {kind:'alarm',frames:'50',alarm:'2',relative:true,appliesTo:'objScope'},
    {kind:'sprite',target:'sprAction',subimage:'3',animationSpeed:'0.25'},
    {kind:'script',target:'scrAction',arguments:['4','choose(1,2)','x','y','"a=b"']},
    {kind:'bounce',precise:'1',collisionAll:'0'},
    {kind:'inherited'}
  ];
  for(const visual of items)asset=edit(dir,asset,'add-action',{section:'Create_0',visual});
  assert.deepEqual(asset.events[0].actions.map(a=>a.id),[102,301,541,601,115,604]);
  const source=readGmAsset(dir,{kind,name,whole:true}).content;
  assert.match(source,/action_id=102[\s\S]*?arg0=random\(360\)\narg1=choose\(3,5\)/);
  assert.match(source,/action_id=301[\s\S]*?arg0=50\narg1=2/);
  assert.deepEqual(asset.events[0].actions[3].visual.arguments,items[3].arguments);
  assert.equal(asset.events[0].actions[0].visual.appliesTo,'other');
  assert(gmObjectReferences(dir,'objScope').includes(path.join('objects',name+'.gml')));
  const selected=asset.events[0].actions[0].visual;
  asset=edit(dir,asset,'visual-action',{section:'Create_0',action:0,visual:{...selected,speed:'7',appliesTo:'self'}});
  assert.equal(asset.events[0].actions[0].visual.direction,'random(360)');
  assert.equal(asset.events[0].actions[0].visual.speed,'7');
  assert.equal(asset.events[0].actions[0].visual.appliesTo,'self');
  assert.deepEqual(asset.events[0].actions[3].visual.arguments,items[3].arguments);
  assert.throws(()=>edit(dir,asset,'add-action',{section:'Create_0',visual:{kind:'alarm',frames:'1',alarm:'12'}}),/Alarm 11/);
  assert.throws(()=>edit(dir,asset,'add-action',{section:'Create_0',visual:{kind:'script',target:'missing',arguments:['0','0','0','0','0']}}),/资源不存在/);
  assert.throws(()=>edit(dir,asset,'add-action',{section:'Create_0',visual:{kind:'sprite',target:'',subimage:'0',animationSpeed:'0'}}),/有效的贴图/);
});
test('editing an imported action preserves its newline style, scope and unrelated actions',()=>{
  const dir=project(),kind='objects',name='objImportedAction';
  editGmAsset(dir,{kind,name,operation:'create'});
  const header=action(301).replace('applies_to=self','applies_to=other\ninvert=1\nrelative=1\narg0=30\narg1=2');
  const tail=action(603)+'speed=99;\n';
  const original=('#define Create_0\n'+header+tail).replace(/\n/g,'\r\n');
  const file=path.join(dir,'objects',name+'.gml');
  fs.writeFileSync(file,original);
  const asset=readGmAsset(dir,{kind,name}),visual=asset.events[0].actions[0].visual;
  assert.equal(visual.appliesTo,'other');
  edit(dir,asset,'visual-action',{section:'Create_0',action:0,visual:{...visual,frames:'60'}});
  assert.equal(fs.readFileSync(file,'utf8'),original.replace('arg0=30','arg0=60'));
});
test('instance fields inherit declarations and edit only the matching generated lines',()=>{
  const dir=project(),room='rTest',id='000AAA01',folder=path.join(dir,'rooms',room);
  fs.mkdirSync(folder,{recursive:true});
  fs.writeFileSync(path.join(folder,'instances.txt'),`objChild,0,0,${id},0,1,1,4294967295,0,1\n`);
  fs.mkdirSync(path.join(dir,'objects'));
  fs.writeFileSync(path.join(dir,'objects','objBase.txt'),'parent=\n');
  fs.writeFileSync(path.join(dir,'objects','objChild.txt'),'parent=objBase\n');
  fs.writeFileSync(path.join(dir,'objects','objBase.gml'),'#define Other_4\n'+action(603)+'//field enabled: false\n  //field count: number(1,20) - spawn count\n');
  fs.writeFileSync(path.join(dir,'objects','objChild.gml'),'#define Other_4\n'+action(603)+'//field target: xy\n');
  const file=path.join(folder,id+'.gml');
  fs.writeFileSync(file,'before();\n//gm82 fields begin\nunknown=3\ncount=5\n//gm82 fields end\nafter();\n');
  const read=describeInstanceCode(dir,room,id);
  assert.deepEqual(read.fields.find(field=>field.name==='count').depends,['enabled']);
  assert.equal(read.fields.find(field=>field.name==='count').description,'spawn count');
  const next=updateInstanceCode(dir,room,id,{hash:read.hash,fields:[{name:'count',value:'9'},{name:'target',value:'25, -3'}]},fs);
  assert.equal(next.source,'before();\n//gm82 fields begin\nunknown=3\ncount=9\ntarget[0]=25\ntarget[1]=-3\n//gm82 fields end\nafter();\n');
  assert.throws(()=>updateInstanceCode(dir,room,id,{hash:read.hash,fields:[{name:'count',value:'200'}]},fs),/字段值无效/);
});
