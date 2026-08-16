from __future__ import annotations

import hashlib
import sys
from pathlib import Path


def main() -> int:
    root = Path(__file__).resolve().parents[1]
    checksum_file = root / "CHECKSUMS.sha256"
    if not checksum_file.is_file():
        print("CHECKSUMS.sha256 is missing.", file=sys.stderr)
        return 1

    errors: list[str] = []
    expected_paths: set[str] = set()

    for line_no, raw in enumerate(checksum_file.read_text(encoding="utf-8").splitlines(), start=1):
        if not raw.strip():
            continue
        try:
            digest, relative = raw.split("  ", 1)
        except ValueError:
            errors.append(f"Malformed checksum line {line_no}.")
            continue
        expected_paths.add(relative)
        path = root / relative
        if not path.is_file():
            errors.append(f"Missing checksummed file: {relative}")
            continue
        actual = hashlib.sha256(path.read_bytes()).hexdigest()
        if actual != digest:
            errors.append(f"Checksum mismatch: {relative}")

    actual_paths = {
        path.relative_to(root).as_posix()
        for path in root.rglob("*")
        if path.is_file()
        and path != checksum_file
        and "__pycache__" not in path.parts
        and path.suffix != ".pyc"
    }
    missing_entries = actual_paths - expected_paths
    stale_entries = expected_paths - actual_paths
    if missing_entries:
        errors.append("Files without checksum: " + ", ".join(sorted(missing_entries)))
    if stale_entries:
        errors.append("Stale checksum entries: " + ", ".join(sorted(stale_entries)))

    if errors:
        print("Checksum validation failed:", file=sys.stderr)
        for error in errors:
            print(f"- {error}", file=sys.stderr)
        return 1

    print(f"Checksum validation passed: {len(expected_paths)} files.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
