#!/usr/bin/env python3
from __future__ import annotations

from pathlib import Path
import sys

root = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else Path.cwd().resolve()

required = [
    "README.md",
    "analysis/03-detailed-findings.md",
    "requirements/02-hard-gates.md",
    "architecture/01-target-solution.md",
    "plan/01-phase10-refactor-plan.md",
    "scripts/gate_check_phase10.py",
    "subbundles/p10-001-load-path-still-mutates-persisted-state-via-stale-projection-retirement-and-layout-cleanup/README.md",
    "subbundles/p10-004-manifest-driven-plugin-editor-regression-proof-is-still-too-weak-for-the-next-plugin-wave/required-tests.md",
]

missing = [item for item in required if not (root / item).exists()]
if missing:
    print("Missing required bundle files:")
    for item in missing:
        print("-", item)
    sys.exit(1)

print("Bundle structure looks valid.")
