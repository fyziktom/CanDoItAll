#!/usr/bin/env python3
"""Verify SHA256 checksums for prepared bundle files."""

from __future__ import annotations

import hashlib
from pathlib import Path

root = Path(__file__).resolve().parents[1]
checksum_file = root / "CHECKSUMS.sha256"
errors: list[str] = []
entries = 0

for raw_line in checksum_file.read_text(encoding="utf-8").splitlines():
    line = raw_line.strip()
    if not line or line.startswith("#"):
        continue
    expected, relative = line.split("  ", 1)
    path = root / relative
    entries += 1
    if not path.is_file():
        errors.append(f"missing checksum target: {relative}")
        continue
    actual = hashlib.sha256(path.read_bytes()).hexdigest()
    if actual != expected:
        errors.append(f"checksum mismatch: {relative}")

tracked = {
    path.relative_to(root).as_posix()
    for path in root.rglob("*")
    if path.is_file() and path.name != "CHECKSUMS.sha256"
}
listed = {
    line.strip().split("  ", 1)[1]
    for line in checksum_file.read_text(encoding="utf-8").splitlines()
    if line.strip() and not line.strip().startswith("#")
}
for missing in sorted(tracked - listed):
    errors.append(f"unlisted bundle file: {missing}")
for extra in sorted(listed - tracked):
    errors.append(f"checksum lists nonexistent file: {extra}")

if errors:
    print("\n".join(f"ERROR: {error}" for error in errors))
    raise SystemExit(1)
print(f"Checksums passed: {entries} files.")

