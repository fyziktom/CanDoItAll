#!/usr/bin/env python3
from __future__ import annotations
from pathlib import Path
import sys

bundle = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else Path.cwd().resolve()
required = [
    "README.md",
    "analysis/01-current-verdict.md",
    "analysis/02-regression-vs-previous-upload.md",
    "analysis/03-runtime-plane-gap.md",
    "analysis/04-advisory-hotspots.md",
    "requirements/01-normalized-requirements.md",
    "requirements/02-hard-gates.md",
    "plan/01-phase12-recovery-sequence.md",
    "plan/02-implementation-order.md",
    "plan/03-closure-evidence-checklist.md",
    "gates/01-stop-conditions.md",
    "gates/02-exit-criteria.md",
    "gates/03-anti-evasion-rules.md",
    "subbundles/p12-001-restore-phase10-zero-write-project-structure-read-path/README.md",
    "subbundles/p12-002-restore-phase10-unknown-manifest-shared-editor-proof/README.md",
    "subbundles/p12-003-add-operational-execution-plane-and-multi-source-automation-signals/README.md",
    "subbundles/p12-004-add-canonical-trigger-registry-and-quartz-backed-scheduler-projection/README.md",
    "subbundles/p12-005-add-durable-internal-message-plane-with-retries-and-dead-letter/README.md",
    "subbundles/p12-006-add-hosted-workers-that-drain-runtime-work/README.md",
    "subbundles/p12-007-add-plugin-ingress-inbox-cursors-deduplication-and-explicit-materialization/README.md",
    "subbundles/p12-008-add-execution-observability-policy-and-optional-mqtt-bridge/README.md",
    "inventories/02-phase10-gate-current-run.txt",
    "inventories/03-phase11-gate-current-run.txt",
    "inventories/04-phase12-gate-current-run.txt",
    "inventories/05-phase10-gate-previous-upload-run.txt",
    "inventories/06-regression-diff-vs-previous-upload.txt",
    "inventories/07-runtime-gap-search-baseline.txt",
    "reviews/01-senior-review.md",
    "reviews/02-hard-gate-review.md",
    "shared-prompts/implementation-prompt.md",
    "shared-prompts/qa-prompt.md",
    "shared-prompts/hard-gate-prompt.md",
    "traceability/01-requirement-traceability.md",
    "traceability/02-finding-to-subbundle-map.md",
    "scripts/gate_check_phase12.py",
]
missing = [item for item in required if not (bundle / item).exists()]
if missing:
    print("MISSING FILES")
    for item in missing:
        print(f"- {item}")
    sys.exit(1)
print("OK")
