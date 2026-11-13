"""Source integration assertions; never a substitute for native UI acceptance."""
from pathlib import Path
import json,re
root=Path(__file__).resolve().parents[1]
def read(name):return (root/'desktop'/name).read_text(encoding='utf-8-sig')
canvas=read('TimelineCanvas.cs');model=read('TrackDisplayFilter.cs');fold=read('LaneGroupFolding.cs');main=read('MainForm.cs');spawn=read('SpawnTimeline.cs');shell=read('EditorShell.cs');checks=0
def check(ok,label):
 global checks
 checks+=1
 assert ok,label
check('trackFilter.VisibleItems(all,bound)' in canvas,'filter inside shared lane projection')
check('if(all.Count>0&&items.Count==0)continue;' in canvas,'fully hidden lanes not empty editable surrogates')
check('!DisplayEligible(target)' in spawn,'all bound template copies filtered')
check('AddRevealedDisplayRows();' in canvas and 'revealedDisplayTracks.Where(DisplayEligible)' in model,'fallback respects filtering')
check('State.tracks.Where(item=>item.kind!="native"&&item.lane==lane.index)' not in canvas,'lane header cannot select hidden objects')
check('KeyHiddenByProjection(k)' in canvas and 'KeyHiddenByProjection(key)' in canvas,'hit testing and box selection protect shared hidden key owner')
check('RefreshHiddenProjectionKeys();ReconcileFoldSelection();' in canvas and 'hiddenProjectionKeys.Contains(key)' in model,'key hit checks use cached projection rather than scanning all keys per candidate')
check(main.index('timeline.RevealLaneGroups(new[]{target});timeline.Expand(target,true);') < main.index('var key=Motion.Copy(item.key);'),'paste destination revealed before mutation')
check('KeyHiddenByProjection(k)' in fold and 'NotifySelection(primary,key)' in fold,'selection/inspector reconciliation through existing callback')
check('RevealFilteredTracks(targets);' in fold,'programmatic reveal uses exact owner closure')
check('TrackDisplayFilter.KeyOwners(State,targets)' in canvas,'every key owner revealed')
check('TimelineLayout.EnsureExclusive(state,timeline.ProtectedDisplayTracks().Concat(' in main,'programmatic edits prioritize unchanged hidden neighbors')
check('TrackDisplayFilter.WouldCutHidden' in canvas and 'protectedTracks.Concat(bars.Keys)' in canvas,'destructive overlap cut guarded')
check('trackFilter.Bind(state)' in canvas and 'revealedDisplayTracks.Clear();hiddenProjectionKeys.Clear();revealedReferenceId=null' in canvas,'document replacement resets identities')
check('HashSet<Track> hidden' in model and 'hidden.RemoveWhere' in model,'reference identity and same-state removal pruning')
check('previewHidden=' not in model and '.enabled=' not in model and 'PushUndo' not in model,'filter not scene visibility or persistent mutation')
check('FinishProjectionGesture();trackFilter.Bind(State);' in model,'finish old gesture before hide')
check('TrackDisplayMenu' in shell and 'HiddenTrackCount>0' in canvas,'View menu and visible hidden-count discovery')
check('timeline.HideTrackRows(timeline.SelectedTracks)' in model and 'timeline.ShowAllTrackRows()' in model and 'timeline.RevealTrackRow(track)' in model,'hide/list/reveal/show-all surfaces')
check('state==scene&&state.tracks.Contains(track)' in model,'stale menu cannot act on replacement document')
check('包括折叠或隐藏行' in fold and 'foreach(var t in ExpandedSelection())' in canvas,'intentional logical descendant operations preserved and disclosed')
check("'TrackDisplayFilter'" in read('Build-Native.ps1'),'explicit native build source included')
locale=json.loads((root/'assets/locales/en.json').read_text())
for text in ['时间轴物件行显示','隐藏所选物件的时间轴行（仅视图）','显示全部物件行（仅视图）','仅过滤编辑列表；场景与导出保留。切换或撤销工程后重置。','已保留隐藏物件；重叠片段改到空闲轨道。']+re.findall(r'Language.Text\("([^"\\]+)"\)',model):check(text in locale,'bilingual label: '+text)
print('PASS',checks,'track-display source contracts (not GUI execution)')
