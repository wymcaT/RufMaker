import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
test('safe UI integration keeps visual blend editor and field categories',()=>{
 const form=fs.readFileSync(new URL('../desktop/MainForm.cs',import.meta.url),'utf8');
 assert.match(form,/Field\(bag,"混合模式","外观",\(\)=>track.blendMode,value=>track.blendMode=value,new\[\]\{"normal","add"\}\)/);
 assert.match(form,/if\(track.rainbow&&!Rainbow.ExplicitFrame\(track\)\)bag.Add/);
 const shell=fs.readFileSync(new URL('../desktop/EditorShell.cs',import.meta.url),'utf8');
 assert.match(shell,/new\[\]\{"x","y","scale","angle","cameraZoom","cameraViewWidth","cameraViewHeight"\}\.Contains\(property\)\)return "变换"/);
 assert.match(shell,/new\[\]\{"alpha","imageAngle","spriteFrame","scaleX","scaleY"\}\.Contains\(property\)\)return "外观"/);
 const motion=fs.readFileSync(new URL('../desktop/Motion.cs',import.meta.url),'utf8');for(const field of ['spriteFrame','scaleX','scaleY','cameraViewWidth','cameraViewHeight'])assert.ok(motion.includes('"'+field+'"'));
});
