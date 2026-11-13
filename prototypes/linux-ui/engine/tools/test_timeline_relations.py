"""Isolated Mono C#5 build, pure tests, and optional native-control tests (--ui).
Uses explicit product source list. No project.mjs imports or Windows operations.
"""
import os,re,subprocess,json,argparse
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--mono-root',required=True);p.add_argument('--ui',action='store_true');a=p.parse_args()
root=Path(__file__).resolve().parents[1];rt=Path(a.mono_root).resolve();out=root/'build/timeline-relations-check';out.mkdir(parents=True,exist_ok=True)
env=os.environ.copy();env.update(MONO_PATH=str(rt/'usr/lib/mono/4.5'),MONO_CFG_DIR=str(rt/'etc'),LD_LIBRARY_PATH=str(rt/'usr/lib')+':'+str(rt/'usr/lib/x86_64-linux-gnu'),HOME=str(out/'home'),XDG_CACHE_HOME=str(out/'cache'),RUFMAKER_ROOT=str(out),RUFMAKER_PROJECT=str(out/'project'))
for d in ['home','cache','project']: (out/d).mkdir(exist_ok=True)
names=[]
for block in re.findall(r'\$names\s*\+?=\s*@\(([^)]*)\)',(root/'desktop/Build-Native.ps1').read_text(encoding='utf-8-sig')):names+=re.findall(r"'([^']+)'",block)
sources=[str(root/'desktop'/(n+'.cs')) for n in names];mono=str(rt/'usr/bin/mono-sgen');compiler=str(rt/'usr/lib/mono/4.5/mcs.exe')
common=['-nologo','-langversion:5','-platform:x86','-optimize+','-codepage:65001']+['-r:'+r for r in ['System.Windows.Forms','System.Drawing','System.Web.Extensions','System.IO.Compression','System.IO.Compression.FileSystem']]+['-resource:'+str(root/'assets/locales/en.json')+',RufMaker.en.json']
results={}
def run(label,cmd):
 r=subprocess.run(cmd,cwd=out,env=env,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True,timeout=120);(out/(label+'.log')).write_text(r.stdout);print(label,r.returncode,r.stdout,flush=True);results[label]=r.returncode
 if r.returncode: raise RuntimeError(label)
def compile(label,extra):run(label,[mono,compiler]+common+['-out:'+str(out/(label+'.exe'))]+extra+sources)
try:
 compile('product',['-target:winexe'])
 compile('relations',['-target:exe','-main:TimelineRelationsTests',str(root/'desktop/tests/TimelineRelationsTests.cs')])
 run('relations-pure',[mono,str(out/'relations.exe')])
 for name in ['TimelineEventMarkerTests','TimelineGroupTests']:
  compile(name,['-target:exe','-main:'+name,str(root/'desktop/tests'/(name+'.cs'))])
 if a.ui:
  run('relations-ui',[mono,str(out/'relations.exe'),'--ui'])
  for name in ['TimelineEventMarkerTests','TimelineGroupTests']:
   run(name+'-ui',[mono,str(out/(name+'.exe')),str(out/name)])
finally:(out/'results.json').write_text(json.dumps(results,indent=2)+'\n')
