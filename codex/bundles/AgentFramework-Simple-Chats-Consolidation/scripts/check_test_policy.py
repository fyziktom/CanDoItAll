#!/usr/bin/env python3
"""Validate focused and broad-gate policy."""

from __future__ import annotations

import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
manifest = json.loads((root / "manifest.json").read_text(encoding="utf-8"))
errors: list[str] = []
broad: list[str] = []

for sub in manifest["subbundles"]:
    selection = sub.get("testSelection", {})
    workspaces = selection.get("workspaces", [])
    if not workspaces:
        errors.append(f"{sub['id']} has no workspace")
    if not selection.get("impactedTestsRequired"):
        errors.append(f"{sub['id']} does not require impacted tests")
    if not selection.get("nonZeroDiscoveryRequired"):
        errors.append(f"{sub['id']} does not require non-zero discovery")
    if selection.get("unfilteredStableGate"):
        broad.append(sub["id"])
        if "tests/Solutions/CanDoItAll.Tests.Stable.slnx" not in workspaces:
            errors.append(f"{sub['id']} broad gate lacks Stable")
    elif "tests/Solutions/CanDoItAll.Tests.Stable.slnx" in workspaces:
        errors.append(f"{sub['id']} references Stable without authorization")
    if "tests/Solutions/CanDoItAll.Tests.Playwright.slnx" in workspaces and not selection.get("browserProof"):
        errors.append(f"{sub['id']} references Playwright without browser proof")

if broad != ["SB11"]:
    errors.append(f"only SB11 may own Stable, found {broad}")

if errors:
    print("\n".join(f"ERROR: {error}" for error in errors))
    raise SystemExit(1)
print("Test policy passed: focused selection everywhere, one final Stable gate, named browser phases only.")

