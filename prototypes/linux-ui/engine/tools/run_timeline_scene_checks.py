"""Read-only event/source declaration checks against a real scene, not native UI/GML."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys
from release_gate import files
from run_timeline_checks import check_count, toolchain

MARKER = r'(?m)^PASS ([1-9][0-9]*) read-only scene reference checks\r?$'


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def definitions(tracks):
    for track in tracks:
        yield track
        yield from definitions(track.get('children', []))


def validate_result(path, scene, output):
    value = json.loads(Path(path).read_text(encoding='utf-8-sig'))
    data = json.loads(Path(scene).read_text(encoding='utf-8-sig'))
    source = data['tracks']
    if not isinstance(value, dict) or type(value.get('schemaVersion')) is not int or value['schemaVersion'] != 1 or value.get('status') != 'passed':
        raise ValueError('Invalid scene reference result schema/status')
    if type(value.get('checks')) is not int or value['checks'] < 1 or check_count(output, MARKER) != value['checks']:
        raise ValueError('Scene reference check marker/count mismatch')
    if value.get('sourcePath') != str(Path(scene).resolve()) or value.get('sourceSha256') != sha(scene):
        raise ValueError('Scene reference input identity mismatch')
    for name, expected in [('trackCount',len(source)),('fps',data['fps']),('duration',data['duration'])]:
        if type(value.get(name)) is not int or value[name] != expected:
            raise ValueError('Scene reference input metadata mismatch: '+name)
    counts = value.get('counts')
    required = ['ordinaryControllers','eventPresets','scripts','templateRoots','localDefinitions',
                'spawnRules','linkedSpawnRules','sourceSelectors','tagSelectors','patterns']
    if not isinstance(counts,dict) or any(type(counts.get(k)) is not int or counts[k] < 0 for k in required):
        raise ValueError('Invalid scene reference inventory')
    if counts['ordinaryControllers'] + counts['eventPresets'] != sum(t['kind']=='control' for t in source):
        raise ValueError('Controller inventory mismatch')
    if counts['scripts'] != sum(t['kind']=='script' for t in source) or counts['patterns'] != sum(t['kind']=='pattern' for t in source):
        raise ValueError('Script/pattern inventory mismatch')
    tracks = value.get('tracks')
    if not isinstance(tracks,list) or len(tracks)!=len(source):
        raise ValueError('Incomplete scene reference rows')
    if [(t.get('id'),t.get('kind')) for t in tracks] != [(t['id'],t['kind']) for t in source]:
        raise ValueError('Scene reference row identities mismatch')
    for row in tracks:
        if row.get('clock') not in ('template-local','song','pattern-container') or not isinstance(row.get('timing'),str):
            raise ValueError('Invalid scene reference clock/timing')
        refs=row.get('references')
        if not isinstance(refs,list) or type(row.get('referenceCount')) is not int or row['referenceCount']!=len(refs):
            raise ValueError('Invalid scene reference count')
        for ref in refs:
            if not isinstance(ref,dict) or any(not isinstance(ref.get(k),str) for k in ('relation','id','name','detail')) or type(ref.get('resolved')) is not bool or not isinstance(ref.get('navigationId'),(str,type(None))):
                raise ValueError('Invalid scene reference detail')
    if not isinstance(value.get('limitations'),list) or not value['limitations'] or any(not isinstance(v,str) for v in value['limitations']):
        raise ValueError('Missing scene reference limits')
    return value



def validate_evidence(report, out, executable, scene, input_root):
    out,executable,scene,input_root=[Path(p).resolve() for p in (out,executable,scene,input_root)]
    if report.get('status')!='passed' or report.get('input_files_verified_unchanged') is not True or type(report.get('exit_code')) is not int or report['exit_code']!=0 or report.get('error'):
        raise ValueError('Scene reference evidence failed')
    for key,path in [('scene',scene),('input_root',input_root),('executable',executable)]:
        if report.get(key)!=str(path):raise ValueError('Scene reference evidence scope mismatch: '+key)
    if report.get('executable_sha256')!=sha(executable) or report.get('result_sha256')!=sha(out/'result.json') or report.get('input_manifest_sha256')!=sha(out/'input-hashes.json'):
        raise ValueError('Scene reference evidence hash mismatch')
    manifest=json.loads((out/'input-hashes.json').read_text())
    if manifest!=files(input_root) or type(report.get('input_file_count')) is not int or report['input_file_count']!=len(manifest):
        raise ValueError('Scene reference input manifest mismatch')
    result=validate_result(out/'result.json',scene,(out/'run.log').read_text())
    if type(report.get('checks')) is not int or report['checks']!=result['checks'] or type(report.get('track_count')) is not int or report['track_count']!=result['trackCount'] or report.get('counts')!=result['counts']:
        raise ValueError('Scene reference evidence result mismatch')
    return result


def run_checks(root, mono_root, executable, scene, input_root, out):
    root,executable,scene,input_root,out = [Path(p).resolve() for p in (root,executable,scene,input_root,out)]
    if not out.is_relative_to(root/'build') or out.is_relative_to(input_root) or not scene.is_relative_to(input_root):
        raise ValueError('Output/input boundary invalid')
    out.mkdir(parents=True,exist_ok=True)
    result_path=out/'result.json';report={'status':'failed','scope':'Read-only declaration/navigation model checks; no native UI, GML, F5 or bridge execution.',
        'scene':str(scene),'input_root':str(input_root),'executable':str(executable),'input_files_verified_unchanged':False}
    before=None;output=''
    try:
        if result_path.exists():result_path.unlink()
        before=files(input_root);report['input_file_count']=len(before)
        (out/'input-hashes.json').write_text(json.dumps(before,indent=2)+'\n')
        report['input_manifest_sha256']=sha(out/'input-hashes.json');report['executable_sha256']=sha(executable)
        if not executable.stat().st_size:raise ValueError('Empty scene reference executable')
        _,runtime,_,env,_=toolchain(mono_root,out)
        command=runtime+[str(executable),'--scene',str(scene),str(result_path)];report['command']=command
        completed=subprocess.run(command,cwd=out,env=env,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True,timeout=120)
        output=completed.stdout or '';report['exit_code']=completed.returncode
        if completed.returncode!=0:raise ValueError('Scene reference process failed')
        value=validate_result(result_path,scene,output)
        if sha(executable)!=report['executable_sha256']:raise ValueError('Executable changed while running')
        report.update(status='passed',checks=value['checks'],track_count=value['trackCount'],counts=value['counts'],limitations=value['limitations'],result_sha256=sha(result_path))
    except Exception as error:
        extra=getattr(error,'stdout','') or ''
        if isinstance(extra,bytes):extra=extra.decode('utf-8','replace')
        output+=extra;report['error']=type(error).__name__+': '+str(error)
    finally:
        try:report['input_files_verified_unchanged']=before is not None and before==files(input_root)
        except Exception as error:report['integrity_error']=str(error)
        if not report['input_files_verified_unchanged']:report['status']='failed'
        (out/'run.log').write_text(output,encoding='utf-8');(out/'report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    return report


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--mono-root',type=Path,required=True);parser.add_argument('--test-exe',type=Path,required=True)
    parser.add_argument('--scene',type=Path,required=True);parser.add_argument('--input-root',type=Path,required=True);parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args();report=run_checks(Path(__file__).resolve().parents[1],args.mono_root,args.test_exe,args.scene,args.input_root,args.output)
    print(json.dumps(report,ensure_ascii=False,indent=2));return 0 if report['status']=='passed' else 1


if __name__=='__main__':sys.exit(main())
