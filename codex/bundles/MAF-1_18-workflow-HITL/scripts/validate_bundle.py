#!/usr/bin/env python3
"""Validate the semantic and structural completeness of this Codex bundle."""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

REQUIRED_FILES = [
    "README.md",
    "AGENTS.md",
    "CODEX-EXECUTION-PROMPT.md",
    "STATUS.md",
    "manifest.json",
    "initiative/INITIATIVE-PROFILE.md",
    "inputs/USER-REQUEST.md",
    "inputs/REQUIREMENTS.md",
    "evidence/CURRENT-STATE.md",
    "evidence/MAF-1.18-DELTA.md",
    "evidence/SOURCE-INDEX.md",
    "architecture/ARCHITECTURE-REVIEW.md",
    "architecture/TOOL-CONCURRENCY-POLICY.md",
    "architecture/HITL-STATE-MACHINE.md",
    "architecture/API-CONTRACT.md",
    "architecture/PERSISTENCE-MODEL.md",
    "inventories/PACKAGE-AND-CODE-IMPACT-MAP.md",
    "inventories/TEST-MAP.md",
    "plan/EXECUTION-PLAN.md",
    "plan/DEPENDENCY-GRAPH.md",
    "plan/INVALIDATION-KEYS.md",
    "traceability/TRACEABILITY.md",
    "proof/VALIDATION-PLAN.md",
    "closeout/EXECUTION-REPORT.md",
    "closeout/CLOSURE-CHECKLIST.md",
]

SUBBUNDLE_HEADINGS = [
    "## Status",
    "## Outcome",
    "## Owned requirements",
    "## Non-goals",
    "## Prerequisites",
    "## Reopen triggers",
    "## Exact sources and discovery",
    "## Implementation boundary",
    "## Acceptance criteria",
    "## Proof tier",
    "## Focused validation",
    "## Invalidation keys",
    "## Broad-gate decision",
    "## Closure record",
]

FORBIDDEN_MARKERS = [
    r"\bTODO\b",
    r"\bTBD\b",
    r"\bFIXME\b",
    r"\bPLACEHOLDER\b",
]


def fail(errors: list[str], message: str) -> None:
    errors.append(message)


def read_text(path: Path, errors: list[str]) -> str:
    try:
        text = path.read_text(encoding="utf-8")
    except Exception as exc:
        fail(errors, f"Cannot read {path}: {exc}")
        return ""
    if not text.strip():
        fail(errors, f"File is empty: {path}")
    return text


def validate(root: Path) -> list[str]:
    errors: list[str] = []

    for relative in REQUIRED_FILES:
        path = root / relative
        if not path.is_file():
            fail(errors, f"Missing required file: {relative}")

    manifest_path = root / "manifest.json"
    if not manifest_path.is_file():
        return errors

    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    except Exception as exc:
        fail(errors, f"Invalid manifest.json: {exc}")
        return errors

    if manifest.get("preparedFor", {}).get("model") != "GPT-5.6":
        fail(errors, "manifest.json must target GPT-5.6.")
    if manifest.get("preparedFor", {}).get("reasoningEffort") != "xhigh":
        fail(errors, "manifest.json must target xhigh reasoning.")
    if manifest.get("framework", {}).get("targetStable") != "1.18.0":
        fail(errors, "Target stable MAF version must be 1.18.0.")
    if manifest.get("framework", {}).get("targetPreview") != "1.18.0-preview.260818.1":
        fail(errors, "Target preview MAF version must be 1.18.0-preview.260818.1.")

    subbundles = manifest.get("subbundles")
    if not isinstance(subbundles, list) or len(subbundles) != 7:
        fail(errors, "Manifest must define exactly seven subbundles.")
        subbundles = []

    ids = [item.get("id") for item in subbundles if isinstance(item, dict)]
    if ids != [f"SB{i:02d}" for i in range(7)]:
        fail(errors, f"Unexpected subbundle order: {ids}")

    for item in subbundles:
        if not isinstance(item, dict):
            fail(errors, "Subbundle manifest entry must be an object.")
            continue
        directory = item.get("directory")
        if not isinstance(directory, str):
            fail(errors, f"Subbundle {item.get('id')} has no directory.")
            continue
        readme = root / directory / "README.md"
        if not readme.is_file():
            fail(errors, f"Missing subbundle README: {directory}/README.md")
            continue
        text = read_text(readme, errors)
        for heading in SUBBUNDLE_HEADINGS:
            if heading not in text:
                fail(errors, f"{directory}/README.md is missing heading: {heading}")

    requirements = read_text(root / "inputs/REQUIREMENTS.md", errors)
    traceability = read_text(root / "traceability/TRACEABILITY.md", errors)
    for number in range(1, 46):
        requirement_id = f"RQ-{number:03d}"
        if requirement_id not in requirements:
            fail(errors, f"Requirements file is missing {requirement_id}.")
        if requirement_id not in traceability:
            fail(errors, f"Traceability file is missing {requirement_id}.")

    all_text_files = list(root.rglob("*.md")) + list(root.rglob("*.json"))
    for path in all_text_files:
        text = read_text(path, errors)
        for pattern in FORBIDDEN_MARKERS:
            if re.search(pattern, text, flags=re.IGNORECASE):
                fail(errors, f"Forbidden unfinished marker {pattern!r} in {path.relative_to(root)}")

    root_readme = read_text(root / "README.md", errors)
    for phrase in [
        "Do **not** globally enable concurrent tool invocation",
        "Do not implement workflow pause by throwing an exception",
        "Do not claim exactly-once execution for arbitrary external side effects",
        "Do not fall back to rerunning from workflow input",
    ]:
        if phrase not in root_readme:
            fail(errors, f"Root README is missing hard constraint: {phrase}")

    return errors


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("bundle_root", nargs="?", default=".")
    args = parser.parse_args()

    root = Path(args.bundle_root).resolve()
    errors = validate(root)
    if errors:
        print("Bundle validation: FAIL")
        for error in errors:
            print(f"- {error}")
        return 1

    print("Bundle validation: PASS")
    print(f"Root: {root}")
    print("Requirements: 45")
    print("Subbundles: 7")
    print("Target: MAF 1.18.0 / A2A preview 1.18.0-preview.260818.1")
    return 0


if __name__ == "__main__":
    sys.exit(main())
