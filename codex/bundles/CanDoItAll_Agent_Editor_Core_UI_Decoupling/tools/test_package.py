"""Package integrity negative controls; all mutations use temporary bundle copies."""
from pathlib import Path
import hashlib
import json
import shutil
import tempfile
import unittest
import validate_package as validator

ROOT = Path(__file__).resolve().parents[1]

class PackageTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='agent-editor-handoff-test-')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / 'bundle'
        shutil.copytree(ROOT, self.root)

    def edit_json(self, name, change):
        path = self.root / name
        value = json.loads(path.read_text(encoding='utf-8'))
        change(value)
        path.write_text(json.dumps(value), encoding='utf-8')

    def seal(self):
        entries = []
        for p in sorted(self.root.rglob('*')):
            if p.is_file() and p.name != 'MANIFEST.sha256':
                entries.append((p.relative_to(self.root).as_posix(), p))
            elif p.is_file() and p.parent != self.root:
                entries.append((p.relative_to(self.root).as_posix(), p))
        (self.root/'MANIFEST.sha256').write_text(''.join(f'{hashlib.sha256(p.read_bytes()).hexdigest()}  {n}\n' for n,p in entries), encoding='utf-8')

    def errors(self):
        return '\n'.join(validator.validate(self.root)[0])

    def test_valid_package(self):
        self.assertEqual('', self.errors())

    def test_changed_file_is_rejected(self):
        with (self.root/'README.md').open('a', encoding='utf-8') as stream:
            stream.write('\nChanged\n')
        self.assertIn('Hash mismatch', self.errors())

    def test_missing_document_is_rejected(self):
        (self.root/'prompt.md').unlink()
        self.assertIn('Missing declared input', self.errors())

    def test_broken_link_is_rejected(self):
        with (self.root/'README.md').open('a', encoding='utf-8') as stream:
            stream.write('\n[Missing](absent.md)\n')
        self.seal()
        self.assertIn('Broken/escaping link', self.errors())

    def test_small_viewport_is_rejected(self):
        self.edit_json('bundle.json', lambda x: x.update(primary_viewport={'width':390,'height':844}))
        self.seal()
        self.assertIn('Non-desktop viewport', self.errors())

    def test_live_budget_increase_is_rejected(self):
        self.edit_json('bundle.json', lambda x: x.update(paid_live_requests_authorized=40))
        self.seal()
        self.assertIn('no new live requests', self.errors())

    def test_fabricated_template_success_is_rejected(self):
        self.edit_json('templates/evidence.json', lambda x: x['groups'][0].update(status='PASS'))
        self.seal()
        self.assertIn('must not contain product successes', self.errors())

    def test_unknown_source_is_rejected(self):
        with (self.root/'README.md').open('a', encoding='utf-8') as stream:
            stream.write('\nUnregistered source R99.\n')
        self.seal()
        self.assertIn('Unknown source R99', self.errors())

    def test_shared_changed_even_when_resealed_is_rejected(self):
        with (self.root/'shared/README.md').open('a', encoding='utf-8') as stream:
            stream.write('\nChanged foundation\n')
        self.seal()
        self.assertIn('Shared source changed', self.errors())

    def test_unsafe_manifest_entry_is_rejected(self):
        with (self.root/'MANIFEST.sha256').open('a', encoding='utf-8') as stream:
            stream.write('0'*64 + '  ../outside.md\n')
        self.assertIn('Unsafe/duplicate manifest entry', self.errors())


    def test_preclaimed_a1_completion_is_rejected(self):
        self.edit_json('templates/evidence.json', lambda x: x.update(agent_editor_a1_complete=True))
        self.seal()
        self.assertIn('must not preclaim completion', self.errors())

    def test_review_pin_is_rejected(self):
        self.edit_json('bundle.json', lambda x: x.update(review_commit_is_execution_pin=True))
        self.seal()
        self.assertIn('must not be an execution pin', self.errors())

    def test_remote_writes_are_rejected(self):
        self.edit_json('bundle.json', lambda x: x.update(remote_writes_authorized=True))
        self.seal()
        self.assertIn('Invalid scoped flag', self.errors())

    def test_source_identity_mismatch_is_rejected(self):
        self.edit_json('sources.json', lambda x: x.update(application_ref='a'*40))
        self.seal()
        self.assertIn('Application review identity mismatch', self.errors())

if __name__ == '__main__':
    unittest.main()
