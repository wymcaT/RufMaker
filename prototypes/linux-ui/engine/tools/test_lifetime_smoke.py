"""Smoke wrapper evidence/failure tests. No native program or UI is executed."""
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import run_lifetime_smoke as smoke


class SmokeTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.project = self.root / 'project'
        (self.project / 'rooms/rAvoidance').mkdir(parents=True)
        (self.project / 'rooms/rAvoidance/room.txt').write_text('width=800\nheight=608\nroomspeed=50\n')
        self.scene = self.project / 'rufmaker.json'
        self.scene.write_text(json.dumps({'duration': 2, 'tracks': []}))
        self.exe = self.root / 'test.exe'
        self.exe.write_bytes(b'fresh-compiled-placeholder')
        self.out = self.root / 'build/smoke'
        self.result = {'Stop': 'complete', 'LastFrame': 2, 'RequestedEnd': 2, 'Seconds': .01, 'Rows': [], 'Warnings': [], 'Error': ''}

    def tearDown(self):
        self.temp.cleanup()

    def run_fake(self, mode='result', **options):
        def execute(command, **kwargs):
            if command[0] == 'node':
                if mode == 'decoder-error':
                    raise subprocess.CalledProcessError(1, command, output='decoder failed')
                Path(command[-1]).write_text('{}')
                return subprocess.CompletedProcess(command, 0, '')
            if mode == 'timeout':
                raise subprocess.TimeoutExpired(command, 120, output='timeout log')
            if mode == 'mutate':
                self.scene.write_text('{}')
            if mode not in {'missing', 'nonzero'}:
                Path(command[-1]).write_text(json.dumps(self.result))
            return subprocess.CompletedProcess(command, 5 if mode == 'nonzero' else 0, 'observed stdout')
        with patch.object(smoke, 'toolchain', return_value=([], ['fake-runtime'], '-', {}, 'fake')), patch.object(smoke.subprocess, 'run', side_effect=execute):
            return smoke.run_smoke(self.root, self.root, self.exe, self.scene, self.out, **options)

    def test_valid_result_and_executable_hash(self):
        report = self.run_fake()
        self.assertEqual(report['status'], 'passed')
        self.assertTrue(report['input_files_verified_unchanged'])
        self.assertEqual(len(report['test_exe_sha256']), 64)
        self.assertEqual(len(report['result_sha256']), 64)

    def test_limit_is_explicit_partial(self):
        self.result.update(Stop='time-limit', LastFrame=1)
        self.assertEqual(self.run_fake()['status'], 'partial')

    def test_missing_or_stale_result_cannot_pass(self):
        self.out.mkdir(parents=True)
        (self.out / 'result.json').write_text(json.dumps(self.result))
        report = self.run_fake('missing')
        self.assertEqual(report['status'], 'failed')
        self.assertFalse((self.out / 'result.json').exists())
        self.assertTrue(report['input_files_verified_unchanged'])

    def test_analysis_error_cannot_pass(self):
        self.result.update(Stop='error', Error='missing mask')
        self.assertEqual(self.run_fake()['status'], 'failed')

    def test_false_complete_and_invalid_fields_cannot_pass(self):
        for change in [{'LastFrame': 1}, {'LastFrame': True}, {'Seconds': float('nan')}, {'RequestedEnd': 3}, {'Error': 'error despite success'}, {'Stop': 'made-up'}]:
            old = self.result.copy();self.result.update(change)
            self.assertEqual(self.run_fake()['status'], 'failed')
            self.result = old

    def test_process_decoder_timeout_errors_still_hash_inputs(self):
        for mode in ['nonzero', 'decoder-error', 'timeout']:
            report = self.run_fake(mode)
            self.assertEqual(report['status'], 'failed')
            self.assertTrue(report['input_files_verified_unchanged'])
            self.assertEqual(json.loads((self.out / 'report.json').read_text()), report)

    def test_explicit_budget_and_horizon_are_forwarded(self):
        self.result.update(RequestedEnd=1, LastFrame=1)
        report = self.run_fake(seconds=180, end_frame=1)
        self.assertEqual(report['status'], 'passed')
        self.assertEqual(report['budget_seconds'], 180)
        self.assertEqual(report['requested_horizon'], 1)
        self.assertIn('--bounded-smoke', report['command'])
        self.assertEqual(report['command'][-1], str(self.out / 'result.json'))

    def test_invalid_budget_or_horizon_is_rejected_before_execution(self):
        for options in [{'seconds': 0}, {'seconds': 181}, {'seconds': float('inf')},
                        {'seconds': float('nan')}, {'seconds': True}, {'end_frame': True},
                        {'end_frame': -1}, {'end_frame': 1.5}]:
            with self.assertRaises(ValueError):
                self.run_fake(**options)

    def test_input_mutation_fails_even_with_valid_result(self):
        self.assertEqual(self.run_fake('mutate')['status'], 'failed')


if __name__ == '__main__':
    unittest.main(verbosity=2)
