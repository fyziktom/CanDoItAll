import copy
import unittest

from check_rendering_register import REQUIRED, validate


class RenderingReviewTests(unittest.TestCase):
    def setUp(self):
        self.current = {"src/Leaf/Surface.razor": {"sha256_lf": "reviewed", "kind": "RAZOR", "role": "PRODUCT"}}
        self.row = {field: "Explicit reviewed disposition" for field in REQUIRED}
        self.row.update(path="src/Leaf/Surface.razor", classification="EXTRACTED_RENDERER", **self.current["src/Leaf/Surface.razor"])
        self.register = {"entries": [self.row], "families": {"Explicit reviewed disposition": {"project": "src/Leaf/Leaf.csproj"}}}

    def test_reviewed_input_passes(self):
        self.assertEqual([], validate(self.current, self.register))

    def test_changed_source_fails_without_review(self):
        current = copy.deepcopy(self.current)
        current[self.row["path"]]["sha256_lf"] = "changed"
        self.assertTrue(any("Changed rendering input" in error for error in validate(current, self.register)))

    def test_new_csharp_renderer_fails(self):
        current = dict(self.current, **{"src/Native/Hidden.cs": {"sha256_lf": "new", "kind": "CS_RENDER_OR_ACTIVATION", "role": "PRODUCT"}})
        self.assertTrue(any("New rendering input" in error for error in validate(current, {"entries": [self.row]})))

    def test_removed_input_requires_disposition(self):
        self.assertTrue(any("Stale rendering input" in error for error in validate({}, {"entries": [self.row]})))

    def test_unclassified_is_not_closure(self):
        self.row["classification"] = "UNCLASSIFIED"
        self.assertTrue(any("Unreviewed classification" in error for error in validate(self.current, {"entries": [self.row]})))

    def test_missing_rationale_and_duplicate_fail(self):
        self.row.pop("rationale")
        failures = validate(self.current, {"entries": [self.row, self.row]})
        self.assertTrue(any("Missing rationale" in error for error in failures))
        self.assertTrue(any("Duplicate entry" in error for error in failures))

    def test_unresolved_family_cannot_stand_in_for_asset_or_scenario_proof(self):
        self.register["families"].clear()
        failures = validate(self.current, self.register)
        self.assertEqual(3, sum("Unresolved" in error for error in failures))


if __name__ == "__main__":
    unittest.main()
