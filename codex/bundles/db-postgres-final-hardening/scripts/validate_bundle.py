from __future__ import annotations

from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]

REQUIRED_FILES = [
    "README.md",
    "COPY_PASTE_PROMPT_FOR_CODEX.md",
    "inputs/raw-request.md",
    "analysis/01-branch-review.md",
    "analysis/02-db-bottleneck-and-canonicality-risk-inventory.md",
    "requirements/01-normalized-requirements.md",
    "requirements/02-canonicality-invariants.md",
    "plan/01-phase-plan.md",
    "traceability/input-coverage-matrix.md",
    "reviews/00-preparation-self-review.md",
    "scripts/audit_residue_and_bottlenecks.ps1",
    "scripts/final_validation_commands.md",
]

REQUIRED_SUBBUNDLES = [
    "SB01-merge-evidence-and-residue-cleanup",
    "SB02-conditional-finalization-for-leased-outbox-work",
    "SB03-lease-loss-hardening-and-heartbeat-contracts",
    "SB04-throughput-defaults-and-runtime-tuning",
    "SB05-benchmark-and-query-count-proof",
    "SB06-process-dispatch-claim-first-deep-proof",
    "SB07-postgresql-canonicality-invariants-and-admin-boundaries",
    "SB08-final-validation-and-merge-readiness",
]

REQUIRED_PROOF_DIRS = [
    "SB01",
    "SB02",
    "SB03",
    "SB04",
    "SB05",
    "SB06",
    "SB07",
    "SB08",
]


def require_file(relative_path: str) -> None:
    path = ROOT / relative_path
    if not path.is_file():
        raise SystemExit(f"Missing required file: {relative_path}")


def require_directory(relative_path: str) -> None:
    path = ROOT / relative_path
    if not path.is_dir():
        raise SystemExit(f"Missing required directory: {relative_path}")


def main() -> None:
    for relative_path in REQUIRED_FILES:
        require_file(relative_path)

    for subbundle in REQUIRED_SUBBUNDLES:
        require_file(f"subbundles/{subbundle}/README.md")

    for proof_dir in REQUIRED_PROOF_DIRS:
        require_directory(f"proof/{proof_dir}")
        require_file(f"proof/{proof_dir}/manifest.md")
        require_file(f"proof/{proof_dir}/semantic-invariants.md")

    print(f"Bundle validation passed: {ROOT}")


if __name__ == "__main__":
    main()
