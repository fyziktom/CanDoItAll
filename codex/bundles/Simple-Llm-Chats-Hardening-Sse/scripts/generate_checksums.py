#!/usr/bin/env python3
"""Generate SHA-256 checksums for every bundle file except the checksum file."""

from __future__ import annotations

import argparse
import hashlib
from pathlib import Path


def digest(path: Path) -> str:
    hasher = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            hasher.update(chunk)
    return hasher.hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--bundle-root", type=Path, default=Path("."))
    args = parser.parse_args()
    root = args.bundle_root.resolve()
    output = root / "CHECKSUMS.sha256"
    paths = sorted(
        path for path in root.rglob("*")
        if path.is_file() and path != output
    )
    lines = [
        f"{digest(path)}  {path.relative_to(root).as_posix()}"
        for path in paths
    ]
    output.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"Wrote {len(lines)} checksums to {output}.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
