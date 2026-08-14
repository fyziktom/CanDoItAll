#!/usr/bin/env python3
"""Create or verify the bundle file index and SHA-256 checksum manifest."""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
from datetime import datetime, timezone
from pathlib import Path

INDEX_NAME = "bundle-index.json"
CHECKSUM_NAME = "CHECKSUMS.sha256"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bundle-root", required=True, type=Path)
    parser.add_argument("--verify", action="store_true")
    return parser.parse_args()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def all_files(root: Path, *, exclude: set[str]) -> list[Path]:
    return sorted(
        (path for path in root.rglob("*") if path.is_file() and path.name not in exclude),
        key=lambda path: path.relative_to(root).as_posix(),
    )


def create_integrity_files(root: Path) -> None:
    payload_files = all_files(root, exclude={INDEX_NAME, CHECKSUM_NAME})
    index = {
        "schema_version": 1,
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "root_name": root.name,
        "payload_file_count": len(payload_files),
        "payload_total_bytes": sum(path.stat().st_size for path in payload_files),
        "excluded_integrity_files": [INDEX_NAME, CHECKSUM_NAME],
        "files": [
            {
                "path": path.relative_to(root).as_posix(),
                "size_bytes": path.stat().st_size,
                "sha256": sha256_file(path),
            }
            for path in payload_files
        ],
    }
    (root / INDEX_NAME).write_text(
        json.dumps(index, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
        newline="\n",
    )

    checksum_files = all_files(root, exclude={CHECKSUM_NAME})
    lines = [
        f"{sha256_file(path)}  {path.relative_to(root).as_posix()}"
        for path in checksum_files
    ]
    (root / CHECKSUM_NAME).write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")


def verify_integrity_files(root: Path) -> list[str]:
    errors: list[str] = []
    index_path = root / INDEX_NAME
    checksum_path = root / CHECKSUM_NAME
    if not index_path.is_file():
        errors.append(f"Missing {INDEX_NAME}")
        return errors
    if not checksum_path.is_file():
        errors.append(f"Missing {CHECKSUM_NAME}")
        return errors

    try:
        index = json.loads(index_path.read_text(encoding="utf-8"))
    except (json.JSONDecodeError, UnicodeDecodeError) as exc:
        errors.append(f"Invalid {INDEX_NAME}: {exc}")
        return errors

    payload_files = {
        path.relative_to(root).as_posix(): path
        for path in all_files(root, exclude={INDEX_NAME, CHECKSUM_NAME})
    }
    indexed = index.get("files")
    if not isinstance(indexed, list):
        errors.append("Index files must be an array")
    else:
        indexed_paths: set[str] = set()
        for item in indexed:
            if not isinstance(item, dict):
                errors.append("Index entry is not an object")
                continue
            relative_path = str(item.get("path", ""))
            if relative_path in indexed_paths:
                errors.append(f"Duplicate index path: {relative_path}")
                continue
            indexed_paths.add(relative_path)
            actual = payload_files.get(relative_path)
            if actual is None:
                errors.append(f"Indexed file is missing: {relative_path}")
                continue
            if item.get("size_bytes") != actual.stat().st_size:
                errors.append(f"Size mismatch: {relative_path}")
            if item.get("sha256") != sha256_file(actual):
                errors.append(f"Hash mismatch: {relative_path}")
        if indexed_paths != set(payload_files):
            errors.append("Index path set differs from payload path set")
        if index.get("payload_file_count") != len(payload_files):
            errors.append("Index payload_file_count differs from actual payload")

    checksum_entries: dict[str, str] = {}
    for line_number, line in enumerate(checksum_path.read_text(encoding="utf-8").splitlines(), start=1):
        if not line.strip():
            continue
        try:
            digest, relative_path = line.split("  ", 1)
        except ValueError:
            errors.append(f"Malformed checksum line {line_number}")
            continue
        if len(digest) != 64 or any(character not in "0123456789abcdef" for character in digest):
            errors.append(f"Invalid checksum digest at line {line_number}")
            continue
        checksum_entries[relative_path] = digest

    checksum_files = {
        path.relative_to(root).as_posix(): path
        for path in all_files(root, exclude={CHECKSUM_NAME})
    }
    if set(checksum_entries) != set(checksum_files):
        errors.append("Checksum path set differs from actual file path set")
    for relative_path, digest in checksum_entries.items():
        actual = checksum_files.get(relative_path)
        if actual is not None and sha256_file(actual) != digest:
            errors.append(f"Checksum mismatch: {relative_path}")
    return errors


def main() -> int:
    args = parse_args()
    root = args.bundle_root.expanduser().resolve()
    if not root.is_dir():
        print(f"ERROR: bundle root does not exist: {root}", file=sys.stderr)
        return 2

    if args.verify:
        errors = verify_integrity_files(root)
        for error in errors:
            print(f"ERROR: {error}")
        print("RESULT: PASS" if not errors else "RESULT: FAIL")
        return 1 if errors else 0

    create_integrity_files(root)
    print(f"Created {root / INDEX_NAME}")
    print(f"Created {root / CHECKSUM_NAME}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
