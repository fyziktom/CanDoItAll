#!/usr/bin/env python3

from __future__ import annotations

import hashlib
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).with_name("scan_artifacts_for_secrets.py")


class SecretArtifactScannerTests(unittest.TestCase):
    def test_report_is_metadata_only_when_line_contains_multiple_secrets(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            first = "adjacent-secret-value"
            second = "another-secret-value"
            (root / "artifact.log").write_text(
                f'api_key={first}; secret: {second}\n',
                encoding="utf-8",
            )
            report_path = root / "report.json"

            result = self.run_scanner(root, report_path, "--report-only")

            self.assertEqual(0, result.returncode, result.stderr)
            report_text = report_path.read_text(encoding="utf-8")
            report = json.loads(report_text)
            self.assertEqual(1, report["finding_count"])
            self.assertNotIn("redacted_excerpt", report["findings"][0])
            self.assertNotIn(first, report_text)
            self.assertNotIn(second, report_text)

    def test_private_sentinel_is_fingerprinted_without_value_disclosure(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            sentinel_path = root.parent / f"{root.name}-sentinels.txt"
            sentinel = "private-sentinel-value-a04"
            try:
                sentinel_path.write_text(sentinel + "\n", encoding="utf-8")
                (root / "artifact.log").write_text(
                    f"before {sentinel} after\n",
                    encoding="utf-8",
                )
                report_path = root / "report.json"

                result = self.run_scanner(
                    root,
                    report_path,
                    "--report-only",
                    "--sentinel-file",
                    str(sentinel_path),
                    "--max-file-bytes",
                    "4096",
                    "--exclude-directory",
                    "excluded",
                )

                self.assertEqual(0, result.returncode, result.stderr)
                report_text = report_path.read_text(encoding="utf-8")
                report = json.loads(report_text)
                self.assertEqual(3, report["schema_version"])
                self.assertEqual(1, report["sentinel_input_file_count"])
                self.assertEqual(1, report["sentinel_value_count"])
                self.assertEqual(1, report["sentinel_finding_count"])
                self.assertEqual(4096, report["max_file_bytes"])
                self.assertEqual(["excluded"], report["excluded_directories"])
                self.assertEqual(
                    hashlib.sha256(sentinel.encode("utf-8")).hexdigest()[:16],
                    report["findings"][0]["fingerprint"],
                )
                self.assertNotIn(sentinel, report_text)
                self.assertNotIn(sentinel, result.stdout)
            finally:
                sentinel_path.unlink(missing_ok=True)

    def test_report_accounts_for_scanned_oversized_and_non_text_files(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            (root / "evidence.patch").write_text("safe evidence\n", encoding="utf-8")
            (root / "oversized.log").write_text("x" * 32, encoding="utf-8")
            (root / "source.tar").write_bytes(b"binary")
            report_path = root / "report.json"

            result = self.run_scanner(
                root,
                report_path,
                "--report-only",
                "--max-file-bytes",
                "16",
            )

            self.assertEqual(0, result.returncode, result.stderr)
            report = json.loads(report_path.read_text(encoding="utf-8"))
            coverage = report["coverage"]
            self.assertEqual(1, coverage["scanned_text_files"])
            self.assertEqual(1, coverage["oversized_text_files"]["count"])
            self.assertEqual("oversized.log", coverage["oversized_text_files"]["files"][0]["path"])
            self.assertEqual(1, coverage["excluded_non_text_files"]["count"])
            self.assertEqual("source.tar", coverage["excluded_non_text_files"]["files"][0]["path"])
            self.assertEqual(0, coverage["unreadable_text_files"]["count"])

    def run_scanner(self, root: Path, report_path: Path, *arguments: str) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [
                sys.executable,
                str(SCRIPT),
                "--root",
                str(root),
                "--output",
                str(report_path),
                *arguments,
            ],
            check=False,
            capture_output=True,
            text=True,
        )


if __name__ == "__main__":
    unittest.main()
