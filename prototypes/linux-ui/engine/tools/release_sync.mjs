// Adapted from v029 verify_recovered.mjs: no project module is loaded before isolation.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import {pathToFileURL} from 'node:url';

const [engineArg, projectArg, entryArg] = process.argv.slice(2);
assert(engineArg && projectArg && entryArg, 'Expected engine, isolated project and entry');
const engine = fs.realpathSync(engineArg), project = fs.realpathSync(projectArg);
const entry = fs.realpathSync(path.resolve(project, entryArg));
const relativeEntry = path.relative(project, entry);
assert(relativeEntry && relativeEntry !== '..' && !relativeEntry.startsWith('..'+path.sep)
  && !path.isAbsolute(relativeEntry) && fs.statSync(entry).isFile(), 'Entry must stay inside isolated project');
process.env.RUFMAKER_ROOT = engine;
process.env.RUFMAKER_PROJECT = entry;
const sha = data => crypto.createHash('sha256').update(data).digest('hex');
function snapshot(folder) {
  const result = {};
  function visit(dir) {
    for (const row of fs.readdirSync(dir, {withFileTypes: true}).sort((a,b)=>a.name.localeCompare(b.name))) {
      const file = path.join(dir, row.name);
      assert(!row.isSymbolicLink(), 'Symlink in isolated project: '+file);
      if (row.isDirectory()) visit(file);
      else if (row.isFile()) result[path.relative(folder,file)] = sha(fs.readFileSync(file));
    }
  }
  visit(folder);
  return result;
}
const before = snapshot(project);
const api = await import(pathToFileURL(path.join(engine, 'studio/project.mjs')));
const {normalizeState} = await import(pathToFileURL(path.join(engine, 'studio/motion.mjs')));
let previous;
for (let pass = 1; pass <= 3; pass++) {
  const state = normalizeState(api.readState());
  api.validateState(state);
  api.syncToGame(state);
  const current = snapshot(project);
  // A released project must already be synchronized, not merely settle after mutation.
  assert.deepEqual(current, before, 'Clean recovered project changed on sync '+pass);
  if (previous) assert.deepEqual(current, previous, 'Repeated sync changed bytes');
  previous = current;
}
console.log(JSON.stringify({status:'pass', passes:3, allProjectFilesByteIdentical:true, files:Object.keys(before).length}));
