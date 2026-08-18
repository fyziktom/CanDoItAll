#!/usr/bin/env python3
"""Check phase dependency order and forbidden final-gate placement."""

from __future__ import annotations

import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
manifest = json.loads((root / "manifest.json").read_text(encoding="utf-8"))
subbundles = manifest["subbundles"]
index = {item["id"]: position for position, item in enumerate(subbundles)}
errors: list[str] = []

for sub in subbundles:
    for prerequisite in sub.get("prerequisites", []):
        if prerequisite not in index:
            errors.append(f"{sub['id']} has unknown prerequisite {prerequisite}")
        elif index[prerequisite] >= index[sub["id"]]:
            errors.append(f"{sub['id']} prerequisite {prerequisite} is not earlier")

if subbundles[-1]["id"] != "SB11" or subbundles[-1]["stage"] != "final":
    errors.append("SB11 must be the final subbundle")
if any(item["stage"] == "final" for item in subbundles[:-1]):
    errors.append("only SB11 may have final stage")

if errors:
    print("\n".join(f"ERROR: {error}" for error in errors))
    raise SystemExit(1)
print("Phase exclusions passed: prerequisites are ordered and final work is isolated.")

