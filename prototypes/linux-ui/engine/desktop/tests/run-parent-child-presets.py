"""Rebuild parent/child model fixtures and verify generated GML; not a GM runner."""
from pathlib import Path
import argparse,json,os,re,subprocess,sys
p=argparse.ArgumentParser(description=__doc__);p.add_argument('mono_root');p.add_argument('--node',default=os.environ.get('RUFMAKER_RELEASE_NODE','node'));args=p.parse_args()
src=Path(__file__).resolve().parents[2];rt=Path(args.mono_root).resolve();out=src/'build/parent-child';out.mkdir(parents=True,exist_ok=True)
env=os.environ.copy();env.update(MONO_PATH=str(rt/'usr/lib/mono/4.5'),MONO_CFG_DIR=str(rt/'etc'),LD_LIBRARY_PATH=str(rt/'usr/lib')+':'+str(rt/'usr/lib/x86_64-linux-gnu'),HOME=str(out),XDG_CACHE_HOME=str(out/'cache'))
results={'status':'running','platform':'Mono','nativeGameMaker':False,'guiInteraction':False}
def run(command,name,cwd=src):
 try:r=subprocess.run(command,cwd=cwd,env=env,capture_output=True,text=True,timeout=120)
 except subprocess.TimeoutExpired as error:
  (out/(name+'.log')).write_text('Timed out after 120 seconds\n'+str(error));raise RuntimeError(name+' timed out')
 (out/(name+'.log')).write_text(r.stdout+r.stderr);print(name,r.returncode,r.stdout+r.stderr,flush=True)
 if r.returncode!=0:raise RuntimeError(name+' failed with exit '+str(r.returncode))
 return r.stdout
try:
 names=[]
 for block in re.findall(r'\$names\s*\+?=\s*@\(([^)]*)\)',(src/'desktop/Build-Native.ps1').read_text(encoding='utf-8-sig')):names+=re.findall(r"'([^']+)'",block)
 if not names or len(names)!=len(set(names)):raise RuntimeError('Missing or duplicate explicit product sources')
 common=['-langversion:5','-platform:x86','-optimize+']+['-r:'+r for r in ['System.Windows.Forms','System.Drawing','System.Web.Extensions','System.IO.Compression','System.IO.Compression.FileSystem']]+['-resource:'+str(src/'assets/locales/en.json')+',RufMaker.en.json']
 for name in ['Product','ParentChildPresetTests','SpawnTimingTests']:
  sources=[str(src/'desktop'/(n+'.cs')) for n in names if name=='Product' or n!='Program']
  command=common+['-out:'+str(out/(name+'.exe'))]+(['-target:winexe'] if name=='Product' else ['-target:exe','-main:'+name,str(src/'desktop/tests'/(name+'.cs'))])+sources
  run([str(rt/'usr/bin/mono-sgen'),str(rt/'usr/lib/mono/4.5/mcs.exe')]+command,name+'.compile');results[name]={'compile':0}
  if name!='Product':
   text=run([str(rt/'usr/bin/mono-sgen'),str(out/(name+'.exe')),str(out)],name+'.run',out);results[name]['run']=0
   match=re.search(r'PASS (?:SpawnTiming )?(\d+)',text)
   if match:results[name]['checks']=int(match.group(1))
 text=run([args.node,'desktop/tests/verify-parent-child-presets.mjs',str(out)],'gml');results['generatedGml']={'status':'pass','checks':int(re.search(r'PASS (\d+)',text).group(1))}
 run([args.node,'--test','studio/spawn-timing.test.mjs','studio/template-system.test.mjs','studio/presets.test.mjs'],'node');results['targetedNode']={'status':'pass'}
 results['status']='pass'
except Exception as error:
 results.update(status='fail',error=str(error));print(str(error),file=sys.stderr)
finally:(out/'report.json').write_text(json.dumps(results,indent=2)+'\n')
print(json.dumps(results,indent=2));sys.exit(0 if results['status']=='pass' else 1)
