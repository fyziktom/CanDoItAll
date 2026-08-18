#!/usr/bin/env python3
"""Validate the prepared bundle's semantic structure."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

REQUIRED_ROOT = [
    'README.md', 'manifest.json', 'bundle-status.json', 'CHECKSUMS.sha256',
    'inputs/01-user-request.md', 'analysis/01-review-verdict.md',
    'requirements/requirements.json', 'plan/01-phase-plan.md',
    'traceability/traceability.json', 'reviews/01-preparation-review.md',
]
REQUIRED_SB_HEADINGS = [
    '## Status', '## Objective', '## Owned Requirements', '## Prerequisites',
    '## Current Source Anchors', '## Explicit Non-Goals', '## Implementation Steps',
    '## Acceptance Criteria', '## Validation Depth', '## Focused Test Selection',
    '## Invalidation And Broad-Gate Decision', '## UI Composition Contract',
    '## C# Architecture Impact', '## Boundary Ownership', '## Dependency Direction',
    '## Pattern Decision', '## Testability Contract', '## Partial Class Policy',
    '## Architecture Proof Required', '## Progression Gate', '## Reopen Triggers',
]


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument('--stage', default='prepared')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    errors: list[str] = []
    for rel in REQUIRED_ROOT:
        if not (root / rel).is_file():
            errors.append(f'missing required file: {rel}')
    manifest = json.loads((root / 'manifest.json').read_text(encoding='utf-8'))
    status = json.loads((root / 'bundle-status.json').read_text(encoding='utf-8'))
    if manifest.get('stage') != args.stage:
        errors.append(f"manifest stage is {manifest.get('stage')!r}, expected {args.stage!r}")
    if status.get('stage') != args.stage:
        errors.append(f"status stage is {status.get('stage')!r}, expected {args.stage!r}")
    subs = manifest.get('subbundles', [])
    if len(subs) != 12:
        errors.append(f'expected 12 subbundles, found {len(subs)}')
    ids = [s.get('id') for s in subs]
    if ids != [f'SB{i:02d}' for i in range(1, 13)]:
        errors.append(f'unexpected subbundle order: {ids}')
    for sb in subs:
        matches = list((root / 'subbundles').glob(f"{sb['id']}-*/README.md"))
        if len(matches) != 1:
            errors.append(f"{sb['id']} requires exactly one README, found {len(matches)}")
            continue
        text = matches[0].read_text(encoding='utf-8')
        for heading in REQUIRED_SB_HEADINGS:
            if heading not in text:
                errors.append(f"{matches[0].relative_to(root)} missing heading {heading}")
    reqs = manifest.get('requirements', [])
    if len(reqs) != 64 or len(set(reqs)) != 64:
        errors.append(f'expected 64 unique requirements, found {len(reqs)}')
    if len(manifest.get('findings', [])) != 17:
        errors.append('expected 17 findings')
    if errors:
        print('\n'.join(f'ERROR: {e}' for e in errors))
        return 1
    print(f"Bundle validation passed: {len(subs)} subbundles, {len(reqs)} requirements, stage={args.stage}.")
    return 0

if __name__ == '__main__':
    raise SystemExit(main())
