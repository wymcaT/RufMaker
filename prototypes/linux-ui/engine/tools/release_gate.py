#!/usr/bin/env python3
"""Local source/project release gate. Standard library only; no installs or publishing.

Resource slots adapt verify_resource_ids.py; sync adapts verify_recovered.mjs;
Mono compilation reuses build/mono_checks.py and checks its result (not only exit code).
"""
from pathlib import Path, PurePosixPath
import argparse
import hashlib
import json
import math
import os
import re
import shutil
import subprocess
import sys
import tempfile
import zipfile


class GateError(RuntimeError):
    pass


def require(condition, message):
    if not condition:
        raise GateError(message)


def sha(file):
    return hashlib.sha256(file.read_bytes()).hexdigest()


def files(root):
    result = {}
    for p in sorted(root.rglob('*')):
        require(not p.is_symlink(), f'Symlinks are not portable release inputs: {p}')
        if p.is_file():
            result[p.relative_to(root).as_posix()] = sha(p)
    return result


def lines(file):
    # splitlines drops the newline terminator but preserves actual empty ID slots.
    return file.read_text(encoding='utf-8-sig').splitlines()


def inside(root, name):
    relative = PurePosixPath(name)
    require(name and '\\' not in name and not relative.is_absolute()
            and '..' not in relative.parts and ':' not in name, f'Unsafe path: {name!r}')
    result = (root / name).resolve()
    require(result.is_relative_to(root.resolve()), f'Path escapes root: {name}')
    return result


def compare_maps(expected, actual, label):
    missing = sorted(expected.keys() - actual.keys())
    extra = sorted(actual.keys() - expected.keys())
    changed = sorted(k for k in expected.keys() & actual.keys() if expected[k] != actual[k])
    require(not (missing or extra or changed),
            f'{label}: missing={missing[:12]}, extra={extra[:12]}, changed={changed[:12]}')


# All known YYD kinds are checked for ID stability. These have known native layouts.
LAYOUTS = {'scripts':('.gml',), 'objects':('.txt','.gml'),
           'sprites':('/sprite.txt',), 'rooms':('/room.txt',),
           'timelines':('.gml',), 'paths':('.txt','.gml'),
           'backgrounds':('.txt',), 'fonts':('.txt',), 'sounds':('.txt',)}


def resource_slots(baseline, candidate, removals=()):
    require(baseline.is_dir() and candidate.is_dir(), 'Resource roots must exist')
    allow = set(removals)
    used = set()
    old_indexes = {p.relative_to(baseline).as_posix():p for p in baseline.rglob('index.yyd')}
    new_indexes = {p.relative_to(candidate).as_posix():p for p in candidate.rglob('index.yyd')}
    require(old_indexes and new_indexes, 'No resource indexes found')
    stable = blank = 0
    for rel, old in old_indexes.items():
        require(rel in new_indexes, f'Missing resource index: {rel}')
        before, after = lines(old), lines(new_indexes[rel])
        require(len(after) >= len(before), f'Resource index shrank: {rel}')
        kind = str(PurePosixPath(rel).parent)
        for slot, name in enumerate(before):
            key = kind+'/'+name
            expected = '' if name and key in allow else name
            require(after[slot] == expected,
                    f'Resource slot shifted: {rel}[{slot}] expected {expected!r}, got {after[slot]!r}')
            if name and key in allow:
                used.add(key)
                require(name not in after, f'Removed resource moved instead of removed: {key}')
            elif name:
                stable += 1
            else:
                blank += 1
    require(used == allow, f'Unused removal approvals: {sorted(allow-used)}')
    return dict(status='pass', preservedNamedSlots=stable, preservedBlankSlots=blank,
                approvedRemovedSlots=len(used), indexes=len(new_indexes))


def resource_integrity(baseline, candidate, removals=()):
    slots = resource_slots(baseline,candidate,removals)
    new_indexes = {p.relative_to(candidate).as_posix():p for p in candidate.rglob('index.yyd')}
    indexed = {}
    for rel, index in new_indexes.items():
        folder = index.parent
        kind = folder.relative_to(candidate).as_posix()
        names = [n for n in lines(index) if n]
        require(len(names) == len(set(names)), f'Duplicate resource names: {rel}')
        require(len(names) == len({n.casefold() for n in names}), f'Case-colliding resource names: {rel}')
        require(all('/' not in n and '\\' not in n and n not in ('.','..') for n in names), f'Unsafe resource name: {rel}')
        indexed[kind] = set(names)
        tree = folder/'tree.yyd'
        if tree.exists():
            leaves = [line.strip()[1:] for line in lines(tree) if line.strip().startswith('|')]
            require(len(leaves) == len(set(leaves)), f'Duplicate tree entries: {tree}')
            require(set(leaves) == set(names), f'Tree/index mismatch: {kind}: missing={sorted(set(names)-set(leaves))[:8]}, orphan={sorted(set(leaves)-set(names))[:8]}')
        elif names and kind in LAYOUTS:
            raise GateError(f'Missing resource tree: {kind}')
        if kind not in LAYOUTS:
            require(not names, f'Unsupported populated resource kind requires explicit validation: {kind}')
            continue
        for name in names:
            for suffix in LAYOUTS[kind]:
                require((folder/(name+suffix)).is_file(), f'Missing indexed resource file: {kind}/{name+suffix}')
        # Include both object files so an orphan .gml cannot hide behind a missing .txt.
        if kind in ('sprites','rooms'):
            disk = {p.name for p in folder.iterdir() if p.is_dir()}
        else:
            disk = {p.stem for p in folder.iterdir() if p.is_file() and p.suffix in ('.gml','.txt','.png','.wav','.ogg','.mp3')}
        require(disk <= set(names), f'Orphan resource files: {kind}: {sorted(disk-set(names))[:12]}')
    for name in indexed.get('sprites',set()):
        folder = candidate/'sprites'/name
        metadata = dict(line.split('=',1) for line in lines(folder/'sprite.txt') if '=' in line)
        frames = int(metadata.get('frames','0'))
        require(0 <= frames <= 100000, f'Invalid sprite frame count: {name}')
        for frame in range(frames):
            require((folder/(str(frame)+'.png')).is_file(), f'Missing sprite image: {name}/{frame}.png')
    for name in indexed.get('backgrounds',set()):
        folder = candidate/'backgrounds'
        metadata = dict(line.split('=',1) for line in lines(folder/(name+'.txt')) if '=' in line)
        if metadata.get('exists') == '1':
            require((folder/(name+'.png')).is_file(), f'Missing background image: {name}')
    objects, sprites = indexed.get('objects', set()), indexed.get('sprites', set())
    for name in objects:
        metadata = dict(line.split('=',1) for line in lines(candidate/'objects'/(name+'.txt')) if '=' in line)
        for key, targets in [('parent',objects),('sprite',sprites),('mask',sprites)]:
            value = metadata.get(key,'')
            require(not value or value in targets, f'Missing object dependency: {name}.{key}={value}')
        source = (candidate/'objects'/(name+'.gml')).read_text(encoding='utf-8-sig')
        for target in re.findall(r'^#define\s+Collision_(\w+)', source, re.M):
            require(target in objects, f'Missing collision object: {name} -> {target}')
    for name in indexed.get('rooms',set()):
        instances = candidate/'rooms'/name/'instances.txt'
        require(instances.is_file(), f'Missing room instances: {name}')
        for row in lines(instances):
            if row:
                target = row.split(',',1)[0]
                require(target in objects, f'Missing room object: {name} -> {target}')
    return dict(**slots, resources=sum(map(len,indexed.values())))


# Conservative literal checks, not a JS parser. Actual extracted tests are mandatory.
IMPORTS = [r'\b(?:import|export)\s+(?:[^;]*?\s+)?from\s*[\'"]([^\'"\n]+)[\'"]',
           r'\bimport\s*[\'"]([^\'"\n]+)[\'"]',
           r'\bimport\s*\(\s*[\'"]([^\'"\n]+)[\'"]\s*\)',
           r'\bnew\s+URL\(\s*[\'"]([^\'"\n]+)[\'"]\s*,\s*import\.meta\.url']


def dependency_closure(engine):
    count = 0
    missing = []
    optional = {('studio/native-preview.test.mjs','../non-ruf projects/Verse3/objects/world.gml')}
    skipped = []
    for sub in ('studio','tests','tools'):
        for file in sorted((engine/sub).rglob('*.mjs')):
            text = file.read_text(encoding='utf-8-sig')
            for pattern in IMPORTS:
                for spec in re.findall(pattern, text):
                    # Directory URLs identify scratch/output roots, not file inputs.
                    if pattern == IMPORTS[-1] and (spec.endswith('/') or not PurePosixPath(spec).suffix):
                        continue
                    if spec.startswith('node:'):
                        continue
                    if not spec.startswith(('./','../')):
                        raise GateError(f'Unsupported nonlocal dependency: {file.relative_to(engine)} -> {spec}')
                    if (file.relative_to(engine).as_posix(),spec) in optional:
                        skipped.append(spec)
                        continue
                    target = (file.parent/spec).resolve()
                    require(target.is_relative_to(engine.resolve()), f'Import escapes source archive: {file}: {spec}')
                    if not target.is_file():
                        missing.append(f'{file.relative_to(engine)} -> {spec}')
                    count += 1
    require(not missing, 'Missing import or fixture: '+'; '.join(missing))
    build = engine/'desktop/Build-Native.ps1'
    require(build.is_file(), 'Missing desktop/Build-Native.ps1')
    names = []
    for block in re.findall(r'\$names\s*\+?=\s*@\(([^)]*)\)', build.read_text(encoding='utf-8-sig')):
        names.extend(re.findall(r"'([^']+)'",block))
    require(names and len(names) == len(set(names)), 'Empty/duplicate explicit C# source list')
    for name in names:
        require((engine/'desktop'/(name+'.cs')).is_file(), f'Missing native source: {name}.cs')
    require((engine/'assets/icons/icon.ico').is_file(), 'Missing application icon')
    require((engine/'assets/locales/en.json').is_file(), 'Missing language resource')
    return dict(status='pass', literalLocalDependencies=count, nativeSources=len(names), optionalReferenceInputs=skipped,
                limitation='Literal import/URL scan; complete file manifest and extracted execution also required')


def validate_mono_results(results, tests):
    required = [('RufMaker.object-engine.mono', 'compile')]
    required += [(name, stage) for name in tests for stage in ('compile', 'run')]
    for name, stage in required:
        value = results.get(name, {}).get(stage)
        require(type(value) is int and value == 0, f'Mono helper reported missing/failed {name} {stage}')



def validate_lifetime_evidence(report, output, executable, scene, expected_budget=15):
    """Bind observed coverage to the fresh executable/result and the full input horizon."""
    output, executable, scene = (Path(p).resolve() for p in (output, executable, scene))
    require(isinstance(report, dict), 'Invalid lifetime smoke report')
    require(type(report.get('budget_seconds')) in (int,float)
            and report['budget_seconds'] == expected_budget and 5 <= expected_budget <= 180,
            'Lifetime smoke budget mismatch')
    require(report.get('input_files_verified_unchanged') is True, 'Lifetime smoke input integrity failed')
    require(report.get('source') == str(scene) and report.get('input_hash_root') == str(scene.parent),
            'Lifetime smoke input scope mismatch')
    require(type(report.get('input_file_count')) is int
            and report['input_file_count'] == len(files(scene.parent)), 'Lifetime smoke input count mismatch')
    require(report.get('test_exe') == str(executable)
            and report.get('test_exe_sha256') == sha(executable), 'Lifetime smoke executable mismatch')
    require(report.get('result_sha256') == sha(output/'result.json'), 'Lifetime smoke result hash mismatch')
    result = json.loads((output/'result.json').read_text())
    end = json.loads(scene.read_text(encoding='utf-8-sig'))['duration']
    require(type(end) is int and end >= 0, 'Invalid lifetime scene horizon')
    require(isinstance(result, dict), 'Invalid lifetime result')
    for name, result_name in [('last_frame','LastFrame'), ('requested_end','RequestedEnd')]:
        require(type(report.get(name)) is int and type(result.get(result_name)) is int
                and report[name] == result[result_name], 'Lifetime smoke coverage mismatch: '+name)
    require(report['requested_end'] == end and -1 <= report['last_frame'] <= end,
            'Lifetime smoke horizon mismatch')
    seconds = result.get('Seconds')
    require(type(seconds) in (int,float) and math.isfinite(seconds) and seconds >= 0
            and type(report.get('seconds')) in (int,float) and report['seconds'] == seconds,
            'Invalid lifetime smoke elapsed time')
    stop = result.get('Stop')
    require(stop in ('complete','cancelled','time-limit','work-limit','entity-limit','row-limit',
                     'frame-limit','sample-limit','interval-limit','clone-limit')
            and report.get('stop') == stop, 'Lifetime smoke stop mismatch')
    require(result.get('Error') == '' and report.get('error') == ''
            and type(report.get('exit_code')) is int and report['exit_code'] == 0,
            'Lifetime smoke analysis failed')
    require(isinstance(result.get('Rows'),list) and isinstance(result.get('Warnings'),list)
            and report.get('warnings') == result['Warnings'], 'Invalid lifetime smoke result shape')
    require(report.get('status') == ('passed' if stop == 'complete' else 'partial')
            and (stop != 'complete' or report['last_frame'] == end), 'False lifetime completion status')
    return result


def package_sources(engine):
    result = {}
    for folder in ('studio','desktop','assets','tests','tools','docs'):
        require((engine/folder).is_dir(), f'Missing source directory: {folder}')
        result.update({folder+'/'+rel:digest for rel,digest in files(engine/folder).items()
                       if '__pycache__' not in PurePosixPath(rel).parts})
    # Complete native source imports also carry editable template/engine trees.
    # Optional for old narrow checkpoints, but never silently omit them when present.
    for folder in ('templates','engine'):
        if (engine/folder).exists():
            require((engine/folder).is_dir(), f'Invalid source directory: {folder}')
            result.update({folder+'/'+rel:digest for rel,digest in files(engine/folder).items()
                           if '__pycache__' not in PurePosixPath(rel).parts})
    for file in engine.iterdir():
        if file.is_file() and (file.suffix == '.md' or file.name == 'LICENSE'):
            result[file.name] = sha(file)
    helper = 'build/mono_checks.py'
    if (engine/helper).is_file():
        result[helper] = sha(engine/helper)
    return result


def verify_archive(archive, expected, destination):
    require(not destination.exists(), f'Extraction directory must be new: {destination}')
    with zipfile.ZipFile(archive) as z:
        members = z.infolist()
        names = [i.filename for i in members]
        require(len(names) == len(set(names)), 'Duplicate ZIP entries')
        require(len(names) == len({n.casefold() for n in names}), 'Case-colliding ZIP entries')
        for member in members:
            inside(destination, member.filename)
            require(not member.is_dir(), 'Only regular-file entries are supported')
            require((member.external_attr >> 16) & 0o170000 != 0o120000, 'Archive symlink rejected')
        require(z.testzip() is None, 'Archive CRC failed')
        actual = {i.filename:hashlib.sha256(z.read(i)).hexdigest() for i in members}
        compare_maps(expected, actual, 'Archive dependency closure')
        destination.mkdir(parents=True)
        z.extractall(destination)
    compare_maps(expected, files(destination), 'Extracted hash closure')
    return dict(status='pass', files=len(expected), archiveSha256=sha(archive), bytes=archive.stat().st_size)


def run(command, cwd, log, env=None, timeout=240):
    with log.open('w',encoding='utf-8') as out:
        out.write('Command: '+json.dumps([str(c) for c in command])+'\n'); out.flush()
        proc = subprocess.run([str(c) for c in command], cwd=cwd, stdout=out,
                              stderr=subprocess.STDOUT, env=env, timeout=timeout)
    require(proc.returncode == 0, f'Command failed ({proc.returncode}); see {log}')
    return dict(status='pass', command=[str(c) for c in command], log=str(log))


def full_gate(args):
    engine, project, baseline = (Path(p).resolve() for p in (args.engine,args.project,args.baseline))
    work = Path(args.work).resolve()
    require(not work.exists(), f'Work directory must be new: {work}')
    require(not work.is_relative_to(project) and not work.is_relative_to(baseline), 'Work directory must not be inside an input project')
    work.mkdir(parents=True)
    report = dict(status='running', scope='source/project recovery gate, not a Windows/GM release approval',
                  limitations=['Windows UI and native GM8.2 compile/game not run',
                               'Windows source merged offline; no native deployment, EXE replacement or publication'])
    source_before, project_before, baseline_before = package_sources(engine), files(project), files(baseline)
    stage_errors = []
    def check(name, fn):
        try:
            report[name] = fn()
        except Exception as error:
            report[name] = {'status':'fail','error':str(error)}
            stage_errors.append(name)
    try:
        entry = inside(project,args.entry)
        data = json.loads(entry.read_text(encoding='utf-8-sig'))
        gm_file = inside(entry.parent, data['gameProject'])
        require(gm_file.is_file(), 'Relative project entry does not resolve')
        gm = gm_file.parent
        gm_rel = gm.relative_to(project)
        check('dependencies', lambda: dependency_closure(engine))
        check('resourceSlots', lambda: resource_slots(baseline,gm,args.allow_remove))
        check('resourceFiles', lambda: resource_integrity(baseline,gm,args.allow_remove))
        expected = {'engine/'+p:h for p,h in source_before.items()}
        expected.update({'project/'+p:h for p,h in project_before.items()})
        # Expected manifest comes from trusted input trees, never from the archive itself.
        (work/'expected-sha256.json').write_text(json.dumps(expected,indent=2)+'\n')
        if args.archive:
            archive = Path(args.archive).resolve()
        else:
            archive = work/'source-project-recovery.zip'
            with zipfile.ZipFile(archive,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=6) as z:
                for prefix,root,manifest in [('engine',engine,source_before),('project',project,project_before)]:
                    for rel in sorted(manifest):
                        z.write(root/rel,prefix+'/'+rel)
        recovered = work/'recovered'
        report['archive'] = verify_archive(archive,expected,recovered)
        clean_engine, clean_project = recovered/'engine', recovered/'project'
        (clean_engine/'build').mkdir(exist_ok=True)
        check('extractedDependencies', lambda: dependency_closure(clean_engine))
        env = os.environ.copy()
        # Even an accidentally unisolated test import can reach only this disposable copy.
        env['RUFMAKER_ROOT'] = str(clean_engine)
        env['RUFMAKER_PROJECT'] = str(clean_project/args.entry)
        env['RUFMAKER_RELEASE_NODE'] = args.node
        timeline_runner = clean_engine/'tools/run_timeline_checks.py'
        if timeline_runner.is_file() and (args.mono_root or args.native_windows):
            timeline_output = clean_engine/'build/timeline-checks'
            command = [sys.executable, '-B', timeline_runner, '--output', timeline_output]
            if args.mono_root:
                command += ['--mono-root', str(Path(args.mono_root).resolve())]
            report['timelineChecks'] = run(command, clean_engine, work/'timeline-checks.log', env)
            timeline_report = json.loads((timeline_output/'report.json').read_text())
            # Validate the recovered runner's required stages, not a stale success flag.
            import importlib.util
            spec = importlib.util.spec_from_file_location('recovered_timeline_checks', timeline_runner)
            timeline_module = importlib.util.module_from_spec(spec)
            spec.loader.exec_module(timeline_module)
            timeline_module.validate_report(timeline_report)
            report['timelineChecks']['results'] = timeline_report
        filter_runner = clean_engine/'tools/run_track_display_checks.py'
        if filter_runner.is_file() and args.mono_root:
            filter_output = clean_engine/'build/track-display-checks'
            report['trackDisplayChecks'] = run([sys.executable, '-B', filter_runner,
                '--mono-root', str(Path(args.mono_root).resolve()), '--output', filter_output],
                clean_engine, work/'track-display-checks.log', env)
            filter_report = json.loads((filter_output/'report.json').read_text())
            timeline_module.validate_report(filter_report)
            filter_stages = {stage['name']:stage for stage in filter_report['stages']}
            for name, kind in [('TrackDisplayFilterTests.compile','compile'),
                               ('TrackDisplayFilterTests.pure','pure'),
                               ('TrackDisplayFilterUiTests.compile','compile'),
                               ('track-display-contract','contract')]:
                stage = filter_stages.get(name,{})
                require(stage.get('kind') == kind and stage.get('status') == 'passed',
                        'Missing or failed required display-filter stage: '+name)
            require(filter_stages.get('TrackDisplayFilterUiTests.ui',{}).get('status') == 'not_run',
                    'This recovery gate does not execute filter UI')
            report['trackDisplayChecks']['results'] = filter_report
        scene_runner = clean_engine/'tools/run_timeline_scene_checks.py'
        if scene_runner.is_file() and args.mono_root:
            scene_output = clean_engine/'build/timeline-scene-checks'
            scene_executable = clean_engine/'build/timeline-checks/TimelineRelationsTests.exe'
            report['timelineSceneChecks'] = run([sys.executable, '-B', scene_runner,
                '--mono-root', str(Path(args.mono_root).resolve()), '--test-exe', scene_executable,
                '--scene', clean_project/gm_rel/'rufmaker.json', '--input-root', clean_project,
                '--output', scene_output], clean_engine, work/'timeline-scene-checks.log', env)
            scene_report = json.loads((scene_output/'report.json').read_text())
            scene_spec = importlib.util.spec_from_file_location('recovered_scene_checks', scene_runner)
            scene_module = importlib.util.module_from_spec(scene_spec); scene_spec.loader.exec_module(scene_module)
            scene_module.validate_evidence(scene_report, scene_output, scene_executable,
                clean_project/gm_rel/'rufmaker.json', clean_project)
            report['timelineSceneChecks']['results'] = scene_report
        smoke_runner = clean_engine/'tools/run_lifetime_smoke.py'
        if smoke_runner.is_file() and args.mono_root:
            smoke_output = clean_engine/'build/lifetime-project-smoke'
            report['lifetimeProjectSmoke'] = run([sys.executable, '-B', smoke_runner,
                '--mono-root', str(Path(args.mono_root).resolve()),
                '--test-exe', clean_engine/'build/timeline-checks/SimulatedLifetimeTests.exe',
                '--scene', clean_project/gm_rel/'rufmaker.json',
                '--output', smoke_output, '--seconds', str(args.lifetime_seconds)],
                clean_engine, work/'lifetime-project-smoke.log', env, timeout=args.lifetime_seconds+120)
            smoke_report = json.loads((smoke_output/'report.json').read_text())
            validate_lifetime_evidence(smoke_report, smoke_output,
                clean_engine/'build/timeline-checks/SimulatedLifetimeTests.exe',
                clean_project/gm_rel/'rufmaker.json', args.lifetime_seconds)
            report['lifetimeProjectSmoke']['results'] = smoke_report
        family_runner = clean_engine/'desktop/tests/run-parent-child-presets.py'
        if family_runner.is_file() and args.mono_root:
            report['parentChildPresets'] = run([sys.executable, '-B', family_runner,
                str(Path(args.mono_root).resolve()), '--node', args.node], clean_engine,
                work/'parent-child-presets.log', env)
            family_report = json.loads((clean_engine/'build/parent-child/report.json').read_text())
            require(family_report.get('status') == 'pass', 'Parent/child model and GML checks failed')
            report['parentChildPresets']['results'] = family_report
        elif family_runner.is_file() and args.native_windows:
            report['parentChildPresets'] = run(['powershell', '-NoProfile', '-File',
                clean_engine/'desktop/Test-ParentChildPresets.ps1'], clean_engine,
                work/'parent-child-presets.log', env)
            report['parentChildPresets']['scope'] = 'Windows model/generated-GML checks, not GUI or GM gameplay'
        visual_runner = clean_engine/'tools/run_visual_oracles.py'
        if visual_runner.is_file() and (args.mono_root or args.native_windows):
            command = [sys.executable, '-B', visual_runner, '--output', work/'visual-oracles']
            if args.mono_root:
                command += ['--mono-root', str(Path(args.mono_root).resolve())]
            report['visualOracles'] = run(command, clean_engine, work/'visual-oracles.log', env)
            oracle_report = json.loads((work/'visual-oracles/report.json').read_text())
            require(oracle_report.get('status') == 'pass', 'Visual oracle report failed')
            env['CAMERA_PARITY_ORACLE'] = str(work/'visual-oracles/camera-oracle.json')
            report['visualOracles']['results'] = oracle_report
        tests = sorted((clean_engine/'studio').glob('*.test.mjs'))
        skipped = ['timeline-regression.test.mjs']
        tests = [p for p in tests if p.name not in skipped]
        require(tests, 'No extracted Node tests found')
        report['nodeTests'] = run([args.node,'--test',*tests],clean_engine,work/'node-tests.log',env)
        report['nodeTests']['testFiles'] = len(tests)
        report['nodeTests']['excludedRequiresNativeGM'] = skipped
        report['guardTests'] = run([sys.executable,'-B','tools/test_release_gate.py'],clean_engine,work/'guard-tests.log',env)
        if (clean_engine/'tools/test_visual_oracles.py').is_file():
            report['visualRunnerTests'] = run([sys.executable,'-B','tools/test_visual_oracles.py'],clean_engine,work/'visual-runner-tests.log',env)
        if (clean_engine/'tools/test_timeline_checks.py').is_file():
            report['timelineRunnerTests'] = run([sys.executable, '-B', 'tools/test_timeline_checks.py'], clean_engine, work/'timeline-runner-tests.log', env)
        for test_name in ('test_track_display_checks.py','test_lifetime_smoke.py','test_profile_lifetime.py','test_timeline_scene_checks.py'):
            if (clean_engine/'tools'/test_name).is_file():
                report[test_name[:-3]] = run([sys.executable, '-B', 'tools/'+test_name], clean_engine,
                    work/(test_name[:-3]+'.log'), env)
        report['repeatedSync'] = run([args.node,clean_engine/'tools/release_sync.mjs',clean_engine,clean_project,args.entry],clean_engine,work/'sync.log',env)
        check('postSyncResourceSlots', lambda: resource_slots(baseline,clean_project/gm_rel,args.allow_remove))
        check('postSyncResourceFiles', lambda: resource_integrity(baseline,clean_project/gm_rel,args.allow_remove))
        compare_maps(project_before,files(clean_project),'Recovered project after all tests/sync')
        if args.mono_root:
            require((clean_engine/'build/mono_checks.py').is_file(), 'Mono mode requires the existing build/mono_checks.py evidence helper')
            env['RUFMAKER_MONO_ROOT'] = str(Path(args.mono_root).resolve())
            report['cleanBuild'] = run([sys.executable,'-B','build/mono_checks.py'],clean_engine,work/'clean-build.log',env)
            results = json.loads((clean_engine/'build/mono-results.json').read_text())
            required_tests = ['ObjectMotion.tests','SpriteFrameMask.tests','SpriteFrame.tests','TrailDepth.tests','FrozenRainbowSampling.tests','HomingSampling.tests']
            if (clean_engine/'desktop/WalkthroughView.cs').is_file():
                required_tests.append('WalkthroughView.tests')
            validate_mono_results(results, required_tests)
            require(results['FrozenRainbowSampling.tests'].get('generatedRun') == 0, 'Generated rainbow sampling comparison failed')
            require(results['HomingSampling.tests'].get('generatedRun') == 0, 'Generated homing sampling comparison failed')
            report['cleanBuild']['results'] = results
            report['cleanBuild']['platform'] = 'existing Mono, not native Windows .NET'
        elif args.native_windows:
            require(os.name == 'nt', '--native-windows requires Windows')
            report['cleanBuild'] = run(['powershell','-NoProfile','-File',clean_engine/'desktop/Build-Native.ps1','-Output',clean_engine/'build/ReleaseGate.exe'],clean_engine,work/'clean-build.log',env)
        else:
            report['cleanBuild'] = {'status':'not_run','reason':'No --mono-root or --native-windows supplied'}
        compare_maps(source_before,package_sources(clean_engine),'Extracted source after execution')
        report['failedStages'] = stage_errors
        report['status'] = 'fail' if stage_errors else ('pass' if report['cleanBuild']['status'] == 'pass' else 'incomplete')
    except Exception as error:
        report['status'] = 'fail'; report['error'] = str(error)
    finally:
        try:
            compare_maps(source_before,package_sources(engine),'Input engine unchanged')
            compare_maps(project_before,files(project),'Input project unchanged')
            compare_maps(baseline_before,files(baseline),'Baseline unchanged')
            report['inputsUnchanged'] = True
        except Exception as error:
            report['status'] = 'fail'; report['inputError'] = str(error)
        (work/'report.json').write_text(json.dumps(report,indent=2,ensure_ascii=False)+'\n')
    print(json.dumps(report,indent=2,ensure_ascii=False))
    return 0 if report['status'] == 'pass' else 1


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest='command',required=True)
    resource = sub.add_parser('resources')
    resource.add_argument('--baseline',required=True); resource.add_argument('--candidate',required=True)
    resource.add_argument('--allow-remove',action='append',default=[],help='Explicit kind/name; deletion must retain blank slot')
    imports = sub.add_parser('dependencies'); imports.add_argument('--engine',required=True)
    gate = sub.add_parser('verify')
    for arg in ('engine','project','entry','baseline','work'):
        gate.add_argument('--'+arg,required=True)
    gate.add_argument('--allow-remove',action='append',default=[])
    gate.add_argument('--node',default='node',help='Existing Node executable, e.g. runtime/node.exe')
    gate.add_argument('--archive',help='Verify an existing candidate archive against current input trees instead of creating it')
    gate.add_argument('--lifetime-seconds',type=int,choices=range(5,181),default=15,
                      metavar='5..180',help='Explicit recovered C# smoke soft budget; product default stays 15')
    builds = gate.add_mutually_exclusive_group()
    builds.add_argument('--mono-root',help='Existing Mono root layout used by build/mono_checks.py')
    builds.add_argument('--native-windows',action='store_true')
    args = parser.parse_args()
    if args.command == 'verify' and ('/' in args.node or '\\' in args.node):
        args.node = str(Path(args.node).resolve())
    try:
        if args.command == 'verify':
            return full_gate(args)
        result = (resource_integrity(Path(args.baseline).resolve(),Path(args.candidate).resolve(),args.allow_remove)
                  if args.command == 'resources' else dependency_closure(Path(args.engine).resolve()))
        print(json.dumps(result,indent=2)); return 0
    except Exception as error:
        print(json.dumps({'status':'fail','error':str(error)},indent=2),file=sys.stderr); return 1


if __name__ == '__main__':
    sys.exit(main())
