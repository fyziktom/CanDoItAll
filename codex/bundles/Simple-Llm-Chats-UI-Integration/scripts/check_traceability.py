#!/usr/bin/env python3
"""Ensure every requirement has an owner and proof target."""

from __future__ import annotations

import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
manifest = json.loads((root / 'manifest.json').read_text(encoding='utf-8'))
trace = json.loads((root / 'traceability/traceability.json').read_text(encoding='utf-8'))['traceability']
by_id = {row['requirement']: row for row in trace}
errors: list[str] = []
for rid in manifest['requirements']:
    row = by_id.get(rid)
    if row is None:
        errors.append(f'missing trace row: {rid}')
        continue
    if not row.get('owners'):
        errors.append(f'unowned requirement: {rid}')
    if not row.get('proof'):
        errors.append(f'missing proof target: {rid}')
extra = set(by_id) - set(manifest['requirements'])
if extra:
    errors.append(f'extra trace requirements: {sorted(extra)}')
if errors:
    print('\n'.join(f'ERROR: {e}' for e in errors))
    raise SystemExit(1)
print(f"Traceability passed: {len(by_id)} requirements.")
