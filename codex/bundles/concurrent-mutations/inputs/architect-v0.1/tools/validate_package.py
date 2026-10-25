#!/usr/bin/env python3
"""Validate architecture-package integrity, local links and registry references.

This does not build, test, inspect or modify CanDoItAll application code.
"""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path
import re
import sys
from urllib.parse import unquote, urlsplit


def package_files(root: Path) -> list[Path]:
    return sorted(p for p in root.rglob('*') if p.is_file()
                  and '__pycache__' not in p.parts and p.suffix != '.pyc'
                  and p.name != '.DS_Store')


def validate(root: Path, check_manifest: bool = True) -> dict:
    root = root.resolve()
    errors: list[str] = []
    required = ['package.json', 'README.md', 'INDEX.md', 'CHANGELOG.md']
    for relative in required:
        if not (root / relative).is_file():
            errors.append(f'Missing required file: {relative}')
    registries: dict[str, list[dict]] = {}
    names = ['sources', 'invariants', 'phases', 'decisions', 'mutation_inventory',
             'performance_scenarios', 'test_scenarios', 'risks', 'findings',
             'traceability', 'open_questions']
    for name in names:
        try:
            value = json.loads((root / 'registers' / f'{name}.json').read_text(encoding='utf-8'))
            if not isinstance(value, list) or any(not isinstance(row, dict) for row in value):
                raise ValueError('Expected a list of objects')
            registries[name] = value
        except (OSError, ValueError) as exc:
            errors.append(f'Registry {name}: {exc}')
            registries[name] = []
    ids: dict[str, set[str]] = {}
    for name, rows in registries.items():
        if name == 'traceability':
            continue
        row_ids = [row.get('id') for row in rows]
        if any(not isinstance(ident, str) or not ident for ident in row_ids):
            errors.append(f'Missing/invalid ID in {name}')
        valid_ids = [ident for ident in row_ids if isinstance(ident, str)]
        if len(valid_ids) != len(set(valid_ids)):
            errors.append(f'Duplicate IDs in {name}')
        ids[name] = set(valid_ids)

    def refs(rows: list[dict], field: str, target: str) -> None:
        for row in rows:
            values = row.get(field, [])
            if isinstance(values, str):
                values = [values]
            if not isinstance(values, list):
                errors.append(f'Invalid references: {row.get("id", "trace")}.{field}')
                continue
            for value in values:
                if value not in ids.get(target, set()):
                    errors.append(f'Unknown {target} reference: {row.get("id", "trace")}.{field}={value}')

    for name in ['decisions', 'test_scenarios']:
        refs(registries[name], 'invariants', 'invariants')
    for name in ['decisions', 'mutation_inventory', 'risks']:
        refs(registries[name], 'sources', 'sources')
    for name in ['mutation_inventory', 'risks', 'findings', 'test_scenarios']:
        refs(registries[name], 'phase', 'phases')
    for name in ['decisions', 'open_questions']:
        refs(registries[name], 'decision_gate', 'phases')
    for name in ['risks', 'findings', 'traceability']:
        refs(registries[name], 'tests', 'test_scenarios')
    refs(registries['findings'], 'invariant', 'invariants')
    refs(registries['findings'], 'decision', 'decisions')
    refs(registries['findings'], 'source', 'sources')
    refs(registries['phases'], 'depends_on', 'phases')
    refs(registries['phases'], 'performance', 'performance_scenarios')
    refs(registries['traceability'], 'invariant', 'invariants')
    refs(registries['traceability'], 'decisions', 'decisions')
    refs(registries['traceability'], 'findings', 'findings')
    traced = [r.get('invariant') for r in registries['traceability']]
    if set(traced) != ids.get('invariants', set()) or len(traced) != len(set(traced)):
        errors.append('Traceability must cover each invariant exactly once')
    for row in registries['traceability']:
        if not row.get('tests'):
            errors.append(f'Invariant without proposed tests: {row.get("invariant")}')
        linked = [t['id'] for t in registries['test_scenarios'] if row.get('invariant') in t.get('invariants', [])]
        if set(linked) != set(row.get('tests', [])):
            errors.append(f'Inconsistent invariant/test reverse mapping: {row.get("invariant")}')
    graph = {p['id']: p.get('depends_on', []) for p in registries['phases'] if 'id' in p}
    visiting: set[str] = set()
    visited: set[str] = set()

    def visit(node: str) -> None:
        if node in visiting:
            errors.append(f'Phase dependency cycle at {node}')
            return
        if node in visited or node not in graph:
            return
        visiting.add(node)
        for dep in graph[node]:
            visit(dep)
        visiting.remove(node)
        visited.add(node)
    for node in graph:
        visit(node)
    for phase in graph:
        if not (root / 'phases' / f'{phase}.md').is_file():
            errors.append(f'Missing phase card: {phase}')

    files = package_files(root)
    links_checked = 0
    for p in files:
        if p.suffix != '.md':
            continue
        text = p.read_text(encoding='utf-8')
        for target in re.findall(r'\[[^\]\n]*\]\(([^)\n]+)\)', text):
            target = target.split(' "', 1)[0]
            parts = urlsplit(target)
            if parts.scheme or target.startswith('#') or not parts.path:
                continue
            destination = (p.parent / unquote(parts.path)).resolve()
            if not destination.is_relative_to(root):
                errors.append(f'Link escapes package: {p.relative_to(root)} -> {target}')
            elif not destination.exists():
                errors.append(f'Broken local link: {p.relative_to(root)} -> {target}')
            links_checked += 1

    if check_manifest:
        try:
            manifest = json.loads((root / 'MANIFEST.json').read_text(encoding='utf-8'))
            entries = manifest['files']
            listed = {e['path'] for e in entries}
            actual = {p.relative_to(root).as_posix() for p in files if p.name != 'MANIFEST.json'}
            if listed != actual or len(listed) != len(entries):
                errors.append(f'Manifest membership mismatch: missing={sorted(actual-listed)} extra={sorted(listed-actual)}')
            for entry in entries:
                p = (root / entry['path']).resolve()
                if not p.is_relative_to(root) or not p.is_file():
                    errors.append(f'Invalid manifest path: {entry["path"]}')
                    continue
                payload = p.read_bytes()
                if len(payload) != entry['bytes'] or hashlib.sha256(payload).hexdigest() != entry['sha256']:
                    errors.append(f'Manifest content mismatch: {entry["path"]}')
        except (OSError, KeyError, TypeError, ValueError) as exc:
            errors.append(f'Manifest read: {exc}')
    return dict(status='pass' if not errors else 'fail', file_count=len(files),
                local_links_checked=links_checked,
                registry_counts={n: len(r) for n, r in registries.items()}, errors=errors,
                application_validation='Not performed by this tool')


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('package_root', nargs='?', type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument('--without-manifest', action='store_true', help='For authoring before rebuilding MANIFEST.json')
    args = parser.parse_args()
    try:
        result = validate(args.package_root, not args.without_manifest)
    except (OSError, ValueError, TypeError) as exc:
        print(json.dumps({'status': 'fail', 'error': str(exc)}))
        return 2
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if result['status'] == 'pass' else 1


if __name__ == '__main__':
    sys.exit(main())
