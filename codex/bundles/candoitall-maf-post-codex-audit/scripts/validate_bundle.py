#!/usr/bin/env python3
from __future__ import annotations
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
REQUIRED = [
    'README.md',
    'plan/01-phase-plan.md',
    'audit/post-codex-maf-stabilization-audit.md',
    'audit/evidence-map.md',
    'requirements/requirements.md',
    'traceability/01-requirements-traceability.md',
    'shared-prompts/codex-master-prompt.md',
    'shared-prompts/codex-qa-prompt.md',
    'reviews/readiness-gate.md',
    'reviews/01-execution-report.md',
    'subbundles/01-required-finalizer-mode/README.md',
    'subbundles/02-transcript-finalized-output-consistency/README.md',
    'subbundles/03-output-repair-retry/README.md',
    'subbundles/04-provider-capability-and-approval-alignment/README.md',
    'subbundles/05-tool-policy-require-approval-enforcement/README.md',
    'subbundles/06-validator-null-safety-and-contract-registry/README.md',
    'subbundles/07-critical-contract-finalizers/README.md',
    'subbundles/08-observability-proof-and-release-gate/README.md',
    'subbundles/09-domain-recovery-guidance/README.md',
]

missing = [p for p in REQUIRED if not (ROOT / p).exists()]
if missing:
    print('Missing required files:')
    for item in missing:
        print(f'- {item}')
    sys.exit(1)

empty = [p for p in REQUIRED if (ROOT / p).stat().st_size == 0]
if empty:
    print('Empty required files:')
    for item in empty:
        print(f'- {item}')
    sys.exit(1)

print(f'Bundle OK: {len(REQUIRED)} required files present.')
