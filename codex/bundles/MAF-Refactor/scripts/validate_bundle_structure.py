#!/usr/bin/env python3
"""Validate the Claude architecture bundle structure and dependency order."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

REQUIRED_ROOT_FILES = [
    "00-READ-ME-FIRST.md",
    "01-EXECUTION-ORDER.md",
    "04-CLAUDE-CODE-EXECUTION-GUIDE.md",
    "05-REVISION-NOTES-AND-CHANGE-IMPACT.md",
    "manifest.json",
    "BUNDLE-CONTENTS.md",
    "architecture/00-csharp-current-state-inventory.md",
    "architecture/01-csharp-boundary-map.md",
    "architecture/02-csharp-dependency-direction.md",
    "architecture/03-csharp-pattern-selection-records.md",
    "architecture/04-csharp-testability-plan.md",
    "architecture/11-change-impact-and-adaptation-map.md",
    "architecture/12-high-risk-cutover-playbook.md",
    "architecture/13-post-refactor-debugging-and-bugfixing.md",
    "architecture/14-lightweight-llm-and-ordinary-chat-foundation.md",
    "architecture/15-exact-code-adaptation-inventory.md",
    "baseline/static-caller-and-registration-snapshot.md",
    "plan/affected-call-chain-matrix.md",
    "plan/architecture-checkpoints.md",
    "plan/cutover-and-rollback-matrix.md",
    "plan/observability-and-regression-plan.md",
    "reviews/csharp-architecture-gate.md",
    "sharedinfo/required-skills.md",
    "claude/CLAUDE.bundle.template.md",
    "claude/MODEL-FALLBACK-AND-HANDOFF.md",
    "claude/START-SUBBUNDLE-PROMPT.md",
    "claude/REGRESSION-BUGFIX-PROMPT.md",
    "claude/FINAL-ARCHITECTURE-REVIEW-PROMPT.md",
]

REQUIRED_README_SECTIONS = [
    "## C# Architecture Impact",
    "## Boundary Ownership",
    "## Dependency Direction",
    "## Pattern Decision",
    "## Testability Contract",
    "## Partial Class Policy",
    "## Architecture Proof Required",
    "## Claude Code execution profile",
    "## High-risk adaptation points",
    "## Safe cutover sequence",
    "## Post-change verification and bugfix procedure",
    "## Durable session handoff",
]

REQUIRED_PROMPT_TAGS = [
    "<role>",
    "<executor_profile>",
    "<mission>",
    "<required_context>",
    "<constraints>",
    "<workflow>",
    "<stop_conditions>",
    "<completion_output>",
]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "bundle_root",
        nargs="?",
        type=Path,
        default=Path(__file__).resolve().parents[1],
    )
    return parser.parse_args()


def main() -> int:
    root = parse_args().bundle_root.resolve()
    errors: list[str] = []

    for relative_path in REQUIRED_ROOT_FILES:
        if not (root / relative_path).is_file():
            errors.append(f"Missing required file: {relative_path}")

    manifest_path = root / "manifest.json"
    if not manifest_path.is_file():
        errors.append("Cannot validate subbundles without manifest.json")
        return report(errors)

    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        errors.append(f"Invalid manifest.json: {exc}")
        return report(errors)

    target = manifest.get("targetExecutor", {})
    if target.get("tool") != "Claude Code":
        errors.append("targetExecutor.tool must be Claude Code")
    if "Fable 5" not in target.get("preferredModelDisplayName", ""):
        errors.append("Preferred model must identify Claude Fable 5")

    subbundles = manifest.get("subbundles")
    if not isinstance(subbundles, list) or not subbundles:
        errors.append("manifest.json must contain a non-empty subbundles array")
        return report(errors)

    ids = [item.get("id") for item in subbundles if isinstance(item, dict)]
    if len(ids) != len(set(ids)):
        errors.append("Subbundle IDs must be unique")

    index_by_id = {subbundle_id: index for index, subbundle_id in enumerate(ids)}
    for item in subbundles:
        if not isinstance(item, dict):
            errors.append("Every subbundle manifest entry must be an object")
            continue

        subbundle_id = item.get("id")
        relative_path = item.get("path")
        if not isinstance(subbundle_id, str) or not subbundle_id:
            errors.append("Subbundle entry has no valid id")
            continue
        if not isinstance(relative_path, str) or not relative_path:
            errors.append(f"Subbundle {subbundle_id} has no valid path")
            continue

        subbundle_root = root / relative_path
        readme = subbundle_root / "README.md"
        prompt = subbundle_root / "CLAUDE-CODE-PROMPT.md"
        proof = subbundle_root / "proof-manifest.template.json"
        handoff = subbundle_root / "proof" / "SESSION-HANDOFF.template.md"
        for path in (readme, prompt, proof, handoff):
            if not path.is_file():
                errors.append(f"{subbundle_id}: missing {path.relative_to(subbundle_root)}")

        if readme.is_file():
            text = readme.read_text(encoding="utf-8")
            for section in REQUIRED_README_SECTIONS:
                if section not in text:
                    errors.append(f"{subbundle_id}: README missing section {section}")
            if "GPT-5.6" in text or "CODEX-PROMPT" in text:
                errors.append(f"{subbundle_id}: stale Claude Code executor text remains")

        if prompt.is_file():
            prompt_text = prompt.read_text(encoding="utf-8")
            for tag in REQUIRED_PROMPT_TAGS:
                if tag not in prompt_text:
                    errors.append(f"{subbundle_id}: prompt missing tag {tag}")
            if "return only a plan" in prompt_text.lower():
                errors.append(f"{subbundle_id}: prompt may incorrectly permit plan-only completion")

        dependencies = item.get("dependsOn", [])
        if not isinstance(dependencies, list):
            errors.append(f"{subbundle_id}: dependsOn must be an array")
            continue
        for dependency in dependencies:
            if dependency not in index_by_id:
                errors.append(f"{subbundle_id}: unknown dependency {dependency}")
                continue
            if index_by_id[dependency] >= index_by_id[subbundle_id]:
                errors.append(
                    f"{subbundle_id}: dependency {dependency} must appear earlier in manifest order"
                )

        if item.get("checkpoint") is True:
            checkpoint_template = subbundle_root / "checkpoint-result.template.md"
            if not checkpoint_template.is_file():
                errors.append(f"{subbundle_id}: checkpoint template is missing")

    if len(subbundles) != 19:
        errors.append(f"Expected 19 subbundles, found {len(subbundles)}")

    for stale in root.rglob("CODEX-PROMPT.md"):
        errors.append(f"Stale Codex prompt remains: {stale.relative_to(root)}")

    for cache in root.rglob("__pycache__"):
        errors.append(f"Python cache directory must not be packaged: {cache.relative_to(root)}")
    for bytecode in root.rglob("*.pyc"):
        errors.append(f"Python bytecode must not be packaged: {bytecode.relative_to(root)}")

    return report(errors)


def report(errors: list[str]) -> int:
    if errors:
        print("Bundle validation failed:")
        for error in errors:
            print(f"- {error}")
        return 1

    print("Bundle validation passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
