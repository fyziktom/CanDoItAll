#!/usr/bin/env python3

from __future__ import annotations

import argparse
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

    if issues:
        print("Bundle validation failed:")
        for issue in issues:
            print(f"- {issue}")
        return 1

    print(f"Bundle is valid: {bundle_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
