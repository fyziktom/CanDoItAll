"""Test static census helper behavior, not the product's component boundaries."""
from pathlib import Path
import tempfile
import unittest
from inventory_renderers import inventory, selected

class InventoryTests(unittest.TestCase):
    def test_existing_family_selected(self):
        self.assertTrue(selected("src/UI/CanDoItAll.AgentFramework.UI/Chat/A.razor"))
    def test_other_module_not_implicitly_added(self):
        self.assertFalse(selected("src/Modules/CanDoItAll.Modules.Workbench/A.razor"))
    def test_codebehind_not_claimed_renderer(self):
        self.assertFalse(selected("src/Modules/CanDoItAll.Modules.AgentFramework/A.razor.cs"))
    def test_bom_and_markers_are_only_pending_inventory(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            name = "src/Modules/CanDoItAll.Modules.AgentFramework/A.razor"
            p = root / name
            p.parent.mkdir(parents=True)
            p.write_text('@page "/agents"\n@inject IFixture Owner\n<div/>', encoding="utf-8-sig")
            rows = inventory(root, [name, name])
            self.assertEqual(1, len(rows))
            self.assertEqual("PENDING", rows[0]["classification"])
            self.assertEqual(1, rows[0]["route_markers"])
            self.assertEqual(1, rows[0]["inject_markers"])
            self.assertEqual(64, len(rows[0]["sha256"]))
    def test_missing_tracked_input_fails(self):
        with tempfile.TemporaryDirectory() as folder:
            with self.assertRaises(FileNotFoundError):
                inventory(Path(folder), ["src/Modules/CanDoItAll.Modules.AgentFramework/Missing.razor"])

if __name__ == "__main__":
    unittest.main()
