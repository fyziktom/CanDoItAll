#!/usr/bin/env python3
"""Exercise WB2 handoff data checks, not the application."""
from __future__ import annotations
import copy
import unittest
from test_handoff import ROOT, TEMPLATE, REQUIRED, complete_evidence
from validate_handoff import check_evidence, load

class WB2EvidenceTests(unittest.TestCase):
    def test_exact_scope_and_empty_product_evidence(self):
        self.assertEqual(29, len(REQUIRED))
        self.assertEqual("CDA-WORKBENCH-INSIGHTS-WB2", TEMPLATE["bundle_id"])
        self.assertFalse(TEMPLATE["closure"]["gantt_cleanup_fixed"])
        self.assertFalse(TEMPLATE["closure"]["insights_ui_boundary_complete"])

    def test_obsolete_bundle_rejected(self):
        value = complete_evidence()
        value["bundle_id"] = "CDA-WORKBENCH-PLANNING-WB1"
        self.assertTrue(check_evidence(value, REQUIRED, True))

    def test_duplicate_attempt_rejected(self):
        value = complete_evidence()
        value["attempts"].append(copy.deepcopy(value["attempts"][0]))
        self.assertTrue(check_evidence(value, REQUIRED, True))

    def test_discovery_mismatch_rejected(self):
        value = complete_evidence()
        value["attempts"][0]["actual_discovery"] += 1
        self.assertTrue(check_evidence(value, REQUIRED, True))

    def test_boolean_counters_rejected(self):
        value = complete_evidence()
        value["attempts"][0]["skipped"] = False
        self.assertTrue(check_evidence(value, REQUIRED, True))

    def test_failed_original_attempt_can_be_retained(self):
        value = complete_evidence()
        value["attempts"].insert(0, {"id": "original-failure", "kind": "test", "status": "FAIL", "notes": "Synthetic parser fixture."})
        value["groups"][0]["attempt_ids"].insert(0, "original-failure")
        self.assertFalse(check_evidence(value, REQUIRED, True))

    def test_published_component_ref_in_provenance(self):
        data = load(ROOT / "review-provenance.json")
        self.assertEqual("dc573e2b438621599401a28968acef3682d14e63", data["components"]["head"])
        self.assertTrue(data["components"]["published"])
        self.assertFalse(data["product_tests_executed"])

if __name__ == "__main__":
    unittest.main()
