"""Exercise handoff integrity and lexical collector behavior, not the C# product."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import shutil
import tempfile
import unittest
from unittest.mock import patch

from collect_inventory import collect_from_paths, inspect_text, raw_project_references, safe_path, source_role
from verify_bundle import BundleError, payload_paths, safe_relative, verify

PACKAGE = Path(__file__).resolve().parent.parent


def seal(root: Path) -> None:
    records = []
    for name in sorted(payload_paths(root)):
        data = (root / name).read_bytes()
        records.append({"path": name, "bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()})
    (root / "MANIFEST.json").write_text(json.dumps({"schema_version": 1, "files": records}, indent=2) + "\n", encoding="utf-8")


class CollectorTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def put(self, name: str, data: str | bytes) -> None:
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data.encode() if isinstance(data, str) else data)

    def test_razor_is_not_semantic_certification(self) -> None:
        self.put("src/A/Home.razor", '@page "/"\n@inject Snapshot Owner\n<Thing />')
        report = collect_from_paths(self.root, ["src/A/Home.razor"], "abc")
        self.assertFalse(report["semantic_certification"])
        self.assertFalse(report["evaluated_graph"])
        self.assertEqual(["/"], report["entries"][0]["route_literals"])
        self.assertEqual("UNCLASSIFIED", report["entries"][0]["semantic_status"])
        self.assertTrue(report["entries"][0]["has_injection_directive"])

    def test_import_is_not_automatically_a_renderer(self) -> None:
        self.assertEqual("RAZOR_IMPORT", inspect_text("src/A/_Imports.razor", "@using X")["kind"])

    def test_csharp_render_builder_candidate(self) -> None:
        result = inspect_text("src/A/C.cs", "protected override void BuildRenderTree(RenderTreeBuilder b) {}")
        self.assertIn("BuildRenderTree", result["lexical_signals"])

    def test_dynamic_activation_candidate(self) -> None:
        result = inspect_text("src/A/Dialog.cs", "await dialogs.OpenAsync<NativeDialog>();")
        self.assertIn("OpenAsync<", result["lexical_signals"])

    def test_plain_backend_is_not_a_lexical_render_entry(self) -> None:
        self.assertIsNone(inspect_text("src/A/Repository.cs", "sealed class Repository {}"))

    def test_codebehind_is_retained_for_review(self) -> None:
        self.assertIsNotNone(inspect_text("src/A/Home.razor.cs", "partial class Home {}"))

    def test_assets_not_claimed_served(self) -> None:
        self.assertEqual("NOT_VERIFIED", inspect_text("src/A/wwwroot/app.css", ".a{}")["served_or_consumed"])

    def test_unevaluated_property_reference(self) -> None:
        refs = raw_project_references('<Project><ItemGroup><ProjectReference Include="$(Root)/A.csproj" /></ItemGroup></Project>')
        self.assertEqual("UNEVALUATED", refs[0]["status"])
        self.assertEqual("$(Root)/A.csproj", refs[0]["include"])

    def test_xml_namespace(self) -> None:
        refs = raw_project_references('<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003"><Import Project="x.targets" /></Project>')
        self.assertEqual("Import", refs[0]["kind"])

    def test_invalid_xml_is_explicit_issue(self) -> None:
        self.put("src/A/A.csproj", "<Project>")
        report = collect_from_paths(self.root, ["src/A/A.csproj"], None)
        self.assertEqual(1, len(report["issues"]))
        self.assertFalse(report["entries"])

    def test_duplicate_paths_are_not_double_counted(self) -> None:
        self.put("src/A/A.razor", "<A />")
        report = collect_from_paths(self.root, ["src/A/A.razor"] * 2, None)
        self.assertEqual(1, len(report["entries"]))

    def test_missing_indexed_source_is_not_silently_ignored(self) -> None:
        report = collect_from_paths(self.root, ["src/A/Missing.razor"], None)
        self.assertEqual(1, len(report["issues"]))

    def test_invalid_utf8_is_reported(self) -> None:
        self.put("src/A/A.razor", b"\xff\xff")
        self.assertEqual(1, len(collect_from_paths(self.root, ["src/A/A.razor"], None)["issues"]))

    def test_bounded_text_scan(self) -> None:
        self.put("src/A/A.razor", "a" * 20)
        with patch("collect_inventory.MAX_TEXT_BYTES", 8):
            self.assertEqual(1, len(collect_from_paths(self.root, ["src/A/A.razor"], None)["issues"]))

    def test_unsafe_paths_rejected(self) -> None:
        for name in ["../bad.cs", "/etc/a.cs", "C:/a.cs", "src/../../bad.cs", "src\\a.cs"]:
            with self.subTest(name=name), self.assertRaises(ValueError):
                safe_path(self.root, name)

    def test_symlink_is_not_followed(self) -> None:
        with patch.object(Path, "is_symlink", return_value=True), self.assertRaises(ValueError):
            safe_path(self.root, "src/A.razor")

    def test_role_and_content_hash(self) -> None:
        self.put("src/Sandboxes/A/A.razor", "<p>Fixture</p>")
        row = collect_from_paths(self.root, ["src/Sandboxes/A/A.razor"], None)["entries"][0]
        self.assertEqual("SCENARIO_SOURCE", row["source_role"])
        self.assertEqual(hashlib.sha256(b"<p>Fixture</p>").hexdigest(), row["sha256"])
        self.assertEqual("EXAMPLE_OR_SAMPLE_REQUIRES_CLASSIFICATION", source_role("src/A/Examples/Demo.razor"))

    def test_root_build_inputs_are_collected(self) -> None:
        self.put("Directory.Build.targets", "<Project />")
        self.assertEqual(1, len(collect_from_paths(self.root, ["Directory.Build.targets"], None)["entries"]))


class PackageTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / "bundle"
        shutil.copytree(PACKAGE, self.root, ignore=shutil.ignore_patterns("__pycache__", "*.pyc"))

    def modify_json(self, name: str, action) -> None:
        path = self.root / name
        value = json.loads(path.read_text(encoding="utf-8"))
        action(value)
        path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")
        seal(self.root)

    def test_valid_bundle(self) -> None:
        result = verify(self.root)
        self.assertEqual("PASS", result["package_integrity"])
        self.assertEqual("NOT_RUN", result["product_validation"])

    def test_tampering_detected(self) -> None:
        (self.root / "prompt.md").write_text("changed", encoding="utf-8")
        with self.assertRaises(BundleError):
            verify(self.root)

    def test_missing_file_detected(self) -> None:
        (self.root / "prompt.md").unlink()
        with self.assertRaises(BundleError):
            verify(self.root)

    def test_unlisted_payload_detected(self) -> None:
        (self.root / "extra.txt").write_text("unlisted", encoding="utf-8")
        with self.assertRaises(BundleError):
            verify(self.root)

    def test_shared_archive_content_rejected(self) -> None:
        path = self.root / "UI_Decoupling_Shared_Bundle" / "README.md"
        path.parent.mkdir()
        path.write_text("not allowed", encoding="utf-8")
        seal(self.root)
        with self.assertRaises(BundleError):
            verify(self.root)

    def test_shared_version_requirement_rejected(self) -> None:
        self.modify_json("bundle.json", lambda d: d["shared_policy"].update(require_version="next"))
        with self.assertRaises(BundleError):
            verify(self.root)

    def test_execution_pin_rejected(self) -> None:
        self.modify_json("bundle.json", lambda d: d.update(execution_pin=True))
        with self.assertRaises(BundleError):
            verify(self.root)

    def test_false_product_result_rejected(self) -> None:
        self.modify_json("evidence-template.json", lambda d: d["acceptance"][0].update(status="PASS"))
        with self.assertRaises(BundleError):
            verify(self.root)

    def test_acceptance_duplicates_rejected(self) -> None:
        self.modify_json("acceptance-plan.json", lambda d: d["groups"].append(d["groups"][0]))
        with self.assertRaises(BundleError):
            verify(self.root)

    def test_missing_evidence_row_rejected(self) -> None:
        self.modify_json("evidence-template.json", lambda d: d["acceptance"].pop())
        with self.assertRaises(BundleError):
            verify(self.root)

    def test_broken_link_rejected(self) -> None:
        with (self.root / "README.md").open("a", encoding="utf-8") as stream:
            stream.write("\n[Missing](missing.md)\n")
        seal(self.root)
        with self.assertRaises(BundleError):
            verify(self.root)

    def test_duplicate_manifest_record_rejected(self) -> None:
        path = self.root / "MANIFEST.json"
        value = json.loads(path.read_text())
        value["files"].append(value["files"][0])
        path.write_text(json.dumps(value), encoding="utf-8")
        with self.assertRaises(BundleError):
            verify(self.root)

    def test_unsafe_manifest_paths_rejected(self) -> None:
        for name in ["../outside", "/absolute", "C:/absolute", "a\\b"]:
            with self.subTest(name=name), self.assertRaises(BundleError):
                safe_relative(self.root, name)

    def test_duplicate_source_ids_rejected(self) -> None:
        self.modify_json("review-sources.json", lambda d: d["sources"].append(d["sources"][0]))
        with self.assertRaises(BundleError):
            verify(self.root)

    def test_seed_cannot_claim_exhaustive_proof(self) -> None:
        self.modify_json("inventory-seed.json", lambda d: d.update(exhaustive=True))
        with self.assertRaises(BundleError):
            verify(self.root)

    def test_push_not_implicitly_authorized(self) -> None:
        self.modify_json("bundle.json", lambda d: d["signing"].update(automatic_push_authorized=True))
        with self.assertRaises(BundleError):
            verify(self.root)


if __name__ == "__main__":
    unittest.main()
