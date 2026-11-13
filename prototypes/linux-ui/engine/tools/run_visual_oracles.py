#!/usr/bin/env python3
"""Rebuild actual C# visual oracles from this source tree; no tool installation."""
from pathlib import Path
import argparse,json,os,re,subprocess
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--mono-root');p.add_argument('--output',required=True);a=p.parse_args()
root=Path(__file__).resolve().parents[1];out=Path(a.output).resolve();out.mkdir(parents=True,exist_ok=False)
env=os.environ.copy()
if a.mono_root:
 rt=Path(a.mono_root).resolve();runtime=[str(rt/'usr/bin/mono-sgen')];compiler=runtime+[str(rt/'usr/lib/mono/4.5/mcs.exe')]
 env.update(MONO_PATH=str(rt/'usr/lib/mono/4.5'),MONO_CFG_DIR=str(rt/'etc'),LD_LIBRARY_PATH=str(rt/'usr/lib')+':'+str(rt/'usr/lib/x86_64-linux-gnu'))
else:
 if os.name!='nt':raise SystemExit('Supply an existing --mono-root on non-Windows systems')
 compiler=[str(Path(os.environ['WINDIR'])/'Microsoft.NET/Framework64/v4.0.30319/csc.exe')];runtime=[]
names=[]
for block in re.findall(r'\$names\s*\+?=\s*@\(([^)]*)\)',(root/'desktop/Build-Native.ps1').read_text(encoding='utf-8-sig')):names+=re.findall(r"'([^']+)'",block)
assert len(names)==len(set(names)), 'Duplicate explicit product source'
refs=['System.Windows.Forms','System.Drawing','System.Web.Extensions','System.IO.Compression','System.IO.Compression.FileSystem']
common=['/nologo','/langversion:5','/platform:x86','/optimize+','/codepage:65001','/target:exe']+['/reference:'+x+'.dll' for x in refs]
results={'status':'running','platform':'Mono' if a.mono_root else 'Windows .NET','nativeGameMaker':False}
for name,files in [('CameraExportOracle',[root/'desktop'/(n+'.cs') for n in names if n!='Program']),('BlendArithmeticOracle',[root/'desktop/SpriteBlending.cs']),('SpriteBlendingTests',[root/'desktop'/(n+'.cs') for n in names if n!='Program'])]:
 exe=out/(name+'.exe');files=files+[root/'desktop/tests'/(name+'.cs')]
 args=common+['/main:'+name,'/out:'+str(exe)]+list(map(str,files));rsp=out/(name+'.rsp');rsp.write_text('\n'.join('"'+x+'"' for x in args))
 run=subprocess.run(compiler+['@'+str(rsp)],cwd=root,env=env,text=True,capture_output=True,timeout=120)
 (out/(name+'.compile.log')).write_text(run.stdout+run.stderr)
 if run.returncode!=0:
  results.update(status='fail',failedOracle=name,failedStage='compile');(out/'report.json').write_text(json.dumps(results,indent=2)+'\n');raise SystemExit(name+' compile failed')
 run_args=[]
 if name=='SpriteBlendingTests':
  fixture=out/'sprite-fixture';fixture.mkdir();run_args=[str(fixture)]
 run=subprocess.run(runtime+[str(exe)]+run_args,cwd=out,env=env,text=True,capture_output=True,timeout=60)
 (out/(name+'.run.log')).write_text(run.stdout+run.stderr)
 if run.returncode!=0:
  results.update(status='fail',failedOracle=name,failedStage='execution');(out/'report.json').write_text(json.dumps(results,indent=2)+'\n');raise SystemExit(name+' execution failed')
 if name=='CameraExportOracle':
  rows=json.loads(run.stdout);assert len(rows)==54;dest=out/'camera-oracle.json';dest.write_text(run.stdout);results[name]={'samples':len(rows),'json':str(dest)}
 elif name=='BlendArithmeticOracle':
  rows=[list(map(int,line.split(','))) for line in run.stdout.splitlines() if re.match(r'^\d+,',line)];assert len(rows)==12
  for alpha,d,r,g,b,rr,gg,bb in rows:assert [rr,gg,bb]==[min(255,d+(v*alpha+127)//255) for v in [r,g,b]],(alpha,d,r,g,b,rr,gg,bb)
  results[name]={'arithmeticComparisons':len(rows)}
 else:
  results[name]={'status':'pass','validation':'bitmap rendering on '+results['platform'],'nativeGameMaker':False}
results['status']='pass';(out/'report.json').write_text(json.dumps(results,indent=2)+'\n');print(json.dumps(results,indent=2))
