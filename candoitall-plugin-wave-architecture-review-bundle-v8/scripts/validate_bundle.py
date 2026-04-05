#!/usr/bin/env python3
from __future__ import annotations
from pathlib import Path
import sys

root = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else Path.cwd().resolve()
required_files = [
    "README.md",
    "analysis/03-detailed-findings.md",
    "requirements/02-hard-gates.md",
    "architecture/01-target-solution.md",
    "plan/01-phase8-refactor-plan.md",
    "shared-prompts/implementation-prompt.md",
    "scripts/gate_check_phase8.py",
    "spreadsheets/01-phase8-plugin-gate-findings.xlsx",
]
missing = [item for item in required_files if not (root / item).exists()]
subbundles_dir = root / "subbundles"
subbundle_missing = []
if subbundles_dir.exists():
    for subbundle in sorted(item for item in subbundles_dir.iterdir() if item.is_dir()):
        for name in ["README.md", "acceptance.md", "forbidden-patterns.md", "required-tests.md", "required-implementation-evidence.md"]:
            if not (subbundle / name).exists():
                subbundle_missing.append(f"{subbundle.name}/{name}")
else:
    missing.append("subbundles/")

if missing or subbundle_missing:
    print("BUNDLE VALIDATION: FAIL")
    for item in missing:
        print(f"- missing: {item}")
    for item in subbundle_missing:
        print(f"- missing subbundle file: {item}")
    sys.exit(1)

print("BUNDLE VALIDATION: PASS")
