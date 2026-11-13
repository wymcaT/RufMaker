// Source contracts only. Run the Windows UI checks before claiming interactive validation.
import fs from 'node:fs';
import assert from 'node:assert/strict';
import test from 'node:test';
const shell=fs.readFileSync(new URL('../desktop/EditorShell.cs',import.meta.url),'utf8');
const section=(start,end)=>shell.slice(shell.indexOf(start),shell.indexOf(end,shell.indexOf(start)));
test('single selection browses sprites without changing tracks',()=>{
  const select=section('resources.SelectedIndexChanged+=','resources.DoubleClick+=');
  assert.match(select,/if\(resourceKind=="sprites"\)SetStatus/);
  assert.match(select,/else if\(resourceKind!="rooms"\)SelectResource\(\)/);
  assert(!select.includes('ApplySprite('));
  assert(!select.includes('ApplyBrowsedSprite('));
  assert(select.includes('resourceName=null;RefreshResourceActions();return;'));
});
test('explicit apply validates live list selection and explains batch scope',()=>{
  const apply=section('private void ApplyBrowsedSprite()','private void BuildShellResources');
  assert(apply.includes('busy||state==null||resourceKind!="sprites"||resources.SelectedItems.Count==0'));
  assert(apply.includes('ApplySprite(item.Tag as string??item.Text)'));
  const refresh=section('private void RefreshResourceActions()','private void ApplyBrowsedSprite()');
  assert(refresh.includes('EligibleSpriteTargets().Length'));
  assert(shell.includes('timeline.SelectedTracks.Count==0&&selectedTrack!=null&&Theme.Visual(selectedTrack.kind)'));
  assert(refresh.includes('非可视轨道不受影响'));
  assert(!refresh.includes('resourceName!=null')); // Track selection may clear this field.
  assert(shell.includes('Tool("应用贴图到所选（{0}）",ApplyBrowsedSprite)'));
  assert(shell.includes('if(resourceKind=="sprites"){ApplyBrowsedSprite();return;}'));
});
test('room browsing and preset drag/activation retain existing paths',()=>{
  assert(shell.includes('await ActivatePresetResource()'));
  assert(shell.includes('if(resourceKind=="rooms")await BrowseRoom(resourceName)'));
  assert(shell.includes('IsPresetKind(resourceKind)?PresetDragText()'));
  assert(shell.includes('resources.DoDragDrop(payload,DragDropEffects.Copy)'));
});
test('tutorial search is read-only, wraps, and offers keyboard navigation',()=>{
  const tutorial=section('private void OpenLocalReadme()','private void RefreshResourceActions()');
  assert(tutorial.includes('ReadOnly=true,HideSelection=false'));
  assert(tutorial.includes('if(at<0&&start>0)'));
  for(const key of ['Keys.F','Keys.F3','Keys.Enter']) assert(tutorial.includes(key));
  assert(tutorial.includes('content.ScrollToCaret()'));
  assert(!tutorial.includes('File.Write'));
});
