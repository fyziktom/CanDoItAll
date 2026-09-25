#!/usr/bin/env python3
"""Read-only integrity checks for this preparation pack, not for CanDoItAll."""
from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
import sys
from pathlib import Path
from urllib.parse import unquote, urlsplit


def validate(root: Path) -> tuple[list[str], dict[str, int]]:
    errors: list[str] = []
    counts = {"files": 0, "markdown_links": 0, "sources": 0, "traceability_rows": 0}
    root = root.resolve()
    manifest_path = root / "MANIFEST.sha256"
    if not manifest_path.is_file():
        return ["MANIFEST.sha256 is missing; package is not sealed."], counts
    expected: dict[str, str] = {}
    for number, line in enumerate(manifest_path.read_text(encoding="utf-8").splitlines(), 1):
        match = re.fullmatch(r"([0-9a-f]{64})  (.+)", line)
        if not match:
            errors.append(f"Invalid manifest line {number}.")
            continue
        digest, name = match.groups()
        destination = (root / name).resolve()
        if not destination.is_relative_to(root) or Path(name).is_absolute():
            errors.append(f"Unsafe manifest path: {name}")
            continue
        if name in expected:
            errors.append(f"Duplicate manifest entry: {name}")
        expected[name] = digest
        if not destination.is_file():
            errors.append(f"Missing file: {name}")
        elif hashlib.sha256(destination.read_bytes()).hexdigest() != digest:
            errors.append(f"Hash mismatch: {name}")
    actual = {p.relative_to(root).as_posix() for p in root.rglob("*") if p.is_file()}
    for extra in sorted(actual - set(expected) - {"MANIFEST.sha256"}):
        errors.append(f"Unlisted file: {extra}")
    counts["files"] = len(actual)

    required = [
        "00_START_HERE.md", "01_CODEX_PROMPT.md", "02_BASELINE_AND_ARCHITECTURE.md",
        "03_MIGRATION_MATRIX.md", "04_FINDINGS_AND_REPRODUCTIONS.md", "05_PROCESS_RECOVERY.md",
        "06_CODEANALYTICS_AND_TEST_PLAN.md", "07_UI_ACCEPTANCE_JOURNEYS.md",
        "08_UPGRADE_COMPATIBILITY_AND_ROLLBACK.md", "09_ACCEPTANCE_CHECKLIST.md",
        "10_DELIVERY_REPORT_TEMPLATE.md", "PACK_REVIEW.md", "reference/SOURCE_INDEX.md",
        "reference/audit_manifest.json", "reference/test_map.csv", "templates/README.md",
        "templates/impact_request.example.json", "templates/suite_results.csv",
        "templates/scenario_results.csv", "tools/validate_pack.py",
    ]
    errors.extend(f"Required artifact missing: {name}" for name in required if name not in actual)
    review_path = root / "PACK_REVIEW.md"
    if review_path.is_file() and "Status: PREPARATION_REVIEW_PASSED" not in review_path.read_text(encoding="utf-8"):
        errors.append("Preparation-package substantive review is not marked complete.")
    allowed_extensions = {".md", ".json", ".csv", ".py", ".sha256"}
    for name in sorted(actual):
        path = root / name
        if path.suffix not in allowed_extensions:
            errors.append(f"Unexpected artifact type: {name}")
        try:
            text = path.read_text(encoding="utf-8")
        except UnicodeDecodeError:
            errors.append(f"Not UTF-8 text: {name}")
            continue
        if "\x00" in text or "\ufffd" in text:
            errors.append(f"Invalid text characters: {name}")
        if path.suffix == ".md":
            if len(re.findall(r"^```", text, flags=re.MULTILINE)) % 2:
                errors.append(f"Unbalanced code fences: {name}")
            for target in re.findall(r"\[[^\]\n]+\]\(([^\s)]+)\)", text):
                counts["markdown_links"] += 1
                parsed = urlsplit(target)
                if parsed.scheme or target.startswith("#"):
                    continue
                destination = (path.parent / unquote(parsed.path)).resolve()
                if not destination.is_relative_to(root) or not destination.exists():
                    errors.append(f"Broken/unsafe local link in {name}: {target}")
            if any(marker in text for marker in ("\ue200cite", "\ue200filecite", "turn310149view")):
                errors.append(f"Nonportable chat citation in {name}")
        if path.suffix == ".json":
            try:
                json.loads(text)
            except json.JSONDecodeError as exc:
                errors.append(f"Invalid JSON {name}: {exc}")

    try:
        audit = json.loads((root / "reference/audit_manifest.json").read_text(encoding="utf-8"))
        source_ids = {s["id"] for s in audit["sources"]}
        counts["sources"] = len(source_ids)
        if len(source_ids) != len(audit["sources"]):
            errors.append("Duplicate primary-source IDs.")
        if audit.get("audit_head_is_execution_pin") is not False:
            errors.append("Audit HEAD must not be an execution pin.")
        if audit.get("artifact_kind") != "preparation_package_not_execution_evidence":
            errors.append("Wrong artifact evidence classification.")
        for source in audit["sources"]:
            for key in ("blob_sha", "tree_sha"):
                if key in source and not re.fullmatch(r"[0-9a-f]{40}", source[key]):
                    errors.append(f"Malformed {key} for {source['id']}")
        for path in root.glob("*.md"):
            for source_id in set(re.findall(r"\b[RU]\d{2}\b", path.read_text(encoding="utf-8"))):
                if source_id not in source_ids:
                    errors.append(f"Unknown source ID in {path.name}: {source_id}")
        with (root / "reference/test_map.csv").open(encoding="utf-8", newline="") as handle:
            rows = list(csv.DictReader(handle))
        counts["traceability_rows"] = len(rows)
        seen = {row["requirement_id"] for row in rows}
        must_map = ({f"M{i:02}" for i in range(1, 13)} |
                    {f"F{i:02}" for i in range(1, 6)} |
                    {f"P{i:02}" for i in range(1, 10)} |
                    {"FINAL-WINDOWS", "FINAL-LINUX"})
        errors.extend(f"Unmapped requirement: {item}" for item in sorted(must_map - seen))
        for row in rows:
            if row["execution_status"] != "NOT_RUN":
                errors.append("Preparation test map must not imply execution.")
            for source_id in row["source_ids"].split(";"):
                if source_id not in source_ids:
                    errors.append(f"Unknown traceability source: {source_id}")
        for filename in ("suite_results.csv", "scenario_results.csv"):
            with (root / "templates" / filename).open(encoding="utf-8", newline="") as handle:
                if any(row["status"] != "NOT_RUN" for row in csv.DictReader(handle)):
                    errors.append(f"Preparation results must remain NOT_RUN: {filename}")
    except (OSError, KeyError, TypeError, ValueError) as exc:
        errors.append(f"Malformed package metadata: {exc}")
    return errors, counts


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parent.parent)
    args = parser.parse_args()
    try:
        errors, counts = validate(args.root)
    except OSError as exc:
        print(f"Package could not be read: {exc}", file=sys.stderr)
        return 1
    if errors:
        for error in errors:
            print(f"FAIL: {error}", file=sys.stderr)
        return 1
    print("PASS: preparation-package integrity and structure checks.")
    print(json.dumps(counts, sort_keys=True))
    print("This does not validate CanDoItAll code, test execution, external links or runtime behavior.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
