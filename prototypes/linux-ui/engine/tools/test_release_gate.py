#!/usr/bin/env python3
"""Failure-injection tests. Every mutation is confined to a fresh temporary folder."""
from pathlib import Path
import json
import os
import shutil
import subprocess
import tempfile
import unittest
import zipfile
from release_gate import (GateError, resource_integrity, dependency_closure,
                          verify_archive, files, inside, compare_maps, package_sources,
                          validate_mono_results, validate_lifetime_evidence, sha)


NODE = os.environ.get('RUFMAKER_RELEASE_NODE', 'node')


class ReleaseGateTests(unittest.TestCase):
    def test_required_mono_results_pass(self):
        validate_mono_results({'RufMaker.object-engine.mono': {'compile': 0},
                              'ObjectMotion.tests': {'compile': 0, 'run': 0},
                              'WalkthroughView.tests': {'compile': 0, 'run': 0}},
                             ['ObjectMotion.tests', 'WalkthroughView.tests'])

    def test_missing_walkthrough_execution_fails(self):
        with self.assertRaisesRegex(GateError, 'WalkthroughView.tests run'):
            validate_mono_results({'RufMaker.object-engine.mono': {'compile': 0},
                                  'WalkthroughView.tests': {'compile': 0}},
                                 ['WalkthroughView.tests'])

    def test_failed_or_malformed_mono_result_fails(self):
        for value in (1, None, False, '0'):
            with self.subTest(value=value), self.assertRaises(GateError):
                validate_mono_results({'RufMaker.object-engine.mono': {'compile': value}}, [])

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='ruf-release-guard-')
        self.root = Path(self.temp.name)
        self.addCleanup(self.temp.cleanup)

    def write(self, rel, text=''):
        file = self.root/rel; file.parent.mkdir(parents=True,exist_ok=True); file.write_text(text)
        return file

    def lifetime_evidence(self, last=2, stop='complete'):
        executable=self.write('check.exe','fresh executable')
        scene=self.write('project/rufmaker.json',json.dumps({'duration':2}))
        result={'RequestedEnd':2,'LastFrame':last,'Seconds':0.25,'Stop':stop,'Error':'','Rows':[],'Warnings':[]}
        output=self.root/'output'; self.write('output/result.json',json.dumps(result))
        report={'status':'passed' if stop=='complete' else 'partial','source':str(scene),'budget_seconds':15,
                'input_hash_root':str(scene.parent),'input_file_count':1,
                'input_files_verified_unchanged':True,'test_exe':str(executable),
                'test_exe_sha256':sha(executable),'result_sha256':sha(output/'result.json'),
                'last_frame':last,'requested_end':2,'seconds':0.25,'stop':stop,'error':'',
                'warnings':[],'exit_code':0}
        return report,output,executable,scene

    def test_lifetime_complete_and_partial_evidence_pass(self):
        for last,stop in [(2,'complete'),(1,'time-limit'),(-1,'cancelled')]:
            args=self.lifetime_evidence(last,stop)
            self.assertEqual(validate_lifetime_evidence(*args)['LastFrame'],last)

    def test_lifetime_cannot_promote_partial_or_shorten_horizon(self):
        for field,value in [('status','passed'),('requested_end',1),('last_frame',True),('stop','complete')]:
            with self.subTest(field=field):
                args=self.lifetime_evidence(1,'time-limit');args[0][field]=value
                with self.assertRaises(GateError):validate_lifetime_evidence(*args)
        with self.assertRaisesRegex(GateError,'False lifetime completion'):
            validate_lifetime_evidence(*self.lifetime_evidence(1,'complete'))

    def test_lifetime_stale_executable_or_result_fails(self):
        for field in ['test_exe_sha256','result_sha256']:
            with self.subTest(field=field):
                args=self.lifetime_evidence();args[0][field]='0'*64
                with self.assertRaises(GateError):validate_lifetime_evidence(*args)
        args=self.lifetime_evidence();args[2].write_text('changed binary')
        with self.assertRaises(GateError):validate_lifetime_evidence(*args)

    def test_lifetime_input_scope_and_process_result_required(self):
        mutations=[('input_files_verified_unchanged',False),('input_hash_root','different'),
                   ('source','different'),('input_file_count',True),('input_file_count',2),
                   ('exit_code',False),('exit_code',1),('error','failure'),('seconds',float('nan')),
                   ('budget_seconds',True),('budget_seconds',180)]
        for field,value in mutations:
            with self.subTest(field=field):
                args=self.lifetime_evidence();args[0][field]=value
                with self.assertRaises(GateError):validate_lifetime_evidence(*args)

    def test_lifetime_matching_invalid_result_still_fails(self):
        for field,value in [('Seconds',float('nan')),('LastFrame',True),('RequestedEnd',1),
                            ('Rows',None),('Warnings',None),('Error','failure'),('Stop','error')]:
            with self.subTest(field=field):
                args=self.lifetime_evidence();report,output,_,_=args
                result=json.loads((output/'result.json').read_text());result[field]=value
                (output/'result.json').write_text(json.dumps(result));report['result_sha256']=sha(output/'result.json')
                mapped={'Seconds':'seconds','LastFrame':'last_frame','RequestedEnd':'requested_end','Warnings':'warnings','Error':'error','Stop':'stop'}
                if field in mapped:report[mapped[field]]=value
                with self.assertRaises(GateError):validate_lifetime_evidence(*args)

    def projects(self):
        for folder in ('before','after'):
            self.write(folder+'/scripts/index.yyd','alpha\n\nretired\nomega\n')
            self.write(folder+'/scripts/tree.yyd','+group\n\t|alpha\n\t|retired\n\t|omega\n')
            for name in ('alpha','retired','omega'):
                self.write(folder+'/scripts/'+name+'.gml','return 0;')
        return self.root/'before', self.root/'after'

    def engine(self):
        self.write('engine/desktop/Build-Native.ps1',"$names=@('Main')")
        self.write('engine/desktop/Main.cs')
        self.write('engine/assets/icons/icon.ico')
        self.write('engine/assets/locales/en.json','{}')
        self.write('engine/tests/event-expression-runtime.mjs','export const value = 1;')
        self.write('engine/tests/fixtures/orbit-scope-probe.json','{}')
        self.write('engine/studio/check.mjs',"import {value} from '../tests/event-expression-runtime.mjs';\nconst file=new URL('../tests/fixtures/orbit-scope-probe.json',import.meta.url);")
        return self.root/'engine'

    def test_unchanged_slots_and_holes_pass(self):
        before,after = self.projects()
        self.assertEqual(resource_integrity(before,after)['preservedBlankSlots'],1)

    def test_source_package_preserves_optional_native_trees_and_license(self):
        engine = self.engine()
        for folder in ('tools','docs'):
            (engine/folder).mkdir()
        self.write('engine/templates/avoidance.gm82/game.gm82','gm82_version=5\n')
        self.write('engine/engine/scripts/native.gml','return 1;\n')
        self.write('engine/LICENSE','License text\n')
        self.write('engine/build/unrelated.log','Do not package this log')
        manifest = package_sources(engine)
        self.assertIn('templates/avoidance.gm82/game.gm82',manifest)
        self.assertIn('engine/scripts/native.gml',manifest)
        self.assertIn('LICENSE',manifest)
        self.assertNotIn('build/unrelated.log',manifest)

    def test_source_package_keeps_narrow_checkpoint_compatibility(self):
        engine = self.engine()
        for folder in ('tools','docs'):
            (engine/folder).mkdir()
        self.assertIn('desktop/Main.cs',package_sources(engine))

    def test_shifted_legacy_slot_is_rejected(self):
        before,after = self.projects()
        self.write('after/scripts/index.yyd','alpha\nretired\nomega\n')
        with self.assertRaisesRegex(GateError,'shrank|slot shifted'):
            resource_integrity(before,after)

    def test_shift_even_with_same_total_slot_count_is_rejected(self):
        before,after = self.projects()
        self.write('after/scripts/index.yyd','alpha\nretired\nomega\n\n')
        with self.assertRaisesRegex(GateError,'slot shifted'):
            resource_integrity(before,after)

    def test_approved_deletion_retains_hole(self):
        before,after = self.projects()
        self.write('after/scripts/index.yyd','alpha\n\n\nomega\n')
        self.write('after/scripts/tree.yyd','|alpha\n|omega\n')
        (after/'scripts/retired.gml').unlink()
        self.assertEqual(resource_integrity(before,after,['scripts/retired'])['approvedRemovedSlots'],1)
        with self.assertRaisesRegex(GateError,'slot shifted'):
            resource_integrity(before,after)

    def test_reusing_historical_hole_is_rejected(self):
        before,after = self.projects()
        self.write('after/scripts/index.yyd','alpha\nnew\nretired\nomega\n')
        with self.assertRaisesRegex(GateError,'slot shifted'):
            resource_integrity(before,after)

    def test_new_resource_appends(self):
        before,after = self.projects()
        with (after/'scripts/index.yyd').open('a') as f: f.write('new\n')
        with (after/'scripts/tree.yyd').open('a') as f: f.write('|new\n')
        self.write('after/scripts/new.gml')
        self.assertEqual(resource_integrity(before,after)['resources'],4)

    def test_missing_and_orphan_resource_files_rejected(self):
        before,after = self.projects()
        (after/'scripts/omega.gml').unlink()
        with self.assertRaisesRegex(GateError,'Missing indexed resource'):
            resource_integrity(before,after)
        self.write('after/scripts/omega.gml'); self.write('after/scripts/orphan.gml')
        with self.assertRaisesRegex(GateError,'Orphan resource'):
            resource_integrity(before,after)

    def test_tree_dangling_entry_rejected(self):
        before,after = self.projects()
        with (after/'scripts/tree.yyd').open('a') as f: f.write('|missing\n')
        with self.assertRaisesRegex(GateError,'Tree/index mismatch'):
            resource_integrity(before,after)

    def test_dependency_closure_catches_historical_missing_import(self):
        engine = self.engine()
        self.assertEqual(dependency_closure(engine)['literalLocalDependencies'],2)
        (engine/'tests/event-expression-runtime.mjs').unlink()
        with self.assertRaisesRegex(GateError,'Missing import or fixture.*event-expression-runtime'):
            dependency_closure(engine)

    def test_missing_fixture_and_native_source_rejected(self):
        engine = self.engine()
        fixture = engine/'tests/fixtures/orbit-scope-probe.json'; fixture.unlink()
        with self.assertRaisesRegex(GateError,'Missing import or fixture.*orbit-scope'):
            dependency_closure(engine)
        fixture.write_text('{}'); (engine/'desktop/Main.cs').unlink()
        with self.assertRaisesRegex(GateError,'Missing native source'):
            dependency_closure(engine)

    def test_directory_urls_are_not_file_dependencies(self):
        engine = self.engine()
        self.write('engine/studio/output-paths.mjs', "const root=new URL('..',import.meta.url); const output=new URL('../build/',import.meta.url);")
        self.assertEqual(dependency_closure(engine)['literalLocalDependencies'],2)

    def test_multiline_export_dependency_rejected(self):
        engine = self.engine()
        self.write('engine/studio/multiline.mjs', "export {\n value\n} from './missing.mjs';")
        with self.assertRaisesRegex(GateError,'Missing import or fixture.*missing.mjs'):
            dependency_closure(engine)

    def test_unbundled_external_dependency_rejected(self):
        engine = self.engine()
        self.write('engine/studio/external.mjs', "import x from 'unbundled-package';")
        with self.assertRaisesRegex(GateError,'Unsupported nonlocal dependency'):
            dependency_closure(engine)

    def test_missing_sprite_frame_rejected(self):
        before,after = self.projects()
        for prefix in ('before','after'):
            self.write(prefix+'/sprites/index.yyd','sprite\n')
            self.write(prefix+'/sprites/tree.yyd','|sprite\n')
            self.write(prefix+'/sprites/sprite/sprite.txt','frames=1\n')
        with self.assertRaisesRegex(GateError,'Missing sprite image'):
            resource_integrity(before,after)

    def archive(self, name, source, omit=None):
        archive = self.root/name
        with zipfile.ZipFile(archive,'w') as z:
            for rel in files(source):
                if rel != omit: z.write(source/rel,rel)
        return archive

    def test_archive_manifest_catches_omitted_import_and_fixture(self):
        source = self.engine(); expected = files(source)
        valid = self.archive('valid.zip',source)
        self.assertEqual(verify_archive(valid,expected,self.root/'restored')['status'],'pass')
        for i,missing in enumerate(['tests/event-expression-runtime.mjs','tests/fixtures/orbit-scope-probe.json']):
            archive = self.archive('broken%d.zip'%i,source,missing)
            with self.assertRaisesRegex(GateError,'Archive dependency closure: missing='):
                verify_archive(archive,expected,self.root/('broken%d'%i))
            self.assertFalse((self.root/('broken%d'%i)).exists())

    def test_archive_damaged_file_rejected(self):
        source = self.engine(); expected = files(source)
        (source/'desktop/Main.cs').write_text('broken')
        archive = self.archive('changed.zip',source)
        with self.assertRaisesRegex(GateError,'changed=.*desktop/Main.cs'):
            verify_archive(archive,expected,self.root/'restore')

    def test_archive_traversal_rejected_without_extraction(self):
        archive = self.root/'escape.zip'
        with zipfile.ZipFile(archive,'w') as z: z.writestr('../escape.txt','bad')
        with self.assertRaisesRegex(GateError,'Unsafe path'):
            verify_archive(archive,{},self.root/'restore')
        self.assertFalse((self.root/'escape.txt').exists())

    def test_absolute_project_entry_rejected(self):
        with self.assertRaises(GateError): inside(self.root,'/tmp/not-allowed')
        with self.assertRaises(GateError): inside(self.root,'../not-allowed')

    @unittest.skipUnless(shutil.which(NODE),'Existing Node executable unavailable')
    def test_sync_guard_detects_second_sync_mutation(self):
        engine = self.root/'sync-engine'; project = self.root/'sync-project'
        self.write('sync-project/test.rufmaker','{}')
        self.write('sync-engine/studio/motion.mjs','export const normalizeState = x => x;')
        self.write('sync-engine/studio/project.mjs',"import fs from 'node:fs'; import path from 'node:path'; let calls=0; export const readState=()=>({}); export const validateState=()=>{}; export function syncToGame(){ if(++calls===2) fs.writeFileSync(path.join(path.dirname(process.env.RUFMAKER_PROJECT),'unexpected.txt'),'bad'); }")
        command = [NODE,str(Path(__file__).with_name('release_sync.mjs')),str(engine),str(project),'test.rufmaker']
        result = subprocess.run(command,text=True,capture_output=True)
        self.assertNotEqual(result.returncode,0)
        self.assertIn('changed on sync 2',result.stderr)
        self.assertNotIn('ERR_MODULE_NOT_FOUND',result.stderr)


    def test_trailing_blank_slots_cannot_disappear(self):
        before, after = self.projects()
        for folder in ('before', 'after'):
            with (self.root/folder/'scripts/index.yyd').open('a') as f: f.write('\n\n')
        self.assertEqual(resource_integrity(before, after)['preservedBlankSlots'], 3)
        (after/'scripts/index.yyd').write_text('alpha\n\nretired\nomega\n\n')
        with self.assertRaisesRegex(GateError, 'shrank'):
            resource_integrity(before, after)

    def test_unused_deletion_approval_rejected(self):
        before, after = self.projects()
        with self.assertRaisesRegex(GateError, 'Unused removal approvals'):
            resource_integrity(before, after, ['scripts/missing'])

    def test_case_colliding_resource_names_rejected(self):
        before, after = self.projects()
        with (after/'scripts/index.yyd').open('a') as f: f.write('ALPHA\n')
        with self.assertRaisesRegex(GateError, 'Case-colliding resource'):
            resource_integrity(before, after)

    def test_object_reference_to_unindexed_sprite_rejected(self):
        before, after = self.projects()
        for prefix in ('before', 'after'):
            self.write(prefix+'/objects/index.yyd', 'player\n')
            self.write(prefix+'/objects/tree.yyd', '|player\n')
            self.write(prefix+'/objects/player.txt', 'sprite=missing\n')
            self.write(prefix+'/objects/player.gml')
        with self.assertRaisesRegex(GateError, 'Missing object dependency'):
            resource_integrity(before, after)

    def test_archive_duplicate_and_case_collision_rejected(self):
        import warnings
        for i, names in enumerate([['same.txt', 'same.txt'], ['same.txt', 'SAME.txt']]):
            archive = self.root/f'collision{i}.zip'
            with warnings.catch_warnings():
                warnings.simplefilter('ignore', UserWarning)
                with zipfile.ZipFile(archive, 'w') as z:
                    for name in names: z.writestr(name, 'bad')
            with self.assertRaisesRegex(GateError, 'Duplicate ZIP|Case-colliding ZIP'):
                verify_archive(archive, {}, self.root/f'restore{i}')
            self.assertFalse((self.root/f'restore{i}').exists())

    def test_archive_symlink_rejected_before_extraction(self):
        archive = self.root/'symlink.zip'
        entry = zipfile.ZipInfo('link'); entry.create_system = 3
        entry.external_attr = (0o120777 << 16)
        with zipfile.ZipFile(archive, 'w') as z: z.writestr(entry, '../target')
        with self.assertRaisesRegex(GateError, 'symlink rejected'):
            verify_archive(archive, {}, self.root/'restore')
        self.assertFalse((self.root/'restore').exists())

    @unittest.skipUnless(shutil.which(NODE), 'Existing Node executable unavailable')
    def test_sync_rejects_escaping_entry_before_import(self):
        engine = self.root/'sync-engine'; project = self.root/'sync-project'
        engine.mkdir(); project.mkdir()
        target = self.write('outside.rufmaker', '{}')
        (project/'escape.rufmaker').symlink_to(target)
        result = subprocess.run([NODE, str(Path(__file__).with_name('release_sync.mjs')),
                                 str(engine), str(project), 'escape.rufmaker'], text=True, capture_output=True)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn('Entry must stay inside isolated project', result.stderr)
        self.assertNotIn('ERR_MODULE_NOT_FOUND', result.stderr)

    @unittest.skipUnless(shutil.which(NODE), 'Existing Node executable unavailable')
    def test_sync_rejects_first_and_third_mutation(self):
        for pass_number in (1, 3):
            prefix = f'pass{pass_number}'
            self.write(prefix+'/project/test.rufmaker', '{}')
            self.write(prefix+'/engine/studio/motion.mjs', 'export const normalizeState=x=>x;')
            self.write(prefix+'/engine/studio/project.mjs',
                "import fs from 'node:fs'; let calls=0; export const readState=()=>({}); "
                "export const validateState=()=>{}; export function syncToGame(){if(++calls==="
                +str(pass_number)+")fs.writeFileSync(process.env.RUFMAKER_PROJECT,'changed');}")
            result = subprocess.run([NODE, str(Path(__file__).with_name('release_sync.mjs')),
                str(self.root/prefix/'engine'), str(self.root/prefix/'project'), 'test.rufmaker'],
                text=True, capture_output=True)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn(f'changed on sync {pass_number}', result.stderr)


if __name__ == '__main__':
    unittest.main(verbosity=2)
