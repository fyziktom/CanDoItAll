#!/usr/bin/env python3
"""Validate the db-process-runtime-final-hardening-v5 bundle structure."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path


BUNDLE_ROOT = Path(__file__).resolve().parents[1]

REQUIRED_ROOT_FILES = [
    "README.md",
    "bundle-manifest.json",
    "plan/01-phase-plan.md",
    "requirements/01-normalized-requirements.md",
    "requirements/02-canonicality-invariants.md",
    "traceability/input-coverage-matrix.md",
    "reviews/00-preparation-self-review.md",
    "reviews/01-expected-execution-report-outline.md",
    "scripts/audit_process_db_canonicality.ps1",
]

SUBBUNDLES = [
    "SB01-validation-evidence-and-merge-scope",
    "SB02-startup-recovery-lease-reclaim-canonicality",
    "SB03-long-running-process-dispatch-heartbeat",
    "SB04-process-outbox-idempotency-and-side-effect-canonicality",
    "SB05-postgresql-process-db-indexes-and-claim-query-plan",
    "SB06-throughput-benchmark-and-runtime-metrics",
    "SB07-process-db-red-team-tests",
    "SB08-final-merge-readiness",
]

CRITICAL_PROOF_DIRS = ["SB02", "SB03", "SB04", "SB07"]


def fail(message: str) -> None:
    print(f"Bundle validation failed: {message}", file=sys.stderr)
    raise SystemExit(1)


def require_file(path: Path) -> None:
    if not path.is_file():
        fail(f"missing file: {path.relative_to(BUNDLE_ROOT)}")


def require_dir(path: Path) -> None:
    if not path.is_dir():
        fail(f"missing directory: {path.relative_to(BUNDLE_ROOT)}")


def validate_prepared() -> None:
    for relative_path in REQUIRED_ROOT_FILES:
        require_file(BUNDLE_ROOT / relative_path)

    for subbundle in SUBBUNDLES:
        require_file(BUNDLE_ROOT / "subbundles" / subbundle / "README.md")

    for index in range(1, 9):
        proof_dir = BUNDLE_ROOT / "proof" / f"SB{index:02d}"
        require_file(proof_dir / "manifest.md")
        require_file(proof_dir / "semantic-invariants.md")


def validate_completed() -> None:
    validate_prepared()

    readme = (BUNDLE_ROOT / "README.md").read_text(encoding="utf-8")
    if "Prepared for Codex execution" in readme:
        fail("root README still says the bundle is only prepared")

    for subbundle in SUBBUNDLES:
        text = (BUNDLE_ROOT / "subbundles" / subbundle / "README.md").read_text(encoding="utf-8")
        if "Prepared" in text:
            fail(f"subbundle still appears prepared: {subbundle}")

    for proof_id in CRITICAL_PROOF_DIRS:
        proof_dir = BUNDLE_ROOT / "proof" / proof_id
        manifest = (proof_dir / "manifest.md").read_text(encoding="utf-8")
        invariants = (proof_dir / "semantic-invariants.md").read_text(encoding="utf-8")
        for forbidden in ["Pending execution", "To be filled by Codex"]:
            if forbidden in manifest or forbidden in invariants:
                fail(f"{proof_id} proof still contains placeholder text")
        require_dir(proof_dir / "transcripts")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--stage", choices=["prepared", "completed"], default="prepared")
    args = parser.parse_args()

    if args.stage == "prepared":
        validate_prepared()
    else:
        validate_completed()

    print(f"Bundle validation passed ({args.stage}): {BUNDLE_ROOT}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
