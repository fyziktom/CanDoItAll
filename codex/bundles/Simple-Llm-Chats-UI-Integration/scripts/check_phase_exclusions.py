#!/usr/bin/env python3
"""Validate phase locks and explicit feature exclusions."""

from __future__ import annotations

import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
manifest = json.loads((root / 'manifest.json').read_text(encoding='utf-8'))
status = json.loads((root / 'bundle-status.json').read_text(encoding='utf-8'))
errors: list[str] = []
by_id = {sb['id']: sb for sb in manifest['subbundles']}
for sbid in ['SB01','SB02','SB03','SB04','SB05']:
    if by_id[sbid]['stage'] not in {'hardening','checkpoint'}:
        errors.append(f'{sbid} must remain pre-UI hardening/checkpoint')
for sbid in ['SB06','SB07','SB08','SB09','SB10']:
    if by_id[sbid]['stage'] not in {'ui','checkpoint'}:
        errors.append(f'{sbid} must remain main-UI work/checkpoint')
if by_id['SB11']['stage'] != 'floating':
    errors.append('SB11 must own floating integration')
completed = set(status.get('completedSubbundles', []))
simple_chat_unlocked = 'SB05' in completed
floating_integration_unlocked = 'SB10' in completed
if status.get('simpleChatUiActivationAllowed') is not simple_chat_unlocked:
    errors.append('Simple Chat UI activation must exactly follow CP1 completion')
if status.get('floatingIntegrationAllowed') is not floating_integration_unlocked:
    errors.append('floating integration activation must exactly follow CP2 completion')
exclusions = ' '.join(manifest.get('explicitExclusions', [])).lower()
for phrase in ['project structure', 'tools', 'voice', 'chatbot', 'loopback http']:
    if phrase not in exclusions:
        errors.append(f'missing explicit exclusion phrase: {phrase}')
if errors:
    print('\n'.join(f'ERROR: {e}' for e in errors))
    raise SystemExit(1)
print('Phase/exclusion validation passed: CP1 and CP2 activation matches checkpoint completion.')
