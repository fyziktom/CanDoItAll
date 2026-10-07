#!/usr/bin/env python3
"""Validate handoff structure, links and hashes; never validate or modify product code."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
from urllib.parse import unquote, urlsplit

LINK = re.compile(r'\[[^\]\n]*\]\(([^)\n]+)\)')
OID = re.compile(r'[0-9a-f]{40}')


def validate(root: Path) -> dict[str, int]:
    root = root.resolve()
    files = {p.relative_to(root).as_posix(): p for p in root.rglob('*') if p.is_file()}
    if any(p.is_symlink() for p in root.rglob('*')):
        raise ValueError('Symlinks are not permitted in this portable handoff.')
    parsed = {}
    links = 0
    json_count = 0
    for name, path in files.items():
        if path.suffix not in {'.md', '.json', '.py', '.sha256'}:
            raise ValueError(f'Unexpected file type: {name}')
        text = path.read_text(encoding='utf-8')
        if path.suffix == '.json':
            parsed[name] = json.loads(text)
            json_count += 1
        if path.suffix == '.md':
            for match in LINK.finditer(text):
                target = match.group(1).strip().strip('<>')
                parts = urlsplit(target)
                if parts.scheme or target.startswith('#'):
                    continue
                destination = (path.parent / unquote(parts.path)).resolve()
                destination.relative_to(root)
                if not destination.exists():
                    raise ValueError(f'Broken local link in {name}: {target}')
                links += 1
    bundle = parsed['bundle.json']
    register = parsed['sources.json']
    if bundle['review_commit_is_execution_pin'] is not False:
        raise ValueError('The review commit must not be an execution pin.')
    if bundle['review_commit'] != register['review_commit'] or not OID.fullmatch(register['review_commit']):
        raise ValueError('Invalid or inconsistent review commit.')
    if bundle['executable'] is not True:
        raise ValueError('This module assignment must be marked executable.')
    for field in ['entry_point', 'source_register', 'shared_foundation']:
        if bundle[field] not in files:
            raise ValueError(f'Missing metadata path: {field}')
    for name in bundle['documents']:
        if name not in files:
            raise ValueError(f'Missing declared document: {name}')
    ids = set()
    repository_paths = set()
    for source in register['sources']:
        sid = source['id']
        if sid in ids:
            raise ValueError(f'Duplicate source ID: {sid}')
        ids.add(sid)
        if not source.get('coverage'):
            raise ValueError(f'Missing coverage: {sid}')
        if source['kind'].startswith('repository-'):
            if source['path'] in repository_paths:
                raise ValueError(f'Duplicate repository source path: {sid}')
            repository_paths.add(source['path'])
            pp = PurePosixPath(source['path'])
            if pp.is_absolute() or '..' in pp.parts or '\\' in source['path']:
                raise ValueError(f'Unsafe repository path: {sid}')
            if source['review_commit'] != bundle['review_commit'] or f"/{bundle['review_commit']}/" not in source['url']:
                raise ValueError(f'Unpinned repository source: {sid}')
            for field in ['observed_blob_sha', 'observed_tree_sha']:
                if field in source and not OID.fullmatch(source[field]):
                    raise ValueError(f'Invalid object hash: {sid}')
    for path in root.glob('*.md'):
        used = set(re.findall(r'\b(?:EV|PL|SC|ME|RS|WS)\d{2}\b', path.read_text(encoding='utf-8')))
        if used - ids:
            raise ValueError(f'Unknown source IDs in {path.name}: {sorted(used - ids)}')
    expected = {}
    for line in files['MANIFEST.sha256'].read_text(encoding='utf-8').splitlines():
        match = re.fullmatch(r'([0-9a-f]{64})  (.+)', line)
        if match is None:
            raise ValueError('Invalid manifest line.')
        digest, name = match.groups()
        pp = PurePosixPath(name)
        if pp.is_absolute() or '..' in pp.parts or '\\' in name or name in expected or name == 'MANIFEST.sha256':
            raise ValueError(f'Unsafe, duplicate or self-referential manifest path: {name}')
        expected[name] = digest
    if set(expected) != set(files) - {'MANIFEST.sha256'}:
        raise ValueError('Manifest inventory does not match the package.')
    for name, digest in expected.items():
        if hashlib.sha256(files[name].read_bytes()).hexdigest() != digest:
            raise ValueError(f'Hash mismatch: {name}')
    return {'files': len(files), 'local_links': links, 'json_files': json_count,
            'sources': len(ids), 'manifest_entries': len(expected)}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[1])
    args = parser.parse_args()
    try:
        result = validate(args.root)
    except (OSError, ValueError, KeyError, TypeError) as error:
        print(f'FAIL: {error}')
        return 1
    print('PASS: ' + json.dumps(result, sort_keys=True))
    print('External URLs, source authenticity, Markdown anchors and product behavior are not certified by this utility.')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
