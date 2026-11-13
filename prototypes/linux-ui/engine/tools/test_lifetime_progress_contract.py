"""Static lifetime progress wiring/locale contracts. Never executes native UI."""
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
source = (ROOT / 'desktop/SimulatedLifetimeView.cs').read_text(encoding='utf-8-sig')
tests = (ROOT / 'desktop/tests/SimulatedLifetimeUiTests.cs').read_text(encoding='utf-8-sig')
pairs = json.loads((ROOT / 'assets/locales/en.json').read_text(encoding='utf-8-sig'), object_pairs_hook=list)
locale = dict(pairs)
checks = 0


def check(condition, label):
    global checks
    checks += 1
    if not condition:
        raise AssertionError(label)


check(len(locale) == len(pairs), 'locale has no duplicate keys')
check('new Progress<' not in source and 'BeginInvoke' not in source and 'SynchronizationContext' not in source,
      'worker reports never create UI-post backlog')
mailbox = source.split('internal sealed class LifetimeProgressMailbox', 1)[1].split('internal sealed class LifetimeProgressState', 1)[0]
check('lock (sync)' in mailbox and 'latest = value;' in mailbox and 'Queue<' not in mailbox and 'List<' not in mailbox,
      'mailbox stores one synchronized latest immutable payload')
check('if (closed ||' in mailbox and 'value.LastCompletedFrame < latest.LastCompletedFrame' in mailbox,
      'late and regressive payloads are dropped')
check('new LifetimeProgress(-1, requestedEnd, 0, maxSeconds, -1, "prepare")' in mailbox,
      'initial progress never marks frame0 completed')
check('cancelling = true' in mailbox and 'cancelling = false' not in mailbox,
      'cancellation acknowledgement is monotonic for one request')
check('string stage = value.Cancelling ?' in source, 'cancel text wins over later heartbeat phase')
check('Progress.LastCompletedFrame + 1' in source and 'Progress.RequestedEnd + 1' in source,
      'inclusive completed/requested totals use complete frames only')
check('ActiveFrame > progress.LastCompletedFrame' in source and '正在处理第 {0} 帧（尚未完成）…' in source,
      'in-flight frame is explicitly uncompleted')
check('Interval=200' in source and 'else RefreshProgress()' in source, 'UI timer polls at bounded frequency')
check('Task.Run(()=>analyze(snapshot,options,tokenSource.Token,mailbox.Report))' in source,
      'worker receives only immutable input/options/token and mailbox callback')
check('new LifetimeProgressMailbox(options.EndFrame,options.MaxSeconds)' in source,
      'each run creates its own mailbox')
completion = source.split('var completed=await', 1)[1].split('}catch(Exception error)', 1)[0]
check(completion.index('gate.Accept(request,true)') < completion.index('result=completed'),
      'generation gate rejects stale completion before assignment')
check(completion.index('if(!current())') < completion.index('result=completed'),
      'completion verifies source revision before assignment')
finalization = source.split('finally{\n            mailbox.Close();tokenSource.Dispose();', 1)[1].split('private void Populate()', 1)[0]
check('ReferenceEquals(cancellation,tokenSource)' in finalization and 'ReferenceEquals(progressMailbox,mailbox)' in finalization and 'activeRequest==request' in finalization,
      'old finally cannot clear replacement run state')
invalidation = source.split('private void InvalidateResult()', 1)[1].split('private async void BeginAnalysis()', 1)[0]
check('gate.Invalidate()' in invalidation and 'progressMailbox.Close()' in invalidation and 'progressBar.Value=0' in invalidation,
      'scene invalidation drops callbacks and clears observed progress')
close = source.split('private void CloseAnalysis()', 1)[1].split('protected override void OnFormClosing', 1)[0]
check('closing=true' in close and 'gate.Close()' in close and 'progressMailbox.Close()' in close and 'cancellation.Cancel()' in close,
      'close invalidates all progress/completion and requests cancellation')
check('base.OnFormClosing(e);if(!e.Cancel)CloseAnalysis();' in source,
      'a cancelled close leaves request usable')
check('maxSeconds < 180' in source and 'value.Stop == "time-limit" || value.Stop == "work-limit" || value.Stop == "sample-limit"' in source,
      'increase-budget hint never promises to fix fixed caps or exceed maximum')
check('Minimum=5,Maximum=180,Increment=15,Value=15' in source,
      'original explicit time controls and default15 preserved')
check('不执行原生 GML' in source and '部分观察，后续未知' in source,
      'native and partial limitations remain explicit')
check('status.MaximumSize=' in source and 'RowStyles.Add(new RowStyle(SizeType.AutoSize))' in source,
      'progress text can wrap independently of toolbar')
pure = tests.split('private static void Pure(string output)', 1)[1].split('private static void Native(string output)', 1)[0]
check('new SimulatedLifetimeView' not in pure and 'Application.' not in pure and 'Language.Select' not in pure,
      'pure tests do not instantiate controls or enumerate open forms')
check('if(pure){Pure(output);return;}' in tests and 'PASS "+checks+" pure simulated lifetime progress checks' in tests,
      'pure execution entry is explicit and distinguishable from native suite')
keys = set(re.findall(r'Language\.Text\("((?:\\.|[^"\\])*)"\)', source))
keys.add(re.search(r'internal const string ScopeNote="([^"]+)";', source).group(1))
for key in sorted(keys):
    check(key in locale, 'English key exists: ' + key)
    tokens = lambda text: sorted(re.findall(r'\{\d+(?:,[^}:]+)?(?::[^}]+)?\}', text))
    check(tokens(key) == tokens(locale[key]), 'translation preserves format placeholders: ' + key)

print('PASS ' + str(checks) + ' lifetime progress source contracts (not GUI execution)')
