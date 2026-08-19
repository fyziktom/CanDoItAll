#!/usr/bin/env python3
"""Validate the prepared or executed Simple LLM Chats hardening bundle."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any


REQUIRED_ROOT_FILES = (
    "README.md",
    "manifest.json",
    "bundle-index.json",
    "bundle-status.json",
    "requirements-index.md",
    "EXECUTION-PROGRESS.md",
    "CODEX-EXECUTION-CONTRACT.md",
    "test-budget.json",
    "traceability/requirements-matrix.md",
    "traceability/input-coverage.md",
    "traceability/finding-closure.md",
    "analysis/01-findings-register.md",
    "architecture/00-csharp-current-state-inventory.md",
    "architecture/01-csharp-boundary-map.md",
    "architecture/02-csharp-dependency-direction.md",
    "architecture/03-csharp-pattern-selection-records.md",
    "architecture/04-csharp-testability-plan.md",
    "plan/03-architecture-checkpoints.md",
    "reviews/CP0-BASELINE-AND-PROOF.md",
    "reviews/CP1-BACKEND-HARDENING.md",
    "reviews/CP2-STREAMING-API.md",
    "reviews/FINAL-RELEASE-DECISION.md",
)


def read_json(path: Path) -> Any:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise ValueError(f"Unable to read valid JSON from {path}: {exc}") from exc


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--bundle-root", type=Path, default=Path("."))
    parser.add_argument(
        "--stage",
        choices=("prepared", "executing", "closed"),
        default="prepared",
    )
    args = parser.parse_args()
    root = args.bundle_root.resolve()
    errors: list[str] = []

    for relative in REQUIRED_ROOT_FILES:
        if not (root / relative).is_file():
            errors.append(f"Missing required file: {relative}")

    manifest_path = root / "manifest.json"
    index_path = root / "bundle-index.json"
    status_path = root / "bundle-status.json"
    if errors:
        print("\n".join(f"ERROR: {error}" for error in errors), file=sys.stderr)
        return 1

    try:
        manifest = read_json(manifest_path)
        index = read_json(index_path)
        status = read_json(status_path)
    except ValueError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1

    manifest_subbundles = manifest.get("subbundles", [])
    index_subbundles = index.get("subbundles", [])
    manifest_ids = [item.get("id") for item in manifest_subbundles]
    index_ids = [item.get("id") for item in index_subbundles]
    expected_ids = [f"SB{number:02d}" for number in range(14)]
    if manifest_ids != expected_ids:
        errors.append(
            f"manifest.json subbundle order must be {expected_ids}, got {manifest_ids}"
        )
    if index_ids != expected_ids:
        errors.append(
            f"bundle-index.json subbundle order must be {expected_ids}, got {index_ids}"
        )

    requirement_ids = {
        item.get("id") for item in index.get("requirements", []) if item.get("id")
    }
    if len(requirement_ids) != 35:
        errors.append(
            f"Expected 35 unique requirements, found {len(requirement_ids)}"
        )

    all_owned: set[str] = set()
    for item in index_subbundles:
        sb_id = item.get("id", "")
        relative = item.get("path", "")
        sb_root = root / relative
        for file_name in (
            "README.md",
            "AGENT-PROMPT.md",
            "SESSION-HANDOFF.md",
            "acceptance-evidence.md",
            "proof-manifest.json",
        ):
            if not (sb_root / file_name).is_file():
                errors.append(f"{sb_id}: missing {relative}/{file_name}")

        proof_path = sb_root / "proof-manifest.json"
        if proof_path.is_file():
            try:
                proof = read_json(proof_path)
            except ValueError as exc:
                errors.append(str(exc))
                continue
            if proof.get("subbundle") != sb_id:
                errors.append(
                    f"{sb_id}: proof manifest declares {proof.get('subbundle')}"
                )
            declared = set(item.get("requirements", []))
            proof_requirements = set(proof.get("requirements", []))
            if declared != proof_requirements:
                errors.append(
                    f"{sb_id}: proof requirements differ from bundle index"
                )
            all_owned.update(declared)
            readme_text = (sb_root / "README.md").read_text(
                encoding="utf-8", errors="replace"
            )
            required_headings = (
                "## C# Architecture Impact",
                "## Boundary Ownership",
                "## Dependency Direction",
                "## Pattern Decision",
                "## Testability Contract",
                "## Partial Class Policy",
                "## Architecture Proof Required",
            )
            for heading in required_headings:
                if heading not in readme_text:
                    errors.append(f"{sb_id}: README missing heading {heading}")

            acceptance = proof.get("acceptance", [])
            if not acceptance:
                errors.append(f"{sb_id}: proof manifest has no acceptance criteria")
            if args.stage == "closed":
                unsatisfied = [
                    item.get("criterion", "<unknown>")
                    for item in acceptance
                    if not item.get("satisfied") or not item.get("evidence")
                ]
                if unsatisfied:
                    errors.append(
                        f"{sb_id}: closed stage has unsatisfied acceptance: {unsatisfied}"
                    )
                commit = proof.get("implementationCommit", "")
                if len(commit) != 40:
                    errors.append(
                        f"{sb_id}: closed stage requires a 40-character commit SHA"
                    )

    missing_owners = requirement_ids - all_owned
    unknown_owned = all_owned - requirement_ids
    if missing_owners:
        errors.append(f"Requirements without an owner: {sorted(missing_owners)}")
    if unknown_owned:
        errors.append(f"Unknown owned requirements: {sorted(unknown_owned)}")

    checkpoint_paths = {
        item.get("review") for item in manifest.get("checkpoints", [])
    }
    expected_checkpoints = {
        "reviews/CP0-BASELINE-AND-PROOF.md",
        "reviews/CP1-BACKEND-HARDENING.md",
        "reviews/CP2-STREAMING-API.md",
        "reviews/FINAL-RELEASE-DECISION.md",
    }
    if checkpoint_paths != expected_checkpoints:
        errors.append(
            "Manifest checkpoint review paths do not match the canonical set"
        )

    if status.get("currentSubbundle") != "SB00" and args.stage == "prepared":
        errors.append("Prepared bundle must start with currentSubbundle SB00")

    if errors:
        print("\n".join(f"ERROR: {error}" for error in errors), file=sys.stderr)
        return 1

    print(
        f"Bundle validation passed: {len(expected_ids)} subbundles, "
        f"{len(requirement_ids)} requirements, stage={args.stage}."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
