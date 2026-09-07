import codecs
import hashlib
import subprocess
import tempfile
import unittest
from pathlib import Path

from scan_text_encoding import inspect_bytes
from validate_bundle_delivery import Inventory, markdown_targets, validate


class EncodingTests(unittest.TestCase):
    def test_ascii_is_valid(self):
        self.assertEqual([], inspect_bytes("source.cs", b"public class Example {}"))

    def test_english_punctuation_is_valid(self):
        self.assertEqual([], inspect_bytes("notes.md", "Agents \u00b7 Overview \u2014 it\u2019s ready".encode()))

    def test_localized_utf8_is_valid(self):
        self.assertEqual([], inspect_bytes("notes.md", "\u010cesk\u00fd jazyk; fran\u00e7ais; \u00c2ngela; \u4e2d\u6587".encode()))

    def test_known_mojibake_is_rejected(self):
        for text in ["Agents \u00c2\u00b7", "\u00c3\u00a9", "\u00e2\u20ac\u201d", "\u0102\u02d8\u00e2\u201a\u00ac"]:
            with self.subTest(text=ascii(text)):
                self.assertIn("mojibake", [f["kind"] for f in inspect_bytes("text.md", text.encode())])

    def test_replacement_and_c1_are_rejected(self):
        self.assertEqual(["replacement-character", "c1-control"], [f["kind"] for f in inspect_bytes("a.cs", "\ufffd\u0085".encode())])

    def test_invalid_or_mixed_encoding_is_rejected(self):
        self.assertEqual("invalid-utf8", inspect_bytes("a.md", b"UTF-8 then \xe9")[0]["kind"])
        self.assertTrue(inspect_bytes("a.cs", "text".encode("utf-16")))

    def test_binary_is_excluded_without_decoding(self):
        self.assertEqual([], inspect_bytes("capture.jpeg", b"\xff\x00\x81"))

    def test_bom_change_is_visible(self):
        self.assertEqual("bom-change", inspect_bytes("a.md", codecs.BOM_UTF8 + b"text", b"text")[0]["kind"])
        self.assertEqual([], inspect_bytes("a.md", codecs.BOM_UTF8 + b"text", codecs.BOM_UTF8 + b"old"))


class DeliveryTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.run_git("init", "-q")
        (self.root / "bundle").mkdir()

    def run_git(self, *args):
        return subprocess.check_output(["git", "-C", str(self.root), *args], stderr=subprocess.STDOUT)

    def write(self, name, data):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)

    def seal(self, files):
        self.write("bundle/MANIFEST.sha256", "".join(f"{hashlib.sha256(data).hexdigest()}  {name}\n" for name, data in files.items()).encode())
        for name, data in files.items():
            self.write("bundle/" + name, data)

    def test_proposed_untracked_retained_files_are_valid(self):
        self.seal({"README.md": b"[Receipt](retained/result.json)\n", "retained/result.json": b"{}\n"})
        self.assertEqual([], validate(Inventory(self.root), "bundle")["errors"])

    def test_proposed_proof_rejects_git_clean_byte_conversion(self):
        self.write(".gitattributes", b"* text=auto eol=lf\n")
        self.seal({"retained/result.txt": b"PASS\r\n"})
        self.assertIn("Git clean filters change retained bytes", " ".join(validate(Inventory(self.root), "bundle")["errors"]))
        self.write(".gitattributes", b"* text=auto eol=lf\nbundle/retained/** -text\n")
        self.assertEqual([], validate(Inventory(self.root), "bundle")["errors"])
        self.run_git("add", ".gitattributes", "bundle")
        tree = self.run_git("write-tree").decode().strip()
        self.assertEqual([], validate(Inventory(self.root, tree), "bundle")["errors"])

    def test_ignored_local_proof_does_not_satisfy_a_manifest(self):
        self.write(".gitignore", b"proof/\n")
        self.seal({"proof/result.txt": b"PASS"})
        self.assertIn("Not in Git inventory", " ".join(validate(Inventory(self.root), "bundle")["errors"]))

    def test_bad_hash_and_unsealed_file_are_rejected(self):
        self.seal({"README.md": b"first"})
        self.write("bundle/README.md", b"second")
        self.write("bundle/extra.txt", b"unsealed")
        errors = " ".join(validate(Inventory(self.root), "bundle")["errors"])
        self.assertIn("Hash mismatch", errors)
        self.assertIn("Unsealed", errors)

    def test_escaping_path_and_missing_link_are_rejected(self):
        self.seal({"README.md": b"[Escape](../../outside) [Missing](retained/missing.txt)"})
        errors = " ".join(validate(Inventory(self.root), "bundle")["errors"])
        self.assertIn("escapes repository", errors)
        self.write("bundle/README.md", b"[Missing](retained/missing.txt)")
        self.assertIn("Unresolved Git link", " ".join(validate(Inventory(self.root), "bundle")["errors"]))

    def test_actual_tree_does_not_accept_uncommitted_evidence(self):
        self.seal({"README.md": b"[Receipt](retained/result.txt)", "retained/result.txt": b"PASS"})
        self.run_git("add", "bundle/MANIFEST.sha256", "bundle/README.md")
        tree = self.run_git("write-tree").decode().strip()
        self.assertIn("Not in Git inventory", " ".join(validate(Inventory(self.root, tree), "bundle")["errors"]))
        self.run_git("add", "bundle/retained/result.txt")
        tree = self.run_git("write-tree").decode().strip()
        self.assertEqual([], validate(Inventory(self.root, tree), "bundle")["errors"])
        self.write("bundle/retained/result.txt", b"changed after commit")
        self.assertEqual([], validate(Inventory(self.root, tree), "bundle")["errors"])

    def test_absolute_windows_link_is_not_mistaken_for_a_web_scheme(self):
        self.seal({"README.md": b"[Local](C:/outside/receipt.txt)"})
        self.assertIn("Absolute local target", " ".join(validate(Inventory(self.root), "bundle")["errors"]))

    def test_links_decode_spaces_and_ignore_external_or_fenced_examples(self):
        text = "[Receipt](<retained/my%20file.txt>) [Web](https://example.invalid/)\n```text\n[x](not-a-real-link)\n```\n"
        self.assertEqual(["retained/my%20file.txt"], list(markdown_targets(text)))


if __name__ == "__main__":
    unittest.main()
