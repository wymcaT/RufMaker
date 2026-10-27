import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import {execFileSync} from 'node:child_process';
import {createProject} from '../../studio/project-files.mjs';

const root=path.resolve(import.meta.dirname,'../..'),base=path.join(root,'build/lane-exclusivity-check');
const made=createProject(root,{name:'Lane Sync '+Date.now(),parent:base,preset:'floor',width:800,height:608,fps:50});
process.env.RUFMAKER_ROOT=root;process.env.RUFMAKER_PROJECT=made.path;
const project=await import('../../studio/project.mjs');
const fixture=JSON.parse(fs.readFileSync(path.join(base,'migrated.json'),'utf8'));
fixture.preserveTrackOrder=false;for(const track of fixture.tracks)if(!track.templateOnly)track.lane=0;
const source={...project.readState(),...fixture,timelineInitialized:true};
fs.writeFileSync(project.makerFile,JSON.stringify(source));
const original=project.readState();project.syncToGame(original);
const generated=()=>{
  const scripts=path.join(project.project,'scripts');
  return Object.fromEntries(fs.readdirSync(scripts).filter(name=>/^rf_.*\.gml$/.test(name)).sort().map(name=>[name,fs.readFileSync(path.join(scripts,name),'utf8')]).concat([['objRufController.gml',fs.readFileSync(path.join(project.project,'objects','objRufController.gml'),'utf8')]]));
};
const before=generated(),input=path.join(base,'before-migration.json'),output=path.join(base,'after-migration.json');
fs.writeFileSync(input,JSON.stringify(original));
execFileSync(path.join(base,'LaneExclusivity.tests.exe'),['--migrate',input,output],{windowsHide:true});
const migrated=JSON.parse(fs.readFileSync(output,'utf8'));project.syncToGame(migrated);
const saved=project.readState();
assert.deepEqual(generated(),before,'Regenerated GM code changed after moving clips to lanes');
assert.deepEqual(saved.tracks.map(t=>t.id),original.tracks.map(t=>t.id),'Execution order changed');
assert.equal(saved.preserveTrackOrder,true);
for(const old of original.tracks){const next=saved.tracks.find(t=>t.id===old.id);assert.deepEqual([next.start,next.end,next.parentId],[old.start,old.end,old.parentId]);}
const visible=saved.tracks.filter(t=>!t.templateOnly&&t.kind!=='native');
for(const a of visible)for(const b of visible)if(a!==b&&a.lane===b.lane)assert.ok(a.end<b.start||b.end<a.start,`${a.id} overlaps ${b.id}`);
console.log(`PASS: isolated GM project save/reopen separates ${visible.length} clips and preserves execution order, times, parent references and generated GML. ${project.projectFile}`);
