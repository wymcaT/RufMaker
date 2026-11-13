"""Run explicit C#5 product + pure timeline/filter tests and compile UI suites only.
No UI/display opt-in exists here. Existing native acceptance sources remain available
for a separately authorized Windows check. Outputs must be within this tree/build.
"""
import argparse,json,sys
from pathlib import Path
import run_timeline_checks as base

def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--mono-root',type=Path,required=True);p.add_argument('--output',type=Path);a=p.parse_args()
 root=Path(__file__).resolve().parents[1];out=(a.output or root/'build/track-display-checks').resolve()
 base.PURE_MARKERS['TrackDisplayFilterTests']=r'(?m)^PASS ([1-9][0-9]*) pure track-display filter checks\r?$'
 base.UI_TESTS.append('TrackDisplayFilterUiTests')
 report=base.run_checks(root,out,a.mono_root,False,120)
 cc,rt,prefix,env,name=base.toolchain(a.mono_root,out)
 contract=base.Executor(out,env,120).stage('track-display-contract','contract',[sys.executable,str(root/'tools/test_track_display_contract.py')],marker=r'(?m)^PASS ([1-9][0-9]*) track-display source contracts \(not GUI execution\)\r?$')
 report['stages'].append(contract)
 if contract['status']!='passed':report['status']='failed'
 base.save_report(report,out)
 print(json.dumps({'status':report['status'],'report':str(out/'report.json'),'ui_execution':'not_run'},indent=2))
 return 0 if report['status']=='passed' else 1
if __name__=='__main__':sys.exit(main())
