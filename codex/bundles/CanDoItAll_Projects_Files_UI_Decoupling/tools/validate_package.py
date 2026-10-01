#!/usr/bin/env python3
"""Check this handoff's metadata, links and integrity; never certify product behavior."""
from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
from pathlib import Path, PurePosixPath
import re
import sys
from urllib.parse import unquote, urlsplit

sys.dont_write_bytecode = True
OID = re.compile(r'[0-9a-f]{40}')
LINE = re.compile(r'([0-9a-f]{64})  (.+)')
LINK = re.compile(r'\[[^\]\n]*\]\(([^)\n]+)\)')

def safe_path(value: str) -> bool:
    p = PurePosixPath(value)
    return bool(value) and not p.is_absolute() and '..' not in p.parts and '\\' not in value and ':' not in value

def load_json(root: Path, name: str, errors: list[str]):
    try:
        return json.loads((root / name).read_text(encoding='utf-8'))
    except (OSError, ValueError) as exc:
        errors.append(f'Invalid JSON {name}: {type(exc).__name__}')
        return {}

def validate(root: Path) -> tuple[list[str], dict[str, int]]:
    root = root.resolve()
    errors: list[str] = []
    files: dict[str, Path] = {}
    for path in sorted(root.rglob('*')):
        if path.is_symlink():
            errors.append(f'Symlink not permitted: {path.relative_to(root)}')
        elif path.is_file():
            name = path.relative_to(root).as_posix()
            files[name] = path
            if path.suffix not in {'.md', '.json', '.py', '.sha256'}:
                errors.append(f'Unexpected artifact: {name}')
            try:
                path.read_text(encoding='utf-8')
            except (OSError, UnicodeDecodeError):
                errors.append(f'Invalid UTF-8 text: {name}')
    for name in files:
        if name.endswith('.json'):
            load_json(root, name, errors)
    metadata = load_json(root, 'bundle.json', errors)
    sources = load_json(root, 'sources.json', errors)
    evidence = load_json(root, 'templates/evidence.json', errors)
    provenance = load_json(root, 'shared-provenance.json', errors)
    if not isinstance(metadata, dict) or not isinstance(sources, dict) or not isinstance(evidence, dict) or not isinstance(provenance, dict):
        return errors + ['Invalid top-level JSON shape'], {'files':len(files), 'sources':0, 'local_links':0}
    for name in [metadata.get('entry_point'), metadata.get('source_register'), metadata.get('evidence_template'), *metadata.get('required_documents', [])]:
        if not isinstance(name, str) or name not in files:
            errors.append(f'Missing declared input: {name!r}')
    if metadata.get('review_commit_is_execution_pin') is not False:
        errors.append('Review must not be an execution pin')
    for flag in ['responsive_tuning', 'remote_writes_authorized']:
        if metadata.get(flag) is not False:
            errors.append(f'Invalid scoped flag: {flag}')
    if metadata.get('paid_live_requests_authorized') != 0:
        errors.append('This handoff authorizes no new live requests')
    if metadata.get('application_validation_by_reviewer') != 'NOT_RUN':
        errors.append('Reviewer must not claim application execution')
    for field in ['primary_viewport', 'optional_viewport']:
        viewport = metadata.get(field, {})
        if not isinstance(viewport, dict) or viewport.get('width', 0) < 1440 or viewport.get('height', 0) < 800:
            errors.append(f'Non-desktop viewport: {field}')
    rows = sources.get('sources', [])
    ids: set[str] = set()
    if not isinstance(rows, list):
        errors.append('Sources must be a list')
        rows = []
    for row in rows:
        if not isinstance(row, dict):
            errors.append('Invalid source row'); continue
        sid = row.get('id', '')
        if not re.fullmatch(r'R\d{2}', sid) or sid in ids:
            errors.append(f'Invalid/duplicate source: {sid}')
        ids.add(sid)
        ref, path = row.get('ref', ''), row.get('path', '')
        if not OID.fullmatch(ref) or not safe_path(path) or f'/blob/{ref}/{path}' not in row.get('url', ''):
            errors.append(f'Invalid pinned source: {sid}')
        if not row.get('coverage') or not row.get('role'):
            errors.append(f'Missing source coverage: {sid}')
        blob = row.get('observed_blob_sha')
        if blob is not None and not OID.fullmatch(blob):
            errors.append(f'Invalid blob SHA: {sid}')
    if metadata.get('review_commit') != sources.get('application_ref'):
        errors.append('Application review identity mismatch')
    if metadata.get('components_commit') != sources.get('components_ref'):
        errors.append('Components review identity mismatch')
    links = 0
    for name, path in files.items():
        if not name.endswith('.md'):
            continue
        text = path.read_text(encoding='utf-8')
        if not name.startswith('shared/'):
            for sid in set(re.findall(r'\bR\d{2}\b', text)):
                if sid not in ids:
                    errors.append(f'Unknown source {sid} in {name}')
        for target in LINK.findall(text):
            parts = urlsplit(target.strip().strip('<>'))
            if parts.scheme or not parts.path:
                continue
            links += 1
            dest = (path.parent / unquote(parts.path)).resolve()
            if not dest.is_relative_to(root) or not dest.exists():
                errors.append(f'Broken/escaping link in {name}: {target}')
    shared_rows = provenance.get('files', {})
    shared_actual = {n.removeprefix('shared/'):p for n,p in files.items() if n.startswith('shared/')}
    if not isinstance(shared_rows, dict) or len(shared_actual) != 22 or set(shared_actual) != set(shared_rows):
        errors.append('Shared v3 inventory mismatch')
    else:
        for name, path in shared_actual.items():
            if hashlib.sha256(path.read_bytes()).hexdigest() != shared_rows[name]:
                errors.append(f'Shared source changed: {name}')
    groups = evidence.get('groups', [])
    if not isinstance(groups, list) or [g.get('id') for g in groups] != metadata.get('product_outcome_groups'):
        errors.append('Evidence group identity mismatch')
    elif any(g.get('status') != 'NOT_RUN' or g.get('evidence') for g in groups):
        errors.append('Sealed evidence template must not contain product successes')
    if evidence.get('projects_p2_complete') is not False or evidence.get('application_tests_executed_by_package_author') is not False:
        errors.append('Evidence template must not preclaim completion')
    expected: dict[str,str] = {}
    manifest = files.get('MANIFEST.sha256')
    if manifest is None:
        errors.append('Missing root manifest')
    else:
        for line in manifest.read_text(encoding='utf-8').splitlines():
            match = LINE.fullmatch(line)
            if not match:
                errors.append('Malformed manifest line'); continue
            digest, name = match.groups()
            if not safe_path(name) or name == 'MANIFEST.sha256' or name in expected:
                errors.append(f'Unsafe/duplicate manifest entry: {name}'); continue
            expected[name] = digest
        actual = set(files) - {'MANIFEST.sha256'}
        if set(expected) != actual:
            errors.append('Manifest inventory mismatch')
        for name in actual & set(expected):
            if hashlib.sha256(files[name].read_bytes()).hexdigest() != expected[name]:
                errors.append(f'Hash mismatch: {name}')
    return errors, {'files':len(files), 'sources':len(rows), 'local_links':links, 'groups':len(groups)}

def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[1])
    args = parser.parse_args()
    try:
        errors, counts = validate(args.root)
    except (OSError, ValueError, TypeError, KeyError) as exc:
        print(f'Validation could not complete: {type(exc).__name__}', file=sys.stderr)
        return 2
    for error in errors:
        print('FAIL:', error, file=sys.stderr)
    print(json.dumps(counts, sort_keys=True))
    print('Package checks only; no product execution, external link verification or runtime certification.')
    return 1 if errors else 0

if __name__ == '__main__':
    raise SystemExit(main())
