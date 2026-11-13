#!/usr/bin/env python3
"""Build the isolated Linux UI prototype; never replaces the Windows executable."""
import argparse, os, re, shutil, subprocess, tempfile
from pathlib import Path
base = Path(__file__).resolve().parent
root = base / 'engine'
repo = base.parents[1]
p = argparse.ArgumentParser()
p.add_argument('--runtime', type=Path, help='Optional extracted Mono runtime root; otherwise use system Mono')
p.add_argument('--tests', action='store_true')
a = p.parse_args()
env = os.environ.copy()
if a.runtime:
    rt = a.runtime.resolve()
    env.update(MONO_PATH=str(rt/'usr/lib/mono/4.5'), MONO_CFG_DIR=str(rt/'etc'), LD_LIBRARY_PATH=str(rt/'usr/lib')+':'+str(rt/'usr/lib/x86_64-linux-gnu'))
    mono = str(rt/'usr/bin/mono')
    compiler = [mono, str(rt/'usr/lib/mono/4.5/mcs.exe')]
    refs = ['-r:'+str(rt/'usr/lib/mono/4.5'/f'{n}.dll') for n in ['System.Windows.Forms','System.Drawing','System.Web.Extensions','System.IO.Compression','System.IO.Compression.FileSystem']]
else:
    mono = shutil.which('mono') or 'mono'
    compiler = [shutil.which('mcs') or 'mcs']
    refs = ['-r:'+n for n in ['System.Windows.Forms','System.Drawing','System.Web.Extensions','System.IO.Compression','System.IO.Compression.FileSystem']]
for name in ['assets', 'templates']:
    target = root/name
    if not target.exists(): target.symlink_to(repo/name, target_is_directory=True)
names = re.findall(r"'([\w]+)'", (root/'desktop/Build-Native.ps1').read_text())
names = [n for n in names if (root/'desktop'/f'{n}.cs').exists()]
common = compiler+['-langversion:latest']+refs+['-resource:'+str(root/'assets/icons/icon.ico')+',RufMaker.App.ico','-resource:'+str(root/'assets/locales/en.json')+',RufMaker.en.json']+[str(root/'desktop'/f'{n}.cs') for n in names]
subprocess.run(common+['-target:winexe','-out:'+str(root/'RufMaker.Linux.exe')],env=env,check=True)
if a.tests:
    with tempfile.TemporaryDirectory(prefix='rufmaker-linux-test-') as td:
        exe = Path(td)/'Smoke.exe'; tone=Path(td)/'tone.wav'
        subprocess.run(['ffmpeg','-hide_banner','-loglevel','error','-f','lavfi','-i','sine=frequency=440:duration=2',str(tone)],check=True)
        subprocess.run(common+['-target:exe','-main:LinuxPortSmokeTests','-out:'+str(exe),str(base/'LinuxPortSmokeTests.cs')],env=env,check=True)
        subprocess.run([mono,str(exe),str(tone)],env=env,check=True)
