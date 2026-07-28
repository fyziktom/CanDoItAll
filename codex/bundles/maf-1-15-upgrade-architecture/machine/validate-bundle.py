#!/usr/bin/env python3
"""Validate the internal structure of the MAF 1.15 migration bundle."""

from __future__ import annotations

import csv
import json
import sys
import xml.etree.ElementTree as ET
from collections import Counter
from pathlib import Path

REQUIRED_PATHS = {
    "README.md",
    "BUNDLE-INDEX.md",
    "00-user-summary-cs.md",
    "requirements/01-normalized-requirements.md",
    "analysis/02-impact-matrix.md",
    "analysis/06-session-approval-migration.md",
    "analysis/07-workflow-handoff-streaming.md",
    "plan/01-phase-plan.md",
    "plan/03-test-plan.md",
    "plan/04-rollout-rollback.md",
    "machine/migration-tasks.json",
    "machine/package-baseline.json",
    "reviews/01-execution-report.md",
}

EXPECTED_SUBBUNDLES = {
    "subbundles/01-baseline-discovery-and-1-13-fixtures/README.md",
    "subbundles/02-package-alignment-and-compilation/README.md",
    "subbundles/03-approval-binding-and-state-migration/README.md",
    "subbundles/04-handoff-terminal-output-and-message-ordering/README.md",
    "subbundles/05-session-and-checkpoint-compatibility/README.md",
    "subbundles/06-file-tools-and-capability-security-regression/README.md",
    "subbundles/07-a2a-hosting-and-optional-api-inventory/README.md",
    "subbundles/08-workaround-cleanup-rollout-and-closure/README.md",
}


def validate_json(path: Path) -> list[str]:
    try:
        json.loads(path.read_text(encoding="utf-8"))
    except Exception as exc:  # noqa: BLE001 - validation must report every parse failure
        return [f"{path}: invalid JSON: {exc}"]
    return []


def validate_csv(path: Path) -> list[str]:
    failures: list[str] = []
    try:
        with path.open(encoding="utf-8", newline="") as stream:
            rows = list(csv.reader(stream))
    except Exception as exc:  # noqa: BLE001 - validation must report every parse failure
        return [f"{path}: invalid CSV: {exc}"]

    if not rows:
        return [f"{path}: CSV is empty"]

    widths = Counter(len(row) for row in rows)
    if len(widths) != 1:
        failures.append(f"{path}: inconsistent CSV column counts: {dict(widths)}")

    return failures


def validate_xml(path: Path) -> list[str]:
    try:
        ET.parse(path)
    except Exception as exc:  # noqa: BLE001 - validation must report every parse failure
        return [f"{path}: invalid XML: {exc}"]
    return []


def validate_task_graph(path: Path) -> list[str]:
    failures: list[str] = []
    payload = json.loads(path.read_text(encoding="utf-8"))
    tasks = payload.get("tasks")
    if not isinstance(tasks, list) or not tasks:
        return [f"{path}: tasks must be a non-empty array"]

    ids = [task.get("id") for task in tasks]
    duplicate_ids = sorted(item for item, count in Counter(ids).items() if item and count > 1)
    if duplicate_ids:
        failures.append(f"{path}: duplicate task IDs: {', '.join(duplicate_ids)}")

    known_ids = {item for item in ids if isinstance(item, str)}
    for task in tasks:
        task_id = task.get("id", "<missing>")
        for dependency in task.get("depends_on", []):
            if dependency not in known_ids:
                failures.append(f"{path}: {task_id} depends on unknown task {dependency}")

    return failures


def main() -> int:
    root = Path(sys.argv[1] if len(sys.argv) > 1 else ".").resolve()
    failures: list[str] = []

    expected = REQUIRED_PATHS | EXPECTED_SUBBUNDLES
    for relative in sorted(expected):
        path = root / relative
        if not path.is_file():
            failures.append(f"Missing required file: {relative}")
        elif path.stat().st_size == 0:
            failures.append(f"Required file is empty: {relative}")

    for path in root.rglob("*.json"):
        failures.extend(validate_json(path))

    for path in root.rglob("*.csv"):
        failures.extend(validate_csv(path))

    for pattern in ("*.props", "*.targets", "*.xml"):
        for path in root.rglob(pattern):
            failures.extend(validate_xml(path))

    task_graph = root / "machine/migration-tasks.json"
    if task_graph.is_file():
        failures.extend(validate_task_graph(task_graph))

    for number in range(1, 9):
        proof_readme = root / f"proof/SB{number:02d}/README.md"
        if not proof_readme.is_file():
            failures.append(f"Missing proof workspace: {proof_readme.relative_to(root)}")

    if failures:
        print("Bundle validation failed:", file=sys.stderr)
        for failure in failures:
            print(f"  - {failure}", file=sys.stderr)
        return 1

    file_count = sum(1 for path in root.rglob("*") if path.is_file())
    print(f"Bundle validation passed. Files: {file_count}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
