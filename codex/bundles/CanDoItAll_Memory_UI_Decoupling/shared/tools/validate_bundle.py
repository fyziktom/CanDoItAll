#!/usr/bin/env python3
"""Validate this text bundle's metadata, local file links and sealed manifest; not product behavior."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import sys
from urllib.parse import unquote, urlsplit

OID = re.compile(r"^[0-9a-f]{40}$")
SHA256_LINE = re.compile(r"^([0-9a-f]{64})  (.+)$")
LINK = re.compile(r"\[[^\]\n]*\]\(([^)\n]+)\)")
REFERENCE = re.compile(r"\b[SF]\d{2}\b")


def validate(root: Path) -> list[str]:
    root = root.resolve()
    errors: list[str] = []
    files: dict[str, Path] = {}
    for path in sorted(root.rglob("*")):
        if path.is_symlink():
            errors.append(f"Symlink is not allowed in this portable text bundle: {path.relative_to(root)}")
            continue
        if path.is_file():
            name = path.relative_to(root).as_posix()
            files[name] = path
            if path.suffix.lower() not in {".md", ".json", ".py", ".sha256"}:
                errors.append(f"Unexpected non-text artifact: {name}")
            try:
                path.read_text(encoding="utf-8")
            except (OSError, UnicodeDecodeError) as exc:
                errors.append(f"Cannot read UTF-8 file {name}: {exc}")
    parsed: dict[str, object] = {}
    for name, path in files.items():
        if name.endswith(".json"):
            try:
                parsed[name] = json.loads(path.read_text(encoding="utf-8"))
            except (OSError, ValueError) as exc:
                errors.append(f"Invalid JSON {name}: {exc}")
    bundle = parsed.get("bundle.json")
    register = parsed.get("audit/source-register.json")
    source_ids: set[str] = set()
    if not isinstance(bundle, dict):
        errors.append("Missing/invalid bundle.json")
    else:
        for key in ("executable", "review_commit_is_execution_pin", "product_changes_authorized_by_this_bundle"):
            if bundle.get(key) is not False:
                errors.append(f"Shared reference must explicitly set {key}=false")
        for key in ("entry_point", "child_template", "evidence_template", "source_register", "input_provenance"):
            if bundle.get(key) not in files:
                errors.append(f"Missing declared {key}: {bundle.get(key)!r}")
    if not isinstance(register, dict) or not isinstance(register.get("sources"), list):
        errors.append("Missing/invalid source register")
    else:
        sha = register.get("review_commit", "")
        if not isinstance(sha, str) or not OID.fullmatch(sha):
            errors.append("Invalid source review commit")
        if isinstance(bundle, dict) and bundle.get("review_commit") != sha:
            errors.append("Bundle/source review commit mismatch")
        for source in register["sources"]:
            if not isinstance(source, dict):
                errors.append("Non-object source record")
                continue
            sid = source.get("id", "")
            if not isinstance(sid, str) or not re.fullmatch(r"[SF]\d{2}", sid) or sid in source_ids:
                errors.append(f"Invalid or duplicate source ID: {sid!r}")
            source_ids.add(str(sid))
            if source.get("kind", "").startswith("repository-"):
                if source.get("review_commit") != sha:
                    errors.append(f"Unexplained mixed review commit at {sid}")
                object_sha = source.get("observed_blob_sha") or source.get("observed_tree_sha")
                if object_sha is not None and (not isinstance(object_sha, str) or not OID.fullmatch(object_sha)):
                    errors.append(f"Invalid Git object ID at {sid}")
                path = source.get("path", "")
                if not isinstance(path, str) or not path or PurePosixPath(path).is_absolute() or ".." in PurePosixPath(path).parts:
                    errors.append(f"Invalid source path at {sid}")
                if f"/{sha}/" not in source.get("url", ""):
                    errors.append(f"Repository source URL is not pinned to the review at {sid}")
                if not source.get("coverage"):
                    errors.append(f"Missing review coverage at {sid}")
    for name, path in files.items():
        if not name.endswith(".md"):
            continue
        try:
            text = path.read_text(encoding="utf-8")
        except (OSError, UnicodeDecodeError):
            continue
        for sid in set(REFERENCE.findall(text)):
            if sid not in source_ids:
                errors.append(f"Unknown source {sid} in {name}")
        for match in LINK.finditer(text):
            target = match.group(1).strip().strip("<>")
            parsed_target = urlsplit(target)
            if parsed_target.scheme or target.startswith("#"):
                continue  # External connectivity and Markdown fragments are not certified by this utility.
            relative = unquote(parsed_target.path)
            destination = (path.parent / relative).resolve()
            try:
                destination.relative_to(root)
            except ValueError:
                errors.append(f"Local link escapes bundle in {name}: {target}")
                continue
            if not destination.exists():
                errors.append(f"Broken local link in {name}: {target}")
    manifest = files.get("MANIFEST.sha256")
    expected: dict[str, str] = {}
    if manifest is None:
        errors.append("Missing MANIFEST.sha256")
    else:
        for line in manifest.read_text(encoding="utf-8").splitlines():
            match = SHA256_LINE.fullmatch(line)
            if match is None:
                errors.append(f"Invalid manifest line: {line!r}")
                continue
            digest, name = match.groups()
            pp = PurePosixPath(name)
            if pp.is_absolute() or ".." in pp.parts or "\\" in name or name == "MANIFEST.sha256":
                errors.append(f"Unsafe/self-referential manifest path: {name}")
                continue
            if name in expected:
                errors.append(f"Duplicate manifest path: {name}")
            expected[name] = digest
        actual = set(files) - {"MANIFEST.sha256"}
        for name in sorted(actual - set(expected)):
            errors.append(f"Unsealed file: {name}")
        for name in sorted(set(expected) - actual):
            errors.append(f"Manifest references missing file: {name}")
        for name in sorted(actual & set(expected)):
            if hashlib.sha256(files[name].read_bytes()).hexdigest() != expected[name]:
                errors.append(f"Hash mismatch: {name}")
    return errors


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    args = parser.parse_args()
    try:
        errors = validate(args.root)
    except (OSError, ValueError, TypeError) as exc:
        print(f"Bundle validation failed to run: {exc}", file=sys.stderr)
        return 2
    if errors:
        for error in errors:
            print(f"FAIL: {error}", file=sys.stderr)
        return 1
    print("PASS: bundle metadata, source IDs, local file links, UTF-8 text inventory and SHA-256 manifest.")
    print("This does not validate external URLs, GitHub contents, product builds/tests or runtime performance.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
