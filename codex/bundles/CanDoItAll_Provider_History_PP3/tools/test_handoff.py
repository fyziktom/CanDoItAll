"""Regression tests for handoff/evidence checks, not for CanDoItAll."""
from __future__ import annotations

import copy
import json
import tempfile
import unittest
from pathlib import Path

from validate_handoff import check_evidence, check_links, under

ROOT = Path(__file__).resolve().parents[1]
REQUIRED = json.loads((ROOT / "bundle.json").read_text())["required_group_ids"]
TEMPLATE = json.loads((ROOT / "templates/evidence.json").read_text())


def complete_evidence() -> dict:
    data = copy.deepcopy(TEMPLATE)
    data["source_pair"] = {key: "a" * 40 for key in data["source_pair"]}
    data["attempts"] = [{"id": "example", "kind": "test", "status": "PASS", "command": "synthetic validator fixture",
                         "source_fingerprint": "b" * 64, "artifact_refs": ["private/example.trx"],
                         "expected_discovery": 1, "actual_discovery": 1, "executed": 1,
                         "passed": 1, "failed": 0, "skipped": 0}]
    for group in data["groups"]:
        group.update(status="PASS", attempt_ids=["example"])
    data["closure"].update(history_ui_complete=True, tooltip_followup_complete=True,
                           native_history_campaign_passed=True, dependency_delivery="VERIFIED_LOCAL",
                           signed_commits=[{"sha": "c" * 40, "signature_verified": True}])
    return data


class EvidenceTests(unittest.TestCase):
    def test_empty_template_is_valid_structure(self):
        self.assertEqual([], check_evidence(TEMPLATE, REQUIRED))

    def test_empty_template_is_not_complete(self):
        self.assertTrue(check_evidence(TEMPLATE, REQUIRED, True))

    def test_synthetic_complete_shape(self):
        self.assertEqual([], check_evidence(complete_evidence(), REQUIRED, True))

    def test_missing_group(self):
        data = complete_evidence()
        data["groups"].pop()
        self.assertTrue(check_evidence(data, REQUIRED, True))

    def test_duplicate_group(self):
        data = complete_evidence()
        data["groups"].append(data["groups"][0])
        self.assertTrue(check_evidence(data, REQUIRED))

    def test_pass_without_attempt(self):
        data = complete_evidence()
        data["groups"][0]["attempt_ids"] = []
        self.assertTrue(check_evidence(data, REQUIRED))

    def test_unknown_attempt(self):
        data = complete_evidence()
        data["groups"][0]["attempt_ids"] = ["missing"]
        self.assertTrue(check_evidence(data, REQUIRED))

    def test_bad_test_counters(self):
        for changes in ({"expected_discovery": 0}, {"actual_discovery": 2}, {"executed": 0},
                        {"failed": 1}, {"skipped": 1}, {"passed": True}):
            with self.subTest(changes=changes):
                data = complete_evidence()
                data["attempts"][0].update(changes)
                self.assertTrue(check_evidence(data, REQUIRED))

    def test_theory_expansion_needs_note(self):
        data = complete_evidence()
        data["attempts"][0].update(executed=2, passed=2)
        self.assertTrue(check_evidence(data, REQUIRED))
        data["attempts"][0]["expansion_note"] = "Two executed theory rows, one discovery item."
        self.assertEqual([], check_evidence(data, REQUIRED))

    def test_failed_attempt_can_be_retained_beside_followup(self):
        data = complete_evidence()
        data["attempts"].append({"id": "original", "status": "FAIL"})
        data["groups"][0]["attempt_ids"].append("original")
        self.assertEqual([], check_evidence(data, REQUIRED, True))

    def test_unknown_source_or_unsigned_commit_blocks_closure(self):
        data = complete_evidence()
        data["source_pair"]["components_head"] = None
        self.assertTrue(check_evidence(data, REQUIRED, True))
        data = complete_evidence()
        data["closure"]["signed_commits"][0]["signature_verified"] = False
        self.assertTrue(check_evidence(data, REQUIRED, True))

    def test_qualified_not_complete(self):
        data = complete_evidence()
        data["groups"][0].update(status="QUALIFIED", notes="Original raw logs unavailable.")
        self.assertEqual([], check_evidence(data, REQUIRED))
        self.assertTrue(check_evidence(data, REQUIRED, True))

    def test_missing_artifacts(self):
        data = complete_evidence()
        data["attempts"][0]["artifact_refs"] = []
        self.assertTrue(check_evidence(data, REQUIRED))


class PathTests(unittest.TestCase):
    def test_reject_escape(self):
        with self.assertRaises(ValueError):
            under(ROOT, "../outside")

    def test_local_links(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "a.md").write_text("[local](b.md) [web](https://example.test) [self](#heading)")
            (root / "b.md").write_text("# Heading")
            self.assertEqual(([], 1), check_links(root))
            (root / "b.md").unlink()
            self.assertTrue(check_links(root)[0])


if __name__ == "__main__":
    unittest.main()
