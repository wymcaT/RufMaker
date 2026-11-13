import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import {editGmAsset,readGmAsset} from './gm-resources.mjs';
import {describeInstanceCode,updateInstanceCode} from './gm-instance-fields.mjs';
import {nativeEventBlock,replaceNativeEventRange} from './native-blocks.mjs';
import {fileRevision,readRoom,resourceName} from './native-room.mjs';
import {analyzeNativeProject} from './native-import.mjs';
import {ProjectSyncTransaction} from './project-sync.mjs';
const source=name=>fs.readFileSync(new URL(name,import.meta.url),'utf8');
const between=(text,start,end)=>{assert(text.includes(start)&&text.includes(end));return text.slice(text.indexOf(start)+start.length,text.indexOf(end,text.indexOf(start)+start.length));};
const bridge=source('bridge.mjs');
function fixture(t){const root=path.resolve('build/code-editor-save-check');fs.mkdirSync(root,{recursive:true});const dir=fs.mkdtempSync(path.join(root,'case-'));t.after(()=>fs.rmSync(dir,{recursive:true,force:true}));return dir;}
test('all long-lived code-editor callbacks advance revisions and propagate failed project saves',()=>{
  const object=source('../desktop/GmObjectEditor.cs'),asset=source('../desktop/GmAssetEditor.cs'),room=source('../desktop/GmResources.cs');
  assert(object.includes('int actionIndex=-1;'));assert(object.includes('if(actionIndex<0)'));assert(object.includes('operation="code",section=current.id,action=actionIndex'));
  assert(object.includes('source.hash=data.revision;'));
  assert.equal(asset.split('source.hash=data.revision;').length-1,2);
  assert(room.includes('revision=result.instanceRevision;code.hash=result.codeHash;'));
  const preview=source('../desktop/NativeGamePreview.cs'),native=source('../desktop/NativeRoomTools.cs');
  assert(preview.includes('source.hash=result.hash;source.start=result.start;source.end=result.end;saved=true;'));
  assert(preview.includes('start=source.start,end=source.end'));
  assert(preview.includes('if(!await SaveNow(false))throw new IOException'));
  assert(native.includes('if(!await SaveNow(false))throw new System.IO.IOException'));
});
test('GML action saves reuse the created action, advance revisions, and reject external edits',t=>{
  const project=fixture(t),kind='objects',name='objRepeat';
  let data=editGmAsset(project,{kind,name,operation:'create'});
  data=editGmAsset(project,{kind,name,revision:data.revision,operation:'add',section:'Create_0',empty:true});
  data=editGmAsset(project,{kind,name,revision:data.revision,operation:'add-action',section:'Create_0',content:'x=1;'});
  const action=data.events[0].actions.at(-1).index;
  for(const content of ['x=2;','x=3;'])data=editGmAsset(project,{kind,name,revision:data.revision,operation:'code',section:'Create_0',action,content});
  assert.equal(data.events[0].actions.length,1);
  assert.equal(readGmAsset(project,{kind,name,section:'Create_0',action}).content,'x=3;\n');
  fs.appendFileSync(path.join(project,'objects',name+'.gml'),'// external\n');
  assert.throws(()=>editGmAsset(project,{kind,name,revision:data.revision,operation:'code',section:'Create_0',action,content:'x=4;'}),/已被修改/);
});
test('asset responses retain the saved snapshot when an external writer edits immediately after commit',t=>{
  const project=fixture(t),kind='objects',name='objRace';let data=editGmAsset(project,{kind,name,operation:'create'});
  const original=ProjectSyncTransaction.prototype.commit;
  ProjectSyncTransaction.prototype.commit=function(){original.call(this);fs.appendFileSync(path.join(project,'objects',name+'.gml'),'// intervening edit\n');};
  try{data=editGmAsset(project,{kind,name,revision:data.revision,operation:'add',section:'Create_0',empty:true});}
  finally{ProjectSyncTransaction.prototype.commit=original;}
  assert.notEqual(data.revision,readGmAsset(project,{kind,name}).revision);
  assert.throws(()=>editGmAsset(project,{kind,name,revision:data.revision,operation:'add-action',section:'Create_0',content:'x=2;'}),/已被修改/);
});
test('native-event bridge returns the hash of saved bytes for two saves and rejects intervening edits',t=>{
  const project=fixture(t),file=path.join(project,'event.gml');fs.writeFileSync(file,'#define Step_0\nswitch(clock){\ncase 10:\n  x=1;\n  break;\n}\n');
  const event={kind:'step',line:3,file:'event.gml'};
  // Execute the actual command branch without importing bridge.mjs (which opens the user project).
  const save=new Function('payload','fs','nativeSourcePath','verifiedNativeEvent','replaceNativeEventRange','nativeEventBlock',between(bridge,"case 'native-event-save':", "case 'move-instance':"));
  let hash=nativeEventBlock(fs.readFileSync(file,'utf8'),event).hash;
  for(const content of ['  x=2;\n  break;\n','  x=3;\n  break;\n']){
    const result=save({event,content,hash},fs,()=>file,value=>value,replaceNativeEventRange,nativeEventBlock);
    assert.equal(result.hash,fileRevision(fs.readFileSync(file)));hash=result.hash;
  }
  fs.appendFileSync(file,'// external\n');
  assert.throws(()=>save({event,content:'x=4;',hash},fs,()=>file,value=>value,replaceNativeEventRange,nativeEventBlock),/已改变/);
});
test('instance bridge advances room and code revisions, including empty-to-code flag, without rereading saved code',t=>{
  const project=fixture(t),room='rTest',id='1234abcd',folder=path.join(project,'rooms',room);fs.mkdirSync(folder,{recursive:true});
  fs.writeFileSync(path.join(folder,'room.txt'),'width=800\nheight=608\nroomspeed=50\n');
  fs.writeFileSync(path.join(folder,'instances.txt'),'objTest,0,0,'+id+',0,1,1,4294967295,0,0\n');
  fs.writeFileSync(path.join(folder,id+'.gml'),'');
  const body=between(source('project.mjs'),'export function editRoomInstances(room,revision,edit) {','\nexport function moveRoomInstance');
  const edit=new Function('project','path','ProjectSyncTransaction','fileRevision','safeName','readRoom','room','revision','edit',body.trim().slice(0,-1));
  const editRoomInstances=(r,rev,callback)=>edit(project,path,ProjectSyncTransaction,fileRevision,resourceName,readRoom,r,rev,callback);
  const save=new Function('payload','project','editRoomInstances','updateInstanceCode','fileRevision',between(bridge,"case 'gm-instance-code-save':", "case 'source-read':"));
  let revision=readRoom(project,room).instanceRevision,hash=describeInstanceCode(project,room,id).hash;
  for(const content of ['x=1;','x=2;']){
    const result=save({room,id,revision,hash,content},project,editRoomInstances,updateInstanceCode,fileRevision);
    assert.equal(result.codeHash,describeInstanceCode(project,room,id).hash);
    assert.equal(result.instanceRevision,readRoom(project,room).instanceRevision);
    revision=result.instanceRevision;hash=result.codeHash;
  }
  fs.appendFileSync(path.join(folder,id+'.gml'),'// external');
  assert.throws(()=>save({room,id,revision,hash,content:'x=3;'},project,editRoomInstances,updateInstanceCode,fileRevision),/已修改实例代码/);
});

test('native-event editor keeps its hash-bound span when a verified condition changes or disappears',t=>{
  const project=fixture(t),write=(name,content)=>{const file=path.join(project,name);fs.mkdirSync(path.dirname(file),{recursive:true});fs.writeFileSync(file,content);};
  write('game.gm82','gm82_version=5\n');write('rooms/rTest/room.txt','width=800\nheight=608\nroomspeed=50\n');write('rooms/rTest/code.gml','');
  write('rooms/rTest/instances.txt','objAd,0,0,1234,0,1,1,4294967295,0,0\n');
  const filename='objects/objAd.gml',file=path.join(project,filename);
  const original='#define Create_0\nclock=0;\n#define Step_0\nclock+=1;\nif(clock == 10) { x=1; }\nif(clock == 30) { y=2; }\n';write(filename,original);
  const events=()=>analyzeNativeProject(path.join(project,'game.gm82'),'rTest','objAd',true).events;
  const event=events().find(e=>e.frame===10);
  let verifications=0;
  const verify=value=>{verifications++;const found=events().find(e=>e.file===value.file&&e.line===value.line&&e.kind===value.kind&&e.expression===value.expression);if(!found)throw Error('所选原生事件已变化，请重新读取工程。');return found;};
  const read=new Function('payload','fs','nativeSourcePath','verifiedNativeEvent','nativeEventBlock',between(bridge,"case 'native-event-read':", "case 'native-event-save':"));
  const save=new Function('payload','fs','nativeSourcePath','verifiedNativeEvent','replaceNativeEventRange','nativeEventBlock',between(bridge,"case 'native-event-save':", "case 'move-instance':"));
  let current=read({event},fs,()=>file,verify,nativeEventBlock);
  assert.equal(verifications,1);assert.equal(current.content,'if(clock == 10) { x=1; }');
  const prefix=original.slice(0,current.start),suffix=original.slice(current.end);
  for(const content of ['if(clock == 20) { x=3; }','// no longer an event\nx=4;','','if(clock == 25) { x=5; }']){
    current=save({event,hash:current.hash,start:current.start,end:current.end,content},fs,()=>file,verify,replaceNativeEventRange,nativeEventBlock);
    assert.equal(fs.readFileSync(file,'utf8'),prefix+content+suffix);
    assert.equal(current.hash,fileRevision(fs.readFileSync(file)));
  }
  assert.equal(verifications,1,'Only the initial read resolves event identity; subsequent saves own a hash-bound span');
  assert.throws(()=>verify(event),/已变化/,'Original identity is now stale');
  fs.appendFileSync(file,'// external\n');const external=fs.readFileSync(file,'utf8');
  assert.throws(()=>save({event,...current,content:'x=6;'},fs,()=>file,verify,replaceNativeEventRange,nativeEventBlock),/已改变/);
  assert.equal(fs.readFileSync(file,'utf8'),external,'A rejected save must leave external content untouched');
});
test('native-event ranges reject invalid bounds and resource headers',()=>{
  const source='x=1;\n',hash=fileRevision(source);
  for(const range of [{start:-1,end:2},{start:3,end:1},{start:0,end:99},{start:0.5,end:2}])assert.throws(()=>replaceNativeEventRange(source,range,'x=2;',hash),/范围无效/);
  assert.throws(()=>replaceNativeEventRange(source,{start:0,end:source.length},'#define Step_0\nx=2;',hash),/事件头/);
});
