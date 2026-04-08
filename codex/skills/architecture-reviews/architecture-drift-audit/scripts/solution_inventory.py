#!/usr/bin/env python3
"""
Create a lightweight inventory of a .NET solution or repository.

This script is intentionally heuristic. It helps a review workflow gather
evidence quickly, but it is not a source of truth on its own.
"""

from __future__ import annotations

import argparse
import json
import re
from collections import Counter
from pathlib import Path
import sys


TYPE_PATTERNS = {
    "entity_like": re.compile(r"\b(class|record)\s+([A-Z][A-Za-z0-9_]*(Entity|Model|Aggregate|Root))\b"),
    "service_like": re.compile(r"\b(class|record)\s+([A-Z][A-Za-z0-9_]*(Service|Manager|Coordinator|Engine))\b"),
    "repository_like": re.compile(r"\b(interface|class|record)\s+([A-Z][A-Za-z0-9_]*(Repository|Store))\b"),
    "projection_like": re.compile(r"\b(class|record)\s+([A-Z][A-Za-z0-9_]*(Dto|ViewModel|Projection|Snapshot|Export|Import))\b"),
    "component_like": re.compile(r"\bpartial\s+class\s+([A-Z][A-Za-z0-9_]*)\b"),
    "enum_like": re.compile(r"\benum\s+([A-Z][A-Za-z0-9_]*)\b"),
}

SUSPICIOUS_NAME_PATTERNS = {
    "helper": re.compile(r"\b[A-Z][A-Za-z0-9_]*Helper\b"),
    "util": re.compile(r"\b[A-Z][A-Za-z0-9_]*Util(s)?\b"),
    "manager": re.compile(r"\b[A-Z][A-Za-z0-9_]*Manager\b"),
    "god_service": re.compile(r"\b[A-Z][A-Za-z0-9_]*(Orchestrator|Facade|Coordinator)\b"),
}


def read_text(path: Path) -> str:
    try:
        return path.read_text(encoding="utf-8")
    except UnicodeDecodeError:
        return path.read_text(encoding="utf-8", errors="ignore")
    except Exception:
        return ""


def parse_project_references(csproj_text: str) -> list[str]:
    return re.findall(r'<ProjectReference\s+Include="([^"]+)"', csproj_text)


def parse_namespace(code: str) -> str:
    match = re.search(r"\bnamespace\s+([A-Za-z0-9_.]+)\b", code)
    return match.group(1) if match else ""


def parse_declared_types(code: str) -> list[dict]:
    declared = []
    for kind, pattern in TYPE_PATTERNS.items():
        for match in pattern.finditer(code):
            name = match.group(2) if match.lastindex and match.lastindex >= 2 else match.group(1)
            declared.append({"kind": kind, "name": name})
    return declared


def scan_repo(root: Path) -> dict:
    sln_files = sorted(root.rglob("*.sln"))
    csproj_files = sorted(root.rglob("*.csproj"))
    cs_files = sorted(
        path for path in root.rglob("*.cs")
        if "/bin/" not in path.as_posix() and "/obj/" not in path.as_posix()
    )
    razor_files = sorted(root.rglob("*.razor"))

    projects = []
    namespace_counter: Counter[str] = Counter()
    type_counter: Counter[str] = Counter()
    suspicious_counter: Counter[str] = Counter()
    file_summaries = []

    for csproj in csproj_files:
        text = read_text(csproj)
        refs = parse_project_references(text)
        tfms = re.findall(r"<TargetFrameworks?>([^<]+)</TargetFrameworks?>", text)
        projects.append(
            {
                "path": str(csproj.relative_to(root)),
                "name": csproj.stem,
                "project_references": refs,
                "target_frameworks": tfms,
            }
        )

    for code_file in cs_files:
        text = read_text(code_file)
        namespace = parse_namespace(text)
        if namespace:
            namespace_counter[namespace] += 1

        declared_types = parse_declared_types(text)
        for item in declared_types:
            type_counter[item["kind"]] += 1

        suspicious_hits = []
        for label, pattern in SUSPICIOUS_NAME_PATTERNS.items():
            if pattern.search(text):
                suspicious_counter[label] += 1
                suspicious_hits.append(label)

        file_summaries.append(
            {
                "path": str(code_file.relative_to(root)),
                "namespace": namespace,
                "declared_type_kinds": sorted({item["kind"] for item in declared_types}),
                "suspicious_markers": suspicious_hits,
            }
        )

    return {
        "root": str(root.resolve()),
        "solutions": [str(path.relative_to(root)) for path in sln_files],
        "stats": {
            "csproj_count": len(csproj_files),
            "cs_file_count": len(cs_files),
            "razor_file_count": len(razor_files),
        },
        "projects": projects,
        "top_namespaces": namespace_counter.most_common(25),
        "type_counters": dict(type_counter),
        "suspicious_name_counters": dict(suspicious_counter),
        "files": file_summaries,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Create a lightweight inventory of a .NET repository.")
    parser.add_argument("--root", default=".", help="Repository root.")
    parser.add_argument("--output", default="", help="Optional JSON output path.")
    args = parser.parse_args()

    root = Path(args.root).resolve()
    result = scan_repo(root)
    payload = json.dumps(result, indent=2, ensure_ascii=False)

    if args.output:
        output_path = Path(args.output)
        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_text(payload, encoding="utf-8")
    else:
        print(payload)

    return 0


if __name__ == "__main__":
    sys.exit(main())
