"""Verifier regression tests; no CanDoItAll product behavior is executed."""
from __future__ import annotations

import csv
import hashlib
import json
import shutil
import tempfile
import unittest
from pathlib import Path

from verify_package import PackageError, verify

SOURCE = Path(__file__).resolve().parents[1]


def seal(root: Path) -> None:
    records = []
    for path in sorted(root.rglob("*")):
        if path.is_file() and path.name != "MANIFEST.json" and "__pycache__" not in path.parts:
            records.append({"path": path.relative_to(root).as_posix(), "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
    (root / "MANIFEST.json").write_text(json.dumps({"algorithm": "sha256", "files": records}), encoding="utf-8")


class PackageTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name) / SOURCE.name
        shutil.copytree(SOURCE, self.root, ignore=shutil.ignore_patterns("__pycache__"))

    def change_json(self, name: str, edit) -> None:
        path = self.root / name
        value = json.loads(path.read_text())
        edit(value)
        path.write_text(json.dumps(value), encoding="utf-8")
        seal(self.root)

    def test_valid_package(self) -> None:
        result = verify(self.root)
        self.assertEqual(36, result["acceptance_groups"])
        self.assertEqual(123, result["carry_forward"])

    def test_hash_corruption(self) -> None:
        with (self.root / "prompt.md").open("a") as stream:
            stream.write("corruption")
        with self.assertRaisesRegex(PackageError, "Hash mismatch"):
            verify(self.root)

    def test_missing_file(self) -> None:
        (self.root / "prompt.md").unlink()
        with self.assertRaisesRegex(PackageError, "missing"):
            verify(self.root)

    def test_unmanifested_file(self) -> None:
        (self.root / "extra.txt").write_text("extra")
        with self.assertRaisesRegex(PackageError, "coverage"):
            verify(self.root)

    def test_traversal_manifest(self) -> None:
        manifest = json.loads((self.root / "MANIFEST.json").read_text())
        manifest["files"][0]["path"] = "../outside"
        (self.root / "MANIFEST.json").write_text(json.dumps(manifest))
        with self.assertRaisesRegex(PackageError, "Unsafe path"):
            verify(self.root)

    def test_duplicate_manifest_path(self) -> None:
        manifest = json.loads((self.root / "MANIFEST.json").read_text())
        manifest["files"].append(manifest["files"][0])
        (self.root / "MANIFEST.json").write_text(json.dumps(manifest))
        with self.assertRaisesRegex(PackageError, "Duplicate manifest"):
            verify(self.root)

    def test_shared_copy(self) -> None:
        path = self.root / "shared" / "README.md"
        path.parent.mkdir()
        path.write_text("Not allowed")
        seal(self.root)
        with self.assertRaisesRegex(PackageError, "Shared copy"):
            verify(self.root)

    def test_shared_pin(self) -> None:
        self.change_json("bundle.json", lambda value: value["shared_policy"].update(version_pin=True))
        with self.assertRaisesRegex(PackageError, "version_pin"):
            verify(self.root)

    def test_execution_pin(self) -> None:
        self.change_json("bundle.json", lambda value: value.update(execution_pin=True))
        with self.assertRaisesRegex(PackageError, "execution pin"):
            verify(self.root)

    def test_missing_local_link(self) -> None:
        with (self.root / "README.md").open("a") as stream:
            stream.write("\n[Missing](not-present.md)\n")
        seal(self.root)
        with self.assertRaisesRegex(PackageError, "Missing local link"):
            verify(self.root)

    def test_duplicate_acceptance(self) -> None:
        self.change_json("acceptance-plan.json", lambda value: value["groups"][1].update(id=value["groups"][0]["id"]))
        with self.assertRaisesRegex(PackageError, "acceptance IDs"):
            verify(self.root)

    def test_missing_carry_forward(self) -> None:
        path = self.root / "CARRY_FORWARD.csv"
        lines = path.read_text().splitlines()
        path.write_text("\n".join(lines[:-1]) + "\n")
        seal(self.root)
        with self.assertRaisesRegex(PackageError, "PC3 carry-forward"):
            verify(self.root)

    def test_premature_product_pass(self) -> None:
        self.change_json("evidence-template.json", lambda value: value["lanes"][0].update(status="PASS"))
        with self.assertRaisesRegex(PackageError, "Premature product"):
            verify(self.root)

    def test_unknown_source_reference(self) -> None:
        self.change_json("findings.json", lambda value: value["findings"][0]["sources"].append("S999"))
        with self.assertRaisesRegex(PackageError, "Unknown source"):
            verify(self.root)

    def test_nested_archive(self) -> None:
        (self.root / "shared.zip").write_bytes(b"not a real archive")
        seal(self.root)
        with self.assertRaisesRegex(PackageError, "Nested archive"):
            verify(self.root)

    def test_symlink(self) -> None:
        path = self.root / "linked.md"
        try:
            path.symlink_to(self.root / "README.md")
        except (OSError, NotImplementedError):
            self.skipTest("This test host cannot create symlinks")
        with self.assertRaisesRegex(PackageError, "Symlink"):
            verify(self.root)


if __name__ == "__main__":
    unittest.main()
