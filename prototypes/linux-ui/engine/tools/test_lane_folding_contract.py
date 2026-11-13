"""Source wiring checks supplement pure model tests, not native interaction QA."""
from pathlib import Path
import re,json
root=Path(__file__).resolve().parents[1]
def read(name):return (root/'desktop'/name).read_text(encoding='utf-8-sig')
canvas=read('TimelineCanvas.cs');fold=read('LaneGroupFolding.cs');main=read('MainForm.cs');groups=read('TimelineGroups.cs');spawn=read('SpawnTimeline.cs');audio=read('TimelineAudio.cs');checks=0
def check(ok,label):
 global checks
 checks+=1
 assert ok,label
check('foreach(var projected in LaneGroupProjection.Build(State,collapsedLaneGroups))' in canvas,'one shared projected lane list')
check('summaryGroup=projected.group' in canvas and 'public int index=-1' in canvas,'summary never adopts hidden index')
check('lane.summaryGroup!=null){DrawLaneSummary' in canvas,'summary paint exits before editable bar drawing')
check('if(lane.summaryGroup!=null)return "lane-group:"' in audio,'summary height stable identity')
check('if(lane!=null&&lane.summaryGroup!=null){ActiveLane=-1;return;}' in canvas,'drop rejects summary before callback')
check('lane.summaryGroup!=null)e.Effect=DragDropEffects.None' in canvas,'drag-over feedback rejects summary')
check('SummaryGroup!=null){ActiveLane=-1;ResetSelection(null,null);NotifySelection(null,null);' in canvas,'summary click clears stale selection and lane')
check('if(timeline.SummaryGroup!=null){LaneFoldingMenu(menu.Items);Language.Attach(menu);return;}' in groups,'summary context only group commands')
check('if(state==null||timeline.SummaryGroup!=null)return;CommitInspectorEdits();' in main,'keyboard/menu paste rejects summary')
check('track.lane=timeline.SummaryGroup!=null?state.laneCount:' in main,'global create from summary uses new lane, not hidden representative')
check('dragRowLanes[row]<0)return;' in canvas,'summary bar target rejected')
check('LaneGroupProjection.CanMoveIndividualLanes' in canvas and canvas.index('LaneGroupProjection.CanMoveIndividualLanes')<canvas.index('if(!dragChanged){if(BeginEdit'),'all explicit drag targets checked before undo')
check('DisplayEligible(o)' in canvas,'clip snapping ignores hidden clips')
check('timeline.SelectVisibleTracks();else timeline.SelectAllKeys' in main,'Ctrl+A keeps key workspace path')
select=fold.split('public void SelectVisibleTracks()',1)[1].split('private void DrawLaneSummary',1)[0]
check('NotifySelection' in select and 'SetTrackSelection' not in select,'visible Ctrl+A does not expand hidden source groups')
check('if(target==null||!DisplayEligible(target))continue;' in spawn,'shared template cards respect actual folded membership')
check('if(changed)NotifySelection(primary,key)' in fold and 'HiddenByFoldProjection' in fold,'collapse goes through main selection synchronization')
check('if(!timeline.NotifyingSelection)timeline.ResetSelection(track,key)' in main,'collapse callback does not re-reveal primary')
check('AbortProjectionGesture();collapsedLaneGroups.Clear();foldedGroupObjects.Clear();' in canvas,'replacement aborts gestures and clears fold IDs')
check('original!=current[0]' in fold,'same-state ID reuse cannot inherit fold')
check('TrackDisplayFilter.KeyOwners(State,targets).Concat(new[]{primaryTrack})' in canvas,'multi-owner key selection reveals all owners')
check('LaneGroupProjection.UniqueTrack(state,item.trackId)' in main,'key paste skips ambiguous ID')
check('timeline.RevealLaneGroups(new[]{target});timeline.Expand(target,true);' in main,'key paste reveals every destination')
check('SetLaneGroupCollapsed(group,false);SetTrackSelection(LaneGroupProjection.Members' in fold,'whole group selection expands first')
check('collapsedLaneGroups' in read('WorkspaceView.cs') and 'collapsedLaneGroups' not in read('EditorCore.cs'),'view persistence isolated from scene')
check("'LaneGroupFolding'" in read('Build-Native.ps1'),'product explicit source list includes implementation')
check('foreach(var t in ExpandedSelection())' in canvas,'logical descendant edit closure preserved')
check('CommitInspectorEdits();timeline.SetLaneGroupCollapsed' in fold,'pending inspector commit before fold')
locale=json.loads((root/'assets/locales/en.json').read_text())
for key in re.findall(r'Language.Text\("([^"\\]+)"\)',fold):check(key in locale,'English entry: '+key)
print('PASS',checks,'source contracts (not GUI execution)')
