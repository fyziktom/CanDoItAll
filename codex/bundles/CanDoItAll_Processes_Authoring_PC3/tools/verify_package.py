#!/usr/bin/env python3
"""Verify this task archive locally without reading or updating a shared bundle.

Integrity is not authenticity, source compatibility, or a passing product test.
The tool uses only the Python standard library and never modifies the package.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from pathlib import Path, PurePosixPath
from typing import Any

MANIFEST_NAME = "MANIFEST.json"


def canonical_digest(path: Path) -> str:
    """Normalize CRLF only, preserving other meaningful text differences."""
    data = path.read_bytes().decode("utf-8").replace("\r\n", "\n")
    return hashlib.sha256(data.encode("utf-8")).hexdigest()


def load_object(path: Path) -> dict[str, Any]:
    """Require a JSON object rather than silently accepting another JSON type."""
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"Expected JSON object: {path.name}")
    return value


def safe_path(value: str) -> PurePosixPath:
    """Reject traversal, ambiguous separators, and non-canonical paths."""
    if not isinstance(value, str) or not value or any(c in value for c in "\\:\0"):
        raise ValueError("Invalid manifest path")
    path = PurePosixPath(value)
    if path.is_absolute() or ".." in path.parts or path.as_posix() != value or value == ".":
        raise ValueError(f"Unsafe manifest path: {value}")
    return path


def verify_package(root: Path) -> list[str]:
    """Return package problems; do not inspect a companion or Git working tree."""
    if root.is_symlink() or not root.is_dir():
        return ["Package root must be a real directory, not a symbolic link."]
    root = root.resolve(strict=True)
    found = list(root.rglob("*"))
    links = [p.relative_to(root).as_posix() for p in found if p.is_symlink()]
    if links:
        return ["Symbolic link is not allowed: " + name for name in sorted(links)]
    if not (root / MANIFEST_NAME).is_file() or not (root / "bundle.json").is_file():
        return ["MANIFEST.json and bundle.json must both exist."]

    manifest = load_object(root / MANIFEST_NAME)
    if manifest.get("algorithm") != "sha256":
        raise ValueError("Manifest algorithm must be sha256")
    entries = manifest.get("files")
    if not isinstance(entries, dict) or not entries:
        raise ValueError("Manifest files must be a non-empty object")
    errors: list[str] = []
    expected: set[str] = set()
    for name, expected_digest in entries.items():
        rel = safe_path(name)
        if name == MANIFEST_NAME:
            raise ValueError("Manifest must not hash itself")
        if not isinstance(expected_digest, str) or not re.fullmatch(r"[0-9a-f]{64}", expected_digest):
            raise ValueError(f"Invalid digest: {name}")
        expected.add(name)
        file = root.joinpath(*rel.parts)
        if not file.is_file():
            errors.append("Missing file: " + name)
        elif canonical_digest(file) != expected_digest:
            errors.append("Digest mismatch: " + name)

    actual = {p.relative_to(root).as_posix() for p in found if p.is_file()}
    errors.extend("Unlisted file: " + name for name in sorted(actual - expected - {MANIFEST_NAME}))
    if "bundle.json" not in expected:
        errors.append("bundle.json must be sealed by the manifest.")
    if any("UI_Decoupling_Shared_Bundle" in p.relative_to(root).parts for p in found):
        errors.append("A shared snapshot must not be embedded in this task archive.")

    metadata = load_object(root / "bundle.json")
    if "shared_requirement" in metadata:
        errors.append("The task must not require a shared version or manifest pin.")
    if metadata.get("delivery") != "single_archive_task_only":
        errors.append("Expected single-archive task-only delivery.")
    policy = metadata.get("shared_policy")
    if not isinstance(policy, dict):
        errors.append("The task must declare its existing-baseline shared policy.")
    else:
        for key in ("version_pin", "manifest_pin", "separate_download_required"):
            if policy.get(key) is not False:
                errors.append("Shared policy must disable " + key + ".")
        if policy.get("mode") != "reuse_existing_repository_baseline":
            errors.append("Shared policy must reuse the existing repository baseline.")
    return sorted(set(errors))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("package", type=Path, help="Extracted PC3 task directory")
    args = parser.parse_args()
    try:
        errors = verify_package(args.package)
    except (OSError, ValueError, UnicodeError) as error:
        print(json.dumps({"package_integrity": "ERROR", "error": str(error),
                          "product_validation": "NOT_RUN"}), file=sys.stderr)
        return 2
    print(json.dumps({"package_integrity": "FAIL" if errors else "PASS", "issues": errors,
                      "shared_bundle_accessed": False, "product_validation": "NOT_RUN"}, indent=2))
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
