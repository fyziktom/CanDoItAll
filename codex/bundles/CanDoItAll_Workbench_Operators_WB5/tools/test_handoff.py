#!/usr/bin/env python3
"""Adversarial checks of package utilities, not product tests."""
from __future__ import annotations

import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import sys
import tempfile
import unittest

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('handoff_validator', ROOT/'tools/validate_handoff.py')
assert spec is not None and spec.loader is not None
validator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(validator)


def seal(root: Path) -> None:
    lines = []
    for path in sorted(root.rglob('*')):
        if path.is_file() and not path.is_symlink() and path.name != 'MANIFEST.sha256':
            lines.append(f'{hashlib.sha256(path.read_bytes()).hexdigest()}  {path.relative_to(root).as_posix()}')
        elif path.is_file() and path.relative_to(root).as_posix() == 'shared/MANIFEST.sha256':
            lines.append(f'{hashlib.sha256(path.read_bytes()).hexdigest()}  shared/MANIFEST.sha256')
    (root/'MANIFEST.sha256').write_text('\n'.join(lines)+'\n', encoding='utf-8')


class PackageTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='wb5-package-test-')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)/'package'
        shutil.copytree(ROOT, self.root)

    def check(self):
        return validator.validate(self.root)[0]

    def test_pristine_package(self):
        self.assertEqual([], self.check())

    def test_tampered_text(self):
        (self.root/'README.md').write_text('tampered', encoding='utf-8')
        self.assertTrue(any('Hash mismatch' in error for error in self.check()))

    def test_unsealed_file(self):
        (self.root/'extra.md').write_text('not sealed', encoding='utf-8')
        self.assertTrue(any('inventory' in error for error in self.check()))

    def test_invalid_json(self):
        (self.root/'sources.json').write_text('{', encoding='utf-8')
        self.assertTrue(any('Invalid text/JSON' in error for error in self.check()))

    def test_broken_link(self):
        with (self.root/'README.md').open('a', encoding='utf-8') as handle:
            handle.write('\n[missing](not-present.md)\n')
        seal(self.root)
        self.assertTrue(any('broken/escaping' in error for error in self.check()))

    def test_unknown_source(self):
        with (self.root/'README.md').open('a', encoding='utf-8') as handle:
            handle.write('\nUnreviewed assertion [S99].\n')
        seal(self.root)
        self.assertTrue(any('unknown source' in error for error in self.check()))

    def test_shared_cannot_be_resealed_as_unchanged(self):
        with (self.root/'shared/README.md').open('a', encoding='utf-8') as handle:
            handle.write('\nchanged\n')
        seal(self.root)
        self.assertTrue(any('Shared v3' in error for error in self.check()))

    def test_manifest_traversal(self):
        with (self.root/'MANIFEST.sha256').open('a', encoding='utf-8') as handle:
            handle.write('0'*64+'  ../outside.md\n')
        self.assertTrue(any('Unsafe/duplicate' in error for error in self.check()))

    def test_manifest_duplicate(self):
        path = self.root/'MANIFEST.sha256'
        text = path.read_text(encoding='utf-8')
        path.write_text(text + text.splitlines()[0]+'\n', encoding='utf-8')
        self.assertTrue(any('Unsafe/duplicate' in error for error in self.check()))

    def test_review_is_not_a_checkout_pin(self):
        path = self.root/'bundle.json'
        value = json.loads(path.read_text(encoding='utf-8'))
        value['review_commit_is_execution_pin'] = True
        path.write_text(json.dumps(value), encoding='utf-8')
        seal(self.root)
        self.assertTrue(any('provenance' in error for error in self.check()))

    def test_invalid_blob(self):
        path = self.root/'sources.json'
        value = json.loads(path.read_text(encoding='utf-8'))
        next(item for item in value['sources'] if item['kind'] == 'repository-file')['observed_blob_sha'] = 'invalid'
        path.write_text(json.dumps(value), encoding='utf-8')
        seal(self.root)
        self.assertTrue(any('invalid observed blob' in error for error in self.check()))


class EvidenceTests(unittest.TestCase):
    def setUp(self):
        self.value = json.loads((ROOT/'templates/evidence.json').read_text(encoding='utf-8'))
        self.ids = [f'G{i:02}' for i in range(32)]

    def check(self, complete=False):
        return validator.evidence_errors(self.value, self.ids, complete)

    def test_template_is_valid_but_not_complete(self):
        self.assertEqual([], self.check())
        self.assertTrue(self.check(True))

    def test_foreign_bundle(self):
        self.value['bundle_id'] = 'different'
        self.assertTrue(self.check())

    def test_duplicate_group(self):
        self.value['groups'][1]['id'] = 'G00'
        self.assertTrue(self.check())

    def test_unknown_status(self):
        self.value['groups'][0]['status'] = 'GREEN_ENOUGH'
        self.assertTrue(self.check())

    def test_unbacked_success(self):
        self.value['groups'][0]['status'] = 'PASS'
        self.assertTrue(any('no recorded attempt' in error for error in self.check()))

    def test_unknown_attempt(self):
        self.value['groups'][0]['attempt_ids'] = ['nonexistent']
        self.assertTrue(any('unknown attempt' in error for error in self.check()))

    def test_unexplained_qualification(self):
        self.value['groups'][0]['status'] = 'QUALIFIED'
        self.assertTrue(any('qualification' in error for error in self.check()))

    def test_working_evidence_requires_actual_pair(self):
        self.value['evidence_is_template'] = False
        self.assertTrue(any('actual main SHA' in error for error in self.check()))

    def test_synthetic_complete_shape_and_blocker_detection(self):
        # This is a validator unit-test fixture, not application evidence.
        self.value['evidence_is_template'] = False
        self.value['runtime_tests_executed'] = True
        self.value['source_pair'] = dict(main='1'*40, components='2'*40, filetools='3'*40)
        self.value['attempts'] = [{'id':'synthetic-unit-test-only'}]
        for group in self.value['groups']:
            group['status'] = 'PASS'
            group['attempt_ids'] = ['synthetic-unit-test-only']
        self.assertEqual([], self.check(True))
        self.value['groups'][0]['remaining_product_blocker'] = 'unresolved test fixture blocker'
        self.assertTrue(self.check(True))


if __name__ == '__main__':
    unittest.main(verbosity=2)
