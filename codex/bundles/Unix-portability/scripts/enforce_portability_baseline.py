#!/usr/bin/env python3
"""Fail when portability-sensitive executable source drifts from its reviewed baseline."""

from __future__ import annotations

import argparse
import json
import sys
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path


SCHEMA_VERSION = 1
POLICY_CATEGORIES = (
    "absolute-path-field",
    "atomic-write",
    "case-policy",
    "dataprotection",
    "direct-process-host",
    "dpapi",
    "environment",
    "external-tool",
    "filesystem-enumeration",
    "link-reparse",
    "manager-discovery",
    "mcp",
    "os-branch",
    "path-api",
    "path-normalization",
    "permissions",
    "process-domain",
    "process-start",
    "secret-provider",
    "shell-elevation",
    "windows-executable",
    "windows-path",
)
PROTECTED_PREFIXES = (
    ".github/",
    "src/",
    "Templates/",
    "tools/",
)
PROTECTED_ROOT_FILES = {
    ".env.example",
    "Directory.Build.props",
    "Directory.Build.targets",
    "Directory.Packages.props",
    "docker-compose.yml",
    "global.json",
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--scan", required=True, type=Path)
    parser.add_argument("--baseline", required=True, type=Path)
    parser.add_argument(
        "--write-baseline",
        action="store_true",
        help="Replace the baseline from the supplied complete scan after explicit review.",
    )
    return parser.parse_args()


def is_protected_path(path: str) -> bool:
    return path in PROTECTED_ROOT_FILES or path.startswith(PROTECTED_PREFIXES)


def is_sha256(value: str) -> bool:
    return len(value) == 64 and all(character in "0123456789abcdef" for character in value)


def load_scan(path: Path) -> dict:
    document = json.loads(path.read_text(encoding="utf-8"))
    if document.get("schema_version") != 2:
        raise ValueError("Portability scan schema 2 is required.")
    if document.get("summary", {}).get("truncated"):
        raise ValueError("A truncated portability scan cannot be enforced.")
    if not is_sha256(str(document.get("scan", {}).get("patterns_sha256", ""))):
        raise ValueError("Portability scan does not identify its pattern set.")
    if tuple(document.get("scan", {}).get("pattern_categories", [])) != tuple(sorted(POLICY_CATEGORIES)):
        raise ValueError("Portability scanner categories do not match the enforcement policy.")
    return document


def protected_counter(scan: dict) -> Counter[tuple[str, str, str]]:
    counter: Counter[tuple[str, str, str]] = Counter()
    for finding in scan.get("findings", []):
        path = str(finding.get("path", "")).replace("\\", "/")
        category = str(finding.get("category", ""))
        if category not in POLICY_CATEGORIES or not is_protected_path(path):
            continue

        fingerprint = str(finding.get("source_fingerprint", ""))
        if not is_sha256(fingerprint):
            raise ValueError(f"Protected finding lacks a valid fingerprint: {path} ({category}).")
        counter[(path, category, fingerprint)] += 1
    return counter


def serialize_allowances(counter: Counter[tuple[str, str, str]]) -> list[dict]:
    return [
        {
            "path": path,
            "category": category,
            "source_fingerprint": fingerprint,
            "count": count,
        }
        for (path, category, fingerprint), count in sorted(counter.items())
    ]


def write_baseline(path: Path, scan: dict, counter: Counter[tuple[str, str, str]]) -> None:
    baseline = {
        "schema_version": SCHEMA_VERSION,
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "review_gate": "A07 Core Gate C4 portability static guard",
        "patterns_sha256": scan["scan"]["patterns_sha256"],
        "policy_categories": list(POLICY_CATEGORIES),
        "protected_prefixes": list(PROTECTED_PREFIXES),
        "protected_root_files": sorted(PROTECTED_ROOT_FILES),
        "allowance_count": sum(counter.values()),
        "allowances": serialize_allowances(counter),
    }
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(baseline, indent=2) + "\n", encoding="utf-8", newline="\n")


def load_baseline(path: Path, scan: dict) -> Counter[tuple[str, str, str]]:
    baseline = json.loads(path.read_text(encoding="utf-8"))
    if baseline.get("schema_version") != SCHEMA_VERSION:
        raise ValueError(f"Portability baseline schema {SCHEMA_VERSION} is required.")
    if tuple(baseline.get("policy_categories", [])) != POLICY_CATEGORIES:
        raise ValueError("Portability baseline categories do not match the enforcement policy.")
    if tuple(baseline.get("protected_prefixes", [])) != PROTECTED_PREFIXES:
        raise ValueError("Portability baseline prefixes do not match the enforcement policy.")
    if set(baseline.get("protected_root_files", [])) != PROTECTED_ROOT_FILES:
        raise ValueError("Portability baseline root files do not match the enforcement policy.")
    if baseline.get("patterns_sha256") != scan["scan"]["patterns_sha256"]:
        raise ValueError("Portability scanner patterns changed; review and refresh the baseline explicitly.")

    counter: Counter[tuple[str, str, str]] = Counter()
    for allowance in baseline.get("allowances", []):
        key = (
            str(allowance.get("path", "")),
            str(allowance.get("category", "")),
            str(allowance.get("source_fingerprint", "")),
        )
        count = allowance.get("count")
        if (
            not is_protected_path(key[0])
            or key[1] not in POLICY_CATEGORIES
            or not is_sha256(key[2])
            or not isinstance(count, int)
            or count < 1
        ):
            raise ValueError(f"Invalid portability baseline allowance: {key[0]} ({key[1]}).")
        counter[key] += count

    if baseline.get("allowance_count") != sum(counter.values()):
        raise ValueError("Portability baseline allowance count is inconsistent.")
    return counter


def describe(counter: Counter[tuple[str, str, str]]) -> list[str]:
    return [
        f"{path} [{category}] {fingerprint[:16]} count={count}"
        for (path, category, fingerprint), count in sorted(counter.items())
    ]


def main() -> int:
    args = parse_args()
    try:
        scan = load_scan(args.scan)
        current = protected_counter(scan)
        if args.write_baseline:
            write_baseline(args.baseline, scan, current)
            print(f"Wrote reviewed portability baseline with {sum(current.values())} allowances: {args.baseline}")
            return 0

        baseline = load_baseline(args.baseline, scan)
    except (OSError, ValueError, json.JSONDecodeError) as exception:
        print(f"ERROR: {exception}", file=sys.stderr)
        return 2

    additions = current - baseline
    stale = baseline - current
    if additions or stale:
        print("RESULT: FAIL (portability-sensitive executable source changed)", file=sys.stderr)
        for item in describe(additions):
            print(f"ADDED: {item}", file=sys.stderr)
        for item in describe(stale):
            print(f"STALE: {item}", file=sys.stderr)
        print("Review the source change, then refresh the baseline explicitly with --write-baseline.", file=sys.stderr)
        return 1

    print(f"RESULT: PASS ({sum(current.values())} reviewed executable-source findings unchanged)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
