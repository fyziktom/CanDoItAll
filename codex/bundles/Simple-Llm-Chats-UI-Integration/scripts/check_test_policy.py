#!/usr/bin/env python3
"""Validate focused-test and broad-gate policy encoded in the manifest."""

from __future__ import annotations

import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
manifest = json.loads((root / 'manifest.json').read_text(encoding='utf-8'))
errors: list[str] = []
broad = []
for sb in manifest['subbundles']:
    test = sb.get('testSelection', {})
    if not test.get('workspaces'):
        errors.append(f"{sb['id']} has no test/check workspace")
    if not test.get('nonZeroDiscoveryRequired'):
        errors.append(f"{sb['id']} does not require non-zero discovery")
    if test.get('unfilteredStableGate'):
        broad.append(sb['id'])
        if 'tests/Solutions/CanDoItAll.Tests.Stable.slnx' not in test.get('workspaces', []):
            errors.append(f"{sb['id']} broad gate lacks Stable workspace")
    elif 'tests/Solutions/CanDoItAll.Tests.Stable.slnx' in test.get('workspaces', []):
        errors.append(f"{sb['id']} references Stable without authorization")
if broad != ['SB12']:
    errors.append(f'unfiltered Stable gate must be SB12 only, found {broad}')
if errors:
    print('\n'.join(f'ERROR: {e}' for e in errors))
    raise SystemExit(1)
print('Test-policy validation passed: impacted selection required, one final Stable gate.')
