"""Tests for local package integrity, without accessing a repository or shared files."""
from __future__ import annotations

import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from verify_package import canonical_digest, verify_package


class PackageTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / "task"
        self.root.mkdir()
        self.metadata = {
            "delivery": "single_archive_task_only",
            "shared_policy": {
                "mode": "reuse_existing_repository_baseline",
                "version_pin": False,
                "manifest_pin": False,
                "separate_download_required": False,
            },
        }
        (self.root / "README.md").write_text("Sample task\n", encoding="utf-8")
        self.write_metadata()
        self.seal()

    def write_metadata(self) -> None:
        (self.root / "bundle.json").write_text(json.dumps(self.metadata), encoding="utf-8")

    def seal(self) -> None:
        self.manifest = {
            "algorithm": "sha256",
            "files": {p.relative_to(self.root).as_posix(): canonical_digest(p)
                      for p in self.root.rglob("*") if p.is_file() and p.name != "MANIFEST.json"},
        }
        self.write_manifest()

    def write_manifest(self) -> None:
        (self.root / "MANIFEST.json").write_text(json.dumps(self.manifest), encoding="utf-8")

    def test_valid_without_shared(self) -> None:
        self.assertEqual([], verify_package(self.root))

    def test_read_only(self) -> None:
        before = {p.name: p.read_bytes() for p in self.root.iterdir()}
        verify_package(self.root)
        self.assertEqual(before, {p.name: p.read_bytes() for p in self.root.iterdir()})

    def test_crlf_is_equivalent(self) -> None:
        (self.root / "README.md").write_bytes(b"Sample task\r\n")
        self.assertEqual([], verify_package(self.root))

    def test_lone_cr_is_not_equivalent(self) -> None:
        (self.root / "README.md").write_bytes(b"Sample task\r")
        self.assertIn("Digest mismatch: README.md", verify_package(self.root))

    def test_tampered(self) -> None:
        (self.root / "README.md").write_text("Changed\n", encoding="utf-8")
        self.assertIn("Digest mismatch: README.md", verify_package(self.root))

    def test_missing(self) -> None:
        (self.root / "README.md").unlink()
        self.assertIn("Missing file: README.md", verify_package(self.root))

    def test_extra_file(self) -> None:
        (self.root / "extra.md").write_text("Unexpected", encoding="utf-8")
        self.assertIn("Unlisted file: extra.md", verify_package(self.root))

    def test_bad_paths(self) -> None:
        for path in ("../escape", "/absolute", "C:/drive", "a\\b", "./x", "a//b", "a/../b"):
            with self.subTest(path=path):
                self.manifest["files"] = {path: "0" * 64}
                self.write_manifest()
                with self.assertRaises(ValueError):
                    verify_package(self.root)

    def test_invalid_digest(self) -> None:
        self.manifest["files"]["README.md"] = "invalid"
        self.write_manifest()
        with self.assertRaises(ValueError):
            verify_package(self.root)

    def test_manifest_cannot_hash_itself(self) -> None:
        self.manifest["files"]["MANIFEST.json"] = "0" * 64
        self.write_manifest()
        with self.assertRaises(ValueError):
            verify_package(self.root)

    def test_symlink_rejected(self) -> None:
        try:
            (self.root / "link.md").symlink_to(self.root / "README.md")
        except (OSError, NotImplementedError):
            self.skipTest("Symlinks unavailable in this environment")
        self.assertTrue(any("Symbolic link" in value for value in verify_package(self.root)))

    def test_shared_pin_rejected(self) -> None:
        self.metadata["shared_requirement"] = {"id": "legacy"}
        self.write_metadata()
        self.seal()
        self.assertTrue(any("pin" in value for value in verify_package(self.root)))

    def test_second_download_rejected(self) -> None:
        self.metadata["shared_policy"]["separate_download_required"] = True
        self.write_metadata()
        self.seal()
        self.assertTrue(any("separate_download_required" in value for value in verify_package(self.root)))

    def test_shared_copy_rejected(self) -> None:
        (self.root / "UI_Decoupling_Shared_Bundle").mkdir()
        self.assertTrue(any("shared snapshot" in value for value in verify_package(self.root)))

    def test_no_shared_access(self) -> None:
        shared = self.root.parent / "UI_Decoupling_Shared_Bundle"
        shared.mkdir()
        (shared / "MANIFEST.json").write_text("Deliberately invalid", encoding="utf-8")
        before = (shared / "MANIFEST.json").read_bytes()
        self.assertEqual([], verify_package(self.root))
        self.assertEqual(before, (shared / "MANIFEST.json").read_bytes())

    def test_digest_matches_sha256(self) -> None:
        self.assertEqual(hashlib.sha256(b"Sample task\n").hexdigest(),
                         canonical_digest(self.root / "README.md"))


if __name__ == "__main__":
    unittest.main()
