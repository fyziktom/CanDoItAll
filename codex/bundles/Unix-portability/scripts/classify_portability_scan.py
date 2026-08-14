#!/usr/bin/env python3
import argparse
import csv
import json
from collections import Counter
from pathlib import Path


REFERENCE_PREFIXES = (
    ".github/",
    "codex/",
    "docs/",
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Classify every portability scanner finding into an owned program requirement."
    )
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--json-output", required=True, type=Path)
    parser.add_argument("--csv-output", required=True, type=Path)
    parser.add_argument("--summary-output", required=True, type=Path)
    return parser.parse_args()


def category_route(category: str) -> tuple[str, str, str]:
    routes = {
        "windows-path": ("A01", "PATH-001", "Core paths"),
        "path-normalization": ("A01", "PATH-002", "Core paths"),
        "absolute-path-field": ("A01", "PATH-005", "Core paths"),
        "path-api": ("A01", "PATH-006", "Core paths"),
        "environment": ("A05", "PLAT-005", "Composition and capabilities"),
        "case-policy": ("A02", "FS-001", "Core filesystem"),
        "filesystem-enumeration": ("A02", "FS-002", "Core filesystem"),
        "link-reparse": ("A02", "FS-003", "Core filesystem"),
        "atomic-write": ("A02", "FS-004", "Core filesystem"),
        "permissions": ("A02", "FS-007", "Core filesystem"),
        "secret-provider": ("A04", "SEC-001", "Core security"),
        "dpapi": ("A04", "SEC-002", "Core security"),
        "dataprotection": ("A04", "SEC-003", "Core security"),
        "process-start": ("B01", "EXEC-001", "MAF runtime"),
        "direct-process-host": ("B01", "EXEC-002", "MAF runtime"),
        "windows-executable": ("B01", "EXEC-003", "MAF runtime"),
        "shell-elevation": ("B02", "NODE-005", "Workbench runtime nodes"),
        "manager-discovery": ("B03", "MGR-001", "Manager supervision"),
        "mcp": ("B04", "MCP-001", "MCP and external tools"),
        "external-tool": ("B04", "TOOL-001", "MCP and external tools"),
        "process-domain": ("B06", "PROC-001", "Processes domain"),
        "os-branch": ("A05", "PLAT-001", "Composition and capabilities"),
    }
    return routes.get(category, ("A00", "PREP-002", "Portability preparation"))


def executable_route(path: str, category: str) -> tuple[str, str, str]:
    phase, requirement, owner = category_route(category)

    if category in {"secret-provider", "dpapi", "dataprotection"}:
        return phase, requirement, owner

    if path.startswith(
        (
            "src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Paths/",
            "src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimePathResolver.cs",
        )
    ) and category in {
        "windows-path",
        "path-normalization",
        "absolute-path-field",
        "path-api",
        "case-policy",
        "filesystem-enumeration",
        "link-reparse",
        "atomic-write",
        "permissions",
    }:
        return phase, requirement, owner

    if path.startswith("src/Modules/CanDoItAll.Modules.Security/"):
        return "A04", requirement if requirement.startswith("SEC-") else "SEC-001", "Core security"
    if path.startswith("src/Integration/CanDoItAll.FileTools"):
        return "B05", "PLUG-003", "FileTools integration"
    if path.startswith("src/plugins/"):
        return "B05", "PLUG-005", "Plugins"
    if path.startswith("tools/App/CanDoItAll.Manager/"):
        return "B03", "MGR-007" if category in {"case-policy", "absolute-path-field", "windows-path"} else "MGR-001", "Manager supervision"
    if path.startswith("src/Processes/"):
        return "B06", "PROC-001", "Processes domain"
    if path.startswith("src/Modules/CanDoItAll.Modules.Processes/"):
        return "B06", "PROC-001", "Processes domain"
    if path.startswith("src/Modules/CanDoItAll.Modules.Plugins/"):
        return "B05", "PLUG-005", "Plugins"
    if path.startswith(
        (
            "src/Modules/CanDoItAll.Modules.Projects/",
            "src/Modules/CanDoItAll.Modules.Workspace/",
        )
    ):
        return "A02", "FS-001", "Core filesystem consumers"
    if path.startswith(
        (
            "src/Modules/CanDoItAll.Modules.CrmHr/",
            "src/Modules/CanDoItAll.Modules.Prompts/",
            "src/Modules/CanDoItAll.Modules.Resources/",
            "src/UI/CanDoItAll.AppComponents/",
        )
    ):
        return "A05", "PLAT-005", "Composition and capabilities"
    if path.startswith("src/Modules/CanDoItAll.Modules.Workbench/"):
        return "B02", "NODE-008" if category in {"case-policy", "absolute-path-field", "windows-path", "path-normalization", "link-reparse"} else "NODE-001", "Workbench runtime nodes"
    if path.startswith("src/MAF/") or path.startswith("src/Modules/CanDoItAll.Modules.AgentFramework/"):
        if category in {"mcp", "external-tool"}:
            return "B04", "MCP-001" if category == "mcp" else "TOOL-001", "MCP and external tools"
        return "B01", "EXEC-001", "MAF runtime"
    if path.startswith("src/App/") and category in {"windows-path", "absolute-path-field", "path-normalization"}:
        return "A01", "PATH-001", "Core paths"
    if path.startswith("tools/install/"):
        return "A06", "HOST-001", "Hosting and installation"
    if path.startswith("tools/"):
        return "A06", "HOST-001", "Developer tooling and hosting"
    return phase, requirement, owner


def classify(finding: dict) -> dict:
    path = finding["path"].replace("\\", "/")
    category = finding["category"]
    is_reference = path.startswith(REFERENCE_PREFIXES) or path in {
        "README.md",
        "CanDoItAll.slnx",
        "Directory.Build.props",
        "Directory.Packages.props",
    }
    is_test = path.startswith("tests/")
    is_fixture = path.startswith("Templates/") or "/SeedAssets/" in path

    if is_reference:
        phase, requirement, owner = "A00", "PREP-002", "Documentation and build metadata"
        disposition = "Reference-only occurrence; no executable behavior. Retain under documentation and redaction validation."
        review_rule = "reference-only"
    elif is_test or is_fixture:
        phase, requirement, implementation_owner = executable_route(path.removeprefix("tests/"), category)
        owner = f"Tests, seeds, or templates for {implementation_owner}"
        disposition = f"Test, serialized seed, or template occurrence; retain as characterization evidence for {phase}/{requirement}."
        review_rule = "test-or-fixture"
    else:
        phase, requirement, owner = executable_route(path, category)
        disposition = f"Executable, configuration, template, or persisted-data surface assigned to {phase}/{requirement}."
        review_rule = "owned-runtime-surface"

    reviewed = dict(finding)
    reviewed.update(
        {
            "owner_domain": owner,
            "review_status": "Classified",
            "disposition": disposition,
            "requirement_id": requirement,
            "owner_phase": phase,
            "review_rule": review_rule,
        }
    )
    return reviewed


def write_csv(path: Path, findings: list[dict]) -> None:
    columns = [
        "id",
        "path",
        "line",
        "category",
        "severity",
        "owner_phase",
        "owner_domain",
        "requirement_id",
        "review_status",
        "review_rule",
        "disposition",
        "pattern",
        "source_fingerprint",
        "evidence_excerpt",
    ]
    with path.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=columns, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(findings)


def write_summary(path: Path, scan: dict, findings: list[dict]) -> None:
    severity_counts = Counter(item["severity"] for item in findings)
    phase_counts = Counter(item["owner_phase"] for item in findings)
    rule_counts = Counter(item["review_rule"] for item in findings)
    high_priority = [item for item in findings if item["severity"] in {"critical", "high"}]
    unclassified = [
        item
        for item in findings
        if item["review_status"] != "Classified"
        or not item["owner_domain"]
        or not item["requirement_id"]
        or not item["owner_phase"]
    ]

    lines = [
        "# Portability scan classification",
        "",
        f"- Input generator: `{scan.get('generator', 'unknown')}`",
        f"- Findings reviewed: `{len(findings)}`",
        f"- Critical/high findings: `{len(high_priority)}`",
        f"- Unclassified findings: `{len(unclassified)}`",
        "- Review method: deterministic path/category routing with explicit separation of executable surfaces, tests/fixtures, and reference-only material.",
        "",
        "## Severity",
        "",
        "| Severity | Count |",
        "|---|---:|",
    ]
    lines.extend(f"| {key} | {value} |" for key, value in sorted(severity_counts.items()))
    lines.extend(["", "## Owning phase", "", "| Phase | Count |", "|---|---:|"])
    lines.extend(f"| {key} | {value} |" for key, value in sorted(phase_counts.items()))
    lines.extend(["", "## Review disposition", "", "| Rule | Count |", "|---|---:|"])
    lines.extend(f"| {key} | {value} |" for key, value in sorted(rule_counts.items()))
    lines.extend(
        [
            "",
            "## Gate interpretation",
            "",
            "The scanner is deliberately lexical and reports examples in documentation, bundle evidence, test fixtures, and serialized snapshots. Classification is not a claim that each match is a defect. Executable/configuration findings remain assigned to the implementation phase and requirement that must either change the behavior or close it with characterization proof.",
            "",
        ]
    )
    path.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    args = parse_args()
    scan = json.loads(args.input.read_text(encoding="utf-8"))
    findings = [classify(item) for item in scan["findings"]]

    for output in (args.json_output, args.csv_output, args.summary_output):
        output.parent.mkdir(parents=True, exist_ok=True)

    reviewed_scan = dict(scan)
    reviewed_scan["findings"] = findings
    reviewed_scan["review"] = {
        "method": "deterministic-category-and-path-routing",
        "classified_count": len(findings),
        "unclassified_count": sum(
            1
            for item in findings
            if item["review_status"] != "Classified"
            or not item["owner_domain"]
            or not item["requirement_id"]
            or not item["owner_phase"]
        ),
    }
    args.json_output.write_text(json.dumps(reviewed_scan, indent=2) + "\n", encoding="utf-8")
    write_csv(args.csv_output, findings)
    write_summary(args.summary_output, scan, findings)
    return 0 if reviewed_scan["review"]["unclassified_count"] == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
