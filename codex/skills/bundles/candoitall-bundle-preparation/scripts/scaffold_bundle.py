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

ASSUMPTIONS_AND_RISKS_TEMPLATE = """# Assumptions And Risks

## Assumptions

- Record the assumptions made during bundle preparation.

## Critical Path Risks

- Identify the subbundles that unlock later work and the regressions that would force rework if they are wrong.

## Validation Risks

- Record where proof may be weak, blocked, environment-dependent, or expensive to reproduce.

## Reopen Triggers

- List the conditions that must reopen an earlier subbundle instead of letting later work continue.
"""

PHASE_PLAN_TEMPLATE = """# Phase Plan

## Phase Sequence

1. Describe the intended execution order.
2. Call out the validator checkpoints between phases.
3. End with the final closure audit.

## Subbundle Dependency Map

```mermaid
gantt
title Replace with the real subbundle dependency and validation map
dateFormat  YYYY-MM-DD
section Foundations
Foundation subbundle :done, foundation, 2026-01-01, 1d
section Follow-on work
Dependent subbundle :after foundation, dependent, 1d
```

- Replace the placeholder map with the real subbundle order, prerequisites, and validation checkpoints.

## Critical Subbundles

- Identify the foundation subbundles whose correctness unlocks later phases.
- Assign `Standard`, `Behavioral`, or `Governed` proof to each subbundle.
- State the downstream check required before dependent subbundles may continue. Full manifests and transcripts apply only to `Governed` proof.

## Phase Gates

- Gate after preparation: run the bundle validator and repair failures.
- Gate before each subbundle: confirm prerequisites are complete and still valid.
- Gate after each subbundle: capture proof, review screenshots, and decide whether downstream work may continue.
- Gate before closure: rerun validators, close raw notes, and reopen anything with weak proof.

## UI Target Policy

- CanDoItAll applications target large-screen desktop viewports. Do not add small/medium/mobile tuning unless explicitly requested.
- Reusable basic `CanDoItAll.Components.BaseLib` work validates small, medium, and large viewports.
"""


def to_title_case(bundle_name: str) -> str:
    words = re.split(r"[-_]+", bundle_name.strip())
    return " ".join(word.capitalize() for word in words if word)


def load_template(template_directory: Path, template_name: str, replacements: dict[str, str]) -> str:
    template_path = template_directory / template_name
    content = template_path.read_text(encoding="utf-8")
    for key, value in replacements.items():
        content = content.replace(f"{{{{{key}}}}}", value)
    return content


def ensure_file(path: Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if not path.exists():
        path.write_text(content, encoding="utf-8")


def subbundle_slug(name: str) -> str:
    slug = re.sub(r"[^a-z0-9]+", "-", name.lower()).strip("-")
    return slug or "workstream"


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Create a CanDoItAll bundle scaffold.")
    parser.add_argument("bundle_name", help="Folder name for the bundle.")
    parser.add_argument("--root", required=True, help="Directory where the bundle should be created.")
    parser.add_argument("--profile", choices=("feedback", "initiative"), default="feedback")
    parser.add_argument("--title", help="Optional human-readable title. Defaults to title-cased bundle name.")
    parser.add_argument("--source", action="append", default=[], help="Source artifact or path to list in the bundle.")
    parser.add_argument(
        "--subbundle",
        action="append",
        default=[],
        help="Seed subbundle name. Repeat for multiple subbundles.",
    )
    return parser.parse_args()


def main() -> int:
    arguments = parse_arguments()
    skill_root = Path(__file__).resolve().parents[1]
    template_directory = skill_root / "assets" / "templates"

    bundle_root = Path(arguments.root).resolve() / arguments.bundle_name
    bundle_root.mkdir(parents=True, exist_ok=False)

    bundle_title = arguments.title or to_title_case(arguments.bundle_name)
    replacements = {
        "BUNDLE_NAME": arguments.bundle_name,
        "BUNDLE_TITLE": bundle_title,
        "PROFILE_NAME": arguments.profile,
    }

    directories = [*COMMON_DIRECTORIES, *PROFILE_DIRECTORIES[arguments.profile]]
    for directory in directories:
        (bundle_root / directory).mkdir(parents=True, exist_ok=True)

    ensure_file(bundle_root / "README.md", load_template(template_directory, "root-readme-template.md", replacements))
    ensure_file(
        bundle_root / "inputs" / "00-original-request.md",
        "# Original Request\n\nPaste the raw user request or source note here without rewriting it.\n",
    )

    source_lines = ["# Source Artifacts", ""]
    if arguments.source:
        source_lines.extend(f"- `{source}`" for source in arguments.source)
    else:
        source_lines.append("- Add every source artifact path here.")
    source_lines.append("")
    ensure_file(bundle_root / "inputs" / "01-source-artifacts.md", "\n".join(source_lines))
    ensure_file(
        bundle_root / "inputs" / "02-structured-input.md",
        load_template(template_directory, "structured-input-template.md", replacements),
    )

    ensure_file(bundle_root / "analysis" / "01-current-state.md", "# Current State\n\nDocument the relevant repo state, affected files, and evidence gathered from real inspection.\n")
    ensure_file(bundle_root / "analysis" / "02-assumptions-and-risks.md", ASSUMPTIONS_AND_RISKS_TEMPLATE)
    ensure_file(bundle_root / "requirements" / "01-normalized-requirements.md", "# Normalized Requirements\n\nConvert the raw inputs into concrete, testable requirements with observable success criteria.\n")
    ensure_file(bundle_root / "architecture" / "01-target-solution.md", "# Target Solution\n\nDescribe the intended end state, important boundaries, and allowed side effects.\n")
    ensure_file(bundle_root / "plan" / "01-phase-plan.md", PHASE_PLAN_TEMPLATE)
    ensure_file(
        bundle_root / "traceability" / "01-requirement-traceability.md",
        load_template(template_directory, "traceability-template.md", replacements),
    )
    ensure_file(
        bundle_root / "shared-prompts" / "implementation-prompt.md",
        "# Implementation Prompt\n\nWrite an outcome-first reusable implementation prompt. Include owned inputs, hard constraints, prerequisite checks, smallest-change guidance, required proof, status-update rules, and stop conditions.\n",
    )
    ensure_file(
        bundle_root / "shared-prompts" / "qa-prompt.md",
        "# QA Prompt\n\nWrite an outcome-first reusable QA prompt. Include coverage checks, dependency gates, proof review, browser or host validation when applicable, raw-note closure, and blocker handling.\n",
    )
    ensure_file(
        bundle_root / "reviews" / "00-bundle-self-review.md",
        load_template(template_directory, "self-review-template.md", replacements),
    )
    ensure_file(
        bundle_root / "reviews" / "01-execution-report.md",
        load_template(template_directory, "execution-report-template.md", replacements),
    )

    if arguments.profile == "initiative":
        ensure_file(bundle_root / "inventories" / "01-scope-inventory.md", "# Scope Inventory\n\nInventory the relevant code, assets, or dependencies for the planned work.\n")
        ensure_file(bundle_root / "templates" / "subbundle-readme-template.md", load_template(template_directory, "subbundle-readme-template.md", replacements))

    for index, subbundle_name in enumerate(arguments.subbundle, start=1):
        slug = subbundle_slug(subbundle_name)
        subbundle_directory = bundle_root / "subbundles" / f"{index:02d}-{slug}"
        subbundle_directory.mkdir(parents=True, exist_ok=True)
        subbundle_replacements = {
            **replacements,
            "SUBBUNDLE_TITLE": subbundle_name,
        }
        ensure_file(
            subbundle_directory / "README.md",
            load_template(template_directory, "subbundle-readme-template.md", subbundle_replacements),
        )

    print(f"Created bundle scaffold at {bundle_root}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
