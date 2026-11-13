"""Runner regression tests: no display, native app, or GameMaker invocation."""
import copy,importlib.util,tempfile,unittest
from pathlib import Path
import run_timeline_checks as base
import run_track_display_checks as runner
class TrackDisplayRunnerTests(unittest.TestCase):
 def test_no_ui_flag(self):
  source=Path(runner.__file__).read_text()
  self.assertNotIn("add_argument('--ui'",source)
  self.assertIn('base.run_checks(root,out,a.mono_root,False,120)',source)
 def test_new_tests_required(self):
  old_pure=dict(base.PURE_MARKERS);old_ui=list(base.UI_TESTS)
  try:
   base.PURE_MARKERS['TrackDisplayFilterTests']=r'PASS';base.UI_TESTS.append('TrackDisplayFilterUiTests')
   report={'schema_version':1,'ui_requested':False,'stages':[],'status':'passed'}
   with self.assertRaises(ValueError):base.validate_report(report)
  finally:base.PURE_MARKERS.clear();base.PURE_MARKERS.update(old_pure);base.UI_TESTS[:]=old_ui
 def test_contract_failure_propagates(self):
  source=Path(runner.__file__).read_text();self.assertIn("if contract['status']!='passed':report['status']='failed'",source)
 def test_missing_compile_cannot_succeed(self):
  with tempfile.TemporaryDirectory() as tmp:
   executor=base.Executor(Path(tmp),{},1)
   record=executor.stage('missing','compile',['/bin/true'],artifact=Path(tmp)/'x.exe')
   self.assertEqual(record['status'],'missing_output')
if __name__=='__main__':unittest.main()
