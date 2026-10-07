#!/usr/bin/env python3
"""Test handoff consistency checks, never the C# application."""
from __future__ import annotations
import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from validate_handoff import check_evidence, check_links, check_manifest, load, under, validate

ROOT = Path(__file__).resolve().parents[1]
REQUIRED = load(ROOT / "bundle.json")["required_group_ids"]
TEMPLATE = load(ROOT / "templates/evidence.json")

def complete_evidence():
    value = copy.deepcopy(TEMPLATE)
    value["source_pair"] = {key: "a" * 40 for key in ("main_head", "components_head", "filetools_head")}
    value["attempts"] = [{"id": "fixture-test", "kind": "test", "status": "PASS", "command": "fixture command",
        "source_fingerprint": "b" * 64, "artifact_refs": ["fixture-only.trx"], "expected_discovery": 2,
        "actual_discovery": 2, "executed": 2, "passed": 2, "failed": 0, "skipped": 0}]
    for group in value["groups"]:
        group["status"] = "PASS"
        group["attempt_ids"] = ["fixture-test"]
    value["closure"].update(workflow_authoring_ui_complete=True, lossless_native_roundtrip_passed=True,
        native_consumer_campaign_passed=True, dependency_delivery="VERIFIED_REMOTE",
        signed_commits=[{"sha": "c" * 40, "signature_verified": True}])
    return value

class HandoffTests(unittest.TestCase):
    def test_package_valid(self):
        self.assertEqual([], validate(ROOT)[0])
    def test_sealed_template_is_unexecuted(self):
        self.assertFalse(TEMPLATE["attempts"])
        self.assertTrue(all(g["status"] == "NOT_RUN" for g in TEMPLATE["groups"]))
    def test_template_not_complete(self):
        self.assertTrue(check_evidence(TEMPLATE, REQUIRED, True))
    def test_consistent_fixture_accepted_as_structure_only(self):
        self.assertFalse(check_evidence(complete_evidence(), REQUIRED, True))
    def test_missing_group(self):
        value=complete_evidence(); value["groups"].pop()
        self.assertTrue(check_evidence(value, REQUIRED, True))
    def test_duplicate_group(self):
        value=complete_evidence(); value["groups"].append(value["groups"][0])
        self.assertTrue(check_evidence(value, REQUIRED, True))
    def test_unknown_attempt(self):
        value=complete_evidence(); value["groups"][0]["attempt_ids"]=["absent"]
        self.assertTrue(check_evidence(value, REQUIRED, True))
    def test_no_command_is_not_pass(self):
        value=complete_evidence(); value["attempts"][0]["command"]=""
        self.assertTrue(check_evidence(value, REQUIRED, True))
    def test_no_fingerprint_is_not_pass(self):
        value=complete_evidence(); value["attempts"][0]["source_fingerprint"]="wrong"
        self.assertTrue(check_evidence(value, REQUIRED, True))
    def test_zero_tests_is_not_pass(self):
        value=complete_evidence(); value["attempts"][0].update(expected_discovery=0, actual_discovery=0, executed=0, passed=0)
        self.assertTrue(check_evidence(value, REQUIRED, True))
    def test_failed_case_is_not_pass(self):
        value=complete_evidence(); value["attempts"][0].update(failed=1, passed=1)
        self.assertTrue(check_evidence(value, REQUIRED, True))
    def test_skipped_case_is_not_pass(self):
        value=complete_evidence(); value["attempts"][0]["skipped"]=1
        self.assertTrue(check_evidence(value, REQUIRED, True))
    def test_expansion_needs_note(self):
        value=complete_evidence(); value["attempts"][0].update(executed=3, passed=3)
        self.assertTrue(check_evidence(value, REQUIRED, True))
        value["attempts"][0]["expansion_note"]="One fixture theory expands to two runtime cases."
        self.assertFalse(check_evidence(value, REQUIRED, True))
    def test_qualified_not_complete(self):
        value=complete_evidence(); value["groups"][0].update(status="QUALIFIED", notes="Fixture only")
        self.assertTrue(check_evidence(value, REQUIRED, True))
    def test_missing_signature(self):
        value=complete_evidence(); value["closure"]["signed_commits"][0]["signature_verified"]=False
        self.assertTrue(check_evidence(value, REQUIRED, True))
    def test_local_delivery_needs_qualification(self):
        value=complete_evidence(); value["closure"]["dependency_delivery"]="VERIFIED_LOCAL"
        self.assertTrue(check_evidence(value, REQUIRED, True))
        value["closure"]["dependency_delivery_notes"]="Remote publication remains pending."
        self.assertFalse(check_evidence(value, REQUIRED, True))
    def test_unresolved_finding_not_closed(self):
        value=complete_evidence(); value["closure"]["unresolved_findings"]=["fixture-critical"]
        self.assertTrue(check_evidence(value, REQUIRED, True))
    def test_paid_call_not_authorized(self):
        value=complete_evidence(); value["closure"]["paid_model_calls"]=1
        self.assertTrue(check_evidence(value, REQUIRED, True))
    def test_path_escape_rejected(self):
        with self.assertRaises(ValueError): under(ROOT, "../outside")
    def test_manifest_detects_change(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp); (root/"a.md").write_text("a")
            digest=hashlib.sha256(b"a").hexdigest()
            (root/"MANIFEST.sha256").write_text(digest+"  a.md\n")
            self.assertFalse(check_manifest(root)[0])
            (root/"a.md").write_text("b")
            self.assertTrue(check_manifest(root)[0])
    def test_broken_link(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp); (root/"a.md").write_text("[bad](missing.md)")
            self.assertTrue(check_links(root)[0])
    def test_source_ids_cover_references(self):
        import re
        ids={row["id"] for row in load(ROOT/"sources.json")["sources"]}
        refs=set()
        for path in ROOT.glob("*.md"):
            refs.update(re.findall(r"\bS\d{2}\b", path.read_text()))
        self.assertTrue(refs <= ids, refs-ids)

if __name__ == "__main__":
    unittest.main()
