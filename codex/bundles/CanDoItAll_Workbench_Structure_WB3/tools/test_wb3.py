"""Validate the handoff structure and its unexecuted evidence template, not the product."""
from __future__ import annotations
import copy
import hashlib
import json
import re
import unittest
from pathlib import Path
import validate_handoff as validator

ROOT = Path(__file__).resolve().parents[1]

class StructureBundleTests(unittest.TestCase):
    def test_manifest_and_links_cover_actual_package(self):
        errors, _ = validator.validate(ROOT)
        self.assertEqual([], errors)

    def test_all_declared_sources_have_exact_refs(self):
        sources = validator.load(ROOT / "sources.json")["sources"]
        self.assertTrue(sources)
        self.assertEqual(len(sources), len({s["id"] for s in sources}))
        for source in sources:
            self.assertRegex(source["ref"], r"^[0-9a-f]{40}$")
            self.assertIn(source["ref"], source["url"])
            self.assertTrue(source["reviewed_ranges"])

    def test_reference_ids_used_by_top_level_docs_exist(self):
        known = {s["id"] for s in validator.load(ROOT / "sources.json")["sources"]}
        referenced = set()
        for path in ROOT.glob("*.md"):
            referenced.update(re.findall(r"\bS\d{2}\b", path.read_text(encoding="utf-8")))
        self.assertFalse(referenced - known, referenced - known)

    def test_sealed_template_is_not_product_completion(self):
        data = validator.load(ROOT / "templates/evidence.json")
        required = validator.load(ROOT / "bundle.json")["required_group_ids"]
        self.assertEqual([], data["attempts"])
        self.assertEqual({"NOT_RUN"}, {g["status"] for g in data["groups"]})
        self.assertTrue(validator.check_evidence(data, required, complete=True))
        self.assertFalse(data["closure"]["ready_for_next_workbench_slice"])

    def test_matrix_and_evidence_use_identical_group_ids(self):
        required = validator.load(ROOT / "bundle.json")["required_group_ids"]
        evidence = validator.load(ROOT / "templates/evidence.json")
        matrix = (ROOT / "VALIDATION_MATRIX.md").read_text(encoding="utf-8")
        self.assertEqual(set(required), set(re.findall(r"\bG\d{2}\b", matrix)))
        self.assertEqual(set(required), {g["id"] for g in evidence["groups"]})

    def test_shared_bytes_match_recorded_parent(self):
        provenance = validator.load(ROOT / "shared-provenance.json")
        self.assertEqual(22, len(provenance["files"]))
        for record in provenance["files"]:
            self.assertEqual(record["sha256"], hashlib.sha256((ROOT / record["path"]).read_bytes()).hexdigest())

    def test_qualification_cannot_be_silently_marked_complete(self):
        data = validator.load(ROOT / "templates/evidence.json")
        data = copy.deepcopy(data)
        data["groups"][0].update(status="QUALIFIED", notes="Original mixed run retained.")
        required = validator.load(ROOT / "bundle.json")["required_group_ids"]
        self.assertEqual([], validator.check_evidence(data, required))
        self.assertTrue(validator.check_evidence(data, required, complete=True))

    def test_review_does_not_claim_product_execution(self):
        provenance = validator.load(ROOT / "review-provenance.json")
        self.assertFalse(provenance["product_build_executed"])
        self.assertFalse(provenance["product_tests_executed"])
        self.assertFalse(provenance["runtime_reproduction_executed"])

if __name__ == "__main__":
    unittest.main()
