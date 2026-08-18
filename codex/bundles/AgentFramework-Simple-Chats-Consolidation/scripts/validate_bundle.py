#!/usr/bin/env python3
"""Validate the prepared consolidation bundle."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

REQUIRED_ROOT = [
    "README.md",
    "manifest.json",
    "bundle-status.json",
    "CHECKSUMS.sha256",
    "inputs/01-user-request.md",
    "inputs/02-source-authority.md",
    "inputs/03-input-coverage.json",
    "analysis/01-review-verdict.md",
    "analysis/02-findings-register.md",
    "analysis/03-risk-register.md",
    "analysis/04-semantic-adequacy.md",
    "requirements/requirements.json",
    "plan/01-phase-plan.md",
    "plan/02-test-selection-and-invalidation.md",
    "plan/03-ui-composition-contract.md",
    "plan/architecture-checkpoints.md",
    "traceability/traceability.json",
    "reviews/01-preparation-review.md",
    "reviews/csharp-architecture-gate.md",
]

ARCHITECTURE = [
    "architecture/00-csharp-current-state-inventory.md",
    "architecture/01-csharp-boundary-map.md",
    "architecture/02-csharp-dependency-direction.md",
    "architecture/03-csharp-pattern-selection-records.md",
    "architecture/04-csharp-testability-plan.md",
]

SB_HEADINGS = [
    "## Status",
    "## Objective",
    "## Owned Requirements",
    "## Prerequisites",
    "## Current Source Anchors",
    "## Explicit Non-Goals",
    "## Implementation Steps",
    "## Acceptance Criteria",
    "## Validation Depth",
    "## Focused Test Selection",
    "## Invalidation And Broad-Gate Decision",
    "## UI Composition Contract",
    "## C# Architecture Impact",
    "## Boundary Ownership",
    "## Dependency Direction",
    "## Pattern Decision",
    "## Testability Contract",
    "## Partial Class Policy",
    "## Architecture Proof Required",
    "## Progression Gate",
    "## Reopen Triggers",
]


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--stage", default="prepared")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    errors: list[str] = []

    for rel in REQUIRED_ROOT + ARCHITECTURE:
        if not (root / rel).is_file():
            errors.append(f"missing required file: {rel}")

    manifest = json.loads((root / "manifest.json").read_text(encoding="utf-8"))
    status = json.loads((root / "bundle-status.json").read_text(encoding="utf-8"))
    requirements = json.loads((root / "requirements/requirements.json").read_text(encoding="utf-8"))["requirements"]
    findings = json.loads((root / "analysis/findings-register.json").read_text(encoding="utf-8"))["findings"]

    for label, document in (("manifest", manifest), ("status", status)):
        if document.get("stage") != args.stage:
            errors.append(f"{label} stage {document.get('stage')!r}, expected {args.stage!r}")

    if status.get("implementationPerformedDuringPreparation") is not False:
        errors.append("preparation must explicitly record no implementation")

    subs = manifest.get("subbundles", [])
    expected_ids = [f"SB{index:02d}" for index in range(1, 12)]
    actual_ids = [item.get("id") for item in subs]
    if actual_ids != expected_ids:
        errors.append(f"unexpected subbundle sequence: {actual_ids}")

    manifest_requirements = manifest.get("requirements", [])
    requirement_ids = [item.get("id") for item in requirements]
    if len(manifest_requirements) != len(set(manifest_requirements)):
        errors.append("manifest requirements must be unique")
    if manifest_requirements != requirement_ids:
        errors.append("manifest and requirements.json requirement order differ")
    finding_ids = [item.get("id") for item in findings]
    if len(finding_ids) != len(set(finding_ids)):
        errors.append("findings register IDs must be unique")
    if manifest.get("findings", []) != finding_ids:
        errors.append("manifest and findings-register.json finding order differ")

    by_sub = {item["id"]: item for item in subs}
    for sub in subs:
        matches = list((root / "subbundles").glob(f"{sub['id']}-*/README.md"))
        if len(matches) != 1:
            errors.append(f"{sub['id']} requires exactly one README, found {len(matches)}")
            continue
        content = matches[0].read_text(encoding="utf-8")
        for heading in SB_HEADINGS:
            if heading not in content:
                errors.append(f"{matches[0].relative_to(root)} missing {heading}")
        for requirement in sub.get("requirements", []):
            if requirement not in manifest_requirements:
                errors.append(f"{sub['id']} owns unknown requirement {requirement}")

    for requirement in requirements:
        owners = requirement.get("owners", [])
        if not owners:
            errors.append(f"{requirement['id']} has no owner")
        for owner in owners:
            if owner not in by_sub:
                errors.append(f"{requirement['id']} has unknown owner {owner}")
            elif requirement["id"] not in by_sub[owner].get("requirements", []):
                errors.append(f"{requirement['id']} missing from manifest owner {owner}")

    if errors:
        print("\n".join(f"ERROR: {error}" for error in errors))
        return 1
    print(f"Bundle validation passed: {len(subs)} subbundles, {len(requirements)} requirements, stage={args.stage}.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
