#!/usr/bin/env python3
"""Validate exact requirement owner/proof traceability."""

from __future__ import annotations

import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
requirements = json.loads((root / "requirements/requirements.json").read_text(encoding="utf-8"))["requirements"]
traceability = json.loads((root / "traceability/traceability.json").read_text(encoding="utf-8"))["traceability"]
trace_by_id = {row["requirement"]: row for row in traceability}
errors: list[str] = []

for requirement in requirements:
    requirement_id = requirement["id"]
    row = trace_by_id.get(requirement_id)
    if row is None:
        errors.append(f"missing trace row: {requirement_id}")
        continue
    if row.get("owners") != requirement.get("owners"):
        errors.append(f"owner mismatch for {requirement_id}")
    proof = row.get("proof", [])
    for owner in requirement["owners"]:
        if not any(path.startswith(f"proof/{owner}/manifest.md ") for path in proof):
            errors.append(f"{requirement_id} lacks proof target for {owner}")

extras = sorted(set(trace_by_id) - {item["id"] for item in requirements})
if extras:
    errors.append(f"extra trace rows: {extras}")

if errors:
    print("\n".join(f"ERROR: {error}" for error in errors))
    raise SystemExit(1)
print(f"Traceability passed: {len(traceability)} exact requirement rows.")

