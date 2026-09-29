#!/usr/bin/env python3
"""Test only the handoff validator, using disposable package copies."""
from __future__ import annotations

import importlib.util
import json
from pathlib import Path
import shutil
import sys
import tempfile
import unittest

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('handoff_validator', ROOT / 'tools/validate_package.py')
if spec is None or spec.loader is None:
    raise RuntimeError('Cannot load handoff validator.')
validator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(validator)

class PackageTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='workspace-handoff-test-')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / 'bundle'
        shutil.copytree(ROOT, self.root, ignore=shutil.ignore_patterns('__pycache__'))

    def edit_json(self, name, change):
        path = self.root / name
        value = json.loads(path.read_text(encoding='utf-8'))
        change(value)
        path.write_text(json.dumps(value), encoding='utf-8')

    def test_valid_package(self):
        self.assertGreater(validator.validate(self.root)['sources'], 25)

    def test_changed_content_is_detected(self):
        with (self.root / 'README.md').open('a', encoding='utf-8') as handle:
            handle.write('\nChanged after sealing.\n')
        with self.assertRaisesRegex(ValueError, 'Hash mismatch'):
            validator.validate(self.root)

    def test_missing_link_is_detected(self):
        with (self.root / 'README.md').open('a', encoding='utf-8') as handle:
            handle.write('\n[Missing](missing-file.md)\n')
        with self.assertRaisesRegex(ValueError, 'Broken local link'):
            validator.validate(self.root)

    def test_unknown_source_is_detected(self):
        with (self.root / 'README.md').open('a', encoding='utf-8') as handle:
            handle.write('\nEvidence ME99.\n')
        with self.assertRaisesRegex(ValueError, 'Unknown source'):
            validator.validate(self.root)

    def test_duplicate_source_is_detected(self):
        self.edit_json('sources.json', lambda value: value['sources'].append(value['sources'][0].copy()))
        with self.assertRaisesRegex(ValueError, 'Duplicate source ID'):
            validator.validate(self.root)

    def test_execution_pin_is_rejected(self):
        self.edit_json('bundle.json', lambda value: value.update(review_commit_is_execution_pin=True))
        with self.assertRaisesRegex(ValueError, 'execution pin'):
            validator.validate(self.root)

    def test_added_file_is_not_sealed(self):
        (self.root / 'extra.md').write_text('Unsealed.\n', encoding='utf-8')
        with self.assertRaisesRegex(ValueError, 'Manifest inventory'):
            validator.validate(self.root)

    def test_unsafe_manifest_is_rejected(self):
        with (self.root / 'MANIFEST.sha256').open('a', encoding='utf-8') as handle:
            handle.write('0' * 64 + '  ../outside.md\n')
        with self.assertRaisesRegex(ValueError, 'Unsafe'):
            validator.validate(self.root)

    def test_wrong_source_commit_is_rejected(self):
        def change(value):
            item = next(item for item in value['sources'] if item['kind'] == 'repository-file')
            item['review_commit'] = '1' * 40
        self.edit_json('sources.json', change)
        with self.assertRaisesRegex(ValueError, 'Unpinned repository source'):
            validator.validate(self.root)

    def test_unexpected_binary_is_rejected(self):
        (self.root / 'unexpected.bin').write_bytes(b'\x00\xff')
        with self.assertRaisesRegex(ValueError, 'Unexpected file type'):
            validator.validate(self.root)

    def test_symlink_is_rejected(self):
        link = self.root / 'linked.md'
        try:
            link.symlink_to(self.root / 'README.md')
        except (OSError, NotImplementedError):
            self.skipTest('Symlink creation is unavailable in this environment.')
        with self.assertRaisesRegex(ValueError, 'Symlinks'):
            validator.validate(self.root)

if __name__ == '__main__':
    unittest.main(verbosity=2)
