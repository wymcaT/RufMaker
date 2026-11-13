"""Compile the explicit native product list and run isolated timeline checks.
Default does NOT execute UI tests or start a display. --ui is an explicit opt-in.
"""
import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import time

SCHEMA = 1
PURE_MARKERS = {
    'PlayerTraceSessionTests': r'(?m)^PASS ([1-9][0-9]*) pure player trace session checks\r?$',
    'SimulatedLifetimeTests': r'(?m)^PASS ([1-9][0-9]*) pure simulated lifetime checks\r?$',
    'TimelineRelationsTests': r'(?m)^Pure reference checks: ([1-9][0-9]*)\r?$\nPASS\r?$',
    'LaneGroupFoldingTests': r'(?m)^PASS ([1-9][0-9]*) pure lane-group folding checks\r?$',
}
UI_PURE_MARKERS = {'SimulatedLifetimeUiTests': r'(?m)^PASS ([1-9][0-9]*) pure simulated lifetime progress checks\r?$'}
UI_TESTS = ['SimulatedLifetimeUiTests', 'LaneGroupFoldingUiTests', 'TimelineEventMarkerTests', 'TimelineGroupTests', 'TimelineIntegrationUiTests']
REFERENCES = ['System.Windows.Forms', 'System.Drawing', 'System.Web.Extensions',
              'System.IO.Compression', 'System.IO.Compression.FileSystem']


def check_count(output, pattern):
    matches = re.findall(pattern, output)
    if len(matches) != 1:
        raise ValueError('Expected exactly one successful check-count marker')
    return int(matches[0])


def sources_from_build(root):
    text = (root / 'desktop/Build-Native.ps1').read_text(encoding='utf-8-sig')
    blocks = re.findall(r'\$names\s*\+?=\s*@\(([^)]*)\)', text)
    names = [n for block in blocks for n in re.findall(r"'([^']+)'", block)]
    if not names or len(names) != len(set(names)):
        raise ValueError('Missing or duplicate explicit product source list')
    if any(not re.fullmatch(r'[A-Za-z0-9_]+', n) for n in names):
        raise ValueError('Unsafe explicit source name')
    sources = [root / 'desktop' / (n + '.cs') for n in names]
    missing = [str(p) for p in sources if not p.is_file()]
    if missing:
        raise FileNotFoundError('Missing product sources: ' + ', '.join(missing))
    return sources


def toolchain(mono_root, out):
    env = os.environ.copy()
    env.update(RUFMAKER_ROOT=str(out), RUFMAKER_PROJECT=str(out / 'project'),
               HOME=str(out / 'home'), XDG_CACHE_HOME=str(out / 'cache'))
    for folder in ['project', 'home', 'cache']:
        (out / folder).mkdir(exist_ok=True)
    if mono_root is not None:
        rt = Path(mono_root).resolve()
        mono = rt / 'usr/bin/mono-sgen'
        compiler = rt / 'usr/lib/mono/4.5/mcs.exe'
        for p in [mono, compiler]:
            if not p.is_file():
                raise FileNotFoundError('Missing Mono tool: ' + str(p))
        env.update(MONO_PATH=str(rt / 'usr/lib/mono/4.5'), MONO_CFG_DIR=str(rt / 'etc'),
                   LD_LIBRARY_PATH=str(rt / 'usr/lib') + ':' + str(rt / 'usr/lib/x86_64-linux-gnu'))
        return [str(mono), str(compiler)], [str(mono)], '-', env, 'mono-csharp5'
    if os.name != 'nt':
        raise ValueError('Non-Windows execution requires --mono-root')
    windows = Path(os.environ.get('WINDIR', 'C:/Windows'))
    candidates = [windows / 'Microsoft.NET/Framework64/v4.0.30319/csc.exe',
                  windows / 'Microsoft.NET/Framework/v4.0.30319/csc.exe']
    compiler = next((str(p) for p in candidates if p.is_file()), shutil.which('csc'))
    if not compiler:
        raise FileNotFoundError('No Windows .NET Framework csc.exe found')
    return [compiler], [], '/', env, 'windows-csharp5'


class Executor:
    def __init__(self, out, env, timeout):
        self.out, self.env, self.timeout = out, env, timeout

    def stage(self, name, kind, command, artifact=None, marker=None):
        record = {'name': name, 'kind': kind, 'status': 'failed', 'command': command,
                  'exit_code': None, 'log': name + '.log'}
        log = self.out / record['log']
        start = time.monotonic()
        try:
            # Never accept a previous run's EXE as this compile's output.
            if artifact is not None and artifact.exists():
                artifact.unlink()
            result = subprocess.run(command, cwd=self.out, env=self.env, stdout=subprocess.PIPE,
                                    stderr=subprocess.STDOUT, text=True, timeout=self.timeout)
            output = result.stdout or ''
            log.write_text(output, encoding='utf-8')
            record['exit_code'] = result.returncode
            if result.returncode != 0:
                record['reason'] = 'Process exited unsuccessfully'
            elif artifact is not None and (not artifact.is_file() or artifact.stat().st_size == 0):
                record['status'] = 'missing_output'
                record['reason'] = 'Compiler returned success without a nonempty output artifact'
            else:
                if marker:
                    record['checks'] = check_count(output, marker)
                if artifact is not None:
                    record['artifact'] = artifact.name
                    record['artifact_sha256'] = hashlib.sha256(artifact.read_bytes()).hexdigest()
                record['status'] = 'passed'
        except subprocess.TimeoutExpired as error:
            output = error.stdout or ''
            if isinstance(output, bytes):
                output = output.decode('utf-8', 'replace')
            log.write_text(output, encoding='utf-8')
            record.update(status='timed_out', reason='Stage exceeded timeout', timeout_seconds=self.timeout)
        except Exception as error:
            record['reason'] = type(error).__name__ + ': ' + str(error)
            if not log.exists():
                log.write_text(record['reason'] + '\n', encoding='utf-8')
        record['seconds'] = round(time.monotonic() - start, 3)
        return record


def not_run(name, kind, reason):
    return {'name': name, 'kind': kind, 'status': 'not_run', 'reason': reason}


def validate_report(report, require_ui=False, require_success=True):
    """Reject a report that could overclaim execution or hide a requested failure."""
    if type(report.get('schema_version')) is not int or report['schema_version'] != SCHEMA or not isinstance(report.get('stages'), list) or type(report.get('ui_requested')) is not bool:
        raise ValueError('Invalid report schema')
    if require_ui and not report.get('ui_requested'):
        raise ValueError('Required native UI validation was not requested')
    stages = report['stages']
    names = [s.get('name') for s in stages]
    if len(names) != len(set(names)):
        raise ValueError('Duplicate stage names')
    for stage in stages:
        if stage.get('status') not in {'passed', 'failed', 'timed_out', 'missing_output', 'not_run'}:
            raise ValueError('Invalid stage status')
        if stage['status'] == 'passed' and (type(stage.get('exit_code')) is not int or stage['exit_code'] != 0):
            raise ValueError('Passed stage requires observed zero exit code')
        if stage['status'] == 'passed' and stage['kind'] == 'compile' and (
                not isinstance(stage.get('artifact_sha256'), str) or not re.fullmatch(r'[0-9a-f]{64}', stage['artifact_sha256']) or
                not isinstance(stage.get('artifact'), str) or not re.fullmatch(r'[A-Za-z0-9_.-]+\.exe', stage['artifact'])):
            raise ValueError('Passed compilation requires fresh artifact hash')
        if stage['status'] == 'passed' and stage['kind'] in {'pure', 'contract'} and (
                type(stage.get('checks')) is not int or stage['checks'] <= 0):
            raise ValueError('Passed pure execution requires exact positive check count')
        if stage['kind'] == 'ui' and stage['status'] != 'not_run' and not report.get('ui_requested'):
            raise ValueError('UI executed without explicit opt-in')
    required = ['product.compile', 'lane-folding-contract', 'lifetime-progress-contract', 'player-trace-contract'] + [name + suffix for name in PURE_MARKERS for suffix in ['.compile', '.pure']]
    required += [name + '.compile' for name in UI_TESTS]
    required += [name + '.pure' for name in UI_PURE_MARKERS]
    if report.get('ui_requested'):
        required += [name + '.ui' for name in ['TimelineRelationsTests'] + UI_TESTS]
    lookup = {s['name']: s for s in stages}
    expected_kinds = {'product.compile': 'compile', 'lane-folding-contract': 'contract', 'lifetime-progress-contract': 'contract', 'player-trace-contract': 'contract'}
    expected_kinds.update({name + '.compile': 'compile' for name in list(PURE_MARKERS) + UI_TESTS})
    expected_kinds.update({name + '.pure': 'pure' for name in list(PURE_MARKERS) + list(UI_PURE_MARKERS)})
    expected_kinds.update({name + '.ui': 'ui' for name in ['TimelineRelationsTests'] + UI_TESTS})
    for name, kind in expected_kinds.items():
        if name in lookup and lookup[name].get('kind') != kind:
            raise ValueError('Incorrect stage kind: ' + name)
    succeeded = all(lookup.get(name, {}).get('status') == 'passed' for name in required)
    succeeded = succeeded and all(s['status'] in {'passed', 'not_run'} for s in stages)
    succeeded = succeeded and all(name + '.ui' in lookup for name in ['TimelineRelationsTests'] + UI_TESTS)
    succeeded = succeeded and 'lane-folding-contract' in lookup
    if report.get('status') != ('passed' if succeeded else 'failed'):
        raise ValueError('Aggregate status disagrees with required stage outcomes')
    if require_success and report['status'] != 'passed':
        raise ValueError('Validation report contains failed or missing required stages')
    return report


def save_report(report, out):
    validate_report(report, require_success=False)
    temp = out / 'report.json.tmp'
    temp.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    temp.replace(out / 'report.json')


def run_checks(root, out, mono_root=None, ui=False, timeout=120):
    root, out = Path(root).resolve(), Path(out).resolve()
    if not out.is_relative_to(root / 'build'):
        raise ValueError('Output must remain inside this source root/build')
    if not math.isfinite(timeout) or timeout <= 0:
        raise ValueError('Timeout must be finite and positive')
    out.mkdir(parents=True, exist_ok=True)
    report = {'schema_version': SCHEMA, 'status': 'failed', 'root': str(root),
              'ui_requested': ui, 'scope': 'native-ui-opt-in' if ui else 'compile-and-pure-only',
              'limitations': ['No GameMaker execution or Windows runtime acceptance is implied.'], 'stages': []}
    if not ui:
        report['limitations'].append('Native UI execution explicitly not run; compilation is not interaction validation.')
    stages = report['stages']
    try:
        sources = sources_from_build(root)
        cc, runtime, prefix, env, name = toolchain(mono_root, out)
        report['toolchain'] = name
        report['test_sources'] = {}
        report['locale_sha256'] = hashlib.sha256((root / 'assets/locales/en.json').read_bytes()).hexdigest()
        report['product_sources'] = {str(p.relative_to(root)): hashlib.sha256(p.read_bytes()).hexdigest() for p in sources}
        executor = Executor(out, env, timeout)
        common = [prefix + opt for opt in ['nologo', 'langversion:5', 'platform:x86', 'optimize+', 'codepage:65001']]
        common += [prefix + 'reference:' + ref + '.dll' for ref in REFERENCES]
        common += [prefix + 'resource:' + str(root / 'assets/locales/en.json') + ',RufMaker.en.json']
        def compile_test(label, test=None):
            artifact = out / (label + '.exe')
            inputs = [str(p) for p in sources]
            args = [prefix + 'target:' + ('exe' if test else 'winexe')]
            if test:
                path = root / 'desktop/tests' / (test + '.cs')
                if not path.is_file():
                    record = {'name': label + '.compile', 'kind': 'compile', 'status': 'failed', 'reason': 'Missing test source: ' + str(path)}
                    stages.append(record)
                    return False
                report['test_sources'][str(path.relative_to(root))] = hashlib.sha256(path.read_bytes()).hexdigest()
                inputs.append(str(path)); args.append(prefix + 'main:' + test)
            record = executor.stage(label + '.compile', 'compile', cc + common + args + [prefix + 'out:' + str(artifact)] + inputs, artifact=artifact)
            stages.append(record)
            return record['status'] == 'passed'
        compile_test('product')
        compiled = {}
        for test, marker in PURE_MARKERS.items():
            compiled[test] = compile_test(test, test)
            pure_args = []
            if test == 'PlayerTraceSessionTests':
                scratch = out / (test + '-pure-output'); scratch.mkdir(exist_ok=True); pure_args = [str(scratch)]
            stages.append(executor.stage(test + '.pure', 'pure', runtime + [str(out / (test + '.exe'))] + pure_args, marker=marker)
                          if compiled[test] else not_run(test + '.pure', 'pure', 'Compilation failed'))
        for test in UI_TESTS:
            compiled[test] = compile_test(test, test)
        # These entry points instantiate no controls. The native UI path remains opt-in.
        for test, marker in UI_PURE_MARKERS.items():
            pure_output = out / (test + '-pure-output')
            pure_output.mkdir(exist_ok=True)
            stages.append(executor.stage(test + '.pure', 'pure', runtime +
                          [str(out / (test + '.exe')), '--pure', str(pure_output)], marker=marker)
                          if compiled.get(test) else not_run(test + '.pure', 'pure', 'Compilation failed'))
        for test in ['TimelineRelationsTests'] + UI_TESTS:
            args = ['--ui'] if test == 'TimelineRelationsTests' else [str(out / (test + '-output'))]
            if ui and compiled[test]:
                (out / (test + '-output')).mkdir(exist_ok=True)
                stages.append(executor.stage(test + '.ui', 'ui', runtime + [str(out / (test + '.exe'))] + args))
            else:
                stages.append(not_run(test + '.ui', 'ui', 'UI execution not requested' if not ui else 'Compilation failed'))
        contract = root / 'tools/test_lane_folding_contract.py'
        if contract.is_file():
            report['contract_sha256'] = hashlib.sha256(contract.read_bytes()).hexdigest()
        stages.append(executor.stage('lane-folding-contract', 'contract', [sys.executable, str(contract)],
                      marker=r'(?m)^PASS ([1-9][0-9]*) source contracts \(not GUI execution\)\r?$')
                      if contract.is_file() else {'name': 'lane-folding-contract', 'kind': 'contract', 'status': 'failed', 'reason': 'Required contract source is absent'})
        progress_contract = root / 'tools/test_lifetime_progress_contract.py'
        if progress_contract.is_file():
            report['lifetime_progress_contract_sha256'] = hashlib.sha256(progress_contract.read_bytes()).hexdigest()
        stages.append(executor.stage('lifetime-progress-contract', 'contract', [sys.executable, str(progress_contract)],
                      marker=r'(?m)^PASS ([1-9][0-9]*) lifetime progress source contracts \(not GUI execution\)\r?$')
                      if progress_contract.is_file() else {'name': 'lifetime-progress-contract', 'kind': 'contract', 'status': 'failed', 'reason': 'Required progress contract source is absent'})
        trace_contract = root / 'tools/test_player_trace_contract.py'
        if trace_contract.is_file():
            report['player_trace_contract_sha256'] = hashlib.sha256(trace_contract.read_bytes()).hexdigest()
        trace_command = [sys.executable, str(trace_contract)]
        if mono_root: trace_command += ['--mono-root', str(Path(mono_root).resolve())]
        trace_command += ['--output', str(out / 'player-trace-contract')]
        stages.append(executor.stage('player-trace-contract', 'contract', trace_command,
                      marker=r'(?m)^PASS ([1-9][0-9]*) player trace source checks\r?$')
                      if trace_contract.is_file() else {'name': 'player-trace-contract', 'kind': 'contract', 'status': 'failed', 'reason': 'Required player trace contract is absent'})
        report['status'] = 'passed' if all(s['status'] in {'passed', 'not_run'} for s in stages) and all(
            s['status'] == 'passed' for s in stages if s['kind'] in {'compile', 'pure'} or ui and s['kind'] == 'ui') else 'failed'
    except Exception as error:
        stages.append({'name': 'preparation', 'kind': 'setup', 'status': 'failed', 'reason': type(error).__name__ + ': ' + str(error)})
    finally:
        save_report(report, out)
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--mono-root', type=Path)
    parser.add_argument('--ui', action='store_true', help='Explicitly execute native controls; never starts a display')
    parser.add_argument('--timeout', type=float, default=120)
    parser.add_argument('--output', type=Path, help='Output directory within this source root/build')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    report = run_checks(root, args.output or root / 'build/timeline-checks', args.mono_root, args.ui, args.timeout)
    print(json.dumps({'status': report['status'], 'report': str((args.output or root / 'build/timeline-checks') / 'report.json'),
                      'ui_requested': report['ui_requested']}, indent=2))
    return 0 if report['status'] == 'passed' else 1


if __name__ == '__main__':
    sys.exit(main())
