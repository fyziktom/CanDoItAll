"""Test this handoff's gates and unexecuted scope, not the C# product."""
import copy
import unittest
from pathlib import Path
from validate_handoff import check_evidence, load
from test_handoff import complete_evidence, REQUIRED

ROOT = Path(__file__).resolve().parents[1]

class PlanningContractTests(unittest.TestCase):
    def test_not_started_product_ledger(self):
        data = load(ROOT / "templates/evidence.json")
        self.assertEqual(33, len(data["groups"]))
        self.assertTrue(all(group["status"] == "NOT_RUN" for group in data["groups"]))
    def test_quote_fix_is_required_for_closure(self):
        data = complete_evidence()
        data["closure"]["quote_lifetime_fixed"] = False
        self.assertTrue(check_evidence(data, REQUIRED, True))
    def test_native_task_proof_is_required(self):
        data = complete_evidence()
        data["closure"]["calendar_gantt_task_native_proof"] = False
        self.assertTrue(check_evidence(data, REQUIRED, True))
    def test_retained_shared_bytes_exact(self):
        import hashlib
        for entry in load(ROOT / "shared-provenance.json")["files"]:
            self.assertEqual(entry["sha256"], hashlib.sha256((ROOT / entry["path"]).read_bytes()).hexdigest())
    def test_counter_booleans_are_not_test_counts(self):
        data = complete_evidence()
        data["attempts"][0]["expected_discovery"] = True
        self.assertTrue(check_evidence(data, REQUIRED, True))
    def test_not_all_workbench_claim(self):
        text = (ROOT / "prompt.md").read_text(encoding="utf-8")
        self.assertIn("No global Structure canvas", text)
        self.assertIn("Do not stop after S0", text)
        self.assertIn("1920×1080", text)

if __name__ == "__main__":
    unittest.main()
