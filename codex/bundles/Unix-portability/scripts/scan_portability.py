#!/usr/bin/env python3
"""Scan a CanDoItAll checkout for platform-sensitive implementation surfaces."""

from __future__ import annotations

import argparse
import csv
import json
import os
import platform
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
from typing import Iterable

DEFAULT_IGNORED_DIRECTORIES = {
    ".artifacts",
    ".git",
    ".mcp-state",
    ".vs",
    "TestResults",
    "artifacts",
    "bin",
    "coverage",
    "node_modules",
    "obj",
    "playwright-report",
    "screenshots",
    "test-results",
}
TEXT_SUFFIXES = {
    ".bat",
    ".cmd",
    ".config",
    ".cs",
    ".cshtml",
    ".csproj",
    ".css",
    ".editorconfig",
    ".fs",
    ".fsproj",
    ".html",
    ".js",
    ".json",
    ".jsx",
    ".md",
    ".props",
    ".ps1",
    ".py",
    ".razor",
    ".sh",
    ".sln",
    ".slnx",
    ".sql",
    ".targets",
    ".toml",
    ".ts",
    ".tsx",
    ".txt",
    ".xml",
    ".yaml",
    ".yml",
}
SEVERITY_BY_CATEGORY = {
    "dpapi": "critical",
    "secret-provider": "critical",
    "dataprotection": "high",
    "windows-path": "high",
    "process-start": "high",
    "shell-elevation": "high",
    "direct-process-host": "high",
    "manager-discovery": "high",
    "absolute-path-field": "high",
    "link-reparse": "high",
    "atomic-write": "medium",
    "permissions": "medium",
    "windows-executable": "medium",
    "case-policy": "medium",
    "path-normalization": "medium",
    "environment": "medium",
    "mcp": "medium",
    "external-tool": "medium",
    "process-domain": "medium",
    "os-branch": "low",
    "path-api": "low",
    "filesystem-enumeration": "low",
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--patterns", type=Path)
    parser.add_argument("--max-file-bytes", type=int, default=2 * 1024 * 1024)
    parser.add_argument("--max-findings", type=int, default=100_000)
    parser.add_argument("--include-untracked", action="store_true", default=True)
    parser.add_argument("--tracked-only", action="store_true")
    parser.add_argument("--fail-on-unreviewed-critical", action="store_true")
    return parser.parse_args()


def run_git(repo_root: Path, *arguments: str, check: bool = True) -> str:
    completed = subprocess.run(
        ["git", "-C", str(repo_root), *arguments],
        check=check,
        capture_output=True,
        text=True,
    )
    return completed.stdout.strip()


def load_patterns(path: Path) -> list[tuple[str, str, re.Pattern[str]]]:
    patterns: list[tuple[str, str, re.Pattern[str]]] = []
    for line_number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), start=1):
        stripped = line.strip()
        if not stripped or stripped.startswith("#"):
            continue
        try:
            category, expression = stripped.split("\t", 1)
        except ValueError as exc:
            raise ValueError(f"Invalid pattern line {line_number}; expected category<TAB>regex") from exc
        try:
            compiled = re.compile(expression, re.IGNORECASE)
        except re.error as exc:
            raise ValueError(f"Invalid regex at line {line_number}: {exc}") from exc
        patterns.append((category.strip(), expression, compiled))
    if not patterns:
        raise ValueError("Pattern file contains no active patterns")
    return patterns


def is_ignored(relative_path: Path, additional_ignored: set[str]) -> bool:
    return any(part in DEFAULT_IGNORED_DIRECTORIES or part in additional_ignored for part in relative_path.parts)


def enumerate_files(repo_root: Path, tracked_only: bool, output: Path) -> list[Path]:
    output_relative: Path | None = None
    try:
        output_relative = output.resolve().relative_to(repo_root.resolve())
    except ValueError:
        pass
    additional_ignored = {output_relative.parts[0]} if output_relative and output_relative.parts else set()

    candidates: list[Path] = []
    try:
        arguments = ["ls-files", "-z", "--cached"]
        if not tracked_only:
            arguments.extend(["--others", "--exclude-standard"])
        completed = subprocess.run(
            ["git", "-C", str(repo_root), *arguments],
            check=True,
            capture_output=True,
        )
        for raw in completed.stdout.split(b"\0"):
            if not raw:
                continue
            relative = Path(os.fsdecode(raw))
            if is_ignored(relative, additional_ignored):
                continue
            candidates.append(repo_root / relative)
    except (FileNotFoundError, subprocess.CalledProcessError):
        for path in repo_root.rglob("*"):
            if not path.is_file():
                continue
            relative = path.relative_to(repo_root)
            if not is_ignored(relative, additional_ignored):
                candidates.append(path)

    return sorted(set(candidates), key=lambda path: path.relative_to(repo_root).as_posix())


def should_scan(path: Path, max_file_bytes: int) -> bool:
    try:
        if path.is_symlink() or not path.is_file() or path.stat().st_size > max_file_bytes:
            return False
    except OSError:
        return False
    name = path.name.casefold()
    if name in {"dockerfile", "makefile", "global.json", "nuget.config"}:
        return True
    return path.suffix.casefold() in TEXT_SUFFIXES


def owner_domain(relative_path: str) -> str:
    normalized = relative_path.replace("\\", "/")
    mappings = [
        ("src/Modules/CanDoItAll.Modules.Security/", "Security"),
        ("src/Foundation/CanDoItAll.Infrastructure/", "Foundation Infrastructure"),
        ("src/Foundation/CanDoItAll.Security.Abstractions/", "Security Abstractions"),
        ("src/Modules/CanDoItAll.Modules.Workbench/", "Workbench"),
        ("src/Modules/CanDoItAll.Modules.Processes/", "Processes Module"),
        ("src/Processes/", "Processes"),
        ("src/MAF/", "Agent Framework"),
        ("tools/App/CanDoItAll.Manager/", "Development Manager"),
        ("src/plugins/", "Plugins"),
        ("src/Integration/CanDoItAll.FileTools", "FileTools Integration"),
        ("src/App/", "Application Composition"),
        ("tests/", "Tests"),
        ("tools/install/", "Installation"),
        (".github/", "CI"),
    ]
    for prefix, owner in mappings:
        if normalized.startswith(prefix):
            return owner
    return "Unassigned"


def redact_excerpt(line: str) -> str:
    value = line.strip()
    value = re.sub(
        r"(?i)(password|pwd|api[_-]?key|access[_-]?token|client[_-]?secret|private[_-]?key)\s*([:=])\s*([^,;\s\"']+|\"[^\"]*\"|'[^']*')",
        r"\1\2<redacted>",
        value,
    )
    value = re.sub(r"(?i)(Password|Pwd)=[^;\s]+", r"\1=<redacted>", value)
    value = re.sub(r"(?i)Bearer\s+[A-Za-z0-9._~+/=-]{12,}", "Bearer <redacted>", value)
    value = re.sub(r"\bsk-[A-Za-z0-9_-]{16,}\b", "sk-<redacted>", value)
    if len(value) > 400:
        value = value[:397] + "..."
    return value


def scan_file(
    path: Path,
    repo_root: Path,
    patterns: list[tuple[str, str, re.Pattern[str]]],
    next_number: int,
    max_findings: int,
) -> tuple[list[dict], int]:
    try:
        text = path.read_text(encoding="utf-8")
    except (UnicodeDecodeError, OSError):
        return [], next_number

    relative_path = path.relative_to(repo_root).as_posix()
    findings: list[dict] = []
    for line_number, line in enumerate(text.splitlines(), start=1):
        for category, expression, pattern in patterns:
            if not pattern.search(line):
                continue
            findings.append(
                {
                    "id": f"SCAN-{next_number:06d}",
                    "path": relative_path,
                    "line": line_number,
                    "category": category,
                    "severity": SEVERITY_BY_CATEGORY.get(category, "medium"),
                    "owner_domain": owner_domain(relative_path),
                    "pattern": expression,
                    "evidence_excerpt": redact_excerpt(line),
                    "review_status": "Unreviewed",
                    "disposition": "",
                    "requirement_id": "",
                }
            )
            next_number += 1
            if next_number > max_findings:
                return findings, next_number
    return findings, next_number


def read_xml_text(root: ET.Element, local_name: str) -> list[str]:
    values: list[str] = []
    for element in root.iter():
        if element.tag.rsplit("}", 1)[-1] == local_name and element.text and element.text.strip():
            values.extend(item.strip() for item in element.text.split(";") if item.strip())
    return values


def project_inventory(files: Iterable[Path], repo_root: Path) -> list[dict]:
    projects: list[dict] = []
    for path in files:
        if path.suffix.casefold() not in {".csproj", ".fsproj"}:
            continue
        relative_path = path.relative_to(repo_root).as_posix()
        item: dict = {
            "path": relative_path,
            "owner_domain": owner_domain(relative_path),
            "target_frameworks": [],
            "runtime_identifiers": [],
            "package_references": [],
            "project_references": [],
            "parse_error": "",
        }
        try:
            root = ET.parse(path).getroot()
            item["target_frameworks"] = sorted(
                set(read_xml_text(root, "TargetFramework") + read_xml_text(root, "TargetFrameworks"))
            )
            item["runtime_identifiers"] = sorted(
                set(read_xml_text(root, "RuntimeIdentifier") + read_xml_text(root, "RuntimeIdentifiers"))
            )
            packages: list[dict] = []
            references: list[str] = []
            for element in root.iter():
                name = element.tag.rsplit("}", 1)[-1]
                if name == "PackageReference":
                    include = element.attrib.get("Include") or element.attrib.get("Update") or ""
                    version = element.attrib.get("Version") or ""
                    if not version:
                        for child in element:
                            if child.tag.rsplit("}", 1)[-1] == "Version" and child.text:
                                version = child.text.strip()
                    packages.append({"id": include, "version": version})
                elif name == "ProjectReference":
                    include = element.attrib.get("Include") or ""
                    if include:
                        references.append(include.replace("\\", "/"))
            item["package_references"] = sorted(packages, key=lambda value: (value["id"].casefold(), value["version"]))
            item["project_references"] = sorted(set(references), key=str.casefold)
        except (ET.ParseError, OSError) as exc:
            item["parse_error"] = str(exc)
        projects.append(item)
    return projects


def write_csv(path: Path, findings: list[dict]) -> None:
    fieldnames = [
        "id",
        "severity",
        "category",
        "owner_domain",
        "path",
        "line",
        "review_status",
        "disposition",
        "requirement_id",
        "evidence_excerpt",
        "pattern",
    ]
    with path.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows({key: finding.get(key, "") for key in fieldnames} for finding in findings)


def write_markdown(path: Path, document: dict) -> None:
    summary = document["summary"]
    lines = [
        "# Portability scan summary",
        "",
        f"- Repository: `{document['repository']['root']}`",
        f"- Commit: `{document['repository']['head']}`",
        f"- Scanned files: {summary['scanned_files']}",
        f"- Findings: {summary['finding_count']}",
        f"- Truncated: {str(summary['truncated']).lower()}",
        "",
        "## Severity",
        "",
        "| Severity | Count |",
        "|---|---:|",
    ]
    for key, count in sorted(summary["by_severity"].items()):
        lines.append(f"| {key} | {count} |")
    lines.extend(["", "## Category", "", "| Category | Count |", "|---|---:|"])
    for key, count in sorted(summary["by_category"].items(), key=lambda item: (-item[1], item[0])):
        lines.append(f"| {key} | {count} |")
    lines.extend(["", "## Owner domain", "", "| Owner | Count |", "|---|---:|"])
    for key, count in sorted(summary["by_owner_domain"].items(), key=lambda item: (-item[1], item[0])):
        lines.append(f"| {key} | {count} |")
    lines.extend(
        [
            "",
            "## Review rule",
            "",
            "Every high/critical hit and every unassigned owner must be reviewed. A pattern match is an inventory lead, not proof of a defect.",
            "",
        ]
    )
    path.write_text("\n".join(lines), encoding="utf-8", newline="\n")


def main() -> int:
    args = parse_args()
    repo_root = args.repo_root.expanduser().resolve()
    output = args.output.expanduser().resolve()
    if not (repo_root / "CanDoItAll.slnx").is_file():
        print("ERROR: repo root does not contain CanDoItAll.slnx", file=sys.stderr)
        return 2

    default_patterns = Path(__file__).resolve().parent.parent / "shared/platform-sensitive-patterns.txt"
    patterns_path = args.patterns.expanduser().resolve() if args.patterns else default_patterns
    try:
        patterns = load_patterns(patterns_path)
    except (OSError, ValueError) as exc:
        print(f"ERROR: unable to load patterns: {exc}", file=sys.stderr)
        return 2

    tracked_only = args.tracked_only
    files = enumerate_files(repo_root, tracked_only=tracked_only, output=output)
    findings: list[dict] = []
    scanned_files = 0
    skipped_large_or_binary = 0
    next_number = 1
    truncated = False
    for path in files:
        if not should_scan(path, args.max_file_bytes):
            skipped_large_or_binary += 1
            continue
        scanned_files += 1
        file_findings, next_number = scan_file(
            path,
            repo_root,
            patterns,
            next_number,
            args.max_findings,
        )
        findings.extend(file_findings)
        if len(findings) >= args.max_findings:
            findings = findings[: args.max_findings]
            truncated = True
            break

    try:
        head = run_git(repo_root, "rev-parse", "HEAD")
        branch = run_git(repo_root, "branch", "--show-current") or "(detached)"
        dirty = bool(run_git(repo_root, "status", "--short"))
    except (FileNotFoundError, subprocess.CalledProcessError):
        head = "unknown"
        branch = "unknown"
        dirty = False

    severity_counts = Counter(finding["severity"] for finding in findings)
    category_counts = Counter(finding["category"] for finding in findings)
    owner_counts = Counter(finding["owner_domain"] for finding in findings)
    document = {
        "schema_version": 1,
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "generator": "scripts/scan_portability.py",
        "host": {
            "system": platform.system(),
            "release": platform.release(),
            "machine": platform.machine(),
            "python": platform.python_version(),
        },
        "repository": {
            "root": repo_root.as_posix(),
            "branch": branch,
            "head": head,
            "dirty": dirty,
        },
        "scan": {
            "patterns_file": patterns_path.as_posix(),
            "pattern_count": len(patterns),
            "tracked_only": tracked_only,
            "max_file_bytes": args.max_file_bytes,
            "max_findings": args.max_findings,
        },
        "summary": {
            "candidate_files": len(files),
            "scanned_files": scanned_files,
            "skipped_large_or_binary": skipped_large_or_binary,
            "finding_count": len(findings),
            "truncated": truncated,
            "by_severity": dict(sorted(severity_counts.items())),
            "by_category": dict(sorted(category_counts.items())),
            "by_owner_domain": dict(sorted(owner_counts.items())),
            "unassigned_owner_count": owner_counts.get("Unassigned", 0),
        },
        "projects": project_inventory(files, repo_root),
        "findings": findings,
    }

    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(document, indent=2, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    write_csv(output.with_suffix(".csv"), findings)
    write_markdown(output.with_suffix(".md"), document)

    print(f"Scanned files: {scanned_files}")
    print(f"Findings: {len(findings)}")
    print(f"JSON: {output}")
    print(f"CSV: {output.with_suffix('.csv')}")
    print(f"Markdown: {output.with_suffix('.md')}")
    if truncated:
        print("WARNING: finding limit reached; the inventory is incomplete")
    if args.fail_on_unreviewed_critical and severity_counts.get("critical", 0) > 0:
        print("RESULT: FAIL (unreviewed critical findings)")
        return 4
    print("RESULT: PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
