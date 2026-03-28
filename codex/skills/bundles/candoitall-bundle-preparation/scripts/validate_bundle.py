#!/usr/bin/env python3

from __future__ import annotations

import argparse
from pathlib import Path
import re

COMMON_DIRECTORIES = [
    "inputs",
    "analysis",
    "requirements",
    "architecture",
    "plan",
    "traceability",
    "shared-prompts",
    "subbundles",
    "reviews",
]

PROFILE_DIRECTORIES = {
    "feedback": [],
    "initiative": ["inventories", "templates"],
}

REQUIRED_FILES = [
    "README.md",
    "inputs/00-original-request.md",
    "inputs/01-source-artifacts.md",
    "inputs/02-structured-input.md",
    "analysis/01-current-state.md",
    "requirements/01-normalized-requirements.md",
    "architecture/01-target-solution.md",
    "plan/01-phase-plan.md",
    "traceability/01-requirement-traceability.md",
    "shared-prompts/implementation-prompt.md",
    "shared-prompts/qa-prompt.md",
    "reviews/00-bundle-self-review.md",
    "reviews/01-execution-report.md",
]

PLAN_HEADINGS = [
    "## Subbundle Dependency Map",
    "## Critical Subbundles",
    "## Phase Gates",
]

ANALYSIS_RISK_HEADINGS = [
    "## Critical Path Risks",
    "## Validation Risks",
    "## Reopen Triggers",
]

SUBBUNDLE_HEADING_GROUPS = [
    ("## Status",),
    ("## Objective",),
    ("## Covered Inputs", "## Covered Notes"),
    ("## Prerequisites",),
    ("## Exact Source References",),
    ("## Deliverables", "## Scope"),
    ("## Dependency Impact",),
    ("## Validation Depth",),
    ("## Implementation Steps",),
    ("## Do Not Do",),
    ("## Acceptance Checklist",),
    ("## Proof Required",),
    ("## Browser Validation Logging",),
    ("## Progression Gate",),
    ("## Suggested Agent Prompt",),
]

EXECUTION_REPORT_HEADINGS = [
    "## Status",
    "## Subbundle Gate Results",
    "## Browser Validation Analytics",
    "## Analytics Review",
    "## Raw Note Closure",
]

SUBBUNDLE_GATE_RESULTS_HEADER = "| Subbundle | Entry gate | Closure gate | Downstream dependencies checked | Progression result | Notes |"
BROWSER_ANALYTICS_HEADER = "| Subbundle | Route | Viewport | Playwright MCP evidence | Screenshots | Result |"
RAW_NOTE_CLOSURE_HEADER = "| Raw note | Status | Proof |"
PENDING_MARKERS = ("`Pending`", "`Not started`", "Pending implementation")


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Validate a CanDoItAll bundle structure.")
    parser.add_argument("bundle_path", help="Path to the bundle root.")
    parser.add_argument("--profile", choices=("feedback", "initiative"), default="feedback")
    parser.add_argument("--stage", choices=("prepared", "completed"), default="prepared")
    return parser.parse_args()


def collect_missing_paths(bundle_path: Path, profile: str) -> list[str]:
    missing: list[str] = []

    for directory in [*COMMON_DIRECTORIES, *PROFILE_DIRECTORIES[profile]]:
        if not (bundle_path / directory).is_dir():
            missing.append(directory)

    for relative_file in REQUIRED_FILES:
        if not (bundle_path / relative_file).is_file():
            missing.append(relative_file)

    return missing


def validate_required_headings(path: Path, headings: list[str]) -> list[str]:
    content = path.read_text(encoding="utf-8")
    issues: list[str] = []

    for heading in headings:
        if heading not in content:
            issues.append(f"{path}: missing required heading {heading}")

    return issues


def validate_subbundle_readme(path: Path, stage: str) -> list[str]:
    content = path.read_text(encoding="utf-8")
    issues: list[str] = []

    for heading_group in SUBBUNDLE_HEADING_GROUPS:
        if not any(heading in content for heading in heading_group):
            issues.append(f"{path}: missing one of {', '.join(heading_group)}")

    issues.extend(validate_exact_source_references(path, content))

    if stage == "completed":
        status_line = next((line.strip() for line in content.splitlines() if line.strip().startswith("- `")), "")
        if status_line in ("- `Ready`", "- `In progress`"):
            issues.append(f"{path}: completed-stage validation does not allow subbundle status {status_line}")

    return issues


def extract_markdown_section(content: str, heading: str) -> str | None:
    lines = content.splitlines()
    start_index: int | None = None

    for index, line in enumerate(lines):
        if line.strip() == heading:
            start_index = index + 1
            break

    if start_index is None:
        return None

    end_index = len(lines)
    for index in range(start_index, len(lines)):
        if lines[index].startswith("## "):
            end_index = index
            break

    return "\n".join(lines[start_index:end_index])


def extract_bullet_values(section_content: str) -> list[str]:
    values: list[str] = []
    for line in section_content.splitlines():
        stripped = line.strip()
        if not stripped.startswith("- "):
            continue

        value = stripped[2:].strip()
        exact_match = re.fullmatch(r"`([^`]+)`", value)
        if exact_match is not None:
            value = exact_match.group(1).strip()

        values.append(value)

    return values


def validate_exact_source_references(path: Path, content: str) -> list[str]:
    section_content = extract_markdown_section(content, "## Exact Source References")
    if section_content is None:
        return []

    references = extract_bullet_values(section_content)
    if not references:
        return [f"{path}: ## Exact Source References must include at least one markdown bullet path"]

    issues: list[str] = []
    for reference in references:
        reference_path = Path(reference)
        if not reference_path.is_absolute():
            issues.append(f"{path}: source reference is not an absolute path: {reference}")
            continue

        if not reference_path.exists():
            issues.append(f"{path}: source reference does not exist: {reference}")

    return issues


def validate_root_readme(path: Path, stage: str) -> list[str]:
    content = path.read_text(encoding="utf-8")
    issues: list[str] = []

    if "## Validation Summary" not in content:
        issues.append(f"{path}: missing required heading ## Validation Summary")
        return issues

    if stage == "prepared" and "Bundle preparation status: `Draft`" in content:
        issues.append(f"{path}: prepared-stage validation does not allow draft preparation status")

    if stage == "completed":
        forbidden_lines = [
            "Execution status: `Not started`",
            "Subbundle gate review: `Not started`",
            "Final closure gate: `Not started`",
            "Browser validation analytics: `Not started`",
        ]
        for forbidden_line in forbidden_lines:
            if forbidden_line in content:
                issues.append(f"{path}: completed-stage validation does not allow '{forbidden_line}'")

    return issues


def validate_execution_report(path: Path, stage: str) -> list[str]:
    content = path.read_text(encoding="utf-8")
    issues: list[str] = []

    for heading in EXECUTION_REPORT_HEADINGS:
        if heading not in content:
            issues.append(f"{path}: missing required heading {heading}")

    gate_section = extract_markdown_section(content, "## Subbundle Gate Results")
    if gate_section is not None and SUBBUNDLE_GATE_RESULTS_HEADER not in gate_section:
        issues.append(f"{path}: ## Subbundle Gate Results must include the '{SUBBUNDLE_GATE_RESULTS_HEADER}' table header")

    browser_section = extract_markdown_section(content, "## Browser Validation Analytics")
    if browser_section is not None and BROWSER_ANALYTICS_HEADER not in browser_section:
        issues.append(f"{path}: ## Browser Validation Analytics must include the '{BROWSER_ANALYTICS_HEADER}' table header")

    raw_note_section = extract_markdown_section(content, "## Raw Note Closure")
    if raw_note_section is not None and RAW_NOTE_CLOSURE_HEADER not in raw_note_section:
        issues.append(f"{path}: ## Raw Note Closure must include the '{RAW_NOTE_CLOSURE_HEADER}' table header")

    if stage == "completed":
        for section_name, section_content in (
            ("## Subbundle Gate Results", gate_section),
            ("## Browser Validation Analytics", browser_section),
            ("## Raw Note Closure", raw_note_section),
        ):
            if section_content is None:
                continue

            for marker in PENDING_MARKERS:
                if marker in section_content:
                    issues.append(f"{path}: {section_name} still contains pending marker {marker}")

    return issues


def main() -> int:
    arguments = parse_arguments()
    bundle_path = Path(arguments.bundle_path).resolve()

    issues: list[str] = []
    if not bundle_path.is_dir():
        print(f"Bundle path does not exist: {bundle_path}")
        return 1

    for missing_path in collect_missing_paths(bundle_path, arguments.profile):
        issues.append(f"Missing required path: {missing_path}")

    readme_path = bundle_path / "README.md"
    if readme_path.is_file():
        issues.extend(validate_root_readme(readme_path, arguments.stage))

    plan_path = bundle_path / "plan" / "01-phase-plan.md"
    if plan_path.is_file():
        issues.extend(validate_required_headings(plan_path, PLAN_HEADINGS))
        plan_content = plan_path.read_text(encoding="utf-8")
        if "```mermaid" not in plan_content:
            issues.append(f"{plan_path}: missing mermaid diagram block")

    analysis_risks_path = bundle_path / "analysis" / "02-assumptions-and-risks.md"
    if analysis_risks_path.is_file():
        issues.extend(validate_required_headings(analysis_risks_path, ANALYSIS_RISK_HEADINGS))

    subbundle_directories = sorted(directory for directory in (bundle_path / "subbundles").glob("*") if directory.is_dir())
    if not subbundle_directories:
        issues.append("No subbundle directories found under subbundles/")
    else:
        for subbundle_directory in subbundle_directories:
            readme_path = subbundle_directory / "README.md"
            if not readme_path.is_file():
                issues.append(f"Missing README.md in {subbundle_directory}")
                continue
            issues.extend(validate_subbundle_readme(readme_path, arguments.stage))

    execution_report_path = bundle_path / "reviews" / "01-execution-report.md"
    if execution_report_path.is_file():
        issues.extend(validate_execution_report(execution_report_path, arguments.stage))

    if issues:
        print("Bundle validation failed:")
        for issue in issues:
            print(f"- {issue}")
        return 1

    print(f"Bundle is valid for stage '{arguments.stage}': {bundle_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
