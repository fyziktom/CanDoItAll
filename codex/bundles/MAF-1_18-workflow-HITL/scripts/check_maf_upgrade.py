#!/usr/bin/env python3
"""Scan a CanDoItAll checkout for MAF upgrade and unsafe opt-in conditions."""

from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Iterable

SKIP_DIRECTORIES = {
    ".git",
    ".vs",
    "bin",
    "obj",
    "node_modules",
    "TestResults",
    "artifacts",
    "codex",
}

ACTIVE_EXTENSIONS = {".cs", ".csproj", ".props", ".targets"}
VERSION_EXTENSIONS = {".csproj", ".props", ".targets", ".json"}

OLD_ISOLATION_SYMBOLS = [
    "SessionIsolationKeyProvider",
    "SessionIsolationKeyProviderOptions",
    "AddSessionIsolationKeyProvider",
    "ClaimsIdentitySessionIsolationKeyProvider",
]

UNSAFE_PATTERNS = {
    "concurrent_tool_invocation_enabled": re.compile(
        r"\bAllowConcurrentInvocation\b[^\n=]{0,80}=\s*true\b", re.IGNORECASE
    ),
    "declaration_only_storage_enabled": re.compile(
        r"\bStoreInvocableFunctionCallsForFutureTurns\b[^\n=]{0,80}=\s*true\b",
        re.IGNORECASE,
    ),
}

INFO_PATTERNS = {
    "provided_chat_client_as_is": re.compile(
        r"\bUseProvidedChatClientAsIs\s*=\s*true\b", re.IGNORECASE
    ),
    "function_invoking_chat_client": re.compile(r"\bFunctionInvokingChatClient\b"),
    "tool_approval_agent": re.compile(r"\bToolApprovalAgent(?:Options)?\b"),
}


@dataclass(frozen=True)
class Finding:
    category: str
    path: str
    line: int
    text: str


def iter_files(root: Path, extensions: set[str]) -> Iterable[Path]:
    git_files = subprocess.run(
        [
            "git",
            "-C",
            str(root),
            "ls-files",
            "--cached",
            "--others",
            "--exclude-standard",
            "-z",
        ],
        check=False,
        capture_output=True,
    )
    if git_files.returncode == 0:
        for raw_path in git_files.stdout.split(b"\0"):
            if not raw_path:
                continue
            relative_path = Path(raw_path.decode("utf-8", errors="surrogateescape"))
            if any(part in SKIP_DIRECTORIES for part in relative_path.parts):
                continue
            path = root / relative_path
            if path.is_file() and path.suffix.lower() in extensions:
                yield path
        return

    for directory, child_directories, file_names in os.walk(root):
        child_directories[:] = [
            name for name in child_directories if name not in SKIP_DIRECTORIES
        ]
        directory_path = Path(directory)
        for file_name in file_names:
            path = directory_path / file_name
            if path.suffix.lower() in extensions:
                yield path


def scan_patterns(
    root: Path,
    patterns: dict[str, tuple[re.Pattern[str], str]],
) -> dict[str, list[Finding]]:
    findings = {key: [] for key in patterns}
    for path in iter_files(root, ACTIVE_EXTENSIONS):
        try:
            lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
        except OSError:
            continue
        for number, line in enumerate(lines, start=1):
            for key, (pattern, category) in patterns.items():
                if pattern.search(line):
                    findings[key].append(
                        Finding(
                            category=category,
                            path=str(path.relative_to(root)),
                            line=number,
                            text=line.strip()[:300],
                        )
                    )
    return findings


def read_versions(props_path: Path) -> tuple[str, str]:
    tree = ET.parse(props_path)
    root = tree.getroot()
    stable = ""
    preview = ""
    for element in root.iter():
        tag = element.tag.split("}")[-1]
        if tag == "MicrosoftAgentsAIStableVersion":
            stable = (element.text or "").strip()
        elif tag == "MicrosoftAgentsAIPreviewVersion":
            preview = (element.text or "").strip()
    return stable, preview


def find_active_old_versions(root: Path) -> list[Finding]:
    pattern = re.compile(r"\b1\.17\.0(?:-preview\.260804\.1)?\b")
    findings: list[Finding] = []
    for path in iter_files(root, VERSION_EXTENSIONS):
        try:
            lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
        except OSError:
            continue
        for number, line in enumerate(lines, start=1):
            if "Microsoft.Agents.AI" in line or path.name == "MicrosoftAgentFramework.Packages.props":
                if pattern.search(line):
                    findings.append(
                        Finding(
                            category="active_maf_1_17_reference",
                            path=str(path.relative_to(root)),
                            line=number,
                            text=line.strip()[:300],
                        )
                    )
    return findings


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("repo_root")
    parser.add_argument("--mode", choices=["baseline", "upgraded"], required=True)
    parser.add_argument("--json-output")
    args = parser.parse_args()

    repo_root = Path(args.repo_root).resolve()
    props_path = repo_root / "src/MAF/MicrosoftAgentFramework.Packages.props"
    if not props_path.is_file():
        print(f"ERROR: Missing {props_path}", file=sys.stderr)
        return 2

    stable, preview = read_versions(props_path)
    expected = {
        "baseline": ("1.17.0", "1.17.0-preview.260804.1"),
        "upgraded": ("1.18.0", "1.18.0-preview.260818.1"),
    }[args.mode]

    errors: list[str] = []
    warnings: list[str] = []
    findings: list[Finding] = []

    if (stable, preview) != expected:
        errors.append(
            f"Version properties are stable={stable!r}, preview={preview!r}; "
            f"expected {expected!r} for mode {args.mode}."
        )

    source_patterns = {
        **{
            f"old:{symbol}": (
                re.compile(rf"\b{re.escape(symbol)}\b"),
                "old_isolation_symbol",
            )
            for symbol in OLD_ISOLATION_SYMBOLS
        },
        **{
            f"unsafe:{category}": (pattern, category)
            for category, pattern in UNSAFE_PATTERNS.items()
        },
        **{
            f"info:{category}": (pattern, category)
            for category, pattern in INFO_PATTERNS.items()
        },
    }
    source_findings = scan_patterns(repo_root, source_patterns)

    for symbol in OLD_ISOLATION_SYMBOLS:
        symbol_findings = source_findings[f"old:{symbol}"]
        findings.extend(symbol_findings)
        if args.mode == "upgraded" and symbol_findings:
            errors.append(f"Old isolation symbol remains: {symbol}")

    for category in UNSAFE_PATTERNS:
        matches = source_findings[f"unsafe:{category}"]
        findings.extend(matches)
        if matches:
            errors.append(f"Unsafe opt-in found: {category}")

    for category in INFO_PATTERNS:
        matches = source_findings[f"info:{category}"]
        findings.extend(matches)
        if matches:
            warnings.append(
                f"Manual audit required for {category}: {len(matches)} occurrence(s)."
            )

    old_versions = find_active_old_versions(repo_root)
    findings.extend(old_versions)
    if args.mode == "upgraded" and old_versions:
        errors.append("Active MAF 1.17 references remain after upgrade.")

    report = {
        "mode": args.mode,
        "repoRoot": str(repo_root),
        "versions": {"stable": stable, "preview": preview},
        "expectedVersions": {"stable": expected[0], "preview": expected[1]},
        "errors": errors,
        "warnings": warnings,
        "findings": [asdict(item) for item in findings],
    }

    if args.json_output:
        output_path = Path(args.json_output)
        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")

    print(json.dumps(report, indent=2))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
