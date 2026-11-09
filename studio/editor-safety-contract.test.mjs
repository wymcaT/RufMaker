// Source-contract checks supplement, but do not replace, the Windows tests in
// desktop/Test-ProjectLoadSafety.ps1. They keep the dangerous ordering visible
// on hosts that cannot run WinForms or FMOD.
import fs from 'node:fs';
import assert from 'node:assert/strict';
import test from 'node:test';
const read=name=>fs.readFileSync(new URL('../desktop/'+name,import.meta.url),'utf8');
const section=(text,start,end)=>text.slice(text.indexOf(start),text.indexOf(end,text.indexOf(start)));
test('project switches prepare audio before committing the bridge and discard old history first',()=>{
  const commands=read('ProjectCommands.cs'),main=read('MainForm.cs');
  const change=section(commands,'private async Task SwitchProject','private sealed class PreparedProject');
  assert(change.indexOf('await PrepareProjectLoad(loaded,candidate)')<change.indexOf('await LoadProject(loaded,prepared,candidate)'));
  assert(!change.includes('bridge=candidate'));
  const load=section(main,'private async Task LoadProject','private string ResourceFilter');
  assert(load.indexOf('ReturnToTimelineRoom(false)')<load.indexOf('bridge=nextBridge'));
  assert(load.indexOf('undo.Clear();redo.Clear();')<load.indexOf('bridge=nextBridge'));
  assert(load.indexOf('document=prepared.Document;state=document.state;')<load.indexOf('bridge=nextBridge'));
  assert(!load.includes('catch(Exception error){Error(error);}'));
});
test('failed saves do not auto-retry and close confirmation defaults to retaining changes',()=>{
  const main=read('MainForm.cs');
  const save=section(main,'private async Task<bool> SaveNow','private void BindSelection');
  assert(save.includes('bool saved=false'));assert(save.includes('FinishSaveAttempt(saved)'));
  assert(save.includes('if(saved&&dirty&&!closeInProgress&&!parameterScrubbing)saveTimer.Start();'));
  const close=section(main,'private async void OnClosing','internal sealed class CodeForm');
  assert(close.includes('MessageBoxDefaultButton.Button2'));assert(!close.includes('saveTimer.Start()'));
  const locale=JSON.parse(fs.readFileSync(new URL('../assets/locales/en.json',import.meta.url),'utf8'));
  assert(locale['保存失败。是否放弃尚未保存的更改并关闭？']);
});
