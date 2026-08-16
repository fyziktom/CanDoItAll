#!/usr/bin/env python3
"""Verify bundle file checksums, excluding the checksum file itself."""

from __future__ import annotations

import hashlib
from pathlib import Path

root = Path(__file__).resolve().parents[1]
checksum_path = root / 'CHECKSUMS.sha256'
expected: dict[str, str] = {}
for line in checksum_path.read_text(encoding='utf-8').splitlines():
    if not line.strip():
        continue
    digest, rel = line.split('  ', 1)
    expected[rel] = digest
errors: list[str] = []
actual_files = {
    p.relative_to(root).as_posix(): p
    for p in root.rglob('*')
    if p.is_file() and p != checksum_path
}
for rel, digest in expected.items():
    path = actual_files.get(rel)
    if path is None:
        errors.append(f'missing file: {rel}')
        continue
    actual = hashlib.sha256(path.read_bytes()).hexdigest()
    if actual != digest:
        errors.append(f'checksum mismatch: {rel}')
for rel in sorted(set(actual_files) - set(expected)):
    errors.append(f'unlisted file: {rel}')
if errors:
    print('\n'.join(f'ERROR: {e}' for e in errors))
    raise SystemExit(1)
print(f'Checksum validation passed: {len(expected)} files.')
