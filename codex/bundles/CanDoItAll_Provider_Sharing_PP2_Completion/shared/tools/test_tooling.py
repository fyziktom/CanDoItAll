#!/usr/bin/env python3
"""Tests for package utilities. Git writes occur only in newly created disposable test repositories."""
from __future__ import annotations

import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]


def load_module(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load test target {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


drift = load_module("bundle_drift", ROOT / "tools/check_review_drift.py")
validator = load_module("bundle_validator", ROOT / "tools/validate_bundle.py")


@unittest.skipUnless(shutil.which("git"), "Git is required for disposable-repository tests")
class DriftTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="ui-bundle-test-")
        self.addCleanup(self.temp.cleanup)
        self.repo = Path(self.temp.name) / "repository"
        self.repo.mkdir()
        self.hooks = Path(self.temp.name) / "empty-hooks"
        self.hooks.mkdir()
        self.command("init", "--quiet")
        self.command("config", "user.name", "Bundle Utility Test")
        self.command("config", "user.email", "bundle-test@example.invalid")
        (self.repo / "example.cs").write_text("// initial test fixture\n", encoding="utf-8")
        self.commit()
        self.head = self.command("rev-parse", "HEAD").strip()
        self.blob = self.command("rev-parse", "HEAD:example.cs").strip()
        self.register = {
            "review_commit": self.head,
            "sources": [{"id": "S01", "kind": "repository-file", "path": "example.cs", "observed_blob_sha": self.blob}],
        }

    def command(self, *args):
        # This override applies only to the synthetic test repository, never a user's checkout.
        result = subprocess.run(
            ["git", "-c", "commit.gpgsign=false", "-c", f"core.hooksPath={self.hooks}", "-C", str(self.repo), *args],
            text=True, capture_output=True, check=True, timeout=15,
        )
        return result.stdout

    def commit(self):
        self.command("add", "--all")
        self.command("commit", "--quiet", "-m", "Disposable fixture checkpoint")

    def test_matching_review_does_not_require_checkout(self):
        result = drift.report(self.repo, self.register)
        self.assertEqual("same-blob", result["reviewed_files"][0]["state"])
        self.assertEqual(0, result["files_requiring_attention"])
        self.assertFalse(result["execution_pin_required"])
        self.assertFalse(result["dirty"])

    def test_uncommitted_change_is_not_reported_as_clean(self):
        (self.repo / "example.cs").write_text("// local edit\n", encoding="utf-8")
        result = drift.report(self.repo, self.register)
        self.assertEqual("same-blob", result["reviewed_files"][0]["state"])
        self.assertTrue(result["reviewed_files"][0]["index_or_worktree_dirty"])
        self.assertEqual(1, result["files_requiring_attention"])

    def test_changed_commit_is_informational_drift(self):
        (self.repo / "example.cs").write_text("// changed implementation\n", encoding="utf-8")
        self.commit()
        result = drift.report(self.repo, self.register)
        self.assertEqual("changed-blob", result["reviewed_files"][0]["state"])
        self.assertFalse(result["head_equals_review"])
        self.assertFalse(result["dirty"])

    def test_missing_reviewed_file_is_visible(self):
        (self.repo / "example.cs").unlink()
        self.commit()
        result = drift.report(self.repo, self.register)
        self.assertEqual("missing-at-head", result["reviewed_files"][0]["state"])

    def test_shallow_or_missing_history_does_not_fetch(self):
        self.register["review_commit"] = "1" * 40
        self.register["sources"][0]["observed_blob_sha"] = None
        result = drift.report(self.repo, self.register)
        self.assertEqual("baseline-unavailable", result["reviewed_files"][0]["state"])
        self.assertFalse(result["execution_pin_required"])

    def test_unsafe_register_path_is_rejected(self):
        self.register["sources"][0]["path"] = "../outside.cs"
        with self.assertRaises(ValueError):
            drift.report(self.repo, self.register)

    def test_rename_status_preserves_both_paths(self):
        entries = drift.status_entries("R  new name.cs\0old name.cs\0")
        self.assertEqual("new name.cs", entries[0]["path"])
        self.assertEqual("old name.cs", entries[0]["original_path"])

    def test_non_repository_is_an_explicit_error(self):
        with self.assertRaises(RuntimeError):
            drift.report(self.hooks, self.register)


class ValidatorTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="ui-bundle-integrity-")
        self.addCleanup(self.temp.cleanup)
        self.copy = Path(self.temp.name) / "bundle"
        shutil.copytree(ROOT, self.copy, ignore=shutil.ignore_patterns("__pycache__"))

    def test_sealed_bundle_is_valid(self):
        self.assertEqual([], validator.validate(self.copy))

    def test_changed_content_breaks_hash(self):
        with (self.copy / "README.md").open("a", encoding="utf-8") as handle:
            handle.write("\nChanged after sealing.\n")
        self.assertTrue(any("Hash mismatch: README.md" in e for e in validator.validate(self.copy)))

    def test_missing_local_link_is_found(self):
        with (self.copy / "README.md").open("a", encoding="utf-8") as handle:
            handle.write("\n[Missing](not-in-this-bundle.md)\n")
        self.assertTrue(any("Broken local link" in e for e in validator.validate(self.copy)))

    def test_unknown_source_id_is_found(self):
        with (self.copy / "README.md").open("a", encoding="utf-8") as handle:
            handle.write("\nUnregistered evidence [S99].\n")
        self.assertTrue(any("Unknown source S99" in e for e in validator.validate(self.copy)))

    def test_unsealed_added_file_is_found(self):
        (self.copy / "extra.md").write_text("Unexpected addition.\n", encoding="utf-8")
        self.assertTrue(any("Unsealed file: extra.md" in e for e in validator.validate(self.copy)))

    def test_binary_extension_is_rejected(self):
        # Only a dummy byte sequence, not a real font asset.
        (self.copy / "not-a-font.woff2").write_bytes(b"\xff\x00")
        self.assertTrue(any("Unexpected non-text artifact" in e for e in validator.validate(self.copy)))


if __name__ == "__main__":
    unittest.main(verbosity=2)
