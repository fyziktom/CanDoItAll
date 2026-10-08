"""Self-tests use only isolated synthetic packages and temporary Git repositories."""
from __future__ import annotations

import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

import handoff_tools as tools


def write(root: Path, name: str, text: str) -> None:
    file = root / name
    file.parent.mkdir(parents=True, exist_ok=True)
    file.write_text(text, encoding="utf-8")


def seal(root: Path) -> None:
    entries = {p.relative_to(root).as_posix(): tools.digest(p)
               for p in root.rglob("*") if p.is_file() and p.name != tools.MANIFEST_NAME}
    write(root, tools.MANIFEST_NAME, json.dumps({"files": entries}, sort_keys=True))


class PackageTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.base = Path(self.temp.name)
        self.root = self.base / "packet"
        self.root.mkdir()
        write(self.root, "bundle.json", json.dumps({"id": "test-packet"}))
        write(self.root, "README.md", "# Test package\nEnglish content.\n")
        seal(self.root)

    def tearDown(self) -> None:
        self.temp.cleanup()

    def test_valid_package(self) -> None:
        self.assertEqual([], tools.verify_package(self.root))

    def test_meaningful_change_is_rejected(self) -> None:
        write(self.root, "README.md", "Changed content\n")
        self.assertTrue(any("Digest mismatch" in x for x in tools.verify_package(self.root)))

    def test_extra_file_is_rejected(self) -> None:
        write(self.root, "unexpected.md", "Unexpected\n")
        self.assertTrue(any("Unlisted file" in x for x in tools.verify_package(self.root)))

    def test_missing_file_is_rejected(self) -> None:
        (self.root / "README.md").unlink()
        self.assertTrue(any("Missing file" in x for x in tools.verify_package(self.root)))

    def test_crlf_text_checkout_is_supported(self) -> None:
        for file in self.root.rglob("*"):
            if file.is_file():
                file.write_bytes(file.read_bytes().replace(b"\n", b"\r\n"))
        self.assertEqual([], tools.verify_package(self.root))

    def test_traversal_manifest_is_rejected(self) -> None:
        write(self.root, tools.MANIFEST_NAME, json.dumps({"files": {"../outside": "0" * 64}}))
        with self.assertRaises(ValueError):
            tools.verify_package(self.root)

    @unittest.skipUnless(hasattr(os, "symlink"), "Symbolic links are unavailable.")
    def test_symlink_is_rejected(self) -> None:
        outside = self.base / "outside.md"
        outside.write_text("Outside\n", encoding="utf-8")
        try:
            (self.root / "link.md").symlink_to(outside)
        except OSError:
            self.skipTest("Creating a symbolic link is not permitted in this environment.")
        self.assertTrue(any("Symbolic link" in x for x in tools.verify_package(self.root)))

    def make_shared(self) -> Path:
        shared = self.base / "shared"
        shared.mkdir()
        write(shared, "bundle.json", json.dumps({"id": "shared-test"}))
        write(shared, "README.md", "Shared companion\n")
        seal(shared)
        metadata = {"id": "test-packet", "shared_requirement": {
            "id": "shared-test", "manifest_sha256": tools.digest(shared / tools.MANIFEST_NAME)}}
        write(self.root, "bundle.json", json.dumps(metadata))
        seal(self.root)
        return shared

    def test_valid_shared_requirement(self) -> None:
        shared = self.make_shared()
        self.assertEqual([], tools.verify_package(self.root, shared))

    def test_missing_shared_requirement_is_rejected(self) -> None:
        self.make_shared()
        self.assertTrue(any("requires --shared" in x for x in tools.verify_package(self.root)))

    def test_changed_shared_digest_is_rejected(self) -> None:
        shared = self.make_shared()
        write(shared, "README.md", "Changed shared companion\n")
        seal(shared)
        self.assertTrue(any("manifest digest mismatch" in x for x in tools.verify_package(self.root, shared)))

    def test_wrong_shared_identity_is_rejected(self) -> None:
        shared = self.make_shared()
        write(shared, "bundle.json", json.dumps({"id": "different-shared"}))
        seal(shared)
        self.assertTrue(any("ID mismatch" in x for x in tools.verify_package(self.root, shared)))


@unittest.skipUnless(shutil.which("git"), "Git is required for synthetic repository tests.")
class RepositoryTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name) / "repository"
        self.root.mkdir()
        # Isolate Git configuration; no operator repository or signing settings are changed.
        self.env = dict(os.environ, GIT_CONFIG_NOSYSTEM="1", GIT_CONFIG_GLOBAL=os.devnull)
        self.run_git("init", "-q")
        self.run_git("config", "user.name", "Synthetic Fixture")
        self.run_git("config", "user.email", "fixture@example.invalid")
        write(self.root, "src/Example/Example.razor", '@page "/fixture"\n<p>Fixture</p>\n')
        self.commit()
        self.review_head = self.run_git("rev-parse", "HEAD")
        self.register = {"review_commit": self.review_head, "sources": [{
            "path": "src/Example/Example.razor",
            "blob_sha": self.run_git("rev-parse", "HEAD:src/Example/Example.razor"),
            "coverage": "full"}]}

    def tearDown(self) -> None:
        self.temp.cleanup()

    def run_git(self, *args: str) -> str:
        proc = subprocess.run(["git", "-C", str(self.root), *args], env=self.env,
                              capture_output=True, check=True, timeout=20)
        return proc.stdout.decode().strip()

    def commit(self) -> None:
        self.run_git("add", ".")
        self.run_git("commit", "-qm", "Synthetic fixture checkpoint")

    def test_clean_registered_source(self) -> None:
        result = tools.inspect_repository(self.root, self.register)
        self.assertFalse(result["drift_detected"])
        self.assertEqual(["/fixture"], result["surface_candidates"][0]["routes"])

    def test_committed_drift_is_detected(self) -> None:
        write(self.root, "src/Example/Example.razor", "<p>New committed value</p>\n")
        self.commit()
        result = tools.inspect_repository(self.root, self.register)
        self.assertTrue(result["registered_sources"][0]["head_differs"])
        self.assertIn("Example.razor", result["review_to_head_name_delta"])

    def test_dirty_change_is_not_hidden_by_matching_head(self) -> None:
        write(self.root, "src/Example/Example.razor", "<p>Dirty value</p>\n")
        result = tools.inspect_repository(self.root, self.register)
        row = result["registered_sources"][0]
        self.assertFalse(row["head_differs"])
        self.assertTrue(row["working_tree_changed"])
        self.assertTrue(result["drift_detected"])

    def test_untracked_renderer_is_included(self) -> None:
        write(self.root, "src/Example/New.razor", "@inject IServiceProvider Services\n")
        result = tools.inspect_repository(self.root, self.register)
        row = next(x for x in result["surface_candidates"] if x["path"].endswith("New.razor"))
        self.assertFalse(row["tracked"])
        self.assertFalse(row["in_review_register"])
        self.assertEqual(1, row["injection_directives"])
        self.assertIn("IServiceProvider", row["review_markers"])

    def test_removed_registered_file_is_reported(self) -> None:
        (self.root / "src/Example/Example.razor").unlink()
        self.commit()
        result = tools.inspect_repository(self.root, self.register)
        self.assertIsNone(result["registered_sources"][0]["head_blob"])
        self.assertTrue(result["drift_detected"])

    def test_inspection_does_not_change_worktree_or_head(self) -> None:
        write(self.root, "src/Example/Example.razor", "<p>Owned pending change</p>\n")
        before = (self.run_git("status", "--porcelain=v1"), self.run_git("rev-parse", "HEAD"))
        tools.inspect_repository(self.root, self.register)
        after = (self.run_git("status", "--porcelain=v1"), self.run_git("rev-parse", "HEAD"))
        self.assertEqual(before, after)

    def test_unsafe_registered_path_is_rejected(self) -> None:
        self.register["sources"][0]["path"] = "../outside.razor"
        with self.assertRaises(ValueError):
            tools.inspect_repository(self.root, self.register)


if __name__ == "__main__":
    unittest.main()
