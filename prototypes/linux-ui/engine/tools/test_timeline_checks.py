"""Runner failure/report tests; does not execute native UI or product builds."""
import copy
import importlib.util
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('timeline_checks', Path(__file__).with_name('run_timeline_checks.py'))
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)


class RunnerChecks(unittest.TestCase):
    def setUp(self):
        build = Path(__file__).resolve().parents[1] / 'build'
        build.mkdir(exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(prefix='runner-tests-', dir=build)
        self.root = Path(self.temp.name)
        self.out = self.root / 'build/output'
        self.out.mkdir(parents=True)
        self.executor = runner.Executor(self.out, os.environ.copy(), .1)

    def tearDown(self):
        self.temp.cleanup()

    def command(self, code):
        return [sys.executable, '-c', code]

    def test_nonzero_is_failure(self):
        stage = self.executor.stage('error', 'pure', self.command('print("failure"); raise SystemExit(7)'))
        self.assertEqual(stage['status'], 'failed')
        self.assertEqual(stage['exit_code'], 7)
        self.assertIn('failure', (self.out / 'error.log').read_text())

    def test_timeout_is_recorded(self):
        stage = self.executor.stage('timeout', 'pure', self.command('import time; print("started", flush=True); time.sleep(5)'))
        self.assertEqual(stage['status'], 'timed_out')
        self.assertIsNone(stage['exit_code'])
        self.assertIn('started', (self.out / 'timeout.log').read_text())

    def test_missing_artifact_rejects_stale_output(self):
        artifact = self.out / 'old.exe'
        artifact.write_bytes(b'old successful build')
        stage = self.executor.stage('missing', 'compile', self.command('pass'), artifact=artifact)
        self.assertEqual(stage['status'], 'missing_output')
        self.assertFalse(artifact.exists())

    def test_fresh_artifact_hash(self):
        artifact = self.out / 'fresh.exe'
        stage = self.executor.stage('fresh', 'compile', self.command('from pathlib import Path; Path("fresh.exe").write_bytes(b"new")'), artifact=artifact)
        self.assertEqual(stage['status'], 'passed')
        self.assertEqual(len(stage['artifact_sha256']), 64)

    def test_missing_executable_is_recorded(self):
        stage = self.executor.stage('absent', 'pure', [str(self.out / 'no-such-process')])
        self.assertEqual(stage['status'], 'failed')
        self.assertIn('FileNotFoundError', stage['reason'])

    def test_zero_exit_without_marker_is_failure(self):
        stage = self.executor.stage('bad-marker', 'pure', self.command('print("PASS")'), marker=runner.PURE_MARKERS['TimelineRelationsTests'])
        self.assertEqual(stage['status'], 'failed')
        self.assertEqual(stage['exit_code'], 0)

    def test_marker_exact_and_unique(self):
        self.assertEqual(runner.check_count('PASS 23 pure simulated lifetime progress checks\n', runner.UI_PURE_MARKERS['SimulatedLifetimeUiTests']), 23)
        self.assertEqual(runner.check_count('PASS 68 pure simulated lifetime checks\n', runner.PURE_MARKERS['SimulatedLifetimeTests']), 68)
        self.assertEqual(runner.check_count('Pure reference checks: 29\nPASS\n', runner.PURE_MARKERS['TimelineRelationsTests']), 29)
        self.assertEqual(runner.check_count('PASS 20 pure lane-group folding checks\n', runner.PURE_MARKERS['LaneGroupFoldingTests']), 20)
        for text in ['PASS 0 pure lane-group folding checks', 'prefix PASS 20 pure lane-group folding checks',
                     'PASS 20 pure lane-group folding checks\nPASS 21 pure lane-group folding checks']:
            with self.assertRaises(ValueError):
                runner.check_count(text, runner.PURE_MARKERS['LaneGroupFoldingTests'])

    def fixture(self):
        desktop = self.root / 'desktop'
        (desktop / 'tests').mkdir(parents=True)
        (desktop / 'Build-Native.ps1').write_text("$names=@('Program')\n")
        (desktop / 'Program.cs').write_text('// fake test input')
        for name in list(runner.PURE_MARKERS) + runner.UI_TESTS:
            (desktop / 'tests' / (name + '.cs')).write_text('// fake test input')
        (self.root / 'assets/locales').mkdir(parents=True)
        (self.root / 'assets/locales/en.json').write_text('{}')
        (self.root / 'tools').mkdir()
        (self.root / 'tools/test_lane_folding_contract.py').write_text('print("PASS 1 source contracts (not GUI execution)")')
        (self.root / 'tools/test_lifetime_progress_contract.py').write_text('print("PASS 1 lifetime progress source contracts (not GUI execution)")')
        (self.root / 'tools/test_player_trace_contract.py').write_text('print("PASS 1 player trace source checks")')

    def fake_chain(self, fail_compile=False, fail_pure=False):
        code = 'import sys; from pathlib import Path; '
        code += 'raise SystemExit(9)' if fail_compile else 'Path(next(a[5:] for a in sys.argv if a.startswith("-out:"))).write_bytes(b"compiled")'
        runtime = 'import sys; '
        runtime += 'raise SystemExit(8)' if fail_pure else 'print("Pure reference checks: 29\\nPASS" if "Relations" in sys.argv[1] else "PASS 11 pure player trace session checks" if "PlayerTraceSessionTests" in sys.argv[1] else "PASS 23 pure simulated lifetime progress checks" if "SimulatedLifetimeUiTests" in sys.argv[1] else "PASS 61 pure simulated lifetime checks" if "SimulatedLifetime" in sys.argv[1] else "PASS 20 pure lane-group folding checks")'
        return self.command(code), self.command(runtime), '-', os.environ.copy(), 'unit-test-fake'

    def complete_report(self):
        self.fixture()
        with patch.object(runner, 'toolchain', return_value=self.fake_chain()):
            report = runner.run_checks(self.root, self.out, timeout=5)
        self.assertEqual(report['status'], 'passed')
        return report

    def test_default_end_to_end_no_ui_and_persisted_report(self):
        report = self.complete_report()
        self.assertTrue(all(s['status'] == 'not_run' for s in report['stages'] if s['kind'] == 'ui'))
        self.assertEqual(json.loads((self.out / 'report.json').read_text()), report)
        self.assertEqual(sum(s.get('checks', 0) for s in report['stages'] if s['kind'] == 'pure'), 144)
        runner.validate_report(report)
        with self.assertRaises(ValueError):
            runner.validate_report(report, require_ui=True)

    def test_compile_failure_persists_report_and_skips_execution(self):
        self.fixture()
        with patch.object(runner, 'toolchain', return_value=self.fake_chain(fail_compile=True)):
            report = runner.run_checks(self.root, self.out, timeout=5)
        self.assertEqual(report['status'], 'failed')
        self.assertTrue(all(s['status'] == 'not_run' for s in report['stages'] if s['kind'] == 'pure'))
        self.assertTrue((self.out / 'report.json').is_file())
        with self.assertRaises(ValueError):
            runner.validate_report(report)

    def test_compile_timeout_persists_report(self):
        self.fixture()
        chain = list(self.fake_chain())
        chain[0] = self.command('import time; time.sleep(5)')
        with patch.object(runner, 'toolchain', return_value=tuple(chain)):
            report = runner.run_checks(self.root, self.out, timeout=.05)
        self.assertEqual(report['status'], 'failed')
        self.assertTrue(all(s['status'] == 'timed_out' for s in report['stages'] if s['kind'] == 'compile'))
        self.assertEqual(json.loads((self.out / 'report.json').read_text()), report)
        with self.assertRaises(ValueError):
            runner.validate_report(report)

    def test_missing_contract_is_failure(self):
        self.fixture()
        (self.root / 'tools/test_lane_folding_contract.py').unlink()
        with patch.object(runner, 'toolchain', return_value=self.fake_chain()):
            report = runner.run_checks(self.root, self.out, timeout=5)
        self.assertEqual(report['status'], 'failed')
        with self.assertRaises(ValueError):
            runner.validate_report(report)

    def test_missing_progress_contract_is_failure(self):
        self.fixture()
        (self.root / 'tools/test_lifetime_progress_contract.py').unlink()
        with patch.object(runner, 'toolchain', return_value=self.fake_chain()):
            report = runner.run_checks(self.root, self.out, timeout=5)
        self.assertEqual(report['status'], 'failed')
        with self.assertRaises(ValueError):
            runner.validate_report(report)

    def test_missing_trace_contract_is_failure(self):
        self.fixture()
        (self.root / 'tools/test_player_trace_contract.py').unlink()
        with patch.object(runner, 'toolchain', return_value=self.fake_chain()):
            report = runner.run_checks(self.root, self.out, timeout=5)
        self.assertEqual(report['status'], 'failed')
        with self.assertRaises(ValueError):runner.validate_report(report)

    def test_missing_test_source_persists_failure(self):
        self.fixture()
        (self.root / 'desktop/tests/LaneGroupFoldingTests.cs').unlink()
        with patch.object(runner, 'toolchain', return_value=self.fake_chain()):
            report = runner.run_checks(self.root, self.out, timeout=5)
        self.assertEqual(report['status'], 'failed')
        stage = next(s for s in report['stages'] if s['name'] == 'LaneGroupFoldingTests.compile')
        self.assertIn('Missing test source', stage['reason'])

    def test_execution_failure_persists_report(self):
        self.fixture()
        with patch.object(runner, 'toolchain', return_value=self.fake_chain(fail_pure=True)):
            report = runner.run_checks(self.root, self.out, timeout=5)
        self.assertEqual(report['status'], 'failed')
        self.assertTrue(all(s['status'] == 'failed' for s in report['stages'] if s['kind'] == 'pure'))

    def test_preparation_error_persists_failure(self):
        report = runner.run_checks(self.root, self.out)
        self.assertEqual(report['status'], 'failed')
        self.assertEqual(report['stages'][0]['name'], 'preparation')
        self.assertTrue((self.out / 'report.json').is_file())

    def test_report_rejects_omitted_compile_pure_or_ui(self):
        report = self.complete_report()
        for name in ['product.compile', 'LaneGroupFoldingTests.pure', 'TimelineRelationsTests.ui', 'SimulatedLifetimeTests.compile', 'SimulatedLifetimeTests.pure', 'SimulatedLifetimeUiTests.ui', 'SimulatedLifetimeUiTests.pure', 'lifetime-progress-contract', 'PlayerTraceSessionTests.pure', 'player-trace-contract']:
            bad = copy.deepcopy(report)
            bad['stages'] = [s for s in bad['stages'] if s['name'] != name]
            with self.assertRaises(ValueError):
                runner.validate_report(bad)

    def test_report_rejects_false_pass_timeout_zero_checks_and_ui(self):
        report = self.complete_report()
        for change in ['timeout', 'zero', 'bad-exit', 'bool-exit', 'ui', 'duplicate', 'no-hash', 'bad-hash', 'no-artifact', 'contract-count']:
            bad = copy.deepcopy(report)
            pure = next(s for s in bad['stages'] if s['kind'] == 'pure')
            if change == 'timeout':
                pure['status'] = 'timed_out'
            elif change == 'zero':
                pure['checks'] = 0
            elif change == 'bad-exit':
                pure['exit_code'] = 1
            elif change == 'bool-exit':
                pure['exit_code'] = False
            elif change == 'contract-count':
                next(s for s in bad['stages'] if s['kind'] == 'contract')['checks'] = False
            elif change == 'bad-hash':
                bad['stages'][0]['artifact_sha256'] = 'pretend-hash'
            elif change == 'no-artifact':
                bad['stages'][0]['artifact'] = ''
            elif change == 'ui':
                ui = next(s for s in bad['stages'] if s['kind'] == 'ui')
                ui.update(status='passed', exit_code=0)
            elif change == 'duplicate':
                bad['stages'].append(copy.deepcopy(pure))
            else:
                bad['stages'][0].pop('artifact_sha256')
            with self.assertRaises(ValueError, msg=change):
                runner.validate_report(bad)

    def test_output_boundary_and_timeout_validation(self):
        with self.assertRaises(ValueError):
            runner.run_checks(self.root, self.root / 'outside-build')
        for timeout in [0, -1, float('nan'), float('inf')]:
            with self.assertRaises(ValueError):
                runner.run_checks(self.root, self.out, timeout=timeout)


if __name__ == '__main__':
    unittest.main(verbosity=2)
