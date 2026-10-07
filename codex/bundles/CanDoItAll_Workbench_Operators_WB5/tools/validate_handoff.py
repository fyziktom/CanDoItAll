#!/usr/bin/env python3
"""Check this handoff's integrity and evidence shape, never application correctness."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import sys
from urllib.parse import unquote, urlsplit

sys.dont_write_bytecode = True
LINK = re.compile(r'\[[^\]\n]+\]\(([^)\n]+)\)')
REFERENCE = re.compile(r'\b[SF]\d{2}\b')
SHA256 = re.compile(r'^[0-9a-f]{64}$')
OID = re.compile(r'^[0-9a-f]{40}$')
STATES = {'NOT_RUN', 'PASS', 'QUALIFIED', 'FAIL', 'BLOCKED'}
BUNDLE_ID = 'CDA-WORKBENCH-OPERATORS-WB5'


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def evidence_errors(value: object, group_ids: list[str], complete: bool = False) -> list[str]:
    errors: list[str] = []
    if not isinstance(value, dict):
        return ['Evidence must be an object.']
    if value.get('bundle_id') != BUNDLE_ID:
        errors.append('Evidence belongs to another bundle.')
    groups = value.get('groups')
    if not isinstance(groups, list) or any(not isinstance(group, dict) for group in groups):
        return errors + ['Invalid evidence groups.']
    if [group.get('id') for group in groups] != group_ids:
        errors.append('Evidence group identities/order differ from the contract.')
    attempts = value.get('attempts', [])
    if not isinstance(attempts, list) or any(not isinstance(item, dict) for item in attempts):
        return errors + ['Invalid attempts collection.']
    attempt_ids = [item.get('id') for item in attempts]
    if any(not isinstance(key, str) or not key for key in attempt_ids) or len(set(attempt_ids)) != len(attempt_ids):
        errors.append('Attempt identifiers must be unique nonempty strings.')
    if value.get('evidence_is_template') is False:
        pair = value.get('source_pair', {})
        for key in ('main', 'components', 'filetools'):
            if not isinstance(pair, dict) or not OID.fullmatch(str(pair.get(key, ''))):
                errors.append(f'Working evidence needs an actual {key} SHA.')
    for group in groups:
        gid = group.get('id')
        status = group.get('status')
        if status not in STATES:
            errors.append(f'{gid}: unknown status.')
        refs = group.get('attempt_ids', [])
        if not isinstance(refs, list) or any(ref not in attempt_ids for ref in refs):
            errors.append(f'{gid}: unknown attempt reference.')
        if status in {'PASS', 'QUALIFIED'} and not refs:
            errors.append(f'{gid}: completed claim has no recorded attempt.')
        if status == 'QUALIFIED' and not group.get('qualification'):
            errors.append(f'{gid}: qualification must be explicit.')
        if complete and (status not in {'PASS', 'QUALIFIED'} or group.get('remaining_product_blocker')):
            errors.append(f'{gid}: unresolved group.')
    if value.get('evidence_is_template') is True:
        if attempts or value.get('runtime_tests_executed') is not False or value.get('ready_for_next_slice') is not False:
            errors.append('Sealed template contains executed/ready claims.')
        if any(group.get('status') != 'NOT_RUN' for group in groups):
            errors.append('Sealed template contains completed product results.')
    if complete and (value.get('evidence_is_template') is not False or value.get('runtime_tests_executed') is not True):
        errors.append('Completion requires a working runtime evidence record, not the template.')
    return errors


def validate(root: Path) -> tuple[list[str], dict[str, int]]:
    root = root.resolve()
    errors: list[str] = []
    files: dict[str, Path] = {}
    texts: dict[str, str] = {}
    objects: dict[str, object] = {}
    for path in sorted(root.rglob('*')):
        name = path.relative_to(root).as_posix()
        if path.is_symlink():
            errors.append(f'Symlink is not allowed: {name}')
            continue
        if not path.is_file():
            continue
        files[name] = path
        if path.suffix not in {'.md', '.json', '.py', '.sha256'}:
            errors.append(f'Unexpected file type: {name}')
        try:
            texts[name] = path.read_text(encoding='utf-8')
            if path.suffix == '.json':
                objects[name] = json.loads(texts[name])
        except (OSError, UnicodeError, ValueError) as exc:
            errors.append(f'Invalid text/JSON {name}: {exc}')
    bundle = objects.get('bundle.json')
    if not isinstance(bundle, dict):
        return errors + ['Invalid bundle.json.'], {'files': len(files), 'links': 0, 'sources': 0}
    if bundle.get('id') != BUNDLE_ID or bundle.get('entry_point') not in files:
        errors.append('Bundle identity or entry point invalid.')
    if bundle.get('review_commit_is_execution_pin') is not False or bundle.get('product_tests_executed_by_reviewer') is not False:
        errors.append('Review execution/checkout provenance is not explicit.')
    for key in ('review_main_commit', 'review_components_commit'):
        if not OID.fullmatch(str(bundle.get(key, ''))):
            errors.append(f'Invalid {key}.')
    register = objects.get('sources.json', {})
    records = register.get('sources', []) if isinstance(register, dict) else []
    source_ids: set[str] = set()
    for item in records:
        if not isinstance(item, dict):
            errors.append('Source must be an object.')
            continue
        sid = item.get('id')
        if not isinstance(sid, str) or not re.fullmatch('[SF][0-9]{2}', sid) or sid in source_ids:
            errors.append(f'Invalid/duplicate source ID: {sid}')
        source_ids.add(str(sid))
        if not item.get('coverage') or not item.get('url'):
            errors.append(f'{sid}: missing coverage or URL.')
        if item.get('kind') == 'repository-file':
            ref = item.get('review_commit')
            blob = item.get('observed_blob_sha')
            if not OID.fullmatch(str(ref)) or f'/blob/{ref}/' not in item.get('url', ''):
                errors.append(f'{sid}: invalid reviewed source reference.')
            if blob is not None and not OID.fullmatch(str(blob)):
                errors.append(f'{sid}: invalid observed blob.')
    local_links = 0
    for name, text in texts.items():
        if not name.endswith('.md'):
            continue
        if not name.startswith('shared/'):
            for sid in REFERENCE.findall(text):
                if sid not in source_ids:
                    errors.append(f'{name}: unknown source {sid}.')
        for target in LINK.findall(text):
            target = target.strip().strip('<>')
            parsed = urlsplit(target)
            if parsed.scheme or target.startswith('#'):
                continue
            local_links += 1
            destination = (files[name].parent / unquote(parsed.path)).resolve()
            if not destination.is_relative_to(root) or not destination.exists():
                errors.append(f'{name}: broken/escaping link {target}.')
    seal: dict[str, str] = {}
    for line in texts.get('MANIFEST.sha256', '').splitlines():
        parts = line.split('  ', 1)
        if len(parts) != 2 or not SHA256.fullmatch(parts[0]):
            errors.append('Invalid manifest line.')
            continue
        checksum, name = parts
        pp = PurePosixPath(name)
        if pp.is_absolute() or '..' in pp.parts or '\\' in name or name == 'MANIFEST.sha256' or name in seal:
            errors.append(f'Unsafe/duplicate manifest path: {name}')
            continue
        seal[name] = checksum
    if set(seal) != set(files) - {'MANIFEST.sha256'}:
        errors.append('Manifest inventory does not match package files.')
    for name in set(seal) & set(files):
        if digest(files[name]) != seal[name]:
            errors.append(f'Hash mismatch: {name}')
    provenance = objects.get('shared-provenance.json', {})
    shared = provenance.get('files', {}) if isinstance(provenance, dict) else {}
    actual_shared = {name: digest(path) for name, path in files.items() if name.startswith('shared/')}
    if actual_shared != shared:
        errors.append('Shared v3 differs from its recorded input hashes.')
    group_ids = bundle.get('validation_groups', [])
    if len(group_ids) != 32 or group_ids != [f'G{i:02}' for i in range(32)]:
        errors.append('Invalid validation group contract.')
    errors.extend(evidence_errors(objects.get('templates/evidence.json'), group_ids))
    return errors, {'files': len(files), 'links': local_links, 'sources': len(records), 'groups': len(group_ids), 'shared_files': len(actual_shared)}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument('--evidence', type=Path)
    parser.add_argument('--require-complete', action='store_true')
    args = parser.parse_args()
    try:
        errors, metrics = validate(args.root)
        if args.require_complete and args.evidence is None:
            errors.append('--require-complete requires a separate working --evidence file.')
        if args.evidence is not None:
            actual = json.loads(args.evidence.read_text(encoding='utf-8'))
            contract = json.loads((args.root / 'bundle.json').read_text(encoding='utf-8'))
            errors.extend(evidence_errors(actual, contract['validation_groups'], args.require_complete))
    except (OSError, ValueError, TypeError) as exc:
        print(f'Validation could not run: {exc}', file=sys.stderr)
        return 2
    print(json.dumps({'status': 'FAIL' if errors else 'PASS', 'metrics': metrics, 'errors': errors}, indent=2))
    print('Integrity and evidence shape only; no application, native receipt or performance certification.')
    return 1 if errors else 0


if __name__ == '__main__':
    raise SystemExit(main())
