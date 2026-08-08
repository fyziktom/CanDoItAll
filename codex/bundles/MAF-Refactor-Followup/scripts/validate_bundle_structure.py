#!/usr/bin/env python3
"""Validate the follow-up bundle structure and local Markdown links."""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REQUIRED_ROOT = [
    '00-READ-ME-FIRST.md',
    '01-REVIEW-VERDICT.md',
    '02-FINDINGS-REGISTER.md',
    '03-EXECUTION-ORDER.md',
    'manifest.json',
]

errors: list[str] = []
for rel in REQUIRED_ROOT:
    if not (ROOT / rel).is_file():
        errors.append(f'Missing required file: {rel}')

manifest_path = ROOT / 'manifest.json'
if manifest_path.is_file():
    try:
        manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    except Exception as exc:  # noqa: BLE001
        errors.append(f'Invalid manifest.json: {exc}')
        manifest = {}
    for sb in manifest.get('subbundles', []):
        directory = ROOT / 'subbundles' / f"{sb['id']}-{sb['slug']}"
        for name in ['README.md', 'CLAUDE-CODE-PROMPT.md', 'CODEX-PROMPT.md', 'proof-manifest.template.json']:
            if not (directory / name).is_file():
                errors.append(f'Missing {directory.relative_to(ROOT) / name}')

link_pattern = re.compile(r'\[[^\]]+\]\((?!https?://|mailto:|#)([^)]+)\)')
for md in ROOT.rglob('*.md'):
    text = md.read_text(encoding='utf-8')
    if text.count('```') % 2:
        errors.append(f'Unbalanced code fences: {md.relative_to(ROOT)}')
    for raw in link_pattern.findall(text):
        target = raw.split('#', 1)[0].strip()
        if not target:
            continue
        if not (md.parent / target).resolve().exists():
            errors.append(f'Broken local link in {md.relative_to(ROOT)}: {raw}')

if errors:
    print('\n'.join(errors))
    sys.exit(1)
print(f'Bundle structure OK: {ROOT}')
