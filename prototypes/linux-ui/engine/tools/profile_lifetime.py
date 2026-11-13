"""Reproducible, read-only same-input/same-horizon C# lifetime comparison.
Compiles both explicit product source lists with one common UI-free harness.
Native room instances, film and GML are not executed. Full result equality is
required for a speedup claim; incomplete runs are reported as partial instead.
"""
import argparse
import hashlib
import json
import math
import statistics
import subprocess
import sys
from pathlib import Path

from run_lifetime_smoke import hashes, read_result, run_smoke
from run_timeline_checks import REFERENCES, sources_from_build, toolchain

HARNESS = r'''using System;using System.Collections.Generic;using System.IO;using System.Threading;
internal static class LifetimeBenchmark {
    static void Main(string[] args) {
        bool bounded=args.Length>0&&args[0]=="--bounded-smoke";int offset=bounded?2:0;
        if(args.Length!=5+offset||!bounded&&args[0]!="--smoke")throw new ArgumentException("Invalid benchmark arguments");
        var scene=Json.Read<EditorState>(File.ReadAllText(args[1+offset]));
        var room=Json.Read<RoomData>(File.ReadAllText(args[2+offset]));
        var metadata=Json.Read<Dictionary<string,SpriteMeta>>(File.ReadAllText(args[3+offset]));
        int end=bounded?Int32.Parse(args[2]):scene.duration;
        double seconds=bounded?Double.Parse(args[1],System.Globalization.CultureInfo.InvariantCulture):15;
        var result=SimulatedLifetime.Analyze(new LifetimeSnapshot(scene,room,metadata),LifetimeOptions.ForWindow(end,seconds),CancellationToken.None);
        File.WriteAllText(args[4+offset],Json.Write(result));
        Console.WriteLine("SMOKE stop="+result.Stop+" last_frame="+result.LastFrame+" requested_end="+result.RequestedEnd+" seconds="+result.Seconds+" rows="+result.Rows.Count+" error="+result.Error);
    }
}'''


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def canonical(result):
    # Wall time is the only intentionally differing field. Row and interval
    # order, metadata, exact counts, censoring and warnings all remain covered.
    return {key: value for key, value in result.items() if key != 'Seconds'}


def compile_harness(source_root, mono_root, out, harness):
    out.mkdir(parents=True, exist_ok=True)
    cc, _, prefix, env, name = toolchain(mono_root, out)
    sources = sources_from_build(source_root)
    tracked = sources + [source_root / 'desktop/Build-Native.ps1', source_root / 'assets/locales/en.json']
    before = {str(p.relative_to(source_root)): sha(p) for p in tracked}
    artifact = out / 'LifetimeBenchmark.exe'
    if artifact.exists():
        artifact.unlink()
    command = cc + [prefix + opt for opt in ['nologo', 'langversion:5', 'platform:x86', 'optimize+', 'target:exe', 'main:LifetimeBenchmark']]
    command += [prefix + 'reference:' + ref + '.dll' for ref in REFERENCES]
    command += [prefix + 'resource:' + str(source_root / 'assets/locales/en.json') + ',RufMaker.en.json', prefix + 'out:' + str(artifact)]
    command += [str(p) for p in sources] + [str(harness)]
    result = subprocess.run(command, cwd=out, env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, timeout=120)
    (out / 'compile.log').write_text(result.stdout)
    if result.returncode or not artifact.is_file() or not artifact.stat().st_size:
        raise RuntimeError('Benchmark compilation failed: ' + str(out / 'compile.log'))
    if before != {str(p.relative_to(source_root)): sha(p) for p in tracked}:
        raise RuntimeError('Sources changed while compiling: ' + str(source_root))
    (out / 'source-hashes.json').write_text(json.dumps(before, indent=2) + '\n')
    return {'source_root': str(source_root), 'toolchain': name, 'executable': str(artifact),
            'executable_sha256': sha(artifact), 'source_manifest': str(out / 'source-hashes.json'),
            'source_manifest_sha256': sha(out / 'source-hashes.json'), 'command': command}


def benchmark(root, baseline_root, mono_root, scene, input_root, out, seconds=180, end_frame=None, runs=3):
    root, baseline_root, scene, input_root, out = [Path(p).resolve() for p in (root, baseline_root, scene, input_root, out)]
    if not out.is_relative_to(root / 'build') or out.is_relative_to(input_root):
        raise ValueError('Output must be in candidate/build and outside input tree')
    if not scene.is_relative_to(input_root) or not input_root.is_dir():
        raise ValueError('Input hash root must contain scene and its resources')
    if type(runs) is not int or not 1 <= runs <= 5:
        raise ValueError('Runs must be between 1 and 5')
    if type(seconds) not in (int, float) or not math.isfinite(seconds) or not 5 <= seconds <= 180:
        raise ValueError('Analysis seconds must be finite and between 5 and 180')
    if end_frame is not None and (type(end_frame) is not int or not 0 <= end_frame <= 1000000):
        raise ValueError('Analysis horizon must be an integer between 0 and 1000000')
    out.mkdir(parents=True, exist_ok=True)
    before = hashes(input_root)
    (out / 'input-hashes.json').write_text(json.dumps(before, indent=2) + '\n')
    report = {'schema_version': 1, 'status': 'failed', 'seconds_budget': seconds, 'runs_per_version': runs,
              'input_root': str(input_root), 'scene': str(scene), 'input_file_count': len(before),
              'input_manifest_sha256': sha(out / 'input-hashes.json'), 'compiles': {}, 'runs': [],
              'limitations': ['C# simulation only; no native UI or GML execution.',
                              'Uses room dimensions/fps and sprite metadata only, without native room instances/brick environment or film.',
                              'Timings are local observations, not universal performance guarantees.']}
    try:
        horizon = json.loads(scene.read_text(encoding='utf-8-sig'))['duration'] if end_frame is None else end_frame
        report['requested_end'] = horizon
        harness = out / 'LifetimeBenchmark.cs'
        harness.write_text(HARNESS)
        report['harness_sha256'] = sha(harness)
        for label, source_root in [('baseline', baseline_root), ('candidate', root)]:
            report['compiles'][label] = compile_harness(source_root, mono_root, out / (label + '-compile'), harness)
        results = {'baseline': [], 'candidate': []}
        for iteration in range(runs):
            # Alternate order to reduce consistent warmup/order bias.
            for label in (['baseline', 'candidate'] if iteration % 2 == 0 else ['candidate', 'baseline']):
                run_out = out / ('%s-%d' % (label, iteration + 1))
                smoke = run_smoke(root, mono_root, report['compiles'][label]['executable'], scene, run_out, seconds, horizon)
                record = {'version': label, 'iteration': iteration + 1, 'report': str(run_out / 'report.json'),
                          'report_sha256': sha(run_out / 'report.json'), 'status': smoke['status']}
                report['runs'].append(record)
                if smoke['status'] == 'failed':
                    raise RuntimeError('Benchmark smoke failed: ' + record['report'])
                result = read_result(run_out / 'result.json', horizon)
                results[label].append(result)
                record.update(stop=result['Stop'], last_frame=result['LastFrame'], seconds=result['Seconds'],
                              result_sha256=sha(run_out / 'result.json'), room_sha256=sha(run_out / 'room.json'),
                              metadata_sha256=sha(run_out / 'metadata.json'))
                print(label, iteration + 1, result['Stop'], result['LastFrame'], result['Seconds'], flush=True)
        complete = all(result['Stop'] == 'complete' for group in results.values() for result in group)
        equal_inputs = len({(run['room_sha256'], run['metadata_sha256']) for run in report['runs']}) == 1
        exact = complete and all(canonical(result) == canonical(results['baseline'][0]) for group in results.values() for result in group)
        report.update(all_completed=complete, identical_decoded_inputs=equal_inputs, exact_result_parity=exact)
        if not equal_inputs or complete and not exact:
            raise RuntimeError('Identical-input or exact-result comparison failed')
        if exact:
            times = {label: statistics.median(r['Seconds'] for r in group) for label, group in results.items()}
            report['median_seconds'] = times
            report['speedup'] = times['baseline'] / times['candidate']
            report['status'] = 'passed'
        else:
            report['status'] = 'partial'
    except Exception as error:
        report.update(status='failed', error=type(error).__name__ + ': ' + str(error))
    finally:
        report['source_changes_after_compile'] = {}
        for label, compiled in report['compiles'].items():
            manifest = json.loads(Path(compiled['source_manifest']).read_text())
            source_root = Path(compiled['source_root'])
            changed = [name for name, digest in manifest.items()
                       if not (source_root / name).is_file() or sha(source_root / name) != digest]
            report['source_changes_after_compile'][label] = changed
        report['source_files_verified_unchanged'] = (len(report['compiles']) == 2 and
            not any(report['source_changes_after_compile'].values()))
        report['harness_verified_unchanged'] = ('harness_sha256' in report and
            (out / 'LifetimeBenchmark.cs').is_file() and sha(out / 'LifetimeBenchmark.cs') == report['harness_sha256'])
        if not report['source_files_verified_unchanged'] or not report['harness_verified_unchanged']:
            report['status'] = 'failed'
            report['error'] = report.get('error', 'Sources changed after compilation; timing/result evidence applies only to the recorded compiled source manifest, not the final source tree.')
        report['input_files_verified_unchanged'] = before == hashes(input_root)
        if not report['input_files_verified_unchanged']:
            report['status'] = 'failed'
        (out / 'report.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n')
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--baseline-root', type=Path, required=True)
    parser.add_argument('--mono-root', type=Path)
    parser.add_argument('--scene', type=Path, required=True)
    parser.add_argument('--input-root', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--seconds', type=float, default=180)
    parser.add_argument('--end-frame', type=int)
    parser.add_argument('--runs', type=int, default=3)
    args = parser.parse_args()
    report = benchmark(Path(__file__).resolve().parents[1], args.baseline_root, args.mono_root, args.scene,
                       args.input_root, args.output, args.seconds, args.end_frame, args.runs)
    print(json.dumps({key: report[key] for key in ['status', 'exact_result_parity', 'input_files_verified_unchanged', 'median_seconds', 'speedup', 'error'] if key in report}, indent=2))
    return 0 if report['status'] in {'passed', 'partial'} else 1


if __name__ == '__main__':
    sys.exit(main())
