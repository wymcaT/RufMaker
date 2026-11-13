"""Failure injection for actual-scene wrapper. No product/UI/GM execution."""
import copy
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import run_timeline_scene_checks as runner


class SceneChecks(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(prefix='ruf-scene-reference-');self.addCleanup(self.temp.cleanup)
        self.root=Path(self.temp.name);self.inputs=self.root/'paired';self.inputs.mkdir()
        self.scene=self.inputs/'scene.json';self.scene.write_text(json.dumps({'fps':50,'duration':100,'tracks':[{'id':'a','kind':'object'}]}))
        self.exe=self.root/'test.exe';self.exe.write_bytes(b'fresh')
        self.out=self.root/'build/result';self.out.mkdir(parents=True)
        self.result={'schemaVersion':1,'status':'passed','checks':3,'sourcePath':str(self.scene),'sourceSha256':runner.sha(self.scene),
            'trackCount':1,'fps':50,'duration':100,'counts':{k:0 for k in ['ordinaryControllers','eventPresets','scripts','templateRoots','localDefinitions','spawnRules','linkedSpawnRules','sourceSelectors','tagSelectors','patterns']},
            'tracks':[{'id':'a','kind':'object','clock':'song','timing':'declared','referenceCount':0,'references':[]}],
            'limitations':['No GML or UI execution.']}

    def write_result(self,value=None):
        path=self.out/'result.json';path.write_text(json.dumps(self.result if value is None else value));return path

    def test_valid_result_is_bound_to_source_and_marker(self):
        self.assertEqual(runner.validate_result(self.write_result(),self.scene,'PASS 3 read-only scene reference checks\n')['checks'],3)

    def test_invalid_schema_count_identity_metadata_rejected(self):
        for field,value in [('schemaVersion',True),('status','partial'),('checks',True),('checks',2),('sourcePath','wrong'),('sourceSha256','0'*64),('trackCount',True),('fps',30),('duration',99)]:
            with self.subTest(field=field),self.assertRaises(ValueError):
                bad=copy.deepcopy(self.result);bad[field]=value
                runner.validate_result(self.write_result(bad),self.scene,'PASS 3 read-only scene reference checks\n')

    def test_missing_or_duplicate_marker_rejected(self):
        for text in ['', 'PASS', 'PASS 3 read-only scene reference checks\n'*2]:
            with self.subTest(text=text),self.assertRaises(ValueError):runner.validate_result(self.write_result(),self.scene,text)

    def test_missing_rows_wrong_ids_or_clock_rejected(self):
        for mutate in [lambda r:r.update(tracks=[]),lambda r:r['tracks'][0].update(id='other'),lambda r:r['tracks'][0].update(clock='executed-gml'),lambda r:r['tracks'][0].update(referenceCount=1),lambda r:r.update(limitations=[])]:
            bad=copy.deepcopy(self.result);mutate(bad)
            with self.assertRaises(ValueError):runner.validate_result(self.write_result(bad),self.scene,'PASS 3 read-only scene reference checks\n')

    def test_false_counts_rejected(self):
        for key in ['scripts','ordinaryControllers','patterns']:
            bad=copy.deepcopy(self.result);bad['counts'][key]=1
            with self.assertRaises(ValueError):runner.validate_result(self.write_result(bad),self.scene,'PASS 3 read-only scene reference checks\n')
        bad=copy.deepcopy(self.result);bad['counts']['spawnRules']=True
        with self.assertRaises(ValueError):runner.validate_result(self.write_result(bad),self.scene,'PASS 3 read-only scene reference checks\n')

    def process(self,code=0,write=True,change_input=False):
        if write:self.write_result()
        if change_input:(self.inputs/'changed').write_text('bad')
        return subprocess.CompletedProcess([],code,'PASS 3 read-only scene reference checks\n')

    def run_wrapper(self,**kwargs):
        with patch.object(runner,'toolchain',return_value=(None,[],None,{},'mock')),patch.object(runner.subprocess,'run',side_effect=lambda *a,**k:self.process(**kwargs)):
            return runner.run_checks(self.root,None,self.exe,self.scene,self.inputs,self.out)

    def test_fresh_success_and_input_manifest(self):
        report=self.run_wrapper();self.assertEqual(report['status'],'passed');self.assertTrue(report['input_files_verified_unchanged'])
        self.assertEqual(report['input_file_count'],1);self.assertEqual(report['result_sha256'],runner.sha(self.out/'result.json'))

    def test_evidence_rejects_stale_hash_or_false_success(self):
        report=self.run_wrapper()
        runner.validate_evidence(report,self.out,self.exe,self.scene,self.inputs)
        for key,value in [('status','partial'),('executable_sha256','0'*64),('result_sha256','0'*64),('input_file_count',True),('checks',False),('exit_code',False)]:
            bad=copy.deepcopy(report);bad[key]=value
            with self.subTest(key=key),self.assertRaises(ValueError):runner.validate_evidence(bad,self.out,self.exe,self.scene,self.inputs)

    def test_failed_process_never_uses_old_success(self):
        self.write_result();report=self.run_wrapper(code=1,write=False)
        self.assertEqual(report['status'],'failed');self.assertFalse((self.out/'result.json').exists())

    def test_zero_exit_without_result_fails(self):
        self.write_result();report=self.run_wrapper(write=False)
        self.assertEqual(report['status'],'failed');self.assertFalse((self.out/'result.json').exists())

    def test_input_mutation_overrides_pass(self):
        report=self.run_wrapper(change_input=True)
        self.assertEqual(report['status'],'failed');self.assertFalse(report['input_files_verified_unchanged'])

    def test_timeout_and_boundary_are_explicit(self):
        with patch.object(runner,'toolchain',return_value=(None,[],None,{},'mock')),patch.object(runner.subprocess,'run',side_effect=subprocess.TimeoutExpired('fixture',120,output='partial')):
            report=runner.run_checks(self.root,None,self.exe,self.scene,self.inputs,self.out)
        self.assertEqual(report['status'],'failed');self.assertIn('TimeoutExpired',report['error'])
        with self.assertRaises(ValueError):runner.run_checks(self.root,None,self.exe,self.scene,self.inputs,self.root/'outside')


if __name__=='__main__':unittest.main(verbosity=2)
