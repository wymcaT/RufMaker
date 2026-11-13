"""Read-only paired-scene smoke, no native UI, bridge, or GameMaker.
Reads room dimensions/fps only; room instances and native film are not loaded.
Uses the existing read-only sprite-mask.mjs decoder for sprite metadata.
"""
import argparse
import hashlib
import json
import math
import re
import subprocess
import sys
from pathlib import Path
from run_timeline_checks import toolchain


def hashes(root):
    return {str(p.relative_to(root)): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in root.rglob('*') if p.is_file()}


def read_result(path, requested_end):
    result = json.loads(path.read_text())
    stops = {'complete', 'cancelled', 'time-limit', 'work-limit', 'entity-limit',
             'row-limit', 'frame-limit', 'sample-limit', 'interval-limit', 'clone-limit', 'error'}
    if not isinstance(result, dict) or result.get('Stop') not in stops:
        raise ValueError('Invalid analysis stop status')
    if type(result.get('LastFrame')) is not int or not -1 <= result['LastFrame'] <= requested_end:
        raise ValueError('Invalid observed frame')
    if type(result.get('RequestedEnd')) is not int or result['RequestedEnd'] != requested_end:
        raise ValueError('Result horizon does not match input')
    seconds = result.get('Seconds')
    if type(seconds) not in (int, float) or not math.isfinite(seconds) or seconds < 0:
        raise ValueError('Invalid observed timing')
    if not isinstance(result.get('Rows'), list) or not isinstance(result.get('Warnings'), list) or not isinstance(result.get('Error'), str):
        raise ValueError('Missing result fields')
    if result['Stop'] == 'complete' and result['LastFrame'] != requested_end:
        raise ValueError('False complete result')
    if result['Stop'] == 'error' or result['Error']:
        raise ValueError('Analysis failed: ' + result['Error'])
    return result


def run_smoke(root, mono_root, test_exe, scene, out, seconds=15, end_frame=None):
    root, out, scene, test_exe = (Path(p).resolve() for p in (root, out, scene, test_exe))
    if not out.is_relative_to(root / 'build'):
        raise ValueError('Output must be inside this source root/build')
    if type(seconds) not in (int, float) or not math.isfinite(seconds) or not 5 <= seconds <= 180:
        raise ValueError('Analysis seconds must be finite and between 5 and 180')
    if end_frame is not None and (type(end_frame) is not int or not 0 <= end_frame <= 1000000):
        raise ValueError('Analysis horizon must be an integer between 0 and 1000000')
    out.mkdir(parents=True, exist_ok=True)
    project = scene.parent
    report = {'status': 'failed', 'input_hash_root': str(project), 'source': str(scene),
              'test_exe': str(test_exe), 'input_files_verified_unchanged': False,
              'limits': 'Product default budgets; partial frames excluded.',
              'limitations': ['C# simulation only; native UI/GML never executed.',
                             'Room instances/brick collision environment and native film not loaded. Verified dimensions/fps plus scene mask metadata supplied.']}
    before = None
    result_path = out / 'result.json'
    output = ''
    try:
        # Never accept a previous run's result, even after decoder/process failure.
        if result_path.exists():
            result_path.unlink()
        before = hashes(project)
        report['input_file_count'] = len(before)
        report['test_exe_sha256'] = hashlib.sha256(test_exe.read_bytes()).hexdigest()
        if test_exe.stat().st_size == 0:
            raise ValueError('Empty test executable')
        data = json.loads(scene.read_text(encoding='utf-8-sig'))
        requested_end = data['duration'] if end_frame is None else end_frame
        report['budget_seconds'] = seconds
        report['requested_horizon'] = requested_end
        report['limits'] = 'Explicit %.6g-second compute budget; fixed product memory caps; partial frames excluded.' % seconds
        room_name = data.get('room', 'rAvoidance')
        if not re.fullmatch(r'[A-Za-z_][A-Za-z0-9_]*', room_name):
            raise ValueError('Invalid room resource name')
        room_path = project / 'rooms' / room_name / 'room.txt'
        values = dict(re.findall(r'(?m)^([a-z_]+)=([^\r\n]*)$', room_path.read_text(encoding='utf-8-sig')))
        room = {key: int(values[source]) for key, source in [('width', 'width'), ('height', 'height'), ('fps', 'roomspeed')]}
        report.update(track_count=len(data['tracks']), duration=data['duration'], room_source=str(room_path), room=room)
        (out / 'room.json').write_text(json.dumps(room))
        # This verified module imports only standard read-only fs/path/crypto/zlib.
        decoder = (root / 'studio/sprite-mask.mjs').as_uri()
        code = "import fs from 'node:fs';import {spriteMetadata} from " + json.dumps(decoder) + ";const project=process.argv[1],state=JSON.parse(fs.readFileSync(process.argv[2],'utf8'));const names=new Set();function walk(tracks){for(const t of tracks){for(const k of ['sprite','boundsSprite','rainbowSprite'])if(t[k]&&t[k]!=='-1')names.add(t[k]);walk(t.children??[]);}}walk(state.tracks);fs.writeFileSync(process.argv[3],JSON.stringify(spriteMetadata(project,[...names])));"
        subprocess.run(['node', '--input-type=module', '-e', code, str(project), str(scene), str(out / 'metadata.json')],
                       check=True, timeout=120, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
        _, runtime, _, env, _ = toolchain(mono_root, out)
        command = runtime + [str(test_exe)]
        command += ['--smoke'] if seconds == 15 and end_frame is None else ['--bounded-smoke', str(seconds), str(requested_end)]
        command += [str(scene), str(out / 'room.json'), str(out / 'metadata.json'), str(result_path)]
        report['command'] = command
        completed = subprocess.run(command, cwd=out, env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, timeout=seconds + 120)
        output = completed.stdout or ''
        report['exit_code'] = completed.returncode
        if completed.returncode != 0:
            raise ValueError('Analysis process exited unsuccessfully')
        result = read_result(result_path, requested_end)
        report.update(status='passed' if result['Stop'] == 'complete' else 'partial',
                      stop=result['Stop'], last_frame=result['LastFrame'], requested_end=result['RequestedEnd'],
                      seconds=result['Seconds'], warnings=result['Warnings'], error=result['Error'],
                      result_sha256=hashlib.sha256(result_path.read_bytes()).hexdigest())
    except Exception as error:
        extra = getattr(error, 'stdout', '') or ''
        if isinstance(extra, bytes):
            extra = extra.decode('utf-8', 'replace')
        output += extra
        report.update(status='failed', error=type(error).__name__ + ': ' + str(error))
    finally:
        try:
            after = hashes(project)
            report['input_files_verified_unchanged'] = before is not None and before == after
        except Exception as error:
            report['input_hash_error'] = type(error).__name__ + ': ' + str(error)
        if not report['input_files_verified_unchanged']:
            report['status'] = 'failed'
        report['stdout'] = output
        (out / 'run.log').write_text(output)
        (out / 'report.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n')
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--mono-root', type=Path, required=True)
    parser.add_argument('--test-exe', type=Path, required=True)
    parser.add_argument('--scene', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--seconds', type=float, default=15, help='Explicit 5–180 second analysis budget (default: 15)')
    parser.add_argument('--end-frame', type=int, help='Exact inclusive horizon; default is full scene duration')
    args = parser.parse_args()
    report = run_smoke(Path(__file__).resolve().parents[1], args.mono_root, args.test_exe, args.scene, args.output, args.seconds, args.end_frame)
    print(json.dumps(report, ensure_ascii=False, indent=2))
    return 0 if report['status'] in {'passed', 'partial'} else 1


if __name__ == '__main__':
    sys.exit(main())
