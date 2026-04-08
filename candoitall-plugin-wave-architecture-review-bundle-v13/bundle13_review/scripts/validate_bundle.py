#!/usr/bin/env python3
from __future__ import annotations

from pathlib import Path
import argparse
import sys


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("root", nargs="?", default=".")
    parser.add_argument("--stage", choices=["prepared", "completed"], default="prepared")
    return parser.parse_args()


def ensure_exists(root: Path, required: list[str]) -> list[str]:
    return [item for item in required if not (root / item).exists()]


def main() -> int:
    args = parse_args()
    root = Path(args.root).resolve()

    required_files = [
        "README.md",
        "analysis/01-phase13-hidden-gap-summary.md",
        "requirements/bundle13-scope.md",
        "plan/01-phase13-execution-plan.md",
        "plan/02-implementation-order.md",
        "plan/03-closure-evidence-checklist.md",
        "reviews/00-bundle-self-review.md",
        "reviews/01-execution-report.md",
        "reviews/01-detailed-current-state-review.md",
        "reviews/02-phase13-failure-map.md",
        "reviews/03-validation-method.md",
        "scripts/gate_check_phase13.py",
    ]
    subbundles = [
        "p13-001-bind-automation-runtime-options-from-production-configuration",
        "p13-002-make-runtime-idempotency-atomic-under-concurrency",
        "p13-003-add-lease-based-due-work-acquisition-and-db-side-filtering",
        "p13-004-harden-hosted-workers-with-iteration-exception-isolation",
        "p13-005-retire-the-legacy-background-job-queue-seam",
    ]

    missing = ensure_exists(root, required_files)
    for slug in subbundles:
        missing.extend(
            ensure_exists(
                root,
                [
                    f"subbundles/{slug}/README.md",
                    f"subbundles/{slug}/acceptance.md",
                    f"subbundles/{slug}/forbidden-patterns.md",
                    f"subbundles/{slug}/required-implementation-evidence.md",
                    f"subbundles/{slug}/required-tests.md",
                ]))

    if missing:
        print("Bundle validation FAILED.")
        for item in missing:
            print(f"- Missing: {item}")
        return 1

    phase_plan = (root / "plan/01-phase13-execution-plan.md").read_text(encoding="utf-8")
    for required_phrase in [
        "```mermaid",
        "critical foundation",
        "## Entry gate",
        "## Progression gate",
    ]:
        if required_phrase.lower() not in phase_plan.lower():
            print("Bundle validation FAILED.")
            print(f"- plan/01-phase13-execution-plan.md is missing required execution metadata: {required_phrase}")
            return 1

    if args.stage == "completed":
        execution_report = (root / "reviews/01-execution-report.md").read_text(encoding="utf-8")
        for required_phrase in [
            "## Subbundle Gate Results",
            "## Validation Runs",
            "## Raw Feedback Closure Audit",
            "Solved",
        ]:
            if required_phrase not in execution_report:
                print("Bundle validation FAILED.")
                print(f"- reviews/01-execution-report.md is missing completed-stage evidence marker: {required_phrase}")
                return 1

    print(f"Bundle validation OK for stage '{args.stage}'.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
