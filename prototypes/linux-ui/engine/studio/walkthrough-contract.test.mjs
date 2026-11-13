import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
const source=readFileSync(new URL('../desktop/WalkthroughView.cs',import.meta.url),'utf8');
test('walkthrough is isolated from mutable editor commands',()=>{
 const viewer=source.split('internal sealed class WalkthroughView : Form')[1].split('internal sealed partial class MainForm')[0];
 for(const forbidden of ['NativeBridge','PushUndo','MarkChanged','SaveNow','TimelineCanvas','PresetDropped','InstanceMoved=','BeginEdit=','SelectTrack='])assert.ok(!viewer.includes(forbidden),forbidden);
 assert.match(viewer,/Enabled=false,AllowDrop=false/);assert.match(viewer,/new Dictionary<string,string>\(objects\)/);
 assert.match(viewer,/Keys.Control\|Keys.Alt/);assert.match(viewer,/Keys.F1&&keyData<=Keys.F12/);
 assert.match(viewer,/timer.Stop\(\);timer.Dispose\(\);clock.Stop\(\);assets.Dispose\(\)/);
});
test('native coverage is visible; filter is not persisted or applied to rendering',()=>{
 assert.match(source,/NativePreviewCoverage.Evaluate/);assert.match(source,/NativeFilmInfo=value.Film,NativeFilmIds=new HashSet<string>\(value.FilmIds\)/);assert.match(source,/coverage.Description/);
 assert.match(source,/State=value.Scene,Room=value.Room/);assert.match(source,/snapshot.Hide\(rows.SelectedItems.Cast<int>\(\).ToArray\(\)\)/);
 assert.match(source,/ShowDialog\(viewer,this\)/);assert.match(source,/finally\{if\(resumeSave&&!IsDisposed\)saveTimer.Start\(\);\}/);
 const build=readFileSync(new URL('../desktop/Build-Native.ps1',import.meta.url),'utf8');assert.match(build,/'WalkthroughView'/);
});
