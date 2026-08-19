#!/usr/bin/env python3

import importlib.util
import json
import tempfile
import unittest
from collections import Counter
from pathlib import Path


SCRIPT_PATH = Path(__file__).with_name("enforce_portability_baseline.py")
SPEC = importlib.util.spec_from_file_location("enforce_portability_baseline", SCRIPT_PATH)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class PortabilityBaselineTests(unittest.TestCase):
    def test_exact_reviewed_baseline_passes(self) -> None:
        scan = self.scan([self.finding("src/App/Program.cs", "os-branch", "a" * 64)])
        current = MODULE.protected_counter(scan)

        self.assertEqual(current, self.round_trip_baseline(scan, current))

    def test_new_or_copied_executable_finding_is_rejected(self) -> None:
        scan = self.scan([self.finding("src/App/Program.cs", "windows-path", "b" * 64)])
        baseline = MODULE.protected_counter(scan)
        changed = self.scan(
            [
                self.finding("src/App/Program.cs", "windows-path", "b" * 64),
                self.finding("src/App/Program.cs", "windows-path", "b" * 64),
            ]
        )

        additions = MODULE.protected_counter(changed) - baseline

        self.assertEqual(1, sum(additions.values()))

    def test_removed_finding_leaves_stale_allowance(self) -> None:
        scan = self.scan([self.finding("tools/Runner.cs", "secret-provider", "c" * 64)])
        baseline = MODULE.protected_counter(scan)

        stale = baseline - MODULE.protected_counter(self.scan([]))

        self.assertEqual(1, sum(stale.values()))

    def test_test_and_documentation_findings_do_not_create_production_allowances(self) -> None:
        scan = self.scan(
            [
                self.finding("tests/Unit/ExampleTests.cs", "windows-path", "d" * 64),
                self.finding("docs/example.md", "secret-provider", "e" * 64),
            ]
        )

        self.assertFalse(MODULE.protected_counter(scan))

    def test_pattern_drift_requires_explicit_baseline_refresh(self) -> None:
        scan = self.scan([])
        with tempfile.TemporaryDirectory() as root:
            baseline_path = Path(root) / "baseline.json"
            MODULE.write_baseline(baseline_path, scan, MODULE.protected_counter(scan))
            changed_scan = self.scan([])
            changed_scan["scan"]["patterns_sha256"] = "f" * 64

            with self.assertRaisesRegex(ValueError, "patterns changed"):
                MODULE.load_baseline(baseline_path, changed_scan)

    def test_unenforced_pattern_category_is_rejected(self) -> None:
        scan = self.scan([])
        scan["scan"]["pattern_categories"].append("new-risk")
        with tempfile.TemporaryDirectory() as root:
            scan_path = Path(root) / "scan.json"
            scan_path.write_text(json.dumps(scan), encoding="utf-8")

            with self.assertRaisesRegex(ValueError, "categories do not match"):
                MODULE.load_scan(scan_path)

    @staticmethod
    def finding(path: str, category: str, fingerprint: str) -> dict:
        return {
            "path": path,
            "category": category,
            "source_fingerprint": fingerprint,
        }

    @staticmethod
    def scan(findings: list[dict]) -> dict:
        return {
            "schema_version": 2,
            "scan": {
                "patterns_sha256": "0" * 64,
                "pattern_categories": sorted(MODULE.POLICY_CATEGORIES),
            },
            "summary": {"truncated": False},
            "findings": findings,
        }

    @staticmethod
    def round_trip_baseline(scan: dict, current: Counter) -> Counter:
        with tempfile.TemporaryDirectory() as root:
            path = Path(root) / "baseline.json"
            MODULE.write_baseline(path, scan, current)
            return MODULE.load_baseline(path, scan)


if __name__ == "__main__":
    unittest.main()
