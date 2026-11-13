// Run after ParentChildPresetTests. Reads isolated build fixtures only; no project import.
import fs from 'node:fs';import path from 'node:path';import assert from 'node:assert/strict';
import {normalizeState} from '../../studio/motion.mjs';
import {templateScripts} from '../../studio/template-system.mjs';
import {bulletSystemScripts} from '../../studio/bullet-system.mjs';
const folder=process.argv[2]||'build/parent-child';let checks=0;
const expected=['nested.json','after-move.json','action-time.json',...['emitter','object','rod','group'].flatMap(kind=>['child-burst','replacement-burst'].map(key=>`${kind}-${key}.json`))];
for(const name of expected){assert(fs.existsSync(path.join(folder,name)),`Missing generated fixture: ${name}; run ParentChildPresetTests first`);

 const state=normalizeState(JSON.parse(fs.readFileSync(path.join(folder,name),'utf8')));const scripts=s=>Object.fromEntries([...templateScripts(s),...bulletSystemScripts(s)]);const actual=scripts(state);
 const plain=JSON.parse(JSON.stringify(state));for(const t of plain.tracks)for(const a of [...t.bulletActions,...t.spawnActions])if(['child-burst','replacement-burst'].includes(a.generationPreset))a.generationPreset='';
 assert.deepEqual(actual,scripts(normalizeState(plain)),name+': preset remains ordinary native spawn semantics');checks++;
 for(const t of state.tracks)for(const a of [...t.bulletActions,...t.spawnActions])if(a.type==='spawn')assert(state.tracks.some(c=>c.id===a.templateId));checks++;
 if(name==='action-time.json'){const g=actual.rf_pattern_tick;assert(g.indexOf('rf_template_bullet_action(rf_world,0)')<g.indexOf('if !rf_action_stop && (rf_age>0'));assert.match(actual.rf_template_bullet_action,/rf_inst=rf_template_spawn[\s\S]*if instance_exists\(rf_inst\) \{[\s\S]*rf_dead/);checks++;}
 if(name==='after-move.json'){assert.match(actual.rf_template_bullet_actions,/rf_inst=rf_template_spawn[\s\S]*if instance_exists\(rf_inst\) \{[\s\S]*rf_dead/);assert.doesNotMatch(actual.rf_template_bullet_action,/rf_inst=rf_template_spawn/);checks++;}
 if(name==='nested.json'){assert.equal(state.tracks[0].bulletActions[0].maxGeneration,4);checks++;}
}
const ui=fs.readFileSync('desktop/BulletBehaviorForm.cs','utf8');assert.match(ui,/Actions=\(templateMode\?track.spawnActions:track.bulletActions\).Select\(a=>a.Snapshot\(\)\)/);assert.match(ui,/DialogResult=DialogResult.Cancel/);checks+=2;
const locale=JSON.parse(fs.readFileSync('assets/locales/en.json','utf8'));for(const k of ['母子弹 · 延迟发射','母子弹 · 延迟替换','关联子模板','模板已缺失；请在展开编辑中重新选择。','请先将生成执行时点改为移动后，再切换到发射器本体。']){assert(locale[k]);checks++;}
console.log(`PASS ${checks} generated GML/link/localization/source-contract checks (not a GM runner or GUI test)`);
