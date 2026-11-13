"""Runner orchestration/reporting checks; pixel correctness is tested by C#."""
import json
from pathlib import Path
import runpy
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

class VisualOracleReportingTests(unittest.TestCase):
    def exercise(self, fail_sprite=False):
        with tempfile.TemporaryDirectory() as temp:
            output=Path(temp)/'result'
            calls=[]
            def fake_run(command, **kwargs):
                calls.append(command)
                if any(str(arg).startswith('@') for arg in command):
                    return subprocess.CompletedProcess(command,0,'','')
                name=Path(command[1]).stem
                if name=='CameraExportOracle':
                    text=json.dumps([{}]*54)
                elif name=='BlendArithmeticOracle':
                    text='\n'.join(','.join(map(str,[a,d,120,80,40]+[min(255,d+(v*a+127)//255) for v in [120,80,40]])) for a in [0,64,128,255] for d in [0,40,240])
                else:
                    self.assertEqual(name,'SpriteBlendingTests')
                    self.assertEqual(Path(command[2]),output/'sprite-fixture')
                    self.assertTrue(Path(command[2]).is_dir())
                    text='pixel assertions passed' if not fail_sprite else 'pixel assertion failed'
                return subprocess.CompletedProcess(command,1 if fail_sprite and name=='SpriteBlendingTests' else 0,text,'')
            with patch.object(sys,'argv',['runner','--mono-root',temp,'--output',str(output)]),patch('subprocess.run',side_effect=fake_run):
                if fail_sprite:
                    with self.assertRaises(SystemExit):runpy.run_path(str(Path(__file__).with_name('run_visual_oracles.py')),run_name='__main__')
                else:runpy.run_path(str(Path(__file__).with_name('run_visual_oracles.py')),run_name='__main__')
            self.assertEqual(len(calls),6)
            report=json.loads((output/'report.json').read_text())
            self.assertFalse(report['nativeGameMaker'])
            self.assertEqual(report['BlendArithmeticOracle']['arithmeticComparisons'],12)
            self.assertEqual(report['CameraExportOracle']['samples'],54)
            self.assertTrue((output/'SpriteBlendingTests.run.log').is_file())
            return report
    def test_success_distinguishes_bitmap_runtime(self):
        result=self.exercise()
        self.assertEqual(result['status'],'pass')
        self.assertEqual(result['SpriteBlendingTests']['validation'],'bitmap rendering on Mono')
        self.assertFalse(result['SpriteBlendingTests']['nativeGameMaker'])
    def test_render_failure_fails_gate(self):
        result=self.exercise(True)
        self.assertEqual(result['status'],'fail')
        self.assertEqual(result['failedOracle'],'SpriteBlendingTests')
        self.assertEqual(result['failedStage'],'execution')

if __name__=='__main__':unittest.main()
