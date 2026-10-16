#!/usr/bin/env python3
"""Rebuild this package's content manifest after deliberate package edits.

This changes MANIFEST.json only. It does not certify application behavior.
"""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path
import sys


def rebuild(root: Path) -> dict:
    root = root.resolve()
    if not (root / 'package.json').is_file():
        raise ValueError('Not an architecture package root: package.json missing')
    entries = []
    for p in sorted(root.rglob('*')):
        if (not p.is_file() or p.name in {'MANIFEST.json', '.DS_Store'}
                or '__pycache__' in p.parts or p.suffix == '.pyc'):
            continue
        if p.is_symlink():
            raise ValueError(f'Symlink not allowed in distributable package: {p}')
        data = p.read_bytes()
        entries.append(dict(path=p.relative_to(root).as_posix(), bytes=len(data), sha256=hashlib.sha256(data).hexdigest()))
    result = dict(schema_version=1, hash_algorithm='SHA-256',
                  excludes=['MANIFEST.json', '__pycache__ directories', '*.pyc', '.DS_Store'],
                  meaning='Package integrity only; not a signature or application test result', files=entries)
    (root / 'MANIFEST.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('package_root', nargs='?', type=Path, default=Path(__file__).resolve().parents[1])
    args = parser.parse_args()
    try:
        result = rebuild(args.package_root)
        print(f'Manifest rebuilt: {len(result["files"])} files')
        return 0
    except (OSError, ValueError) as exc:
        print(str(exc), file=sys.stderr)
        return 1


if __name__ == '__main__':
    sys.exit(main())
