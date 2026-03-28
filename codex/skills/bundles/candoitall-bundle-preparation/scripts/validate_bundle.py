#!/usr/bin/env python3

from __future__ import annotations

import argparse
import re
from pathlib import Path

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

FEEDBACK_EXECUTION_REPORT_HEADINGS = [
    "## Status",
    "## Subbundle Gate Results",
    "## Browser Validation Analytics",
    "## Analytics Review",
    "## Raw Note Closure",
]

PHASE_PLAN_HEADINGS = [
    "## Phase Sequence",
    "## Subbundle Dependency Map",
    "## Critical Subbundles",
    "## Phase Gates",
]

ASSUMPTIONS_AND_RISKS_HEADINGS = [
    "## Assumptions",
    "## Critical Path Risks",
    "## Validation Risks",
    "## Reopen Triggers",
]

FINAL_ALLOWED_SUBBUNDLE_STATUSES = {
    "Completed",
    "Blocked",
}

PENDING_VALUES = {
    "Not started",
    "Pending",
    "Pending implementation",
}


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


def validate_subbundle_readme(path: Path) -> list[str]:
    content = path.read_text(encoding="utf-8")
    issues: list[str] = []

    for heading_group in SUBBUNDLE_HEADING_GROUPS:
        if not any(heading in content for heading in heading_group):
            issues.append(f"{path}: missing one of {', '.join(heading_group)}")

    issues.extend(validate_exact_source_references(path, content))
    issues.extend(validate_required_bullets(path, content, "## Prerequisites"))
    issues.extend(validate_required_bullets(path, content, "## Dependency Impact"))
    issues.extend(validate_required_bullets(path, content, "## Progression Gate"))
    issues.extend(validate_validation_depth(path, content))
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


def normalize_markdown_value(value: str) -> str:
    exact_match = re.fullmatch(r"`([^`]+)`", value.strip())
    if exact_match is not None:
        return exact_match.group(1).strip()

    return value.strip()


def extract_bullet_values(section_content: str) -> list[str]:
    values: list[str] = []
    for line in section_content.splitlines():
        stripped = line.strip()
        if not stripped.startswith("- "):
            continue

        values.append(normalize_markdown_value(stripped[2:].strip()))

    return values


def validate_required_bullets(path: Path, content: str, heading: str) -> list[str]:
    section_content = extract_markdown_section(content, heading)
    if section_content is None:
        return []

    values = extract_bullet_values(section_content)
    if values:
        return []

    return [f"{path}: {heading} must include at least one markdown bullet"]


def validate_validation_depth(path: Path, content: str) -> list[str]:
    section_content = extract_markdown_section(content, "## Validation Depth")
    if section_content is None:
        return []

    values = extract_bullet_values(section_content)
    if not values:
        return [f"{path}: ## Validation Depth must include at least one markdown bullet"]

    if any(value.startswith(("Standard", "Critical")) for value in values):
        return []

    return [f"{path}: ## Validation Depth must start with 'Standard' or 'Critical'"]


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


def validate_feedback_execution_report(path: Path) -> list[str]:
    content = path.read_text(encoding="utf-8")
    issues: list[str] = []

    for heading in FEEDBACK_EXECUTION_REPORT_HEADINGS:
        if heading not in content:
            issues.append(f"{path}: missing required heading {heading}")

    gate_section = extract_markdown_section(content, "## Subbundle Gate Results")
    if gate_section is None:
        return issues

    if "| Subbundle | Gate | Inputs covered | Dependencies checked | Proof review | Result |" not in gate_section:
        issues.append(
            f"{path}: ## Subbundle Gate Results must include the '| Subbundle | Gate | Inputs covered | Dependencies checked | Proof review | Result |' table header"
        )

    raw_note_section = extract_markdown_section(content, "## Raw Note Closure")
    if raw_note_section is None:
        return issues

    if "| Raw note | Status | Proof |" not in raw_note_section:
        issues.append(f"{path}: ## Raw Note Closure must include the '| Raw note | Status | Proof |' table header")

    browser_analytics_section = extract_markdown_section(content, "## Browser Validation Analytics")
    if browser_analytics_section is None:
        return issues

    if "| Subbundle | Route | Viewport | Playwright MCP evidence | Screenshots | Result |" not in browser_analytics_section:
        issues.append(
            f"{path}: ## Browser Validation Analytics must include the '| Subbundle | Route | Viewport | Playwright MCP evidence | Screenshots | Result |' table header"
        )

    return issues


def validate_required_headings(path: Path, headings: list[str]) -> list[str]:
    content = path.read_text(encoding="utf-8")
    issues: list[str] = []

    for heading in headings:
        if heading not in content:
            issues.append(f"{path}: missing required heading {heading}")

    return issues


def validate_phase_plan(path: Path) -> list[str]:
    issues = validate_required_headings(path, PHASE_PLAN_HEADINGS)
    content = path.read_text(encoding="utf-8")

    dependency_map = extract_markdown_section(content, "## Subbundle Dependency Map")
    if dependency_map is not None and "```mermaid" not in dependency_map:
        issues.append(f"{path}: ## Subbundle Dependency Map must include a mermaid diagram")

    critical_subbundles = extract_markdown_section(content, "## Critical Subbundles")
    if critical_subbundles is not None and not extract_bullet_values(critical_subbundles):
        issues.append(f"{path}: ## Critical Subbundles must include at least one markdown bullet")

    phase_gates = extract_markdown_section(content, "## Phase Gates")
    if phase_gates is not None and not extract_bullet_values(phase_gates):
        issues.append(f"{path}: ## Phase Gates must include at least one markdown bullet")

    return issues


def validate_assumptions_and_risks(path: Path) -> list[str]:
    issues = validate_required_headings(path, ASSUMPTIONS_AND_RISKS_HEADINGS)
    content = path.read_text(encoding="utf-8")

    for heading in ASSUMPTIONS_AND_RISKS_HEADINGS:
        section_content = extract_markdown_section(content, heading)
        if section_content is None:
            continue

        if extract_bullet_values(section_content):
            continue

        issues.append(f"{path}: {heading} must include at least one markdown bullet")

    return issues


def extract_first_status_value(content: str) -> str | None:
    status_section = extract_markdown_section(content, "## Status")
    if status_section is None:
        return None

    values = extract_bullet_values(status_section)
    if not values:
        return None

    first_value = values[0]
    if ":" in first_value:
        _, first_value = first_value.split(":", 1)

    return normalize_markdown_value(first_value)


def extract_table_rows(section_content: str) -> list[list[str]]:
    rows: list[list[str]] = []

    for line in section_content.splitlines():
        stripped = line.strip()
        if not stripped.startswith("|"):
            continue

        columns = [column.strip() for column in stripped.strip("|").split("|")]
        if not columns:
            continue

        if all(re.fullmatch(r"[:\- ]+", column) for column in columns):
            continue

        rows.append(columns)

    return rows


def data_table_rows(section_content: str) -> list[list[str]]:
    rows = extract_table_rows(section_content)
    if len(rows) <= 1:
        return []

    return rows[1:]


def validate_completed_root_readme(path: Path) -> list[str]:
    content = path.read_text(encoding="utf-8")
    issues: list[str] = []

    if "- Bundle readiness gate: `Not run`" in content:
        issues.append(f"{path}: final closure cannot leave bundle readiness gate as `Not run`")

    if "- Execution status: `Not started`" in content:
        issues.append(f"{path}: final closure cannot leave execution status as `Not started`")

    if "- Subbundle gate review: `Not started`" in content:
        issues.append(f"{path}: final closure cannot leave subbundle gate review as `Not started`")

    if "- Final closure gate: `Not run`" in content:
        issues.append(f"{path}: final closure cannot leave final closure gate as `Not run`")

    return issues


def validate_completed_subbundles(subbundle_directories: list[Path]) -> list[str]:
    issues: list[str] = []

    for subbundle_directory in subbundle_directories:
        readme_path = subbundle_directory / "README.md"
        if not readme_path.is_file():
            continue

        content = readme_path.read_text(encoding="utf-8")
        status = extract_first_status_value(content)
        if status is None:
            issues.append(f"{readme_path}: final closure requires an explicit subbundle status bullet")
            continue

        if status in FINAL_ALLOWED_SUBBUNDLE_STATUSES:
            continue

        issues.append(f"{readme_path}: final closure requires status `Completed` or `Blocked`, found `{status}`")

    return issues


def validate_completed_execution_report(path: Path) -> list[str]:
    content = path.read_text(encoding="utf-8")
    issues: list[str] = []

    report_status = extract_first_status_value(content)
    if report_status in PENDING_VALUES:
        issues.append(f"{path}: final closure cannot leave execution report status as `{report_status}`")

    gate_section = extract_markdown_section(content, "## Subbundle Gate Results")
    if gate_section is not None:
        gate_rows = data_table_rows(gate_section)
        if not gate_rows:
            issues.append(f"{path}: final closure requires at least one populated subbundle gate result row")
        else:
            for row in gate_rows:
                if len(row) < 6:
                    issues.append(f"{path}: subbundle gate result row is incomplete: {' | '.join(row)}")
                    continue

                if normalize_markdown_value(row[5]) in PENDING_VALUES:
                    issues.append(f"{path}: subbundle gate result cannot remain pending: {' | '.join(row)}")

    browser_section = extract_markdown_section(content, "## Browser Validation Analytics")
    if browser_section is not None:
        browser_rows = data_table_rows(browser_section)
        if not browser_rows:
            issues.append(f"{path}: final closure requires at least one populated browser validation analytics row")
        else:
            for row in browser_rows:
                if len(row) < 6:
                    issues.append(f"{path}: browser validation row is incomplete: {' | '.join(row)}")
                    continue

                if normalize_markdown_value(row[5]) in PENDING_VALUES:
                    issues.append(f"{path}: browser validation result cannot remain pending: {' | '.join(row)}")

    raw_note_section = extract_markdown_section(content, "## Raw Note Closure")
    if raw_note_section is not None:
        raw_note_rows = data_table_rows(raw_note_section)
        if not raw_note_rows:
            issues.append(f"{path}: final closure requires at least one populated raw note closure row")
        else:
            for row in raw_note_rows:
                if len(row) < 3:
                    issues.append(f"{path}: raw note closure row is incomplete: {' | '.join(row)}")
                    continue

                if normalize_markdown_value(row[1]) in PENDING_VALUES:
                    issues.append(f"{path}: raw note cannot remain pending at final closure: {' | '.join(row)}")

    return issues


def main() -> int:
    arguments = parse_arguments()
    bundle_path = Path(arguments.bundle_path).resolve()

    issues: list[str] = []
    if not bundle_path.is_dir():
        print(f"Bundle path does not exist: {bundle_path}")
        return 1

    missing_paths = collect_missing_paths(bundle_path, arguments.profile)
    for missing_path in missing_paths:
        issues.append(f"Missing required path: {missing_path}")

    phase_plan_path = bundle_path / "plan" / "01-phase-plan.md"
    if phase_plan_path.is_file():
        issues.extend(validate_phase_plan(phase_plan_path))

    assumptions_and_risks_path = bundle_path / "analysis" / "02-assumptions-and-risks.md"
    if assumptions_and_risks_path.is_file():
        issues.extend(validate_assumptions_and_risks(assumptions_and_risks_path))

    subbundle_directories = sorted(directory for directory in (bundle_path / "subbundles").glob("*") if directory.is_dir())
    if not subbundle_directories:
        issues.append("No subbundle directories found under subbundles/")
    else:
        for subbundle_directory in subbundle_directories:
            readme_path = subbundle_directory / "README.md"
            if not readme_path.is_file():
                issues.append(f"Missing README.md in {subbundle_directory}")
                continue
            issues.extend(validate_subbundle_readme(readme_path))

    if arguments.profile == "feedback":
        execution_report_path = bundle_path / "reviews" / "01-execution-report.md"
        if execution_report_path.is_file():
            issues.extend(validate_feedback_execution_report(execution_report_path))

    if arguments.stage == "completed":
        readme_path = bundle_path / "README.md"
        if readme_path.is_file():
            issues.extend(validate_completed_root_readme(readme_path))

        issues.extend(validate_completed_subbundles(subbundle_directories))

        execution_report_path = bundle_path / "reviews" / "01-execution-report.md"
        if execution_report_path.is_file():
            issues.extend(validate_completed_execution_report(execution_report_path))

    if issues:
        print("Bundle validation failed:")
        for issue in issues:
            print(f"- {issue}")
        return 1

    print(f"Bundle is valid: {bundle_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
