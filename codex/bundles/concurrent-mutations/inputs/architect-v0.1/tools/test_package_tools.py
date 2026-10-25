#!/usr/bin/env python3
"""Self-tests for package utilities only, using disposable temporary Git fixtures."""
from __future__ import annotations
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
from refresh_evidence import refresh
from rebuild_manifest import rebuild
from validate_package import validate

ROOT = Path(__file__).resolve().parents[1]


def run_git(repo: Path, *args: str) -> str:
    result = subprocess.run(['git', '-C', str(repo), *args], check=True,
                            stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=20)
    return result.stdout.decode().strip()


class EvidenceRefreshTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.base = Path(self.temp.name)
        self.repo = self.base / 'repo'
        self.repo.mkdir()
        run_git(self.repo, 'init')
        run_git(self.repo, 'config', 'user.email', 'fixture@example.invalid')
        run_git(self.repo, 'config', 'user.name', 'Disposable fixture')
        run_git(self.repo, 'config', 'commit.gpgsign', 'false')
        (self.repo / 'sample.cs').write_text('original\n', encoding='utf-8')
        run_git(self.repo, 'add', 'sample.cs')
        run_git(self.repo, 'commit', '-m', 'fixture')
        self.commit = run_git(self.repo, 'rev-parse', 'HEAD')
        self.blob = run_git(self.repo, 'rev-parse', 'HEAD:sample.cs')
        self.ledger = self.base / 'sources.json'
        self.ledger.write_text(json.dumps([dict(id='SRC-FIXTURE',kind='repository',
                                               ref=self.commit,path='sample.cs',git_blob_sha=self.blob)]),encoding='utf-8')

    def tearDown(self) -> None:
        self.temp.cleanup()

    def test_same_blob_no_logical_repo_changes(self) -> None:
        before = run_git(self.repo, 'status', '--porcelain')
        result = refresh(self.repo, 'HEAD', self.ledger, self.base / 'result.json')
        self.assertEqual(result['sources'][0]['status'], 'same-blob-review-callers')
        self.assertFalse(result['sources'][0]['semantic_review_completed'])
        self.assertEqual(before, run_git(self.repo, 'status', '--porcelain'))
        self.assertEqual(self.commit, run_git(self.repo, 'rev-parse', 'HEAD'))

    def test_changed_blob_requires_review(self) -> None:
        (self.repo / 'sample.cs').write_text('changed\n', encoding='utf-8')
        run_git(self.repo, 'add', '.')
        run_git(self.repo, 'commit', '-m', 'change')
        result = refresh(self.repo, 'HEAD', self.ledger, self.base / 'result.json')
        self.assertEqual(result['sources'][0]['status'], 'changed-reread-required')

    def test_relocation_is_candidate_not_approval(self) -> None:
        run_git(self.repo, 'mv', 'sample.cs', 'moved.cs')
        run_git(self.repo, 'commit', '-m', 'move')
        result = refresh(self.repo, 'HEAD', self.ledger, self.base / 'result.json')
        self.assertEqual(result['sources'][0]['status'], 'missing-relocation-candidate')
        self.assertEqual(result['sources'][0]['possible_relocations'], ['moved.cs'])
        self.assertFalse(result['sources'][0]['semantic_review_completed'])

    def test_dirty_tree_is_explicit_not_silently_inspected(self) -> None:
        (self.repo / 'sample.cs').write_text('uncommitted\n', encoding='utf-8')
        result = refresh(self.repo, 'HEAD', self.ledger, self.base / 'result.json')
        self.assertTrue(result['working_tree_dirty'])
        self.assertFalse(result['uncommitted_content_inspected'])
        self.assertEqual((self.repo / 'sample.cs').read_text(), 'uncommitted\n')

    def test_output_inside_repo_rejected(self) -> None:
        with self.assertRaises(ValueError):
            refresh(self.repo, 'HEAD', self.ledger, self.repo / 'result.json')
        self.assertFalse((self.repo / 'result.json').exists())

    def test_existing_output_not_overwritten(self) -> None:
        output = self.base / 'result.json'
        output.write_text('preserve', encoding='utf-8')
        with self.assertRaises(ValueError):
            refresh(self.repo, 'HEAD', self.ledger, output)
        self.assertEqual(output.read_text(), 'preserve')

    def test_option_like_invalid_ref_rejected(self) -> None:
        with self.assertRaises(ValueError):
            refresh(self.repo, '--help', self.ledger, self.base / 'result.json')
        self.assertEqual(self.commit, run_git(self.repo, 'rev-parse', 'HEAD'))


class PackageValidationTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.copy = Path(self.temp.name) / 'package'
        shutil.copytree(ROOT, self.copy, ignore=shutil.ignore_patterns('__pycache__', '*.pyc'))
        rebuild(self.copy)

    def tearDown(self) -> None:
        self.temp.cleanup()

    def test_valid_and_tampered_manifest(self) -> None:
        self.assertEqual(validate(self.copy)['status'], 'pass')
        with (self.copy / 'README.md').open('a', encoding='utf-8') as stream:
            stream.write('\nTamper probe.\n')
        report = validate(self.copy)
        self.assertEqual(report['status'], 'fail')
        self.assertTrue(any('Manifest content mismatch' in error for error in report['errors']))

    def test_unknown_reference_detected(self) -> None:
        path = self.copy / 'registers' / 'risks.json'
        rows = json.loads(path.read_text())
        rows[0]['tests'].append('T-NOT-REAL')
        path.write_text(json.dumps(rows), encoding='utf-8')
        report = validate(self.copy, check_manifest=False)
        self.assertTrue(any('Unknown test_scenarios reference' in error for error in report['errors']))

    def test_phase_cycle_detected(self) -> None:
        path = self.copy / 'registers' / 'phases.json'
        rows = json.loads(path.read_text())
        rows[0]['depends_on'] = ['P08']
        path.write_text(json.dumps(rows), encoding='utf-8')
        report = validate(self.copy, check_manifest=False)
        self.assertTrue(any('Phase dependency cycle' in error for error in report['errors']))

    def test_broken_link_detected(self) -> None:
        with (self.copy / 'README.md').open('a', encoding='utf-8') as stream:
            stream.write('\n[Missing](not-present.md)\n')
        report = validate(self.copy, check_manifest=False)
        self.assertTrue(any('Broken local link' in error for error in report['errors']))


if __name__ == '__main__':
    unittest.main(verbosity=2)
