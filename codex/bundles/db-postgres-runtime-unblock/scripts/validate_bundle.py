from __future__ import annotations

import argparse
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SUBBUNDLES = [
    "SB01-rebase-scope-cleanup",
    "SB02-legacy-profile-quarantine-hardening",
    "SB03-canonical-runtime-db-pooled-factory",
    "SB04-maintenance-restart-db-activation",
    "SB05-postgresql-batch-claim-outbox",
    "SB06-process-dispatch-durable-leases",
    "SB07-background-transfer-boundaries",
    "SB08-final-validation-benchmark-gate",
]
CRITICAL = {
    "SB02-legacy-profile-quarantine-hardening",
    "SB03-canonical-runtime-db-pooled-factory",
    "SB04-maintenance-restart-db-activation",
    "SB05-postgresql-batch-claim-outbox",
    "SB06-process-dispatch-durable-leases",
    "SB08-final-validation-benchmark-gate",
}


def fail(message: str, failures: list[str]) -> None:
    failures.append(message)


def require_file(path: Path, failures: list[str]) -> None:
    if not path.is_file():
        fail(f"Missing file: {path.relative_to(ROOT)}", failures)


def require_contains(path: Path, needles: list[str], failures: list[str]) -> None:
    if not path.is_file():
        fail(f"Missing file: {path.relative_to(ROOT)}", failures)
        return

    text = path.read_text(encoding="utf-8")
    for needle in needles:
        if needle not in text:
            fail(f"{path.relative_to(ROOT)} does not contain required marker: {needle}", failures)


def validate_prepared(failures: list[str]) -> None:
    for relative in [
        "README.md",
        "bundle-manifest.json",
        "plan/01-phase-plan.md",
        "inputs/raw-request.md",
        "inputs/source-observations.md",
        "requirements/01-normalized-requirements.md",
        "requirements/02-canonicality-invariants.md",
        "traceability/input-coverage-matrix.md",
        "reviews/00-bundle-self-review.md",
        "scripts/final_validation_commands.md",
        "scripts/audit_residue_and_bottlenecks.ps1",
    ]:
        require_file(ROOT / relative, failures)

    for subbundle in SUBBUNDLES:
        require_contains(
            ROOT / "subbundles" / subbundle / "README.md",
            ["## Status", "## Acceptance Checklist", "## Proof Required", "## Progression Gate"],
            failures,
        )


def validate_completed(failures: list[str]) -> None:
    validate_prepared(failures)

    require_contains(
        ROOT / "reviews/01-execution-report.md",
        [
            "## Subbundle Gate Results",
            "## Browser Validation Analytics",
            "## Raw Note Closure",
            "## Remaining Risks",
        ],
        failures,
    )

    for subbundle in SUBBUNDLES:
        readme = ROOT / "subbundles" / subbundle / "README.md"
        require_contains(readme, ["Completed"], failures)

        manifest = ROOT / "proof" / subbundle / "manifest.md"
        require_contains(
            manifest,
            [
                "## Subbundle",
                "## Changed Files",
                "## Commands",
                "## Semantic Positive Proof",
                "## Adversarial Negative Proof",
                "## Canonicality Proof",
                "## Anti-Stub Audit",
                "## Remaining Risks",
            ],
            failures,
        )

    for subbundle in CRITICAL:
        require_contains(
            ROOT / "proof" / subbundle / "semantic-invariants.md",
            [
                "## Invariants",
                "Shallow-pass trap",
                "Adversarial negative proof",
                "Semantic positive proof",
                "Production assertions",
            ],
            failures,
        )

    for relative in [
        "proof/SB08-final-validation-benchmark-gate/changed-file-hashes.tsv",
        "proof/SB08-final-validation-benchmark-gate/fake-proof-red-team.md",
        "proof/SB08-final-validation-benchmark-gate/transcripts/dotnet-build-final.txt",
        "proof/SB08-final-validation-benchmark-gate/transcripts/dotnet-test-unit-full.txt",
        "proof/SB08-final-validation-benchmark-gate/transcripts/dotnet-test-integration-focused.txt",
        "proof/SB08-final-validation-benchmark-gate/transcripts/dotnet-ef-has-pending-model-changes.txt",
        "proof/SB08-final-validation-benchmark-gate/transcripts/audit-residue-and-bottlenecks.txt",
        "proof/SB08-final-validation-benchmark-gate/transcripts/source-assertions.txt",
        "proof/SB08-final-validation-benchmark-gate/transcripts/anti-stub-audit.txt",
        "proof/SB04-maintenance-restart-db-activation/transcripts/dotnet-test-playwright-database-switch.txt",
        "proof/SB04-maintenance-restart-db-activation/browser/db-switch-stale-artifact-recovery-desktop.png",
        "proof/SB04-maintenance-restart-db-activation/browser/db-switch-cross-tab-desktop.png",
        "proof/SB04-maintenance-restart-db-activation/browser/db-switch-stale-artifact-responsive.png",
    ]:
        require_file(ROOT / relative, failures)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--stage", choices=["prepared", "completed"], required=True)
    args = parser.parse_args()

    failures: list[str] = []
    if args.stage == "prepared":
        validate_prepared(failures)
    else:
        validate_completed(failures)

    if failures:
        for failure in failures:
            print(f"FAIL: {failure}")
        return 1

    print(f"PASS: bundle {args.stage} validation succeeded for {ROOT}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
