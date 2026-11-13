import assert from 'node:assert/strict';
import {test} from 'node:test';
import {patchNativeSurfaceReset} from './native-compositor.mjs';

test('redirect actual native surface resets, preserving comments, quoted code and repeated sync',()=>{
  const source=`// surface_reset_target();\ntext="surface_reset_target()";\nsurface_reset_target /* restore native view */ ();\n`;
  const patched=patchNativeSurfaceReset(source);
  assert.equal(patched,`// surface_reset_target();\ntext="surface_reset_target()";\nrf_native_surface_reset /* restore native view */ ();\n`);
  assert.equal(patchNativeSurfaceReset(patched),patched);
});
