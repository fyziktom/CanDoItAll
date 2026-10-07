#!/usr/bin/env python3
"""Test WB4 package constraints and evidence shape, never product behavior."""
from pathlib import Path
import unittest
from test_handoff import complete_evidence, REQUIRED, ROOT
from validate_handoff import check_evidence, load, QUALIFIABLE_GROUPS

class Wb4Tests(unittest.TestCase):
    def text(self, name):
        return (ROOT/name).read_text(encoding="utf-8")
    def test_36_named_groups(self):
        self.assertEqual([f"G{i:02d}" for i in range(36)], REQUIRED)
    def test_shared_files_identical_to_provenance(self):
        import hashlib
        for item in load(ROOT/"shared-provenance.json")["files"]:
            self.assertEqual(item["sha256"], hashlib.sha256((ROOT/item["path"]).read_bytes()).hexdigest())
    def test_scope_does_not_claim_whole_workbench(self):
        self.assertIn("not all of Workbench", self.text("SCOPE_AND_ARCHITECTURE.md"))
        self.assertIn("Processes product module", self.text("DEFERRED_AND_ROADMAP.md"))
    def test_original_target_native_reproduction(self):
        text=self.text("S0_TEXT_TARGET.md")
        for token in ("same public", "native", "capturedSurface", "known saved node", "not a claimed runtime incident"):
            self.assertIn(token, text)
    def test_direct_edit_is_not_readonly_browser(self):
        text=self.text("FILES_AND_INTERACTION.md")
        for token in ("read-only", "SaveTarget", "persisted revision", "Diff", "dirty"):
            self.assertIn(token, text)
    def test_queue_not_falsely_durable(self):
        text=self.text("GENERATED_CONTENT.md")
        for token in ("Channel.CreateBounded(64)", "in-memory", "not", "replay", "placeholder"):
            self.assertIn(token, text)
    def test_stored_export_not_only_download(self):
        text=self.text("SUMMARY_AND_TRANSCRIPT.md")
        self.assertIn("not just browser downloads", text)
        self.assertIn("does not recognize audio", text)
    def test_explicit_dependency_unavailable(self):
        text=self.text("DEPENDENCY_DELIVERY.md")
        for token in ("404", "VERIFIED_LOCAL", "do not", "a120106b"):
            self.assertIn(token.lower(), text.lower())
    def test_actual_hidden_canary_and_denial(self):
        text=self.text("APPLICATION_JOURNEYS.md")
        self.assertIn("unknown to the model prompt", text)
        self.assertIn("Deny a separate proposal", text)
    def test_desktop_signing_history(self):
        text=self.text("prompt.md")
        for token in ("1920x1080/DPR1", "PGP", "historical bundles", "Do not stop", "No paid"):
            self.assertIn(token, text)
    def test_qualified_native_authority_rejected(self):
        value=complete_evidence()
        value["groups"][14].update(status="QUALIFIED", notes="fixture", limitation="fixture", qualification_review="fixture")
        self.assertTrue(check_evidence(value, REQUIRED, True))
    def test_allowed_qualification_needs_details(self):
        value=complete_evidence()
        value["groups"][28].update(status="QUALIFIED", notes="Measured fixture limitation")
        self.assertTrue(check_evidence(value, REQUIRED, True))
    def test_allowed_qualification_with_evidence(self):
        value=complete_evidence()
        value["groups"][28].update(status="QUALIFIED", notes="Measured fixture limitation", limitation="CSS removal", qualification_review="No native behavior waived")
        self.assertFalse(check_evidence(value, REQUIRED, True))
    def test_qualification_cannot_waive_required_static(self):
        value=complete_evidence()
        value["groups"][34].update(status="QUALIFIED", notes="Historical source scan", limitation="Retained control", qualification_review="Separate scope")
        value["closure"]["static_gates_passed"]=False
        self.assertTrue(check_evidence(value, REQUIRED, True))
    def test_qualification_requires_referenced_attempt(self):
        value=complete_evidence()
        value["groups"][33].update(status="QUALIFIED", notes="Mixed broad original", limitation="Fixture", qualification_review="Fixture", attempt_ids=[])
        self.assertTrue(check_evidence(value, REQUIRED, True))
    def test_no_duplicate_discovery_counts(self):
        value=complete_evidence()
        value["attempts"][0]["actual_discovery"]=3
        self.assertTrue(check_evidence(value, REQUIRED, True))
    def test_boolean_is_not_counter(self):
        value=complete_evidence()
        value["attempts"][0]["failed"]=False
        self.assertTrue(check_evidence(value, REQUIRED, True))
    def test_blocked_does_not_close(self):
        value=complete_evidence()
        value["groups"][0].update(status="BLOCKED", notes="Missing dependency")
        self.assertTrue(check_evidence(value, REQUIRED, True))
    def test_sources_exact_ref_and_bounds(self):
        sources=load(ROOT/"sources.json")["sources"]
        self.assertEqual(25, len(sources))
        self.assertTrue(all(len(s["ref"])==40 and s["review_scope"] for s in sources))
    def test_only_three_qualifiable_groups(self):
        self.assertEqual({"G28", "G33", "G34"}, QUALIFIABLE_GROUPS)

if __name__ == "__main__":
    unittest.main()
