"""Headless trace wiring contracts and exact-method flow probe.
No MainForm/WinForms construction, process lookup, native game or display runs.
The extracted methods retain their source bodies; UI and document holders are stubs.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import sys

from run_timeline_checks import Executor, toolchain


def body(source, signature):
    start = source.index(signature)
    opening = source.index('{', start)
    depth = 1
    end = opening + 1
    while depth:
        if source[end] == '{':
            depth += 1
        elif source[end] == '}':
            depth -= 1
        end += 1
    return source[start:end]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--mono-root')
    parser.add_argument('--out', '--output', dest='out', type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    out = (args.out or root / 'build/player-trace-contract-check').resolve()
    out.mkdir(parents=True, exist_ok=True)
    trace = (root / 'desktop/PlayerTrace.cs').read_text()
    main_source = (root / 'desktop/MainForm.cs').read_text()
    core = (root / 'desktop/EditorCore.cs').read_text()
    checks = 0

    def check(value, message):
        nonlocal checks
        if not value:
            raise AssertionError(message)
        checks += 1

    load = body(main_source, 'private async Task LoadProject(')
    run = body(main_source, 'private async Task RunGame(')
    build = body(trace, 'private void BuildTraceTools(')
    poll = body(trace, 'private void PollLivePlayerTrace(')
    finish = body(trace, 'private void FinishPlayerTrace(')
    manual = body(trace, 'private void ImportPlayerTrace(')
    check(load.index('await PrepareProjectLoad(') < load.index('StopWatchingPlayerTrace();') < load.index('document=prepared.Document;state=document.state;'), 'cancel watcher only at committed project replacement, after preparation')
    check(run.index('BeginPlayerTraceRequest()') < run.index('await EnsureEngine()'), 'request identity captured before async engine setup')
    check(run.index('if(!IsCurrentPlayerTrace(traceRequest)){StopWatchingPlayerTrace(traceRequest);return;}') < run.index('new NativeReplayProgressForm('), 'reject replaced engine-setup callback')
    check(run.index('if(!IsCurrentPlayerTrace(traceRequest))return;', run.index('dialog.Result')) < run.index('if(current==revision)dirty=false'), 'reject stale modal result before dirty/state mutation')
    check('WatchPlayerTrace(result,traceRequest)' in run, 'watch receives the originating request')
    check('finally{if(!watchingTrace)StopWatchingPlayerTrace(traceRequest);SetBusy(false);}' in run, 'cancel and failure retire only their own request')
    check('if(IsCurrentPlayerTrace(traceRequest))AppWindow.FocusGame(gamePid)' in run, 'queued focus callback checks identity')
    check(build.index('if(!IsCurrentPlayerTrace(request))') < build.index('Process.GetProcessById(request.Pid)'), 'timer checks context before any process or data access')
    check('if(running)PollLivePlayerTrace(request);' in build and 'else FinishPlayerTrace(request);' in build, 'timer branches use captured request')
    check('FormClosed+=(s,e)=>{StopWatchingPlayerTrace();traceWatch.Dispose();}' in build, 'close retires pending and active requests')
    check('if(!tracePreview.Checked)StopWatchingPlayerTrace();' in build, 'uncheck retires watch without changing native process')
    check(poll.count('IsCurrentPlayerTrace(request)') == 2, 'poll checks ownership before and after IO')
    check('PlayerTrace.Same(state.playerTrace,points)' in poll and 'points.Count!=state.playerTrace.Count' not in poll, 'poll compares all values, not point count')
    check('finally{traceRequests.Cancel(request);}' in finish, 'finished request cannot stop a newer watch')
    check('points=request.LastComplete' in finish, 'partial final write retains last complete session snapshot')
    check(manual.index('ReferenceEquals(document,originalDocument)') > manual.index('dialog.ShowDialog(this)'), 'manual modal import rechecks document identity')
    check(manual.index('ReferenceEquals(state,originalState)') > manual.index('dialog.ShowDialog(this)'), 'manual modal import rechecks state identity')
    check(manual.index('points.Count==0') < manual.index('StopWatchingPlayerTrace();'), 'cancelled or invalid import preserves active session')
    check('Kill(' not in trace and 'game_restart' not in trace, 'watch retirement never controls native game process')
    check('PlayerTraceRequests' not in core and 'PlayerTraceSession' not in core, 'request state is not serialized into scene')

    pure = trace[trace.index('internal static class PlayerTrace'):trace.index('internal sealed partial class MainForm')]
    point = body(core, 'internal sealed class PlayerTracePoint')
    methods = '\n'.join(body(trace, signature) for signature in [
        'private bool IsCurrentPlayerTrace(', 'private PlayerTraceSession BeginPlayerTraceRequest(',
        'private void StopWatchingPlayerTrace(', 'private void PollLivePlayerTrace(',
        'private bool WatchPlayerTrace(', 'private void FinishPlayerTrace(',
        'private void ApplyPlayerTrace(', 'private void ImportPlayerTrace('])
    toggle = body(build, 'tracePreview.CheckedChanged+=(s,e)=>')
    toggle = toggle[toggle.index('{'):]
    stub = r'''
internal sealed class EditorState {public List<PlayerTracePoint> playerTrace=new List<PlayerTracePoint>();public bool usePlayerTrace;}
internal sealed class LoadData {public string project;}
internal sealed class BuildResult {public int pid;public string dir;}
internal sealed class TraceTimer {public bool Active;public void Start(){Active=true;}public void Stop(){Active=false;}}
internal sealed class TraceCheck {public bool Checked;}
internal sealed class TraceStage {public int Calls;public void SceneChanged(){Calls++;}}
internal enum DialogResult {OK,Cancel}
internal sealed class OpenFileDialog:IDisposable {
 public string Title,Filter,InitialDirectory,FileName;
 internal static DialogResult Result;internal static string NextPath;internal static Action During;
 public DialogResult ShowDialog(object owner){if(During!=null)During();FileName=NextPath;return Result;}
 public void Dispose(){}
}
internal sealed class MainForm {
 private EditorState state=new EditorState();private LoadData document=new LoadData{project="A"};
 private TraceTimer traceWatch=new TraceTimer();private TraceCheck tracePreview=new TraceCheck();
 private TraceStage stage=new TraceStage();private PlayerTraceRequests traceRequests=new PlayerTraceRequests();private string tracePath;
 private int undo,dirty,status,errors;private void PushUndo(){undo++;}private void MarkChanged(){dirty++;}
 private void SetStatus(string value){status++;}private void Error(Exception value){errors++;}
 METHODS
 private void Toggle() TOGGLE
 private static int checks;private static void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
 private static MainForm New(){return new MainForm();}
 private static PlayerTraceSession Start(MainForm f,string dir){var r=f.BeginPlayerTraceRequest();Check(f.WatchPlayerTrace(new BuildResult{pid=12,dir=dir},r),"watch starts only for originating request");return r;}
 private static void ResetDialog(){OpenFileDialog.Result=DialogResult.Cancel;OpenFileDialog.NextPath=null;OpenFileDialog.During=null;}
 public static int Main(string[] args){
  string dir=Path.Combine(args[0],Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(dir,"Data"));string path=Path.Combine(dir,"Data","rufmaker_player_trace.csv");
  try{
   var f=New();var a=Start(f,dir);File.WriteAllText(path,"0,100,200\n1,110,210\n");f.PollLivePlayerTrace(a);
   Check(f.state.playerTrace.Count==2&&f.state.playerTrace[0].x==100&&f.stage.Calls==2,"live poll applies complete originating trace");
   Check(f.undo==0&&f.dirty==0,"live refresh does not add history or schedule saves");
   f.PollLivePlayerTrace(a);Check(f.stage.Calls==2,"unchanged poll does not invalidate cache");
   File.WriteAllText(path,"0,700,500\n1,710,510\n");f.PollLivePlayerTrace(a);
   Check(f.state.playerTrace[0].x==700&&f.stage.Calls==3,"same count rewrite refreshes actual method");
   File.WriteAllText(path,"0,700,500,sprPlayerIdle,1,1,1,0.2,20,0\n1,710,510,sprPlayerFall,2,-1,1,1,0,0\n");f.PollLivePlayerTrace(a);
   Check(f.state.playerTrace[0].alpha==.2&&f.state.playerTrace[1].sprite=="sprPlayerFall"&&f.stage.Calls==4,"same count appearance-only rewrite refreshes");
   File.WriteAllText(path,"0,900,500\n1,910,51");f.PollLivePlayerTrace(a);
   Check(f.state.playerTrace[0].x==700&&f.stage.Calls==4,"partial write leaves last complete state and cache untouched");
   f.document=new LoadData{project="B"};f.state=new EditorState();f.PollLivePlayerTrace(a);f.FinishPlayerTrace(a);
   Check(f.state.playerTrace.Count==0&&!f.state.usePlayerTrace&&f.stage.Calls==4&&f.undo==0&&f.dirty==0,"old project poll and exit have no state/history/cache effects");
   var b=Start(f,dir);f.StopWatchingPlayerTrace(a);f.FinishPlayerTrace(a);f.PollLivePlayerTrace(a);
   Check(f.IsCurrentPlayerTrace(b)&&f.traceWatch.Active,"old cancel/exit/poll cannot retire new watcher");
   var samePath=f.document.project;f.document=new LoadData{project=samePath};f.state=new EditorState();f.PollLivePlayerTrace(b);f.FinishPlayerTrace(b);
   Check(f.state.playerTrace.Count==0&&f.undo==0&&f.dirty==0,"same-path document reload rejects old trace and exit");
   var c=Start(f,dir);f.state=new EditorState();f.PollLivePlayerTrace(c);f.FinishPlayerTrace(c);
   Check(f.state.playerTrace.Count==0&&f.undo==0&&f.dirty==0,"undo-like state replacement rejects old trace");
   var d=Start(f,dir);f.StopWatchingPlayerTrace();f.PollLivePlayerTrace(d);f.FinishPlayerTrace(d);
   Check(!f.traceWatch.Active&&f.traceRequests.Current==null&&f.tracePath==null,"committed load stop clears ephemeral watcher and remembered path");
   Check(!f.WatchPlayerTrace(new BuildResult{pid=99,dir=dir},d),"late build result cannot rebind old request");
   File.WriteAllText(path,"0,100,200\n");var e=Start(f,dir);f.PollLivePlayerTrace(e);f.FinishPlayerTrace(e);
   Check(!f.traceWatch.Active&&f.traceRequests.Current==null&&f.undo==1&&f.dirty==1&&f.status==1,"normal finish commits exactly once and retires request");
   f.FinishPlayerTrace(e);Check(f.undo==1&&f.dirty==1,"duplicate finish has no side effects");
   var g=New();var g1=Start(g,dir);g.PollLivePlayerTrace(g1);File.WriteAllText(path,"0,900,50");g.FinishPlayerTrace(g1);
   Check(g.state.playerTrace[0].x==100&&g.dirty==1&&g.undo==1,"incomplete finish commits last complete session data");
   var h=New();var h1=Start(h,dir);h.FinishPlayerTrace(h1);
   Check(h.state.playerTrace.Count==0&&h.dirty==0&&h.undo==0&&!h.traceWatch.Active,"no complete data finish is inert and stops watch");
   var i=New();var i1=Start(i,dir);i.tracePreview.Checked=false;i.Toggle();
   Check(!i.traceWatch.Active&&!i.state.usePlayerTrace&&i.undo==1&&i.dirty==1,"explicit uncheck stops future auto-application without native process action");
   File.WriteAllText(path,"0,700,500\n");i.PollLivePlayerTrace(i1);i.FinishPlayerTrace(i1);
   Check(i.state.playerTrace.Count==0&&!i.state.usePlayerTrace&&i.undo==1&&i.dirty==1,"late polling cannot turn explicit off back on");
   var j=New();var j1=Start(j,dir);ResetDialog();j.ImportPlayerTrace(true);
   Check(j.IsCurrentPlayerTrace(j1)&&j.traceWatch.Active&&j.undo==0&&j.dirty==0,"cancelled manual import preserves watch");
   OpenFileDialog.Result=DialogResult.OK;OpenFileDialog.NextPath=Path.Combine(dir,"missing.csv");j.ImportPlayerTrace(true);
   Check(j.IsCurrentPlayerTrace(j1)&&j.undo==0,"missing manual file preserves watch");
   string invalid=Path.Combine(dir,"invalid.csv");File.WriteAllText(invalid,"invalid");OpenFileDialog.NextPath=invalid;j.ImportPlayerTrace(true);
   Check(j.IsCurrentPlayerTrace(j1)&&j.undo==0&&j.dirty==0,"invalid manual trace preserves watch and state");
   OpenFileDialog.NextPath=path;j.ImportPlayerTrace(true);
   Check(j.state.playerTrace[0].x==700&&!j.traceWatch.Active&&j.traceRequests.Current==null&&j.undo==1&&j.dirty==1,"successful manual import supersedes automatic trace");
   File.WriteAllText(path,"0,900,500\n");j.PollLivePlayerTrace(j1);j.FinishPlayerTrace(j1);
   Check(j.state.playerTrace[0].x==700&&j.undo==1&&j.dirty==1,"old callback cannot overwrite manual import");
   var k=New();var k1=Start(k,dir);ResetDialog();OpenFileDialog.Result=DialogResult.OK;OpenFileDialog.NextPath=path;
   OpenFileDialog.During=()=>{k.document=new LoadData{project="A"};k.state=new EditorState();k.StopWatchingPlayerTrace();};k.ImportPlayerTrace(true);
   Check(k.state.playerTrace.Count==0&&k.undo==0&&k.dirty==0,"manual import rejects document replacement during modal loop");
   ResetDialog();var l=New();var l1=Start(l,dir);OpenFileDialog.Result=DialogResult.OK;OpenFileDialog.NextPath=path;OpenFileDialog.During=()=>{l.state=new EditorState();};l.ImportPlayerTrace(true);
   Check(l.state.playerTrace.Count==0&&l.undo==0&&l.dirty==0,"manual import rejects state replacement during modal loop");
   ResetDialog();var m=New();var m1=Start(m,dir);m.PollLivePlayerTrace(m1);
   Check(m.IsCurrentPlayerTrace(m1)&&m.state.playerTrace[0].x==900,"preparation cancellation with unchanged context preserves watching");
   var n=m.BeginPlayerTraceRequest();Check(!m.WatchPlayerTrace(new BuildResult{pid=0,dir=dir},n),"invalid process result not watched");m.StopWatchingPlayerTrace(n);
   Check(m.traceRequests.Current==null&&!m.traceWatch.Active,"cancel/failure after pending request leaves no watcher");
   var mode=New();mode.state.playerTrace=PlayerTrace.Read(path);var mode1=Start(mode,dir);mode.PollLivePlayerTrace(mode1);
   Check(mode.state.usePlayerTrace&&mode.stage.Calls==1,"false-to-true watch invalidates cached preview exactly once even if points are unchanged");
   var mode2=Start(mode,dir);mode.PollLivePlayerTrace(mode2);
   Check(mode.stage.Calls==1,"already enabled watch and unchanged poll avoid redundant redraw");
   Console.WriteLine("PASS "+checks+" extracted player trace flow checks");return 0;
  }finally{Directory.Delete(dir,true);}
 }
}
'''.replace(' METHODS', methods).replace('TOGGLE', toggle)
    source = out / 'PlayerTraceFlowProbe.cs'
    source.write_text('using System;using System.IO;using System.Collections.Generic;using System.Globalization;\n' + point + '\n' + pure + '\n' + stub)
    compiler, runner, prefix, env, label = toolchain(args.mono_root, out)
    executor = Executor(out, env, 120)
    exe = out / 'PlayerTraceFlowProbe.exe'
    stages = []
    stages.append(executor.stage('flow.compile', 'compile', compiler + [prefix + 'nologo', prefix + 'langversion:5', prefix + 'target:exe', prefix + 'out:' + str(exe), str(source)], artifact=exe))
    if stages[-1]['status'] == 'passed':
        stages.append(executor.stage('flow.pure', 'pure', runner + [str(exe), str(out)], marker=r'(?m)^PASS ([1-9][0-9]*) extracted player trace flow checks\r?$'))
    report = {'source_contract_checks': checks, 'toolchain': label, 'ui': 'not_run', 'game_process': 'not_run', 'stages': stages,
              'scope': 'Actual parser/session helpers and verbatim extracted poll/watch/finish/import methods. UI/document holders are stubs; source contracts cover RunGame/LoadProject wiring. Not Windows UI or GM acceptance.',
              'source_sha256': {str(p.relative_to(root)): hashlib.sha256(p.read_bytes()).hexdigest() for p in [root / 'desktop/PlayerTrace.cs', root / 'desktop/MainForm.cs', root / 'desktop/EditorCore.cs']}}
    (out / 'report.json').write_text(json.dumps(report, indent=2) + '\n')
    if any(s['status'] != 'passed' for s in stages) or len(stages) != 2:
        for stage in stages:
            print((out / stage['log']).read_text())
        raise RuntimeError('Extracted trace flow probe failed; see ' + str(out))
    print('PASS {} player trace source checks'.format(checks + stages[-1]['checks']))


if __name__ == '__main__':
    main()
