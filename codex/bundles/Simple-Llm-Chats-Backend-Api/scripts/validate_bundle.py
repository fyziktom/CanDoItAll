#!/usr/bin/env python3
"""Validate structure, traceability, dependencies, and proof templates for this bundle."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import re
import sys


REQUIRED_FILES = [
    "README.md",
    "EXECUTIVE-SUMMARY-CS.md",
    "CODEX-EXECUTION-CONTRACT.md",
    "CHANGE-CONTROL.md",
    "INSTALLATION.md",
    "EXECUTION-PROGRESS.md",
    "CLOSURE-AUDIT.md",
    "manifest.json",
    "bundle-status.json",
    "requirements-index.md",
    "test-budget.json",
    "source/01-current-user-request.md",
    "source/02-original-architect-notes.md",
    "source/03-repository-evidence.md",
    "source/04-sharedinfo-skills-used.md",
    "source/05-thinking-effort-follow-up.md",
    "architecture/00-current-state.md",
    "architecture/01-feature-block-review.md",
    "architecture/02-canonical-model.md",
    "architecture/03-generic-conversation-adapter.md",
    "architecture/04-project-boundaries.md",
    "architecture/05-persistence-and-transactions.md",
    "architecture/06-profile-lifecycle-and-security.md",
    "architecture/07-operation-idempotency-recovery-audit.md",
    "architecture/08-http-api-contract.md",
    "architecture/09-enterprise-chatbot-readiness.md",
    "architecture/10-decision-register.md",
    "architecture/11-deferred-work.md",
    "architecture/12-class-and-interface-plan.md",
    "architecture/13-turn-data-flow.md",
    "plan/00-execution-principles.md",
    "plan/01-execution-order.md",
    "plan/02-dependency-graph.md",
    "plan/03-traceability.md",
    "plan/04-test-budget-and-gates.md",
    "plan/05-checkpoints.md",
    "plan/06-release-gate.md",
    "plan/07-command-catalog.md",
    "inventories/planned-source-hotspots.md",
    "inventories/future-case-matrix.md",
    "specifications/state-machines.md",
    "specifications/error-catalog.md",
    "specifications/api-resource-shapes.md",
    "specifications/persistence-invariants.md",
    "reviews/CP0-BASELINE-DECISION.md",
    "reviews/CP1-BACKEND-ARCHITECTURE.md",
    "reviews/CP2-API-ARCHITECTURE.md",
    "reviews/FINAL-MERGE-DECISION.md",
    "scripts/README.md",
    "scripts/validate_bundle.py",
    "scripts/check_test_policy.py",
    "scripts/check_architecture_boundaries.py",
]

ACCEPTANCE_PATTERN = re.compile(r"^- \[[ xX]\] (.+)$")


def load_json(path: Path) -> object:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise ValueError(f"Invalid JSON file: {path}: {exc}") from exc


def read_acceptance(readme: Path) -> list[str]:
    criteria: list[str] = []
    in_section = False
    for line in readme.read_text(encoding="utf-8").splitlines():
        if line.strip() == "## Acceptance criteria":
            in_section = True
            continue
        if in_section and line.startswith("## "):
            break
        match = ACCEPTANCE_PATTERN.match(line)
        if in_section and match:
            criteria.append(match.group(1).strip())
    return criteria


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--bundle-root", type=Path, required=True)
    args = parser.parse_args()
    root = args.bundle_root.resolve()

    errors: list[str] = []
    if not root.is_dir():
        print(f"Bundle root does not exist: {root}")
        return 1

    for relative in REQUIRED_FILES:
        path = root / relative
        if not path.is_file():
            errors.append(f"Missing required file: {relative}")
        elif path.stat().st_size == 0:
            errors.append(f"Required file is empty: {relative}")

    manifest_path = root / "manifest.json"
    manifest: dict[str, object] | None = None
    if manifest_path.is_file():
        try:
            loaded = load_json(manifest_path)
            if not isinstance(loaded, dict):
                errors.append("manifest.json must contain an object.")
            else:
                manifest = loaded
        except ValueError as exc:
            errors.append(str(exc))

    if manifest is not None:
        subbundles = manifest.get("subbundles", [])
        if not isinstance(subbundles, list):
            errors.append("manifest subbundles must be an array.")
            subbundles = []

        expected_ids = [f"SB{index:02d}" for index in range(12)]
        actual_ids = [item.get("id") for item in subbundles if isinstance(item, dict)]
        if actual_ids != expected_ids:
            errors.append(f"Unexpected subbundle order: expected {expected_ids}, got {actual_ids}")

        known_ids = set(actual_ids)
        completed_ids: set[str] = set()
        for item in subbundles:
            if not isinstance(item, dict):
                errors.append("Every subbundle manifest entry must be an object.")
                continue
            subbundle_id = str(item.get("id", ""))
            slug = str(item.get("slug", ""))
            tier = str(item.get("proofTier", ""))
            dependencies = item.get("dependsOn", [])
            if tier not in {"Governed", "Behavioral"}:
                errors.append(f"Invalid proof tier for {subbundle_id}: {tier}")
            if not isinstance(dependencies, list):
                errors.append(f"dependsOn must be an array for {subbundle_id}")
                dependencies = []
            for dependency in dependencies:
                if dependency not in known_ids:
                    errors.append(f"Unknown dependency {dependency} for {subbundle_id}")
                if dependency not in completed_ids:
                    errors.append(
                        f"Dependency {dependency} for {subbundle_id} is not earlier in execution order"
                    )

            directory = root / "subbundles" / f"{subbundle_id}-{slug}"
            readme = directory / "README.md"
            proof = directory / "proof" / "proof-manifest.template.json"
            for relative in ["README.md", "SESSION-HANDOFF.md", "proof/proof-manifest.template.json"]:
                if not (directory / relative).is_file():
                    errors.append(f"Missing subbundle file: {directory.relative_to(root) / relative}")

            if readme.is_file() and proof.is_file():
                try:
                    proof_data = load_json(proof)
                    if not isinstance(proof_data, dict):
                        errors.append(f"Proof template must be an object: {proof.relative_to(root)}")
                    else:
                        if proof_data.get("subbundleId") != subbundle_id:
                            errors.append(f"Proof subbundleId mismatch: {proof.relative_to(root)}")
                        if proof_data.get("proofTier") != tier:
                            errors.append(f"Proof tier mismatch: {proof.relative_to(root)}")
                        expected_acceptance = read_acceptance(readme)
                        actual_acceptance = [
                            entry.get("criterion")
                            for entry in proof_data.get("acceptance", [])
                            if isinstance(entry, dict)
                        ]
                        if actual_acceptance != expected_acceptance:
                            errors.append(
                                f"Acceptance mismatch between README and proof template: {subbundle_id}"
                            )
                        if not expected_acceptance:
                            errors.append(f"No acceptance criteria found for {subbundle_id}")
                except ValueError as exc:
                    errors.append(str(exc))
            completed_ids.add(subbundle_id)

        checkpoints = manifest.get("checkpoints", [])
        if not isinstance(checkpoints, list):
            errors.append("manifest checkpoints must be an array.")
        else:
            for checkpoint in checkpoints:
                if not isinstance(checkpoint, dict):
                    errors.append("Every checkpoint entry must be an object.")
                    continue
                review = checkpoint.get("review")
                if not isinstance(review, str) or not (root / review).is_file():
                    errors.append(f"Missing checkpoint review file: {review}")

        test_policy = manifest.get("testPolicy")
        if not isinstance(test_policy, str) or not (root / test_policy).is_file():
            errors.append(f"Missing manifest test policy: {test_policy}")

        status_path = root / "bundle-status.json"
        if status_path.is_file():
            try:
                status = load_json(status_path)
                if isinstance(status, dict):
                    status_ids = set((status.get("subbundles") or {}).keys())
                    if status_ids != known_ids:
                        errors.append(
                            f"bundle-status subbundle keys differ from manifest: {sorted(status_ids)}"
                        )
                else:
                    errors.append("bundle-status.json must contain an object.")
            except ValueError as exc:
                errors.append(str(exc))

    for json_path in root.rglob("*.json"):
        try:
            load_json(json_path)
        except ValueError as exc:
            errors.append(str(exc))

    for path in root.rglob("*"):
        if path.is_symlink():
            errors.append(f"Symlinks are not allowed in the portable bundle: {path.relative_to(root)}")

    if errors:
        print("\n".join(dict.fromkeys(errors)))
        return 1

    print("Bundle structure and proof-template validation passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
