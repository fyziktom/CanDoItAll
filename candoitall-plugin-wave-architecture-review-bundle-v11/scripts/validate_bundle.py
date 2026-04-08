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


args = parse_args()
root = Path(args.root).resolve()

required_files = [
    "README.md",
    "requirements/01-normalized-requirements.md",
    "requirements/02-hard-gates.md",
    "requirements/03-behavior-guardrail-gates.md",
    "plan/01-phase11-refactor-plan.md",
    "plan/02-implementation-sequencing.md",
    "plan/03-closure-evidence-checklist.md",
    "scripts/gate_check_phase11.py",
    "gates/01-stop-conditions.md",
    "gates/02-exit-criteria.md",
    "gates/03-anti-evasion-rules.md",
    "reviews/00-bundle-self-review.md",
    "reviews/01-execution-report.md",
    "reviews/02-senior-qa-review.md",
    "reviews/03-hard-gate-review.md",
    "traceability/01-requirement-traceability.md",
    "traceability/02-finding-to-subbundle-map.md",
]
subbundles = [
    "p11-001-operational-messages-must-not-be-canonical-workbench-nodes",
    "p11-002-add-canonical-trigger-registry-and-quartz-backed-scheduler-projection",
    "p11-003-add-durable-internal-message-bus-outbox-inbox-and-subscriptions",
    "p11-004-add-hosted-workers-that-drain-background-jobs-triggers-and-connector-outbox",
    "p11-005-add-plugin-ingress-inbox-cursors-deduplication-and-explicit-materialization",
    "p11-006-add-execution-policy-observability-and-optional-mqtt-telemetry-bridge",
]

missing = []

for rel in required_files:
    if not (root / rel).exists():
        missing.append(rel)

for slug in subbundles:
    for rel in [
        f"subbundles/{slug}/README.md",
        f"subbundles/{slug}/acceptance.md",
        f"subbundles/{slug}/forbidden-patterns.md",
        f"subbundles/{slug}/required-implementation-evidence.md",
        f"subbundles/{slug}/required-tests.md",
    ]:
        if not (root / rel).exists():
            missing.append(rel)

if missing:
    print("Bundle validation FAILED.")
    for item in missing:
        print(f"- Missing: {item}")
    sys.exit(1)

phase_plan_text = (root / "plan/01-phase11-refactor-plan.md").read_text(encoding="utf-8")
if "```mermaid" not in phase_plan_text:
    print("Bundle validation FAILED.")
    print("- plan/01-phase11-refactor-plan.md is missing a mermaid dependency map.")
    sys.exit(1)

for required_phrase in [
    "critical foundation",
    "## Entry gate",
    "## Progression gate",
]:
    if required_phrase.lower() not in phase_plan_text.lower():
        print("Bundle validation FAILED.")
        print(f"- plan/01-phase11-refactor-plan.md is missing required gate language: {required_phrase}")
        sys.exit(1)

if args.stage == "completed":
    execution_report = (root / "reviews/01-execution-report.md").read_text(encoding="utf-8")
    for required_phrase in [
        "## Browser Validation Analytics",
        "## Subbundle Gate Results",
        "Solved",
    ]:
        if required_phrase not in execution_report:
            print("Bundle validation FAILED.")
            print(f"- reviews/01-execution-report.md is missing completed-stage evidence marker: {required_phrase}")
            sys.exit(1)

print(f"Bundle validation OK for stage '{args.stage}'.")
