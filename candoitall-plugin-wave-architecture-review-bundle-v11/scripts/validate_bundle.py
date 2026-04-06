#!/usr/bin/env python3
from __future__ import annotations

from pathlib import Path
import sys

root = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else Path.cwd().resolve()

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

print("Bundle validation OK.")
