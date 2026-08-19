#!/usr/bin/env python3
"""Check requirement and finding ownership/closure paths."""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path


REQUIREMENT_PATTERN = re.compile(r"\bRQ-\d{3}\b")
FINDING_PATTERN = re.compile(r"\bF-\d{3}\b")


def load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--bundle-root", type=Path, default=Path("."))
    args = parser.parse_args()
    root = args.bundle_root.resolve()
    errors: list[str] = []

    index = load_json(root / "bundle-index.json")
    requirements = {item["id"] for item in index["requirements"]}
    owners: dict[str, set[str]] = {item: set() for item in requirements}

    for subbundle in index["subbundles"]:
        sb_id = subbundle["id"]
        proof_path = root / subbundle["path"] / "proof-manifest.json"
        if not proof_path.is_file():
            errors.append(f"Missing proof manifest: {proof_path}")
            continue
        proof = load_json(proof_path)
        for requirement in proof.get("requirements", []):
            if requirement not in requirements:
                errors.append(f"{sb_id} owns unknown requirement {requirement}")
                continue
            owners[requirement].add(sb_id)

    for requirement, requirement_owners in owners.items():
        if not requirement_owners:
            errors.append(f"{requirement} has no proof owner")

    matrix_text = (root / "traceability/requirements-matrix.md").read_text(
        encoding="utf-8"
    )
    matrix_ids = set(REQUIREMENT_PATTERN.findall(matrix_text))
    if matrix_ids != requirements:
        errors.append(
            "Requirement matrix IDs differ: "
            f"missing={sorted(requirements - matrix_ids)}, "
            f"extra={sorted(matrix_ids - requirements)}"
        )

    findings_json = load_json(root / "analysis/findings-register.json")
    finding_ids = {item["id"] for item in findings_json}
    closure_text = (root / "traceability/finding-closure.md").read_text(
        encoding="utf-8"
    )
    closure_ids = set(FINDING_PATTERN.findall(closure_text))
    if closure_ids != finding_ids:
        errors.append(
            "Finding closure IDs differ: "
            f"missing={sorted(finding_ids - closure_ids)}, "
            f"extra={sorted(closure_ids - finding_ids)}"
        )

    valid_subbundles = {item["id"] for item in index["subbundles"]}
    for finding in findings_json:
        if finding["owner"] not in valid_subbundles:
            errors.append(
                f"{finding['id']} has invalid owner {finding['owner']}"
            )

    if errors:
        print("\n".join(f"ERROR: {error}" for error in errors), file=sys.stderr)
        return 1

    print(
        f"Traceability passed: {len(requirements)} requirements and "
        f"{len(finding_ids)} findings."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
