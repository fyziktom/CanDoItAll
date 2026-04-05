#!/usr/bin/env python3
from __future__ import annotations

import sys
from pathlib import Path

REQUIRED = [
    "README.md",
    "inputs/00-original-request.md",
    "analysis/03-detailed-findings.md",
    "requirements/02-hard-gates.md",
    "architecture/01-target-solution.md",
    "plan/01-phase7-refactor-plan.md",
    "reviews/02-senior-qa-review.md",
    "scripts/gate_check_phase7.py",
    "spreadsheets/01-phase7-plugin-gate-findings.xlsx",
]

def main() -> int:
    root = Path(__file__).resolve().parents[1]
    missing = [item for item in REQUIRED if not (root / item).exists()]
    if missing:
        print("Bundle validation failed.")
        for item in missing:
            print(f"- missing: {item}")
        return 1

    subbundles = list((root / "subbundles").iterdir()) if (root / "subbundles").exists() else []
    if not subbundles:
        print("Bundle validation failed.")
        print("- missing: subbundles/*")
        return 1

    print("Bundle validation passed.")
    print(f"Subbundles: {len(subbundles)}")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
