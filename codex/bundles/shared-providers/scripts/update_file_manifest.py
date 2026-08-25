#!/usr/bin/env python3
"""Regenerate the bundle's portable SHA-256 file inventory."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path


def included_files(root: Path) -> list[Path]:
    return sorted(
        path
        for path in root.rglob("*")
        if path.is_file()
        and path.name != "bundle-file-manifest.json"
        and "__pycache__" not in path.parts
        and path.suffix != ".pyc"
    )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("bundle", nargs="?", default=".", help="Bundle root")
    args = parser.parse_args()

    root = Path(args.bundle).resolve()
    bundle = json.loads((root / "bundle.json").read_text(encoding="utf-8"))
    entries = []
    total_bytes = 0
    for path in included_files(root):
        content = path.read_bytes()
        total_bytes += len(content)
        entries.append(
            {
                "path": path.relative_to(root).as_posix(),
                "sizeBytes": len(content),
                "sha256": hashlib.sha256(content).hexdigest(),
            }
        )

    manifest = {
        "schemaVersion": "1.0",
        "bundleId": bundle["bundleId"],
        "algorithm": "SHA-256",
        "selfExcluded": True,
        "fileCount": len(entries),
        "totalContentBytes": total_bytes,
        "files": entries,
    }
    output = root / "bundle-file-manifest.json"
    output.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"Updated {output} with {len(entries)} file(s).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
