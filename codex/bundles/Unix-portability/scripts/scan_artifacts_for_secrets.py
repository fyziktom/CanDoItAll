#!/usr/bin/env python3
"""Scan generated proof artifacts for likely secret-bearing material without echoing values."""

from __future__ import annotations

import argparse
import hashlib
import json
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
    ".json",
    ".log",
    ".md",
    ".ps1",
    ".sh",
    ".txt",
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
    parser.add_argument("--report-only", action="store_true")
    parser.add_argument("--include-placeholders", action="store_true")
    return parser.parse_args()


def should_scan(path: Path, root: Path, max_file_bytes: int) -> bool:
    try:
        relative = path.relative_to(root)
        if any(part in IGNORED_DIRECTORIES for part in relative.parts):
            return False
        if path.is_symlink() or not path.is_file() or path.stat().st_size > max_file_bytes:
            return False
    except OSError:
        return False
    return path.suffix.casefold() in TEXT_SUFFIXES or path.name.casefold() in {"dockerfile", "nuget.config"}


def fingerprint(value: str) -> str:
    return hashlib.sha256(value.encode("utf-8", errors="replace")).hexdigest()[:16]


def is_placeholder(value: str) -> bool:
    normalized = value.strip().casefold()
    return any(marker in normalized for marker in PLACEHOLDER_MARKERS)


def redact_line(line: str) -> str:
    stripped = line.strip()
    stripped = re.sub(
        r"(?i)((?:password|pwd|api[_-]?key|access[_-]?token|client[_-]?secret|private[_-]?key)\s*[:=]\s*)[^,;\s]+",
        r"\1<redacted>",
        stripped,
    )
    stripped = re.sub(r"(?i)((?:Password|Pwd)=)[^;\s]+", r"\1<redacted>", stripped)
    stripped = re.sub(r"\bgh[opusr]_[A-Za-z0-9]{20,}\b", "ghx_<redacted>", stripped)
    stripped = re.sub(r"\bsk-[A-Za-z0-9_-]{16,}\b", "sk-<redacted>", stripped)
    if len(stripped) > 300:
        stripped = stripped[:297] + "..."
    return stripped


def main() -> int:
    args = parse_args()
    root = args.root.expanduser().resolve()
    if not root.is_dir():
        print(f"ERROR: scan root does not exist: {root}", file=sys.stderr)
        return 2

    findings: list[dict] = []
    scanned_files = 0
    for path in sorted(root.rglob("*")):
        if not should_scan(path, root, args.max_file_bytes):
            continue
        scanned_files += 1
        try:
            text = path.read_text(encoding="utf-8")
        except (UnicodeDecodeError, OSError):
            continue
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
                        "redacted_excerpt": redact_line(line),
                    }
                )

    counts = Counter(item["rule"] for item in findings)
    report = {
        "schema_version": 1,
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "root": root.as_posix(),
        "scanned_files": scanned_files,
        "finding_count": len(findings),
        "by_rule": dict(sorted(counts.items())),
        "value_disclosure": "Secret values are never stored in this report; fingerprints are truncated SHA-256 identifiers.",
        "findings": findings,
    }
    if args.output:
        output = args.output.expanduser().resolve()
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
        print(f"Report: {output}")

    print(f"Scanned files: {scanned_files}")
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
