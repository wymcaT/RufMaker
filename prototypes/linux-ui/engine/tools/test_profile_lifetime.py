"""Benchmark report failure-injection checks; no compiler, UI or project execution."""
import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import profile_lifetime as profile


class ProfileChecks(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='ruf-lifetime-benchmark-')
        self.addCleanup(self.temp.cleanup)
        self.top = Path(self.temp.name)
        self.root = self.top/'candidate'; self.baseline = self.top/'baseline'
        for root in [self.root, self.baseline]:
            (root/'desktop').mkdir(parents=True)
            (root/'desktop/Fake.cs').write_text('fixture source')
        self.inputs = self.root/'paired-project'; self.inputs.mkdir()
        self.scene = self.inputs/'rufmaker.json'; self.scene.write_text('{"duration":2}')
        self.out = self.root/'build/comparison'
        self.mutation = None
        self.calls = []

    def compile(self, root, mono, out, harness):
        out.mkdir(parents=True)
        exe = out/'LifetimeBenchmark.exe'; exe.write_bytes(b'fresh '+str(root).encode())
        manifest = out/'source-hashes.json'
        manifest.write_text(json.dumps({'desktop/Fake.cs': profile.sha(root/'desktop/Fake.cs')}))
        return {'source_root':str(root),'toolchain':'test fake','executable':str(exe),
                'executable_sha256':profile.sha(exe),'source_manifest':str(manifest),
                'source_manifest_sha256':profile.sha(manifest),'command':['fake compile']}

    def smoke(self, root, mono, exe, scene, out, seconds, horizon):
        out.mkdir(parents=True,exist_ok=True)
        label = 'baseline' if 'baseline-compile' in exe else 'candidate'
        self.calls.append(label)
        result={'RequestedEnd':horizon,'LastFrame':horizon,'Seconds':2.0 if label=='baseline' else 1.0,
                'Stop':'complete','Error':'','Rows':[{'Id':'same','Alive':{'Samples':3}}],'Warnings':[]}
        report={'status':'passed'}
        if self.mutation=='partial':
            result.update(Stop='time-limit',LastFrame=1);report['status']='partial'
        if self.mutation=='parity' and label=='candidate': result['Rows'][0]['Alive']['Samples']=4
        if self.mutation=='error': report['status']='failed'
        if self.mutation=='input': (self.inputs/'new-file').write_text('changed')
        if self.mutation=='source': (self.root/'desktop/Fake.cs').write_text('changed after compile')
        if self.mutation=='harness': (self.out/'LifetimeBenchmark.cs').write_text('changed after compile')
        (out/'result.json').write_text(json.dumps(result))
        (out/'report.json').write_text(json.dumps(report))
        (out/'room.json').write_text('{}')
        (out/'metadata.json').write_text('{"different":true}' if self.mutation=='metadata' and label=='candidate' else '{}')
        return report

    def run_report(self):
        with patch.object(profile,'compile_harness',side_effect=self.compile), patch.object(profile,'run_smoke',side_effect=self.smoke):
            return profile.benchmark(self.root,self.baseline,None,self.scene,self.inputs,self.out,runs=2)

    def test_canonical_ignores_only_wall_time(self):
        before={'Seconds':1,'Rows':[{'Id':'a','Alive':{'Samples':2}}],'Warnings':['unknown'],'Stop':'complete'}
        after=copy.deepcopy(before);after['Seconds']=5
        self.assertEqual(profile.canonical(before),profile.canonical(after))
        for field,value in [('Rows',[{'Id':'a','Alive':{'Samples':3}}]),('Warnings',[]),('Stop','time-limit')]:
            changed=copy.deepcopy(after);changed[field]=value
            self.assertNotEqual(profile.canonical(before),profile.canonical(changed))
        self.assertEqual(before['Seconds'],1)

    def test_complete_exact_alternating_report(self):
        report=self.run_report()
        self.assertEqual(report['status'],'passed')
        self.assertTrue(report['all_completed'] and report['exact_result_parity'] and report['input_files_verified_unchanged'])
        self.assertEqual(self.calls,['baseline','candidate','candidate','baseline'])
        self.assertEqual(report['median_seconds'],{'baseline':2.0,'candidate':1.0})
        self.assertEqual(json.loads((self.out/'report.json').read_text()),report)

    def test_partial_has_no_speedup_claim(self):
        self.mutation='partial';report=self.run_report()
        self.assertEqual(report['status'],'partial');self.assertFalse(report['exact_result_parity'])
        self.assertNotIn('speedup',report);self.assertNotIn('median_seconds',report)

    def test_mismatching_results_fail(self):
        self.mutation='parity';report=self.run_report()
        self.assertEqual(report['status'],'failed');self.assertFalse(report['exact_result_parity'])
        self.assertNotIn('speedup',report)

    def test_different_decoded_inputs_fail(self):
        self.mutation='metadata';report=self.run_report()
        self.assertEqual(report['status'],'failed');self.assertFalse(report['identical_decoded_inputs'])

    def test_process_failure_or_changed_input_fails(self):
        self.mutation='error';report=self.run_report();self.assertEqual(report['status'],'failed')

    def test_changed_input_fails_even_with_exact_results(self):
        self.mutation='input';report=self.run_report()
        self.assertEqual(report['status'],'failed');self.assertFalse(report['input_files_verified_unchanged'])

    def test_source_change_after_compile_invalidates_claim(self):
        self.mutation='source';report=self.run_report()
        self.assertEqual(report['status'],'failed')
        self.assertTrue(report['exact_result_parity'])
        self.assertFalse(report['source_files_verified_unchanged'])
        self.assertIn('desktop/Fake.cs',report['source_changes_after_compile']['candidate'])

    def test_harness_change_after_compile_invalidates_claim(self):
        self.mutation='harness';report=self.run_report()
        self.assertEqual(report['status'],'failed')
        self.assertFalse(report['harness_verified_unchanged'])

    def test_bad_output_boundary_or_run_count_rejected(self):
        for out,runs in [(self.root/'outside',2),(self.out,0),(self.out,True),(self.out,6)]:
            with self.subTest(out=out,runs=runs),self.assertRaises(ValueError):
                profile.benchmark(self.root,self.baseline,None,self.scene,self.inputs,out,runs=runs)
        with self.assertRaises(ValueError):
            profile.benchmark(self.root,self.baseline,None,self.scene,self.root/'unrelated',self.out)


if __name__=='__main__':unittest.main(verbosity=2)
