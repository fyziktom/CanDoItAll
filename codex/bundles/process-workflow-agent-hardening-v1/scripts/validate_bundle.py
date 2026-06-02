#!/usr/bin/env python3
"""Structural validator for the prepared CanDoItAll hardening bundle."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

REQUIRED_ROOT = [
    "README.md",
    "inputs",
    "analysis",
    "requirements",
    "architecture",
    "plan",
    "traceability",
    "shared-prompts",
    "subbundles",
    "reviews",
]

REQUIRED_SUBBUNDLE_SECTIONS = [
    "## Status",
    "## Objective",
    "## Covered Inputs",
    "## Prerequisites",
    "## Exact Source References",
    "## Deliverables",
    "## Dependency Impact",
    "## Validation Depth",
    "## Implementation Steps",
    "## Scope Exceptions",
    "## Do Not Do",
    "## Acceptance Checklist",
    "## Proof Required",
    "## Browser Validation Logging",
    "## Progression Gate",
    "## Suggested Agent Prompt",
]

CRITICAL_SUBBUNDLES = {"SB01", "SB02", "SB03", "SB04", "SB05", "SB06", "SB08", "SB09"}


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def fail(errors: list[str], message: str) -> None:
    errors.append(message)


def validate(root: Path, stage: str) -> list[str]:
    errors: list[str] = []

    for rel in REQUIRED_ROOT:
        if not (root / rel).exists():
            fail(errors, f"Missing root item: {rel}")

    plan = root / "plan" / "01-phase-plan.md"
    if plan.exists():
        text = read_text(plan)
        for heading in ["## Subbundle Dependency Map", "## Critical Subbundles", "## Phase Gates"]:
            if heading not in text:
                fail(errors, f"Phase plan missing heading: {heading}")
        if "```mermaid" not in text:
            fail(errors, "Phase plan missing mermaid dependency map.")
    else:
        fail(errors, "Missing plan/01-phase-plan.md")

    risks = root / "analysis" / "02-assumptions-and-risks.md"
    if risks.exists():
        text = read_text(risks)
        for heading in ["## Critical Path Risks", "## Validation Risks", "## Reopen Triggers"]:
            if heading not in text:
                fail(errors, f"Risk analysis missing heading: {heading}")
    else:
        fail(errors, "Missing analysis/02-assumptions-and-risks.md")

    report = root / "reviews" / "01-execution-report.md"
    if report.exists():
        text = read_text(report)
        for heading in [
            "## Status",
            "## Subbundle Gate Results",
            "## Browser Validation Analytics",
            "## Analytics Review",
            "## Raw Note Closure",
        ]:
            if heading not in text:
                fail(errors, f"Execution report missing heading: {heading}")
    else:
        fail(errors, "Missing reviews/01-execution-report.md")

    subbundle_dir = root / "subbundles"
    readmes = sorted(subbundle_dir.glob("SB*/README.md")) if subbundle_dir.exists() else []
    if len(readmes) < 5:
        fail(errors, "Expected at least five subbundle READMEs.")

    for readme in readmes:
        text = read_text(readme)
        for heading in REQUIRED_SUBBUNDLE_SECTIONS:
            if heading not in text:
                fail(errors, f"{readme.relative_to(root)} missing section: {heading}")

        sb_id = readme.parent.name.split("-", 1)[0]
        if sb_id in CRITICAL_SUBBUNDLES:
            for phrase in [
                "Semantic Adequacy Gate",
                "shallow-pass trap",
                "adversarial negative proof",
                "semantic positive proof",
                "anti-stub audit",
                "proof/SB",
                "manifest.md",
                "semantic-invariants",
            ]:
                if phrase not in text:
                    fail(errors, f"{readme.relative_to(root)} missing critical proof phrase: {phrase}")

    scenarios = sorted((root / "templates" / "process-test-scenarios").glob("*.json"))
    if len(scenarios) != 5:
        fail(errors, f"Expected exactly five JSON process test scenarios, found {len(scenarios)}.")

    if stage == "completed":
        proof_dir = root / "proof"
        if not proof_dir.exists():
            fail(errors, "Completed stage requires proof directory.")
        for sb in sorted(CRITICAL_SUBBUNDLES):
            if not (proof_dir / sb / "manifest.md").exists():
                fail(errors, f"Completed stage missing proof/{sb}/manifest.md")
            invariant_md = proof_dir / sb / "semantic-invariants.md"
            invariant_json = proof_dir / sb / "semantic-invariants.json"
            if not invariant_md.exists() and not invariant_json.exists():
                fail(errors, f"Completed stage missing semantic invariants for {sb}")

    return errors


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--stage", choices=["prepared", "completed"], default="prepared")
    parser.add_argument("--root", default=".")
    args = parser.parse_args()

    root = Path(args.root).resolve()
    errors = validate(root, args.stage)
    if errors:
        print("FAIL")
        for error in errors:
            print(f"- {error}")
        return 1

    print(f"PASS: bundle structure validated for stage '{args.stage}' at {root}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
