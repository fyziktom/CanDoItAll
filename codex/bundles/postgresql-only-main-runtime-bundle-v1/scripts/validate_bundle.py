#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]

required = [
    "README.md",
    "COPY_PASTE_PROMPT_FOR_CODEX.md",
    "inputs/raw-request.md",
    "inputs/source-observations.md",
    "analysis/01-current-state.md",
    "analysis/02-assumptions-and-risks.md",
    "analysis/03-dependency-map.md",
    "requirements/01-normalized-requirements.md",
    "requirements/02-out-of-scope.md",
    "architecture/01-target-persistence-architecture.md",
    "architecture/02-postgresql-runtime-primitives.md",
    "plan/01-phase-plan.md",
    "inventories/sqlite-removal-inventory.md",
    "traceability/input-coverage-matrix.md",
    "traceability/source-to-subbundle-matrix.md",
    "shared-prompts/implementation-prompt.md",
    "shared-prompts/qa-prompt.md",
    "shared-prompts/postgresql-migration-consolidation-prompt.md",
    "reviews/01-execution-report.md",
    "reviews/02-preparation-self-review.md",
]

for index in range(1, 10):
    sb = f"SB{index:02d}"
    required.extend([
        f"subbundles/{sb}/README.md",
        f"subbundles/{sb}/checklist.md",
        f"proof/{sb}/manifest.md",
        f"proof/{sb}/semantic-invariants.md",
    ])

missing = [path for path in required if not (ROOT / path).exists()]
if missing:
    print("Missing required files:")
    for path in missing:
        print(f" - {path}")
    sys.exit(1)

print("Bundle structure validation passed.")
