#!/usr/bin/env python3
"""Scan generated proof artifacts for likely secret-bearing material without echoing values."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import sys
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path

IGNORED_DIRECTORIES = {".git", ".vs", "bin", "node_modules", "obj"}
TEXT_SUFFIXES = {
    ".config",
    ".csv",
    ".env",
    ".html",
    ".json",
    ".log",
    ".md",
    ".patch",
    ".ps1",
    ".sh",
    ".txt",
    ".trx",
    ".xml",
    ".yaml",
    ".yml",
}
PLACEHOLDER_MARKERS = {
    "<redacted>",
    "change-me",
    "changeme",
    "dummy",
    "example",
    "placeholder",
    "replace-me",
    "sample",
    "test-only",
}
RULES: list[tuple[str, re.Pattern[str], int]] = [
    ("private-key", re.compile(r"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----"), 0),
    ("github-token", re.compile(r"\bgh[opusr]_[A-Za-z0-9]{30,}\b"), 0),
    ("openai-token", re.compile(r"\bsk-[A-Za-z0-9_-]{32,}\b"), 0),
    ("aws-access-key", re.compile(r"\bAKIA[0-9A-Z]{16}\b"), 0),
    ("slack-token", re.compile(r"\bxox[baprs]-[A-Za-z0-9-]{20,}\b"), 0),
    (
        "secret-assignment",
        re.compile(
            r"(?i)(?:password|pwd|api[_-]?key|access[_-]?token|client[_-]?secret|private[_-]?key)\s*[:=]\s*[\"']?([^\"'\s,;]{8,})"
        ),
        1,
    ),
    ("connection-string-password", re.compile(r"(?i)(?:Password|Pwd)=([^;\s]{4,})"), 1),
]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", required=True, type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--max-file-bytes", type=int, default=5 * 1024 * 1024)
    parser.add_argument(
        "--sentinel-file",
        action="append",
        default=[],
        type=Path,
        help="Private UTF-8 file containing one exact sentinel per line. Repeat for multiple inputs.",
    )
    parser.add_argument("--report-only", action="store_true")
    parser.add_argument("--include-placeholders", action="store_true")
    parser.add_argument(
        "--exclude-directory",
        action="append",
        default=[],
        help="Directory name to exclude recursively. Repeat for multiple generated source snapshots.",
    )
    return parser.parse_args()


def is_text_candidate(path: Path) -> bool:
    return path.suffix.casefold() in TEXT_SUFFIXES or path.name.casefold() in {"dockerfile", "nuget.config"}


def coverage_entry(path: Path, root: Path, size: int | None = None) -> dict:
    entry = {"path": path.relative_to(root).as_posix()}
    if size is not None:
        entry["bytes"] = size
    return entry


def fingerprint(value: str) -> str:
    return hashlib.sha256(value.encode("utf-8", errors="replace")).hexdigest()[:16]


def is_placeholder(value: str) -> bool:
    normalized = value.strip().casefold()
    return any(marker in normalized for marker in PLACEHOLDER_MARKERS)


def load_sentinels(paths: list[Path]) -> list[str]:
    values: list[str] = []
    seen: set[str] = set()
    for path in paths:
        try:
            lines = path.expanduser().resolve().read_text(encoding="utf-8").splitlines()
        except (UnicodeDecodeError, OSError) as exception:
            raise ValueError("A private sentinel input could not be read as UTF-8.") from exception
        for line in lines:
            if not line or line in seen:
                continue
            seen.add(line)
            values.append(line)
    if paths and not values:
        raise ValueError("Private sentinel inputs must contain at least one non-empty line.")
    return values


def iter_candidates(root: Path, excluded_directories: set[str]):
    for current_root, directory_names, file_names in os.walk(
        root,
        topdown=True,
        onerror=lambda _: None,
    ):
        directory_names[:] = sorted(
            name
            for name in directory_names
            if name not in IGNORED_DIRECTORIES and name not in excluded_directories
        )
        current_path = Path(current_root)
        for file_name in sorted(file_names):
            yield current_path / file_name


def main() -> int:
    args = parse_args()
    root = args.root.expanduser().resolve()
    if not root.is_dir():
        print(f"ERROR: scan root does not exist: {root}", file=sys.stderr)
        return 2

    excluded_directories = set(args.exclude_directory)
    output = args.output.expanduser().resolve() if args.output else None
    sentinel_paths = {path.expanduser().resolve() for path in args.sentinel_file}
    try:
        sentinels = load_sentinels(args.sentinel_file)
    except ValueError as exception:
        print(f"ERROR: {exception}", file=sys.stderr)
        return 2
    findings: list[dict] = []
    oversized_text_files: list[dict] = []
    non_text_files: list[dict] = []
    unreadable_text_files: list[dict] = []
    control_input_files: list[dict] = []
    candidate_files = 0
    scanned_files = 0
    for path in iter_candidates(root, excluded_directories):
        candidate_files += 1
        try:
            resolved_path = path.resolve()
            size = path.stat().st_size
        except OSError:
            unreadable_text_files.append(coverage_entry(path, root))
            continue
        if (output is not None and resolved_path == output) or resolved_path in sentinel_paths:
            control_input_files.append(coverage_entry(path, root, size))
            continue
        if path.is_symlink() or not path.is_file():
            unreadable_text_files.append(coverage_entry(path, root, size))
            continue
        if not is_text_candidate(path):
            non_text_files.append(coverage_entry(path, root, size))
            continue
        if size > args.max_file_bytes:
            oversized_text_files.append(coverage_entry(path, root, size))
            continue
        try:
            text = path.read_text(encoding="utf-8")
        except (UnicodeDecodeError, OSError):
            unreadable_text_files.append(coverage_entry(path, root, size))
            continue
        scanned_files += 1
        for line_number, line in enumerate(text.splitlines(), start=1):
            for rule_name, pattern, value_group in RULES:
                match = pattern.search(line)
                if match is None:
                    continue
                value = match.group(value_group) if value_group else match.group(0)
                if not args.include_placeholders and is_placeholder(value):
                    continue
                findings.append(
                    {
                        "id": f"SECRET-{len(findings) + 1:05d}",
                        "path": path.relative_to(root).as_posix(),
                        "line": line_number,
                        "rule": rule_name,
                        "fingerprint": fingerprint(value),
                    }
                )
            for sentinel in sentinels:
                start = 0
                while True:
                    match_index = line.find(sentinel, start)
                    if match_index < 0:
                        break
                    findings.append(
                        {
                            "id": f"SECRET-{len(findings) + 1:05d}",
                            "path": path.relative_to(root).as_posix(),
                            "line": line_number,
                            "rule": "seeded-sentinel",
                            "fingerprint": fingerprint(sentinel),
                        }
                    )
                    start = match_index + len(sentinel)

    counts = Counter(item["rule"] for item in findings)
    report = {
        "schema_version": 3,
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "root": root.as_posix(),
        "excluded_directories": sorted(excluded_directories),
        "max_file_bytes": args.max_file_bytes,
        "scanned_files": scanned_files,
        "coverage": {
            "candidate_files": candidate_files,
            "scanned_text_files": scanned_files,
            "oversized_text_files": {
                "count": len(oversized_text_files),
                "files": oversized_text_files,
            },
            "excluded_non_text_files": {
                "count": len(non_text_files),
                "files": non_text_files,
            },
            "unreadable_text_files": {
                "count": len(unreadable_text_files),
                "files": unreadable_text_files,
            },
            "excluded_control_input_files": {
                "count": len(control_input_files),
                "files": control_input_files,
            },
        },
        "finding_count": len(findings),
        "sentinel_input_file_count": len(sentinel_paths),
        "sentinel_value_count": len(sentinels),
        "sentinel_finding_count": counts.get("seeded-sentinel", 0),
        "by_rule": dict(sorted(counts.items())),
        "value_disclosure": "Findings contain metadata and truncated SHA-256 fingerprints only; source excerpts and secret values are never stored.",
        "findings": findings,
    }
    if output is not None:
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
        print(f"Report: {output}")

    print(f"Scanned files: {scanned_files}")
    print(f"Oversized text files skipped: {len(oversized_text_files)}")
    print(f"Non-text files excluded: {len(non_text_files)}")
    print(f"Unreadable text files skipped: {len(unreadable_text_files)}")
    print(f"Findings: {len(findings)}")
    for item in findings[:20]:
        print(f"FOUND: {item['path']}:{item['line']} [{item['rule']}] fingerprint={item['fingerprint']}")
    if len(findings) > 20:
        print(f"Additional findings omitted from console: {len(findings) - 20}")
    if findings and not args.report_only:
        print("RESULT: FAIL")
        return 1
    print("RESULT: PASS" if not findings else "RESULT: REPORT-ONLY")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
