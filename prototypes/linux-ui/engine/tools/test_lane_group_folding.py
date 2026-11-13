"""Isolated Mono C#5 build, pure tests, and optional native-control tests (--ui).
Uses explicit product source list. No project.mjs imports or Windows operations.
"""
import os,re,subprocess,json,argparse
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--mono-root',required=True);p.add_argument('--ui',action='store_true');a=p.parse_args()
root=Path(__file__).resolve().parents[1];rt=Path(a.mono_root).resolve();out=root/'build/lane-folding-check';out.mkdir(parents=True,exist_ok=True)
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
 compile('folding',['-target:exe','-main:LaneGroupFoldingTests',str(root/'desktop/tests/LaneGroupFoldingTests.cs')])
 run('folding-pure',[mono,str(out/'folding.exe')])
 run('source-contracts',[os.sys.executable,str(root/'tools/test_lane_folding_contract.py')])
 compile('folding-ui',['-target:exe','-main:LaneGroupFoldingUiTests',str(root/'desktop/tests/LaneGroupFoldingUiTests.cs')])
 if a.ui:run('folding-ui-run',[mono,str(out/'folding-ui.exe')])
 else:results['folding-ui-run']='not run: no supported native display'
 for name in ['TimelineEventMarkerTests','TimelineGroupTests']:
  compile(name,['-target:exe','-main:'+name,str(root/'desktop/tests'/(name+'.cs'))])

finally:(out/'results.json').write_text(json.dumps(results,indent=2)+'\n')
