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

SUBBUNDLE_HEADING_GROUPS = [
    ("## Status",),
    ("## Objective",),
    ("## Covered Inputs", "## Covered Notes"),
    ("## Exact Source References",),
    ("## Deliverables", "## Scope"),
    ("## Implementation Steps",),
    ("## Do Not Do",),
    ("## Acceptance Checklist",),
    ("## Proof Required",),
    ("## Suggested Agent Prompt",),
]

FEEDBACK_EXECUTION_REPORT_HEADINGS = [
    "## Status",
    "## Raw Note Closure",
]


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Validate a CanDoItAll bundle structure.")
    parser.add_argument("bundle_path", help="Path to the bundle root.")
    parser.add_argument("--profile", choices=("feedback", "initiative"), default="feedback")
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


def validate_feedback_execution_report(path: Path) -> list[str]:
    content = path.read_text(encoding="utf-8")
    issues: list[str] = []

    for heading in FEEDBACK_EXECUTION_REPORT_HEADINGS:
        if heading not in content:
            issues.append(f"{path}: missing required heading {heading}")

    raw_note_section = extract_markdown_section(content, "## Raw Note Closure")
    if raw_note_section is None:
        return issues

    if "| Raw note | Status | Proof |" not in raw_note_section:
        issues.append(f"{path}: ## Raw Note Closure must include the '| Raw note | Status | Proof |' table header")

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

    if issues:
        print("Bundle validation failed:")
        for issue in issues:
            print(f"- {issue}")
        return 1

    print(f"Bundle is valid: {bundle_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
