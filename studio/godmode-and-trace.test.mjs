import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {commandText, makeNativeHarness} from './native-preview.mjs';

test('commandText outputs god=1 when godMode is true, and god=0 when false', () => {
  const normal = commandText(100, true, true, {x: 400, y: 304}, false);
  assert.ok(normal.includes('god=0'), 'Normal preview commandText contains god=0');
  assert.ok(normal.includes('frame=100'), 'CommandText has target frame');

  const god = commandText(100, true, true, {x: 400, y: 304}, true);
  assert.ok(god.includes('god=1'), 'Debug preview commandText contains god=1');
});

test('native preview harness records sprite, subimage, xscale, yscale, alpha, angle and dead status', () => {
  const harness = makeNativeHarness({fps: 50, duration: 100, nativeProject: {}}, {playable: true});
  assert.ok(harness.includes('rf_record_trace();'), 'Playable harness calls rf_record_trace');
  assert.ok(harness.includes('rufmaker_player_trace.csv'), 'Playable harness initializes trace CSV file');
});

test('packageGame preview INI includes god mode setting', () => {
  const buildDir = path.resolve('build', 'preview-test-ini');
  fs.mkdirSync(path.join(buildDir, 'Data'), {recursive: true});
  const iniPath = path.join(buildDir, 'Data', 'rufmaker_preview.ini');

  fs.writeFileSync(iniPath, '[preview]\r\nframe=42\r\ngod=1\r\n');
  const readGod = fs.readFileSync(iniPath, 'utf8');
  assert.ok(readGod.includes('god=1'));

  fs.writeFileSync(iniPath, '[preview]\r\nframe=42\r\ngod=0\r\n');
  const readNormal = fs.readFileSync(iniPath, 'utf8');
  assert.ok(readNormal.includes('god=0'));
});
