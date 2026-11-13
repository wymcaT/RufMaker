import fs from 'node:fs';
import test from 'node:test';
import assert from 'node:assert/strict';
const main=fs.readFileSync(new URL('../desktop/MainForm.cs',import.meta.url),'utf8');
const part=(a,b)=>main.slice(main.indexOf(a),main.indexOf(b,main.indexOf(a)));
test('autosave failure stays dirty, does not pop a modal, and offers retry',()=>{
 const save=part('private async Task<bool> SaveNow','private void BindSelection');
 assert(save.includes('dirty=true;saveFailure=error.Message'));
 assert(save.includes('if(notify)Language.Message'));
 assert(save.includes('saved=true;saveFailure=null'));
 assert(save.includes('saving=false;saveGate.Release()'));
 assert(main.includes('saveRetry.Click+=async(s,e)=>await SaveNow(true)'));
 assert(main.includes('if(await SaveNow(false))SetStatus('));
});
test('GML save retains editor, snapshots submitted text, guards close and concurrent saves',()=>{
 const save=part('private async Task<bool> Save()','protected override bool ProcessCmdKey');
 assert(save.includes('if(saving)return false'));
 assert(save.includes('string submitted=code.Text'));
 assert(save.includes('savedText=submitted;saveFailed=false;return true'));
 assert(!save.slice(0,save.indexOf('protected override void')).includes('Close()'));
 assert(save.includes('MessageBoxButtons.YesNoCancel'));
 assert(save.includes('saveFailed?'));
 assert(save.includes('saveFailed=true;Language.Message'));
 assert(save.includes('已提交的修改可能仍保留在主工程'));
 assert(save.includes('if(await Save()&&code.Text==savedText)'));
 assert(main.includes('if(!await SaveNow(false))throw new IOException'));
});
test('export preserves backup path and distinguishes completed export from folder-open failure',()=>{
 const exp=part('private async Task ExportGame()','private void ResolveUndoTracks');
 assert(exp.includes('if(!await SaveNow(false))return'));
 assert(exp.includes('lastExportBackup=result.previousDir'));
 assert(exp.includes('exportBackup.Visible=!String.IsNullOrEmpty(lastExportBackup)'));
 assert(exp.includes('导出已完成，但无法打开目录：'));
 assert(!exp.includes('ProgressBar'));
});

test('busy and save transitions share retry state refresh',()=>{
 const shell=fs.readFileSync(new URL('../desktop/EditorShell.cs',import.meta.url),'utf8');
 assert(shell.includes('saveRetry.Visible=saveFailure!=null;saveRetry.Enabled=!saving&&!busy;'));
 assert(part('private void SetBusy','private async Task RunGame').includes('RefreshShell()'));
 assert(part('private async Task<bool> SaveNow','private void FinishSaveAttempt').includes('FinishSaveAttempt(saved);RefreshShell()'));
});
